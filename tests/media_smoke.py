"""Exercise the application's format arguments with yt-dlp and FFmpeg locally.

Uses short generated clips and saved extractor metadata served on loopback;
requires dotnet, yt-dlp, ffmpeg and ffprobe. No YouTube access is needed.
"""

import argparse
import functools
import http.server
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
import unittest


class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass


def command(arguments, timeout=120):
    result = subprocess.run(arguments, capture_output=True, text=True, timeout=timeout)
    if result.returncode:
        raise AssertionError(f"Command failed ({result.returncode}): {arguments}\n{result.stdout}\n{result.stderr}")
    return result.stdout


class MediaSmokeTests(unittest.TestCase):
    argument_helper: Path

    @classmethod
    def setUpClass(cls):
        cls.dotnet = shutil.which("dotnet")
        cls.ytdlp = os.environ.get("YT_DLP") or shutil.which("yt-dlp")
        cls.ffmpeg = shutil.which("ffmpeg")
        cls.ffprobe = shutil.which("ffprobe")
        if not all((cls.dotnet, cls.ytdlp, cls.ffmpeg, cls.ffprobe)):
            raise RuntimeError("Required tools: dotnet, yt-dlp (or YT_DLP), ffmpeg and ffprobe.")
        if not cls.argument_helper.is_file():
            raise RuntimeError(f"Build the test argument helper first: {cls.argument_helper}")

        cls.temp = tempfile.TemporaryDirectory(prefix="ytd-media-")
        cls.addClassCleanup(cls.temp.cleanup)
        cls.root = Path(cls.temp.name)
        cls.clips = cls.root / "clips"
        cls.clips.mkdir()

        def generate(name, *options):
            command([cls.ffmpeg, "-nostdin", "-y", "-loglevel", "error", *options,
                     "-threads", "2", str(cls.clips / name)])

        generate("mp4-video.mp4", "-f", "lavfi", "-i", "color=c=blue:s=1280x720:r=5:d=0.4",
                 "-an", "-c:v", "libx264", "-preset", "ultrafast")
        for height, width in ((360, 640), (240, 426)):
            generate(f"webm-{height}.webm", "-f", "lavfi", "-i", f"color=c=red:s={width}x{height}:r=5:d=0.4",
                     "-an", "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8")
        generate("aac.m4a", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=0.4",
                 "-vn", "-c:a", "aac", "-b:a", "128k")
        generate("opus.webm", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=0.4",
                 "-vn", "-c:a", "libopus", "-b:a", "96k")
        generate("combined.mp4", "-i", str(cls.clips / "mp4-video.mp4"),
                 "-i", str(cls.clips / "aac.m4a"), "-c", "copy")
        for height in (360, 240):
            generate(f"combined-{height}.webm", "-i", str(cls.clips / f"webm-{height}.webm"),
                     "-i", str(cls.clips / "opus.webm"), "-c", "copy")

        cls.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0),
            functools.partial(QuietHandler, directory=str(cls.clips)))
        cls.addClassCleanup(cls.server.server_close)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.addClassCleanup(cls.thread.join, 5)
        cls.addClassCleanup(cls.server.shutdown)
        cls.base_url = f"http://127.0.0.1:{cls.server.server_port}"
        cls.formats = [
            cls.stream("mp4-video", "mp4-video.mp4", "avc1.42c01f", "none", 720, 1280),
            cls.stream("webm-360", "webm-360.webm", "vp9", "none", 360, 640),
            cls.stream("webm-240", "webm-240.webm", "vp9", "none", 240, 426),
            cls.stream("aac", "aac.m4a", "none", "mp4a.40.2"),
            cls.stream("opus", "opus.webm", "none", "opus"),
        ]

    @classmethod
    def stream(cls, format_id, filename, vcodec, acodec, height=None, width=None):
        result = {
            "format_id": format_id, "url": f"{cls.base_url}/{filename}",
            "ext": Path(filename).suffix[1:], "protocol": "http", "vcodec": vcodec,
            "acodec": acodec, "filesize": (cls.clips / filename).stat().st_size,
        }
        if height:
            result.update(height=height, width=width, fps=5)
        if acodec != "none":
            result.update(abr=128, asr=48000, audio_channels=1)
        return result

    def download(self, container, height="best", formats=None, expect_failure=False):
        output = self.root / "downloads & spaces" / self._testMethodName
        output.mkdir(parents=True)
        metadata = output / "info.json"
        metadata.write_text(json.dumps({
            "id": "fixture", "title": "fixture", "extractor": "generic", "extractor_key": "Generic",
            "webpage_url": self.base_url, "formats": self.formats if formats is None else formats,
        }), encoding="utf-8")
        options = json.loads(command([self.dotnet, str(self.argument_helper), "--media-arguments", container, str(height)]))
        result = subprocess.run([
            self.ytdlp, "--ignore-config", "--no-cache-dir", "--proxy", "", "--no-playlist",
            "--ffmpeg-location", self.ffmpeg, "--load-info-json", str(metadata),
            "-o", str(output / "%(title)s.%(ext)s"), *options,
        ], capture_output=True, text=True, timeout=120)
        if expect_failure:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Requested format is not available", result.stderr)
            self.assertEqual(list(output.glob("fixture.*")), [])
            return None
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        media = list(output.glob("fixture.*"))
        self.assertEqual(len(media), 1, str(media))
        self.assertEqual(media[0].suffix, "." + container)
        return json.loads(command([self.ffprobe, "-v", "error", "-show_streams", "-show_format",
                                   "-of", "json", str(media[0])]))

    def assert_tracks(self, result, video, audio, height):
        videos = [stream for stream in result["streams"] if stream["codec_type"] == "video"]
        audios = [stream for stream in result["streams"] if stream["codec_type"] == "audio"]
        self.assertEqual(len(videos), 1)
        self.assertEqual(len(audios), 1)
        self.assertEqual(videos[0]["codec_name"], video)
        self.assertEqual(videos[0]["height"], height)
        self.assertEqual(audios[0]["codec_name"], audio)

    def test_webm_excludes_higher_resolution_h264_and_aac(self):
        self.assert_tracks(self.download("webm"), "vp9", "opus", 360)

    def test_webm_quality_limit_applies_to_separate_video(self):
        self.assert_tracks(self.download("webm", 240), "vp9", "opus", 240)

    def test_webm_quality_limit_applies_to_combined_fallback(self):
        formats = [self.stream(f"combined-{height}", f"combined-{height}.webm", "vp9", "opus", height, width)
                   for height, width in ((360, 640), (240, 426))]
        self.assert_tracks(self.download("webm", 240, formats), "vp9", "opus", 240)

    def test_webm_without_compatible_streams_fails_before_downloading(self):
        formats = [fmt for fmt in self.formats if fmt["ext"] in ("mp4", "m4a")]
        self.download("webm", formats=formats, expect_failure=True)

    def test_mp4_selects_mp4_video_and_aac_audio(self):
        self.assert_tracks(self.download("mp4"), "h264", "aac", 720)

    def test_mkv_can_merge_streams_from_different_containers(self):
        formats = [fmt for fmt in self.formats if fmt["format_id"] in ("mp4-video", "opus")]
        self.assert_tracks(self.download("mkv", formats=formats), "h264", "opus", 720)

    def test_mkv_remuxes_a_single_combined_mp4(self):
        formats = [self.stream("combined", "combined.mp4", "avc1.42c01f", "mp4a.40.2", 720, 1280)]
        self.assert_tracks(self.download("mkv", formats=formats), "h264", "aac", 720)

    def test_audio_only_is_converted_to_mp3(self):
        result = self.download("mp3")
        self.assertEqual([stream["codec_type"] for stream in result["streams"]], ["audio"])
        self.assertEqual(result["streams"][0]["codec_name"], "mp3")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--argument-helper", type=Path, required=True,
                        help="Path to the built YouTubeDownloader.Tests.dll")
    args = parser.parse_args()
    MediaSmokeTests.argument_helper = args.argument_helper.resolve()
    unittest.main(argv=[__file__], verbosity=2)

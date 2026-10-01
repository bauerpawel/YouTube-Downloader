# dmgbuild settings for the macOS installer window: the app on the left, an
# arrow, the Applications folder on the right - drag to install.
# macos/package.sh passes -D app=<path to the .app> -D icon=<path to the .icns>.
import os.path

app = defines["app"]  # noqa: F821 - dmgbuild provides `defines`

format = "UDZO"
files = [app]
symlinks = {"Applications": "/Applications"}
icon = defines["icon"]  # noqa: F821 - the mounted volume's icon

# builtin-arrow is 640x240 (with a retina version), arrow centred at about
# (330, 116); the icons sit on either side of it.
background = "builtin-arrow"
window_rect = ((200, 200), (640, 280))
icon_size = 128
text_size = 13
icon_locations = {
    os.path.basename(app): (160, 116),
    "Applications": (500, 116),
}

default_view = "icon-view"
show_status_bar = False
show_tab_view = False
show_toolbar = False
show_pathbar = False
show_sidebar = False

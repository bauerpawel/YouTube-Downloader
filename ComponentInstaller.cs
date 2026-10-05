namespace YouTubeDownloader;

// Preparation and executable checks never touch the installed component. The
// component and its version marker are committed together, with rollback on error.
internal static class ComponentInstaller
{
    public static Task InstallFileAsync(string destination, Func<string, Task> prepare,
        Func<string, Task<bool>> verify, string? versionPath = null, string? version = null) =>
        InstallAsync(destination, false, prepare, verify, versionPath, version);

    public static Task InstallDirectoryAsync(string destination, Func<string, Task> prepare,
        Func<string, Task<bool>> verify, string? versionPath = null, string? version = null) =>
        InstallAsync(destination, true, prepare, verify, versionPath, version);

    private static async Task InstallAsync(string destination, bool directory,
        Func<string, Task> prepare, Func<string, Task<bool>> verify, string? versionPath, string? version)
    {
        string suffix = ".staging-" + Guid.NewGuid().ToString("N");
        var changes = new List<Change> { new(destination + suffix, destination, directory) };
        if (versionPath != null)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidDataException("A component version is required.");
            changes.Add(new(versionPath + suffix, versionPath, false));
        }

        bool committed = false;
        try
        {
            if (directory)
                Directory.CreateDirectory(changes[0].Source);
            await prepare(changes[0].Source);
            if (!await verify(changes[0].Source))
                throw new InvalidDataException("The downloaded component failed its version check.");
            if (versionPath != null)
                await File.WriteAllTextAsync(changes[1].Source, version);

            foreach (var change in changes)
            {
                if (change.Exists(change.Target))
                {
                    change.Move(change.Target, change.Backup);
                    change.BackedUp = true;
                }
                change.Move(change.Source, change.Target);
                change.Installed = true;
            }
            committed = true;
        }
        catch (Exception error)
        {
            var restoreErrors = new List<Exception>();
            foreach (var change in changes.AsEnumerable().Reverse())
            {
                try
                {
                    if (change.Installed)
                        change.Delete(change.Target);
                    if (change.BackedUp)
                        change.Move(change.Backup, change.Target);
                }
                catch (Exception restoreError)
                {
                    // Do not remove the backup if automatic recovery failed.
                    restoreErrors.Add(restoreError);
                }
            }
            if (restoreErrors.Count != 0)
                throw new AggregateException("Component rollback failed; its backup was retained.", new[] { error }.Concat(restoreErrors));
            throw;
        }
        finally
        {
            foreach (var change in changes)
            {
                change.TryDelete(change.Source);
                if (committed)
                    change.TryDelete(change.Backup);
            }
        }
    }

    private sealed class Change(string source, string target, bool directory)
    {
        public string Source { get; } = source;
        public string Target { get; } = target;
        public string Backup { get; } = source + ".previous";
        public bool BackedUp { get; set; }
        public bool Installed { get; set; }
        public bool Exists(string path) => directory ? Directory.Exists(path) : File.Exists(path);
        public void Move(string from, string to)
        {
            if (directory) Directory.Move(from, to);
            else File.Move(from, to);
        }
        public void Delete(string path)
        {
            if (directory) Directory.Delete(path, true);
            else File.Delete(path);
        }
        public void TryDelete(string path)
        {
            if (directory) AppPaths.TryDeleteDirectory(path);
            else AppPaths.TryDeleteFile(path);
        }
    }
}

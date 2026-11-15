namespace Noga.Checking;

/// <summary>
/// A folder's files checked as one set (<c>noga check FOLDER</c>; docs/decisions/2026-11-15-set-check.md): each of the
/// folder's own media files through <see cref="Checker.Run"/> (its reports written as for one file), then the set's
/// report (<see cref="SetReport"/>) as FOLDERNAME.set.qc.html and FOLDERNAME.set.qc.json with them.
/// </summary>
public static class SetChecker
{
    /// <summary>
    /// Checks <paramref name="folder"/>'s media files (not its subfolders') by <paramref name="profile"/> (ebu-r128
    /// when null), the reports into <paramref name="reports"/> (else the folder itself), telling <paramref name="checkedFile"/>
    /// of each file as it is done. A file that can't be read is in the report as such; the others are still checked.
    /// </summary>
    /// <exception cref="CheckException">The folder isn't there or has no media files.</exception>
    public static SetResult Run(string folder, string? reports, Profile? profile = null, CancellationToken cancellationToken = default, Action<SetFile>? checkedFile = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!Directory.Exists(folder))
            throw new CheckException($"no such folder: {folder}");
        profile ??= Profile.EbuR128;
        folder = Path.GetFullPath(folder);
        reports ??= folder;
        var paths = MediaFiles.Directly(folder).ToList();
        if (paths.Count == 0)
            throw new CheckException($"no media files in {folder}");

        var files = new List<SetFile>();
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetFile file;
            try
            {
                file = new SetFile(Path.GetFileName(path), Checker.Run(path, reports, profile, cancellationToken).Report);
            }
            catch (CheckException e)
            {
                file = new SetFile(Path.GetFileName(path), null, e.Message);
            }

            files.Add(file);
            checkedFile?.Invoke(file);
        }

        var report = SetReport.Evaluate(profile, folder, files);
        string name = Name(folder);
        Directory.CreateDirectory(reports);
        string html = Path.Combine(reports, name + ".set.qc.html"), json = Path.Combine(reports, name + ".set.qc.json");
        File.WriteAllText(html, SetHtml.Write(report, name));
        File.WriteAllText(json, SetJson.Write(report));
        return new SetResult(report, html, json);
    }

    // The folder's name; a drive's or the root's has none.
    static string Name(string folder) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)) is { Length: > 0 } name ? name : "folder";
}

/// <summary>A set's report and where its HTML and JSON went.</summary>
public sealed record SetResult(SetReport Report, string Html, string Json);

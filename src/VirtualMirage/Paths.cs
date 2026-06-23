namespace VirtualMirage;

/// <summary>Well-known on-disk locations under %AppData%\VirtualMirage.</summary>
public static class Paths
{
    public static string AppDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VirtualMirage");

    public static string LogsDir { get; } = Path.Combine(AppDir, "logs");
    public static string ConfigPath { get; } = Path.Combine(AppDir, "config.json");

    /// <summary>Crash-recovery snapshot written while a session is active.</summary>
    public static string StatePath { get; } = Path.Combine(AppDir, "session.state.json");

    /// <summary>User-saved "normal" desktop layout, re-applied on disconnect (keep-others mode).</summary>
    public static string NonVrLayoutPath { get; } = Path.Combine(AppDir, "layout.nonvr.json");

    /// <summary>User-saved "VR" layout (e.g. virtual display duplicated to one monitor), re-applied on connect.</summary>
    public static string VrLayoutPath { get; } = Path.Combine(AppDir, "layout.vr.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(LogsDir);
    }

    /// <summary>
    /// One-time migration from the app's former name (AutoVRVD): if %AppData%\AutoVRVD exists and
    /// the new %AppData%\VirtualMirage folder doesn't yet, copy it over so the user keeps their
    /// config.json (including the stable MonitorGuid), logs, and any session state. Best-effort —
    /// failures are swallowed so a bad copy can never block startup.
    /// </summary>
    public static void MigrateLegacyDataIfNeeded()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string legacy = Path.Combine(appData, "AutoVRVD");
            if (Directory.Exists(legacy) && !Directory.Exists(AppDir))
                CopyDirectory(legacy, AppDir);
        }
        catch { /* best-effort; never block startup on migration */ }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(src, dst));
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(src, dst), overwrite: true);
    }
}

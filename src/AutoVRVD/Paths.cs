namespace AutoVRVD;

/// <summary>Well-known on-disk locations under %AppData%\AutoVRVD.</summary>
public static class Paths
{
    public static string AppDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoVRVD");

    public static string LogsDir { get; } = Path.Combine(AppDir, "logs");
    public static string ConfigPath { get; } = Path.Combine(AppDir, "config.json");

    /// <summary>Crash-recovery snapshot written while a session is active.</summary>
    public static string StatePath { get; } = Path.Combine(AppDir, "session.state.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(LogsDir);
    }
}

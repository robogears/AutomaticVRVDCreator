using System.Diagnostics;
using VirtualMirage.UI;

namespace VirtualMirage.Update;

/// <summary>
/// Drives the update flow through a state machine (idle/check -> available -> downloading% ->
/// ready -> restarting). Status shows in two places: the tray "update" menu item (at-a-glance),
/// and a persistent <see cref="UpdateForm"/> window the user opens from that item — so a manual
/// check stays visible instead of vanishing when the tray menu closes.
/// </summary>
public sealed class UpdateController
{
    private enum St { Idle, Checking, Available, Downloading, Ready, Restarting }

    private readonly TrayApp _tray;
    private readonly object _gate = new();
    private St _state = St.Idle;
    private UpdateCheckResult? _available;
    private string? _downloadedPath;
    private UpdateForm? _form;

    public UpdateController(TrayApp tray)
    {
        _tray = tray;
        _tray.UpdateMenuClicked += OpenWindow;
    }

    /// <summary>Silent check on launch; only surfaces a tray notice if an update is available.</summary>
    public async Task StartLaunchCheckAsync()
    {
        var r = await Updater.CheckAsync();
        lock (_gate)
        {
            if (r.Status == UpdateStatus.Available)
            {
                _available = r;
                _state = St.Available;
                _tray.SetUpdateMenu($"Update available: {r.Version} — Download", true);
                _tray.Notify("VirtualMirage", $"Update available: {r.Version}. Open the tray menu to download.");
            }
            else
            {
                _state = St.Idle;
                _tray.SetUpdateMenu("Check for updates", true);
            }
        }
    }

    /// <summary>
    /// Tray "update" item -> open the persistent Updates window. Tray menu clicks arrive on the UI
    /// thread, so we create/show the form directly here. When idle, kick off a fresh check; otherwise
    /// reflect the current state into the window.
    /// </summary>
    private void OpenWindow()
    {
        try
        {
            if (_form is null || _form.IsDisposed)
            {
                _form = new UpdateForm();
                _form.PrimaryActionRequested += () => _ = OnClickAsync();
            }
            _form.Show();
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            _form.BringToFront();
            _form.Activate();

            St s;
            lock (_gate) s = _state;
            switch (s)
            {
                case St.Idle: _ = OnClickAsync(); break; // start a fresh check
                case St.Checking: _form.ShowChecking(); break;
                case St.Available: _form.ShowAvailable(_available?.Version ?? "?", Updater.CurrentVersion()); break;
                case St.Downloading: _form.ShowDownloading(-1); break;
                case St.Ready: _form.ShowReady(_available?.Version ?? ""); break;
                case St.Restarting: _form.ShowInstalling(); break;
            }
        }
        catch (Exception ex) { Log.Error("open update window failed", ex); }
    }

    private async Task OnClickAsync()
    {
        St s;
        lock (_gate) s = _state;
        try
        {
            switch (s)
            {
                case St.Idle: await DoCheckAsync(); break;
                case St.Available: await DoDownloadAsync(); break;
                case St.Ready: DoApply(); break;
                default: break; // checking / downloading / restarting: ignore clicks
            }
        }
        catch (Exception ex) { Log.Error("update click handler failed", ex); }
    }

    private async Task DoCheckAsync()
    {
        lock (_gate) _state = St.Checking;
        _tray.SetUpdateMenu("Checking for updates…", false);
        _form?.ShowChecking();

        var r = await Updater.CheckAsync();
        string current = Updater.CurrentVersion();
        lock (_gate)
        {
            switch (r.Status)
            {
                case UpdateStatus.Available:
                    _available = r; _state = St.Available;
                    _tray.SetUpdateMenu($"Update available: {r.Version} — Download", true);
                    _form?.ShowAvailable(r.Version ?? "?", current);
                    break;
                case UpdateStatus.UpToDate:
                    _state = St.Idle;
                    _tray.SetUpdateMenu("Up to date ✓", true);
                    _form?.ShowUpToDate(current);
                    RevertIdleLater();
                    break;
                default:
                    _state = St.Idle;
                    _tray.SetUpdateMenu("Update check failed", true);
                    _form?.ShowFailed("Couldn't check for updates", r.Message ?? "");
                    RevertIdleLater();
                    break;
            }
        }
    }

    private async Task DoDownloadAsync()
    {
        UpdateCheckResult? r;
        lock (_gate) r = _available;
        if (r?.DownloadUrl is null) return;

        // No self-install (non-Windows) or the asset wasn't an .exe -> open the release page instead.
        if (!Updater.CanSelfInstall() || !r.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try { Process.Start(new ProcessStartInfo { FileName = r.ReleaseUrl ?? r.DownloadUrl, UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("open release page failed", ex); }
            return;
        }

        lock (_gate) _state = St.Downloading;
        _tray.SetUpdateMenu("Starting download…", false);
        _form?.ShowDownloading(-1);

        var progress = new Progress<(long d, long t)>(p =>
        {
            // Ignore late progress once we've left the Downloading state — otherwise a trailing
            // "100%" callback can overwrite the "Restart to apply" text after the download finished.
            lock (_gate) { if (_state != St.Downloading) return; }
            int pct = p.t > 0 ? (int)(p.d * 100 / p.t) : -1;
            _tray.SetUpdateMenu(pct >= 0 ? $"Downloading {pct}%" : $"Downloading {p.d / 1024 / 1024} MB", false);
            _form?.ShowDownloading(pct);
        });

        // Run the download on a worker thread so the 68 MB transfer never freezes the UI thread.
        string? path = await Task.Run(() => Updater.DownloadAsync(r.DownloadUrl, progress));
        lock (_gate)
        {
            if (path is null)
            {
                _state = St.Available;
                _tray.SetUpdateMenu("Download failed — retry", true);
                _form?.ShowFailed("Download failed", "The update download didn't complete. Please try again.");
            }
            else
            {
                _downloadedPath = path;
                _state = St.Ready;
                _tray.SetUpdateMenu("Restart to apply update", true);
                _form?.ShowReady(r.Version ?? "");
            }
        }
    }

    private void DoApply()
    {
        string? path;
        lock (_gate)
        {
            if (_downloadedPath is null) return;
            path = _downloadedPath;
            _state = St.Restarting;
        }
        _tray.SetUpdateMenu("Installing update…", false);
        _form?.ShowInstalling();
        if (Updater.ApplyUpdate(path))
        {
            Application.Exit(); // the installer closes us, installs, and relaunches
        }
        else
        {
            // Installer didn't launch (UAC declined?) — stay ready so the user can retry.
            lock (_gate) _state = St.Ready;
            _tray.SetUpdateMenu("Restart to apply update", true);
            _form?.ShowReady(_available?.Version ?? "");
        }
    }

    private void RevertIdleLater()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            lock (_gate)
            {
                if (_state == St.Idle) _tray.SetUpdateMenu("Check for updates", true);
            }
        });
    }
}

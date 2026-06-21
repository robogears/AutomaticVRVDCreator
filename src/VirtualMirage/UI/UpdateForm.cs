using System.Drawing;

namespace VirtualMirage.UI;

/// <summary>
/// A small persistent (modeless) "Updates" window. The tray context menu closes the moment it's
/// clicked, which used to hide the whole check/download/install sequence — this window keeps that
/// progress and the result visible until the user closes it. Driven by <see cref="Update.UpdateController"/>:
/// it calls the Show* state methods, and raises <see cref="PrimaryActionRequested"/> for its action button.
/// </summary>
public sealed class UpdateForm : Form
{
    private readonly Label _heading = new() { AutoSize = false };
    private readonly Label _detail = new() { AutoSize = false };
    private readonly ProgressBar _progress = new() { Visible = false };
    private readonly Button _action = new() { Visible = false };
    private readonly Button _close = new() { Text = "Close" };

    /// <summary>Raised when the user clicks the primary action button (check / download / install / retry).</summary>
    public event Action? PrimaryActionRequested;

    public UpdateForm()
    {
        Text = "VirtualMirage Updates";
        Icon = IconArt.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(430, 196);

        _heading.SetBounds(18, 18, 394, 28);
        _heading.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);

        _detail.SetBounds(18, 50, 394, 56);

        _progress.SetBounds(18, 112, 394, 18);

        _action.SetBounds(214, 148, 144, 34);
        _action.Click += (_, _) => PrimaryActionRequested?.Invoke();

        _close.SetBounds(366, 148, 46, 34);
        _close.Click += (_, _) => Hide();

        Controls.AddRange(new Control[] { _heading, _detail, _progress, _action, _close });

        // Closing via the X just hides the window — the app keeps running in the tray.
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
    }

    private void Ui(Action a)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(a); } catch { } return; }
        a();
    }

    public void ShowChecking() => Ui(() =>
        Render("Checking for updates…", "Contacting GitHub for the latest VirtualMirage release.", marquee: true));

    public void ShowUpToDate(string current) => Ui(() =>
    {
        Render("You're up to date", $"VirtualMirage {current} is the latest version.");
        SetAction("Check again", true);
    });

    public void ShowAvailable(string version, string current) => Ui(() =>
    {
        Render("Update available", $"VirtualMirage {version} is available — you have {current}.");
        SetAction("Download && install", true);
    });

    public void ShowDownloading(int percent) => Ui(() =>
    {
        if (percent < 0)
            Render("Downloading update…", "Starting download…", marquee: true);
        else
            Render("Downloading update…", $"{percent}% of ~68 MB — this takes a moment.", progress: percent);
        // action stays hidden while the transfer runs
    });

    public void ShowReady(string version) => Ui(() =>
    {
        Render("Update ready to install",
            $"VirtualMirage {version} downloaded. It will close, install the update, and reopen (one UAC prompt).");
        SetAction("Restart && install", true);
    });

    public void ShowInstalling() => Ui(() =>
        Render("Installing update…", "VirtualMirage is restarting to apply the update.", marquee: true));

    public void ShowFailed(string heading, string detail) => Ui(() =>
    {
        Render(heading, string.IsNullOrWhiteSpace(detail) ? "Please try again." : detail);
        SetAction("Retry", true);
    });

    private void Render(string heading, string detail, bool marquee = false, int progress = -1)
    {
        _heading.Text = heading;
        _detail.Text = detail;
        _action.Visible = false; // SetAction re-shows it when an action is relevant

        if (marquee)
        {
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.Visible = true;
        }
        else if (progress >= 0)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = Math.Min(100, Math.Max(0, progress));
            _progress.Visible = true;
        }
        else
        {
            _progress.Visible = false;
        }
    }

    private void SetAction(string text, bool enabled)
    {
        _action.Text = text;
        _action.Enabled = enabled;
        _action.Visible = true;
    }
}

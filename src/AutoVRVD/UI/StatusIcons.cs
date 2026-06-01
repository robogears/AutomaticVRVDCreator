using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AutoVRVD.UI;

public enum AppStatus { Idle, Disabled, Connected, Active, Error }

/// <summary>Runtime-generated colored-dot tray icons so we don't have to ship an .ico set.</summary>
public static class StatusIcons
{
    private static readonly Dictionary<AppStatus, Icon> _cache = Build();

    public static Icon For(AppStatus s) => _cache.TryGetValue(s, out var i) ? i : _cache[AppStatus.Idle];

    private static Dictionary<AppStatus, Icon> Build() => new()
    {
        [AppStatus.Idle]      = Dot(Color.FromArgb(160, 160, 160)),
        [AppStatus.Disabled]  = Dot(Color.FromArgb(90, 90, 90)),
        [AppStatus.Connected] = Dot(Color.FromArgb(60, 140, 235)),
        [AppStatus.Active]    = Dot(Color.FromArgb(55, 200, 90)),
        [AppStatus.Error]     = Dot(Color.FromArgb(225, 70, 60)),
    };

    private static Icon Dot(Color c)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var br = new SolidBrush(c);
            g.FillEllipse(br, 5, 5, 22, 22);
            using var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 2f);
            g.DrawEllipse(pen, 5, 5, 22, 22);
        }

        IntPtr h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally { DestroyIcon(h); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

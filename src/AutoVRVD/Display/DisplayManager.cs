using System.Runtime.InteropServices;
using AutoVRVD.Interop;
using AutoVRVD.VirtualDisplay;

namespace AutoVRVD.Display;

/// <summary>
/// Layer B: display topology operations. Capture/restore the whole topology via
/// the CCD API; set mode / primary / disable-others via the simpler legacy GDI API
/// (ChangeDisplaySettingsEx) — the same split Apollo uses.
/// </summary>
public sealed class DisplayManager
{
    // ---- Capture ----

    public DisplaySnapshot? Capture(Guid virtualGuid)
    {
        uint extra = CcdInterop.QDC_VIRTUAL_MODE_AWARE | CcdInterop.QDC_VIRTUAL_REFRESH_RATE_AWARE;
        if (!CcdInterop.QueryActive(extra, out var paths, out var modes))
        {
            extra = 0;
            if (!CcdInterop.QueryActive(extra, out paths, out modes))
            {
                Log.Error("Capture: QueryDisplayConfig failed.");
                return null;
            }
        }

        string? primary = FindPrimaryGdiName();
        var active = ListActiveGdiNames();
        var snap = DisplaySnapshot.Build(extra, paths, modes, virtualGuid, primary, active);
        Log.Info($"Captured topology: {paths.Length} paths, {modes.Length} modes, primary={primary}, active=[{string.Join(",", active)}].");
        return snap;
    }

    // ---- Apply (make the virtual display the show) ----

    /// <summary>
    /// Make the virtual display the show. Primary mechanism is the CCD API
    /// (atomic, reliable on Win11 24H2 where legacy ChangeDisplaySettingsEx
    /// silently no-ops); the legacy GDI path is kept only as a fallback.
    /// </summary>
    public bool Apply(VirtualDisplayHandle vd, ResolutionConfig res, bool setPrimary, bool disableOthers)
    {
        bool ok;
        if (disableOthers)
        {
            // Activates the virtual target (works whether it starts active or inactive) AND
            // makes it the sole display => primary, in one atomic CCD call.
            ok = ApplyExclusive(vd);
            if (!ok)
            {
                Log.Warn("ApplyExclusive (CCD) failed; falling back to legacy primary+disable.");
                string? gn = ResolveName(vd);
                if (!string.IsNullOrEmpty(gn)) { if (setPrimary) SetPrimary(gn); ok = DisableAllExcept(gn); }
            }
        }
        else if (setPrimary)
        {
            CcdInterop.ExtendAllDisplays(); // ensure the virtual is active alongside the others
            Thread.Sleep(200);
            ok = ApplyPrimaryKeepOthers(vd);
            if (!ok)
            {
                string? gn = ResolveName(vd);
                if (!string.IsNullOrEmpty(gn)) ok = SetPrimary(gn);
            }
        }
        else
        {
            ok = CcdInterop.ExtendAllDisplays(); // just make it visible
        }

        // Now that the display is active, resolve its name and enforce the exact requested mode.
        string? name = ResolveName(vd);
        if (!string.IsNullOrEmpty(name)) SetMode(name, (uint)res.Width, (uint)res.Height, (uint)res.RefreshHz);

        Log.Info($"Apply complete (setPrimary={setPrimary}, disableOthers={disableOthers}, ok={ok}, name={name ?? "?"}). Topology now:\n{GdiInterop.DescribeAll()}");
        return ok;
    }

    /// <summary>Resolve the virtual display's \\.\DISPLAYn name (active paths), waiting briefly for attach.</summary>
    private static string? ResolveName(VirtualDisplayHandle vd)
    {
        for (int i = 0; i < 8; i++)
        {
            var n = CcdInterop.FindGdiNameForTarget(vd.AdapterLuid, vd.TargetId);
            if (!string.IsNullOrEmpty(n)) return n;
            Thread.Sleep(120);
        }
        return string.IsNullOrEmpty(vd.GdiName) ? null : vd.GdiName;
    }

    /// <summary>
    /// CCD: supply a single active path (the virtual target) with OS-chosen modes. Because it's the
    /// only supplied path it becomes the sole display => primary at (0,0). Uses QueryAll so it works
    /// even when the hot-added display came up inactive (Windows remembered it off).
    /// </summary>
    private bool ApplyExclusive(VirtualDisplayHandle vd)
    {
        if (!CcdInterop.QueryAll(out var paths, out _))
        {
            Log.Error("ApplyExclusive: QueryAll failed.");
            return false;
        }

        int vp = FindVirtualPath(paths, vd);
        if (vp < 0) { Log.Error($"ApplyExclusive: virtual path not found among all paths (target {vd.TargetId})."); return false; }

        var vpath = paths[vp];
        vpath.flags |= CcdInterop.DISPLAYCONFIG_PATH_ACTIVE;
        vpath.sourceInfo.modeInfoIdx = CcdInterop.DISPLAYCONFIG_PATH_MODE_IDX_INVALID; // let OS pick the mode
        vpath.targetInfo.modeInfoIdx = CcdInterop.DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        var newPaths = new[] { vpath };

        uint baseFlags = CcdInterop.SDC_APPLY | CcdInterop.SDC_USE_SUPPLIED_DISPLAY_CONFIG | CcdInterop.SDC_ALLOW_CHANGES;
        int r = CcdInterop.SetDisplayConfig(1, newPaths, 0, null, baseFlags | CcdInterop.SDC_SAVE_TO_DATABASE);
        if (r != CcdInterop.ERROR_SUCCESS)
        {
            Log.Warn($"ApplyExclusive: apply+save err={r}; retrying without SAVE_TO_DATABASE.");
            r = CcdInterop.SetDisplayConfig(1, newPaths, 0, null, baseFlags);
        }
        Log.Info($"ApplyExclusive: activate-only-virtual (supplied single path) -> {r}.");
        return r == CcdInterop.ERROR_SUCCESS;
    }

    /// <summary>CCD: keep all displays active but shift positions so the virtual display sits at (0,0) (primary).</summary>
    private bool ApplyPrimaryKeepOthers(VirtualDisplayHandle vd)
    {
        if (!CcdInterop.QueryActive(0, out var paths, out var modes))
        {
            Log.Error("ApplyPrimaryKeepOthers: QueryDisplayConfig failed.");
            return false;
        }

        int vp = FindVirtualPath(paths, vd);
        if (vp < 0) { Log.Error("ApplyPrimaryKeepOthers: virtual path not found."); return false; }

        uint sIdx = paths[vp].sourceInfo.modeInfoIdx;
        if (sIdx == CcdInterop.DISPLAYCONFIG_PATH_MODE_IDX_INVALID || sIdx >= modes.Length) return false;

        int offX = modes[sIdx].mode.sourceMode.position.x;
        int offY = modes[sIdx].mode.sourceMode.position.y;

        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].infoType != CcdInterop.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE) continue;
            modes[i].mode.sourceMode.position.x -= offX;
            modes[i].mode.sourceMode.position.y -= offY;
        }

        uint flags = CcdInterop.SDC_APPLY | CcdInterop.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                   | CcdInterop.SDC_ALLOW_CHANGES | CcdInterop.SDC_SAVE_TO_DATABASE;
        int r = CcdInterop.SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, flags);
        Log.Info($"ApplyPrimaryKeepOthers: SetDisplayConfig -> {r}.");
        return r == CcdInterop.ERROR_SUCCESS;
    }

    private static int FindVirtualPath(CcdInterop.DISPLAYCONFIG_PATH_INFO[] paths, VirtualDisplayHandle vd)
    {
        for (int i = 0; i < paths.Length; i++)
            if (paths[i].targetInfo.adapterId.SameAs(vd.AdapterLuid) && paths[i].targetInfo.id == vd.TargetId)
                return i;

        // fallback: match by source GDI name
        if (!string.IsNullOrEmpty(vd.GdiName))
            for (int i = 0; i < paths.Length; i++)
            {
                var name = CcdInterop.GetSourceGdiName(paths[i].sourceInfo.adapterId, paths[i].sourceInfo.id);
                if (string.Equals(name, vd.GdiName, StringComparison.OrdinalIgnoreCase)) return i;
            }
        return -1;
    }

    // ---- Mode enforcement (counters Virtual Desktop overriding the resolution after connect) ----

    /// <summary>
    /// If the virtual display drifted from the requested mode (e.g. Virtual Desktop applied its own
    /// stream resolution after connecting), force it back. No-op (no flicker) when already correct.
    /// </summary>
    public bool EnforceMode(VirtualDisplayHandle vd, ResolutionConfig res)
    {
        string? name = CcdInterop.FindGdiNameForTarget(vd.AdapterLuid, vd.TargetId);
        if (string.IsNullOrEmpty(name)) return false; // not active

        if (GdiInterop.TryGetCurrentMode(name, out var cur)
            && cur.dmPelsWidth == (uint)res.Width && cur.dmPelsHeight == (uint)res.Height
            && cur.dmDisplayFrequency == (uint)res.RefreshHz)
            return true; // already correct — do nothing

        Log.Info($"Mode drift on {name}: now {cur.dmPelsWidth}x{cur.dmPelsHeight}@{cur.dmDisplayFrequency}; forcing {res}.");

        // Try legacy first (cheap); fall back to the CCD path (reliable on 24H2).
        SetMode(name, (uint)res.Width, (uint)res.Height, (uint)res.RefreshHz);
        if (GdiInterop.TryGetCurrentMode(name, out var after)
            && after.dmPelsWidth == (uint)res.Width && after.dmPelsHeight == (uint)res.Height
            && after.dmDisplayFrequency == (uint)res.RefreshHz)
            return true;

        return SetModeViaCcd(vd, res);
    }

    private bool SetModeViaCcd(VirtualDisplayHandle vd, ResolutionConfig res)
    {
        if (!CcdInterop.QueryActive(0, out var paths, out var modes)) return false;
        int vp = FindVirtualPath(paths, vd);
        if (vp < 0) return false;

        uint sIdx = paths[vp].sourceInfo.modeInfoIdx;
        if (sIdx == CcdInterop.DISPLAYCONFIG_PATH_MODE_IDX_INVALID || sIdx >= modes.Length) return false;

        modes[sIdx].mode.sourceMode.width = (uint)res.Width;
        modes[sIdx].mode.sourceMode.height = (uint)res.Height;
        paths[vp].targetInfo.modeInfoIdx = CcdInterop.DISPLAYCONFIG_PATH_MODE_IDX_INVALID; // OS recomputes timing
        paths[vp].targetInfo.refreshRate = new CcdInterop.DISPLAYCONFIG_RATIONAL { Numerator = (uint)res.RefreshHz, Denominator = 1 };
        paths[vp].targetInfo.scanLineOrdering = 1; // progressive

        uint flags = CcdInterop.SDC_APPLY | CcdInterop.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                   | CcdInterop.SDC_ALLOW_CHANGES | CcdInterop.SDC_SAVE_TO_DATABASE;
        int r = CcdInterop.SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, flags);
        Log.Info($"SetModeViaCcd {res} -> {r}.");
        return r == CcdInterop.ERROR_SUCCESS;
    }

    public static bool SetMode(string gdi, uint w, uint h, uint hz)
    {
        if (!GdiInterop.TryGetCurrentMode(gdi, out var dm))
        {
            Log.Warn($"SetMode: cannot read current mode of {gdi}; using a fresh DEVMODE.");
            dm = GdiInterop.NewDevMode();
        }
        if (dm.dmPelsWidth == w && dm.dmPelsHeight == h && dm.dmDisplayFrequency == hz)
        {
            Log.Info($"SetMode: {gdi} already {w}x{h}@{hz}.");
            return true;
        }

        dm.dmPelsWidth = w; dm.dmPelsHeight = h; dm.dmDisplayFrequency = hz;
        dm.dmFields = GdiInterop.DM_PELSWIDTH | GdiInterop.DM_PELSHEIGHT | GdiInterop.DM_DISPLAYFREQUENCY;

        int r = GdiInterop.ChangeDisplaySettingsExW(gdi, ref dm, IntPtr.Zero, GdiInterop.CDS_UPDATEREGISTRY, IntPtr.Zero);
        if (r != GdiInterop.DISP_CHANGE_SUCCESSFUL && hz > 1)
        {
            foreach (uint alt in new[] { hz - 1, hz + 1 }) // ±1 Hz fallback (Apollo pattern)
            {
                dm.dmDisplayFrequency = alt;
                r = GdiInterop.ChangeDisplaySettingsExW(gdi, ref dm, IntPtr.Zero, GdiInterop.CDS_UPDATEREGISTRY, IntPtr.Zero);
                if (r == GdiInterop.DISP_CHANGE_SUCCESSFUL) { Log.Info($"SetMode: {gdi} accepted {w}x{h}@{alt} (±1 fallback)."); break; }
            }
        }
        Log.Info($"SetMode {gdi} -> {w}x{h}@{hz} : {r}");
        return r == GdiInterop.DISP_CHANGE_SUCCESSFUL;
    }

    /// <summary>Make <paramref name="gdi"/> the primary display (legacy CDS_SET_PRIMARY; Apollo's approach).</summary>
    public static bool SetPrimary(string gdi)
    {
        if (!GdiInterop.TryGetCurrentMode(gdi, out var target))
        {
            Log.Error($"SetPrimary: cannot read {gdi}.");
            return false;
        }
        int offX = target.dmPositionX, offY = target.dmPositionY;

        ForEachActiveDevice(name =>
        {
            if (!GdiInterop.TryGetCurrentMode(name, out var dm)) return;
            dm.dmPositionX -= offX;
            dm.dmPositionY -= offY;
            dm.dmFields = GdiInterop.DM_POSITION;
            uint flags = GdiInterop.CDS_UPDATEREGISTRY | GdiInterop.CDS_NORESET;
            if (string.Equals(name, gdi, StringComparison.OrdinalIgnoreCase)) flags |= GdiInterop.CDS_SET_PRIMARY;
            GdiInterop.ChangeDisplaySettingsExW(name, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
        });

        int r = GdiInterop.ChangeDisplaySettingsExW(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero); // commit
        Log.Info($"SetPrimary {gdi} commit -> {r}");
        return r == GdiInterop.DISP_CHANGE_SUCCESSFUL;
    }

    /// <summary>Detach every active display except <paramref name="keepGdi"/> (zero-size DEVMODE trick).</summary>
    public static bool DisableAllExcept(string keepGdi)
    {
        var toDisable = new List<string>();
        ForEachActiveDevice(name =>
        {
            if (!string.Equals(name, keepGdi, StringComparison.OrdinalIgnoreCase)) toDisable.Add(name);
        });

        if (toDisable.Count == 0) { Log.Info($"DisableAllExcept({keepGdi}): nothing else active."); return true; }

        foreach (var name in toDisable)
        {
            var dm = GdiInterop.NewDevMode();
            dm.dmFields = GdiInterop.DM_POSITION; // per MS docs: DM_POSITION + zero width/height detaches
            dm.dmPositionX = 0; dm.dmPositionY = 0;
            dm.dmPelsWidth = 0; dm.dmPelsHeight = 0;
            int r = GdiInterop.ChangeDisplaySettingsExW(name, ref dm, IntPtr.Zero,
                GdiInterop.CDS_UPDATEREGISTRY | GdiInterop.CDS_NORESET, IntPtr.Zero);
            Log.Info($"Disable {name} -> {r}");
        }
        int c = GdiInterop.ChangeDisplaySettingsExW(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero); // commit
        Log.Info($"DisableAllExcept({keepGdi}) commit -> {c} (disabled {toDisable.Count}).");
        return c == GdiInterop.DISP_CHANGE_SUCCESSFUL;
    }

    // ---- Restore ----

    public bool RestoreWithRetry(DisplaySnapshot snap, int attempts = 6, int delayMs = 800)
    {
        for (int i = 0; i < attempts; i++)
        {
            if (Restore(snap)) return true;
            Thread.Sleep(delayMs);
        }
        Log.Error("RestoreWithRetry: exhausted attempts.");
        return false;
    }

    public bool Restore(DisplaySnapshot snap)
    {
        var paths = snap.GetPaths();
        var modes = snap.GetModes();

        uint flags = CcdInterop.SDC_APPLY | CcdInterop.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                   | CcdInterop.SDC_ALLOW_CHANGES | CcdInterop.SDC_SAVE_TO_DATABASE;
        if ((snap.QueryExtraFlags & CcdInterop.QDC_VIRTUAL_MODE_AWARE) != 0) flags |= CcdInterop.SDC_VIRTUAL_MODE_AWARE;
        if ((snap.QueryExtraFlags & CcdInterop.QDC_VIRTUAL_REFRESH_RATE_AWARE) != 0) flags |= CcdInterop.SDC_VIRTUAL_REFRESH_RATE_AWARE;

        int r = CcdInterop.SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, flags);
        if (r == CcdInterop.ERROR_SUCCESS) { Log.Info("Restore: re-applied saved topology."); return true; }
        Log.Warn($"Restore: supplied-config apply err={r}; trying USE_DATABASE_CURRENT.");

        r = CcdInterop.SetDisplayConfig(0, null, 0, null, CcdInterop.SDC_APPLY | CcdInterop.SDC_USE_DATABASE_CURRENT);
        if (r == CcdInterop.ERROR_SUCCESS) { Log.Info("Restore: applied USE_DATABASE_CURRENT."); return true; }
        Log.Warn($"Restore: USE_DATABASE_CURRENT err={r}; trying TOPOLOGY_EXTEND (turn everything on).");

        r = CcdInterop.SetDisplayConfig(0, null, 0, null, CcdInterop.SDC_APPLY | CcdInterop.SDC_TOPOLOGY_EXTEND);
        Log.Info($"Restore: TOPOLOGY_EXTEND -> {r}");
        return r == CcdInterop.ERROR_SUCCESS;
    }

    // ---- enumeration helpers ----

    public static string? FindPrimaryGdiName()
    {
        string? primary = null;
        ForEachDevice((name, flags) =>
        {
            if ((flags & GdiInterop.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0) primary = name;
        });
        return primary;
    }

    public static List<string> ListActiveGdiNames()
    {
        var list = new List<string>();
        ForEachActiveDevice(list.Add);
        return list;
    }

    private static void ForEachActiveDevice(Action<string> action)
        => ForEachDevice((name, flags) => { if ((flags & GdiInterop.DISPLAY_DEVICE_ACTIVE) != 0) action(name); });

    private static void ForEachDevice(Action<string, uint> action)
    {
        for (uint i = 0; ; i++)
        {
            var dd = new GdiInterop.DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<GdiInterop.DISPLAY_DEVICE>() };
            if (!GdiInterop.EnumDisplayDevicesW(null, i, ref dd, 0)) break;
            action(dd.DeviceName, dd.StateFlags);
        }
    }
}

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ftop.App.Native;
using Ftop.App.Views;
using Ftop.Core;
using Ftop.Sensors;

namespace Ftop.App;

public partial class MainWindow : Window
{
    private readonly Sampler _sampler;
    private readonly EasingEngine _easer = new();
    private readonly CoresView _coresView = new();
    private readonly MetersView _metersView = new();
    private readonly ProcessListView _processView = new();

    private Snapshot? _snap;
    private FtopConfig _config;
    private string? _configError;
    private Tier _tier = Tier.M;
    private TimeSpan _lastRender = TimeSpan.Zero;

    // "A list that rests": keep the displayed order stable; only reorder
    // when the lead changes or 2.5 s have passed.
    private List<int> _displayedPids = new();
    private DateTime _lastReorder = DateTime.MinValue;
    private readonly Dictionary<int, string> _pidExe = new();
    private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private double _netMax = 1;

    // P/Invoke for rounded region, no-activate, and resize hit-testing.
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("user32.dll")] private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_NOACTIVATE = 0x08000000L;

    public MainWindow(FtopConfig config, Sampler sampler)
    {
        _config = config;
        _sampler = sampler;
        InitializeComponent();

        _coresView.Easer = _easer;
        _metersView.Easer = _easer;
        _processView.Easer = _easer;
        _easer.ReducedMotion = config.ReducedMotion;

        ModulesHost.Children.Add(_coresView);
        ModulesHost.Children.Add(_metersView);
        ModulesHost.Children.Add(_processView);
        // Gap between modules.
        foreach (var c in new FrameworkElement[] { _coresView, _metersView })
            c.Margin = new Thickness(0, 0, 0, Theme.ModuleGap);

        ApplyConfig(config, null);
        SizeChanged += (_, _) => { UpdateTier(); ApplyRoundedRegion(); };
        CompositionTarget.Rendering += OnRendering;
    }

    public void ApplyConfig(FtopConfig config, string? error)
    {
        _config = config;
        _configError = error;
        _easer.ReducedMotion = config.ReducedMotion;
        Theme.SetPalette(config.Palette);
        var pal = Theme.Current;
        Panel.Background = new SolidColorBrush(pal.PanelFill);
        Panel.BorderBrush = new SolidColorBrush(pal.PanelStroke);
        if (PresentationSource.FromVisual(this) is not null)
            Acrylic.Enable(new WindowInteropHelper(this).Handle, config.Palette != "light");
        Title = error is null ? "Ftop" : $"Ftop — config: {error}";
        UpdateTier();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        Acrylic.Enable(hwnd, _config.Palette != "light");

        // Non-activating panel, like ftop's NSPanel: clicks never steal focus.
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = Math.Max(1, (int)(ActualWidth * dpi.DpiScaleX));
        int h = Math.Max(1, (int)(ActualHeight * dpi.DpiScaleY));
        int r = (int)(Theme.CornerRadius * dpi.DpiScaleX);
        IntPtr rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, r * 2, r * 2);
        SetWindowRgn(hwnd, rgn, true);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12,
                  HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        if (msg == WM_NCHITTEST)
        {
            int p = lParam.ToInt32();
            int x = unchecked((short)(p & 0xFFFF));
            int y = unchecked((short)((p >> 16) & 0xFFFF));
            var dpi = VisualTreeHelper.GetDpi(this);
            double left = Left * dpi.DpiScaleX, top = Top * dpi.DpiScaleY;
            double right = left + ActualWidth * dpi.DpiScaleX, bottom = top + ActualHeight * dpi.DpiScaleY;
            const int B = 8;
            bool l = x < left + B, r = x >= right - B, t = y < top + B, b = y >= bottom - B;
            int ht = (t, b, l, r) switch
            {
                (true, _, true, _) => HTTOPLEFT,
                (true, _, _, true) => HTTOPRIGHT,
                (_, true, true, _) => HTBOTTOMLEFT,
                (_, true, _, true) => HTBOTTOMRIGHT,
                (_, _, true, _) => HTLEFT,
                (_, _, _, true) => HTRIGHT,
                (true, _, _, _) => HTTOP,
                (_, true, _, _) => HTBOTTOM,
                _ => HTCLIENT,
            };
            handled = true;
            return new IntPtr(ht);
        }
        return IntPtr.Zero;
    }

    private void TitleStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // Don't start dragging when the click began on a button.
        DependencyObject? d = e.OriginalSource as DependencyObject;
        while (d is not null)
        {
            if (d is System.Windows.Controls.Button) return;
            d = VisualTreeHelper.GetParent(d);
        }
        DragMove();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Content = Topmost ? "◉" : "○";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        var menu = new System.Windows.Controls.ContextMenu();
        var palItem = new System.Windows.Controls.MenuItem
            { Header = _config.Palette == "light" ? "切换深色" : "切换浅色" };
        palItem.Click += (_, _) => ApplyConfig(_config with { Palette = _config.Palette == "light" ? "dark" : "light" }, _configError);
        var motionItem = new System.Windows.Controls.MenuItem
            { Header = _config.ReducedMotion ? "开启动效" : "关闭动效", IsCheckable = true, IsChecked = _config.ReducedMotion };
        motionItem.Click += (_, _) => ApplyConfig(_config with { ReducedMotion = !_config.ReducedMotion }, _configError);
        var cfgItem = new System.Windows.Controls.MenuItem { Header = "打开配置文件" };
        cfgItem.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(FtopConfig.DefaultPath) { UseShellExecute = true }); } catch { }
        };
        var quitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
        quitItem.Click += (_, _) => Close();
        menu.Items.Add(palItem);
        menu.Items.Add(motionItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(cfgItem);
        menu.Items.Add(quitItem);
        menu.IsOpen = true;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        double dt = _lastRender == TimeSpan.Zero ? 0.016 : (now - _lastRender).TotalSeconds;
        _lastRender = now;
        dt = Math.Min(dt, 0.1);

        _coresView.FrameDt = dt;
        _metersView.FrameDt = dt;
        _processView.FrameDt = dt;
        _coresView.InvalidateVisual();
        _metersView.InvalidateVisual();
        _processView.InvalidateVisual();
    }

    public void SetSnapshot(Snapshot snap)
    {
        _snap = snap;
        _coresView.Cores = snap.Cores;

        var pal = Theme.Current;
        var rows = new List<(string Key, MeterRow Row)>();

        if (snap.Memory.UsedBytes.IsAvailable && snap.Memory.TotalBytes.IsAvailable)
        {
            double used = snap.Memory.UsedBytes.Value, total = snap.Memory.TotalBytes.Value;
            rows.Add(("memory", new MeterRow("内存", $"{Format.Bytes(used)} / {Format.Bytes(total)}",
                total > 0 ? used / total : 0, pal.Memory, true)));
        }
        else if (_config.ShowUnavailable)
            rows.Add(("memory", new MeterRow("内存", "", 0, pal.Memory, false)));

        if (snap.Network.DownBytesPerSec.IsAvailable)
        {
            double down = snap.Network.DownBytesPerSec.Value, up = snap.Network.UpBytesPerSec.Value;
            _netMax = Math.Max(_netMax * 0.995, Math.Max(down, up)); // slow decay
            rows.Add(("network", new MeterRow("网络", $"↓ {Format.BytesPerSec(down)}  ↑ {Format.BytesPerSec(up)}",
                Math.Clamp(Math.Max(down, up) / _netMax, 0, 1), pal.Network, true)));
        }
        else if (_config.ShowUnavailable)
            rows.Add(("network", new MeterRow("网络", "", 0, pal.Network, false)));

        foreach (var g in snap.Gpus)
        {
            rows.Add(("gpu", new MeterRow(g.Name.Length > 18 ? g.Name[..18] + "…" : g.Name,
                g.UsagePercent.IsAvailable ? Format.Percent(g.UsagePercent.Value) : "",
                g.UsagePercent.IsAvailable ? g.UsagePercent.Value / 100.0 : 0, pal.Gpu,
                g.UsagePercent.IsAvailable)));
        }

        if (_config.ShowUnavailable || snap.PowerWatts.IsAvailable)
            rows.Add(("power", new MeterRow("功耗",
                snap.PowerWatts.IsAvailable ? Format.Watts(snap.PowerWatts.Value) : "",
                0, pal.Power, snap.PowerWatts.IsAvailable)));

        // User-controlled module order from config.json.
        var order = _config.ModuleOrder
            .Select((m, i) => (m.ToLowerInvariant(), i))
            .ToDictionary(t => t.Item1, t => t.i);
        _metersView.Rows = rows
            .OrderBy(r => order.GetValueOrDefault(r.Key, 99))
            .Select(r => r.Row)
            .ToList();

        UpdateProcessRows(snap);
        UpdateTier();
        ApplyRoundedRegion();
    }

    private void UpdateProcessRows(Snapshot snap)
    {
        var top = snap.Processes.ToList();
        var now = DateTime.UtcNow;

        bool leadChanged = top.Count > 0 && _displayedPids.Count > 0 && top[0].Pid != _displayedPids[0];
        bool firstRun = _displayedPids.Count == 0;
        if (firstRun || leadChanged || (now - _lastReorder).TotalSeconds > 2.5)
        {
            _displayedPids = top.Select(p => p.Pid).ToList();
            _lastReorder = now;
            _easer.ForgetWhere(k => k.StartsWith("proc:"));
        }

        // Refresh exe-path cache for currently displayed pids only.
        _pidExe.Clear();
        foreach (int pid in _displayedPids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                string? path = null;
                try { path = p.MainModule?.FileName; } catch { }
                if (!string.IsNullOrEmpty(path)) _pidExe[pid] = path;
            }
            catch { /* exited between ticks */ }
        }

        var byPid = top.ToDictionary(p => p.Pid);
        var rows = new List<ProcessRow>();
        foreach (int pid in _displayedPids)
        {
            if (!byPid.TryGetValue(pid, out var pr)) continue;
            ImageSource? icon = null;
            if (_pidExe.TryGetValue(pid, out var path))
            {
                if (!_iconCache.TryGetValue(path, out icon))
                {
                    icon = ExtractIcon(path);
                    _iconCache[path] = icon; // may be null: negative cache
                    if (_iconCache.Count > 256)
                        foreach (var k in _iconCache.Keys.Take(64).ToList()) _iconCache.Remove(k);
                }
            }
            rows.Add(new ProcessRow(pid, pr.Name,
                pr.CpuPercent.IsAvailable ? Format.Percent(pr.CpuPercent.Value) : "—",
                pr.MemoryBytes.IsAvailable ? Format.Bytes(pr.MemoryBytes.Value) : "",
                pr.CpuPercent.IsAvailable ? pr.CpuPercent.Value / 100.0 : 0,
                icon));
        }
        _processView.Rows = rows;
    }

    private static ImageSource? ExtractIcon(string path)
    {
        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch { return null; }
    }

    private void UpdateTier()
    {
        if (_snap is null) return;
        var (tier, processRows) = LayoutLadder.Pick(ActualWidth, ActualHeight - 30);
        _tier = tier;
        _sampler.ProcessRows = Math.Max(processRows, tier == Tier.L ? 4 : 0);

        _coresView.Visibility = tier >= Tier.S ? Visibility.Visible : Visibility.Collapsed;
        _metersView.Visibility = Visibility.Visible;
        _processView.Visibility = tier == Tier.L ? Visibility.Visible : Visibility.Collapsed;

        // XS tier: single compact CPU row instead of columns.
        if (tier == Tier.Xs && _snap.Cores.Count > 0)
        {
            var pal = Theme.Current;
            double avg = _snap.Cores.Where(c => c.UsagePercent.IsAvailable)
                .Select(c => c.UsagePercent.Value).DefaultIfEmpty(0).Average();
            _metersView.Rows = new[]
            {
                new MeterRow("CPU", Format.Percent(avg), avg / 100.0, pal.Cpu, true)
            };
        }
    }
}

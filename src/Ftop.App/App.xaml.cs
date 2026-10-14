using System.IO;
using System.Threading;
using System.Windows;
using Ftop.Core;
using Ftop.Sensors;

namespace Ftop.App;

public partial class App : Application
{
    private static Mutex? _mutex;
    private Sampler? _sampler;
    private FileSystemWatcher? _configWatcher;
    private FtopConfig _config = FtopConfig.Default;
    private string? _configError;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single instance: a second launch just exits (the panel is already up).
        _mutex = new Mutex(true, "FtopWindowsPanel_SingleInstance", out bool created);
        if (!created)
        {
            Shutdown();
            return;
        }

        LoadConfig();

        _sampler = new Sampler(_config.RefreshMs);
        var window = new MainWindow(_config);
        _sampler.SnapshotReady += snap =>
            window.Dispatcher.BeginInvoke(() => window.SetSnapshot(snap));

        // Pause sampling while minimized; resume on restore.
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Minimized) _sampler.Pause();
            else _sampler.Resume();
        };

        WatchConfig(window);

        _sampler.Start();
        window.Show();
        base.OnStartup(e);
    }

    private void LoadConfig()
    {
        var path = FtopConfig.DefaultPath;
        FtopConfig.EnsureExists(path);
        (_config, _configError) = FtopConfig.Load(path);
    }

    private void WatchConfig(MainWindow window)
    {
        try
        {
            var dir = Path.GetDirectoryName(FtopConfig.DefaultPath)!;
            _configWatcher = new FileSystemWatcher(dir, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _configWatcher.Changed += (_, _) =>
            {
                // Debounce the double-fire that editors cause.
                Thread.Sleep(150);
                var (cfg, err) = FtopConfig.Load(FtopConfig.DefaultPath);
                window.Dispatcher.BeginInvoke(() =>
                {
                    _config = cfg;
                    _configError = err;
                    _sampler!.RefreshMs = cfg.RefreshMs;
                    window.ApplyConfig(cfg, err);
                });
            };
            _configWatcher.EnableRaisingEvents = true;
        }
        catch { /* config hot-reload is a nice-to-have */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _configWatcher?.Dispose();
        _sampler?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

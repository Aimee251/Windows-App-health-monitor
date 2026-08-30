using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using AppHealth.Core;
using ScottPlot;
using ScottPlot.Avalonia;


namespace AvaloniaTest;

public partial class MainWindow : Window
{
    // these live for the whole app, so history accumulates across ticks
    private readonly IMetricSource _source;
    private readonly MetricsStore _store = new(capacityPerProcess: 120);
    private readonly DispatcherTimer _timer;
    private int _sampleCount;

    public MainWindow()
    {
        InitializeComponent();

        var report = new EnvironmentCheck().Inspect();

        var watchlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node", "dotnet", "python3", "java",
            "Antigravity IDE Helper", "Code", "Code Helper",
        };

        _source = report.IsWindows && report.PerfCounters == Capability.Available
            ? new WindowsMetricSource()
            : new CrossPlatformMetricSource(name => watchlist.Contains(name));

        // sample once immediately so the first tick has a CPU baseline
        _store.Ingest(_source.Sample());

        // then sample + refresh every 2 seconds, on the UI thread
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

        private void Refresh()
    {
        _store.Ingest(_source.Sample());
        _sampleCount++;

        var triage = Rubric.Triage(_store, Weights.Default).Take(25).ToList();
        TriageGrid.ItemsSource = triage;

        string leakStatus = _sampleCount >= 30
            ? "leak detection active"
            : $"leak detection in {30 - _sampleCount} more samples";
        StatusText.Text =
            $"{triage.Count} processes · {_sampleCount} samples · " +
            $"updated {DateTime.Now:HH:mm:ss} · {leakStatus}";

        if (triage.Count > 0)
            PlotProcess(triage[0].ProcessId, triage[0].ProcessName);   // worst-ranked
    }

    private void PlotProcess(int pid, string name)
    {
        var history = _store.History(pid);
        if (history.Count < 2) return;

        var plot = MemoryChart.Plot;
        plot.Clear();

        // x = seconds since first sample, y = memory in MB
        double t0 = history[0].Timestamp.Ticks;
        double[] xs = history.Select(s => (s.Timestamp.Ticks - t0) / (double)TimeSpan.TicksPerSecond).ToArray();
        double[] ys = history.Select(s => s.WorkingSetBytes / (1024.0 * 1024.0)).ToArray();

        plot.Add.ScatterPoints(xs, ys);   // the raw memory points

        // draw the fitted regression line (same Stats.Fit that powers leak detection)
        if (history.Count >= 30)
        {
            var fit = Stats.Fit(xs, ys);
            double x0 = xs[0], x1 = xs[^1];
            double y0 = fit.Slope * x0 + fit.Intercept;
            double y1 = fit.Slope * x1 + fit.Intercept;
            var line = plot.Add.Line(x0, y0, x1, y1);
            line.LineWidth = 2;
        }

        plot.Title($"{name} — memory (MB) over time");
        plot.Axes.AutoScale();
        MemoryChart.Refresh();
    }
} 
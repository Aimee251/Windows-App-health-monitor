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
    private int? _selectedPid;

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
        if (_selectedPid is int pid)
        {
            var chosen = triage.FirstOrDefault(c => c.ProcessId == pid);
            if (chosen is not null) ShowDetail(chosen.ProcessId, chosen.ProcessName);
        }
        else if (triage.Count > 0)
        {
            ShowDetail(triage[0].ProcessId, triage[0].ProcessName);
        }
    }

 private void OnRowSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (TriageGrid.SelectedItem is Concern c)
        {
            _selectedPid = c.ProcessId;
            ShowDetail(c.ProcessId, c.ProcessName);
        }
    }

    // ★ CHANGE 4b: new method — handles the "Back to worst" button
    private void OnBackToWorst(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _selectedPid = null;
        TriageGrid.SelectedItem = null;
    }

    // Delete PlotProcess completely — ShowDetail takes its place (two charts + stats line).
    private void ShowDetail(int pid, string name)
    {
        var history = _store.History(pid);
        DetailTitle.Text = _selectedPid is null ? $"{name}  (worst)" : $"{name}  (selected)";

        if (history.Count < 2)
        {
            DetailStats.Text = "collecting…";
            return;
        }

        double t0 = history[0].Timestamp.Ticks;
        double[] xs  = history.Select(s => (s.Timestamp.Ticks - t0) / (double)TimeSpan.TicksPerSecond).ToArray();
        double[] mem = history.Select(s => s.WorkingSetBytes / (1024.0 * 1024.0)).ToArray();
        double[] cpu = history.Select(s => (double)s.CpuPercent).ToArray();

        // memory chart + regression line
        var mp = MemoryChart.Plot;
        mp.Clear();
        mp.Add.ScatterPoints(xs, mem);
        double leakRate = 0, r2 = 0;
        if (history.Count >= 30)
        {
            var fit = Stats.Fit(xs, mem);
            leakRate = fit.Slope * 60.0;
            r2 = fit.RSquared;
            double y0 = fit.Slope * xs[0]  + fit.Intercept;
            double y1 = fit.Slope * xs[^1] + fit.Intercept;
            var line = mp.Add.Line(xs[0], y0, xs[^1], y1);
            line.LineWidth = 2;
        }
        mp.Title("Memory (MB)");
        mp.Axes.AutoScale();
        MemoryChart.Refresh();

        // cpu chart
        var cp = CpuChart.Plot;
        cp.Clear();
        cp.Add.ScatterLine(xs, cpu);
        cp.Title("CPU (%)");
        cp.Axes.AutoScale();
        CpuChart.Refresh();

        // stats line
        double p95 = Stats.Percentile(cpu, 95);
        double vol = Stats.StdDev(cpu);
        DetailStats.Text = history.Count >= 30
            ? $"CPU p95={p95:0.0}%  ·  volatility σ={vol:0.0}  ·  leak={leakRate:+0.0;-0.0} MB/min (R²={r2:0.00})  ·  {history.Count} samples"
            : $"CPU p95={p95:0.0}%  ·  volatility σ={vol:0.0}  ·  leak: need {30 - history.Count} more samples  ·  {history.Count} samples";
    }
} 
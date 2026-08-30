using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using AppHealth.Core;

namespace AvaloniaTest;

public sealed class Monitor
{
    private readonly IMetricSource _source;
    private readonly MetricsStore _store = new(capacityPerProcess: 120);
    private readonly DispatcherTimer _timer;

    public int SampleCount { get; private set; }
    public IReadOnlyList<Concern> Triage { get; private set; } = new List<Concern>();
    public MetricsStore Store => _store;

    // fires every tick so windows can refresh themselves
    public event Action? Updated;

    public Monitor()
    {
        var report = new EnvironmentCheck().Inspect();
        var watchlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node", "dotnet", "python3", "java",
            "Antigravity IDE Helper", "Code", "Code Helper",
        };
        _source = report.IsWindows && report.PerfCounters == Capability.Available
            ? new WindowsMetricSource()
            : new CrossPlatformMetricSource(name => watchlist.Contains(name));

        _store.Ingest(_source.Sample());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        _store.Ingest(_source.Sample());
        SampleCount++;
        Triage = Rubric.Triage(_store, Weights.Default).Take(25).ToList();
        Updated?.Invoke();          // tell any open windows to refresh
    }
}
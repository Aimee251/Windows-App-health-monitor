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

    // digest scheduling
    private DateTime _lastDailyDigest = DateTime.MinValue;
    public int DailyDigestHour { get; set; } = 17;      // 5pm local
    public event Action<string>? DigestReady;

    // latency tracking
    private readonly LatencyStore _latencyStore = new(capacityPerOp: 200);
    public LatencyStore LatencyStore => _latencyStore;
    public IReadOnlyList<OpConcern> LatencyTriage { get; private set; } = new List<OpConcern>();

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
        new WorkloadService(_latencyStore).Start();
    }

    private void Tick()
    {
        _store.Ingest(_source.Sample());
        SampleCount++;
        Triage = Rubric.Triage(_store, Weights.Default).Take(25).ToList();
        Updated?.Invoke();          // tell any open windows to refresh
        CheckDigest();
        LatencyTriage = LatencyRubric.Triage(_latencyStore);
        foreach (var op in LatencyTriage){
            System.Diagnostics.Debug.WriteLine($"{op.Operation}: p99={op.P99:0}ms ({op.SampleCount} calls) [{op.Severity}]");
        }
    }

        private void CheckDigest()
    {
        var now = DateTime.Now;                     // already local time
        // fire once per day when we cross into the trigger hour
        if (now.Hour == DailyDigestHour && _lastDailyDigest.Date != now.Date)
        {
            _lastDailyDigest = now;
            DigestReady?.Invoke(Digest.Build(this, "Daily"));
        }
    }
}
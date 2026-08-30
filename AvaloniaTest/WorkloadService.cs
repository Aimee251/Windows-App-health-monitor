using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AppHealth.Core;

namespace AvaloniaTest;

public sealed class WorkloadService
{
    private readonly LatencyStore _store;
    private readonly Random _rng = new();
    private int _ticks;                       // drives the degrading operation

    public WorkloadService(LatencyStore store) => _store = store;

    // start the background loop that continuously exercises the operations
    public void Start()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                RunOnce("fast-query",   () => Delay(5, 15));       // healthy, quick
                RunOnce("normal-query", () => Delay(30, 80));      // healthy, moderate
                RunOnce("report-build", () => Delay(120, 220));    // borderline slow
                RunOnce("export-job",   () => DegradingDelay());   // gets worse over time
                _ticks++;
                await Task.Delay(400);   // pace the loop
            }
        });
    }

    // time one operation and record its latency
    private void RunOnce(string op, Action work)
    {
        var sw = Stopwatch.StartNew();
        work();
        sw.Stop();
        _store.Record(new LatencySample(DateTime.UtcNow, op, sw.Elapsed.TotalMilliseconds));
    }

    private void Delay(int minMs, int maxMs) => Thread.Sleep(_rng.Next(minMs, maxMs));

    // starts fine, then creeps upward — simulates an operation degrading (e.g. a slow query
    // as a table grows). This is what the latency detector should catch, like a memory leak.
    private void DegradingDelay()
    {
        int baseline = 40;
        int creep = _ticks * 2;                       // +2ms every loop
        int jitter = _rng.Next(0, 20);
        Thread.Sleep(baseline + creep + jitter);
    }
}
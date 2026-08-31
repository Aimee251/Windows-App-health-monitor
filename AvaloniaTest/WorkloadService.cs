using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AppHealth.Core;

///////NOW THE DEMO RUNS 3 APPS//////

namespace AvaloniaTest; 

public sealed class WorkloadService
{
    private readonly LatencyStore _store;
    private readonly Random _rng = new();
    private int _ticks;

    public WorkloadService(LatencyStore store) => _store = store;

    public void Start()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                // App 1: CheckoutService — healthy
                RunOnce("CheckoutService", "validate-cart", () => Delay(5, 20));
                RunOnce("CheckoutService", "process-payment", () => Delay(40, 90));

                // App 2: ReportingApp — one operation degrades over time
                RunOnce("ReportingApp", "fetch-data", () => Delay(20, 60));
                RunOnce("ReportingApp", "export-job", DegradingDelay);   // gets worse

                // App 3: SearchService — moderate but steady
                RunOnce("SearchService", "query", () => Delay(30, 110));
                RunOnce("SearchService", "index-update", () => Delay(80, 160));

                _ticks++;
                await Task.Delay(400);
            }
        });
    }

    private void RunOnce(string app, string op, Action work)
    {
        var sw = Stopwatch.StartNew();
        work();
        sw.Stop();
        _store.Record(new LatencySample(DateTime.UtcNow, app, op, sw.Elapsed.TotalMilliseconds)); 
    }

    private void Delay(int minMs, int maxMs) => Thread.Sleep(_rng.Next(minMs, maxMs));

    private void DegradingDelay()
    {
        int creep = _ticks * 2;                    // +2ms per loop
        Thread.Sleep(40 + creep + _rng.Next(0, 20));
    }
}
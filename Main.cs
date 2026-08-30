using System;
using System.Linq;
using System.Threading;
using AppHealth.Core;

var report = new EnvironmentCheck().Inspect();
Console.WriteLine($"OS: {report.OsDescription} | Windows: {report.IsWindows} | Elevated: {report.IsElevated}");

IMetricSource source = new CrossPlatformMetricSource();   // Mac path
var store = new MetricsStore(capacityPerProcess: 120);

Console.WriteLine("Collecting samples...");
for (int i = 0; i < 10; i++)          // 10 ticks × 2s = ~20s of history
{
    store.Ingest(source.Sample());
    Console.Write($"\r  tick {i + 1}/10");
    Thread.Sleep(2000);
}
Console.WriteLine("\n");

var triage = Rubric.Triage(store, Weights.Default);

Console.WriteLine("=== TRIAGE (worst first) ===");
foreach (var c in triage.Take(15))
    Console.WriteLine($"[{c.Severity,-8}] {c.ProcessName,-22} {c.Score,5:0} — {c.Reason}");
using System;
using System.Collections.Generic;
using System.Linq;             
using System.Threading; 

namespace AppHealth.Core;

class Program {
    static void Main() {
        var report = new EnvironmentCheck().Inspect();

        Console.WriteLine($"OS: {report.OsDescription}");
        Console.WriteLine($"is elevated: {report.IsElevated}");
        Console.WriteLine($"is windows: {report.IsWindows}");

        Console.WriteLine($"PerfCounters: {report.PerfCounters}");
        Console.WriteLine($"EventLog: {report.EventLog}");
        Console.WriteLine($"ETW hangs: {report.EtwHangs}");

        // apps you actually want to watch — expand freely
        var watchlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node", "dotnet", "python3", "java",
            "Antigravity IDE Helper",        // your IDE, from the earlier run
            "Code", "Code Helper",           // VS Code, if you use it
            // add your own service's process name here once you build it (Phase B)
        };

        IMetricSource metricSource =
            report.IsWindows && report.PerfCounters == Capability.Available
                ? new WindowsMetricSource()
                : new CrossPlatformMetricSource(name => watchlist.Contains(name));
        var eventSources = new List<IEventSource>();
        if (report.EtwHangs == Capability.Available) eventSources.Add(new EtwHangSource());
        if (report.EventLog == Capability.Available) eventSources.Add(new EventLogCrashSource());

        // ... start the collector, run the app ...
        Console.WriteLine($"Collector selected platform: {metricSource.Platform}");
        Console.WriteLine($"Active event sources: {eventSources.Count}");

                var store = new MetricsStore(capacityPerProcess: 120);

        Console.WriteLine("\nCollecting samples...");
        for (int i = 0; i < 10; i++)
        {
            store.Ingest(metricSource.Sample());
            Console.Write($"\r  tick {i + 1}/10");
            Thread.Sleep(2000);
        }
        Console.WriteLine("\n");

        var triage = Rubric.Triage(store, Weights.Default);

        Console.WriteLine("=== TRIAGE (worst first) ===");
        foreach (var c in triage.Take(15))
            Console.WriteLine($"[{c.Severity,-8}] {c.ProcessName,-22} {c.Score,5:0} — {c.Reason}");
    }
}
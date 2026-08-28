using System;
using System.Collections.Generic;

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

        IMetricSource metricSource =
            report.IsWindows && report.PerfCounters == Capability.Available
                ? new WindowsMetricSource()
                : new CrossPlatformMetricSource();

        var eventSources = new List<IEventSource>();
        if (report.EtwHangs == Capability.Available) eventSources.Add(new EtwHangSource());
        if (report.EventLog == Capability.Available) eventSources.Add(new EventLogCrashSource());

        // ... start the collector, run the app ...
        Console.WriteLine($"Collector selected platform: {metricSource.Platform}");
        Console.WriteLine($"Active event sources: {eventSources.Count}");
    }
}
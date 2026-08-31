using System;
using System.Collections.Generic;

namespace AppHealth.Core;

public record MetricSample(
     DateTime Timestamp, int ProcessId, string ProcessName,
    double CpuPercent,
    long WorkingSetBytes, long PrivateBytes,
    long DiskReadBytesPerSec, long DiskWriteBytesPerSec,  // rates — delta like CPU
    int HandleCount, int ThreadCount
);

public enum AppEventKind {
    Crash, Hang, Error, Warning
}

public record AppEvent(
    DateTime Timestamp, int ProcessId, string ProcessName,
    AppEventKind Kind, string? Detail
);

public interface IMetricSource {      // pull, on a timer
    string Platform { get; }
    IReadOnlyList<MetricSample> Sample();
}

public interface IEventSource {        // push, event-driven
    event Action<AppEvent> EventRaised;
    void Start();  void Stop();
}

public enum Capability{
    Available,Unavailable,Restricted
}

public record EnvironmentReport(
    string OsDescription, bool IsWindows, bool IsElevated, Capability ProcessMetrics,
    Capability PerfCounters, Capability EventLog, Capability EtwHangs, Capability ServiceState
);

public interface IEnvironmentCheck{
    EnvironmentReport Inspect();
}

public record LatencySample(
    DateTime Timestamp,
    string AppName,
    string OperationName,
    double DurationMs);
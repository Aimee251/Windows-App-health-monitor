using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace AppHealth.Core;

public sealed class EnvironmentCheck : IEnvironmentCheck {
    // checking whether the os is windows or not
    public EnvironmentReport Inspect(){
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isElevated = isWindows && IsElevated();
         
        return new EnvironmentReport(
            OsDescription:  RuntimeInformation.OSDescription,
            IsWindows:      isWindows,
            IsElevated:     isElevated,
            // process metrics work everywhere — this is the one thing we can always do
            ProcessMetrics: Capability.Available,
            // the rest are Windows-only; probe each one instead of assuming
            PerfCounters:   isWindows ? ProbePerfCounters()       : Capability.Unavailable,
            EventLog:       isWindows ? ProbeEventLog(isElevated) : Capability.Unavailable,
            EtwHangs:       isWindows ? ProbeETW(isElevated)      : Capability.Unavailable,
            ServiceState:   isWindows ? Capability.Available      : Capability.Unavailable
        );
    }

    [SupportedOSPlatform("windows")]
    private static bool IsElevated() {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    [SupportedOSPlatform("windows")]
    private static Capability ProbePerfCounters () {
        try{
            using var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            counter.NextValue();
            return Capability.Available;
        }catch(PlatformNotSupportedException){
            return Capability.Unavailable;
        }catch(Exception){
            return Capability.Restricted; // permission denied;
        }
    }
    [SupportedOSPlatform("windows")]
    private static Capability ProbeEventLog(bool isElevated) {
        if(!isElevated) return Capability.Restricted;

        try{
            using var log = new EventLog("Application");
            _ = log.Entries.Count;
            return Capability.Available;
        } catch(PlatformNotSupportedException){
            return Capability.Restricted;
        }catch(Exception){
            return Capability.Unavailable;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Capability ProbeETW(bool isElevated) {
        return isElevated ? Capability.Available : Capability.Restricted;
    }
}

public sealed class CrossPlatformMetricSource : IMetricSource
{
    public string Platform => "cross-platform";
    private readonly Dictionary<int, (TimeSpan cpu, DateTime at)> _prev = new();
    private readonly Func<string, bool> _include;

    public CrossPlatformMetricSource(Func<string, bool>? include = null)=> _include = include ?? (_=>true);
    public IReadOnlyList<MetricSample> Sample()
    {
        var now = DateTime.UtcNow;
        var cores = Environment.ProcessorCount;
        var list = new List<MetricSample>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!_include(p.ProcessName)) continue;
                var cpuTime = p.TotalProcessorTime;
                double cpu = 0;
                if (_prev.TryGetValue(p.Id, out var last))
                {
                    var cpuDelta  = (cpuTime - last.cpu).TotalMilliseconds;
                    var wallDelta = (now - last.at).TotalMilliseconds;
                    if (wallDelta > 0) cpu = cpuDelta / wallDelta / cores * 100.0;
                }
                _prev[p.Id] = (cpuTime, now);

                list.Add(new MetricSample(
                    now, p.Id, p.ProcessName, Math.Round(cpu, 2),
                    p.WorkingSet64, p.PrivateMemorySize64,
                    0, 0,
                    SafeHandles(p), p.Threads.Count));
            }
            catch { /* exited or access denied */ }
            finally { p.Dispose(); }
        }
        return list;
    }

    private static int SafeHandles(Process p)
    {
        try { return p.HandleCount; } catch { return 0; }
    }
}

public sealed class WindowsMetricSource : IMetricSource
{
    public string Platform => "windows";
    private readonly Dictionary<int, (TimeSpan cpu, DateTime at)> _prev = new();
    private readonly Func<string, bool> _include;

    public WindowsMetricSource(Func<string, bool>? include = null)
        => _include = include ?? (_ => true);

    public IReadOnlyList<MetricSample> Sample()
    {
        var now = DateTime.UtcNow;
        var cores = Environment.ProcessorCount;
        var list = new List<MetricSample>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!_include(p.ProcessName)) continue;
                var cpuTime = p.TotalProcessorTime;
                double cpu = 0;
                if (_prev.TryGetValue(p.Id, out var last))
                {
                    var cpuDelta  = (cpuTime - last.cpu).TotalMilliseconds;
                    var wallDelta = (now - last.at).TotalMilliseconds;
                    if (wallDelta > 0) cpu = cpuDelta / wallDelta / cores * 100.0;
                }
                _prev[p.Id] = (cpuTime, now);

                list.Add(new MetricSample(
                    now, p.Id, p.ProcessName, Math.Round(cpu, 2),
                    p.WorkingSet64, p.PrivateMemorySize64,
                    0, 0,
                    p.HandleCount,        // works on Windows (threw on Mac)
                    p.Threads.Count));
            }
            catch { }
            finally { p.Dispose(); }
        }
        return list;
    }
}

public sealed class EtwHangSource : IEventSource
{
    public event Action<AppEvent>? EventRaised;
    public void Start() { }
    public void Stop() { }
}

public sealed class EventLogCrashSource : IEventSource
{
    public event Action<AppEvent>? EventRaised;
    public void Start() { }
    public void Stop() { }
}

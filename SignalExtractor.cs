using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public record ProcessSignals(
    int ProcessId, string ProcessName,
    double CpuP95,           // percent
    double CpuVolatility,    // std dev of CPU
    double LeakMBPerMin,     // memory slope
    double LeakConfidence,   // R² of the memory fit
    double SustainedCpuFrac  // fraction of recent samples that were "hot"
);

public static class SignalExtractor
{
    public static ProcessSignals Extract(IReadOnlyList<MetricSample> history)
    {
        int pid   = history.Count > 0 ? history[^1].ProcessId   : 0;
        string nm = history.Count > 0 ? history[^1].ProcessName : "";

        var cpu = history.Select(s => (double)s.CpuPercent).ToList();

        // memory regression: x = seconds since first sample, y = working set in MB
        double t0 = history.Count > 0 ? history[0].Timestamp.Ticks : 0;
        var xs = history.Select(s => (s.Timestamp.Ticks - t0) / (double)TimeSpan.TicksPerSecond).ToList();
        var ys = history.Select(s => s.WorkingSetBytes / (1024.0 * 1024.0)).ToList();
        var fit = Stats.Fit(xs, ys);

        return new ProcessSignals(
            pid, nm,
            CpuP95:           Stats.Percentile(cpu, 95),
            CpuVolatility:    Stats.StdDev(cpu),
            LeakMBPerMin:     fit.Slope * 60.0,        // MB/sec → MB/min
            LeakConfidence:   fit.RSquared,
            SustainedCpuFrac: SustainedFraction(cpu, threshold: 80, lookback: 30));
    }

    private static double SustainedFraction(IReadOnlyList<double> cpu, double threshold, int lookback)
    {
        if (cpu.Count == 0) return 0;
        var recent = cpu.Skip(Math.Max(0, cpu.Count - lookback)).ToList();
        return recent.Count(v => v >= threshold) / (double)recent.Count;
    }
}
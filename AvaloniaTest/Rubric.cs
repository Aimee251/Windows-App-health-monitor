using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public record Weights(double Leak, double CpuP95, double Volatility, double Sustained)
{
    // leak weighted highest: a steady leak is a real, worsening bug, not a transient spike
    public static readonly Weights Default = new(Leak: 0.45, CpuP95: 0.25, Volatility: 0.10, Sustained: 0.20);
}

public enum Severity { Low, Medium, High, Critical }

public record Concern(int ProcessId, string ProcessName, double Score, Severity Severity, string Reason);

public static class Rubric
{
    // each signal → 0..100 concern points
    private static (double leak, double cpu, double vol, double sus) Normalize(ProcessSignals s)
    {
        double leak = s.LeakConfidence >= 0.85                
          ? Clamp01(s.LeakMBPerMin / 100.0) * 100             
          : 0;
        double cpu = Math.Clamp(s.CpuP95, 0, 100);                  // already a percent
        double vol = Clamp01(s.CpuVolatility / 40.0) * 100;         // σ of 40 = maxed
        double sus = Math.Clamp(s.SustainedCpuFrac * 100, 0, 100);
        return (leak, cpu, vol, sus);
    }

    public static double Score(ProcessSignals s, Weights w)
    {
        var (leak, cpu, vol, sus) = Normalize(s);
        return leak * w.Leak + cpu * w.CpuP95 + vol * w.Volatility + sus * w.Sustained;
    }

    public static Severity Band(double score) => score switch
    {
        >= 75 => Severity.Critical,
        >= 50 => Severity.High,
        >= 25 => Severity.Medium,
        _     => Severity.Low
    };

    public static string PrimaryReason(ProcessSignals s)
    {
        var (leak, cpu, vol, sus) = Normalize(s);
        var ranked = new (string label, double val)[]
        {
            ($"memory leak: +{s.LeakMBPerMin:0.0} MB/min (R²={s.LeakConfidence:0.00})", leak),
            ($"high CPU: p95={s.CpuP95:0}%", cpu),
            ($"erratic CPU: σ={s.CpuVolatility:0}", vol),
            ($"sustained load: {s.SustainedCpuFrac * 100:0}% of recent samples hot", sus),
        };
        var top = ranked.OrderByDescending(r => r.val).First();
        return top.val <= 0 ? "nominal" : top.label;
    }

    // the top-level call: rank every process worst-first
    public static IReadOnlyList<Concern> Triage(MetricsStore store, Weights w)
    {
        var concerns = new List<Concern>();
        foreach (var latest in store.LatestPerProcess())
        {
            var signals = SignalExtractor.Extract(store.History(latest.ProcessId));
            double score = Score(signals, w);
            concerns.Add(new Concern(signals.ProcessId, signals.ProcessName,
                                     score, Band(score), PrimaryReason(signals)));
        }
        return concerns.OrderByDescending(c => c.Score).ToList();
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
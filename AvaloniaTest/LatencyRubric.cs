using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public record OpConcern(
    string AppName, string Operation, int SampleCount,
    double P50, double P95, double P99,
    Severity Severity, string Reason);

public static class LatencyRubric
{
    private const double P99WarnMs = 200;
    private const double P99CritMs = 1000;

    public static IReadOnlyList<OpConcern> Triage(LatencyStore store)
    {
        var concerns = new List<OpConcern>();
        foreach (var b in store.Buckets())
        {
            var samples = store.Samples(b.AppName, b.OperationName);
            if (samples.Count == 0) continue;
            var d = samples.Select(s => s.DurationMs).ToList();

            double p50 = Stats.Percentile(d, 50);
            double p95 = Stats.Percentile(d, 95);
            double p99 = Stats.Percentile(d, 99);

            var sev = p99 >= P99CritMs ? Severity.Critical
                    : p99 >= P99WarnMs ? Severity.High
                    : Severity.Low;

            string reason = sev == Severity.Low
                ? "healthy"
                : $"slow tail: p99={p99:0}ms (p50={p50:0}ms) — {d.Count} calls";

            concerns.Add(new OpConcern(b.AppName, b.OperationName, d.Count, p50, p95, p99, sev, reason));
        }
        return concerns.OrderByDescending(c => c.P99).ToList();
    }
}
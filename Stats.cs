using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public record LinearFit(double Slope, double Intercept, double RSquared);

public static class Stats
{
    public static LinearFit Fit(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        int n = xs.Count;
        if (n < 2) return new LinearFit(0, n == 1 ? ys[0] : 0, 0);

        double xMean = xs.Average(), yMean = ys.Average();
        double sxx = 0, sxy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = xs[i] - xMean;
            sxx += dx * dx;
            sxy += dx * (ys[i] - yMean);
        }
        if (sxx == 0) return new LinearFit(0, yMean, 0);   // all timestamps identical

        double slope = sxy / sxx;
        double intercept = yMean - slope * xMean;

        double ssRes = 0, ssTot = 0;
        for (int i = 0; i < n; i++)
        {
            double predicted = slope * xs[i] + intercept;
            ssRes += Math.Pow(ys[i] - predicted, 2);   // how far points miss the line
            ssTot += Math.Pow(ys[i] - yMean, 2);        // how far points spread from flat
        }
        double r2 = ssTot == 0 ? 0 : 1 - ssRes / ssTot;

        return new LinearFit(slope, intercept, r2);
    }

    public static double StdDev(IReadOnlyList<double> xs)
    {
        if (xs.Count < 2) return 0;
        double mean = xs.Average();
        return Math.Sqrt(xs.Sum(x => (x - mean) * (x - mean)) / xs.Count);
    }

    public static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        int i = Math.Clamp((int)Math.Ceiling(p / 100.0 * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[i];
    }
}


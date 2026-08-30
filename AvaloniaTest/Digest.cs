using System;
using System.Linq;
using System.Text;
using AppHealth.Core;

namespace AvaloniaTest;

public static class Digest
{
    public static string Build(Monitor monitor, string period)
    {
        var triage = monitor.Triage;
        var sb = new StringBuilder();

        sb.AppendLine($"{period} App Health Digest");
        sb.AppendLine($"{DateTime.Now:dddd, MMM d · h:mm tt}");
        sb.AppendLine($"Timezone: {TimeZoneInfo.Local.StandardName}");
        sb.AppendLine();

        var issues = triage.Where(c => c.Severity >= Severity.Medium).ToList();

        if (issues.Count == 0)
        {
            sb.AppendLine("✅ All tracked apps healthy. No issues to report.");
        }
        else
        {
            sb.AppendLine($"⚠️  {issues.Count} app(s) need attention:");
            sb.AppendLine();
            foreach (var c in issues.Take(10))
                sb.AppendLine($"  [{c.Severity}] {c.ProcessName} — {c.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine($"Monitored {triage.Count} processes over {monitor.SampleCount} samples.");
        return sb.ToString();
    }
}
# App Health Monitor

A cross-platform desktop observability tool that monitors the health of running
applications in real time, ranks them by severity, and surfaces problems through a
floating desktop widget and scheduled digests. Built in C#/.NET with Avalonia.

The core design goal was a **source-agnostic pipeline**: platform-specific data
collectors plug in behind a shared interface, so the same analysis, ranking, and UI
run unchanged whether the data comes from a cross-platform process API or a
Windows-native collector.

---

## What it does

The tool watches two independent signals and unifies them in one dashboard:

**Process health** (resource monitoring, from outside the process)
- Samples every running process for CPU %, memory (working set / private bytes),
  handle count, and thread count on a 2-second interval.
- Detects **memory leaks** using linear regression over each process's memory
  history — reporting the growth rate (MB/min) and an R² confidence value, so a
  steadily climbing process is flagged while noisy allocation is not.
- Computes **CPU percentiles** (p50/p95/p99), volatility (standard deviation), and
  sustained-load fraction.
- Combines these into a weighted **health score** and ranks every process
  worst-first, with a plain-language reason for each.

**Application latency** (from inside an instrumented service)
- A companion service performs timed operations, wraps each in a `Stopwatch`, and
  reports per-operation latency.
- Operations are ranked by **p99 tail latency** per app, so a degrading endpoint
  surfaces before its average moves — the metric real APM tools rank on.

**Presentation**
- A **floating always-on-top widget** shows the worst app and its severity at a
  glance; clicking it opens the full dashboard.
- The **dashboard** shows both signals as sortable tables, plus a drill-down detail
  panel with live memory and CPU charts (with the fitted regression line drawn over
  the memory series) for any selected process.
- A **scheduled digest** fires once per day at a configurable local hour,
  summarising which apps need attention.

---

## Architecture

The project is organised as a pipeline, with each stage depending only on the one
before it:

```
  Metric source ─▶ Store ─▶ Analysis / Rubric ─▶ UI (widget + dashboard)
   (platform          (bounded         (percentiles,        (Avalonia)
    specific)          ring buffers)    regression,
                                        health score)
```

The key design decision is the **`IMetricSource` interface**. Collectors implement
it; nothing downstream knows or cares how the data was gathered:

- `CrossPlatformMetricSource` — uses `System.Diagnostics.Process`; runs anywhere.
- `WindowsMetricSource` — the Windows-native collector, reading real per-process
  CPU, memory, handle counts, and thread counts on Windows.

An environment check inspects the OS at startup and selects the appropriate
collector. Swapping the cross-platform source for the Windows one required **zero
changes** to the store, analysis, or UI — which is the whole point of the
abstraction.

Supporting design choices:
- **Bounded ring buffers** keep a fixed window of recent history per process, so
  memory stays constant no matter how long the tool runs.
- **Pure, testable analysis** — the statistics layer (`Stats`, `SignalExtractor`,
  `Rubric`) is stateless functions over lists of numbers, independent of where the
  data came from.
- **Pull vs. push** — resource metrics are *pulled* on a timer; latency samples are
  *pushed* as operations complete. The two stores mirror each other, keyed by
  process ID and by app+operation respectively.

---

## Tech stack

- **C# / .NET 10**
- **Avalonia** — cross-platform desktop UI (frameless always-on-top widget,
  DataGrids, MVVM-style data binding)
- **ScottPlot** — live time-series charts
- Standard library for the rest (collections, LINQ, `System.Diagnostics.Process`);
  the ring buffer and statistics are implemented from scratch rather than pulled in
  as dependencies.

---

## Running it

Requires the .NET 10 SDK.

```bash
cd AvaloniaTest
dotnet run
```

The floating widget appears; click it to open the dashboard. On Windows, the
process table populates with live system processes; on macOS/Linux it uses the
cross-platform collector.

---

## Scope and honesty

This is a learning/portfolio project, and the scope is deliberately bounded:

- **Process monitoring** reads real data via the .NET process APIs. Richer
  Windows-native sources (`PerformanceCounter`/WMI for finer metrics, ETW for hang
  detection, the Event Log for crashes, Win32 window enumeration to identify
  desktop apps) are scaffolded behind interfaces as future work, not yet
  implemented.
- **Latency monitoring** measures a built-in **instrumented demo service**, not
  arbitrary third-party apps — because operation latency can only be measured from
  *inside* an application that reports it. The demo stands in for "an app that has
  been instrumented," and demonstrates the full collect → rank pipeline. Monitoring
  a real app would mean adding the same `Stopwatch` instrumentation to that app's
  code.
- The tool **observes and reports** — it deliberately does not try to "optimise" or
  free other processes' memory, which is unreliable from user space and generally
  counterproductive. The value is in detection and ranking; the developer decides
  what to act on.

---

## What I'd build next

- Windows-native collectors: `PerformanceCounter`/WMI metrics, ETW-based hang
  detection, Event Log crash ingestion.
- A drop-in instrumentation helper so a real app can report its own operation
  latency to the monitor.
- Unit tests for the analysis layer (the pure functions make this straightforward —
  feed synthetic climbing-memory data, assert the leak is flagged).
- Persistent history (SQLite) behind the in-memory ring buffers for longer-term
  trends.
packages

dotnet add package Avalonia.Controls.DataGrid
dotnet add package System.Diagnostics.PerformanceCounter
dotnet add package System.Diagnostics.EventLog



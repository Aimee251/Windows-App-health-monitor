# Windows-App-health-monitor

# Windows App Health Monitor

**Windows App Health Monitor** is a cross-platform diagnostic and monitoring agent designed to collect system metrics, trace application behavior, and detect anomalies. It provides insights into performance, resource usage, and application stability without requiring manual administrative intervention in most scenarios.

## 🏗️ Architecture

The system follows a modular **Collector Pattern** that separates concerns between data acquisition, event handling, and processing. The architecture is built on four core abstractions:

### 1. Capability

A **Capability** represents an authorization scope required for a specific monitoring function. The system determines the environment's capability profile during bootstrap to decide which features can be safely enabled.

| Capability | Description | Required for |
| :--- | :--- | :--- |
| **Low** | Basic process enumeration and status checks. | All |
| **Medium** | CPU, memory, and disk metrics. | `MetricCollectors` |
| **High** | Event log, ETW, and detailed diagnostics. | `EventCollectors` |
| **Restricted** | No monitoring capabilities (e.g., UWP app sandboxes). | None |

### 2. Collector Pattern

The system uses two distinct patterns for data collection:

*   **MetricCollectors** (Pull-based):
    *   Periodically poll system providers (Perf Counters, ETW) for metrics.
    *   Implement `Sample()` to return `MetricSample` records.
    *   Examples: `Win32PerfCounterCollector`, `ETWMetricCollector`.

*   **EventCollectors** (Push-based):
    *   Subscribe to real-time event streams (Event Log, ETW).
    *   Raise `AppEvent` records via the `EventRaised` event.
    *   Examples: `EventLogCollector`, `CrashEventCollector`.

### 3. Environment Check

The `IEnvironmentCheck` interface is responsible for detecting and validating the host environment. It reports on:
*   **Platform** (Windows, Linux, macOS)
*   **Elevated privileges**



widget
Checkpoint 2 — bring your logic in. Add a reference from the Avalonia app to your core code (or copy your .cs files into the Avalonia project's structure). Get it compiling with your Rubric, MetricsStore, etc. available.

Checkpoint 3 — a window showing the triage list. A Window with a DataGrid bound to List<Concern>. Run sampling once, display the ranked results. Static first — no live updates yet, just prove the data reaches the screen.

Checkpoint 4 — the tray icon. Add a TrayIcon (menu bar on Mac), start the window hidden, click the icon to toggle it visible. This is the "expand" behavior you asked for.

Checkpoint 5 — live updates. A timer re-runs sampling + triage every 2s and refreshes the grid, marshaled onto the UI thread (that threadpool-thread detail from way back finally matters).

Each checkpoint is a working, runnable state. If any breaks, we fix it before moving on, so you're never debugging five new things at once.

packages
dotnet add package Avalonia.Controls.DataGrid
dotnet add package System.Diagnostics.PerformanceCounter
dotnet add package System.Diagnostics.EventLog



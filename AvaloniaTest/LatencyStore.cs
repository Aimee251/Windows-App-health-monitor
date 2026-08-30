using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public sealed class LatencyStore
{
    private readonly int _capacity;
    private readonly Dictionary<string, RingBuffer<LatencySample>> _byOp = new();

    public LatencyStore(int capacityPerOp) => _capacity = capacityPerOp;

    public void Record(LatencySample s)
    {
        if (!_byOp.TryGetValue(s.OperationName, out var buf))
            _byOp[s.OperationName] = buf = new RingBuffer<LatencySample>(_capacity);
        buf.Add(s);
    }

    // recent durations (ms) for one operation
    public IReadOnlyList<double> Durations(string operation) =>
        _byOp.TryGetValue(operation, out var buf)
            ? buf.Items().Select(x => x.DurationMs).ToList()
            : Array.Empty<double>();

    // every operation name we've seen
    public IReadOnlyList<string> Operations() => _byOp.Keys.ToList();
}
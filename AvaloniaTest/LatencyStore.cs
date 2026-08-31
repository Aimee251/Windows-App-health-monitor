using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public sealed class LatencyStore
{
    private readonly int _capacity;
    // key = "app|operation" so the same op name in two apps stays separate
    private readonly Dictionary<string, RingBuffer<LatencySample>> _byOp = new();

    public LatencyStore(int capacityPerOp) => _capacity = capacityPerOp;

    private static string Key(string app, string op) => $"{app}|{op}";

    public void Record(LatencySample s)
    {
        var key = Key(s.AppName, s.OperationName);
        if (!_byOp.TryGetValue(key, out var buf))
            _byOp[key] = buf = new RingBuffer<LatencySample>(_capacity);
        buf.Add(s);
    }

    // recent samples for one app+operation
    public IReadOnlyList<LatencySample> Samples(string app, string op) =>
        _byOp.TryGetValue(Key(app, op), out var buf) ? buf.Items().ToList() : Array.Empty<LatencySample>();

    // one representative sample per bucket, so callers know the app+op pairs that exist
    public IReadOnlyList<LatencySample> Buckets() =>
        _byOp.Values.Select(b => b.Items().LastOrDefault()).Where(s => s is not null).ToList()!;
}
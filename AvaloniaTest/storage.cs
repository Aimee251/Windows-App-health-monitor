using System;
using System.Collections.Generic;
using System.Linq;

namespace AppHealth.Core;

public sealed class RingBuffer<T>{
    private readonly T[] _items;
    private int _start;
    private int _end;
    private int _count;

    public RingBuffer (int capacity) => _items = new T[capacity];

    public void Add(T item){
        _items[_end] = item;
        _end = (_end + 1) % _items.Length; // a circle, if exceeds the capacity, it will overwrite the oldest value;
        // the newest value store at the end
        
        if(_count == _items.Length){
            _start = (_start + 1)% _items.Length;
        } else {
            _count++;
        }
    }

    public IEnumerable<T> Items (){
        for (int i = 0; i < _count; i++){
            yield return _items[(_start + i) % _items.Length];
        }
    }
}

public sealed class MetricsStore{
        private readonly int _capacity;
        private readonly Dictionary<int, RingBuffer<MetricSample>> _byProcess = new ();

        public MetricsStore(int capacityPerProcess) => _capacity = capacityPerProcess;

        public void Ingest(IReadOnlyList<MetricSample> batch){
            foreach(var s in batch){
                if (!_byProcess.TryGetValue(s.ProcessId, out var buf))
                _byProcess[s.ProcessId] = buf = new RingBuffer<MetricSample>(_capacity);
            buf.Add(s);
            }
        }
// history for one process, oldest -> newest (for charts and trend checks)
        public IReadOnlyList<MetricSample> History(int processId) => _byProcess.TryGetValue(processId, out var buf) ? buf.Items().ToList() : Array.Empty<MetricSample>();
// the most recent sample for every process still alive (for the table)
        public IReadOnlyList<MetricSample> LatestPerProcess() => _byProcess.Values.Select(b => b.Items().LastOrDefault()).Where(s => s is not null).ToList()!;
}
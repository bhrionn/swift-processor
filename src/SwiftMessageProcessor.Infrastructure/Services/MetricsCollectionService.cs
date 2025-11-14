using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for collecting and reporting application metrics
/// </summary>
public class MetricsCollectionService
{
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly ConcurrentDictionary<string, MetricCounter> _counters = new();
    private readonly ConcurrentDictionary<string, MetricTimer> _timers = new();

    public MetricsCollectionService(ILogger<MetricsCollectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Increments a counter metric
    /// </summary>
    public void IncrementCounter(string name, long value = 1, Dictionary<string, string>? tags = null)
    {
        var counter = _counters.GetOrAdd(name, _ => new MetricCounter(name));
        counter.Increment(value);

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                counter.AddTag(tag.Key, tag.Value);
            }
        }
    }

    /// <summary>
    /// Records a timing metric
    /// </summary>
    public void RecordTiming(string name, double milliseconds, Dictionary<string, string>? tags = null)
    {
        var timer = _timers.GetOrAdd(name, _ => new MetricTimer(name));
        timer.Record(milliseconds);

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                timer.AddTag(tag.Key, tag.Value);
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of all metrics
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            Counters = _counters.Values.Select(c => c.GetSnapshot()).ToList(),
            Timers = _timers.Values.Select(t => t.GetSnapshot()).ToList(),
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Resets all metrics
    /// </summary>
    public void Reset()
    {
        _counters.Clear();
        _timers.Clear();
        _logger.LogInformation("Metrics reset");
    }

    /// <summary>
    /// Logs current metrics summary
    /// </summary>
    public void LogMetricsSummary()
    {
        var snapshot = GetSnapshot();
        
        _logger.LogInformation("=== Metrics Summary ===");
        
        foreach (var counter in snapshot.Counters)
        {
            _logger.LogInformation("Counter: {Name} = {Value}", counter.Name, counter.Value);
        }

        foreach (var timer in snapshot.Timers)
        {
            _logger.LogInformation("Timer: {Name} - Count: {Count}, Avg: {Avg}ms, Min: {Min}ms, Max: {Max}ms",
                timer.Name, timer.Count, timer.Average, timer.Min, timer.Max);
        }
    }
}

/// <summary>
/// Counter metric
/// </summary>
public class MetricCounter
{
    private long _value;
    private readonly ConcurrentDictionary<string, string> _tags = new();

    public string Name { get; }

    public MetricCounter(string name)
    {
        Name = name;
    }

    public void Increment(long value = 1)
    {
        Interlocked.Add(ref _value, value);
    }

    public void AddTag(string key, string value)
    {
        _tags[key] = value;
    }

    public CounterSnapshot GetSnapshot()
    {
        return new CounterSnapshot
        {
            Name = Name,
            Value = Interlocked.Read(ref _value),
            Tags = new Dictionary<string, string>(_tags)
        };
    }
}

/// <summary>
/// Timer metric
/// </summary>
public class MetricTimer
{
    private long _count;
    private double _sum;
    private double _min = double.MaxValue;
    private double _max = double.MinValue;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, string> _tags = new();

    public string Name { get; }

    public MetricTimer(string name)
    {
        Name = name;
    }

    public void Record(double milliseconds)
    {
        lock (_lock)
        {
            _count++;
            _sum += milliseconds;
            _min = Math.Min(_min, milliseconds);
            _max = Math.Max(_max, milliseconds);
        }
    }

    public void AddTag(string key, string value)
    {
        _tags[key] = value;
    }

    public TimerSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new TimerSnapshot
            {
                Name = Name,
                Count = _count,
                Sum = _sum,
                Average = _count > 0 ? _sum / _count : 0,
                Min = _min == double.MaxValue ? 0 : _min,
                Max = _max == double.MinValue ? 0 : _max,
                Tags = new Dictionary<string, string>(_tags)
            };
        }
    }
}

/// <summary>
/// Snapshot of all metrics
/// </summary>
public class MetricsSnapshot
{
    public List<CounterSnapshot> Counters { get; set; } = new();
    public List<TimerSnapshot> Timers { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Snapshot of a counter metric
/// </summary>
public class CounterSnapshot
{
    public string Name { get; set; } = string.Empty;
    public long Value { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Snapshot of a timer metric
/// </summary>
public class TimerSnapshot
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Sum { get; set; }
    public double Average { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

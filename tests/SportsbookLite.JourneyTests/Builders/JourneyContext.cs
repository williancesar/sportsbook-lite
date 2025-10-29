namespace SportsbookLite.JourneyTests.Builders;

public sealed class JourneyContext
{
    private readonly Dictionary<string, object> _data = new();
    private readonly List<AssertionResult> _assertions = new();
    private readonly List<string> _executedSteps = new();

    public void Set<T>(string key, T value) where T : notnull
    {
        _data[key] = value;
    }

    public T Get<T>(string key)
    {
        if (!_data.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"Key '{key}' not found in journey context");
        }

        if (value is not T typedValue)
        {
            throw new InvalidCastException($"Value for key '{key}' is not of type {typeof(T).Name}");
        }

        return typedValue;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool Contains(string key) => _data.ContainsKey(key);

    public void RecordStep(string stepName)
    {
        _executedSteps.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {stepName}");
    }

    public void RecordAssertion(string description, bool success, string? failureMessage = null)
    {
        _assertions.Add(new AssertionResult
        {
            Description = description,
            Success = success,
            FailureMessage = failureMessage,
            Timestamp = DateTime.UtcNow
        });
    }

    public void AssertAllSuccessful()
    {
        var failures = _assertions.Where(a => !a.Success).ToList();
        if (failures.Any())
        {
            var failureMessages = string.Join("\n", failures.Select(f => 
                $"  - {f.Description}: {f.FailureMessage}"));
            throw new AssertionException($"Journey validation failed:\n{failureMessages}");
        }
    }

    public IReadOnlyList<string> ExecutedSteps => _executedSteps.AsReadOnly();
    public IReadOnlyList<AssertionResult> Assertions => _assertions.AsReadOnly();

    public string GetJourneyReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Journey Execution Report ===");
        report.AppendLine();
        
        report.AppendLine("Executed Steps:");
        foreach (var step in _executedSteps)
        {
            report.AppendLine($"  {step}");
        }
        
        report.AppendLine();
        report.AppendLine("Assertions:");
        foreach (var assertion in _assertions)
        {
            var status = assertion.Success ? "✓" : "✗";
            report.AppendLine($"  [{status}] {assertion.Description}");
            if (!assertion.Success && !string.IsNullOrEmpty(assertion.FailureMessage))
            {
                report.AppendLine($"      Error: {assertion.FailureMessage}");
            }
        }
        
        report.AppendLine();
        report.AppendLine($"Total Steps: {_executedSteps.Count}");
        report.AppendLine($"Successful Assertions: {_assertions.Count(a => a.Success)}/{_assertions.Count}");
        
        return report.ToString();
    }
}

public sealed class AssertionResult
{
    public required string Description { get; init; }
    public required bool Success { get; init; }
    public string? FailureMessage { get; init; }
    public DateTime Timestamp { get; init; }
}

public class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}
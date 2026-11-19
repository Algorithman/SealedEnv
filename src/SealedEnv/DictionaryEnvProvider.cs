namespace SealedEnv;

/// <summary>
/// In-memory provider over a dictionary — the test seam for generated members.
/// Lookups are ordinal and case-sensitive regardless of platform, so a test
/// asserting <c>"myapp_port"</c> does not satisfy <c>"MYAPP_PORT"</c> behaves the
/// same on every OS.
/// </summary>
public sealed class DictionaryEnvProvider : IEnvProvider
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    public DictionaryEnvProvider(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
    }

    /// <inheritdoc />
    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
}

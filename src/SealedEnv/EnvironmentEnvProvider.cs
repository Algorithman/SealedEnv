namespace SealedEnv;

/// <summary>
/// Default provider reading from the process environment
/// (<see cref="Environment.GetEnvironmentVariable(string)"/>).
/// </summary>
public sealed class EnvironmentEnvProvider : IEnvProvider
{
    /// <summary>Process-wide shared instance; the generated members' default provider.</summary>
    public static EnvironmentEnvProvider Instance { get; } = new();

    private EnvironmentEnvProvider() { }

    /// <inheritdoc />
    public string? Get(string key) => Environment.GetEnvironmentVariable(key);
}

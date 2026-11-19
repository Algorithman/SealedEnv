// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv
// Filename: EnvironmentEnvProvider.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

namespace SealedEnv;

/// <summary>
///     Default provider reading from the process environment
///     (<see cref="Environment.GetEnvironmentVariable(string)" />).
/// </summary>
public sealed class EnvironmentEnvProvider : IEnvProvider
{
    private EnvironmentEnvProvider()
    {
    }

    /// <summary>Process-wide shared instance; the generated members' default provider.</summary>
    public static EnvironmentEnvProvider Instance { get; } = new EnvironmentEnvProvider();

    /// <inheritdoc />
    public string? Get(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }
}

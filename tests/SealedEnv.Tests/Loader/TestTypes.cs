// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: TestTypes.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SealedEnv.Tests.Loader;

/// <summary>File-local enum for <see cref="SealedEnvLoader.Enum{T}" /> tests.</summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
///     Custom type with a *strict* parser (range-checked, throws for every failure
///     mode) so the <see cref="IParsable{T}" /> passthrough can be attacked with
///     malformed values and every exception flavor. Public because xUnit theory
///     methods may not expose internal parameter types.
/// </summary>
public readonly record struct Port(int Value) : IParsable<Port>
{
    static Port IParsable<Port>.Parse(string s, IFormatProvider? provider)
    {
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new FormatException($"'{s}' is not a valid port.");
        }

        if (value > 65535)
        {
            throw new OverflowException($"'{s}' exceeds the port range.");
        }

        return new Port(value);
    }

    static bool IParsable<Port>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Port result)
    {
        if (s is not null && int.TryParse(s, NumberStyles.Integer, provider, out var value) && value > 0 && value <= 65535)
        {
            result = new Port(value);
            return true;
        }

        result = default;
        return false;
    }
}

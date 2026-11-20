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

/// <summary>Exotic underlying types for the <see cref="SealedEnvLoader.Enum{T}" /> route.</summary>
public enum TinyLevel : byte
{
    Low = 1,
    High = 200,
}

public enum WideLevel : long
{
    Deep = 1L << 40,
}

[Flags]
public enum Perms : ulong
{
    Read = 1,
    Write = 2,
    Execute = 4,
}

/// <summary>
///     <see cref="IParsable{T}" /> whose TEXT form contains the default separator
///     ("x,y") — a list of these only works with an escaped or different separator,
///     pinning that the entry escape composes with element parsing.
/// </summary>
public readonly record struct Point(int X, int Y) : IParsable<Point>
{
    private static Point ParseInternal(string s)
    {
        var parts = s.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return new Point(x, y);
        }

        throw new FormatException($"'{s}' is not a valid point.");
    }

    static Point IParsable<Point>.Parse(string s, IFormatProvider? provider) => ParseInternal(s);

    static bool IParsable<Point>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Point result)
    {
        if (s is not null)
        {
            try
            {
                result = ParseInternal(s);
                return true;
            }
            catch (FormatException)
            {
            }
        }

        result = default;
        return false;
    }
}

/// <summary>
///     Reference-type <see cref="IParsable{T}" /> whose Parse deliberately returns
///     null — attacking the loader's contract: a null from a present value must be
///     an Invalid error, never a smuggled null member.
/// </summary>
public sealed class NullParsable : IParsable<NullParsable>
{
    private NullParsable()
    {
    }

    static NullParsable IParsable<NullParsable>.Parse(string s, IFormatProvider? provider) => null!;

    static bool IParsable<NullParsable>.TryParse(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out NullParsable result)
    {
        result = null!;
        return false;
    }
}

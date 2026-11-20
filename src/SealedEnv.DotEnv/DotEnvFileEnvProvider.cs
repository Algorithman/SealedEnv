// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.DotEnv
// Filename: DotEnvFileEnvProvider.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Text;

namespace SealedEnv.DotEnv;

/// <summary>
///     An <see cref="IEnvProvider" /> over a dotenv file: parsed once when the
///     provider is constructed (file and format errors surface immediately, with
///     the line number), then served from memory so per-key lookups stay cheap.
///     Keys are case-sensitive ordinal; the provider never consults the process
///     environment — layer it under <see cref="EnvironmentEnvProvider" /> with
///     <see cref="ChainedEnvProvider" /> for the conventional env-overrides-file
///     precedence.
/// </summary>
public sealed class DotEnvFileEnvProvider : IEnvProvider
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    /// <summary>Parses the dotenv file at <paramref name="path" /> (UTF-8 by default).</summary>
    /// <exception cref="DotEnvFileFormatException">A line of the file is not a valid entry.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public DotEnvFileEnvProvider(string path, Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path cannot be null or whitespace.", nameof(path));
        }

        _values = DotEnvFile.ParseFile(path, encoding);
    }

    /// <summary>Provider over pre-parsed values — the in-memory shape for tests and composition.</summary>
    public DotEnvFileEnvProvider(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
    }

    /// <inheritdoc />
    public string? Get(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }
}

/// <summary>
///     Two providers, first hit wins: the canonical dotenv layering is
///     <c>new ChainedEnvProvider(new EnvironmentEnvProvider(), new DotEnvFileEnvProvider(".env"))</c>
///     — real environment variables override the file, the file fills the gaps.
///     A provider that returns null for every key (or throws — the loader guards
///     against that) can sit at either position.
/// </summary>
public sealed class ChainedEnvProvider : IEnvProvider
{
    private readonly IEnvProvider _first;
    private readonly IEnvProvider _fallback;

    public ChainedEnvProvider(IEnvProvider first, IEnvProvider fallback)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(fallback);
        _first = first;
        _fallback = fallback;
    }

    /// <inheritdoc />
    public string? Get(string key)
    {
        return _first.Get(key) ?? _fallback.Get(key);
    }
}

// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.DotEnv
// Filename: DotEnvFile.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Text;

namespace SealedEnv.DotEnv;

/// <summary>
///     Parser for the dotenv file format (the de-facto <c>.env</c> conventions).
///     Line-based by design — a malformed line is a developer typo in a
///     developer-authored file, so parsing fails fast with the line number instead
///     of silently skipping what would have been configuration.
/// </summary>
/// <remarks>
///     Supported syntax:
///     <list type="bullet">
///         <item><c>KEY=VALUE</c>, one per line; surrounding whitespace trimmed; keys case-sensitive (ordinal).</item>
///         <item>Blank lines and lines whose first non-whitespace character is <c>#</c> are comments.</item>
///         <item><c>export KEY=VALUE</c> — the shell prefix is stripped.</item>
///         <item>Unquoted values strip an inline comment (<c>value # comment</c>); a <c>#</c> not preceded by whitespace stays literal (URLs are safe).</item>
///         <item>Double-quoted values keep <c>#</c> literally and process <c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\\</c>, <c>\"</c> escapes — multiline values are written with <c>\n</c>, not physical newlines.</item>
///         <item>Single-quoted values are fully literal (no comment stripping, no escapes).</item>
///         <item>A duplicate key overwrites the earlier value (last wins).</item>
///     </list>
/// </remarks>
public static class DotEnvFile
{
    /// <summary>Parses dotenv content into an ordinal, case-sensitive lookup.</summary>
    public static IReadOnlyDictionary<string, string?> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimEnd('\r').Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue; // blank or full-line comment
            }

            var entry = trimmed;
            if (entry.StartsWith("export ", StringComparison.Ordinal))
            {
                entry = entry["export ".Length..].TrimStart();
            }

            var separator = entry.IndexOf('=');
            if (separator <= 0)
            {
                // separator == -1: no '='; separator == 0: empty key. Both are typos
                // in a hand-maintained file — report the line, skip nothing.
                throw new DotEnvFileFormatException(i + 1, lines[i]);
            }

            var key = entry[..separator].TrimEnd();
            if (key.Length == 0)
            {
                throw new DotEnvFileFormatException(i + 1, lines[i]);
            }

            values[key] = UnquoteValue(entry[(separator + 1)..].Trim(), i + 1);
        }

        return values;
    }

    /// <summary>Reads and parses a dotenv file (UTF-8 by default).</summary>
    public static IReadOnlyDictionary<string, string?> ParseFile(string path, Encoding? encoding = null)
    {
        // net7.0 has no ArgumentException.ThrowIfNullOrWhiteSpace (added in 8).
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path cannot be null or whitespace.", nameof(path));
        }

        var content = encoding is null ? File.ReadAllText(path) : File.ReadAllText(path, encoding);
        return Parse(content);
    }

    /// <summary>
    ///     Resolves a raw (whitespace-trimmed) value text to its final string.
    ///     Quoting and comment rules differ per quote style — see the type doc.
    /// </summary>
    private static string UnquoteValue(string raw, int lineNumber)
    {
        if (raw.Length == 0)
        {
            return string.Empty; // KEY= is an explicit empty value, not a mistake
        }

        var quote = raw[0];
        if (quote is '"' or '\'')
        {
            // The closing quote must be the last character: trailing content after
            // a quoted value is a typo this parser refuses to guess at.
            if (raw.Length < 2 || raw[^1] != quote)
            {
                throw new DotEnvFileFormatException(lineNumber, raw);
            }

            var body = raw[1..^1];
            return quote == '\'' ? body : UnescapeDoubleQuoted(body, lineNumber);
        }

        // Unquoted: an inline comment is whitespace followed by '#'. A '#' without
        // preceding whitespace stays literal (URLs keep their fragments).
        var commentStart = raw.IndexOf(" #", StringComparison.Ordinal);
        if (commentStart < 0)
        {
            commentStart = raw.IndexOf("\t#", StringComparison.Ordinal);
        }

        return commentStart < 0 ? raw : raw[..commentStart].TrimEnd();
    }

    /// <summary>Escape processing for double-quoted values only.</summary>
    private static string UnescapeDoubleQuoted(string body, int lineNumber)
    {
        if (!body.Contains('\\'))
        {
            return body;
        }

        var builder = new StringBuilder(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c != '\\' || i + 1 >= body.Length)
            {
                builder.Append(c);
                continue;
            }

            var next = body[++i];
            switch (next)
            {
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case '\\': builder.Append('\\'); break;
                case '"': builder.Append('"'); break;
                default:
                    // An unknown escape is a typo, not a literal — same policy as
                    // every other malformed line: fail with the line number.
                    throw new DotEnvFileFormatException(lineNumber, body);
            }
        }

        return builder.ToString();
    }
}

/// <summary>
///     A dotenv line could not be parsed: missing <c>=</c>, empty key, an unterminated
///     or trailed quoted value, or an unknown escape. Carries the 1-based line number
///     so the typo is one glance away.
/// </summary>
public sealed class DotEnvFileFormatException : FormatException
{
    public DotEnvFileFormatException(int lineNumber, string line)
        : base($"Line {lineNumber} of the .env file is not a valid KEY=VALUE entry: \"{line}\".")
    {
        LineNumber = lineNumber;
        Line = line;
    }

    /// <summary>The 1-based line number of the offending entry.</summary>
    public int LineNumber { get; }

    /// <summary>The offending line, as written in the file.</summary>
    public string Line { get; }
}

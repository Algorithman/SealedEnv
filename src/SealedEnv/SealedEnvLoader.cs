// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv
// Filename: SealedEnvLoader.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 12:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace SealedEnv;

/// <summary>
///     Parsing/validation pipeline called by generated <c>Load()</c>/<c>TryLoad()</c> members.
///     The generator emits thin calls into these helpers so generated code stays short and
///     all parsing logic is versioned with the runtime package.
/// </summary>
/// <remarks>
///     Public despite being generator plumbing: generated code is compiled into the
///     <em>consumer's</em> assembly and therefore cannot see internals of this one
///     (the plan's "internal helpers" idea is unworkable across assemblies).
///     Hidden from editors; call the generated members instead of these helpers.
///     Every helper is culture-invariant and never throws for bad values — it records
///     an <see cref="EnvError" /> into <paramref name="errors" /> and returns
///     <c>default</c>; the generated member discards the value and throws
///     <see cref="SealedEnvException" /> when any error was recorded.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SealedEnvLoader
{
    /// <summary>
    ///     Shared lookup/default/empty handling for every typed helper.
    ///     Returns the raw value to parse (env value, attribute default, or an allowed
    ///     empty string), or <see langword="null" /> when the binding resolves to "no
    ///     value" — either unset-and-acceptable or failed (an error is recorded in that
    ///     case; helpers return <c>default</c> for both).
    /// </summary>
    private static string? Resolve(
        IEnvProvider provider,
        string key,
        string displayName,
        string typeName,
        string? @default,
        bool required,
        bool allowEmpty,
        bool emptyAsUnset,
        bool unsetYieldsNull,
        ref List<EnvError> errors)
    {
        ArgumentNullException.ThrowIfNull(provider);

        string? raw;
        try
        {
            raw = provider.Get(key);
        }
        catch (Exception exception)
        {
            // A hostile provider must not break the TryLoad-never-throws contract:
            // the failure is recorded as an ordinary Invalid error and the load
            // keeps collecting the remaining members.
            Record(
                ref errors,
                key,
                EnvErrorCode.Invalid,
                typeName,
                $"Environment provider {provider.GetType().FullName} threw for '{key}': {exception.Message}");
            return null;
        }

        if (raw is { Length: > 0, })
        {
            return raw;
        }

        // Absent (null) or empty ("").
        if (raw is not null && allowEmpty)
        {
            return raw; // explicit empty passes through; string-ish members accept it
        }

        if (raw is not null)
        {
            if (emptyAsUnset)
            {
                raw = null; // dotenv convention: empty behaves exactly like absent
            }
            else if (!unsetYieldsNull)
            {
                // Members that cannot represent absence treat an explicit empty as
                // misconfiguration; a nullable string simply holds "" as its value.
                Record(
                    ref errors,
                    key,
                    EnvErrorCode.Empty,
                    typeName,
                    $"Environment variable '{key}' is set to an empty string and empty values are not allowed for {displayName}.");
                return null;
            }
            else
            {
                return raw; // nullable member: the empty string is a real value
            }
        }

        // Unset (absent or empty-as-unset): default wins, otherwise it is an error
        // — except for members whose type can express absence (string?, future T?).
        if (@default is not null)
        {
            return @default;
        }

        if (required || !unsetYieldsNull)
        {
            Record(
                ref errors,
                key,
                EnvErrorCode.Missing,
                typeName,
                $"Environment variable '{key}' is not set and no default is configured for {displayName}.");
        }

        return null;
    }

    private static void Record(
        ref List<EnvError> errors,
        string key,
        EnvErrorCode code,
        string typeName,
        string message)
    {
        (errors ??= []).Add(new EnvError(key, code, message, typeName));
    }

    private static void RecordInvalid(
        ref List<EnvError> errors,
        string key,
        string displayName,
        string typeName,
        string raw,
        string expected)
    {
        Record(
            ref errors,
            key,
            EnvErrorCode.Invalid,
            typeName,
            $"Environment variable '{key}' has value \"{raw}\" which is not a valid {expected} for {displayName}.");
    }

    // numeric

    public static int Int(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "int", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        RecordInvalid(ref errors, key, displayName, "int", raw, "Int32 value");
        return default;
    }

    public static long Long(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "long", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        RecordInvalid(ref errors, key, displayName, "long", raw, "Int64 value");
        return default;
    }

    public static double Double(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "double", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        // NumberStyles.Float, deliberately WITHOUT AllowThousands: a thousands
        // separator in a machine-readable value is locale-ambiguous (German
        // "1,234" means 1.234) — rejecting it beats a silent 1000x value error.
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        RecordInvalid(ref errors, key, displayName, "double", raw, "Double value");
        return default;
    }

    // boolean: 1/0/true/false/on/off, case-insensitive

    public static bool Bool(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "bool", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        // OrdinalIgnoreCase: culture rules (e.g. the Turkish dotless İ) must not
        // turn an unknown token into a known one.
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1" || raw.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0" || raw.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        RecordInvalid(ref errors, key, displayName, "bool", raw, "boolean value (expected 1, 0, true, false, on, or off)");
        return default;
    }

    // strings

    /// <summary>Non-nullable string: unset and empty are both errors unless a default applies.</summary>
    public static string String(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "string", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        return raw ?? string.Empty; // null only when faulted; the error gates usage
    }

    /// <summary>
    ///     Nullable string: unset resolves to <see langword="null" /> (unless required);
    ///     an explicit empty string is a real value unless <c>EmptyAsUnset</c> maps it
    ///     to unset. Never reports an Empty error.
    /// </summary>
    public static string? NullableString(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        return Resolve(provider, key, displayName, "string", @default, required, allowEmpty, emptyAsUnset, true, ref errors);
    }

    // other BCL types

    public static TimeSpan TimeSpan(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "TimeSpan", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        if (System.TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        RecordInvalid(ref errors, key, displayName, "TimeSpan", raw, "TimeSpan value");
        return default;
    }

    public static Uri? Uri(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "Uri", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return null;
        }

        if (HasExplicitUriScheme(raw) && System.Uri.TryCreate(raw, UriKind.Absolute, out var value))
        {
            return value;
        }

        RecordInvalid(ref errors, key, displayName, "Uri", raw, "absolute URI");
        return null;
    }

    /// <summary>
    ///     Platform-invariant absoluteness check: on Unix, <see cref="System.Uri.TryCreate" />
    ///     with <see cref="UriKind.Absolute" /> also maps POSIX absolute paths ("/abs/path")
    ///     onto file:// URIs, which Windows rejects — a bare absolute check would accept
    ///     different strings per operating system. Require an RFC 3986 scheme in the text
    ///     itself ("ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )":) before consulting the BCL.
    ///     Mirrored by the generator's SVE0004 default check (SealedEnvModel) — keep in sync.
    /// </summary>
    private static bool HasExplicitUriScheme(string raw)
    {
        if (raw.Length == 0 || !char.IsAsciiLetter(raw[0]))
        {
            return false;
        }

        foreach (var ch in raw.AsSpan(1))
        {
            if (ch == ':')
            {
                return true;
            }

            if (!char.IsAsciiLetterOrDigit(ch) && ch is not '+' or '-' or '.')
            {
                return false;
            }
        }

        return false;
    }

    public static Guid Guid(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "Guid", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        // Guid parsing is format-based (RFC 4122), not culture-dependent.
        if (System.Guid.TryParse(raw, out var value))
        {
            return value;
        }

        RecordInvalid(ref errors, key, displayName, "Guid", raw, "GUID");
        return default;
    }

    // generics

    public static T Enum<T>(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
        where T : struct, Enum
    {
        var typeName = typeof(T).Name;
        var raw = Resolve(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default;
        }

        if (System.Enum.TryParse<T>(raw, true, out var value))
        {
            return value;
        }

        RecordInvalid(
            ref errors, key, displayName, typeName, raw,
            $"{typeName} value (expected one of: {string.Join(", ", System.Enum.GetNames<T>())})");
        return default;
    }

    /// <summary>
    ///     Passthrough for any type implementing <see cref="IParsable{T}" />; parsed with
    ///     <see cref="CultureInfo.InvariantCulture" /> so config never depends on locale.
    /// </summary>
    public static T Parsable<T>(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        ref List<EnvError> errors)
        where T : IParsable<T>
    {
        var typeName = typeof(T).Name;
        var raw = Resolve(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default!;
        }

        try
        {
            var value = T.Parse(raw, CultureInfo.InvariantCulture);
            if (value is null)
            {
                // IParsable<T> is user-implementable and may answer null for a
                // present value — that is a broken parser, not a successful load.
                Record(
                    ref errors, key, EnvErrorCode.Invalid, typeName,
                    $"Environment variable '{key}' has value \"{raw}\" but the {typeName} parser returned null for {displayName}.");
                return default!;
            }

            return value;
        }
        catch (Exception exception)
        {
            // Parse is user code and may throw anything; every failure is an
            // ordinary Invalid error so TryLoad survives hostile parsers too.
            RecordInvalid(
                ref errors, key, displayName, typeName, raw,
                $"{typeName} value (parser threw {exception.GetType().Name}: {exception.Message})");
            return default!;
        }
    }

    /// <summary>
    ///     Passthrough for types with a custom parser (<see cref="EnvKeyAttribute.Parser" />).
    ///     Generated code passes the parser as a static method group — AOT-safe, no
    ///     reflection. Failures of any exception flavor degrade into a structured error.
    /// </summary>
    public static T Custom<T>(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        Func<string, T> parse,
        ref List<EnvError> errors)
    {
        var typeName = typeof(T).Name;
        var raw = Resolve(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return default!;
        }

        try
        {
            var value = parse(raw);
            if (value is null)
            {
                // A parser that answers null for a present value would otherwise
                // smuggle a null member into a "successful" load.
                Record(
                    ref errors, key, EnvErrorCode.Invalid, typeName,
                    $"Environment variable '{key}' has value \"{raw}\" but the {typeName} parser returned null for {displayName}.");
                return default!;
            }

            return value;
        }
        catch (Exception exception)
        {
            // Parsers are user code and may throw anything; every failure is an
            // ordinary Invalid error so TryLoad survives hostile parsers too.
            RecordInvalid(
                ref errors, key, displayName, typeName, raw,
                $"{typeName} value (parser threw {exception.GetType().Name}: {exception.Message})");
            return default!;
        }
    }

    // collections

    /// <summary>
    ///     String list split on <paramref name="separator" />, <c>'\' + separator</c>
    ///     escaped to a literal separator, entries whitespace-trimmed. A backslash
    ///     escapes only a following separator; any other backslash is literal.
    ///     Empty entries between separators are preserved.
    /// </summary>
    public static string[] StringArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, "string[]", @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        return raw is null ? [] : SplitEntries(raw, separator);
    }

    /// <summary>
    ///     Parses one list entry. Returns the value plus, on failure, the per-entry
    ///     reason that goes into the member's single aggregate error
    ///     (<see langword="null" /> reason = "use the member-level expectation text").
    /// </summary>
    private delegate (bool Ok, T Value, string? Reason) ElementParser<T>(string raw);

    /// <summary>
    ///     Shared core of every parsed-element list: resolve, split (escape-aware,
    ///     entries trimmed), parse each entry with the scalar rules, and on any
    ///     failure record ONE Invalid error for the member listing every bad entry
    ///     with its 1-based index and reason. EnvError is per-key, so a variable
    ///     contributes at most one error no matter how many entries are malformed.
    /// </summary>
    private static T[] ArrayCore<T>(
        IEnvProvider provider, string key, string displayName, string typeName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator, ElementParser<T> parseEntry, string expectation,
        ref List<EnvError> errors)
    {
        var raw = Resolve(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset, false, ref errors);
        if (raw is null)
        {
            return [];
        }

        var entries = SplitEntries(raw, separator);
        var values = new T[entries.Length];
        List<string>? failures = null;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            (var ok, var value, var reason) = parseEntry(entry);
            if (ok)
            {
                values[i] = value;
                continue;
            }

            (failures ??= new List<string>()).Add($"entry {i + 1} \"{entry}\": {reason ?? expectation}");
        }

        if (failures is not null)
        {
            Record(
                ref errors, key, EnvErrorCode.Invalid, typeName,
                $"Environment variable '{key}' has value \"{raw}\" which is not valid for {displayName} — "
                + string.Join("; ", failures) + ".");
            return [];
        }

        return values;
    }

    public static int[] IntArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "int[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? (true, value, (string?)null)
                : (false, default, (string?)null),
            "not a valid Int32 value",
            ref errors);
    }

    public static long[] LongArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "long[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? (true, value, (string?)null)
                : (false, default, (string?)null),
            "not a valid Int64 value",
            ref errors);
    }

    public static double[] DoubleArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "double[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            // Same strict styles as the scalar Double: no AllowThousands.
            static raw => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? (true, value, (string?)null)
                : (false, default, (string?)null),
            "not a valid Double value",
            ref errors);
    }

    public static bool[] BoolArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "bool[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw => raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1" || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
                ? (true, true, (string?)null)
                : raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0" || raw.Equals("off", StringComparison.OrdinalIgnoreCase)
                    ? (true, false, (string?)null)
                    : (false, default, "expected 1, 0, true, false, on, or off"),
            "not a valid boolean value",
            ref errors);
    }

    public static TimeSpan[] TimeSpanArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "TimeSpan[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw => System.TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var value)
                ? (true, value, (string?)null)
                : (false, default, (string?)null),
            "not a valid TimeSpan value",
            ref errors);
    }

    public static Uri[] UriArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "Uri[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            // Same platform-invariant absoluteness rule as the scalar Uri.
            static raw => HasExplicitUriScheme(raw) && System.Uri.TryCreate(raw, UriKind.Absolute, out var value)
                ? (true, value, (string?)null)
                : (false, default(Uri)!, "expected an absolute URI"),
            "not a valid absolute URI",
            ref errors);
    }

    public static Guid[] GuidArray(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
    {
        return ArrayCore(provider, key, displayName, "Guid[]", @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw => System.Guid.TryParse(raw, out var value) ? (true, value, (string?)null) : (false, default, (string?)null),
            "not a valid GUID",
            ref errors);
    }

    /// <summary>
    ///     Enum list: each entry keeps the scalar <c>Enum.TryParse</c> semantics
    ///     (member names case-insensitively, numeric values, flag combinations).
    ///     A flag combo containing the separator must be escaped (<c>"Read\,Write"</c>)
    ///     or the separator changed — the list split always happens first.
    /// </summary>
    public static T[] EnumArray<T>(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
        where T : struct, Enum
    {
        var typeName = typeof(T).Name + "[]";
        return ArrayCore<T>(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw => System.Enum.TryParse<T>(raw, true, out var value)
                ? (true, value, (string?)null)
                : (false, default, $"expected one of: {string.Join(", ", System.Enum.GetNames<T>())}"),
            $"not a valid {typeof(T).Name} value",
            ref errors);
    }

    /// <summary>
    ///     List of any <see cref="IParsable{T}" /> element type, invariant culture.
    ///     Parsers are user code: every exception and every null answer for a present
    ///     entry degrades into the entry's reason in the member's single error.
    /// </summary>
    public static T[] ParsableArray<T>(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator,
        ref List<EnvError> errors)
        where T : IParsable<T>
    {
        var typeName = typeof(T).Name + "[]";
        return ArrayCore<T>(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset,
            separator,
            static raw =>
            {
                try
                {
                    var value = T.Parse(raw, CultureInfo.InvariantCulture);
                    return value is null
                        ? (false, default(T)!, "the parser returned null")
                        : (true, value, (string?)null);
                }
                catch (Exception exception)
                {
                    return (false, default(T)!, $"the parser threw {exception.GetType().Name}: {exception.Message}");
                }
            },
            $"not a valid {typeof(T).Name} value",
            ref errors);
    }

    /// <summary>
    ///     List with a custom per-entry parser (<see cref="EnvKeyAttribute.Parser" />
    ///     exposing <c>public static T Parse(string)</c> for the ELEMENT type).
    ///     Same user-code contract as <see cref="Parsable{T}" />: any exception or
    ///     null answer is an entry reason, never an escaped exception.
    /// </summary>
    public static T[] CustomArray<T>(
        IEnvProvider provider, string key, string displayName,
        string? @default, bool required, bool allowEmpty, bool emptyAsUnset,
        char separator, Func<string, T> parse,
        ref List<EnvError> errors)
    {
        ArgumentNullException.ThrowIfNull(parse);
        var typeName = typeof(T).Name + "[]";
        return ArrayCore<T>(provider, key, displayName, typeName, @default, required, allowEmpty, emptyAsUnset,
            separator,
            // Non-static on purpose: the lambda captures the caller's parser delegate.
            raw =>
            {
                try
                {
                    var value = parse(raw);
                    return value is null ? (false, default(T)!, "the parser returned null") : (true, value, (string?)null);
                }
                catch (Exception exception)
                {
                    return (false, default(T)!, $"the parser threw {exception.GetType().Name}: {exception.Message}");
                }
            },
            "not a valid entry",
            ref errors);
    }

    internal static string[] SplitEntries(string raw, char separator)
    {
        var entries = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && i + 1 < raw.Length && raw[i + 1] == separator)
            {
                current.Append(separator);
                i++;
            }
            else if (c == separator)
            {
                entries.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        entries.Add(current.ToString().Trim());
        return [.. entries,];
    }
}

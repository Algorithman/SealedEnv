// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: ParsedArrayTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Globalization;

namespace SealedEnv.Tests.Loader;

/// <summary>
///     Parsed-element lists: <c>SealedEnvLoader.*Array</c> helpers split with the
///     escape-aware splitter and parse each entry with the SAME rule as the scalar
///     helper of that kind (strict double, bool tokens, absolute Uri, invariant
///     culture). Every malformed entry is folded into ONE Invalid error for the
///     member — EnvError is per-key — naming each bad entry with its 1-based index.
/// </summary>
public class ParsedArrayTests
{
    private static List<EnvError> Errors() => [];

    // ---- happy paths, one per element kind ----

    [Fact]
    public void IntArray_unset_without_default_is_missing()
    {
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(TestEnv.Empty().AsProvider(), "K", "C.Ports", null, false, false, true, ',', ref errors);

        // unset without default: [] plus the Missing error (arrays cannot express absence)
        Assert.Equal([], values);
        Assert.Equal(EnvErrorCode.Missing, Assert.Single(errors).Code);
    }

    [Fact]
    public void IntArray_splits_and_parses()
    {
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(
            TestEnv.With("K", "80, 443 ,8080").AsProvider(), "K", "C.Ports", null, false, false, true, ',', ref errors);

        Assert.Equal([80, 443, 8080,], values);
        Assert.Empty(errors);
    }

    [Fact]
    public void IntArray_default_is_split_element_wise()
    {
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(
            TestEnv.Empty().AsProvider(), "K", "C.Ports", "1, 2", false, false, true, ',', ref errors);

        Assert.Equal([1, 2,], values);
        Assert.Empty(errors);
    }

    [Fact]
    public void Long_and_double_arrays_parse()
    {
        var errors = Errors();
        Assert.Equal([1L << 40,], SealedEnvLoader.LongArray(TestEnv.With("K", "1099511627776").AsProvider(), "K", "C.B", null, false, false, true, ',', ref errors));

        // Separator ';' here: a comma inside an entry must not split, and the strict
        // scalar rule (no AllowThousands) must hold per entry.
        Assert.Equal([1.5, 2.25,], SealedEnvLoader.DoubleArray(TestEnv.With("K", "1.5;2.25").AsProvider(), "K", "C.R", null, false, false, true, ';', ref errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void BoolArray_keeps_the_scalar_token_set()
    {
        var errors = Errors();
        var values = SealedEnvLoader.BoolArray(
            TestEnv.With("K", "on, OFF, 1, False").AsProvider(), "K", "C.Flags", null, false, false, true, ',', ref errors);

        Assert.Equal([true, false, true, false,], values);
        Assert.Empty(errors);
    }

    [Fact]
    public void TimeSpan_and_Guid_arrays_parse()
    {
        var errors = Errors();
        Assert.Equal(
            [TimeSpan.FromMinutes(1),],
            SealedEnvLoader.TimeSpanArray(TestEnv.With("K", "00:01:00").AsProvider(), "K", "C.T", null, false, false, true, ',', ref errors));
        Assert.Equal(
            [new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),],
            SealedEnvLoader.GuidArray(TestEnv.With("K", "6f9619ff-8b86-d011-b42d-00c04fc964ff").AsProvider(), "K", "C.G", null, false, false, true, ',', ref errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void UriArray_keeps_the_absolute_only_rule()
    {
        var errors = Errors();
        var values = SealedEnvLoader.UriArray(
            TestEnv.With("K", "https://a.example.com, /relative").AsProvider(), "K", "C.U", null, false, false, true, ',', ref errors);

        // The /relative entry is rejected exactly like the scalar Uri (even on Unix).
        Assert.Equal([], values);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Contains("entry 2 \"/relative\"", error.Message);
        Assert.Contains("absolute URI", error.Message);
    }

    [Fact]
    public void EnumArray_parses_members_numerics_and_flag_combos()
    {
        var errors = Errors();
        var values = SealedEnvLoader.EnumArray<LogLevel>(
            TestEnv.With("K", "warning, 3, Debug").AsProvider(), "K", "C.L", null, false, false, true, ',', ref errors);

        Assert.Equal([LogLevel.Warning, LogLevel.Error, LogLevel.Debug,], values);
        Assert.Empty(errors);
    }

    [Fact]
    public void EnumArray_flag_combo_needs_an_escape_or_a_different_separator()
    {
        // "Read, Write" intended as ONE flag combo: the list split runs first, so the
        // combo's comma must be escaped (or the separator changed). The escape wins:
        var errors = Errors();
        var values = SealedEnvLoader.EnumArray<Perms>(
            TestEnv.With("K", @"Read\,Write, Execute").AsProvider(), "K", "C.P", null, false, false, true, ',', ref errors);

        Assert.Equal([Perms.Read | Perms.Write, Perms.Execute,], values);
        Assert.Empty(errors);

        // Unescaped, the split yields two entries that parse individually.
        var split = SealedEnvLoader.EnumArray<Perms>(
            TestEnv.With("K", "Read, Write").AsProvider(), "K", "C.P", null, false, false, true, ',', ref errors);
        Assert.Equal([Perms.Read, Perms.Write,], split);
    }

    [Fact]
    public void ParsableArray_keeps_invariant_culture_and_parser_contract()
    {
        var errors = Errors();
        var values = SealedEnvLoader.ParsableArray<Port>(
            TestEnv.With("K", "80,443").AsProvider(), "K", "C.G", null, false, false, true, ',', ref errors);

        Assert.Equal([new Port(80), new Port(443),], values);
        Assert.Empty(errors);

        // Port's strict parser throws FormatException for "0" — folded into the entry reason.
        var failing = SealedEnvLoader.ParsableArray<Port>(
            TestEnv.With("K", "80,0").AsProvider(), "K", "C.G", null, false, false, true, ',', ref errors);
        Assert.Equal([], failing);
        var error = Assert.Single(errors);
        Assert.Contains("entry 2 \"0\"", error.Message);
        Assert.Contains("parser threw FormatException", error.Message);
    }

    [Fact]
    public void Point_entries_survive_via_the_escape()
    {
        // Point's text form contains a comma ("1,2"), so a Point list needs '\,'
        // escapes or a different separator — here both: ';' separator, entries "1,2".
        var errors = Errors();
        var values = SealedEnvLoader.ParsableArray<Point>(
            TestEnv.With("K", "1,2; 3,4").AsProvider(), "K", "C.Points", null, false, false, true, ';', ref errors);

        Assert.Equal([new Point(1, 2), new Point(3, 4),], values);
        Assert.Empty(errors);
    }

    [Fact]
    public void CustomArray_parses_each_entry_with_the_supplied_parser()
    {
        var errors = Errors();
        var values = SealedEnvLoader.CustomArray<int>(
            TestEnv.With("K", "1,2,3").AsProvider(), "K", "C.Doubled", null, false, false, true, ',',
            static raw => int.Parse(raw, CultureInfo.InvariantCulture) * 2,
            ref errors);

        Assert.Equal([2, 4, 6,], values);
        Assert.Empty(errors);
    }

    // ---- failure aggregation: ONE error per member, every bad entry named ----

    [Fact]
    public void Multiple_bad_entries_are_reported_in_one_error_with_indexes()
    {
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(
            TestEnv.With("K", "80,abc,x").AsProvider(), "K", "C.Ports", null, false, false, true, ',', ref errors);

        Assert.Equal([], values);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("int[]", error.TypeName);
        Assert.Contains("has value \"80,abc,x\"", error.Message);
        Assert.Contains("entry 2 \"abc\": not a valid Int32 value", error.Message);
        Assert.Contains("entry 3 \"x\": not a valid Int32 value", error.Message);
        Assert.Contains("for C.Ports", error.Message);
    }

    [Fact]
    public void An_empty_entry_is_invalid_for_parsed_lists()
    {
        // Empty entries are preserved by the split; a parsed element cannot parse ""
        // — deterministic Invalid, no silent hole-filling.
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(
            TestEnv.With("K", "80,,443").AsProvider(), "K", "C.Ports", null, false, false, true, ',', ref errors);

        Assert.Equal([], values);
        Assert.Contains("entry 2 \"\": not a valid Int32 value", Assert.Single(errors).Message);
    }

    [Fact]
    public void Null_returning_element_parsers_are_rejected()
    {
        var errors = Errors();
        var values = SealedEnvLoader.ParsableArray<NullParsable>(
            TestEnv.With("K", "anything").AsProvider(), "K", "C.N", null, false, false, true, ',', ref errors);

        Assert.Equal([], values);
        Assert.Contains("entry 1 \"anything\": the parser returned null", Assert.Single(errors).Message);
    }

    [Fact]
    public void A_throwing_element_parser_never_escapes()
    {
        var errors = Errors();
        var values = SealedEnvLoader.CustomArray<string>(
            TestEnv.With("K", "fine,boom").AsProvider(), "K", "C.X", null, false, false, true, ',',
            static raw => raw == "boom" ? throw new InvalidOperationException("nope") : raw,
            ref errors);

        Assert.Equal([], values);
        var error = Assert.Single(errors);
        Assert.Contains("entry 2 \"boom\": the parser threw InvalidOperationException: nope", error.Message);
    }

    [Fact]
    public void Default_with_a_bad_entry_is_reported_with_its_index()
    {
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(
            TestEnv.Empty().AsProvider(), "K", "C.Ports", "1,two", false, false, true, ',', ref errors);

        Assert.Equal([], values);
        Assert.Contains("entry 2 \"two\": not a valid Int32 value", Assert.Single(errors).Message);
    }

    // ---- Resolve interplay (Missing / Empty / EmptyAsUnset / AllowEmpty) ----

    [Fact]
    public void Unset_and_empty_behave_like_the_scalar_route()
    {
        var errors = Errors();

        // EmptyAsUnset=true (default): "" behaves exactly like unset → default applies.
        var defaulted = SealedEnvLoader.IntArray(
            TestEnv.With("K", "").AsProvider(), "K", "C.Ports", "5", false, false, true, ',', ref errors);
        Assert.Equal([5,], defaulted);
        Assert.Empty(errors);

        // EmptyAsUnset=false: "" is misconfiguration (arrays cannot express absence).
        var empty = SealedEnvLoader.IntArray(
            TestEnv.With("K2", "").AsProvider(), "K2", "C.Ports", null, false, false, false, ',', ref errors);
        Assert.Equal([], empty);
        Assert.Equal(EnvErrorCode.Empty, Assert.Single(errors).Code);
    }

    [Fact]
    public void AllowEmpty_hands_the_empty_string_to_the_element_parser()
    {
        // AllowEmpty lets "" through as a value; for a parsed list that value is one
        // empty entry — which cannot parse. String lists keep the pinned [""] result.
        var errors = Errors();
        var parsed = SealedEnvLoader.IntArray(
            TestEnv.With("K", "").AsProvider(), "K", "C.Ports", null, false, true, true, ',', ref errors);
        Assert.Equal([], parsed);
        Assert.Contains("entry 1 \"\"", Assert.Single(errors).Message);

        var strings = SealedEnvLoader.StringArray(
            TestEnv.With("K2", "").AsProvider(), "K2", "C.Hosts", null, false, true, true, ',', ref errors);
        Assert.Equal(["",], strings);
    }

    [Fact]
    public void Hostile_payloads_stay_deterministic()
    {
        var errors = Errors();
        var values = SealedEnvLoader.IntArray(
            TestEnv.With("K", "  \t ,\0,-2147483648,2147483647").AsProvider(), "K", "C.Ports", null, false, false, true, ',', ref errors);

        // entry 1 whitespace-only → invalid; entry 2 NUL → invalid; the int extremes parse.
        Assert.Equal([], values);
        var error = Assert.Single(errors);
        Assert.Contains("entry 1", error.Message);
        Assert.Contains("entry 2", error.Message);
        Assert.DoesNotContain("entry 3", error.Message);
        Assert.DoesNotContain("entry 4", error.Message);
    }
}

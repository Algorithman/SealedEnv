// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: NumericLoaderTests.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Globalization;

namespace SealedEnv.Tests.Loader;

/// <summary>
///     Type integrity for the numeric helpers: well-formed values parse, anything
///     malformed is rejected as an <see cref="EnvErrorCode.Invalid" /> error — never a
///     crash, never a silently-wrong number, and never locale-dependent.
/// </summary>
public class NumericLoaderTests
{
    // ---- int: accepted forms ----

    [Theory]
    [InlineData("8080", 8080)]
    [InlineData("0", 0)]
    [InlineData("+5", 5)] // signed with explicit plus
    [InlineData("-1", -1)]
    [InlineData("007", 7)] // leading zeros are not octal
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    [InlineData(" 8080 ", 8080)] // .NET NumberStyles.Integer allows surrounding whitespace
    public void Int_parses_well_formed_values(string value, int expected)
    {
        var env = TestEnv.With("MYAPP_PORT", value);

        Assert.Equal(expected, env.Int("MYAPP_PORT"));
        Assert.Empty(env.Errors);
    }

    // ---- int: malformed / hostile forms ----

    [Theory]
    [InlineData("abc")] // not a number
    [InlineData("true")] // wrong type injected into a numeric slot
    [InlineData("8080.5")] // fractional
    [InlineData("1e3")] // scientific notation (Double's job, not Int's)
    [InlineData("0x10")] // hex is not allowed under NumberStyles.Integer
    [InlineData("1,234")] // thousands separator not allowed under Integer
    [InlineData("80 80")] // internal whitespace
    [InlineData("8_080")] // digit separators are C# literal syntax, not env syntax
    [InlineData("٤٢")] // Arabic-Indic digits: invariant culture accepts ASCII only
    [InlineData("٢١٠")] // more non-ASCII digits
    [InlineData("2147483648")] // int.MaxValue + 1 → overflow
    [InlineData("-2147483649")] // int.MinValue - 1 → overflow
    [InlineData("99999999999999999999")] // far beyond Int32
    public void Int_rejects_malformed_values_with_a_structured_error(string value)
    {
        var env = TestEnv.With("MYAPP_PORT", value);

        Assert.Equal(0, env.Int("MYAPP_PORT")); // sentinel; the error gates usage
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("MYAPP_PORT", error.Key);
        Assert.Equal("int", error.TypeName);
        Assert.Contains($"\"{value}\"", error.Message);
    }

    [Fact]
    public void Int_uses_a_malformed_default_only_as_last_resort_and_reports_it()
    {
        // Compile-time diagnostics (SVE0004) catch this later; at runtime the same
        // parse pipeline must still produce a structured error, not a crash.
        var env = TestEnv.Empty();

        Assert.Equal(0, env.Int("MYAPP_PORT", "abc"));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Contains("\"abc\"", env.SingleError.Message);
    }

    // ---- long ----

    [Theory]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("8080", 8080L)]
    public void Long_parses_its_full_range(string value, long expected)
    {
        var env = TestEnv.With("MYAPP_BIG", value);

        Assert.Equal(expected, env.Long("MYAPP_BIG"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("9223372036854775808")] // long.MaxValue + 1
    [InlineData("abc")]
    [InlineData("1.0")]
    public void Long_rejects_malformed_values(string value)
    {
        var env = TestEnv.With("MYAPP_BIG", value);

        Assert.Equal(0, env.Long("MYAPP_BIG"));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Equal("long", env.SingleError.TypeName);
    }

    [Fact]
    public void Long_rejects_values_that_would_overflow_int_into_the_wrong_slot()
    {
        // 2147483648 overflows int but is a valid long — proves the helpers do not
        // share one parser with lossy conversions.
        var env = TestEnv.With("MYAPP_PORT", "2147483648");

        Assert.Equal(0, env.Int("MYAPP_PORT"));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
    }

    // ---- double ----

    [Theory]
    [InlineData("1.5")]
    [InlineData("-2.75")]
    [InlineData(".5")] // leading decimal point
    [InlineData("1e3")] // exponent
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Double_parses_its_documented_forms(string value)
    {
        var env = TestEnv.With("MYAPP_RATE", value);
        var parsed = env.Double("MYAPP_RATE");

        Assert.Empty(env.Errors);
        if (value == "NaN")
        {
            Assert.True(double.IsNaN(parsed));
        }
        else if (value == "Infinity")
        {
            Assert.True(double.IsPositiveInfinity(parsed));
        }
        else if (value == "-Infinity")
        {
            Assert.True(double.IsNegativeInfinity(parsed));
        }
        else
        {
            Assert.Equal(double.Parse(value, CultureInfo.InvariantCulture), parsed);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5.5")]
    [InlineData("80 80")]
    [InlineData("--1.5")]
    [InlineData("1,234.5")] // thousands separators are deliberately rejected (see loader)
    [InlineData("1,2,3")] // BCL AllowThousands would silently parse this as 123
    public void Double_rejects_malformed_values(string value)
    {
        var env = TestEnv.With("MYAPP_RATE", value);

        Assert.Equal(0, env.Double("MYAPP_RATE"));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Equal("double", env.SingleError.TypeName);
    }

    [Fact]
    public void Double_is_culture_invariant_even_when_the_process_is_german()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Under de-DE, "1.5" is a thousands-grouped "15" and "1,5" means 1.5 —
            // config must ignore the machine locale entirely: dot-decimal always
            // parses, comma-groupings always fail, whichever machine runs the app.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var dotEnv = TestEnv.With("MYAPP_RATE", "1.5");
            Assert.Equal(1.5, dotEnv.Double("MYAPP_RATE"));
            Assert.Empty(dotEnv.Errors);

            var commaEnv = TestEnv.With("MYAPP_RATE", "1,234.5");
            Assert.Equal(0, commaEnv.Double("MYAPP_RATE"));
            Assert.Equal(EnvErrorCode.Invalid, commaEnv.SingleError.Code);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Int_is_culture_invariant_even_when_the_process_uses_arabic_digits()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            var env = TestEnv.With("MYAPP_PORT", "42");
            Assert.Equal(42, env.Int("MYAPP_PORT"));
            Assert.Empty(env.Errors);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

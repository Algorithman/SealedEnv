using SealedEnv.Tests.Generated;

namespace SealedEnv.Tests.Loader;

/// <summary>
///     Attacks the parser routes' contract. Parsers are user code: they may throw
///     ANY exception and may return null. The loader must convert every failure
///     into an ordinary <see cref="EnvError" /> — a throw escaping to the caller or
///     a null smuggled into a "successful" load is a bug.
/// </summary>
public class ParserContractTests
{
    private static readonly DictionaryEnvProvider Provider = new(new Dictionary<string, string?>
    {
        ["MYAPP_VAT"] = "1.0",
        ["MYAPP_NULLPARSABLE"] = "anything",
    });

    private sealed class BrokenParser
    {
        public static VatRate Parse(string raw)
        {
            throw new InvalidOperationException("the parser is broken");
        }
    }

    private sealed class NullParser
    {
        public static VatRate Parse(string raw) => null!;
    }

    [Fact]
    public void A_parser_throwing_an_unusual_exception_is_an_invalid_error()
    {
        // FormatException/OverflowException/ArgumentException were the historic
        // catch list — anything else used to escape and break TryLoad.
        var errors = new List<EnvError>();
        var value = SealedEnvLoader.Custom<VatRate>(
            Provider, "MYAPP_VAT", "T.Vat", null, false, false, true, BrokenParser.Parse, ref errors);

        Assert.Null(value);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Contains("parser threw InvalidOperationException", error.Message);
        Assert.Contains("the parser is broken", error.Message);
    }

    [Fact]
    public void A_parser_returning_null_is_an_invalid_error_not_a_smuggled_member()
    {
        var errors = new List<EnvError>();
        var value = SealedEnvLoader.Custom<VatRate>(
            Provider, "MYAPP_VAT", "T.Vat", null, false, false, true, NullParser.Parse, ref errors);

        Assert.Null(value);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Contains("parser returned null", error.Message);
    }

    [Fact]
    public void A_parsable_implementation_returning_null_is_an_invalid_error()
    {
        var env = TestEnv.With("MYAPP_NULLPARSABLE", "anything");

        var value = env.Parsable<NullParsable>("MYAPP_NULLPARSABLE");

        Assert.Null(value);
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Contains("parser returned null", env.SingleError.Message);
    }

    // ---- exotic enum underlying types (TryParse must never throw) ----

    [Fact]
    public void Byte_backed_enum_rejects_values_beyond_its_range()
    {
        var env = TestEnv.With("MYAPP_LEVEL", "999");

        Assert.Equal((TinyLevel)0, env.Enum<TinyLevel>("MYAPP_LEVEL")); // default(TinyLevel) = 0, undefined member
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
    }

    [Fact]
    public void Byte_backed_enum_accepts_its_defined_range()
    {
        var env = TestEnv.With("MYAPP_LEVEL", "200");

        Assert.Equal(TinyLevel.High, env.Enum<TinyLevel>("MYAPP_LEVEL"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Long_backed_enum_accepts_far_beyond_int_range()
    {
        var env = TestEnv.With("MYAPP_LEVEL", "1099511627776");

        Assert.Equal(WideLevel.Deep, env.Enum<WideLevel>("MYAPP_LEVEL"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("Read, Write", Perms.Read | Perms.Write)] // flag combo, token edges trimmed
    [InlineData("read,write", Perms.Read | Perms.Write)] // case-insensitive
    [InlineData("5", Perms.Read | Perms.Execute)] // numeric combination, undefined-but-in-range
    public void Ulong_flag_enums_parse_combinations(string value, Perms expected)
    {
        var env = TestEnv.With("MYAPP_PERMS", value);

        Assert.Equal(expected, env.Enum<Perms>("MYAPP_PERMS"));
        Assert.Empty(env.Errors);
    }
}

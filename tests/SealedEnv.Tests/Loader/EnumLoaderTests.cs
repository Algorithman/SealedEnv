using System.Globalization;

namespace SealedEnv.Tests.Loader;

/// <summary>
/// Type integrity for enums: member names parse case-insensitively, numeric values
/// map onto the enum exactly like <see cref="Enum.Parse"/> — including the
/// surprising corners, which are pinned here rather than left undefined.
/// </summary>
public class EnumLoaderTests
{
    [Theory]
    [InlineData("Info", LogLevel.Info)]
    [InlineData("info", LogLevel.Info)] // case-insensitive by design
    [InlineData("INFO", LogLevel.Info)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData(" warning", LogLevel.Warning)] // Enum.Parse trims the token's edges
    [InlineData("warning ", LogLevel.Warning)]
    [InlineData("3", LogLevel.Error)] // numeric member value
    [InlineData("0", LogLevel.Debug)]
    public void Enum_parses_member_names_and_numeric_values(string value, LogLevel expected)
    {
        var env = TestEnv.With("MYAPP_LEVEL", value);

        Assert.Equal(expected, env.Enum<LogLevel>("MYAPP_LEVEL"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("Fatal")] // looks plausible, is not a member
    [InlineData("in fo")] // internal whitespace is not trimmed away
    [InlineData("true")] // bool token injected into an enum slot
    [InlineData("INFO INFO")]
    public void Enum_rejects_unknown_names_with_a_listing_of_valid_ones(string value)
    {
        var env = TestEnv.With("MYAPP_LEVEL", value);

        Assert.Equal(default(LogLevel), env.Enum<LogLevel>("MYAPP_LEVEL"));
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("MYAPP_LEVEL", error.Key);
        Assert.Equal("LogLevel", error.TypeName);
        Assert.Contains("\"" + value + "\"", error.Message);
        Assert.Contains("Debug, Info, Warning, Error", error.Message);
    }

    [Theory]
    [InlineData("9")] // defined members end at 3
    [InlineData("-1")] // any in-range underlying value passes
    public void Undefined_numeric_values_are_accepted_like_Enum_Parse(string value)
    {
        // Pinned: System.Enum.TryParse accepts any number in the underlying type's
        // range, defined member or not (flag combinations rely on this). We do not
        // silently diverge from Enum.Parse semantics.
        var env = TestEnv.With("MYAPP_LEVEL", value);

        Assert.Equal((LogLevel)int.Parse(value, CultureInfo.InvariantCulture), env.Enum<LogLevel>("MYAPP_LEVEL"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Enum_default_is_parsed_through_the_same_pipeline()
    {
        var env = TestEnv.Empty();

        Assert.Equal(LogLevel.Warning, env.Enum<LogLevel>("MYAPP_LEVEL", @default: "Warning"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Enum_malformed_default_is_reported_not_crashed()
    {
        var env = TestEnv.Empty();

        env.Enum<LogLevel>("MYAPP_LEVEL", @default: "Fatal");
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
    }
}

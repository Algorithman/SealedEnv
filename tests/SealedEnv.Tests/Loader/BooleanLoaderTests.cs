using System.Globalization;

namespace SealedEnv.Tests.Loader;

/// <summary>
/// Type integrity for the boolean helper: exactly the six documented tokens
/// (1/0/true/false/on/off, case-insensitive), everything else rejected. Bool is
/// deliberately stricter than the numeric helpers — no surrounding whitespace,
/// no "yes"/"enabled" and similar aliases.
/// </summary>
public class BooleanLoaderTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("1")]
    [InlineData("on")]
    [InlineData("ON")]
    public void Bool_true_tokens(string value)
    {
        var env = TestEnv.With("MYAPP_VERBOSE", value);

        Assert.True(env.Bool("MYAPP_VERBOSE"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("off")]
    [InlineData("OFF")]
    [InlineData("oFf")]
    public void Bool_false_tokens(string value)
    {
        var env = TestEnv.With("MYAPP_VERBOSE", value);

        Assert.False(env.Bool("MYAPP_VERBOSE"));
        Assert.Empty(env.Errors);
    }

    // The classic footguns people put in CI configs / dashboards.
    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("enabled")]
    [InlineData("disabled")]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("t")]
    [InlineData("Y")]
    [InlineData(" true")] // leading whitespace: bool tokens are exact, unlike numerics
    [InlineData("true ")] // trailing whitespace
    [InlineData("on off")]
    [InlineData("0.0")] // a numeric-looking value that is not a bool token
    public void Bool_rejects_everything_outside_the_documented_tokens(string value)
    {
        var env = TestEnv.With("MYAPP_VERBOSE", value);

        Assert.False(env.Bool("MYAPP_VERBOSE")); // sentinel false; error gates usage
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("MYAPP_VERBOSE", error.Key);
        Assert.Equal("bool", error.TypeName);
        Assert.Contains($"\"{value}\"", error.Message);
    }

    [Fact]
    public void Bool_error_message_names_the_accepted_tokens()
    {
        var env = TestEnv.With("MYAPP_VERBOSE", "maybe");

        Assert.False(env.Bool("MYAPP_VERBOSE"));
        Assert.Contains("1, 0, true, false, on, or off", env.SingleError.Message);
    }

    [Fact]
    public void Bool_is_immune_to_turkish_casing()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // tr-TR uppercases 'i' to 'İ'; a current-culture comparison could turn
            // "on"/"off"-style tokens into something else. OrdinalIgnoreCase does not.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var env = TestEnv.With("MYAPP_VERBOSE", "ON");
            Assert.True(env.Bool("MYAPP_VERBOSE"));
            Assert.Empty(env.Errors);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

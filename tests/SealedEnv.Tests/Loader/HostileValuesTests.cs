using SealedEnv.Tests.Generated;

namespace SealedEnv.Tests.Loader;

/// <summary>
///     Adversarial round: inputs and providers actively trying to break the loader —
///     extreme doubles, hostile string payloads, empty values on parsed members,
///     null dictionary entries, and providers that throw. The pinned contract under
///     attack: the loader records an <see cref="EnvError" /> instead of throwing, and
///     <c>TryLoad</c> never throws, whatever the environment does.
/// </summary>
public class HostileValuesTests
{
    // ---- hostile providers ----

    private sealed class ThrowingProvider : IEnvProvider
    {
        public string? Get(string key)
        {
            throw new InvalidOperationException("the environment is on fire");
        }
    }

    [Fact]
    public void A_throwing_provider_records_an_invalid_error_instead_of_throwing()
    {
        var errors = new List<EnvError>();
        var value = SealedEnvLoader.Int(new ThrowingProvider(), "MYAPP_PORT", "TestConfig.Port", null, false, false, true, ref errors);

        Assert.Equal(0, value);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Contains("MYAPP_PORT", error.Message);
        Assert.Contains("ThrowingProvider", error.Message);
        Assert.Contains("the environment is on fire", error.Message);
    }

    [Fact]
    public void TryLoad_reports_a_throwing_provider_as_errors_instead_of_throwing()
    {
        // Every member hits the provider, so every member collects its own error —
        // but TryLoad itself must complete and return false.
        var loaded = DefaultsConfig.TryLoad(out _, out var errors, new ThrowingProvider());

        Assert.False(loaded);
        Assert.Equal(4, errors.Count); // DefaultsConfig has four members
        Assert.All(errors, error => Assert.Equal(EnvErrorCode.Invalid, error.Code));
    }

    // ---- extreme doubles (BCL semantics, pinned so they cannot drift) ----

    [Theory]
    [InlineData("1e999", double.PositiveInfinity)] // overflow parses to +Infinity
    [InlineData("-1e999", double.NegativeInfinity)]
    public void Double_overflow_parses_to_infinity_like_the_bcl(string value, double expected)
    {
        // IEEE-special parsing is pinned in NumericLoaderTests (NaN/±Infinity are
        // accepted BCL semantics); overflow lands in the same family — documented
        // here so the silent-infinity behavior cannot drift unnoticed.
        var env = TestEnv.With("MYAPP_RATE", value);

        Assert.Equal(expected, env.Double("MYAPP_RATE"));
        Assert.Empty(env.Errors);
    }

    // ---- empty values on parsed (non-string) members ----

    [Fact]
    public void Allow_empty_on_a_parsed_member_is_an_invalid_error_not_a_crash()
    {
        var env = TestEnv.With("MYAPP_PORT", "");

        Assert.Equal(0, env.Int("MYAPP_PORT", allowEmpty: true));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Contains("which is not a valid Int32", env.SingleError.Message);
    }

    // ---- hostile string payloads ----

    [Fact]
    public void Newlines_pass_through_string_members()
    {
        var env = TestEnv.With("MYAPP_DBHOST", "line1\nline2\r\nline3");

        Assert.Equal("line1\nline2\r\nline3", env.String("MYAPP_DBHOST"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Null_bytes_pass_through_string_members()
    {
        var env = TestEnv.With("MYAPP_DBHOST", "a\0b");

        Assert.Equal("a\0b", env.String("MYAPP_DBHOST"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Very_large_values_pass_through_string_members()
    {
        var huge = new string('x', 100_000);
        var env = TestEnv.With("MYAPP_DBHOST", huge);

        Assert.Equal(huge, env.String("MYAPP_DBHOST"));
        Assert.Empty(env.Errors);
    }

    // ---- provider edge cases ----

    [Fact]
    public void A_null_dictionary_value_behaves_like_an_unset_variable()
    {
        var env = TestEnv.With("MYAPP_PORT", null);

        Assert.Equal(0, env.Int("MYAPP_PORT"));
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
    }
}

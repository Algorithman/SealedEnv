// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: LoaderPipelineTests.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

namespace SealedEnv.Tests.Loader;

/// <summary>
///     Cross-cutting pipeline guarantees: errors aggregate (never fail-fast), every
///     helper shares one lookup/default/empty policy, and nothing ever throws for bad
///     input. These are the invariants the generated Load()/TryLoad() members rely on.
/// </summary>
public class LoaderPipelineTests
{
    [Fact]
    public void Multiple_malformed_values_aggregate_into_one_error_list()
    {
        // Simulates one generated Load() over a bad environment: every error is
        // reported, in declaration order — never fail-fast.
        var env = TestEnv
            .With("MYAPP_PORT", "abc") // int → Invalid
            .Set("MYAPP_VERBOSE", "maybe"); // bool → Invalid ("MYAPP_DB" stays unset)

        var port = env.Int("MYAPP_PORT");
        var db = env.String("MYAPP_DB", required: true);
        var verbose = env.Bool("MYAPP_VERBOSE");

        Assert.Equal(0, port);
        Assert.Equal(string.Empty, db);
        Assert.False(verbose);

        Assert.Equal(3, env.Errors.Count);
        Assert.Equal(
            new[]
            {
                (EnvErrorCode.Invalid, "MYAPP_PORT"),
                (EnvErrorCode.Missing, "MYAPP_DB"),
                (EnvErrorCode.Invalid, "MYAPP_VERBOSE"),
            },
            env.Errors.Select(e => (e.Code, e.Key)).ToArray());
    }

    [Fact]
    public void Nullable_absence_and_missing_errors_coexist_in_one_load()
    {
        // Optional members resolve to null silently while required members report
        // Missing — the two outcomes must not bleed into each other.
        var env = TestEnv.Empty();

        var tag = env.NullableString("MYAPP_TAG");
        var db = env.String("MYAPP_DB", required: true);

        Assert.Null(tag);
        Assert.Equal(string.Empty, db);
        var error = Assert.Single(env.Errors);
        Assert.Equal("MYAPP_DB", error.Key);
    }

    [Theory]
    [InlineData("MYAPP_PORT", "abc")]
    [InlineData("MYAPP_TIMEOUT", "30s")]
    [InlineData("MYAPP_LEVEL", "Fatal")]
    [InlineData("MYAPP_TENANT", "not-a-guid")]
    [InlineData("MYAPP_ENDPOINT", "/relative")]
    public void Wrong_typed_values_never_throw_they_report(string key, string value)
    {
        var env = TestEnv.With(key, value);

        var exception = Record.Exception(() =>
        {
            env.Int(key);
            env.Long(key);
            env.Double(key);
            env.Bool(key);
            env.TimeSpan(key);
            env.Uri(key);
            env.Guid(key);
            env.Enum<LogLevel>(key);
            env.Parsable<Port>(key);
        });

        Assert.Null(exception);
        Assert.All(env.Errors, e => Assert.Equal(EnvErrorCode.Invalid, e.Code));
    }

    [Fact]
    public void Every_type_agrees_on_the_same_missing_policy()
    {
        var env = TestEnv.Empty();

        env.Int("MYAPP_PORT");
        env.Long("MYAPP_PORT");
        env.Double("MYAPP_PORT");
        env.Bool("MYAPP_PORT");
        env.String("MYAPP_PORT");
        env.TimeSpan("MYAPP_PORT");
        env.Uri("MYAPP_PORT");
        env.Guid("MYAPP_PORT");
        env.Enum<LogLevel>("MYAPP_PORT");
        env.Parsable<Port>("MYAPP_PORT");
        env.StringArray("MYAPP_PORT");

        Assert.All(env.Errors, e =>
        {
            Assert.Equal(EnvErrorCode.Missing, e.Code);
            Assert.Equal("MYAPP_PORT", e.Key);
        });
    }

    [Fact]
    public void Empty_values_are_missing_errors_or_defaults_not_wrong_values()
    {
        // The PORT="" footgun: for every typed slot an empty value must never
        // fabricate a value; it is unset (→ default or Missing error).
        var env = TestEnv.With("MYAPP_PORT", "");

        Assert.Equal(8080, env.Int("MYAPP_PORT", "8080"));
        Assert.Empty(env.Errors);

        var unset = TestEnv.With("MYAPP_PORT", "");
        Assert.Equal(0, unset.Int("MYAPP_PORT"));
        Assert.Equal(EnvErrorCode.Missing, unset.SingleError.Code);
    }

    [Fact]
    public void A_null_provider_is_rejected_loudly()
    {
        var errors = new List<EnvError>();

        Assert.Throws<ArgumentNullException>(() =>
            SealedEnvLoader.Int(null!, "MYAPP_PORT", "TestConfig.Port", null, false, false, true, ref errors));
    }
}

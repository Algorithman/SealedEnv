// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: ParsableLoaderTests.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Globalization;

namespace SealedEnv.Tests.Loader;

/// <summary>
///     Type integrity for the <see cref="IParsable{T}" /> passthrough: the custom type's
///     own parser is the authority, and every way it can fail (FormatException,
///     OverflowException, custom exceptions) must degrade into a structured
///     <see cref="EnvError" /> instead of crashing the load.
/// </summary>
public class ParsableLoaderTests
{
    [Fact]
    public void Valid_value_flows_through_the_custom_parser()
    {
        var env = TestEnv.With("MYAPP_PORT", "8080");

        Assert.Equal(new Port(8080), env.Parsable<Port>("MYAPP_PORT"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void The_custom_parser_is_the_authority_on_validity()
    {
        // "-5" is a perfectly valid int, yet Port's own parser rejects it: the type
        // decides what is valid, the loader only routes failures into EnvError.
        var env = TestEnv.With("MYAPP_PORT", "-5");

        Assert.Equal(default, env.Parsable<Port>("MYAPP_PORT"));
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("MYAPP_PORT", error.Key);
        Assert.Equal("Port", error.TypeName);
    }

    [Theory]
    [InlineData("abc")] // parser throws FormatException
    [InlineData("70000")] // parser throws OverflowException (above port range)
    [InlineData("")] // empty slips past the pipeline only with AllowEmpty
    [InlineData("80 80")] // parser throws FormatException
    public void Every_parser_failure_becomes_a_structured_error(string value)
    {
        var env = TestEnv.With("MYAPP_PORT", value);

        Assert.Equal(default, env.Parsable<Port>("MYAPP_PORT", allowEmpty: true));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Contains($"\"{value}\"", env.SingleError.Message);
    }

    [Fact]
    public void Default_flows_through_the_custom_parser_too()
    {
        var env = TestEnv.Empty();

        Assert.Equal(new Port(3000), env.Parsable<Port>("MYAPP_PORT", "3000"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Malformed_default_is_reported_not_crashed()
    {
        var env = TestEnv.Empty();

        env.Parsable<Port>("MYAPP_PORT", "not-a-port");
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Equal("Port", env.SingleError.TypeName);
    }

    [Fact]
    public void Int_is_a_valid_IParsable_so_the_passthrough_matches_the_dedicated_helper()
    {
        // Any IParsable<T> works — including BCL primitives. Cross-check the two
        // routes agree on the same input.
        var dedicated = TestEnv.With("MYAPP_PORT", "8080");
        var passthrough = TestEnv.With("MYAPP_PORT", "8080");

        Assert.Equal(
            dedicated.Int("MYAPP_PORT"),
            passthrough.Parsable<int>("MYAPP_PORT"));
        Assert.Empty(dedicated.Errors);
        Assert.Empty(passthrough.Errors);
    }

    [Fact]
    public void The_passthrough_stays_invariant_when_the_process_is_german()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Port's parser pins InvariantCulture itself, but this proves no hidden
            // current-culture dependency exists anywhere in the chain.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var env = TestEnv.With("MYAPP_PORT", "8080");
            Assert.Equal(new Port(8080), env.Parsable<Port>("MYAPP_PORT"));
            Assert.Empty(env.Errors);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

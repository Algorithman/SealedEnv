using SealedEnv;

namespace SealedEnv.Tests.Generated;

/// <summary>
/// Carriers for the Phase 3 semantics matrix (SealedEnv-PLAN.md §3). One carrier
/// per configuration row, with the EmptyAsUnset flag pinned explicitly so each
/// matrix cell has exactly one owner.
/// </summary>
[SealedEnv(Prefix = "M_")] // EmptyAsUnset = true (default)
internal sealed partial record ValueTypeRow([EnvKey] int Value);

[SealedEnv(Prefix = "M_", EmptyAsUnset = false)]
internal sealed partial record ValueTypeRowKeepEmpty([EnvKey] int Value);

[SealedEnv(Prefix = "M_", EmptyAsUnset = false)]
internal sealed partial record StringRow([EnvKey] string Value);

[SealedEnv(Prefix = "M_", EmptyAsUnset = false)]
internal sealed partial record NullableStringRow([EnvKey] string? Value);

[SealedEnv(Prefix = "M_")] // EmptyAsUnset = true
internal sealed partial record NullableStringRowEmptyAsUnset([EnvKey] string? Value);

[SealedEnv(Prefix = "M_", EmptyAsUnset = false)]
internal sealed partial record RequiredRow(
    [EnvKey(Required = true)] int Value,
    [EnvKey(Required = true)] string Name,
    [EnvKey(Required = true, AllowEmpty = true)] string Tag);

[SealedEnv(Prefix = "M_")] // EmptyAsUnset = true
internal sealed partial record RequiredRowEmptyAsUnset([EnvKey(Required = true)] string Name);

[SealedEnv(Prefix = "M_", EmptyAsUnset = false)]
internal sealed partial record DefaultRowKeepEmpty(
    [EnvKey(Default = "42")] int Value,
    [EnvKey(Default = "fallback")] string Name);

[SealedEnv(Prefix = "M_")] // EmptyAsUnset = true
internal sealed partial record DefaultRowEmptyAsUnset([EnvKey(Default = "42")] int Value);

/// <summary>
/// The Phase 3 semantics matrix, one test per cell, exercised through the
/// generated TryLoad of the carriers above (TryLoad everywhere: it must also never
/// throw — errors are the mechanism, exceptions are not).
/// </summary>
public class SemanticsMatrixTests
{
    // ---- Row 1: value type, no default ----

    [Fact]
    public void Value_type_unset_is_missing()
    {
        var ok = ValueTypeRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.False(ok);
        Assert.Null(config);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Missing, error.Code);
        Assert.Equal("M_VALUE", error.Key);
        Assert.Equal("int", error.TypeName);
    }

    [Fact]
    public void Value_type_empty_is_unset_under_the_default_flag()
    {
        var ok = ValueTypeRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Missing, Assert.Single(errors).Code); // empty ≈ unset → Missing
    }

    [Fact]
    public void Value_type_empty_is_an_empty_error_when_empty_as_unset_is_off()
    {
        var ok = ValueTypeRowKeepEmpty.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Empty, Assert.Single(errors).Code);
    }

    [Fact]
    public void Value_type_malformed_is_invalid()
    {
        var ok = ValueTypeRowKeepEmpty.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "abc" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Invalid, Assert.Single(errors).Code);
    }

    // ---- Row 2: string, not required, not nullable ----

    [Fact]
    public void String_unset_is_missing()
    {
        var ok = StringRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(EnvErrorCode.Missing, error.Code);
        Assert.Equal("string", error.TypeName);
    }

    [Fact]
    public void String_empty_is_an_empty_error_when_empty_as_unset_is_off()
    {
        var ok = StringRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Empty, Assert.Single(errors).Code);
    }

    // The "Malformed" cell for strings is "—" by definition: every text is valid.
    [Fact]
    public void String_accepts_any_text_verbatim()
    {
        var ok = StringRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "  not <malformed> — just text \t" }));

        Assert.True(ok);
        Assert.Equal("  not <malformed> — just text \t", config!.Value);
        Assert.Empty(errors);
    }

    // ---- Row 3: string? ----

    [Fact]
    public void Nullable_string_unset_is_null_without_an_error()
    {
        var ok = NullableStringRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.True(ok);
        Assert.Null(config!.Value);
        Assert.Empty(errors);
    }

    [Fact]
    public void Nullable_string_empty_is_the_empty_string_when_empty_as_unset_is_off()
    {
        var ok = NullableStringRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.True(ok);
        Assert.Equal(string.Empty, config!.Value);
        Assert.Empty(errors);
    }

    [Fact]
    public void Nullable_string_empty_and_unset_both_resolve_to_null_under_the_default_flag()
    {
        var okEmpty = NullableStringRowEmptyAsUnset.TryLoad(out var emptyConfig, out var emptyErrors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.True(okEmpty);
        Assert.Null(emptyConfig!.Value);
        Assert.Empty(emptyErrors);

        var okUnset = NullableStringRowEmptyAsUnset.TryLoad(out var unsetConfig, out var unsetErrors, new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.True(okUnset);
        Assert.Null(unsetConfig!.Value);
        Assert.Empty(unsetErrors);
    }

    // ---- Row 4: Required = true ----

    [Fact]
    public void Required_unset_is_missing_for_every_type()
    {
        var ok = RequiredRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_TAG"] = "t" })); // Tag present so only the unset rows report

        Assert.False(ok);
        Assert.Equal(2, errors.Count);
        Assert.All(errors, static e => Assert.Equal(EnvErrorCode.Missing, e.Code));
        Assert.Equal(["M_VALUE", "M_NAME"], errors.Select(static e => e.Key).ToArray());
    }

    [Fact]
    public void Required_empty_is_an_empty_error_unless_allow_empty()
    {
        var ok = RequiredRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["M_VALUE"] = "1",
            ["M_NAME"] = "", // Empty error
            ["M_TAG"] = "", // AllowEmpty → ok
        }));

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal("M_NAME", error.Key);
        Assert.Equal(EnvErrorCode.Empty, error.Code);
    }

    [Fact]
    public void Required_with_allow_empty_admits_the_empty_string()
    {
        var ok = RequiredRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["M_VALUE"] = "1",
            ["M_NAME"] = "n",
            ["M_TAG"] = "",
        }));

        Assert.True(ok);
        Assert.Equal(string.Empty, config!.Tag);
        Assert.Empty(errors);
    }

    [Fact]
    public void Required_empty_under_empty_as_unset_reports_missing()
    {
        // Pinned decision: EmptyAsUnset applies before requiredness — empty behaves
        // as unset, and unset + required is Missing. Either way it is an error; the
        // code is Missing, not Empty.
        var ok = RequiredRowEmptyAsUnset.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_NAME"] = "" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Missing, Assert.Single(errors).Code);
    }

    [Fact]
    public void Required_malformed_is_invalid()
    {
        var ok = RequiredRow.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["M_VALUE"] = "1.5", // not an int
            ["M_NAME"] = "n",
            ["M_TAG"] = "t",
        }));

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal("M_VALUE", error.Key);
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
    }

    // ---- Row 5: Default given ----

    [Fact]
    public void Default_is_used_when_unset()
    {
        var ok = DefaultRowKeepEmpty.TryLoad(out var config, out var errors, new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.True(ok);
        Assert.Equal(42, config!.Value);
        Assert.Equal("fallback", config.Name);
        Assert.Empty(errors);
    }

    [Fact]
    public void Default_is_not_used_when_empty_and_empty_is_unset_is_off()
    {
        // Pinned decision: with EmptyAsUnset=false an empty value is an Empty error;
        // the default does NOT rescue it (silently loading a default over an
        // explicitly-emptied variable would hide misconfiguration).
        var ok = DefaultRowKeepEmpty.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Empty, Assert.Single(errors).Code);
    }

    [Fact]
    public void Default_rescues_empty_under_the_default_flag()
    {
        var ok = DefaultRowEmptyAsUnset.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "" }));

        Assert.True(ok);
        Assert.Equal(42, config!.Value); // empty ≈ unset → default applies
        Assert.Empty(errors);
    }

    [Fact]
    public void Present_value_beats_the_default_even_when_malformed()
    {
        var ok = DefaultRowEmptyAsUnset.TryLoad(out var config, out var errors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["M_VALUE"] = "abc" }));

        Assert.False(ok);
        Assert.Equal(EnvErrorCode.Invalid, Assert.Single(errors).Code); // the default does not mask bad input
    }

    // The "malformed default" cell is compile-time: covered by the SVE0004
    // generator tests in SealedEnv.Generator.Tests.

    // ---- TryLoad never throws (plan §3) ----

    [Fact]
    public void Tryload_never_throws_for_hostile_environment_content()
    {
        var provider = new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["E2E_PORT"] = "999999999999999999999999", // far overflow
            ["E2E_DBCONNECTION"] = "\0",
            ["E2E_VERBOSE"] = "\tON\t",
            ["E2E_SHUTDOWNTIMEOUT"] = "99999999999999999999999999999999999999",
            ["E2E_CALLBACKURL"] = "💥💥💥",
            ["E2E_TENANTID"] = "\0\0\0",
            ["E2E_LEVEL"] = "-99999999999999999999",
            ["E2E_HOSTS"] = ",,,,,,,,",
            ["E2E_TAG"] = "ok",
            ["E2E_GATEWAYPORT"] = "+-0",
            ["E2E_VAT"] = "1e999999",
        });

        // The call itself is the assertion: if TryLoad threw, this test fails with
        // the thrown exception. Errors are the mechanism, exceptions are not.
        var ok = FullConfig.TryLoad(out var config, out var errors, provider);

        Assert.False(ok);
        Assert.Null(config);
        Assert.True(errors.Count > 0);
        Assert.All(errors, static e => Assert.NotEmpty(e.Message));
    }
}

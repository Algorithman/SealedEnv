namespace SealedEnv.Tests.Loader;

/// <summary>
/// Type integrity for strings: a string can never be "malformed", so the integrity
/// questions are the unset/empty boundary — what counts as a value, what counts as
/// absence, and that values are passed through byte-for-byte without trimming or
/// re-encoding.
/// </summary>
public class StringLoaderTests
{
    // ---- non-nullable string ----

    [Fact]
    public void String_returns_the_value_verbatim()
    {
        // Connection strings are the classic content here: colons, semicolons,
        // equals signs, unicode — nothing may be touched.
        const string value = "Server=db.internal;Port=5432;User=app;Password=p@ss=wörd/§";
        var env = TestEnv.With("MYAPP_DB", value);

        Assert.Equal(value, env.String("MYAPP_DB"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void String_does_not_trim()
    {
        var env = TestEnv.With("MYAPP_DB", "  padded  ");

        Assert.Equal("  padded  ", env.String("MYAPP_DB"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Unset_string_without_default_is_missing()
    {
        var env = TestEnv.Empty();

        Assert.Equal(string.Empty, env.String("MYAPP_DB")); // sentinel; error gates usage
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
        Assert.Equal("MYAPP_DB", env.SingleError.Key);
        Assert.Equal("string", env.SingleError.TypeName);
    }

    [Fact]
    public void Unset_string_with_default_uses_the_default()
    {
        var env = TestEnv.Empty();

        Assert.Equal("localhost", env.String("MYAPP_DB", @default: "localhost"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Present_value_beats_the_default()
    {
        var env = TestEnv.With("MYAPP_DB", "prod-db");

        Assert.Equal("prod-db", env.String("MYAPP_DB", @default: "localhost"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Empty_string_with_empty_as_unset_behaves_as_unset()
    {
        // The classic PORT="" footgun: with the default EmptyAsUnset there is no
        // way an empty string silently becomes the value.
        var env = TestEnv.With("MYAPP_DB", "");

        Assert.Equal(string.Empty, env.String("MYAPP_DB")); // unset without default → Missing
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
    }

    [Fact]
    public void Empty_string_with_empty_as_unset_falls_back_to_the_default()
    {
        var env = TestEnv.With("MYAPP_DB", "");

        Assert.Equal("localhost", env.String("MYAPP_DB", @default: "localhost"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Empty_string_without_empty_as_unset_is_an_empty_error()
    {
        var env = TestEnv.With("MYAPP_DB", "");

        Assert.Equal(string.Empty, env.String("MYAPP_DB", emptyAsUnset: false));
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Empty, error.Code);
        Assert.Contains("empty string", error.Message);
    }

    [Fact]
    public void Allow_empty_admits_the_empty_string_even_when_empty_is_unset()
    {
        var env = TestEnv.With("MYAPP_DB", "");

        Assert.Equal(string.Empty, env.String("MYAPP_DB", allowEmpty: true));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Required_unset_string_is_missing()
    {
        var env = TestEnv.Empty();

        env.String("MYAPP_DB", required: true);
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
    }

    [Fact]
    public void Required_present_string_is_fine()
    {
        var env = TestEnv.With("MYAPP_DB", "prod-db");

        Assert.Equal("prod-db", env.String("MYAPP_DB", required: true));
        Assert.Empty(env.Errors);
    }

    // ---- nullable string ----

    [Fact]
    public void Nullable_string_unset_resolves_to_null_without_an_error()
    {
        var env = TestEnv.Empty();

        Assert.Null(env.NullableString("MYAPP_TAG"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Nullable_string_with_default_resolves_to_the_default()
    {
        var env = TestEnv.Empty();

        Assert.Equal("latest", env.NullableString("MYAPP_TAG", @default: "latest"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Nullable_string_unset_and_required_is_missing()
    {
        var env = TestEnv.Empty();

        Assert.Null(env.NullableString("MYAPP_TAG", required: true));
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
    }

    [Fact]
    public void Nullable_string_empty_with_empty_as_unset_resolves_to_null()
    {
        var env = TestEnv.With("MYAPP_TAG", "");

        Assert.Null(env.NullableString("MYAPP_TAG"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Nullable_string_empty_without_empty_as_unset_is_a_real_value()
    {
        // Semantics matrix row 3 (plan §3): a nullable string explicitly admits ""
        // — EmptyAsUnset=false makes the empty string the loaded value, not an error.
        var env = TestEnv.With("MYAPP_TAG", "");

        Assert.Equal(string.Empty, env.NullableString("MYAPP_TAG", emptyAsUnset: false));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Nullable_string_empty_with_allow_empty_is_the_empty_string()
    {
        var env = TestEnv.With("MYAPP_TAG", "");

        Assert.Equal(string.Empty, env.NullableString("MYAPP_TAG", allowEmpty: true));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Nullable_string_keeps_whitespace_only_values()
    {
        // " " is a value, not absence — only the empty string maps to unset.
        var env = TestEnv.With("MYAPP_TAG", " ");

        Assert.Equal(" ", env.NullableString("MYAPP_TAG"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void An_explicit_null_dictionary_value_behaves_exactly_like_an_absent_one()
    {
        var env = TestEnv.With("MYAPP_DB", null);

        Assert.Equal(string.Empty, env.String("MYAPP_DB"));
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);

        Assert.Null(TestEnv.With("MYAPP_TAG", null).NullableString("MYAPP_TAG"));
    }
}

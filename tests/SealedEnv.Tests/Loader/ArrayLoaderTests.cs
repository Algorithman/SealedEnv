namespace SealedEnv.Tests.Loader;

/// <summary>
/// Type integrity for list values: split on the separator, <c>'\' + separator</c>
/// escapes to a literal separator, entries trimmed. The exact corners (empty
/// entries, lone separators, non-escaping backslashes) are pinned here.
/// </summary>
public class ArrayLoaderTests
{
    [Theory]
    [InlineData("a,b,c", new[] { "a", "b", "c" })]
    [InlineData("a, b , c", new[] { "a", "b", "c" })] // entries are trimmed
    [InlineData("solo", new[] { "solo" })] // no separator → single entry
    [InlineData("a\t", new[] { "a" })] // tabs are whitespace, trimmed too
    public void StringArray_splits_and_trims(string value, string[] expected)
    {
        var env = TestEnv.With("MYAPP_HOSTS", value);

        Assert.Equal(expected, env.StringArray("MYAPP_HOSTS"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void A_backslash_escapes_the_following_separator()
    {
        var env = TestEnv.With("MYAPP_HOSTS", @"a\,b,c");

        Assert.Equal(new[] { "a,b", "c" }, env.StringArray("MYAPP_HOSTS"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Escaping_works_for_custom_separators_too()
    {
        // '\' before ';' escapes it; '\' before any other char (like '\tools') is
        // literal, and commas are never special when the separator is ';'.
        var env = TestEnv.With("MYAPP_PATHS", @"C:\tools;D:\data;E\;x;a,b");

        Assert.Equal(new[] { @"C:\tools", @"D:\data", "E;x", "a,b" }, env.StringArray("MYAPP_PATHS", separator: ';'));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void A_backslash_before_anything_else_is_literal()
    {
        // "a\b,c": the backslash is followed by 'b', not a separator → literal.
        var env = TestEnv.With("MYAPP_HOSTS", @"a\b,c");

        Assert.Equal(new[] { @"a\b", "c" }, env.StringArray("MYAPP_HOSTS"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Empty_entries_are_preserved()
    {
        // Pinned: "a,,b" keeps the hole — silently dropping it would renumber
        // positional lists. Callers who care about holes see them.
        var env = TestEnv.With("MYAPP_HOSTS", "a,,b");

        Assert.Equal(new[] { "a", "", "b" }, env.StringArray("MYAPP_HOSTS"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData(",", new[] { "", "" })]
    [InlineData("a,", new[] { "a", "" })]
    [InlineData(",a", new[] { "", "a" })]
    [InlineData("", new[] { "" })] // reachable only via AllowEmpty
    public void Lone_and_leading_separators_split_consistently(string value, string[] expected)
    {
        var env = TestEnv.With("MYAPP_HOSTS", value);

        Assert.Equal(expected, env.StringArray("MYAPP_HOSTS", allowEmpty: true));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Unset_without_default_is_missing()
    {
        var env = TestEnv.Empty();

        Assert.Equal([], env.StringArray("MYAPP_HOSTS"));
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
        Assert.Equal("string[]", env.SingleError.TypeName);
    }

    [Fact]
    public void Unset_with_default_splits_the_default_value()
    {
        var env = TestEnv.Empty();

        Assert.Equal(new[] { "x", "y" }, env.StringArray("MYAPP_HOSTS", @default: "x,y"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Empty_value_with_empty_as_unset_falls_back_to_the_default()
    {
        var env = TestEnv.With("MYAPP_HOSTS", "");

        Assert.Equal(new[] { "x", "y" }, env.StringArray("MYAPP_HOSTS", @default: "x,y"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Empty_value_without_empty_as_unset_is_an_empty_error()
    {
        var env = TestEnv.With("MYAPP_HOSTS", "");

        Assert.Equal([], env.StringArray("MYAPP_HOSTS", emptyAsUnset: false));
        Assert.Equal(EnvErrorCode.Empty, env.SingleError.Code);
    }

    [Fact]
    public void Present_value_beats_the_default()
    {
        var env = TestEnv.With("MYAPP_HOSTS", "prod-a,prod-b");

        Assert.Equal(new[] { "prod-a", "prod-b" }, env.StringArray("MYAPP_HOSTS", @default: "x,y"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void Required_unset_array_is_missing()
    {
        var env = TestEnv.Empty();

        env.StringArray("MYAPP_HOSTS", required: true);
        Assert.Equal(EnvErrorCode.Missing, env.SingleError.Code);
    }
}

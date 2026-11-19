namespace SealedEnv.Tests.Loader;

/// <summary>
/// Type integrity for TimeSpan, Uri and Guid: canonical forms round-trip, malformed
/// values become structured <see cref="EnvErrorCode.Invalid"/> errors, and Uri
/// deliberately accepts only absolute URIs.
/// </summary>
public class TimeSpanUriGuidLoaderTests
{
    // ---- TimeSpan ----

    [Theory]
    [InlineData("00:00:30", 30)]
    [InlineData("0:0:0.5", 0.5)]
    [InlineData("01:30:00", 5400)]
    [InlineData("-00:00:10", -10)] // negative durations are legitimate config
    [InlineData("1.02:03:04", 93784)] // d.hh:mm:ss
    public void TimeSpan_parses_well_formed_values(string value, double expectedSeconds)
    {
        var env = TestEnv.With("MYAPP_TIMEOUT", value);
        var parsed = env.TimeSpan("MYAPP_TIMEOUT");

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), parsed);
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void A_bare_number_means_days_like_TimeSpan_Parse()
    {
        // Pinned, with a warning: System.TimeSpan.Parse treats a bare number as
        // DAYS, so SHUTDOWN_TIMEOUT=30 is 30 days, not 30 seconds. We keep BCL
        // semantics (one parsing path, no special cases) — write the unit in the
        // value: "00:00:30" for 30 seconds.
        var env = TestEnv.With("MYAPP_TIMEOUT", "30");

        Assert.Equal(TimeSpan.FromDays(30), env.TimeSpan("MYAPP_TIMEOUT"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("30s")] // no suffix support
    [InlineData("abc")]
    [InlineData("1:70:00")] // minutes out of range
    [InlineData("00:00:30:00:00")] // too many components
    [InlineData("1.2.3")] // not a TimeSpan shape
    public void TimeSpan_rejects_malformed_values(string value)
    {
        var env = TestEnv.With("MYAPP_TIMEOUT", value);

        Assert.Equal(default, env.TimeSpan("MYAPP_TIMEOUT"));
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("MYAPP_TIMEOUT", error.Key);
        Assert.Equal("TimeSpan", error.TypeName);
        Assert.Contains($"\"{value}\"", error.Message);
    }

    [Fact]
    public void TimeSpan_default_is_parsed_through_the_same_pipeline()
    {
        var env = TestEnv.Empty();

        Assert.Equal(TimeSpan.FromSeconds(30), env.TimeSpan("MYAPP_TIMEOUT", @default: "00:00:30"));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void TimeSpan_malformed_default_is_reported_not_crashed()
    {
        var env = TestEnv.Empty();

        env.TimeSpan("MYAPP_TIMEOUT", @default: "30s");
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
    }

    // ---- Uri ----

    [Theory]
    [InlineData("https://example.com/api?key=1#frag")]
    [InlineData("http://localhost:8080")]
    [InlineData("ftp://files.example.com")]
    [InlineData("postgres://db.internal:5432/appdb")]
    [InlineData("file:///etc/sealedenv/config.json")] // file URIs are absolute too
    public void Uri_parses_absolute_uris(string value)
    {
        var env = TestEnv.With("MYAPP_ENDPOINT", value);
        var parsed = env.Uri("MYAPP_ENDPOINT");

        Assert.NotNull(parsed);
        // Compare structurally: Uri canonicalizes text form (e.g. "host:8080"
        // renders back as "host:8080/"), so string equality would pin BCL
        // canonicalization, not the value itself.
        Assert.Equal(new Uri(value, UriKind.Absolute), parsed);
        Assert.Empty(env.Errors);
    }

    // Pinned design decision: config endpoints must be absolute — a relative URI
    // would silently resolve against the process working directory.
    [Theory]
    [InlineData("/relative/path")]
    [InlineData("example.com")] // scheme-less is relative, not a host
    [InlineData("not a url")]
    [InlineData("http://")] // scheme without anything after it
    [InlineData("./config.json")]
    public void Uri_rejects_relative_or_garbage_values(string value)
    {
        var env = TestEnv.With("MYAPP_ENDPOINT", value);

        Assert.Null(env.Uri("MYAPP_ENDPOINT"));
        var error = env.SingleError;
        Assert.Equal(EnvErrorCode.Invalid, error.Code);
        Assert.Equal("Uri", error.TypeName);
        Assert.Contains("absolute URI", error.Message);
    }

    // ---- Guid ----

    [Fact]
    public void Guid_parses_the_canonical_format()
    {
        var env = TestEnv.With("MYAPP_TENANT", "6f9619ff-8b86-d011-b42d-00c04fc964ff");

        Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), env.Guid("MYAPP_TENANT"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("6f9619ff8b86d011b42d00c04fc964ff")] // N
    [InlineData("{6f9619ff-8b86-d011-b42d-00c04fc964ff}")] // B
    [InlineData("(6f9619ff-8b86-d011-b42d-00c04fc964ff)")] // P
    [InlineData("6F9619FF-8B86-D011-B42D-00C04FC964FF")] // upper-case D
    public void Guid_accepts_the_documented_string_formats(string value)
    {
        var env = TestEnv.With("MYAPP_TENANT", value);

        Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), env.Guid("MYAPP_TENANT"));
        Assert.Empty(env.Errors);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("6f9619ff")] // too short
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")] // non-hex characters
    [InlineData("6f9619ff-8b86-d011-b42d-00c04fc964f")] // one hex digit short
    public void Guid_rejects_malformed_values(string value)
    {
        var env = TestEnv.With("MYAPP_TENANT", value);

        Assert.Equal(default, env.Guid("MYAPP_TENANT"));
        Assert.Equal(EnvErrorCode.Invalid, env.SingleError.Code);
        Assert.Equal("Guid", env.SingleError.TypeName);
    }
}

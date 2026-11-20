// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: PartialPropertyLoadTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using SealedEnv.Tests.Loader;

namespace SealedEnv.Tests.Generated;

/// <summary>
///     Runtime behavior of the partial-property carriers (see
///     <see cref="PartialPropertyCarriers" />): the load semantics must be
///     indistinguishable from the primary-constructor mode — same aggregation, same
///     defaults, same provider seam — with the properties as the read surface.
/// </summary>
public class PartialPropertyLoadTests
{
    private static DictionaryEnvProvider HappyEnv()
    {
        return new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["PP_PORT"] = "9090",
            ["PP_DBCONNECTION"] = "Host=db.internal;Database=app",
            ["PP_VERBOSE"] = "on",
            ["PP_SHUTDOWNTIMEOUT"] = "00:01:00",
            ["PP_CALLBACKURL"] = "https://callback.example.com/hook",
            ["PP_LEVEL"] = "warning",
            ["PP_HOSTS"] = "a.example.com; b.example.com",
            ["PP_TAG"] = "",
            ["PP_VAT"] = "0.2",
            ["PP_MY\"QUOTED\"KEY"] = "kw", // member @class, explicit hostile key
        });
    }

    [Fact]
    public void Load_binds_every_partial_property()
    {
        var config = PropertyConfig.Load(HappyEnv());

        Assert.Equal(9090, config.Port);
        Assert.Equal("Host=db.internal;Database=app", config.DbConnection);
        Assert.True(config.Verbose);
        Assert.Equal(TimeSpan.FromMinutes(1), config.ShutdownTimeout);
        Assert.Equal("https://callback.example.com/hook", config.CallbackUrl!.ToString());
        Assert.Equal(LogLevel.Warning, config.Level);
        Assert.Equal(["a.example.com", "b.example.com",], config.Hosts); // ';' separator, trimmed
        Assert.Equal(string.Empty, config.Tag); // AllowEmpty admits the empty string
        Assert.Equal(0.2m, config.Vat.Value); // custom Parser route
        Assert.Equal("kw", config.@class); // keyword member name, hostile explicit key
    }

    [Fact]
    public void Defaults_apply_when_variables_are_unset()
    {
        // PropertyConfig's value-type members have no defaults (like the
        // parameter-mode FullConfig), so the defaults test drives the small class
        // carrier whose every member carries one.
        var config = PropertyClassConfig.Load(new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.Equal(3, config.Retries);
        Assert.Equal("localhost", config.Host);
    }

    [Fact]
    public void Load_aggregates_all_errors_with_member_display_names()
    {
        // A valid environment except two broken entries: PORT malformed (Invalid),
        // DBCONNECTION unset (Missing, required) — exactly two errors, both reported.
        var broken = new Dictionary<string, string?>
        {
            ["PP_PORT"] = "abc",
            ["PP_VERBOSE"] = "on",
            ["PP_SHUTDOWNTIMEOUT"] = "00:01:00",
            ["PP_CALLBACKURL"] = "https://callback.example.com/hook",
            ["PP_LEVEL"] = "warning",
            ["PP_HOSTS"] = "a; b",
            ["PP_TAG"] = "",
            ["PP_VAT"] = "0.2",
            ["PP_MY\"QUOTED\"KEY"] = "kw",
        };

        var exception = Assert.Throws<SealedEnvException>(() => PropertyConfig.Load(new DictionaryEnvProvider(broken)));

        Assert.Equal(2, exception.Errors.Count);
        Assert.Equal(
            [EnvErrorCode.Invalid, EnvErrorCode.Missing,],
            exception.Errors.Select(static e => e.Code).ToArray());
        Assert.Equal(["PP_PORT", "PP_DBCONNECTION",], exception.Errors.Select(static e => e.Key).ToArray());

        // Display names use the PROPERTY name, matching the constructor-parameter mode.
        Assert.Contains("PropertyConfig.Port", exception.Errors[0].Message);
        Assert.Contains("PropertyConfig.DbConnection", exception.Errors[1].Message);
    }

    [Fact]
    public void TryLoad_round_trips_both_outcomes()
    {
        var ok = PropertyConfig.TryLoad(out var config, out var errors, HappyEnv());

        Assert.True(ok);
        Assert.NotNull(config);
        Assert.Equal(9090, config!.Port);
        Assert.Empty(errors);

        var failed = PropertyConfig.TryLoad(out var failedConfig, out var failedErrors, new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PP_PORT"] = "abc", }));

        Assert.False(failed);
        Assert.Null(failedConfig);
        Assert.NotEmpty(failedErrors);
    }

    [Fact]
    public void Two_loads_of_the_same_environment_are_equal_records()
    {
        // The generated backing fields are real instance data: record(-struct)
        // equality sees them, so identical environments produce interchangeable
        // values. (A plain class carrier has no synthesized equality, and VatRate
        // members compare by reference — both out of scope here.)
        var first = PropertyStructConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PPS_RETRIES"] = "7", }));
        var second = PropertyStructConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PPS_RETRIES"] = "7", }));
        var other = PropertyStructConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PPS_RETRIES"] = "9", }));

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Plain_class_carriers_load_from_generated_constructors()
    {
        var config = PropertyClassConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PPC_HOST"] = "db.internal", }));

        Assert.Equal(3, config.Retries);
        Assert.Equal("db.internal", config.Host);
    }

    [Fact]
    public void Struct_carriers_load_with_value_semantics()
    {
        var settings = PropertyStructConfig.Load(new DictionaryEnvProvider(new Dictionary<string, string?>()));
        Assert.Equal(5, settings.Retries);

        var readonlySettings = ReadonlyPropertySettings.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PPS_RETRIES"] = "7", }));
        Assert.Equal(7, readonlySettings.Retries);
    }

    [Fact]
    public void Documentation_opt_in_covers_property_bindings()
    {
        var config = DocumentedPropertyConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["PDOC_DBCONNECTION"] = "Host=db", }));

        Assert.Equal(8080, config.Port);
        Assert.Contains("| `PDOC_PORT` | `int` | No | `8080` | TCP port to listen on |", DocumentedPropertyConfig.ConfigurationDocumentation);
        Assert.Contains("pipes \\| get escaped", DocumentedPropertyConfig.ConfigurationDocumentation);
    }
}

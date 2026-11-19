// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: GeneratedLoadTests.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Globalization;
using SealedEnv.Tests.Loader;

namespace SealedEnv.Tests.Generated;

/// <summary>
///     The real end-to-end contract: [SealedEnv] records whose Load()/TryLoad() members
///     were generated during THIS project's compilation, loaded from an in-memory
///     provider. If the generator regresses, this file stops compiling.
/// </summary>
public class GeneratedLoadTests
{
    private static DictionaryEnvProvider FullHappyEnv()
    {
        return new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["E2E_PORT"] = "9090",
            ["E2E_DBCONNECTION"] = "Host=db.internal;Database=app",
            ["E2E_VERBOSE"] = "on",
            ["E2E_SHUTDOWNTIMEOUT"] = "00:01:00",
            ["E2E_CALLBACKURL"] = "https://callback.example.com/hook",
            ["E2E_TENANTID"] = "6f9619ff-8b86-d011-b42d-00c04fc964ff",
            ["E2E_LEVEL"] = "warning",
            ["E2E_HOSTS"] = "a.example.com, b.example.com",
            ["E2E_TAG"] = "",
            ["E2E_GATEWAYPORT"] = "443",
            ["E2E_VAT"] = "0.2",
        });
    }

    private static DictionaryEnvProvider BrokenEnv()
    {
        return new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["E2E_PORT"] = "abc", // Invalid
            // E2E_DBCONNECTION missing → Missing (required)
            ["E2E_VERBOSE"] = "maybe", // Invalid
        });
    }

    [Fact]
    public void Load_binds_every_member_of_the_full_carrier()
    {
        var config = FullConfig.Load(FullHappyEnv());

        Assert.Equal(9090, config.Port);
        Assert.Equal("Host=db.internal;Database=app", config.DbConnection);
        Assert.True(config.Verbose);
        Assert.Equal(TimeSpan.FromMinutes(1), config.ShutdownTimeout);
        Assert.Equal("https://callback.example.com/hook", config.CallbackUrl!.ToString());
        Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), config.TenantId);
        Assert.Equal(LogLevel.Warning, config.Level);
        Assert.Equal(["a.example.com", "b.example.com",], config.Hosts); // entries trimmed
        Assert.Equal(string.Empty, config.Tag); // AllowEmpty admits the empty string
        Assert.Equal(new Port(443), config.GatewayPort); // IParsable<T> route
        Assert.Equal(0.2m, config.Vat.Value); // custom Parser route
    }

    [Fact]
    public void Load_falls_back_to_defaults_when_only_required_variables_are_set()
    {
        var config = DefaultsConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["E2E_DBCONNECTION"] = "Host=db", }));

        Assert.Equal(8080, config.Port);
        Assert.Equal("Host=db", config.DbConnection);
        Assert.Equal(TimeSpan.FromSeconds(30), config.ShutdownTimeout);
        Assert.Null(config.Tag);
    }

    [Fact]
    public void Load_throws_one_aggregate_exception_for_all_bad_variables()
    {
        // DefaultsConfig: PORT malformed (Invalid), DBCONNECTION absent (Missing);
        // Tag (string?) stays silent at null. Two errors, both reported.
        var exception = Assert.Throws<SealedEnvException>(() => DefaultsConfig.Load(BrokenEnv()));

        Assert.Equal(2, exception.Errors.Count);
        Assert.Equal(
            [EnvErrorCode.Invalid, EnvErrorCode.Missing,],
            exception.Errors.Select(static e => e.Code).ToArray());
        Assert.Equal(
            ["E2E_PORT", "E2E_DBCONNECTION",],
            exception.Errors.Select(static e => e.Key).ToArray());

        // The message is the user-facing startup report: every bad variable at once.
        Assert.Contains("2 errors)", exception.Message);
        Assert.Contains("E2E_PORT", exception.Message);
        Assert.Contains("E2E_DBCONNECTION", exception.Message);
    }

    [Fact]
    public void Every_unbound_value_type_member_is_reported_not_ignored()
    {
        // FullConfig's value-type members have no defaults: a nearly-empty env must
        // surface each one as Missing — the failure report stays complete.
        var exception = Assert.Throws<SealedEnvException>(() => FullConfig.Load(BrokenEnv()));

        // PORT + VERBOSE malformed (Invalid); DbConnection, ShutdownTimeout,
        // CallbackUrl, TenantId, Level, Hosts, GatewayPort, Vat Missing; Tag (string?) silent.
        Assert.Equal(10, exception.Errors.Count);

        Assert.All(
            exception.Errors.Where(static e => e.Key is not ("E2E_PORT" or "E2E_VERBOSE")),
            static e => Assert.Equal(EnvErrorCode.Missing, e.Code));
        Assert.DoesNotContain(exception.Errors, static e => e.Key == "E2E_TAG");
    }

    [Fact]
    public void TryLoad_reports_every_error_without_throwing()
    {
        var ok = DefaultsConfig.TryLoad(out var config, out var errors, BrokenEnv());

        Assert.False(ok);
        Assert.Null(config);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void TryLoad_returns_the_config_and_an_empty_error_list_when_valid()
    {
        var ok = FullConfig.TryLoad(out var config, out var errors, FullHappyEnv());

        Assert.True(ok);
        Assert.NotNull(config);
        Assert.Equal(9090, config!.Port);
        Assert.Empty(errors);
    }

    [Fact]
    public void Record_struct_carriers_load_with_value_semantics()
    {
        var settings = Settings.Load(new DictionaryEnvProvider(new Dictionary<string, string?>()));

        Assert.Equal(10, settings.Retries); // from the attribute default

        var loaded = Settings.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["S_RETRIES"] = "5", }));
        Assert.Equal(5, loaded.Retries);
    }

    [Fact]
    public void Primary_constructor_classes_load_too()
    {
        var host = HostSettings.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["S_HOST"] = "db.internal", }));

        Assert.Equal("db.internal", host.Host);
    }

    [Fact]
    public void Global_namespace_carriers_load()
    {
        var carrier = GlobalNamespaceCarrier.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["GLOBAL_PORT"] = "7", }));

        Assert.Equal(7, carrier.Port);
    }

    [Fact]
    public void A_custom_parser_failure_surfaces_in_the_aggregate_exception()
    {
        var provider = new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["E2E_DBCONNECTION"] = "Host=db",
            ["E2E_VAT"] = "-1", // VatRate.Parse throws FormatException
        });

        var exception = Assert.Throws<SealedEnvException>(() => FullConfig.Load(provider));

        var vatError = Assert.Single(exception.Errors, static e => e.Key == "E2E_VAT");
        Assert.Equal(EnvErrorCode.Invalid, vatError.Code);
        Assert.Equal("VatRate", vatError.TypeName);
        Assert.Contains("\"-1\"", vatError.Message);
    }

    [Fact]
    public void The_full_carrier_is_culture_invariant_under_a_german_process()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            // Happy env parses "0.2" as 0.2 — not 2, as a comma-decimal locale would.
            Assert.Equal(0.2m, FullConfig.Load(FullHappyEnv()).Vat.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void The_same_carrier_loads_from_any_provider_the_caller_supplies()
    {
        // The provider IS the test seam: the identical generated Load works against
        // any IEnvProvider — here two different in-memory environments, with the
        // process environment never touched.
        var production = DefaultsConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["E2E_DBCONNECTION"] = "Host=prod.internal", }));

        var local = DefaultsConfig.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["E2E_DBCONNECTION"] = "Host=localhost", }));

        Assert.Equal("Host=prod.internal", production.DbConnection);
        Assert.Equal("Host=localhost", local.DbConnection);
        Assert.Equal(8080, production.Port); // defaults apply identically in both loads
    }

    [Fact]
    public void Hostile_keys_and_keyword_parameters_load_exactly()
    {
        // The explicit keys are hostile as C# string literals (quotes, backslashes,
        // newlines); the parameters are @-escaped keywords and non-ASCII. If the
        // emitter's literal escaping or the derivation regresses, this stops
        // compiling or the lookups miss.
        var config = HostileKeysConfig.Load(new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["HATE_MY\"QUOTED\"KEY"] = "cls",
            ["HATE_BACK\\SLASH\\KEY"] = "evt",
            ["HATE_NEW\nLINE\\KEY"] = "ns",
            // Derivation uppercases char-wise: ß has no single-character uppercase
            // mapping, so Größe derives GRÖßE (full case folding would give GRÖSSE).
            ["HATE_GRÖßE"] = "42",
        }));

        Assert.Equal("cls", config.@class);
        Assert.Equal("evt", config.@event);
        Assert.Equal("ns", config.@namespace);
        Assert.Equal("42", config.Größe);
    }

    [Fact]
    public void Plain_struct_carriers_load_with_value_semantics()
    {
        var settings = PlainStructSettings.Load(new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["S_MODE"] = "release", }));

        Assert.Equal("release", settings.Mode);

        var fallback = PlainStructSettings.Load(new DictionaryEnvProvider(new Dictionary<string, string?>()));
        Assert.Equal("debug", fallback.Mode);
    }
}

namespace SealedEnv.Generator.Tests;

/// <summary>
/// Snapshot contract of the generated output (SealedEnv-PLAN.md §1 "expected
/// generated output"). The canonical three-field record from the plan plus a
/// full-surface carrier exercising every loader route.
/// </summary>
public class SnapshotTests
{
    private const string ThreeFieldRecord = """
        using System;
        using SealedEnv;

        namespace TestCode;

        [SealedEnv(Prefix = "MYAPP_")]
        public sealed partial record AppConfig(
            [EnvKey("PORT", Default = "8080")] int Port,
            [EnvKey(Required = true)] string DbConnection,
            [EnvKey] TimeSpan ShutdownTimeout);
        """;

    private const string FullSurfaceCarrier = """
        using System;
        using SealedEnv;

        namespace TestCode;

        public enum LogLevel { Debug, Info, Warning, Error }

        public sealed class VatRate
        {
            public static VatRate Parse(string raw) => new();
        }

        [SealedEnv(Prefix = "APP_", EmptyAsUnset = false)]
        internal readonly partial record struct FullSurface(
            [EnvKey("PORT", Default = "8080")] int Port,
            [EnvKey(Required = true)] string DbConnection,
            [EnvKey] bool Verbose,
            [EnvKey] double Rate,
            [EnvKey] long Big,
            [EnvKey] Uri? CallbackUrl,
            [EnvKey] Guid TenantId,
            [EnvKey] LogLevel Level,
            [EnvKey(Separator = ';')] string[] Hosts,
            [EnvKey(AllowEmpty = true)] string? Tag,
            [EnvKey(Parser = typeof(VatRate))] VatRate Vat);
        """;

    [Fact]
    public Task Three_field_record_snapshot()
    {
        var (_, generated) = GeneratorRunner.Run(ThreeFieldRecord);

        Assert.NotNull(generated);
        return Verify(generated);
    }

    [Fact]
    public Task Full_surface_carrier_snapshot()
    {
        var (_, generated) = GeneratorRunner.Run(FullSurfaceCarrier);

        Assert.NotNull(generated);
        return Verify(generated);
    }

    /// <summary>
    /// Phase 4: prefix composition over explicit keys, derived keys and the pinned
    /// acronym rule (plan §4: ID → ID, DbUrl → DBURL, APIKey → API_KEY).
    /// </summary>
    private const string PrefixComposition = """
        using SealedEnv;

        namespace TestCode;

        [SealedEnv(Prefix = "APP_")]
        public sealed partial record Prefixed(
            [EnvKey] int Port, // derived
            [EnvKey("CUSTOM")] string Custom, // explicit, still prefixed
            [EnvKey] string ID, // acronym rule
            [EnvKey] string DbUrl, // acronym rule
            [EnvKey] string APIKey, // acronym-to-word boundary
            [EnvKey] string DbConnection); // no boundaries at all

        [SealedEnv]
        public sealed partial record Unprefixed([EnvKey] int Port); // empty prefix → raw derived key
        """;

    [Fact]
    public Task Prefix_composition_snapshot()
    {
        var (_, generated) = GeneratorRunner.Run(PrefixComposition);

        Assert.NotNull(generated);
        return Verify(generated);
    }

    /// <summary>
    /// Phase 4: separator literals survive Roslyn escaping — quote, backslash,
    /// semicolon.
    /// </summary>
    private const string SeparatorEscaping = """
        using SealedEnv;

        namespace TestCode;

        [SealedEnv]
        public sealed partial record Separators(
            [EnvKey(Separator = ';')] string[] Semicolon,
            [EnvKey(Separator = '"')] string[] Quote,
            [EnvKey(Separator = '\\')] string[] Backslash);
        """;

    [Fact]
    public Task Separator_escaping_snapshot()
    {
        var (_, generated) = GeneratorRunner.Run(SeparatorEscaping);

        Assert.NotNull(generated);
        return Verify(generated);
    }
}

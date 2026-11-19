// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: TestCarriers.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 12:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Globalization;
using SealedEnv.Tests.Loader;

namespace SealedEnv.Tests.Generated;

/// <summary>
///     Compile-time proof that the generator's output compiles against this project
///     (the generator is attached here as an analyzer). These partial records get
///     Load()/TryLoad() members generated during the test build itself.
/// </summary>
[SealedEnv(Prefix = "E2E_")]
internal sealed partial record FullConfig(
    [EnvKey("PORT", Default = "8080")] int Port,
    [EnvKey(Required = true)] string DbConnection,
    [EnvKey] bool Verbose,
    [EnvKey] TimeSpan ShutdownTimeout,
    [EnvKey] Uri? CallbackUrl,
    [EnvKey] Guid TenantId,
    [EnvKey] LogLevel Level,
    [EnvKey] string[] Hosts,
    [EnvKey(AllowEmpty = true)] string? Tag,
    [EnvKey] Port GatewayPort,
    [EnvKey(Parser = typeof(VatRate))] VatRate Vat);

[SealedEnv(Prefix = "E2E_")]
internal sealed partial record DefaultsConfig(
    [EnvKey("PORT", Default = "8080")] int Port,
    [EnvKey(Required = true)] string DbConnection,
    [EnvKey(Default = "00:00:30")] TimeSpan ShutdownTimeout,
    [EnvKey] string? Tag);

[SealedEnv(Prefix = "S_")]
internal readonly partial record struct Settings([EnvKey(Default = "10")] int Retries);

/// <summary>
///     Adversarial: C# keyword parameter names (@-escaped), a non-ASCII parameter
///     name (derived key GRÖßE — char-wise uppercasing leaves ß unchanged), and
///     explicit keys that are hostile as C# string literals — quotes, backslashes,
///     newlines. Pins that the emitter escapes them (SymbolDisplay.FormatLiteral)
///     and that lookups still match exactly.
/// </summary>
[SealedEnv(Prefix = "HATE_")]
internal sealed partial record HostileKeysConfig(
    [EnvKey("MY\"QUOTED\"KEY")] string @class,
    [EnvKey("BACK\\SLASH\\KEY")] string @event,
    [EnvKey("NEW\nLINE\\KEY")] string @namespace,
    [EnvKey] string Größe);

/// <summary>Adversarial: a plain (non-record) struct carrier with a primary constructor.</summary>
[SealedEnv(Prefix = "S_")]
internal partial struct PlainStructSettings([EnvKey(Default = "debug")] string Mode)
{
    public string Mode { get; } = Mode;
}

/// <summary>
///     Doc-export carrier: GenerateDocumentation emits the markdown table as a
///     generated <c>.docs.g.md</c> file plus the ConfigurationDocumentation member.
///     Covers the Description column, defaults, requiredness and pipe escaping.
/// </summary>
[SealedEnv(Prefix = "DOCS_", GenerateDocumentation = true)]
internal sealed partial record DocumentedConfig(
    [EnvKey("PORT", Default = "8080", Description = "TCP port to listen on")] int Port,
    [EnvKey(Required = true)] string DbConnection,
    [EnvKey("HOSTS", Description = "Comma separated hosts")] string[] Hosts,
    [EnvKey("MAGIC|KEY", Description = "Pipes get escaped")] int Magic);

// Plain primary-constructor class: the class body must capture the parameter into a
// property itself — records do this implicitly, classes do not.
[SealedEnv(Prefix = "S_")]
internal sealed partial class HostSettings([EnvKey(Default = "localhost")] string Host)
{
    public string Host { get; } = Host;
}

/// <summary>Custom-parsed type with a reference-type parser route (EnvKeyAttribute.Parser).</summary>
internal sealed class VatRate
{
    private VatRate(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public static VatRate Parse(string raw)
    {
        return decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? new VatRate(value)
            : throw new FormatException($"'{raw}' is not a valid VAT rate.");
    }
}

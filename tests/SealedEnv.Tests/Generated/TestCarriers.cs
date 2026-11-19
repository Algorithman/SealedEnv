using System.Globalization;
using SealedEnv;
using SealedEnv.Tests.Loader;

namespace SealedEnv.Tests.Generated;

/// <summary>
/// Compile-time proof that the generator's output compiles against this project
/// (the generator is attached here as an analyzer). These partial records get
/// Load()/TryLoad() members generated during the test build itself.
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
    private VatRate(decimal value) => Value = value;

    public decimal Value { get; }

    public static VatRate Parse(string raw) =>
        decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? new VatRate(value)
            : throw new FormatException($"'{raw}' is not a valid VAT rate.");
}

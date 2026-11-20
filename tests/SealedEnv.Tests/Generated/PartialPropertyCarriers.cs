// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: PartialPropertyCarriers.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using SealedEnv.Tests.Loader;

namespace SealedEnv.Tests.Generated;

/// <summary>
///     Partial-property carriers (the C# 13 binding surface): [EnvKey] get-only
///     partial properties whose implementation parts, backing fields and
///     constructor are generated during THIS project's compilation. If the emitter
///     regresses, this file stops compiling under Nullable=enable +
///     TreatWarningsAsErrors. Every loader route of the parameter-mode FullConfig
///     appears here as a property, so the two modes cannot drift apart.
/// </summary>
[SealedEnv(Prefix = "PP_")]
internal sealed partial record PropertyConfig
{
    [EnvKey("PORT", Default = "8080")] public partial int Port { get; }
    [EnvKey(Required = true)] public partial string DbConnection { get; }
    [EnvKey] public partial bool Verbose { get; }
    [EnvKey] public partial TimeSpan ShutdownTimeout { get; }
    [EnvKey] public partial Uri? CallbackUrl { get; }
    [EnvKey] public partial LogLevel Level { get; }
    [EnvKey(Separator = ';')] public partial string[] Hosts { get; }
    [EnvKey(AllowEmpty = true)] public partial string? Tag { get; }
    [EnvKey(Parser = typeof(VatRate))] public partial VatRate Vat { get; }

    /// <summary>Keyword member name + hostile key: the emitter must escape both.</summary>
    [EnvKey("MY\"QUOTED\"KEY")] public partial string @class { get; }
}

/// <summary>A plain sealed class — no record machinery, no primary constructor.</summary>
[SealedEnv(Prefix = "PPC_")]
internal sealed partial class PropertyClassConfig
{
    [EnvKey(Default = "3")] public partial int Retries { get; }
    [EnvKey(Default = "localhost")] public partial string Host { get; }
}

[SealedEnv(Prefix = "PPS_")]
internal partial record struct PropertyStructConfig
{
    [EnvKey(Default = "5")] public partial int Retries { get; }
}

/// <summary>
///     Readonly struct: the generated fields are readonly and assigned from the
///     generated constructor only — the one shape where construct-then-mutate is a
///     compile error.
/// </summary>
[SealedEnv(Prefix = "PPS_")]
internal readonly partial record struct ReadonlyPropertySettings
{
    [EnvKey(Default = "10")] public partial int Retries { get; }
}

[SealedEnv(Prefix = "PDOC_", GenerateDocumentation = true)]
internal sealed partial record DocumentedPropertyConfig
{
    [EnvKey("PORT", Default = "8080", Description = "TCP port to listen on")] public partial int Port { get; }
    [EnvKey(Required = true, Description = "Connection string; pipes | get escaped")] public partial string DbConnection { get; }
}

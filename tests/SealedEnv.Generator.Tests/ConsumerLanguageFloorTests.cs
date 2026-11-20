// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Generator.Tests
// Filename: ConsumerLanguageFloorTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     The consumer language floor, pinned by COMPILING (not just parsing) carrier +
///     generated output at the exact LangVersion a consumer project would use.
///     Parameter mode floors at C# 10 — the emitted file-scoped namespace and record
///     structs are C# 10, and no complete carrier surface exists below it. Property
///     mode floors at C# 13 (partial properties). The boundary tests keep the README
///     honest: a lower LangVersion consumer sees compiler errors in BOTH their carrier
///     and the emitted file, never silently-broken output.
/// </summary>
public class ConsumerLanguageFloorTests
{
    /// <summary>
    ///     Every helper route on the floor-version surface: a record with a primary
    ///     constructor (C# 9) in a file-scoped namespace (C# 10).
    /// </summary>
    private const string ParameterModeCarrier = """
                                               using System;
                                               using System.Collections.Generic;
                                               using SealedEnv;

                                               namespace TestCode;

                                               public enum LogLevel { Debug, Info, Warning, Error }

                                               public sealed class StrictRate
                                               {
                                                   public static StrictRate Parse(string raw) => new();
                                               }

                                               [SealedEnv(Prefix = "MYAPP_")]
                                               public sealed partial record AppConfig(
                                                   [EnvKey("PORT", Default = "8080")] int Port,
                                                   [EnvKey(Required = true)] string DbConnection,
                                                   [EnvKey] string? Tag,
                                                   [EnvKey] bool Verbose,
                                                   [EnvKey] long Big,
                                                   [EnvKey] double Rate,
                                                   [EnvKey] TimeSpan Timeout,
                                                   [EnvKey] Uri? Callback,
                                                   [EnvKey] Guid TenantId,
                                                   [EnvKey] LogLevel Level,
                                                   [EnvKey(Separator = ';')] string[] Hosts,
                                                   [EnvKey] IReadOnlyList<int> Ports,
                                                   [EnvKey] IReadOnlyList<LogLevel> Levels,
                                                   [EnvKey(Parser = typeof(StrictRate))] StrictRate RateBox);
                                               """;

    private const string PropertyModeCarrier = """
                                               using SealedEnv;

                                               namespace TestCode;

                                               [SealedEnv(Prefix = "MYAPP_")]
                                               public sealed partial class AppConfig
                                               {
                                                   [EnvKey(Default = "8080")] public partial int Port { get; }
                                                   [EnvKey(Required = true)] public partial string DbConnection { get; }
                                               }
                                               """;

    [Fact]
    public void Parameter_mode_compiles_end_to_end_at_csharp10()
    {
        var (diagnostics, generated) = GeneratorRunner.RunCompiled(ParameterModeCarrier, LanguageVersion.CSharp10);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("record AppConfig", generated); // the generated partial joined the compilation
    }

    [Fact]
    public void Parameter_mode_does_not_compile_at_csharp9()
    {
        // Below C# 10 both the carrier's file-scoped namespace and the emitted one
        // are rejected (CS8773) — the consumer sees errors, never broken output.
        var (diagnostics, _) = GeneratorRunner.RunCompiled(ParameterModeCarrier, LanguageVersion.CSharp9);

        Assert.Contains(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(diagnostics, static d => d.Id == "CS8773");
    }

    [Fact]
    public void Property_mode_compiles_end_to_end_at_csharp13()
    {
        var (diagnostics, generated) = GeneratorRunner.RunCompiled(PropertyModeCarrier, LanguageVersion.CSharp13);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("public partial int Port", generated);
    }

    [Fact]
    public void Property_mode_does_not_compile_at_csharp12()
    {
        // At C# 12 the 'partial' property modifier is gated (CS8703) in BOTH the
        // user's defining declaration and the emitted implementing one — the
        // generator still emits (it compiles independently of the consumer's
        // LangVersion), but the consumer cannot compile the result.
        var (diagnostics, generated) = GeneratorRunner.RunCompiled(PropertyModeCarrier, LanguageVersion.CSharp12);

        Assert.Contains(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(diagnostics, static d => d.Id == "CS8703");
        Assert.Contains(diagnostics, static d => d.Id == "CS8703" && d.Location.SourceTree?.FilePath.EndsWith(".SealedEnv.g.cs") == true);
    }
}

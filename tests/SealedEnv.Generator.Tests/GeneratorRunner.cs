// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Generator.Tests
// Filename: GeneratorRunner.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SealedEnv.Generators;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     Drives <see cref="SealedEnvGenerator" /> in-memory over a source snippet, with the
///     runtime library referenced so [SealedEnv] resolves exactly as in a real project.
/// </summary>
internal static class GeneratorRunner
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    public static (ImmutableArray<Diagnostic> Diagnostics, string? GeneratedSource) Run(string source)
    {
        return Run(source, LanguageVersion.Default);
    }

    /// <summary>LanguageVersion override: pins what the minimum supported consumer can compile.</summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, string? GeneratedSource) Run(string source, LanguageVersion languageVersion)
    {
        var compilation = CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion)), });

        var runResult = CSharpGeneratorDriver
            .Create(new SealedEnvGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        var generated = runResult.GeneratedTrees.FirstOrDefault(static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"));

        // Binding errors break attribute resolution and would leave FAWMN silent;
        // surface them so a null generated source is never mysterious. (CS0009 is
        // the native-only dlls in the runtime dir — noise, not a binding problem.)
        var bindingErrors = compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS0009");
        foreach (var diagnostic in bindingErrors)
        {
            Console.Error.WriteLine($"TEST SNIPPET {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return (runResult.Diagnostics, generated?.ToString());
    }

    /// <summary>Multi-carrier variant: every generated tree, in hint-name order.</summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<string> GeneratedSources) RunAll(string source)
    {
        var compilation = CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source), });

        var runResult = CSharpGeneratorDriver
            .Create(new SealedEnvGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        return (
            runResult.Diagnostics,
            runResult.GeneratedTrees.Select(static tree => tree.ToString()).ToImmutableArray());
    }

    /// <summary>
    ///     Runs the generator twice on the SAME driver, with the carrier's syntax tree
    ///     shared between compilations and only the unrelated file edited between runs —
    ///     the Roslyn-caching scenario: unchanged inputs must be reused, not re-run.
    ///     Tracking is enabled so the test can inspect IncrementalStepRunReason values.
    /// </summary>
    public static (string? FirstGenerated, string? SecondGenerated, GeneratorRunResult SecondRun) RunWithUnrelatedEdit(
        string carrierSource,
        string unrelatedFirst,
        string unrelatedSecond)
    {
        var carrierTree = CSharpSyntaxTree.ParseText(carrierSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new SealedEnvGenerator().AsSourceGenerator(), },
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                true));

        // RunGenerators returns the (stateful, immutable) driver: reassign so the
        // second run sees the first run's cache.
        driver = driver.RunGenerators(CreateCompilation(new[] { carrierTree, CSharpSyntaxTree.ParseText(unrelatedFirst), }));
        var firstGenerated = driver.GetRunResult().GeneratedTrees.FirstOrDefault(static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"))
            ?.ToString();

        driver = driver.RunGenerators(CreateCompilation(new[] { carrierTree, CSharpSyntaxTree.ParseText(unrelatedSecond), }));
        var secondRun = driver.GetRunResult().Results.Single();
        var secondGenerated = driver.GetRunResult().GeneratedTrees.FirstOrDefault(static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"))
            ?.ToString();

        return (firstGenerated, secondGenerated, secondRun);
    }

    /// <summary>
    ///     Full consumer simulation at a pinned language version: compile the source,
    ///     run the generator, and compile the OUTPUT (user trees + generated trees)
    ///     again — the diagnostics of the second compilation are what a consumer with
    ///     that LangVersion would actually see. This is the C#-floor gate: generated
    ///     code and carrier syntax must both survive the minimum supported version.
    /// </summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, string? GeneratedSource) RunCompiled(string source, LanguageVersion languageVersion)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var first = CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source, parseOptions), });

        // The driver must emit at the SAME language version as the user's tree (a
        // compilation cannot mix versions) — and that is precisely the consumer
        // scenario: the project parses the emitted file at ITS LangVersion, so a
        // too-new construct in the generated code surfaces as the consumer's error.
        var driver = CSharpGeneratorDriver.Create(
            new[] { new SealedEnvGenerator().AsSourceGenerator(), },
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(first, out var outputCompilation, out _);

        var generatedTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"));

        // Same noise filter as Run: CS0009 is the native-only dlls in the runtime dir.
        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS0009")
            .ToImmutableArray();

        return (diagnostics, generatedTree?.ToString());
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree[] syntaxTrees)
    {
        // LanguageVersion is a parse option in Roslyn: every tree carries its own
        // parse options (the callers build them) and the compiler gates features
        // per tree — compilation options need no language version.
        return CSharpCompilation.Create(
            "GeneratorTests.Compilation",
            syntaxTrees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        references.Add(MetadataReference.CreateFromFile(typeof(SealedEnvAttribute).Assembly.Location));

        // The whole .NET runtime directory: covers attribute base types, IParsable<T>,
        // Uri and whatever else a test snippet needs, without cherry-picking facades.
        foreach (var dll in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            catch (BadImageFormatException)
            {
                // native-only satellite in the runtime dir; not a metadata reference
            }
        }

        return references.ToImmutable();
    }
}

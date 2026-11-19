using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SealedEnv.Generators;

namespace SealedEnv.Generator.Tests;

/// <summary>
/// Drives <see cref="SealedEnvGenerator"/> in-memory over a source snippet, with the
/// runtime library referenced so [SealedEnv] resolves exactly as in a real project.
/// </summary>
internal static class GeneratorRunner
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    public static (ImmutableArray<Diagnostic> Diagnostics, string? GeneratedSource) Run(string source)
    {
        var compilation = CreateCompilation(new[] { CSharpSyntaxTree.ParseText(source) });

        var runResult = CSharpGeneratorDriver
            .Create(new SealedEnvGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        var generated = runResult.GeneratedTrees.FirstOrDefault(
            static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"));

        // Binding errors break attribute resolution and would leave FAWMN silent;
        // surface them so a null generated source is never mysterious. (CS0009 is
        // the native-only dlls in the runtime dir — noise, not a binding problem.)
        var bindingErrors = compilation.GetDiagnostics().Where(
            static d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS0009");
        foreach (var diagnostic in bindingErrors)
        {
            Console.Error.WriteLine($"TEST SNIPPET {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return (runResult.Diagnostics, generated?.ToString());
    }

    /// <summary>
    /// Runs the generator twice on the SAME driver, with the carrier's syntax tree
    /// shared between compilations and only the unrelated file edited between runs —
    /// the Roslyn-caching scenario: unchanged inputs must be reused, not re-run.
    /// Tracking is enabled so the test can inspect IncrementalStepRunReason values.
    /// </summary>
    public static (string? FirstGenerated, string? SecondGenerated, GeneratorRunResult SecondRun) RunWithUnrelatedEdit(
        string carrierSource,
        string unrelatedFirst,
        string unrelatedSecond)
    {
        var carrierTree = CSharpSyntaxTree.ParseText(carrierSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new SealedEnvGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        // RunGenerators returns the (stateful, immutable) driver: reassign so the
        // second run sees the first run's cache.
        driver = driver.RunGenerators(CreateCompilation(new[] { carrierTree, CSharpSyntaxTree.ParseText(unrelatedFirst) }));
        var firstGenerated = driver.GetRunResult().GeneratedTrees.FirstOrDefault(
            static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"))?.ToString();

        driver = driver.RunGenerators(CreateCompilation(new[] { carrierTree, CSharpSyntaxTree.ParseText(unrelatedSecond) }));
        var secondRun = driver.GetRunResult().Results.Single();
        var secondGenerated = driver.GetRunResult().GeneratedTrees.FirstOrDefault(
            static tree => tree.FilePath.EndsWith(".SealedEnv.g.cs"))?.ToString();

        return (firstGenerated, secondGenerated, secondRun);
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree[] syntaxTrees) =>
        CSharpCompilation.Create(
            "GeneratorTests.Compilation",
            syntaxTrees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        references.Add(MetadataReference.CreateFromFile(typeof(SealedEnv.SealedEnvAttribute).Assembly.Location));

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

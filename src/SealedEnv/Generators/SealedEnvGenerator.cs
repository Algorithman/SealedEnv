using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SealedEnv.Generators;

/// <summary>
/// SealedEnv source generator: for every type attributed with
/// <c>[SealedEnv]</c>, emits a partial type adding <c>Load()</c>/<c>TryLoad()</c>
/// members that bind the primary-constructor parameters to environment variables.
/// </summary>
/// <remarks>
/// Incremental pipeline: <c>ForAttributeWithMetadataName</c> over the attribute's
/// metadata name (<c>SealedEnv.SealedEnvAttribute</c>) feeding a value model
/// (see <see cref="EnvTargetModel.Equals"/>) into the emitter — unrelated edits do
/// not re-run emission.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class SealedEnvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SealedEnv.SealedEnvAttribute",
            static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax,
            static (context, cancellationToken) => SealedEnvModel.Transform(context, cancellationToken));

        context.RegisterSourceOutput(targets, static (productionContext, target) =>
        {
            productionContext.CancellationToken.ThrowIfCancellationRequested();

            foreach (var diagnostic in target.Diagnostics)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    SveDiagnostics.ById[diagnostic.Id],
                    diagnostic.Location,
                    diagnostic.Args));
            }

            if (!target.SkipGeneration)
            {
                productionContext.AddSource(
                    SealedEnvEmitter.HintName(target),
                    Microsoft.CodeAnalysis.Text.SourceText.From(
                        SealedEnvEmitter.Emit(target, GeneratorVersion),
                        System.Text.Encoding.UTF8));
            }
        });

        // SVE0001: [EnvKey] on parameters outside primary constructors. The carrier
        // pass never sees these nodes, so the pipelines cannot double-report. The
        // equality comparer keeps unrelated edits from re-reporting diagnostics.
        var strayEnvKeys = context.SyntaxProvider
            .CreateSyntaxProvider(
                StrayEnvKeyDetection.IsCandidate,
                static (syntaxContext, cancellationToken) => StrayEnvKeyDetection.Transform(syntaxContext, cancellationToken))
            .WithComparer(DiagnosticInfoComparer.Instance);

        context.RegisterSourceOutput(strayEnvKeys, static (productionContext, diagnostic) =>
        {
            if (diagnostic is not null)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    SveDiagnostics.ById[diagnostic.Id],
                    diagnostic.Location,
                    diagnostic.Args));
            }
        });
    }

    private static string GeneratorVersion =>
        typeof(SealedEnvGenerator).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// Value equality for stray-detection outputs, so unchanged diagnostics are
    /// cached across unrelated edits (Location is deliberately excluded).
    /// </summary>
    private sealed class DiagnosticInfoComparer : IEqualityComparer<DiagnosticInfo?>
    {
        public static readonly DiagnosticInfoComparer Instance = new();

        public bool Equals(DiagnosticInfo? x, DiagnosticInfo? y) =>
            x is null ? y is null : x.Equals(y);

        public int GetHashCode(DiagnosticInfo? obj) => obj?.GetHashCode() ?? 0;
    }
}

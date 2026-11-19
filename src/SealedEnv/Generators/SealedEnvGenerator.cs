// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnvGenerator
// Filename: SealedEnvGenerator.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SealedEnv.Generators;

/// <summary>
///     SealedEnv source generator: for every type attributed with
///     <c>[SealedEnv]</c>, emits a partial type adding <c>Load()</c>/<c>TryLoad()</c>
///     members that bind the primary-constructor parameters to environment variables.
/// </summary>
/// <remarks>
///     Incremental pipeline: <c>ForAttributeWithMetadataName</c> over the attribute's
///     metadata name (<c>SealedEnv.SealedEnvAttribute</c>) feeding a value model
///     (see <see cref="EnvTargetModel.Equals" />) into the emitter — unrelated edits do
///     not re-run emission.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class SealedEnvGenerator : IIncrementalGenerator
{
    private static string GeneratorVersion =>
        typeof(SealedEnvGenerator).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SealedEnv.SealedEnvAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
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
                    SourceText.From(
                        SealedEnvEmitter.Emit(target, GeneratorVersion),
                        Encoding.UTF8));

                // Documentation is emitted as the ConfigurationDocumentation member
                // (inside the .cs above): AddSource can only produce C# files — a
                // raw .md hint name gets compiled as C# and breaks the build.
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

    /// <summary>
    ///     Value equality for stray-detection outputs, so unchanged diagnostics are
    ///     cached across unrelated edits (Location is deliberately excluded).
    /// </summary>
    private sealed class DiagnosticInfoComparer : IEqualityComparer<DiagnosticInfo?>
    {
        public static readonly DiagnosticInfoComparer Instance = new DiagnosticInfoComparer();

        public bool Equals(DiagnosticInfo? x, DiagnosticInfo? y)
        {
            return x is null ? y is null : x.Equals(y);
        }

        public int GetHashCode(DiagnosticInfo? obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }
}

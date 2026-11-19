// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnvGenerator
// Filename: StrayEnvKeyDetection.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 12:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SealedEnv.Generators;

/// <summary>
///     SVE0001: flags <c>[EnvKey]</c> on parameters outside primary constructors
///     (methods, regular constructors, lambdas, delegates, local functions), where it
///     silently does nothing. Non-parameter locations don't reach this pass — the C#
///     compiler itself rejects them via <c>AttributeTargets.Parameter</c> (CS0592).
/// </summary>
internal static class StrayEnvKeyDetection
{
    /// <summary>Cheap syntactic prefilter; the semantic check happens in the transform.</summary>
    public static bool IsCandidate(SyntaxNode node, CancellationToken cancellationToken)
    {
        if (node is not ParameterSyntax parameter)
        {
            return false;
        }

        foreach (var attributeList in parameter.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (HasEnvKeyishName(attribute.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     The <c>[SealedEnv]</c> carrier pass never sees these nodes (primary-constructor
    ///     parameters are excluded here), so the two pipelines cannot double-report.
    ///     Resolution goes through the parameter symbol's attribute data — attribute
    ///     names bind specially and are invisible to GetSymbolInfo.
    /// </summary>
    public static DiagnosticInfo? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol parameterSymbol)
        {
            return null;
        }

        var envKeyAttribute = parameterSymbol
            .GetAttributes()
            .FirstOrDefault(static attribute => SealedEnvModel.IsEnvKeyAttribute(attribute));
        if (envKeyAttribute is null)
        {
            return null; // a user attribute that merely shares the name
        }

        if (parameter.Parent is ParameterListSyntax { Parent: TypeDeclarationSyntax, })
        {
            return null; // legitimate primary-constructor parameter
        }

        return new DiagnosticInfo(
            "SVE0001",
            [parameter.Identifier.ValueText,],
            SealedEnvModel.AttributeLocation(envKeyAttribute));
    }

    private static bool HasEnvKeyishName(NameSyntax name)
    {
        return name switch
        {
            IdentifierNameSyntax identifier => IsEnvKeyIdentifier(identifier.Identifier.ValueText),
            QualifiedNameSyntax qualified => IsEnvKeyIdentifier(qualified.Right.Identifier.ValueText),
            AliasQualifiedNameSyntax alias => IsEnvKeyIdentifier(alias.Name.Identifier.ValueText),
            _ => false,
        };
    }

    private static bool IsEnvKeyIdentifier(string text)
    {
        return text is "EnvKey" or "EnvKeyAttribute";
    }
}

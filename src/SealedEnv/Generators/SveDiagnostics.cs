// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnvGenerator
// Filename: SveDiagnostics.cs
//
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SealedEnv.Generators;

/// <summary>
///     Compile-time diagnostics (SVE prefix). Severities and codes follow the plan §3;
///     SVE0001–SVE0008 and SVE0100 are all live as of Phase 4.
/// </summary>
internal static class SveDiagnostics
{
    public const string Category = "SealedEnv";

    public static readonly DiagnosticDescriptor EnvKeyOutsidePrimaryConstructor = new DiagnosticDescriptor("SVE0001",
        "[EnvKey] on a non-primary-constructor parameter",
        "[EnvKey] is only meaningful on primary constructor parameters of [SealedEnv] carriers; it has no effect on '{0}'", Category,
        DiagnosticSeverity.Error, true,
        "Note: on non-parameter locations the C# compiler itself rejects [EnvKey] (AttributeTargets.Parameter), so this diagnostic only fires for method, constructor, lambda, delegate and local-function parameters.");

    public static readonly DiagnosticDescriptor UnsupportedParameterType = new DiagnosticDescriptor("SVE0002", "Unsupported parameter type",
        "Parameter '{0}' has type '{1}', which has no parser route: use a supported BCL type, a type implementing IParsable<T>, or [EnvKey(Parser = typeof(...))]",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DuplicateResolvedKey = new DiagnosticDescriptor("SVE0003", "Duplicate environment key",
        "Members {0} resolve to the same environment key '{1}'; both will read the same variable", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MalformedDefaultValue = new DiagnosticDescriptor("SVE0004",
        "Default value does not parse as the parameter type",
        "Default value \"{0}\" for parameter '{1}' does not parse as {2}; loading would always fail at runtime", Category, DiagnosticSeverity.Error,
        true,
        "Validated for int, long, double, bool, TimeSpan, Uri, Guid and enum parameters using the same rules the runtime loader applies (invariant culture). string/string[] accept any text, and IParsable<T>/custom-parser types are validated at runtime only — the generator cannot execute their parsers.");

    public static readonly DiagnosticDescriptor CarrierNotSealed = new DiagnosticDescriptor("SVE0005", "Carrier type is not sealed",
        "Type '{0}' is a [SealedEnv] carrier but is not sealed; declare it 'sealed partial record' (record structs are implicitly sealed)", Category,
        DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor InvalidParserType = new DiagnosticDescriptor("SVE0006", "Parser type lacks a usable Parse method",
        "Parser type '{0}' for parameter '{1}' has no 'public static <parameter type> Parse(string)' method", Category, DiagnosticSeverity.Error,
        true,
        "The generated code calls the parser as a method group, so it must be public, static, take exactly one string and return the parameter's type. Non-generic signatures only.");

    public static readonly DiagnosticDescriptor MissingEnvKeyAttribute = new DiagnosticDescriptor("SVE0007",
        "Primary constructor parameter lacks [EnvKey]",
        "Every primary constructor parameter of a [SealedEnv] type must be marked [EnvKey]; parameter '{0}' is not", Category,
        DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MemberNameConflict = new DiagnosticDescriptor("SVE0008",
        "Member name conflicts with a generated member",
        "Type '{0}' already declares '{1}'; SealedEnv generates Load, TryLoad and SealedEnvBind members and cannot add a duplicate member with that name", Category,
        DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor UnsupportedCarrierShape = new DiagnosticDescriptor("SVE0100", "Carrier shape not yet supported",
        "{0} — no Load/TryLoad members are generated for this type", Category, DiagnosticSeverity.Warning, true,
        "Covers nested, generic and ref-struct carriers; a top-level sealed non-generic record/class/record-struct carrier always generates.");

    public static readonly ImmutableDictionary<string, DiagnosticDescriptor> ById =
        new Dictionary<string, DiagnosticDescriptor>
        {
            [EnvKeyOutsidePrimaryConstructor.Id] = EnvKeyOutsidePrimaryConstructor,
            [UnsupportedParameterType.Id] = UnsupportedParameterType,
            [DuplicateResolvedKey.Id] = DuplicateResolvedKey,
            [MalformedDefaultValue.Id] = MalformedDefaultValue,
            [CarrierNotSealed.Id] = CarrierNotSealed,
            [InvalidParserType.Id] = InvalidParserType,
            [MissingEnvKeyAttribute.Id] = MissingEnvKeyAttribute,
            [MemberNameConflict.Id] = MemberNameConflict,
            [UnsupportedCarrierShape.Id] = UnsupportedCarrierShape,
        }.ToImmutableDictionary();

    /// <summary>
    ///     Diagnostics that make the carrier unusable; generation is skipped entirely
    ///     (no half-working Load()). SVE0003 duplicates still generate: behavior stays
    ///     deterministic and the diagnostic explains it.
    /// </summary>
    public static readonly string[] BlockingIds = ["SVE0002", "SVE0004", "SVE0006", "SVE0007", "SVE0008",];
}

/// <summary>
///     A ready-to-report diagnostic captured during extraction: descriptor id plus
///     pre-formatted format arguments. The model carries no symbol references, which
///     keeps it equatable for the incremental cache (see <see cref="EnvTargetModel.Equals" />).
/// </summary>
internal sealed class DiagnosticInfo
{
    public DiagnosticInfo(string id, string[] args, Location location)
    {
        Id = id;
        Args = args;
        Location = location;
    }

    public string Id { get; }
    public string[] Args { get; }
    public Location Location { get; }

    /// <summary>Location is deliberately excluded: it is reference-equal per syntax tree.</summary>
    public bool Equals(DiagnosticInfo? other)
    {
        return other is not null && Id == other.Id && Args.SequenceEqual(other.Args);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DiagnosticInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Id.GetHashCode();
            foreach (var arg in Args)
            {
                hash = (hash * 397) ^ arg.GetHashCode();
            }

            return hash;
        }
    }
}

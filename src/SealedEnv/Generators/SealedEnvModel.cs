// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnvGenerator
// Filename: SealedEnvModel.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SealedEnv.Generators;

/// <summary>Which <see cref="SealedEnvLoader" /> helper a member's generated call targets.</summary>
internal enum MemberKind
{
    Int,
    Long,
    Double,
    Bool,
    String,
    NullableString,
    TimeSpan,
    Uri,
    Guid,
    Enum,
    Parsable,
    Custom,
    StringArray,
}

internal sealed record EnvMemberModel(
    string ParameterName,
    string TypeText, // fully qualified with global:: prefixes, keyword-safe
    string Key, // resolved env key, prefix already applied
    MemberKind Kind,
    bool Required,
    string? Default,
    bool AllowEmpty,
    char Separator,
    string? ParserText, // fully qualified type owning the static Parse for Custom
    bool PassWithNullForgiving) // append ! at the constructor argument
{
    public string VariableName => "__" + ParameterName;

    /// <summary>Local declaration type: reference-typed parse results may be null.</summary>
    public string LocalTypeText => PassWithNullForgiving && Kind != MemberKind.String ? TypeText + "?" : TypeText;
}

/// <summary>
///     The full extraction of one [SealedEnv] carrier. A hand-written value object
///     (not a record): <see cref="Location" /> must not participate in equality, and
///     ImmutableArray members need sequence comparison — this Equals is what lets the
///     incremental pipeline skip emission when nothing relevant changed.
/// </summary>
internal sealed class EnvTargetModel
{
    public EnvTargetModel(
        string? @namespace,
        string typeName,
        string typeDeclaration, // e.g. "public sealed partial record"
        bool isValueType,
        bool emptyAsUnset,
        bool skipGeneration, // a blocking diagnostic says why; only diagnostics are reported
        ImmutableArray<EnvMemberModel> members,
        ImmutableArray<DiagnosticInfo> diagnostics,
        Location diagnosticLocation)
    {
        Namespace = @namespace;
        TypeName = typeName;
        TypeDeclaration = typeDeclaration;
        IsValueType = isValueType;
        EmptyAsUnset = emptyAsUnset;
        SkipGeneration = skipGeneration;
        Members = members;
        Diagnostics = diagnostics;
        DiagnosticLocation = diagnosticLocation;
    }

    public string? Namespace { get; }
    public string TypeName { get; }
    public string TypeDeclaration { get; }
    public bool IsValueType { get; }
    public bool EmptyAsUnset { get; }
    public bool SkipGeneration { get; }
    public ImmutableArray<EnvMemberModel> Members { get; }
    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    public Location DiagnosticLocation { get; }

    public bool Equals(EnvTargetModel? other)
    {
        return other is not null &&
               Namespace == other.Namespace &&
               TypeName == other.TypeName &&
               TypeDeclaration == other.TypeDeclaration &&
               IsValueType == other.IsValueType &&
               EmptyAsUnset == other.EmptyAsUnset &&
               SkipGeneration == other.SkipGeneration &&
               Members.SequenceEqual(other.Members) &&
               Diagnostics.SequenceEqual(other.Diagnostics);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EnvTargetModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Namespace?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ TypeName.GetHashCode();
            hash = (hash * 397) ^ TypeDeclaration.GetHashCode();
            hash = (hash * 397) ^ IsValueType.GetHashCode();
            hash = (hash * 397) ^ EmptyAsUnset.GetHashCode();
            hash = (hash * 397) ^ SkipGeneration.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
///     Extracts an <see cref="EnvTargetModel" /> from a type attributed with
///     <c>[SealedEnv]</c>. Pure data out — no symbols survive, so the incremental
///     cache compares models by value.
/// </summary>
internal static class SealedEnvModel
{
    private const string AttributeNamespace = "SealedEnv";

    /// <summary>Members the generated partial adds to the user's carrier type.</summary>
    internal static readonly string[] GeneratedMemberNames = ["Load", "TryLoad", "SealedEnvBind",];

    public static EnvTargetModel Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var typeSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var attributeData = context.Attributes[0];

        string? prefix = null;
        var emptyAsUnset = true;
        foreach (var named in attributeData.NamedArguments)
        {
            switch (named.Key)
            {
                case "Prefix":
                    prefix = (string?)named.Value.Value;
                    break;
                case "EmptyAsUnset":
                    emptyAsUnset = (bool)named.Value.Value!;
                    break;
            }
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var effectivePrefix = prefix is { Length: > 0, } nonEmptyPrefix ? nonEmptyPrefix : string.Empty;
        var attributeLocation = AttributeLocation(attributeData);

        // SVE0009: whitespace around the prefix silently produces keys no environment
        // can match — " myapp_" is an invisible editor typo, never intent (it would
        // yield " MYAPP_PORT"). Internal whitespace stays legal (visible, plausibly
        // deliberate); empty string stays legal — it is the documented way to read
        // global keys. (The Length guard matters: "".Trim() == "" is true and would
        // block the legal empty prefix.)
        if (prefix is { Length: > 0, } && prefix != prefix.Trim())
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0009",
                [prefix, typeSymbol.Name,],
                attributeLocation));
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        // SVE0005: reference-type carriers must be sealed (plan §1); record structs
        // are implicitly sealed and pass here.
        if (!typeSymbol.IsValueType && !typeSymbol.IsSealed)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0005",
                [typeSymbol.Name,],
                attributeLocation));
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        // Nullable<T> over a ref struct does not exist, so SealedEnvBind's
        // `TypeName?` return would not compile — reject the shape instead.
        if (typeSymbol.IsRefLikeType)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0100",
                [$"Type '{typeSymbol.Name}' is a ref struct; ref struct [SealedEnv] carriers are not supported",],
                attributeLocation));
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        // SVE0008: the generated partial adds these members to the user's type —
        // a same-name declaration in the user's part would be a CS0111 duplicate.
        foreach (var generatedName in GeneratedMemberNames)
        {
            if (typeSymbol.GetMembers(generatedName).Length > 0)
            {
                diagnostics.Add(new DiagnosticInfo(
                    "SVE0008",
                    [typeSymbol.Name, generatedName,],
                    attributeLocation));
                return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
            }
        }

        var declaration = BuildTypeDeclaration(typeSymbol, diagnostics, attributeLocation);
        if (declaration is null)
        {
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        var members = ImmutableArray.CreateBuilder<EnvMemberModel>();
        if (ParameterList(context.TargetNode) is { } parameterList)
        {
            foreach (var parameterSyntax in parameterList.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (context.SemanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) is not IParameterSymbol parameter)
                {
                    continue;
                }

                ExtractMember(
                    parameter,
                    effectivePrefix,
                    emptyAsUnset,
                    members,
                    diagnostics);
            }
        }

        DetectDuplicateKeys(members, diagnostics, attributeLocation);
        var blocking = diagnostics.Any(static d => SveDiagnostics.BlockingIds.Contains(d.Id));

        return new EnvTargetModel(
            NamespaceName(typeSymbol),
            typeSymbol.Name,
            declaration,
            typeSymbol.IsValueType,
            emptyAsUnset,
            blocking,
            members.ToImmutable(),
            diagnostics.ToImmutable(),
            attributeLocation);
    }

    private static EnvTargetModel NotGeneratable(
        INamedTypeSymbol typeSymbol,
        bool emptyAsUnset,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Location attributeLocation)
    {
        return new EnvTargetModel(
            NamespaceName(typeSymbol),
            typeSymbol.Name,
            string.Empty,
            false,
            emptyAsUnset,
            true,
            ImmutableArray<EnvMemberModel>.Empty,
            diagnostics.ToImmutable(),
            attributeLocation);
    }

    /// <summary>
    ///     Location of the attribute application, used to anchor diagnostics.
    /// </summary>
    internal static Location AttributeLocation(AttributeData attributeData)
    {
        return attributeData.ApplicationSyntaxReference is { } syntaxReference
            ? syntaxReference.SyntaxTree.GetLocation(syntaxReference.Span)
            : Location.None;
    }

    private static string? NamespaceName(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false, } ns ? ns.ToDisplayString() : null;
    }

    private static ParameterListSyntax? ParameterList(SyntaxNode node)
    {
        return node switch
        {
            RecordDeclarationSyntax record => record.ParameterList,
            ClassDeclarationSyntax @class => @class.ParameterList,
            StructDeclarationSyntax @struct => @struct.ParameterList,
            _ => null,
        };
    }

    /// <summary>
    ///     Renders the partial type declaration header. Returns null (with a diagnostic)
    ///     for shapes the generator does not support yet: nested and generic carriers.
    /// </summary>
    private static string? BuildTypeDeclaration(INamedTypeSymbol typeSymbol, ImmutableArray<DiagnosticInfo>.Builder diagnostics, Location location)
    {
        if (typeSymbol.ContainingType is not null)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0100",
                [$"Type '{typeSymbol.Name}' is nested; nested [SealedEnv] carriers are not supported yet",],
                location));
            return null;
        }

        if (typeSymbol.Arity > 0)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0100",
                [$"Type '{typeSymbol.Name}' is generic; generic [SealedEnv] carriers are not supported yet",],
                location));
            return null;
        }

        var accessibility = typeSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            _ => "internal", // top-level un-annotated types are implicitly internal
        };

        var modifiers = string.Empty;
        if (typeSymbol.IsValueType)
        {
            if (typeSymbol.IsReadOnly)
            {
                modifiers += "readonly ";
            }
        }
        else if (typeSymbol.IsSealed)
        {
            modifiers = "sealed ";
        }

        var kind = typeSymbol.IsRecord
            ? typeSymbol.IsValueType ? "record struct" : "record"
            : typeSymbol.IsValueType
                ? "struct"
                : "class";

        return $"{accessibility} {modifiers}partial {kind}".Trim();
    }

    private static void ExtractMember(
        IParameterSymbol parameter,
        string prefix,
        bool emptyAsUnset,
        ImmutableArray<EnvMemberModel>.Builder members,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var attribute = parameter
            .GetAttributes()
            .FirstOrDefault(IsEnvKeyAttribute);
        if (attribute is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0007",
                [parameter.Name,],
                parameter.Locations.FirstOrDefault() ?? Location.None));
            return;
        }

        var required = false;
        string? defaultValue = null;
        var allowEmpty = false;
        var separator = ',';
        INamedTypeSymbol? parser = null;
        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "Required":
                    required = (bool)named.Value.Value!;
                    break;
                case "Default":
                    defaultValue = (string?)named.Value.Value;
                    break;
                case "AllowEmpty":
                    allowEmpty = (bool)named.Value.Value!;
                    break;
                case "Separator":
                    separator = (char)named.Value.Value!;
                    break;
                case "Parser":
                    parser = named.Value.Value as INamedTypeSymbol;
                    break;
            }
        }

        string? explicitKey = null;
        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string key)
        {
            explicitKey = key;
        }

        var kind = Route(parameter.Type, parser);
        if (kind is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0002",
                [parameter.Name, parameter.Type.ToDisplayString(),],
                parameter.Locations.FirstOrDefault() ?? Location.None));
            return;
        }

        var attributeLocation = AttributeLocation(attribute);

        // SVE0006: the parser is emitted as a method group, so it must be callable
        // exactly — catching this here beats a confusing error inside generated code.
        if (parser is not null && !ParserHasValidParseMethod(parser, parameter.Type))
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0006",
                [parser.ToDisplayString(), parameter.Name,],
                attributeLocation));
            return;
        }

        // SVE0004: a malformed Default would make every load fail forever; the same
        // rules the runtime loader applies are checked at compile time.
        if (defaultValue is not null && !DefaultParsesAs(kind.Value, defaultValue, parameter.Type))
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0004",
                [defaultValue, parameter.Name, parameter.Type.ToDisplayString(),],
                attributeLocation));
            return;
        }

        var resolvedKey = prefix + (explicitKey ?? EnvKeyDerivation.Derive(parameter.Name));

        members.Add(new EnvMemberModel(
            parameter.Name,
            parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            resolvedKey,
            kind.Value,
            required,
            defaultValue,
            allowEmpty,
            separator,
            parser?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            PassWithNullForgiving(parameter.Type, kind.Value)));
    }

    /// <summary>
    ///     Whether the constructor argument needs a <c>!</c>: the helper may return null
    ///     (NullableString, Uri) or the parsed reference type itself may be null-declared.
    /// </summary>
    private static bool PassWithNullForgiving(ITypeSymbol type, MemberKind kind)
    {
        return kind switch
        {
            MemberKind.NullableString or MemberKind.Uri => true,
            MemberKind.Parsable or MemberKind.Custom => type.IsReferenceType,
            _ => false,
        };
    }

    /// <summary>SVE0003: two members resolving to the same key silently share one value.</summary>
    private static void DetectDuplicateKeys(
        ImmutableArray<EnvMemberModel>.Builder members,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Location location)
    {
        foreach (var group in members
                     .GroupBy(static m => m.Key, StringComparer.Ordinal)
                     .Where(static g => g.Count() > 1))
        {
            var names = string.Join(" and ", group.Select(static m => $"'{m.ParameterName}'"));
            diagnostics.Add(new DiagnosticInfo(
                "SVE0003",
                [names, group.Key,],
                location));
        }
    }

    internal static bool IsEnvKeyAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass is { } attributeClass &&
               attributeClass.MetadataName == "EnvKeyAttribute" &&
               attributeClass.ContainingNamespace is { IsGlobalNamespace: false, } ns &&
               ns.ToDisplayString() == AttributeNamespace;
    }

    /// <summary>
    ///     SVE0006: the generated call passes <c>ParserType.Parse</c> as a
    ///     <c>Func&lt;string, T&gt;</c> method group — public, static, non-generic, one
    ///     string in, the parameter's exact type out.
    /// </summary>
    private static bool ParserHasValidParseMethod(INamedTypeSymbol parser, ITypeSymbol parameterType)
    {
        return parser.GetMembers("Parse").OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public &&
            method.Arity == 0 &&
            method.Parameters.Length == 1 &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, parameterType));
    }

    /// <summary>
    ///     SVE0004: mirrors the runtime parse rules per type (invariant culture). Types
    ///     without a compile-time-checkable parser (string, string[], IParsable&lt;T&gt;,
    ///     custom Parser) accept anything here and are validated at runtime.
    /// </summary>
    private static bool DefaultParsesAs(MemberKind kind, string defaultValue, ITypeSymbol parameterType)
    {
        switch (kind)
        {
            case MemberKind.Int:
                return int.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            case MemberKind.Long:
                return long.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            case MemberKind.Double:
                return double.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            case MemberKind.Bool:
                // exact token set of the runtime helper, OrdinalIgnoreCase
                return defaultValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       defaultValue.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                       defaultValue.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                       defaultValue.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                       defaultValue is "1" or "0";
            case MemberKind.TimeSpan:
                return TimeSpan.TryParse(defaultValue, CultureInfo.InvariantCulture, out _);
            case MemberKind.Uri:
                // Mirrors the runtime rule (SealedEnvLoader.HasExplicitUriScheme) — keep in
                // sync: on Unix, Uri.TryCreate(UriKind.Absolute) also accepts POSIX paths
                // ("/relative" becomes file:///relative), making the bare check
                // platform-dependent. Require an RFC 3986 scheme in the text itself.
                return HasExplicitUriScheme(defaultValue) &&
                       Uri.TryCreate(defaultValue, UriKind.Absolute, out _);
            case MemberKind.Guid:
                return Guid.TryParse(defaultValue, out _);
            case MemberKind.Enum:
                return EnumDefaultMatches((INamedTypeSymbol)parameterType, defaultValue);
            default:
                return true; // string-ish/collection/custom: nothing to reject at compile time
        }
    }

    /// <summary>
    ///     Copy of the runtime <c>SealedEnvLoader.HasExplicitUriScheme</c> rule (the
    ///     generator cannot reference the runtime assembly) — keep the two in sync.
    /// </summary>
    private static bool HasExplicitUriScheme(string raw)
    {
        if (raw.Length == 0 || !((raw[0] >= 'a' && raw[0] <= 'z') || (raw[0] >= 'A' && raw[0] <= 'Z')))
        {
            return false;
        }

        foreach (var ch in raw)
        {
            if (ch == ':')
            {
                return true;
            }

            var letterOrDigit = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
            if (!letterOrDigit && ch != '+' && ch != '-' && ch != '.')
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Mirrors runtime <c>Enum.TryParse</c>: member names case-insensitively (comma
    ///     separated for flag combos), or any number in the underlying type's range —
    ///     defined member or not.
    /// </summary>
    private static bool EnumDefaultMatches(INamedTypeSymbol enumType, string defaultValue)
    {
        var memberNames = new HashSet<string>(
            enumType.GetMembers().OfType<IFieldSymbol>()
                .Where(static field => field.ConstantValue is not null)
                .Select(static field => field.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in defaultValue.Split(','))
        {
            var token = entry.Trim();
            if (memberNames.Contains(token))
            {
                continue;
            }

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    ///     Picks the loader helper for a parameter type. Unsupported shapes return null
    ///     (reported as SVE0002): no Nullable&lt;T&gt; values, no parsed-element arrays,
    ///     no collections other than string[], and no shadowing risk (System types are
    ///     matched by namespace + name, not by name alone).
    /// </summary>
    private static MemberKind? Route(ITypeSymbol type, INamedTypeSymbol? parser)
    {
        if (parser is not null)
        {
            return MemberKind.Custom;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Int32: return MemberKind.Int;
            case SpecialType.System_Int64: return MemberKind.Long;
            case SpecialType.System_Double: return MemberKind.Double;
            case SpecialType.System_Boolean: return MemberKind.Bool;
            case SpecialType.System_String:
                return type.NullableAnnotation == NullableAnnotation.Annotated
                    ? MemberKind.NullableString
                    : MemberKind.String;
        }

        if (type is IArrayTypeSymbol array && array.ElementType.SpecialType == SpecialType.System_String)
        {
            return MemberKind.StringArray;
        }

        if (type is INamedTypeSymbol named)
        {
            // Matched by namespace + name, never by name alone: a user type called
            // `TimeSpan` in their own namespace must not hijack the System route.
            if (IsSystemType(named, "TimeSpan"))
            {
                return MemberKind.TimeSpan;
            }

            if (IsSystemType(named, "Uri"))
            {
                return MemberKind.Uri;
            }

            if (IsSystemType(named, "Guid"))
            {
                return MemberKind.Guid;
            }

            if (named.TypeKind == TypeKind.Enum)
            {
                return MemberKind.Enum;
            }

            if (ImplementsParsableOfSelf(named))
            {
                return MemberKind.Parsable;
            }
        }

        return null;
    }

    private static bool IsSystemType(INamedTypeSymbol named, string name)
    {
        return named.Name == name &&
               named.OriginalDefinition.ContainingNamespace is { IsGlobalNamespace: false, } containing &&
               containing.ToDisplayString() == "System";
    }

    private static bool ImplementsParsableOfSelf(INamedTypeSymbol named)
    {
        return named.AllInterfaces.Any(i =>
            i.OriginalDefinition is { MetadataName: "IParsable`1", } definition &&
            definition.ContainingNamespace is { IsGlobalNamespace: false, } ns &&
            ns.ToDisplayString() == "System" &&
            i.TypeArguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], named));
    }
}

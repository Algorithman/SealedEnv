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
    string? Description, // [EnvKey(Description = ...)] — surfaced by the doc export
    bool PassWithNullForgiving, // append ! at the constructor argument
    string AnnotatedTypeText = "", // includes ? on annotated reference types; property-mode fields/accessors/ctor params
    string AccessibilityText = "") // declared accessibility of an [EnvKey] partial property, verbatim
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
        Location diagnosticLocation,
        bool generateDocumentation = false,
        bool propertyMode = false)
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
        GenerateDocumentation = generateDocumentation;
        PropertyMode = propertyMode;
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
    public bool GenerateDocumentation { get; }

    /// <summary>
    ///     The carrier binds [EnvKey] partial properties instead of primary-constructor
    ///     parameters: the emitter adds the implementing declarations plus the private
    ///     backing fields and the constructor Load builds the carrier through.
    /// </summary>
    public bool PropertyMode { get; }

    /// <summary>The first word of <see cref="TypeDeclaration" /> — the type's accessibility.</summary>
    public string TypeAccessibility => TypeDeclaration.Split(' ')[0];

    public bool Equals(EnvTargetModel? other)
    {
        return other is not null &&
               Namespace == other.Namespace &&
               TypeName == other.TypeName &&
               TypeDeclaration == other.TypeDeclaration &&
               IsValueType == other.IsValueType &&
               EmptyAsUnset == other.EmptyAsUnset &&
               SkipGeneration == other.SkipGeneration &&
               GenerateDocumentation == other.GenerateDocumentation &&
               PropertyMode == other.PropertyMode &&
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
            hash = (hash * 397) ^ GenerateDocumentation.GetHashCode();
            hash = (hash * 397) ^ PropertyMode.GetHashCode();
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
        var generateDocumentation = false;
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
                case "GenerateDocumentation":
                    generateDocumentation = (bool)named.Value.Value!;
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
        // The documentation member joins the list only when docs are enabled.
        var generatedMemberNames = generateDocumentation
            ? GeneratedMemberNames.Append("ConfigurationDocumentation").ToArray()
            : GeneratedMemberNames;
        foreach (var generatedName in generatedMemberNames)
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
        var primaryParameters = ResolvePrimaryParameters(context, typeSymbol, cancellationToken);

        var envKeyProperties = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => property.GetAttributes().Any(IsEnvKeyAttribute))
            // A user-implemented partial property surfaces as definition + implementation
            // symbols; the implementation part carries no decision of its own and would
            // double-report SVE0012.
            .Where(static property => property.PartialDefinitionPart is null)
            .ToImmutableArray();

        if (envKeyProperties.Length > 0)
        {
            return TransformPropertyMode(
                typeSymbol,
                envKeyProperties,
                primaryParameters,
                declaration,
                effectivePrefix,
                emptyAsUnset,
                generateDocumentation,
                attributeLocation,
                members,
                diagnostics,
                cancellationToken);
        }

        if (primaryParameters is null)
        {
            // No primary constructor and no [EnvKey]-marked property: an unmarked
            // partial property is the only plausible binding candidate the user
            // declared, so it earns the precise SVE0007 (forgotten [EnvKey]) instead
            // of the blunter SVE0010. Unrelated partial properties are indistinguishable
            // from env candidates without the attribute — this only fires when the
            // carrier has no other binding surface at all.
            var unmarkedProperties = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(static property => property.IsPartialDefinition &&
                       !property.IsIndexer &&
                       !property.IsStatic &&
                       property.PartialDefinitionPart is null)
                .ToImmutableArray();
            if (unmarkedProperties.Length > 0)
            {
                foreach (var property in unmarkedProperties)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    diagnostics.Add(new DiagnosticInfo(
                        "SVE0007",
                        [property.Name,],
                        property.Locations.FirstOrDefault() ?? Location.None));
                }

                return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
            }

            diagnostics.Add(new DiagnosticInfo(
                "SVE0010",
                [typeSymbol.Name,],
                attributeLocation));
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        foreach (var parameter in primaryParameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExtractMember(
                parameter.Name,
                parameter.Type,
                ParameterLocation(parameter),
                parameter.GetAttributes(),
                effectivePrefix,
                emptyAsUnset,
                members,
                diagnostics);
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
            attributeLocation,
            generateDocumentation);
    }

    /// <summary>
    ///     The attributed part has no parameter list: either the carrier has no
    ///     primary constructor at all (SVE0010), or this is a partial record whose
    ///     primary constructor lives on another part. The type symbol sees the
    ///     whole declaration across parts — ask it. Implicitly-declared ctors are
    ///     excluded: the record copy constructor (one parameter of the carrier's
    ///     own type!) must not be mistaken for the primary. If several explicit
    ///     parameterized ctors exist (a hand-written chaining ctor), the
    ///     parameter count wins; records have exactly one primary.
    /// </summary>
    private static ImmutableArray<IParameterSymbol>? ResolvePrimaryParameters(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol typeSymbol,
        CancellationToken cancellationToken)
    {
        var syntaxParameters = ParameterList(context.TargetNode)?.Parameters;
        if (syntaxParameters is { } parameters)
        {
            return parameters
                .Select(parameterSyntax => (ISymbol?)context.SemanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken))
                .OfType<IParameterSymbol>()
                .ToImmutableArray();
        }

        return typeSymbol.InstanceConstructors
            .Where(static ctor => !ctor.IsImplicitlyDeclared && ctor.Parameters.Length > 0)
            .OrderByDescending(static ctor => ctor.Parameters.Length)
            .FirstOrDefault()
            ?.Parameters;
    }

    /// <summary>
    ///     Property mode: the carrier binds [EnvKey] partial properties instead of
    ///     primary-constructor parameters. The emitter supplies the implementation
    ///     parts plus the constructor Load builds the carrier through — so user
    ///     declarations of either are conflicts (SVE0012 / SVE0013), and mixing both
    ///     binding styles in one carrier is SVE0011.
    /// </summary>
    private static EnvTargetModel TransformPropertyMode(
        INamedTypeSymbol typeSymbol,
        ImmutableArray<IPropertySymbol> envKeyProperties,
        ImmutableArray<IParameterSymbol>? primaryParameters,
        string declaration,
        string prefix,
        bool emptyAsUnset,
        bool generateDocumentation,
        Location attributeLocation,
        ImmutableArray<EnvMemberModel>.Builder members,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        // SVE0011: both styles marked [EnvKey] — the generated constructor could not
        // serve the primary ctor and the properties at once.
        if (primaryParameters is { Length: > 0, } parameters &&
            parameters.Any(parameter => parameter.GetAttributes().Any(IsEnvKeyAttribute)))
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0011",
                [typeSymbol.Name,],
                attributeLocation));
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        // SVE0013: any explicit instance constructor (the record primary constructor
        // included — it is user-declared) collides with the generated one and would
        // otherwise route user constructions to a carrier with default fields.
        if (typeSymbol.InstanceConstructors.Any(static ctor => !ctor.IsImplicitlyDeclared))
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0013",
                [typeSymbol.Name,],
                attributeLocation));
            return NotGeneratable(typeSymbol, emptyAsUnset, diagnostics, attributeLocation);
        }

        foreach (var property in envKeyProperties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!PropertyShapeIsValid(property, typeSymbol, diagnostics))
            {
                continue;
            }

            if (BackingFieldCollides(property, typeSymbol, attributeLocation, diagnostics))
            {
                continue;
            }

            ExtractMember(
                property.Name,
                property.Type,
                property.Locations.FirstOrDefault() ?? Location.None,
                property.GetAttributes(),
                prefix,
                emptyAsUnset,
                members,
                diagnostics,
                AnnotatedTypeText(property.Type),
                AccessibilityText(property.DeclaredAccessibility));
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
            attributeLocation,
            generateDocumentation,
            propertyMode: true);
    }

    /// <summary>
    ///     SVE0012: the generator implements the property, so anything beyond a
    ///     partial get-only instance definition cannot be honored — a wrong shape
    ///     here either cannot be implemented (set accessor, implementation part
    ///     already present) or would silently bind nothing (non-partial, static).
    /// </summary>
    private static bool PropertyShapeIsValid(
        IPropertySymbol property,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var problem =
            property.IsIndexer ? "an indexer cannot be an environment binding"
            : property.IsStatic ? "it must not be static"
            : property.IsRequired ? "the 'required' modifier conflicts with the generated constructor"
            : !property.IsPartialDefinition ? "it must be declared 'partial' so SealedEnv can supply the implementation"
            : property.PartialImplementationPart is not null ? "it already has an implementation part"
            : property.SetMethod is not null ? "it must be get-only (no set or init accessor)"
            : property.GetMethod is null ? "it must have a get accessor"
            // An unmodified accessor inherits the property's accessibility in the
            // symbol model — only an explicitly declared one diverges from it.
            : property.GetMethod.DeclaredAccessibility != property.DeclaredAccessibility
                ? "the get accessor must not declare its own accessibility"
            : null;

        if (problem is null)
        {
            return true;
        }

        diagnostics.Add(new DiagnosticInfo(
            "SVE0012",
            [property.Name, typeSymbol.Name, problem,],
            property.Locations.FirstOrDefault() ?? Location.None));
        return false;
    }

    /// <summary>
    ///     SVE0008 for property mode: the emitter adds a private '__' + name backing
    ///     field per partial property; a user member with that exact name would be
    ///     a CS0102 duplicate in the generated partial.
    /// </summary>
    private static bool BackingFieldCollides(
        IPropertySymbol property,
        INamedTypeSymbol typeSymbol,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var fieldName = "__" + property.Name;
        if (typeSymbol.GetMembers(fieldName).Length == 0)
        {
            return false;
        }

        diagnostics.Add(new DiagnosticInfo(
            "SVE0008",
            [typeSymbol.Name, fieldName,],
            location));
        return true;
    }

    /// <summary>
    ///     Fully qualified display text that KEEPS nullable reference annotations —
    ///     FullyQualifiedFormat drops them, and the emitted field, implementing
    ///     declaration and constructor parameter must match the declared property
    ///     type exactly under #nullable enable.
    /// </summary>
    private static string AnnotatedTypeText(ITypeSymbol type)
    {
        return type.ToDisplayString(AnnotatedTypeFormat);
    }

    private static readonly SymbolDisplayFormat AnnotatedTypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static string AccessibilityText(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };
    }

    private static Location ParameterLocation(IParameterSymbol parameter)
    {
        return parameter.Locations.FirstOrDefault() ?? Location.None;
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

    /// <summary>
    ///     Extracts one env binding from a member's [EnvKey] attribute — shared by
    ///     primary-constructor parameters and partial properties. <paramref name="memberName" />
    ///     feeds both the derived key and the error display name ('Type.Member').
    /// </summary>
    private static void ExtractMember(
        string memberName,
        ITypeSymbol memberType,
        Location memberLocation,
        ImmutableArray<AttributeData> attributes,
        string prefix,
        bool emptyAsUnset,
        ImmutableArray<EnvMemberModel>.Builder members,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        string annotatedTypeText = "",
        string accessibilityText = "")
    {
        var attribute = attributes.FirstOrDefault(IsEnvKeyAttribute);
        if (attribute is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0007",
                [memberName,],
                memberLocation));
            return;
        }

        var required = false;
        string? defaultValue = null;
        var allowEmpty = false;
        var separator = ',';
        string? description = null;
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
                case "Description":
                    description = (string?)named.Value.Value;
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

        var kind = Route(memberType, parser);
        if (kind is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0002",
                [memberName, memberType.ToDisplayString(),],
                memberLocation));
            return;
        }

        var attributeLocation = AttributeLocation(attribute);

        // SVE0006: the parser is emitted as a method group, so it must be callable
        // exactly — catching this here beats a confusing error inside generated code.
        if (parser is not null && !ParserHasValidParseMethod(parser, memberType))
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0006",
                [parser.ToDisplayString(), memberName,],
                attributeLocation));
            return;
        }

        // SVE0004: a malformed Default would make every load fail forever; the same
        // rules the runtime loader applies are checked at compile time.
        if (defaultValue is not null && !DefaultParsesAs(kind.Value, defaultValue, memberType))
        {
            diagnostics.Add(new DiagnosticInfo(
                "SVE0004",
                [defaultValue, memberName, memberType.ToDisplayString(),],
                attributeLocation));
            return;
        }

        var resolvedKey = prefix + (explicitKey ?? EnvKeyDerivation.Derive(memberName));

        members.Add(new EnvMemberModel(
            memberName,
            memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            resolvedKey,
            kind.Value,
            required,
            defaultValue,
            allowEmpty,
            separator,
            parser?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            description,
            PassWithNullForgiving(memberType, kind.Value),
            annotatedTypeText,
            accessibilityText));
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

    /// <summary>
    ///     SVE0003: two members resolving to the same key silently share one value.
    ///     The second pass catches case variants ("port" vs "PORT"): they are distinct
    ///     keys ordinally, but on case-insensitive platforms (Windows) they read the
    ///     SAME variable — a platform-dependent trap worth naming at compile time.
    /// </summary>
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

        foreach (var group in members
                     .GroupBy(static m => m.Key, StringComparer.OrdinalIgnoreCase)
                     .Where(static g => g.Count() > 1)
                     .Where(static g => g.Select(static m => m.Key)
                         .Distinct(StringComparer.Ordinal)
                         .Count() == g.Count()))
        {
            var names = string.Join(" and ", group.Select(static m => $"'{m.ParameterName}'"));
            diagnostics.Add(new DiagnosticInfo(
                "SVE0003",
                [names, $"{group.Key} (identical modulo case)",],
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

// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Generator.Tests
// Filename: PartialPropertyTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     The partial-property carrier surface (C# 13): a [SealedEnv] carrier may bind
///     [EnvKey] get-only partial properties instead of primary-constructor
///     parameters. The generator supplies the implementation parts, the private
///     backing fields and the constructor Load builds through — so user declarations
///     of any of those are conflicts (SVE0012 / SVE0013), and mixing both binding
///     styles is SVE0011.
/// </summary>
public class PartialPropertyTests
{
    private const string HappyPathClass = """
                                          using System;
                                          using SealedEnv;

                                          namespace TestCode;

                                          [SealedEnv(Prefix = "MYAPP_")]
                                          public sealed partial class AppConfig
                                          {
                                              [EnvKey("PORT", Default = "8080")] public partial int Port { get; }
                                              [EnvKey(Required = true)] public partial string DbConnection { get; }
                                              [EnvKey] public partial string? Tag { get; }
                                          }
                                          """;

    private const string RecordCarrier = """
                                         using System;
                                         using SealedEnv;

                                         namespace TestCode;

                                         [SealedEnv(Prefix = "MYAPP_")]
                                         public sealed partial record AppConfig
                                         {
                                             [EnvKey] public partial int Port { get; }
                                             [EnvKey] public partial Uri? CallbackUrl { get; }
                                         }
                                         """;

    private const string KeywordNameCarrier = """
                                               using SealedEnv;

                                               namespace TestCode;

                                               [SealedEnv(Prefix = "MYAPP_")]
                                               public sealed partial record AppConfig
                                               {
                                                   [EnvKey] public partial string @class { get; }
                                               }
                                               """;

    private const string MixedStyles = """
                                        using SealedEnv;

                                        namespace TestCode;

                                        [SealedEnv]
                                        public sealed partial record AppConfig([EnvKey] int Port)
                                        {
                                            [EnvKey] public partial int Retries { get; }
                                        }
                                        """;

    private const string PropertyPlusUnmarkedCtorParam = """
                                                          using SealedEnv;

                                                          namespace TestCode;

                                                          [SealedEnv]
                                                          public sealed partial record AppConfig(int Port)
                                                          {
                                                              [EnvKey] public partial int Retries { get; }
                                                          }
                                                          """;

    private const string UserConstructor = """
                                            using SealedEnv;

                                            namespace TestCode;

                                            [SealedEnv]
                                            public sealed partial class AppConfig
                                            {
                                                [EnvKey] public partial int Port { get; }

                                                public AppConfig(int port)
                                                {
                                                    Port = port;
                                                }
                                            }
                                            """;

    private const string NonPartialProperty = """
                                               using SealedEnv;

                                               namespace TestCode;

                                               [SealedEnv]
                                               public sealed partial class AppConfig
                                               {
                                                   [EnvKey] public int Port { get; } // not partial — the generator cannot implement it
                                               }
                                               """;

    private const string SetAccessor = """
                                        using SealedEnv;

                                        namespace TestCode;

                                        [SealedEnv]
                                        public sealed partial class AppConfig
                                        {
                                            [EnvKey] public partial int Port { get; set; }
                                        }
                                        """;

    private const string InitAccessor = """
                                         using SealedEnv;

                                         namespace TestCode;

                                         [SealedEnv]
                                         public sealed partial class AppConfig
                                         {
                                             [EnvKey] public partial int Port { get; init; }
                                         }
                                         """;

    private const string StaticProperty = """
                                           using SealedEnv;

                                           namespace TestCode;

                                           [SealedEnv]
                                           public sealed partial class AppConfig
                                           {
                                               [EnvKey] public static partial int Port { get; }
                                           }
                                           """;

    private const string ImplementedProperty = """
                                                using SealedEnv;

                                                namespace TestCode;

                                                [SealedEnv]
                                                public sealed partial class AppConfig
                                                {
                                                    [EnvKey] public partial int Port { get; }

                                                    public partial int Port { get => 42; }
                                                }
                                                """;

    private const string IndexerBinding = """
                                           using SealedEnv;

                                           namespace TestCode;

                                           [SealedEnv]
                                           public sealed partial class AppConfig
                                           {
                                               [EnvKey] public partial string this[int index] { get; }
                                           }
                                           """;

    private const string BackingFieldCollision = """
                                                  using SealedEnv;

                                                  namespace TestCode;

                                                  [SealedEnv]
                                                  public sealed partial class AppConfig
                                                  {
                                                      [EnvKey] public partial int Port { get; }

                                                      private readonly int __Port; // collides with the generated backing field
                                                  }
                                                  """;

    private const string DuplicatePropertyKeys = """
                                                  using SealedEnv;

                                                  namespace TestCode;

                                                  [SealedEnv(Prefix = "MYAPP_")]
                                                  public sealed partial record AppConfig
                                                  {
                                                      [EnvKey("PORT")] public partial int Port { get; }
                                                      [EnvKey("PORT")] public partial int ListenPort { get; }
                                                  }
                                                  """;

    private const string UnsupportedPropertyType = """
                                                    using System.Collections.Generic;
                                                    using SealedEnv;

                                                    namespace TestCode;

                                                    [SealedEnv]
                                                    public sealed partial class AppConfig
                                                    {
                                                        [EnvKey] public partial List<string> Tags { get; }
                                                    }
                                                    """;

    private const string MissingEnvKeyOnProperty = """
                                                    using SealedEnv;

                                                    namespace TestCode;

                                                    [SealedEnv]
                                                    public sealed partial class AppConfig
                                                    {
                                                        public partial int Port { get; } // partial but unmarked — SVE0007
                                                    }
                                                    """;

    private const string MalformedDefault = """
                                             using SealedEnv;

                                             namespace TestCode;

                                             [SealedEnv]
                                             public sealed partial class AppConfig
                                             {
                                                 [EnvKey(Default = "eighty")] public partial int Port { get; }
                                             }
                                             """;

    private const string PropertyOnOtherPartialPart = """
                                                       using SealedEnv;

                                                       namespace TestCode;

                                                       [SealedEnv(Prefix = "MYAPP_")]
                                                       public sealed partial record AppConfig;

                                                       public sealed partial record AppConfig
                                                       {
                                                           [EnvKey] public partial int Port { get; }
                                                       }
                                                       """;

    [Fact]
    public void A_partial_property_carrier_generates_fields_accessor_and_constructor()
    {
        var (diagnostics, rawGenerated) = GeneratorRunner.Run(HappyPathClass);

        // AppendLine emits Environment.NewLine — normalize so the assertions read the
        // same on Windows and Unix.
        var generated = rawGenerated?.Replace("\r\n", "\n");

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("private readonly int __Port;", generated);
        Assert.Contains("private readonly string __DbConnection;", generated);
        Assert.Contains("private readonly string? __Tag;", generated);
        Assert.Contains("public partial int Port\n    {\n        get => __Port;", generated);
        Assert.Contains("public partial string? Tag\n    {\n        get => __Tag;", generated);
        Assert.Contains("public AppConfig(int __p0, string __p1, string? __p2)", generated);

        // Load/TryLoad construct through the generated constructor, exactly as the
        // parameter mode constructs through the primary one. Only reference-typed
        // parse results carry the ! (DbConnection is a non-nullable string local).
        Assert.Contains("return new AppConfig(__Port, __DbConnection, __Tag!);", generated);
    }

    [Fact]
    public void A_pure_property_carrier_does_not_require_a_primary_constructor()
    {
        // The SVE0010 regression guard: parameter mode blocks carriers without a
        // primary constructor; property mode supplies the constructor itself.
        var (diagnostics, generated) = GeneratorRunner.Run(HappyPathClass);

        Assert.DoesNotContain(diagnostics, static d => d.Id == "SVE0010");
        Assert.NotNull(generated);
        Assert.Contains("public static AppConfig Load(", generated);
    }

    [Fact]
    public void Records_and_nullable_annotated_types_round_trip_the_declaration()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(RecordCarrier);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("private readonly global::System.Uri? __CallbackUrl;", generated);
        Assert.Contains("public partial global::System.Uri? CallbackUrl", generated);
        Assert.Contains("public AppConfig(int __p0, global::System.Uri? __p1)", generated);
    }

    [Fact]
    public void Keyword_property_names_are_emitted_escaped()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(KeywordNameCarrier);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("public partial string @class", generated);
        Assert.Contains("get => __class;", generated);
        Assert.Contains("__class = __p0;", generated);
    }

    [Fact]
    public void Case_variant_property_names_get_positionally_mangled_constructor_parameters()
    {
        // "Port" and "port" are distinct members but would collide after camelCasing
        // as constructor parameters — hence the position-mangled __p0/__p1 scheme.
        // Their keys must be explicit: the derivation would flag the case-variant
        // duplicate (SVE0003), which is a separate scenario.
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv]
                              public sealed partial record AppConfig
                              {
                                  [EnvKey("PORT_A")] public partial int Port { get; }
                                  [EnvKey("PORT_B")] public partial int port { get; }
                              }
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("public AppConfig(int __p0, int __p1)", generated);
        Assert.Contains("__Port = __p0;", generated);
        Assert.Contains("__port = __p1;", generated);
    }

    [Fact]
    public void The_property_may_live_on_another_partial_part_than_the_attribute()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(PropertyOnOtherPartialPart);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("\"MYAPP_PORT\"", generated);
        Assert.Contains("public partial int Port", generated);
    }

    [Fact]
    public void Mixing_parameter_and_property_bindings_is_SVE0011()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(MixedStyles);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(generated); // blocking
    }

    [Fact]
    public void An_unmarked_constructor_parameter_next_to_property_bindings_is_SVE0013()
    {
        // The record's primary constructor exists (user-declared), so the generated
        // constructor would collide with it — and a carrier carrying it silently
        // leaves its fields default. Any explicit constructor is rejected.
        var (diagnostics, generated) = GeneratorRunner.Run(PropertyPlusUnmarkedCtorParam);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0013", diagnostic.Id);
        Assert.Null(generated);
    }

    [Fact]
    public void A_hand_written_constructor_is_SVE0013()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(UserConstructor);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0013", diagnostic.Id);
        Assert.Contains("constructor", diagnostic.GetMessage());
        Assert.Null(generated);
    }

    [Theory]
    [InlineData("NonPartialProperty")]
    [InlineData("SetAccessor")]
    [InlineData("InitAccessor")]
    [InlineData("StaticProperty")]
    [InlineData("ImplementedProperty")]
    [InlineData("IndexerBinding")]
    public void Unsupported_property_shapes_are_SVE0012(string sourceName)
    {
        var source = sourceName switch
        {
            nameof(NonPartialProperty) => NonPartialProperty,
            nameof(SetAccessor) => SetAccessor,
            nameof(InitAccessor) => InitAccessor,
            nameof(StaticProperty) => StaticProperty,
            nameof(ImplementedProperty) => ImplementedProperty,
            nameof(IndexerBinding) => IndexerBinding,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceName)),
        };

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(generated);
    }

    [Fact]
    public void The_SVE0012_message_names_the_property_and_the_supported_shape()
    {
        var (diagnostics, _) = GeneratorRunner.Run(SetAccessor);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("'Port'", diagnostic.GetMessage());
        Assert.Contains("get-only", diagnostic.GetMessage());
        Assert.Contains("public partial T Port { get; }", diagnostic.GetMessage());
    }

    [Fact]
    public void An_indexer_is_rejected_by_name_this()
    {
        var (diagnostics, _) = GeneratorRunner.Run(IndexerBinding);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("'this[]'", diagnostic.GetMessage());
        Assert.Contains("an indexer cannot be an environment binding", diagnostic.GetMessage());
    }

    [Fact]
    public void A_user_member_named_like_a_backing_field_is_SVE0008()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(BackingFieldCollision);

        Assert.Contains(diagnostics, static d => d.Id == "SVE0008");
        var diagnostic = diagnostics.Single(static d => d.Id == "SVE0008");
        Assert.Contains("'__Port'", diagnostic.GetMessage());
        Assert.Null(generated);
    }

    [Fact]
    public void Duplicate_keys_across_properties_are_SVE0003()
    {
        var (diagnostics, _) = GeneratorRunner.Run(DuplicatePropertyKeys);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0003", diagnostic.Id);
        Assert.Contains("'Port' and 'ListenPort'", diagnostic.GetMessage());
        Assert.Contains("'MYAPP_PORT'", diagnostic.GetMessage());
    }

    [Fact]
    public void Property_bindings_share_the_parameter_validation_pipeline()
    {
        // SVE0002: no parser route.
        var (unsupportedDiagnostics, _) = GeneratorRunner.Run(UnsupportedPropertyType);
        var unsupported = Assert.Single(unsupportedDiagnostics);
        Assert.Equal("SVE0002", unsupported.Id);
        Assert.Contains("List<string>", unsupported.GetMessage());

        // SVE0007: unmarked member.
        var (missingDiagnostics, _) = GeneratorRunner.Run(MissingEnvKeyOnProperty);
        var missing = Assert.Single(missingDiagnostics);
        Assert.Equal("SVE0007", missing.Id);

        // SVE0004: default does not parse as the property type.
        var (defaultDiagnostics, _) = GeneratorRunner.Run(MalformedDefault);
        var @default = Assert.Single(defaultDiagnostics);
        Assert.Equal("SVE0004", @default.Id);
        Assert.Contains("\"eighty\"", @default.GetMessage());
    }

    [Fact]
    public void Property_carriers_generate_the_documentation_member_when_opted_in()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "DOCS_", GenerateDocumentation = true)]
                              public sealed partial record AppConfig
                              {
                                  [EnvKey("PORT", Default = "8080", Description = "TCP port")] public partial int Port { get; }
                              }
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("public const string ConfigurationDocumentation", generated);
        Assert.Contains("| `DOCS_PORT` | `int` | No | `8080` | TCP port |", generated);
    }

    [Fact]
    public void Partial_property_carriers_compile_under_csharp13_exactly()
    {
        // The feature needs C# 13 consumers — the happy path must not depend on
        // anything newer.
        var (diagnostics, generated) = GeneratorRunner.Run(HappyPathClass, LanguageVersion.CSharp13);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
    }
}

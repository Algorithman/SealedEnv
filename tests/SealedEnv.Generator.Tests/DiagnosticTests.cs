// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Generator.Tests
// Filename: DiagnosticTests.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     Duplicate-key detection and unsupported-shape handling (the Phase 2 slice of the
///     SVE diagnostics; the full matrix lands in Phase 3).
/// </summary>
public class DiagnosticTests
{
    private const string DuplicateExplicitKeys = """
                                                 using SealedEnv;

                                                 namespace TestCode;

                                                 [SealedEnv(Prefix = "MYAPP_")]
                                                 public sealed partial record AppConfig(
                                                     [EnvKey("PORT")] int Port,
                                                     [EnvKey("PORT", Default = "80")] int ListenPort);
                                                 """;

    private const string DuplicateViaDerivation = """
                                                  using SealedEnv;

                                                  namespace TestCode;

                                                  [SealedEnv(Prefix = "MYAPP_")]
                                                  public sealed partial record AppConfig(
                                                      [EnvKey("FOOBAR")] int A,
                                                      [EnvKey] int FooBar); // derives to FOOBAR
                                                  """;

    private const string UnsupportedType = """
                                           using System.Collections.Generic;
                                           using SealedEnv;

                                           namespace TestCode;

                                           [SealedEnv]
                                           public sealed partial record AppConfig([EnvKey] List<string> Tags);
                                           """;

    private const string MissingEnvKey = """
                                         using SealedEnv;

                                         namespace TestCode;

                                         [SealedEnv]
                                         public sealed partial record AppConfig(int Port);
                                         """;

    private const string GenericCarrier = """
                                          using SealedEnv;

                                          namespace TestCode;

                                          [SealedEnv]
                                          public sealed partial record AppConfig<T>([EnvKey] int Port);
                                          """;

    // ---- Phase 4 hardening: remaining shapes ----

    private const string OptionalParamWithoutEnvKey = """
                                                      using SealedEnv;

                                                      namespace TestCode;

                                                      [SealedEnv]
                                                      public sealed partial record AppConfig(int Port = 8080); // C#-optional but unmarked
                                                      """;

    private const string OptionalParamWithEnvKey = """
                                                   using SealedEnv;

                                                   namespace TestCode;

                                                   [SealedEnv]
                                                   public sealed partial record AppConfig([EnvKey(Default = "8080")] int Port = 1); // C# default is inert; generated code always passes a value
                                                   """;

    private const string MemberNameCollision = """
                                               using SealedEnv;

                                               namespace TestCode;

                                               [SealedEnv]
                                               public sealed partial record AppConfig([EnvKey] int Port)
                                               {
                                                   public static AppConfig Load() => new(1); // collides with the generated member
                                               }
                                               """;

    private const string RefStructCarrier = """
                                            using SealedEnv;

                                            namespace TestCode;

                                            [SealedEnv]
                                            public ref struct AppConfig([EnvKey] int Port);
                                            """;

    private const string GlobalNamespaceCarrier = """
                                                  using System;
                                                  using SealedEnv;

                                                  [SealedEnv(Prefix = "G_")]
                                                  public sealed partial record GlobalCarrier([EnvKey] int Port);
                                                  """;

    // ---- SVE0001: [EnvKey] outside primary constructors ----

    private const string EnvKeyOnMethodParameter = """
                                                   using SealedEnv;

                                                   namespace TestCode;

                                                   public class Service
                                                   {
                                                       public void Run([EnvKey] string key) { }
                                                   }
                                                   """;

    private const string EnvKeyOnRegularConstructorParameter = """
                                                               using SealedEnv;

                                                               namespace TestCode;

                                                               public class Service
                                                               {
                                                                   public Service([EnvKey] int retries) { }
                                                               }
                                                               """;

    private const string EnvKeyOnLambdaParameter = """
                                                   using SealedEnv;

                                                   namespace TestCode;

                                                   public class Service
                                                   {
                                                       public void Run()
                                                       {
                                                           System.Func<int, int> f = ([EnvKey] int x) => x;
                                                       }
                                                   }
                                                   """;

    private const string EnvKeyQualifiedOnMethodParameter = """
                                                            using SealedEnv;

                                                            namespace TestCode;

                                                            public class Service
                                                            {
                                                                public void Run([SealedEnv.EnvKey] string key) { }
                                                            }
                                                            """;

    private const string EnvKeyOnPrimaryConstructorOfPlainRecord = """
                                                                   using SealedEnv;

                                                                   namespace TestCode;

                                                                   // No [SealedEnv] on the record — [EnvKey] on a primary constructor parameter
                                                                   // is a legitimate location, so no SVE0001 (the user may add [SealedEnv] later).
                                                                   public record Point([EnvKey] int X);
                                                                   """;

    private const string EnvKeyOnProperty = """
                                            using SealedEnv;

                                            namespace TestCode;

                                            public class Service
                                            {
                                                [EnvKey]
                                                public string Key { get; set; } = "";
                                            }
                                            """;

    private const string ParsableCarrierWithBadDefault = """
                                                         using System;
                                                         using SealedEnv;

                                                         namespace TestCode;

                                                         public readonly record struct Port(int Value) : IParsable<Port>
                                                         {
                                                             public static Port Parse(string s) => new(int.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
                                                             static Port IParsable<Port>.Parse(string s, IFormatProvider? provider) => Parse(s);
                                                             static bool IParsable<Port>.TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out Port result)
                                                             {
                                                                 if (int.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0) { result = new Port(v); return true; }
                                                                 result = default; return false;
                                                             }
                                                         }

                                                         [SealedEnv]
                                                         public sealed partial record AppConfig([EnvKey(Default = "junk")] Port Gateway);
                                                         """;

    // ---- SVE0005: carrier not sealed ----

    private const string UnsealedRecordCarrier = """
                                                 using SealedEnv;

                                                 namespace TestCode;

                                                 [SealedEnv]
                                                 public partial record AppConfig([EnvKey] int Port);
                                                 """;

    private const string SealedRecordCarrier = """
                                               using SealedEnv;

                                               namespace TestCode;

                                               [SealedEnv]
                                               public sealed partial record AppConfig([EnvKey] int Port);
                                               """;

    private const string RecordStructCarrier = """
                                               using SealedEnv;

                                               namespace TestCode;

                                               [SealedEnv]
                                               public partial record struct AppConfig([EnvKey] int Port); // structs are implicitly sealed
                                               """;

    private const string UnsealedClassCarrier = """
                                                using SealedEnv;

                                                namespace TestCode;

                                                [SealedEnv]
                                                public partial class AppConfig([EnvKey] int Port);
                                                """;

    [Fact]
    public void Two_explicit_keys_colliding_is_reported_and_generation_continues()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(DuplicateExplicitKeys);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0003", diagnostic.Id);
        Assert.Contains("'Port' and 'ListenPort'", diagnostic.GetMessage());
        Assert.Contains("'MYAPP_PORT'", diagnostic.GetMessage());

        // Duplicates bind both members to the same variable deterministically;
        // generation is not blocked (the diagnostic is what tells the user).
        Assert.NotNull(generated);
        Assert.Contains("MYAPP_PORT", generated);
    }

    [Fact]
    public void An_explicit_key_colliding_with_a_derived_key_is_reported()
    {
        var (diagnostics, _) = GeneratorRunner.Run(DuplicateViaDerivation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0003", diagnostic.Id);
        Assert.Contains("'A' and 'FooBar'", diagnostic.GetMessage());
    }

    [Fact]
    public void Unsupported_parameter_type_is_an_error_and_blocks_generation()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(UnsupportedType);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("List<string>", diagnostic.GetMessage());

        Assert.Null(generated); // no half-working Load() for an invalid carrier
    }

    [Fact]
    public void Missing_env_key_attribute_is_an_error_and_blocks_generation()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(MissingEnvKey);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'Port'", diagnostic.GetMessage());

        Assert.Null(generated);
    }

    [Fact]
    public void Generic_carriers_report_the_temporary_diagnostic()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(GenericCarrier);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0100", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("generic", diagnostic.GetMessage());

        Assert.Null(generated);
    }

    [Fact]
    public void An_optional_parameter_without_env_key_is_reported()
    {
        // Pinned decision (plan §4): every primary-ctor parameter must be marked;
        // a C#-default does not make it env-managed.
        var (diagnostics, generated) = GeneratorRunner.Run(OptionalParamWithoutEnvKey);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0007", diagnostic.Id);
        Assert.Null(generated);
    }

    [Fact]
    public void An_optional_parameter_with_env_key_generates_and_ignores_the_csharp_default()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(OptionalParamWithEnvKey);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
    }

    [Fact]
    public void A_user_member_named_like_a_generated_member_is_reported_and_blocked()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(MemberNameCollision);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'Load'", diagnostic.GetMessage());
        Assert.Null(generated);
    }

    [Fact]
    public void A_ref_struct_carrier_is_rejected_gracefully()
    {
        // Nullable<T> does not exist for ref structs, so SealedEnvBind's return
        // type could not compile — the shape is rejected instead of mis-generating.
        var (diagnostics, generated) = GeneratorRunner.Run(RefStructCarrier);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0100", diagnostic.Id);
        Assert.Contains("ref struct", diagnostic.GetMessage());
        Assert.Null(generated);
    }

    [Fact]
    public void A_carrier_without_a_namespace_emits_a_namespace_less_file()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(GlobalNamespaceCarrier);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        // The only "namespace " occurrence in generated output is the declaration.
        Assert.DoesNotContain("namespace ", generated);
        Assert.Contains("public sealed partial record GlobalCarrier", generated);
    }

    [Fact]
    public void Env_key_on_a_method_parameter_is_reported()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(EnvKeyOnMethodParameter);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'key'", diagnostic.GetMessage());
        Assert.Null(generated); // no carrier → nothing to generate anyway
    }

    [Fact]
    public void Env_key_on_a_regular_constructor_parameter_is_reported()
    {
        var (diagnostics, _) = GeneratorRunner.Run(EnvKeyOnRegularConstructorParameter);

        Assert.Equal("SVE0001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void Env_key_on_a_lambda_parameter_is_reported()
    {
        var (diagnostics, _) = GeneratorRunner.Run(EnvKeyOnLambdaParameter);

        Assert.Equal("SVE0001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_qualified_env_key_name_is_still_detected()
    {
        var (diagnostics, _) = GeneratorRunner.Run(EnvKeyQualifiedOnMethodParameter);

        Assert.Equal("SVE0001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void Env_key_on_a_primary_constructor_of_a_plain_record_is_not_stray()
    {
        var (diagnostics, _) = GeneratorRunner.Run(EnvKeyOnPrimaryConstructorOfPlainRecord);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Env_key_on_a_property_is_left_to_the_compiler()
    {
        // AttributeTargets.Parameter makes this CS0592 — the compiler's job, the
        // generator stays out of the way.
        var (diagnostics, _) = GeneratorRunner.Run(EnvKeyOnProperty);

        Assert.Empty(diagnostics);
    }

    // ---- SVE0004: malformed Default values ----

    private static string Carrier(string attribute)
    {
        return $$"""
                 using System;
                 using SealedEnv;

                 namespace TestCode;

                 public enum LogLevel { Debug, Info, Warning, Error }

                 [SealedEnv]
                 public sealed partial record AppConfig([EnvKey({{attribute}})] int Port);
                 """;
    }

    private static string TypedCarrier(string type, string attribute)
    {
        return $$"""
                 using System;
                 using SealedEnv;

                 namespace TestCode;

                 public enum LogLevel { Debug, Info, Warning, Error }

                 [SealedEnv]
                 public sealed partial record AppConfig([EnvKey({{attribute}})] {{type}} Value);
                 """;
    }

    [Theory]
    [InlineData("Default = \"abc\"")] // int
    [InlineData("Default = \"8080.5\"")]
    [InlineData("Default = \"\"")] // empty default is still not an int
    public void Malformed_int_defaults_are_reported(string attribute)
    {
        var (diagnostics, generated) = GeneratorRunner.Run(Carrier(attribute));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(generated);
    }

    [Theory]
    [InlineData("int", "Default = \"8080\"")]
    [InlineData("int", "Default = \"-1\"")]
    [InlineData("bool", "Default = \"on\"")]
    [InlineData("bool", "Default = \"OFF\"")]
    [InlineData("double", "Default = \"1.5\"")]
    [InlineData("double", "Default = \"1e3\"")]
    [InlineData("System.TimeSpan", "Default = \"00:00:30\"")]
    [InlineData("System.Guid", "Default = \"6f9619ff-8b86-d011-b42d-00c04fc964ff\"")]
    [InlineData("string", "Default = \"anything goes\"")] // strings are never malformed
    [InlineData("string[]", "Default = \"a,b\"")] // splitting always succeeds
    [InlineData("LogLevel", "Default = \"Info\"")]
    [InlineData("LogLevel", "Default = \"info\"")] // case-insensitive names
    [InlineData("LogLevel", "Default = \"9\"")] // undefined numerics are legal Enum.Parse values
    [InlineData("LogLevel", "Default = \"Debug, Error\"")] // flag-combo style lists
    public void Well_formed_defaults_pass_validation(string type, string attribute)
    {
        var (diagnostics, generated) = GeneratorRunner.Run(TypedCarrier(type, attribute));

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
    }

    [Theory]
    [InlineData("bool", "Default = \"yes\"")] // not a documented bool token
    [InlineData("System.TimeSpan", "Default = \"30s\"")]
    [InlineData("System.Guid", "Default = \"not-a-guid\"")]
    [InlineData("double", "Default = \"1,234.5\"")] // thousands separators: rejected at runtime too
    [InlineData("Uri", "Default = \"/relative\"")] // endpoints must be absolute, like the runtime rule
    [InlineData("LogLevel", "Default = \"Fatal\"")] // not a member
    [InlineData("LogLevel", "Default = \"3.5\"")] // not a valid underlying value
    [InlineData("LogLevel", "Default = \"\"")] // runtime TryParse would fail too
    public void Malformed_typed_defaults_are_reported(string type, string attribute)
    {
        var (diagnostics, generated) = GeneratorRunner.Run(TypedCarrier(type, attribute));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0004", diagnostic.Id);
        Assert.Null(generated);
    }

    [Fact]
    public void Parsable_route_defaults_are_validated_at_runtime_not_compile_time()
    {
        // The generator cannot execute the custom parser; the runtime loader owns
        // that check (SVE0004 never fires for IParsable<T> / Parser= routes).
        var (diagnostics, generated) = GeneratorRunner.Run(ParsableCarrierWithBadDefault);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
    }

    [Fact]
    public void An_unsealed_record_carrier_is_reported_and_blocked()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(UnsealedRecordCarrier);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(generated);
    }

    [Fact]
    public void Sealed_record_and_record_struct_carriers_are_accepted()
    {
        Assert.Empty(GeneratorRunner.Run(SealedRecordCarrier).Diagnostics);
        Assert.Empty(GeneratorRunner.Run(RecordStructCarrier).Diagnostics);
    }

    [Fact]
    public void An_unsealed_class_carrier_is_reported()
    {
        var (diagnostics, _) = GeneratorRunner.Run(UnsealedClassCarrier);

        Assert.Equal("SVE0005", Assert.Single(diagnostics).Id);
    }

    // ---- SVE0006: parser without a usable Parse method ----

    private static string CustomParserCarrier(string parserBody)
    {
        return $$"""
                 using System;
                 using SealedEnv;

                 namespace TestCode;

                 public sealed class VatRate
                 {
                     {{parserBody}}
                 }

                 [SealedEnv]
                 public sealed partial record AppConfig([EnvKey(Parser = typeof(VatRate))] VatRate Vat);
                 """;
    }

    [Fact]
    public void A_parser_without_a_parse_method_is_reported_and_blocked()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(CustomParserCarrier("public decimal Value { get; }"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(generated);
    }

    [Theory]
    [InlineData("private static VatRate Parse(string raw) => new();")] // not public
    [InlineData("public VatRate Parse(string raw) => new();")] // not static
    [InlineData("public static VatRate Parse(string raw, IFormatProvider? provider) => new();")] // wrong arity
    [InlineData("public static VatRate Parse(int raw) => new();")] // wrong parameter type
    [InlineData("public static decimal Parse(string raw) => 0m;")] // wrong return type
    [InlineData("public static VatRate Parse<T>(string raw) => new();")] // generic method group cannot be used
    public void Unusable_parse_methods_are_reported(string parserBody)
    {
        var (diagnostics, _) = GeneratorRunner.Run(CustomParserCarrier(parserBody));

        Assert.Equal("SVE0006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_valid_parser_generates_the_custom_call()
    {
        const string parser = """
                              public decimal Value { get; }
                              public static VatRate Parse(string raw) => new();
                              """;
        var (diagnostics, generated) = GeneratorRunner.Run(CustomParserCarrier(parser));

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        Assert.Contains("global::TestCode.VatRate.Parse", generated);
    }

    [Fact]
    public void Extra_overloads_next_to_a_valid_parse_do_not_block()
    {
        const string parser = """
                              public decimal Value { get; }
                              public static VatRate Parse(string raw) => new();
                              public static VatRate Parse(string raw, IFormatProvider? provider) => new();
                              """;
        var (diagnostics, generated) = GeneratorRunner.Run(CustomParserCarrier(parser));

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
    }
}

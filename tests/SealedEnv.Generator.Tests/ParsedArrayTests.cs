// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Generator.Tests
// Filename: ParsedArrayTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     Parsed-element list routing: T[] and IReadOnlyList&lt;T&gt; route by ELEMENT
///     through the scalar table, so the emitted call is the kind's Array helper with
///     the separator — and the compile-time default check validates the default
///     entry-wise with the same escape-aware splitter the runtime uses.
/// </summary>
public class ParsedArrayTests
{
    private const string ArraySurface = """
                                         using System;
                                         using System.Collections.Generic;
                                         using SealedEnv;

                                         namespace TestCode;

                                         public enum LogLevel { Debug, Info, Warning, Error }

                                         public sealed class StrictParser
                                         {
                                             public static int Parse(string raw) => int.Parse(raw);
                                         }

                                         [SealedEnv(Prefix = "APP_")]
                                         public sealed partial record Lists(
                                             [EnvKey] int[] Ports,
                                             [EnvKey] long[] Bigs,
                                             [EnvKey] double[] Rates,
                                             [EnvKey] bool[] Flags,
                                             [EnvKey] TimeSpan[] Timeouts,
                                             [EnvKey] Uri[] Callbacks,
                                             [EnvKey] Guid[] TenantIds,
                                             [EnvKey] LogLevel[] Levels,
                                             [EnvKey(Separator = ';')] string[] Hosts,
                                             [EnvKey] IReadOnlyList<string> Aliases,
                                             [EnvKey] IReadOnlyList<LogLevel> LevelViews,
                                             [EnvKey(Parser = typeof(StrictParser))] int[] Parsed);
                                         """;

    [Fact]
    public void Every_element_kind_emits_its_Array_helper_with_the_separator()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(ArraySurface);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("SealedEnvLoader.IntArray(provider, \"APP_PORTS\", \"Lists.Ports\", null, false, false, true, ',', ref __errors)", generated);
        Assert.Contains("SealedEnvLoader.LongArray(", generated);
        Assert.Contains("SealedEnvLoader.DoubleArray(", generated);
        Assert.Contains("SealedEnvLoader.BoolArray(", generated);
        Assert.Contains("SealedEnvLoader.TimeSpanArray(", generated);
        Assert.Contains("SealedEnvLoader.UriArray(", generated);
        Assert.Contains("SealedEnvLoader.GuidArray(", generated);
        Assert.Contains("SealedEnvLoader.EnumArray<global::TestCode.LogLevel>(", generated);
        Assert.Contains("SealedEnvLoader.StringArray(provider, \"APP_HOSTS\", \"Lists.Hosts\", null, false, false, true, ';', ref __errors)", generated);
        Assert.Contains("SealedEnvLoader.StringArray(provider, \"APP_ALIASES\", \"Lists.Aliases\", null, false, false, true, ',', ref __errors)", generated);
        Assert.Contains("SealedEnvLoader.EnumArray<global::TestCode.LogLevel>(provider, \"APP_LEVELVIEWS\", \"Lists.LevelViews\", null, false, false, true, ',', ref __errors)", generated);

        // Per-entry custom parser: the element-typed generic and the trailing method group.
        Assert.Contains("SealedEnvLoader.CustomArray<int>(provider, \"APP_PARSED\", \"Lists.Parsed\", null, false, false, true, ',', global::TestCode.StrictParser.Parse, ref __errors)", generated);

        // List locals need no null-forgiving and no nullable suffix: helpers return T[].
        Assert.Contains("int[] __Ports = ", generated);
        Assert.DoesNotContain("int[]? __Ports", generated);
        Assert.Contains("global::System.Collections.Generic.IReadOnlyList<string> __Aliases = ", generated);
    }

    [Fact]
    public void Whole_value_custom_parsers_keep_the_unsplit_Custom_route()
    {
        // Parse returning the MEMBER type (the array itself) is the pre-arrays shape:
        // one call, no separator, no element splitting.
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              public sealed class CsvPorts
                              {
                                  public static int[] Parse(string raw) => [1, 2];
                              }

                              [SealedEnv]
                              public sealed partial record Config([EnvKey(Parser = typeof(CsvPorts))] int[] Ports);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("SealedEnvLoader.Custom<int[]>(provider, \"PORTS\", \"Config.Ports\", null, false, false, true, global::TestCode.CsvPorts.Parse, ref __errors)", generated);
        Assert.DoesNotContain("CustomArray", generated);
    }

    [Theory]
    [InlineData("List<int> Tags")] // lists other than the two supported shapes
    [InlineData("IEnumerable<int> Seq")]
    [InlineData("IReadOnlyCollection<int> Ro")]
    [InlineData("int?[] MaybeInts")] // Nullable<T> elements
    [InlineData("string[][] Grid")] // nested lists
    [InlineData("IReadOnlyList<IReadOnlyList<int>> Tree")]
    [InlineData("int[,] Matrix")] // multidimensional
    public void Unsupported_list_shapes_are_SVE0002(string member)
    {
        var (diagnostics, _) = GeneratorRunner.Run($$"""
                                                     using System;
                                                     using System.Collections.Generic;
                                                     using SealedEnv;

                                                     namespace TestCode;

                                                     [SealedEnv]
                                                     public sealed partial record Config([EnvKey] {{member}});
                                                     """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0002", diagnostic.Id);
        Assert.Contains("no parser route", diagnostic.GetMessage());
    }

    [Fact]
    public void A_user_type_named_IReadOnlyList_is_not_the_BCL_route()
    {
        // Matching is namespace + arity + name: a user-defined non-generic
        // IReadOnlyList must not hijack System.Collections.Generic.IReadOnlyList`1.
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              public interface IReadOnlyList
                              {
                                  int Count { get; }
                              }

                              [SealedEnv]
                              public sealed partial record Config([EnvKey] IReadOnlyList Items);
                              """;

        var (diagnostics, _) = GeneratorRunner.Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0002", diagnostic.Id);
    }

    [Fact]
    public void Enum_arrays_are_a_supported_shape()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              public enum DateTimeKind2 { Local, Utc }

                              [SealedEnv]
                              public sealed partial record Config([EnvKey] DateTimeKind2[] Kinds);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("SealedEnvLoader.EnumArray<global::TestCode.DateTimeKind2>(", generated);
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData(@"1\,2,3")] // escaped comma inside an entry: "1,2" parses (thousands grouping)
    [InlineData(" 1 , 2 ")] // trims before validating
    public void Int_list_defaults_are_validated_entry_wise(string @default)
    {
        const string SourceTemplate = """
                                      using SealedEnv;

                                      namespace TestCode;

                                      [SealedEnv]
                                      public sealed partial record Config([EnvKey(Default = "{DEFAULT}")] int[] Ports);
                                      """;

        var (diagnostics, _) = GeneratorRunner.Run(SourceTemplate.Replace("{DEFAULT}", @default));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void A_bad_default_entry_is_SVE0004_with_the_type_text()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv]
                              public sealed partial record Config([EnvKey(Default = "1,two,3")] int[] Ports);
                              """;

        var (diagnostics, _) = GeneratorRunner.Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0004", diagnostic.Id);
        Assert.Contains("\"1,two,3\"", diagnostic.GetMessage());
        Assert.Contains("int[]", diagnostic.GetMessage());
    }

    [Fact]
    public void The_default_split_mirrors_the_separator()
    {
        // The attribute's Separator governs the compile-time check too. "on,off" is
        // ONE entry under ';' — an invalid bool token — but TWO valid tokens under a
        // naive ',' split, so this SVE0004 only fires when the separator is mirrored.
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv]
                              public sealed partial record Config([EnvKey(Default = "on,off", Separator = ';')] bool[] Flags);
                              """;

        var (diagnostics, _) = GeneratorRunner.Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0004", diagnostic.Id);
        Assert.Contains("bool[]", diagnostic.GetMessage());
    }

    [Fact]
    public void Parsable_and_string_list_defaults_are_runtime_only()
    {
        // The generator cannot execute IParsable<T> parsers — the default text is
        // accepted here and validated when the helper runs.
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using SealedEnv;

                              namespace TestCode;

                              public sealed class DateBox : IParsable<DateBox>
                              {
                                  public static DateBox Parse(string raw) => new();
                              }

                              [SealedEnv]
                              public sealed partial record Config(
                                  [EnvKey(Default = "not-a-date")] DateBox[] Boxes,
                                  [EnvKey(Default = "anything goes")] string[] Labels);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("ParsableArray<global::TestCode.DateBox>(", generated);
    }

    [Fact]
    public void Property_mode_binds_list_members_too()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "PP_")]
                              public sealed partial class Config
                              {
                                  [EnvKey(Default = "80,443")] public partial int[] Ports { get; }
                                  [EnvKey(Separator = ';')] public partial string[] Hosts { get; }
                              }
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(generated);
        Assert.Contains("private readonly int[] __Ports;", generated);
        Assert.Contains("public partial int[] Ports", generated);
        Assert.Contains("__Ports = __p0;", generated);
        Assert.Contains("SealedEnvLoader.IntArray(", generated);
        Assert.Contains("SealedEnvLoader.StringArray(", generated);
    }
}

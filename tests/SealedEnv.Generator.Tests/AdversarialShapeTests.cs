using System.Text;
using Microsoft.CodeAnalysis;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     Adversarial round: carrier shapes actively trying to break the generator —
///     colliding file names, hostile attribute arguments, extreme scale. Each test
///     pins a behavior that once looked safe and was not.
/// </summary>
public class AdversarialShapeTests
{
    [Fact]
    public void Same_type_name_in_two_namespaces_generates_two_distinct_files()
    {
        // Hint names embed the namespace, so "AppConfig" twice must not collapse
        // into one generated file (a duplicate hint name would abort the whole
        // generator run with CS8785).
        const string source = """
                              using SealedEnv;

                              namespace Billing;

                              [SealedEnv(Prefix = "BILL_")]
                              public sealed partial record AppConfig([EnvKey] int Port);

                              namespace Shipping;

                              [SealedEnv(Prefix = "SHIP_")]
                              public sealed partial record AppConfig([EnvKey] int Port);
                              """;

        var (diagnostics, generated) = GeneratorRunner.RunAll(source);

        Assert.Empty(diagnostics);
        Assert.Equal(2, generated.Length);
        // Tree order is not guaranteed — assert both files exist and are distinct.
        Assert.Contains(generated, static text => text.Contains("BILL_PORT"));
        Assert.Contains(generated, static text => text.Contains("SHIP_PORT"));
        Assert.NotEqual(generated[0], generated[1]);
    }

    [Fact]
    public void A_100_parameter_carrier_generates_completely()
    {
        var source = new StringBuilder("""
                                       using SealedEnv;

                                       namespace TestCode;

                                       [SealedEnv(Prefix = "SCALE_")]
                                       public sealed partial record BigConfig(
                                       """);
        for (var i = 0; i < 100; i++)
        {
            source.Append(i > 0 ? ",\n" : "").Append($"    [EnvKey(Default = \"{i}\")] int Setting{i}");
        }

        source.Append(");");

        var (diagnostics, generated) = GeneratorRunner.RunAll(source.ToString());

        Assert.Empty(diagnostics);
        var text = Assert.Single(generated);
        Assert.Equal(100, text.AsSpan().Count("SealedEnvLoader.Int("));
        Assert.Equal(1, text.AsSpan().Count("new BigConfig(")); // one constructor call
    }

    // The InlineData is C# SOURCE text (escape sequences, not raw characters):
    // a raw newline inside the snippet's string literal would itself be CS1010,
    // while the compiler resolves these escapes to the same runtime values a
    // real project would produce.
    // InlineData holds C# SOURCE text with doubled backslashes: the test
    // receives e.g. the two characters \\n and interpolates them into the
    // snippet, whose compiler then resolves the escape to the whitespace char
    // a real project would produce. (A raw LF inline would be CS1010.)
    [Theory]
    [InlineData(" ")]
    [InlineData("\\t")]
    [InlineData("\\n")]
    [InlineData("\\r\\t ")]
    [InlineData(" myprefix")]      // invisible leading typo
    [InlineData("myprefix ")]      // invisible trailing typo
    public void A_prefix_with_edge_whitespace_is_SVE0009_and_blocks_generation(string prefixSourceText)
    {
        var source = $$"""
                       using SealedEnv;

                       namespace TestCode;

                       [SealedEnv(Prefix = "{{prefixSourceText}}")]
                       public sealed partial record AppConfig([EnvKey] int Port);
                       """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("whitespace", diagnostic.GetMessage());
        Assert.Null(generated); // blocking: silently broken lookups are worse than no generation
    }

    [Fact]
    public void An_empty_prefix_is_still_legal_global_key_syntax()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "")]
                              public sealed partial record AppConfig([EnvKey] int Port);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        Assert.Contains("\"PORT\"", generated); // key without any prefix
    }
    [Fact]
    public void Internal_whitespace_in_the_prefix_stays_legal()
    {
        // SVE0009 draws the line at INVISIBLE whitespace (leading/trailing). An
        // internal space is visible and plausibly deliberate for a legacy system:
        // the generator stays out of the way.
        const string source = """
                               using SealedEnv;

                               namespace TestCode;

                               [SealedEnv(Prefix = "MY APP_")]
                               public sealed partial record AppConfig([EnvKey] int Port);
                               """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        Assert.Contains("\"MY APP_PORT\"", generated);
    }

}

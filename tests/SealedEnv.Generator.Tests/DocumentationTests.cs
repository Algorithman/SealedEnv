using Microsoft.CodeAnalysis;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     The doc export ([SealedEnv(GenerateDocumentation = true)]): a markdown table
///     (Key / Type / Required / Default / Description) emitted as the carrier's
///     ConfigurationDocumentation member. Off by default — nothing
///     documentation-shaped may be emitted without opting in.
/// </summary>
public class DocumentationTests
{
    private const string DocumentedSource = """
                                            using SealedEnv;

                                            namespace TestCode;

                                            [SealedEnv(Prefix = "MYAPP_", GenerateDocumentation = true)]
                                            public sealed partial record AppConfig(
                                                [EnvKey("PORT", Default = "8080", Description = "TCP port")] int Port,
                                                [EnvKey(Required = true)] string DbConnection);
                                            """;

    [Fact]
    public void Opt_in_emits_the_documentation_member_with_the_full_table()
    {
        var (diagnostics, generated) = GeneratorRunner.Run(DocumentedSource);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        Assert.Contains("public const string ConfigurationDocumentation", generated);
        // The markdown rows live inside the escaped C# string; line-level assertions
        // still match because rows contain no newlines.
        Assert.Contains("| Key | Type | Required | Default | Description |", generated);
        Assert.Contains("| `MYAPP_PORT` | `int` | No | `8080` | TCP port |", generated);
        Assert.Contains("| `MYAPP_DBCONNECTION` | `string` | Yes | — |", generated);
    }

    [Fact]
    public void Without_the_flag_nothing_documentation_shaped_is_emitted()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "MYAPP_")]
                              public sealed partial record AppConfig([EnvKey] int Port);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        Assert.DoesNotContain("ConfigurationDocumentation", generated);
        Assert.DoesNotContain("| Key |", generated);
    }

    [Fact]
    public void A_user_declared_ConfigurationDocumentation_conflicts_when_opted_in()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "MYAPP_", GenerateDocumentation = true)]
                              public sealed partial record AppConfig([EnvKey] int Port)
                              {
                                  public const string ConfigurationDocumentation = "mine";
                              }
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SVE0008", diagnostic.Id);
        Assert.Contains("ConfigurationDocumentation", diagnostic.GetMessage());
        Assert.Null(generated);
    }

    [Fact]
    public void Pipes_in_keys_and_descriptions_are_escaped_in_the_markdown()
    {
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "MYAPP_", GenerateDocumentation = true)]
                              public sealed partial record AppConfig(
                                  [EnvKey("A|B", Description = "uses | pipes")] int Weird);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        // Search is against the generated SOURCE text: the markdown's \| is stored
        // C#-escaped as \\| there (at runtime the member value carries single \|).
        // The raw key also appears unescaped in the binding code, so a whole-file
        // DoesNotContain("A|B") would be wrong.
        Assert.Contains("| `MYAPP_A\\\\|B` | `int` | No | — | uses \\\\| pipes |", generated);
    }

    [Fact]
    public void The_documentation_member_carries_valid_csharp_for_hostile_content()
    {
        // Keys/descriptions with quotes and newlines must survive C# string escaping
        // — the const has to compile in a real project (covered end-to-end by the
        // SealedEnv.Tests carriers; here the escaping path is asserted directly).
        const string source = """
                              using SealedEnv;

                              namespace TestCode;

                              [SealedEnv(Prefix = "MYAPP_", GenerateDocumentation = true)]
                              public sealed partial record AppConfig(
                                  [EnvKey("QU\"OTE", Description = "says \"hi\"\nand more")] int Weird);
                              """;

        var (diagnostics, generated) = GeneratorRunner.Run(source);

        Assert.Empty(diagnostics);
        Assert.NotNull(generated);
        Assert.Contains("says \\\"hi\\\"", generated);
        Assert.DoesNotContain("says \"hi\"", generated);
    }
}

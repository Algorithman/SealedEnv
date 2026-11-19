// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Generator.Tests
// Filename: PipelineCachingTests.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace SealedEnv.Generator.Tests;

/// <summary>
///     Phase 4 hardening: the incremental pipeline must reuse unchanged inputs. The
///     carrier's syntax tree is shared between the two compilations and only an
///     unrelated file changes — with value-model equality, no step may re-execute
///     (every tracked output reports Cached/Unchanged, none Changed).
/// </summary>
public class PipelineCachingTests
{
    private const string Carrier = """
                                   using System;
                                   using SealedEnv;

                                   namespace TestCode;

                                   [SealedEnv(Prefix = "MYAPP_")]
                                   public sealed partial record AppConfig([EnvKey] int Port);
                                   """;

    private const string UnrelatedFirst = """
                                          namespace TestCode;

                                          // Something the config carrier has nothing to do with.
                                          public static class Unrelated
                                          {
                                              public static int Value => 1;
                                          }
                                          """;

    private const string UnrelatedSecond = """
                                           namespace TestCode;

                                           // Edited comment AND body — enough to change the tree completely.
                                           public static class Unrelated
                                           {
                                               public static int Value => 2;
                                               public static int Other => 3;
                                           }
                                           """;

    [Fact]
    public void An_edit_to_an_unrelated_file_does_not_regenerate_the_carrier()
    {
        var (first, second, secondRun) = GeneratorRunner.RunWithUnrelatedEdit(Carrier, UnrelatedFirst, UnrelatedSecond);

        Assert.NotNull(first);
        Assert.Equal(first, second); // byte-identical output, not just equivalent

        // IncrementalStepRunReason: Cached = reused from the previous run;
        // New = the step re-ran and produced different output. With the carrier's
        // tree untouched, nothing may re-run with new output.
        var outputReasons = secondRun.TrackedSteps.Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();

        Assert.Contains(IncrementalStepRunReason.Cached, outputReasons);
        Assert.DoesNotContain(IncrementalStepRunReason.New, outputReasons);
    }
}

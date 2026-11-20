// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.DotEnv.Tests
// Filename: DotEnvFileTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Text;
using SealedEnv.DotEnv;

namespace SealedEnv.DotEnv.Tests;

/// <summary>
///     The dotenv format contract: line-based, fail-fast on malformed lines (with
///     the 1-based line number), quoting rules per quote style, ordinal
///     case-sensitive keys, last-wins duplicates. Pinned here because every dotenv
///     implementation drifts differently — this one states its rules in the docs
///     and this file is the proof.
/// </summary>
public class DotEnvFileTests
{
    [Fact]
    public void Basic_entries_parse_with_trimmed_key_and_value()
    {
        var values = DotEnvFile.Parse("KEY=value\r\nOTHER = other value \n");

        Assert.Equal("value", values["KEY"]);
        Assert.Equal("other value", values["OTHER"]);
        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void Blank_lines_and_comments_are_skipped()
    {
        var values = DotEnvFile.Parse("""
                                      # full-line comment

                                        # indented comment
                                      KEY=value
                                      """);

        Assert.Equal(new Dictionary<string, string?> { ["KEY"] = "value", }, values);
    }

    [Fact]
    public void Export_prefix_is_stripped()
    {
        var values = DotEnvFile.Parse("export KEY=value\nexport   SPACED=1\n");

        Assert.Equal("value", values["KEY"]);
        Assert.Equal("1", values["SPACED"]);
    }

    [Fact]
    public void Unquoted_values_strip_an_inline_comment()
    {
        var values = DotEnvFile.Parse("KEY=value # the comment\nURL=https://example.com/#fragment\nPORT=8080");

        Assert.Equal("value", values["KEY"]);
        // A '#' without preceding whitespace stays literal — URLs keep their fragments.
        Assert.Equal("https://example.com/#fragment", values["URL"]);
        Assert.Equal("8080", values["PORT"]);
    }

    [Fact]
    public void Double_quoted_values_process_escapes_and_keep_hashes()
    {
        // \n here is literal FILE text (the escape the format defines); \r\n is the
        // real line terminator, exactly as a text editor would save it.
        var values = DotEnvFile.Parse("KEY=\"line1\\nline2 # not a comment\"\r\n");

        Assert.Equal("line1\nline2 # not a comment", values["KEY"]);
    }

    [Theory]
    [InlineData(@"\\", "\\")]
    [InlineData(@"a\""b", "a\"b")]
    [InlineData(@"a\tb", "a\tb")]
    public void Double_quoted_escapes_resolve(string raw, string expected)
    {
        var values = DotEnvFile.Parse($"KEY=\"{raw}\"\n");

        Assert.Equal(expected, values["KEY"]);
    }

    [Fact]
    public void Single_quoted_values_are_fully_literal()
    {
        var values = DotEnvFile.Parse(@"KEY='a\nb # c'");

        Assert.Equal(@"a\nb # c", values["KEY"]);
    }

    [Fact]
    public void Empty_value_is_an_explicit_empty_string()
    {
        var values = DotEnvFile.Parse("KEY=\n");

        Assert.Equal(string.Empty, values["KEY"]);
    }

    [Fact]
    public void Duplicate_keys_last_wins()
    {
        var values = DotEnvFile.Parse("KEY=first\nKEY=second\n");

        Assert.Equal("second", Assert.Single(values, static pair => pair.Key == "KEY").Value);
        Assert.Single(values);
    }

    [Fact]
    public void Keys_are_case_sensitive_ordinal()
    {
        var values = DotEnvFile.Parse("key=lower\nKEY=upper\n");

        Assert.Equal("lower", values["key"]);
        Assert.Equal("upper", values["KEY"]);
    }

    [Theory]
    [InlineData("NOEQUALS")] // no '='
    [InlineData("=value")] // empty key
    [InlineData("KEY")]
    [InlineData("KEY=\"unterminated")]
    [InlineData("KEY=\"escaped\"trailing")]
    [InlineData(@"KEY=""bad \x escape""")]
    public void Malformed_lines_fail_with_the_line_number(string line)
    {
        var content = "GOOD=1\n" + line + "\nGOOD2=2\n";

        var exception = Assert.Throws<DotEnvFileFormatException>(() => DotEnvFile.Parse(content));

        Assert.Equal(2, exception.LineNumber); // 1-based, pointing at the offending line
        Assert.Contains("Line 2", exception.Message);
        Assert.IsAssignableFrom<FormatException>(exception); // interops with plain format handling
    }

    [Fact]
    public void ParseFile_reads_from_disk_with_encoding()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sealedenv-dotenv-{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(path, "KEY=vàlüé\n", Encoding.UTF8);

            var values = DotEnvFile.ParseFile(path);

            Assert.Equal("vàlüé", values["KEY"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFile_throws_file_not_found()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.env");

        Assert.Throws<FileNotFoundException>(() => DotEnvFile.ParseFile(path));
    }
}

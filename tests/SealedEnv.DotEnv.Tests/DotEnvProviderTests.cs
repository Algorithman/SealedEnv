// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.DotEnv.Tests
// Filename: DotEnvProviderTests.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using SealedEnv.DotEnv;

namespace SealedEnv.DotEnv.Tests;

/// <summary>
///     Provider-level behavior: parsed-once eager load, ordinal lookups, no
///     environment consultation, first-hit-wins chaining — and the real end-to-end:
///     a carrier whose generated Load() reads a .env file from disk through this
///     provider, layered under the process environment.
/// </summary>
public class DotEnvProviderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sealedenv-e2e-{Guid.NewGuid():N}.env");

    public void Dispose()
    {
        File.Delete(_path);
    }

    private DotEnvFileEnvProvider WriteAndCreateProvider(string content)
    {
        File.WriteAllText(_path, content);
        return new DotEnvFileEnvProvider(_path);
    }

    [Fact]
    public void Provider_lookups_come_from_the_file()
    {
        var provider = WriteAndCreateProvider("MYAPP_PORT=9090\nMYAPP_DB=Host=db\n");

        Assert.Equal("9090", provider.Get("MYAPP_PORT"));
        Assert.Equal("Host=db", provider.Get("MYAPP_DB"));
        Assert.Null(provider.Get("MYAPP_MISSING"));
    }

    [Fact]
    public void Provider_parsing_fails_fast_at_construction()
    {
        File.WriteAllText(_path, "NOT A VALID LINE\n");

        Assert.Throws<DotEnvFileFormatException>(() => new DotEnvFileEnvProvider(_path));
    }

    [Fact]
    public void Provider_throws_file_not_found_at_construction()
    {
        Assert.Throws<FileNotFoundException>(() => new DotEnvFileEnvProvider(_path));
    }

    [Fact]
    public void Chained_first_hit_wins_over_the_fallback()
    {
        var provider = new ChainedEnvProvider(
            new DotEnvFileEnvProvider(new Dictionary<string, string?> { ["K"] = "from-file", ["ONLY_FILE"] = "x", }),
            new DotEnvFileEnvProvider(new Dictionary<string, string?> { ["K"] = "from-fallback", ["ONLY_FALLBACK"] = "y", }));

        Assert.Equal("from-file", provider.Get("K"));
        Assert.Equal("x", provider.Get("ONLY_FILE"));
        Assert.Equal("y", provider.Get("ONLY_FALLBACK"));
        Assert.Null(provider.Get("MISSING"));
    }

    [Fact]
    public void Carrier_loads_from_a_real_env_file()
    {
        File.WriteAllText(_path, """
                                 ARR_PORT=9090
                                 ARR_DB=Host=db.internal
                                 ARR_LEVELS=warning,error
                                 ARR_HOST=https://api.example.com/#root
                                 """);

        var config = FileConfig.Load(new DotEnvFileEnvProvider(_path));

        Assert.Equal(9090, config.Port);
        Assert.Equal("Host=db.internal", config.Db);
        Assert.Equal([FileLogLevel.Warning, FileLogLevel.Error,], config.Levels);
        Assert.Equal("https://api.example.com/#root", config.Host.ToString()); // '#' literal in an unquoted URL
    }

    [Fact]
    public void Real_environment_overrides_the_file_via_the_chain()
    {
        File.WriteAllText(_path, "ARR_PORT=9090\nARR_DB=from-file\nARR_LEVELS=info\nARR_HOST=https://file.example.com\n");

        var provider = new ChainedEnvProvider(
            new DictionaryEnvProvider(new Dictionary<string, string?> { ["ARR_DB"] = "from-process-env", }),
            new DotEnvFileEnvProvider(_path));

        var config = FileConfig.Load(provider);

        Assert.Equal(9090, config.Port); // file fills the gap
        Assert.Equal("from-process-env", config.Db); // environment wins
    }

    [Fact]
    public void Hostile_first_provider_degrades_into_aggregated_errors()
    {
        // The loader's TryLoad-never-throws contract holds for a hostile provider:
        // a throw from the chain's first position is an ordinary Invalid per key
        // (a throw is not a miss — the fallback is never reached), and the load
        // still aggregates instead of throwing.
        var provider = new ChainedEnvProvider(new ThrowingProvider(), new DotEnvFileEnvProvider(
            new Dictionary<string, string?> { ["ARR_PORT"] = "9090", }));

        var ok = FileConfig.TryLoad(out var config, out var errors, provider);

        Assert.False(ok);
        Assert.Null(config);
        Assert.Equal(4, errors.Count); // every key, none skipped
        Assert.All(errors, static e => Assert.Equal(EnvErrorCode.Invalid, e.Code));
        Assert.Contains(errors, static e => e.Key == "ARR_PORT");
    }

    private sealed class ThrowingProvider : IEnvProvider
    {
        public string? Get(string key)
        {
            throw new InvalidOperationException("hostile provider");
        }
    }
}

using System.Globalization;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using DotEnv = DotNetEnv.Env;

namespace SealedEnv.Benchmarks;

/// <summary>
///     The 6-field carrier all scenarios bind (SealedEnv-PLAN.md §3 Phase 6).
///     Its values are chosen so every approach can parse them with equivalent
///     semantics — invariant-culture numbers, "true", a TimeSpan literal, a
///     comma-separated host list.
/// </summary>
[SealedEnv(Prefix = "BENCH_")]
public sealed partial record BenchConfig(
    [EnvKey] int Port,
    [EnvKey] bool Verbose,
    [EnvKey] TimeSpan Timeout,
    [EnvKey] string[] Hosts,
    [EnvKey] string DbHost,
    [EnvKey] double Rate);

/// <summary>Settable-property shape the reflection Configuration binder requires.</summary>
public sealed class BinderConfig
{
    public int Port { get; set; }
    public bool Verbose { get; set; }
    public TimeSpan Timeout { get; set; }
    public string[] Hosts { get; set; } = [];
    public string? DbHost { get; set; }
    public double Rate { get; set; }
}

/// <summary>
///     Shared <c>[GlobalSetup]</c>: every scenario class inherits one warm,
///     populated process environment inside BenchmarkDotNet's isolated process.
/// </summary>
public abstract class EnvironmentBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => BenchmarkEnv.Setup();
}

/// <summary>
///     The floor: what the generated code has to beat — six raw
///     <c>Environment.GetEnvironmentVariable</c> calls plus hand-written parsing.
/// </summary>
[MemoryDiagnoser]
public class EnvironmentFloorBenchmarks : EnvironmentBenchmarkBase
{
    private static string? Get(string key)
    {
        return Environment.GetEnvironmentVariable(BenchmarkEnv.Prefix + key);
    }

    [Benchmark(Baseline = true)]
    public BenchConfig ManualParse()
    {
        return new BenchConfig(
            int.Parse(Get("PORT")!, NumberStyles.Integer, CultureInfo.InvariantCulture),
            bool.Parse(Get("VERBOSE")!),
            TimeSpan.Parse(Get("TIMEOUT")!, CultureInfo.InvariantCulture),
            ParseHosts(Get("HOSTS")!),
            Get("DBHOST")!,
            double.Parse(Get("RATE")!, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    internal static string[] ParseHosts(string raw)
    {
        // Hand-rolled on purpose: the floor should be the tightest reasonable
        // hand-written parsing, not LINQ-flavored.
        var parts = raw.Split(',');
        var hosts = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            hosts[i] = parts[i].Trim();
        }

        return hosts;
    }
}

/// <summary>
///     SealedEnv: one generated <c>Load()</c> call. The default
///     <c>EnvironmentEnvProvider</c> performs the same six lookups the floor does;
///     the delta is generated dispatch, validation, error plumbing and record
///     construction.
/// </summary>
[MemoryDiagnoser]
public class SealedEnvBenchmarks : EnvironmentBenchmarkBase
{
    [Benchmark]
    public BenchConfig Load() => BenchConfig.Load();
}

/// <summary>
///     Microsoft.Extensions.Configuration, the idiomatic env-var binding path:
///     build a configuration over the prefixed env vars, then reflection-bind a
///     POCO. The builder enumerates the process environment every call, and
///     <c>Get&lt;T&gt;()</c> is the reflection binder — neither is AOT-safe.
/// </summary>
[MemoryDiagnoser]
public class ConfigurationBinderBenchmarks : EnvironmentBenchmarkBase
{
    [Benchmark]
    public BinderConfig ReflectionBind()
    {
        return new ConfigurationBuilder()
            .AddEnvironmentVariables(BenchmarkEnv.Prefix)
            .Build()
            .Get<BinderConfig>()!;
    }
}

/// <summary>
///     DotNetEnv: per-key typed getters reading the process environment. The
///     package ships only GetString/GetBool/GetInt/GetDouble — the TimeSpan and
///     host-list parsing is hand-written, mirroring what a DotNetEnv user must
///     write for this carrier.
/// </summary>
[MemoryDiagnoser]
public class DotNetEnvBenchmarks : EnvironmentBenchmarkBase
{
    [Benchmark]
    public BenchConfig TypedGetters()
    {
        return new BenchConfig(
            DotEnv.GetInt(BenchmarkEnv.Prefix + "PORT"),
            DotEnv.GetBool(BenchmarkEnv.Prefix + "VERBOSE"),
            TimeSpan.Parse(DotEnv.GetString(BenchmarkEnv.Prefix + "TIMEOUT")!, CultureInfo.InvariantCulture),
            EnvironmentFloorBenchmarks.ParseHosts(DotEnv.GetString(BenchmarkEnv.Prefix + "HOSTS")!),
            DotEnv.GetString(BenchmarkEnv.Prefix + "DBHOST")!,
            DotEnv.GetDouble(BenchmarkEnv.Prefix + "RATE"));
    }
}

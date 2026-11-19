namespace SealedEnv.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Self-check mode: run every scenario once against the same environment and
        // assert they all produce identical config. Guards the benchmark setup (and
        // the README numbers derived from it) against silent drift — cheap enough
        // to run in CI, unlike the benchmarks themselves.
        if (args.Contains("--verify"))
        {
            return ScenarioVerifier.Run();
        }

        BenchmarkDotNet.Running.BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}

/// <summary>Cross-checks all four scenarios field by field; non-zero exit on mismatch.</summary>
internal static class ScenarioVerifier
{
    private static int failures;

    public static int Run()
    {
        BenchmarkEnv.Setup();

        var floor = new EnvironmentFloorBenchmarks().ManualParse();
        var sealedEnv = new SealedEnvBenchmarks().Load();
        var binder = new ConfigurationBinderBenchmarks().ReflectionBind();
        var dotNetEnv = new DotNetEnvBenchmarks().TypedGetters();

        Compare(floor.Port, sealedEnv.Port, "SealedEnv");
        Compare(floor.Port, binder.Port, "Configuration binder");
        Compare(floor.Port, dotNetEnv.Port, "DotNetEnv");
        Compare(floor.Verbose, sealedEnv.Verbose, "SealedEnv");
        Compare(floor.Verbose, binder.Verbose, "Configuration binder");
        Compare(floor.Verbose, dotNetEnv.Verbose, "DotNetEnv");
        Compare(floor.Timeout, sealedEnv.Timeout, "SealedEnv");
        Compare(floor.Timeout, binder.Timeout, "Configuration binder");
        Compare(floor.Timeout, dotNetEnv.Timeout, "DotNetEnv");
        Compare(floor.DbHost, sealedEnv.DbHost, "SealedEnv");
        Compare(floor.DbHost, binder.DbHost, "Configuration binder");
        Compare(floor.DbHost, dotNetEnv.DbHost, "DotNetEnv");
        Compare(floor.Rate, sealedEnv.Rate, "SealedEnv");
        Compare(floor.Rate, binder.Rate, "Configuration binder");
        Compare(floor.Rate, dotNetEnv.Rate, "DotNetEnv");
        Compare(floor.Hosts, sealedEnv.Hosts, "SealedEnv", structural: true);
        Compare(floor.Hosts, binder.Hosts, "Configuration binder", structural: true);
        Compare(floor.Hosts, dotNetEnv.Hosts, "DotNetEnv", structural: true);

        Console.WriteLine(failures == 0
            ? "Scenario verification passed: all four scenarios bind identical values."
            : $"Scenario verification FAILED with {failures} mismatch(es).");
        return failures == 0 ? 0 : 1;
    }

    private static void Compare<T>(T expected, T actual, string scenario, bool structural = false)
    {
        var equal = structural && expected is string[] expectedList && actual is string[] actualList
            ? expectedList.SequenceEqual(actualList)
            : EqualityComparer<T>.Default.Equals(expected, actual);
        if (equal)
        {
            return;
        }

        failures++;
        Console.WriteLine($"MISMATCH [{scenario}]: expected '{Describe(expected)}', got '{Describe(actual)}'.");
    }

    private static string Describe<T>(T value)
    {
        return value is IEnumerable<object> list ? string.Join(", ", list) : value?.ToString() ?? "<null>";
    }
}

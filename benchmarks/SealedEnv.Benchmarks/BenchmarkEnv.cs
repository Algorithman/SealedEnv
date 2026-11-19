namespace SealedEnv.Benchmarks;

/// <summary>
///     One shared environment for every scenario, so all four approaches read the
///     same six variables. <c>[GlobalSetup]</c> runs inside BenchmarkDotNet's
///     isolated benchmark process — the developer machine's environment is never
///     touched, and every scenario reads from the real process environment exactly
///     like a production app would at startup.
/// </summary>
internal static class BenchmarkEnv
{
    internal const string Prefix = "BENCH_";

    internal static void Setup()
    {
        Set("PORT", "8080");
        Set("VERBOSE", "true");
        Set("TIMEOUT", "00:00:30");
        Set("HOSTS", "db.internal,cache.internal,queue.internal");
        // The reflection Configuration binder binds arrays only from indexed
        // children (HOSTS:0..2, delivered via the __ env-var delimiter) — it does
        // NOT split comma strings. BENCH_HOSTS stays for the scenarios that split
        // on the comma themselves; this is the canonical binder input shape.
        Set("HOSTS__0", "db.internal");
        Set("HOSTS__1", "cache.internal");
        Set("HOSTS__2", "queue.internal");
        Set("DBHOST", "postgres.internal");
        Set("RATE", "0.75");
    }

    private static void Set(string key, string value)
    {
        Environment.SetEnvironmentVariable(Prefix + key, value);
    }
}

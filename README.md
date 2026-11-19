# SealedEnv

Source-generated, AOT-friendly, strongly-typed environment variable configuration for .NET.

```csharp
[SealedEnv(Prefix = "MYAPP_")]
public sealed partial record AppConfig(
    [EnvKey("PORT", Default = "8080")] int Port,
    [EnvKey(Required = true)] string DbConnection,   // → MYAPP_DBCONNECTION
    [EnvKey] TimeSpan ShutdownTimeout);              // → MYAPP_SHUTDOWNTIMEOUT

var cfg = AppConfig.Load();   // throws SealedEnvException aggregating ALL errors
```

**Why:** no reflection (NativeAOT-safe), compile-time validation, structured errors
listing every bad variable at once, and a testable provider seam.

> 🚧 Work in progress — the build plan lives in [SealedEnv-PLAN.md](SealedEnv-PLAN.md).
> Current status: **Phase 5 (ecosystem fitness)** — NativeAOT publish of the sample app is
> green and runs on linux-x64 (CI) and win-x64 (verified locally, 1.4 MB binary), trimming
> analyzers are warning-free, and the provider seam is demonstrated in-memory below.
> Remaining: benchmarks (Phase 6), release (Phase 7).

## Repository layout

```
src/SealedEnv/               single NuGet package: runtime + attributes + embedded generator
src/SealedEnv/Generators/    generator assembly (netstandard2.0, packed as analyzer asset)
samples/SampleConsoleApp/    manual end-to-end smoke app
tests/SealedEnv.Tests/       runtime behavior tests (xUnit)
tests/SealedEnv.Generator.Tests/  snapshot tests (Verify) + diagnostics tests
benchmarks/SealedEnv.Benchmarks/  BenchmarkDotNet
```

## Testing your config without touching the environment

`Load`/`TryLoad` accept any `IEnvProvider`. In tests, supply a `DictionaryEnvProvider`
and the process environment is never touched:

```csharp
[Fact]
public void AppConfig_loads_from_the_test_environment()
{
    var config = AppConfig.Load(new DictionaryEnvProvider(new Dictionary<string, string?>
    {
        ["MYAPP_DBCONNECTION"] = "Host=localhost",
        ["MYAPP_PORT"] = "8080",
    }));

    Assert.Equal(8080, config.Port);
}
```

The same carrier type works unchanged against any provider — production code calls
`AppConfig.Load()` (real environment), tests inject a dictionary.

## Not yet supported

- `[EnvKey]` on properties (non-primary-constructor members) — the v0.2 plan is
  partial properties (`[EnvKey] public partial int Port { get; }`, C# 13). Today,
  env-managed members must be primary-constructor parameters.
- Nested or generic carrier types (`SVE0100` warning, nothing is generated).

## Development

```bash
dotnet build
dotnet test
dotnet run --project samples/SampleConsoleApp

# AOT gate: the sample publishes as a self-contained NativeAOT binary
# (PublishAot is declared in its csproj — see SealedEnv-PLAN.md §3 Phase 5).
dotnet publish samples/SampleConsoleApp -c Release -r win-x64 --self-contained
```

## License

[MIT](LICENSE)

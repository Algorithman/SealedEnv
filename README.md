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
> Current status: **Phase 4 (generator hardening)** — incremental caching verified with Roslyn
> tracking tests, all SVE0001–SVE0008 diagnostics live, every carrier shape (record, record
> struct, primary-ctor class, global namespace) generates correctly. Remaining: AOT gate
> (Phase 5), benchmarks (Phase 6), release (Phase 7).

## Repository layout

```
src/SealedEnv/               single NuGet package: runtime + attributes + embedded generator
src/SealedEnv/Generators/    generator assembly (netstandard2.0, packed as analyzer asset)
samples/SampleConsoleApp/    manual end-to-end smoke app
tests/SealedEnv.Tests/       runtime behavior tests (xUnit)
tests/SealedEnv.Generator.Tests/  snapshot tests (Verify) + diagnostics tests
benchmarks/SealedEnv.Benchmarks/  BenchmarkDotNet
```

## Development

```bash
dotnet build
dotnet test
dotnet run --project samples/SampleConsoleApp
```

## License

[MIT](LICENSE)

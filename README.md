# SealedEnv

[![CI](https://github.com/Algorithman/SealedEnv/actions/workflows/ci.yml/badge.svg)](https://github.com/Algorithman/SealedEnv/actions/workflows/ci.yml)
[![NativeAOT](https://img.shields.io/badge/NativeAOT-compatible-brightgreen)](.github/workflows/ci.yml)

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

Supports .NET 7.0–11.0 (net7 is the floor because the AOT-safe `IParsable<T>`
parse route shipped there); the runtime library is multi-targeted and its
semantics matrix re-proven on every maintained runtime. Consumer language
floors, pinned by compiling at the exact version: **parameter-mode carriers
need C# 10** (records, and the emitted file uses a file-scoped namespace;
`record struct` carriers are C# 10 too), **partial-property carriers need
C# 13**. Below a floor you get compiler errors in your carrier and the
generated file — never silently-broken output.

> 🚧 Work in progress — the build plan lives in [SealedEnv-PLAN.md](SealedEnv-PLAN.md).
> Current status: **Phase 7 (release)** — packaging, CHANGELOG and the tag-driven
> release job are wired (`git tag vX.Y.Z` → pack → NuGet push via trusted
> publishing → GitHub release; no stored secrets). The packed nupkg is verified
> end-to-end: installed into a scratch console app via `dotnet add package`,
> generates and runs correctly. Changes since the last tag: see the
> [CHANGELOG](CHANGELOG.md).

## Quickstart

```bash
dotnet add package SealedEnv
```

```csharp
using SealedEnv;

[SealedEnv(Prefix = "MYAPP_")]
public sealed partial record AppConfig(
    [EnvKey("PORT", Default = "8080")] int Port,
    [EnvKey(Required = true)] string DbConnection,
    [EnvKey] TimeSpan ShutdownTimeout);

var config = AppConfig.Load();
```

`Load()` either returns a fully-valid config or throws a `SealedEnvException`
whose message lists every problem at once:

```
SealedEnv configuration is invalid (2 errors):
  - MYAPP_PORT [Invalid] (int) Environment variable 'MYAPP_PORT' has value "cats" which is not a valid Int32 value for AppConfig.Port.
  - MYAPP_DBCONNECTION [Missing] (string) Environment variable 'MYAPP_DBCONNECTION' is not set and no default is configured for AppConfig.DbConnection.
```

For tests and CLIs that prefer structured data, `TryLoad` never throws:

```csharp
if (!AppConfig.TryLoad(out var config, out var errors))
{
    foreach (var error in errors)
        Console.Error.WriteLine($"{error.Key} [{error.Code}]: {error.Message}");
    return 1;
}
```

## Partial-property carriers (C# 13)

Prefer explicit property lists over a primary constructor? Declare the bindings
as **get-only partial properties** and let the generator supply the
implementation, the backing fields and the constructor `Load()` builds through:

```csharp
[SealedEnv(Prefix = "MYAPP_")]
public sealed partial class AppConfig
{
    [EnvKey("PORT", Default = "8080")] public partial int Port { get; }
    [EnvKey(Required = true)] public partial string DbConnection { get; }
    [EnvKey] public partial TimeSpan ShutdownTimeout { get; }
}

var config = AppConfig.Load();
```

Everything else is identical to the primary-constructor mode — same key
derivation, same semantics matrix, same `Load()`/`TryLoad()`, same provider
seam, same doc export. A carrier binds **either** primary-constructor parameters
**or** partial properties, never both (`SVE0011`), and a property carrier may
not declare its own constructor (`SVE0013`) — `Load()` is the way in. The
supported shape is a `partial` get-only instance property with no implementation
part (`SVE0012` says so otherwise); `readonly` structs work, because the
generated fields are readonly and assigned from the generated constructor only.

## How keys are derived

Every key is `Prefix` + the member name (parameter or property) in
`SCREAMING_SNAKE_CASE`, with underscores only at acronym boundaries:

| Member | Key (with prefix `MYAPP_`) |
|---|---|
| `Port` | `MYAPP_PORT` |
| `DbConnection` | `MYAPP_DBCONNECTION` |
| `APIKey` | `MYAPP_API_KEY` |
| `ID` | `MYAPP_ID` |
| explicit `[EnvKey("PORT")]` | `MYAPP_PORT` |

## Semantics

| Configuration | Unset | Empty string | Malformed |
|---|---|---|---|
| value type, no default | `Missing` error | `Empty` error (or unset if `EmptyAsUnset`) | `Invalid` error |
| `string`, not required, not nullable | `Missing` error | `Empty` error | — |
| `string?` | `null` | `""` | — |
| `Required = true` | `Missing` error | `Empty` error unless `AllowEmpty` | `Invalid` error |
| `Default` given | default parsed & used | per `EmptyAsUnset`/`AllowEmpty` | default malformed → **compile-time** diagnostic |

Details pinned by tests: `EmptyAsUnset` applies before requiredness (empty +
required → `Missing`, not `Empty`); a default does not rescue an explicit empty
when `EmptyAsUnset = false`; a present-but-malformed value beats a well-formed
default; `string?` members never raise `Empty`. `EmptyAsUnset` defaults to
`true` (the dotenv convention — `PORT=""` behaves exactly like unset).

## Supported member types

| Type | Parsing |
|---|---|
| `int`, `long`, `double` | invariant culture; `double` rejects thousands separators (`"1,234"` → error, not `1234`) |
| `bool` | `1` / `0` / `true` / `false` / `on` / `off`, case-insensitive |
| `string`, `string?` | as-is |
| `TimeSpan` | BCL semantics, invariant culture (bare `"30"` = 30 days) |
| `Uri` | absolute URIs only — platform-invariant: an explicit scheme is required (`/path` is rejected even on Unix, where the BCL would map it to `file:///path`) |
| `Guid` | `Guid.Parse` semantics |
| enums | `Enum.Parse` semantics, case-insensitive; numeric and flag lists accepted |
| `IParsable<T>` | `T.Parse(value, null)` called directly — zero boxing, AOT-safe |
| custom `[EnvKey(Parser = typeof(X))]` | `public static T Parse(string)` method group |
| lists of the above | `T[]` / `IReadOnlyList<T>` — split on `Separator` (default `,`), `\,` escape, entries trimmed, **each entry parsed by the element's scalar rule** (strict doubles, bool tokens, absolute Uris, `Enum.TryParse` member/numeric/flags) |
| `string[]`, `IReadOnlyList<string>` | split as above, entries kept as strings |

List members whose type implements `IParsable<T>` per element (`DateOnly[]`, `decimal[]`,
`Point[]`…) route automatically; `[EnvKey(Parser = typeof(X))] X[] Items` parses **each entry**
with `X.Parse`, while a parser returning the whole array (`static int[] Parse(string)`) binds
unsplit — your choice. A malformed entry is one `Invalid` error for the member, naming every bad
entry with its 1-based index (`entry 2 "abc": not a valid Int32 value`) — one variable, one error.
A flag-enum combo containing the separator must be escaped (`"Read\,Write, Execute"` is two
entries: the combo + `Execute`) or use a different `Separator`.

Unsupported types fail compilation with `SVE0002` — never silently.

## Compile-time diagnostics

| ID | Meaning |
|---|---|
| `SVE0001` | `[EnvKey]` on a parameter outside a primary constructor |
| `SVE0002` | Member type has no parser route |
| `SVE0003` | Two members resolve to the same env key |
| `SVE0004` | `Default` does not parse as the member type |
| `SVE0005` | Carrier type is not `sealed` |
| `SVE0006` | `Parser` lacks `public static T Parse(string)` |
| `SVE0007` | Env-managed member (parameter or partial property) without `[EnvKey]` |
| `SVE0008` | Carrier already declares a generated member name (`Load`/`TryLoad`/`SealedEnvBind`/`ConfigurationDocumentation`/a `__` backing field) |
| `SVE0009` | `[SealedEnv]` prefix has leading or trailing whitespace |
| `SVE0010` | Carrier has no primary constructor and no partial properties |
| `SVE0011` | Carrier mixes primary-constructor and partial-property bindings |
| `SVE0012` | `[EnvKey]` property is not a get-only partial property |
| `SVE0013` | Property carrier declares its own constructor |
| `SVE0100` | Nested / generic / ref-struct carrier (not supported yet, warning) |

Blocking diagnostics suppress generation; `SVE0003` still generates
(deterministically — both members read the same variable).

## What the generator emits

Thin members only — all parsing lives in the runtime library:

```csharp
public static AppConfig Load(global::SealedEnv.IEnvProvider? provider = null)
{
    var bound = SealedEnvBind(provider ?? global::SealedEnv.EnvironmentEnvProvider.Instance, out var __errors);
    if (bound is null)
    {
        throw new global::SealedEnv.SealedEnvException(__errors);
    }

    return bound;
}

private static AppConfig? SealedEnvBind(global::SealedEnv.IEnvProvider provider, out ...)
{
    int __Port = global::SealedEnv.SealedEnvLoader.Int(provider, "MYAPP_PORT", "AppConfig.Port", "8080", false, false, true, ref __errors);
    string __DbConnection = global::SealedEnv.SealedEnvLoader.String(provider, "MYAPP_DBCONNECTION", "AppConfig.DbConnection", null, true, false, true, ref __errors);
    // read all keys → validate all → construct, or aggregate every error
}
```

## Benchmarks

Four ways to bind the same 6-field carrier (`int`, `bool`, `TimeSpan`,
`string[]`, `string`, `double`) from the process environment, run with
BenchmarkDotNet (`dotnet run -c Release --project benchmarks/SealedEnv.Benchmarks -- --filter "*"`),
default job, x64, .NET 10; all scenarios read identical environment variables
set once per benchmark process. `Ratio` is relative to the hand-written floor.

<!-- Measured 2026-11-19, BenchmarkDotNet v0.15.8 DefaultJob, one run per scenario in its
     own isolated process. Host: Windows 10 22H2, AMD Ryzen 9 9950X3D, .NET SDK 10.0.404,
     runtime .NET 10.0.15, x64 RyuJIT. Ratios computed against the floor's mean. -->

| Scenario | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| ManualParse (floor) | 264.7 ns | 1.00× | 896 B |
| **SealedEnv `Load()`** | **302.3 ns** | **1.14×** | **784 B** |
| DotNetEnv typed getters | 247.6 ns | 0.94× | 608 B |
| Configuration binder (reflection) | 10.88 μs | 41.1× | 36.35 KB |

Generated `Load()` costs ~14% over the hand-written floor and allocates *less*
than it — validation and error plumbing replace a second host array and pay for
themselves. DotNetEnv edges out the floor because its getters use a leaner
per-key environment lookup than `Environment.GetEnvironmentVariable` (whose
Windows path allocates a growing buffer per call) — but it offers no TimeSpan
or array getters, so those parses are hand-written in its column.

The scenarios:

- **ManualParse (floor)** — six `Environment.GetEnvironmentVariable` calls plus
  hand-written invariant-culture parsing. What generated code has to beat.
- **SealedEnvLoad** — one generated `Load()` call.
- **ReflectionBind** — `new ConfigurationBuilder().AddEnvironmentVariables("BENCH_").Build().Get<Poco>()`,
  the idiomatic Microsoft.Extensions.Configuration path.
- **DotNetEnv** — per-key typed getters (`GetInt`/`GetBool`/…). The package
  ships no TimeSpan/array getters, so those two are hand-written, as its users
  must.

A cross-check (`dotnet run --project benchmarks/SealedEnv.Benchmarks -- --verify`)
asserts all four scenarios bind identical values, and runs in CI.

## Why not the Microsoft.Extensions.Configuration binder?

Nothing stops you from binding env vars with `ConfigurationBuilder` + `Get<T>()` —
if you already run Configuration for appsettings.json, keep it. For plain
environment-variable startup config, SealedEnv differs where it matters:

1. **AOT.** The binder is reflection-based; NativeAOT publishes of binder-based
   apps need workarounds, and the reflection path is exactly what the benchmark
   above prices in. SealedEnv generates direct calls into `IParsable<T>`-style
   parsing — publish the sample app and see.
2. **Failure semantics.** The binder silently drops what it cannot map: a
   missing required value binds a `default`, a malformed value binds a default
   or zero, and nothing tells you. SealedEnv reports every bad variable with key
   + error code + message in one exception — the difference between "config was
   quietly empty" and "here is the list to fix".
3. **Semantics you can state.** `EmptyAsUnset`, `Required`, `AllowEmpty`,
   `Default`-must-parse (checked at *compile* time), strict invariant-culture
   doubles, absolute-only URIs — a deliberate, test-pinned matrix (above) rather
   than binder conventions.
4. **Arrays.** The reflection binder binds `string[]` only from indexed children
   (`MYAPP_HOSTS__0`, `MYAPP_HOSTS__1`, …); a comma-separated `MYAPP_HOSTS`
   binds an empty array. SealedEnv splits comma strings with `\,` escapes and
   trimmed entries.
5. **Cost.** ~41× the time and ~41× the allocation of SealedEnv's `Load()` on
   the benchmark carrier — the binder path builds a configuration tree and
   reflection-binds a POCO on every load.

## Doc export

Opt a carrier into documentation with `GenerateDocumentation = true`:

```csharp
[SealedEnv(Prefix = "MYAPP_", GenerateDocumentation = true)]
public sealed partial record AppConfig(
    [EnvKey("PORT", Default = "8080", Description = "TCP port")] int Port,
    [EnvKey(Required = true, Description = "Connection string")] string DbConnection);

Console.WriteLine(AppConfig.ConfigurationDocumentation);
// | Key | Type | Required | Default | Description |
// |-----|------|----------|---------|-------------|
// | `MYAPP_PORT` | `int` | No | `8080` | TCP port |
// | `MYAPP_DBCONNECTION` | `string` | Yes | — | Connection string |
```

The table is a plain markdown string — render it at runtime (`--help-config`
style) or dump it to a file in your docs pipeline. Pipes and other
markdown-hostile characters in keys and descriptions are escaped; declaring a
member named `ConfigurationDocumentation` alongside the flag is a compile-time
error (`SVE0008`).

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

## .env files (satellite package)

[`SealedEnv.DotEnv`](https://www.nuget.org/packages/SealedEnv.DotEnv) adds a
dotenv file provider — no generator surface, just another `IEnvProvider`:

```csharp
using SealedEnv.DotEnv;

// The conventional layering: real environment variables win, the file fills the gaps.
var config = AppConfig.Load(new ChainedEnvProvider(
    new EnvironmentEnvProvider(),
    new DotEnvFileEnvProvider(".env")));
```

The parser is line-based and fail-fast (a malformed line throws
`DotEnvFileFormatException` with the 1-based line number — a typo in a
developer-authored file should be loud, not skipped). `#` comments, `export `
prefixes, quoted values with `\n`/`\t` escapes, last-wins duplicates; see the
package README for the exact contract. Swap the chain to prefer the file, and
keys stay case-sensitive ordinal so tests behave identically on every OS.

## Not yet supported

- Mixing primary-constructor and partial-property bindings in one carrier
  (`SVE0011` blocks it — pick one style per type).
- Nested or generic carrier types (`SVE0100` warning, nothing is generated).

## Development

```bash
dotnet build
dotnet test
dotnet run --project samples/SampleConsoleApp

# AOT gate: the sample publishes as a self-contained NativeAOT binary
# (PublishAot is declared in its csproj — see SealedEnv-PLAN.md §3 Phase 5).
dotnet publish samples/SampleConsoleApp -c Release -r win-x64 --self-contained

# Benchmark scenario cross-check (fast), or a full BenchmarkDotNet run.
dotnet run --project benchmarks/SealedEnv.Benchmarks -- --verify
dotnet run -c Release --project benchmarks/SealedEnv.Benchmarks -- --filter "*"

# Release: bump VersionPrefix in Directory.Build.props, move the
# AnalyzerReleases.Unshipped.md table into Shipped, then tag — CI packs,
# pushes to NuGet via trusted publishing (OIDC, no stored secrets) and
# creates the GitHub release.
grep VersionPrefix Directory.Build.props
git tag vX.Y.Z && git push origin vX.Y.Z
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the generator pipeline, test layout
and diagnostics reference.

## License

[MIT](LICENSE)

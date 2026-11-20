# Changelog

All notable changes to SealedEnv are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com); versions follow the
`VersionPrefix` in `Directory.Build.props`.

## 0.9.0.4 - 2026-11-20

### Added

- **Partial-property carriers (C# 13)**: a `[SealedEnv]` carrier may now bind
  `[EnvKey]` get-only partial properties instead of primary-constructor
  parameters — `[EnvKey] public partial int Port { get; }`. The generator emits
  the implementing declarations, the private readonly backing fields and the
  constructor `Load()`/`TryLoad()` build the carrier through. Semantics are
  identical to the parameter mode (same key derivation, same semantics matrix,
  same error aggregation, same provider seam, same doc export); works on
  `sealed partial class` / `record` / (record) `struct`, including `readonly`
  structs (readonly fields assigned from the generated constructor only).
- New blocking diagnostics: `SVE0011` (carrier mixes parameter and property
  bindings), `SVE0012` (`[EnvKey]` property is not a partial get-only instance
  definition — non-partial, set/init accessor, static, indexer, `required`,
  already implemented, accessor accessibility), `SVE0013` (property carrier
  declares an explicit constructor — the generated one would collide, and user
  constructions would silently see default fields).
- `EnvKeyAttribute` now targets `AttributeTargets.Property` alongside
  `Parameter`; an unmarked partial property on a carrier with no other binding
  surface is reported as `SVE0007` (the precise "forgot `[EnvKey]`" message)
  rather than the blunter `SVE0010`.
- `SVE0008` also guards the generated `__<Property>` backing fields of
  property-mode carriers.

### Notes

- The generator keeps its Microsoft.CodeAnalysis.CSharp 4.11 floor (older
  hosts can still load it); the generator test harness parses C# 13 snippets
  with a `VersionOverride` to 4.14. Consumer projects need C# 13 to declare
  partial-property carriers.

## 0.9.0.3 - 2026-11-19

First release: source-generated, AOT-friendly, strongly-typed environment
variable configuration for .NET.

### Added

- `[SealedEnv]` carrier attribute (prefix, `EmptyAsUnset`) and `[EnvKey]` member
  attribute (explicit key, `Required`, `Default`, `AllowEmpty`, `Separator`,
  `Parser`, `Description`) on primary-constructor parameters of sealed
  record / record struct / class / plain struct carriers.
- Generated `Load(provider = null)` throwing `SealedEnvException` that
  aggregates every bad variable, and `TryLoad(out config, out errors, provider)`
  that never throws.
- Member types: `int`, `long`, `double` (strict, no thousands separators),
  `bool` (`1/0/true/false/on/off`), `string`, `string?`, `TimeSpan`,
  `Uri` (absolute only), `Guid`, enums, `IParsable<T>`, custom
  `[EnvKey(Parser = ...)]`, and `string[]` (separator split with `\,` escape,
  trimmed entries).
- `IEnvProvider` seam with `EnvironmentEnvProvider` and `DictionaryEnvProvider`
  for test injection; a provider that throws has its failure recorded as an
  ordinary `Invalid` error, so `TryLoad` never throws regardless of provider
  behavior.
- Compile-time diagnostics SVE0001–SVE0010 and SVE0100: unsupported types,
  malformed defaults, duplicate keys, non-sealed carriers, invalid parser
  shapes, missing `[EnvKey]`, member name conflicts, unsupported carrier shapes.
- Doc export: `[SealedEnv(GenerateDocumentation = true)]` emits a
  `ConfigurationDocumentation` markdown table (Key / Type / Required / Default /
  Description from `[EnvKey(Description = ...)]`) on the carrier — render
  configuration help at runtime or dump it for docs pipelines.
- SCREAMING_SNAKE_CASE key derivation with acronym-boundary underscores
  (`DbConnection` → `DBCONNECTION`, `APIKey` → `API_KEY`).
- NativeAOT-compatible and trim-clean (CI publishes and runs the sample on
  linux-x64 and win-x64); benchmarks: generated `Load()` at 1.14× a
  hand-written floor with less allocation, ~41× faster than the reflection
  Configuration binder on the benchmark carrier.
- Adversarial hardening: custom parsers may throw any exception and may return
  null — every parser failure is an ordinary `Invalid` error, never an escaped
  exception or a smuggled null member; case-variant duplicate keys (`port` vs
  `PORT`) are flagged at compile time (identical modulo case — they read the same
  variable on Windows); carriers without a primary constructor are rejected
  (`SVE0010`), and `[SealedEnv]` may sit on any partial declaration.
- Adversarial hardening: `Uri` parsing is platform-invariant (explicit scheme
  required — Unix would otherwise accept `/path` as `file:///path`), and
  prefixes with leading or trailing whitespace are rejected at compile time
  (`SVE0009`) instead of silently producing unmatchable keys.
- Strong-named runtime and generator assemblies (public key token
  `0bea3f265278acb1`, key committed with the repo per analyzer-package
  convention).

# Changelog

All notable changes to SealedEnv are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com); versions follow the
`VersionPrefix` in `Directory.Build.props`.

## 0.9.0.3 - 2026-11-19

First release: source-generated, AOT-friendly, strongly-typed environment
variable configuration for .NET.

### Added

- `[SealedEnv]` carrier attribute (prefix, `EmptyAsUnset`) and `[EnvKey]` member
  attribute (explicit key, `Required`, `Default`, `AllowEmpty`, `Separator`,
  `Parser`, `Description`) on primary-constructor parameters of sealed
  record / record struct / class carriers.
- Generated `Load(provider = null)` throwing `SealedEnvException` that
  aggregates every bad variable, and `TryLoad(out config, out errors, provider)`
  that never throws.
- Member types: `int`, `long`, `double` (strict, no thousands separators),
  `bool` (`1/0/true/false/on/off`), `string`, `string?`, `TimeSpan`,
  `Uri` (absolute only), `Guid`, enums, `IParsable<T>`, custom
  `[EnvKey(Parser = ...)]`, and `string[]` (separator split with `\,` escape,
  trimmed entries).
- `IEnvProvider` seam with `EnvironmentEnvProvider` and `DictionaryEnvProvider`
  for test injection.
- Compile-time diagnostics SVE0001–SVE0008 and SVE0100: unsupported types,
  malformed defaults, duplicate keys, non-sealed carriers, invalid parser
  shapes, missing `[EnvKey]`, member name conflicts, unsupported carrier shapes.
- SCREAMING_SNAKE_CASE key derivation with acronym-boundary underscores
  (`DbConnection` → `DBCONNECTION`, `APIKey` → `API_KEY`).
- NativeAOT-compatible and trim-clean (CI publishes and runs the sample on
  linux-x64 and win-x64); benchmarks: generated `Load()` at 1.14× a
  hand-written floor with less allocation, ~41× faster than the reflection
  Configuration binder on the benchmark carrier.
- Strong-named runtime and generator assemblies (public key token
  `0bea3f265278acb1`, key committed with the repo per analyzer-package
  convention).

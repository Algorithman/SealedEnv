# Contributing (notes for my future self)

Prerequisites, commands, and the generator's pipeline — everything needed to
change SealedEnv without re-deriving its design from the source.

## Setup

- .NET SDK **10.0.404** pinned by `global.json` (`rollForward: latestFeature` —
  any newer 10.0.4xx feature band works because of a vulnerability in 10.0.2xx).
- No other tooling. Tests, generator snapshots and the AOT gate are plain
  `dotnet` commands.

## Commands

```bash
dotnet build                                              # everything (solution file: SealedEnv.slnx)
dotnet test                                               # runtime tests + generator snapshot/diagnostic tests
dotnet format --verify-no-changes                         # CI enforces this; run `dotnet format` to fix
dotnet run --project samples/SampleConsoleApp             # manual end-to-end smoke
dotnet publish samples/SampleConsoleApp -c Release -r win-x64 --self-contained   # NativeAOT gate
dotnet run --project benchmarks/SealedEnv.Benchmarks -- --verify   # benchmark scenario cross-check (seconds)
dotnet run -c Release --project benchmarks/SealedEnv.Benchmarks -- --filter "*"   # full BenchmarkDotNet run
```

`TreatWarningsAsErrors` is on everywhere. CI (`.github/workflows/ci.yml`)
runs format-verify, build+test on Linux/Windows, and the AOT publish+run gate
on linux-x64/win-x64.

## Project map

```
src/SealedEnv/                  runtime lib (net10.0): attributes, providers, EnvError, SealedEnvLoader
src/SealedEnv/Generators/       generator assembly (netstandard2.0), packed as analyzer asset of the same package
samples/SampleConsoleApp/       smoke app; its csproj declares PublishAot (see AOT note below)
tests/SealedEnv.Tests/          runtime behavior tests + generated-carrier E2E tests
tests/SealedEnv.Generator.Tests/  Verify snapshots + diagnostic assertions + pipeline-caching tests
benchmarks/SealedEnv.Benchmarks/  BenchmarkDotNet, four scenarios, `--verify` self-check
```

Consumer wiring (the classic footgun — the generator is attached as an
**analyzer**, never referenced as a library):

```xml
<ProjectReference Include="...\src\SealedEnv\Generators\SealedEnvGenerator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Generator pipeline

```
SyntaxProvider.ForAttributeWithMetadataName("SealedEnv.SealedEnvAttribute")
│   candidate: TypeDeclarationSyntax carrying [SealedEnv]
▼
SealedEnvModel.Transform  ── per carrier ────────────────────────────────────
│   attribute args        → Prefix, EmptyAsUnset
│   shape checks          → SVE0005 (not sealed), SVE0100 (nested/generic/ref struct),
│                           SVE0008 (user already declares Load/TryLoad/SealedEnvBind)
│   per primary-ctor param:
│     no [EnvKey]         → SVE0007 (blocking)
│     type routing        → MemberKind (Int/Long/Double/Bool/String/NullableString/
│                           TimeSpan/Uri/Guid/Enum/Parsable/Custom/StringArray)
│                           no route → SVE0002 (blocking)
│     Parser shape        → public static T Parse(string) else SVE0006 (blocking)
│     Default check       → mirrors runtime parse rules, else SVE0004 (blocking)
│     key resolution      → explicit key or SCREAMING_SNAKE_CASE (EnvKeyDerivation,
│                           underscores only at acronym boundaries)
│   duplicate resolved key → SVE0003 (non-blocking: generation continues)
▼
EnvTargetModel  ── hand-written value object ────────────────────────────────
│   Equals compares everything except Location; ImmutableArrays compare
│   element-wise. This is what lets Roslyn's incremental cache skip emission
│   when an unrelated file changed (PipelineCachingTests pins this).
▼
RegisterSourceOutput
│   report DiagnosticInfos → Diagnostic.Create(SveDiagnostics.ById[id], ...)
│   blocking diagnostics   → SkipGeneration (diagnostics only, no source)
▼
SealedEnvEmitter.Emit  ── thin code only ────────────────────────────────────
│   partial type adds Load() / TryLoad() / private SealedEnvBind(provider, out errors)
│   per member: one SealedEnvLoader.<Kind>(...) call; all parsing/validation
│   lives in the runtime lib so generator changes rarely move snapshots
▼
AddSource(HintName)  ── emitted into the consumer's compilation
```

Stray-attribute pipeline (parallel, cannot double-report with the carrier
pass): `CreateSyntaxProvider(StrayEnvKeyDetection.IsCandidate → Transform)`
reports SVE0001 for `[EnvKey]` on parameters outside a primary constructor,
deduplicated through `DiagnosticInfoComparer` so unchanged diagnostics stay
cached.

### Design invariants (do not erode these)

1. **Thin generated code.** Logic belongs in `SealedEnvLoader` (runtime). A
   generator change should not require snapshot rewrites.
2. **Value-model caching.** Nothing implementing `ISymbol`/`SyntaxNode` may
   flow past `SealedEnvModel.Transform` — models compare by value.
3. **Runtime helpers are public** (`[EditorBrowsable(Never)]`): generated code
   compiles into the *consumer's* assembly and cannot see this one's internals.
4. **Culture-invariant everywhere**, aggregate errors (never fail-fast),
   `TryLoad` never throws.
5. **Freeze the attributes.** `SealedEnvAttribute`/`EnvKeyAttribute` signatures
   are parsed by the generator and consumed by generated code — changes ripple
   everywhere.

## Tests

- `tests/SealedEnv.Tests` — every `SealedEnvLoader` helper has direct unit
  tests (malformed-value attacks included), plus the `SemanticsMatrixTests`
  (unset/empty/malformed × value-type/string/string?/required/default) and
  generated-carrier E2E over record / record struct / primary-ctor class.
- `tests/SealedEnv.Generator.Tests` — Verify snapshots for emitted code
  (accept a new snapshot by renaming
  `*.received.txt` → `*.verified.txt`, review it first); assertion-style
  diagnostics tests for every SVE code; `PipelineCachingTests` for cache
  behavior (edited unrelated file → byte-identical output, all steps cached).

## Diagnostics reference

| ID | Meaning | Blocks generation |
|---|---|---|
| SVE0001 | `[EnvKey]` on a parameter outside a primary constructor | n/a (no carrier) |
| SVE0002 | Parameter type has no parser route | yes |
| SVE0003 | Two parameters resolve to the same env key | no (both bind the same value) |
| SVE0004 | `Default` does not parse as the parameter type | yes |
| SVE0005 | Carrier is not `sealed` (record structs pass implicitly) | yes |
| SVE0006 | `Parser` lacks `public static T Parse(string)` | yes |
| SVE0007 | Primary-constructor parameter without `[EnvKey]` | yes |
| SVE0008 | Carrier already declares `Load`/`TryLoad`/`SealedEnvBind` | yes |
| SVE0100 | Nested / generic / ref-struct carrier — not supported yet | yes (warning) |

## Releasing

1. Bump `<VersionPrefix>` in `Directory.Build.props` (the only version source —
   it flows into the assembly, the nupkg and the generated
   `GeneratedCodeAttribute`).
2. Move the `AnalyzerReleases.Unshipped.md` rule table into
   `AnalyzerReleases.Shipped.md` under a `## Release X.Y` header (plain
   version — the RS2007 header parser rejects prerelease suffixes; the
   Unshipped file itself carries **no** release header while work is in
   progress).
3. Update CHANGELOG.md.
4. `git tag vX.Y.Z && git push origin vX.Y.Z` — the tag must match
   `VersionPrefix` exactly (the CI `release` job hard-fails otherwise). CI
   packs with `ContinuousIntegrationBuild`, pushes to nuget.org
   (`NUGET_API_KEY` secret) and creates the GitHub release with the nupkg +
   snupkg attached.

Packaging decisions to remember: SourceLink comes from the SDK's built-in git
integration (the `Microsoft.SourceLink.GitHub` 8.0.0 package conflicts with
SDK 10 targets — MSB4057); the generator dll + pdb are packed as
`analyzers/dotnet/cs` assets via `None` items because `Pack="true"` on a
`ProjectReference` carries no analyzer output. Every assembly strong-names
against the committed root `SealedEnv.snk` (identity, not secrecy — the
established OSS practice for analyzer packages; signing is deterministic and
needs no CI secret). netstandard2.0's `InternalsVisibleToAttribute` surface
has no `PublicKey` property (CS0246), so the generator's friend access to the
test assembly is declared as an MSBuild `InternalsVisibleTo` item with the
full public key in its csproj — regenerating the snk means updating that key.

## AOT note

`PublishAot` lives in the **sample csproj**, not in CI as a global
`-p:PublishAot=true` — global properties flow into the referenced
netstandard2.0 generator project and fail it with NETSDK1207. The CI
`aot-publish` job publishes and *runs* the sample both ways (happy path and
aggregated-failure exit code 1).

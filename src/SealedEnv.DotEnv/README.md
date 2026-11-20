# SealedEnv.DotEnv

A [.env](https://hexdocs.pm/dotenv/env-file.html) file provider for
[SealedEnv](https://www.nuget.org/packages/SealedEnv) — layer dotenv files under
the process environment through the `IEnvProvider` seam. No generator surface,
pure runtime: the core package's `Load(provider)` takes any provider.

```bash
dotnet add package SealedEnv.DotEnv
```

```csharp
using SealedEnv;
using SealedEnv.DotEnv;

// The conventional layering: real environment variables win, the file fills the gaps.
var config = AppConfig.Load(new ChainedEnvProvider(
    new EnvironmentEnvProvider(),
    new DotEnvFileEnvProvider(".env")));
```

The provider parses the file once at construction (format errors throw
immediately, with the line number), then serves lookups from memory.

## File format

`KEY=VALUE`, one per line; `#` comments (full-line, and inline on unquoted
values); `export ` prefixes stripped; `KEY=` is an explicit empty value;
duplicate keys — last wins. Double-quoted values keep `#` literally and process
`\n`, `\r`, `\t`, `\\`, `\"` escapes (multiline values are written with `\n`,
not physical newlines); single-quoted values are fully literal. A malformed
line throws `DotEnvFileFormatException` with the 1-based line number — a typo
in a developer-authored file should be loud, not skipped.

Prefer file-over-environment? Swap the chain:
`new ChainedEnvProvider(new DotEnvFileEnvProvider(".env"), new EnvironmentEnvProvider())`.
Keys are case-sensitive ordinal, matching `DictionaryEnvProvider`, so test
assertions behave the same on every OS.

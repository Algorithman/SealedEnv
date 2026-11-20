## Release 1.0.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SVE0001 | SealedEnv | Error | [EnvKey] on a non-primary-constructor parameter
SVE0002 | SealedEnv | Error | Unsupported parameter type
SVE0003 | SealedEnv | Error | Duplicate environment key
SVE0004 | SealedEnv | Error | Default value does not parse as the parameter type
SVE0005 | SealedEnv | Error | Carrier type is not sealed
SVE0006 | SealedEnv | Error | Parser type lacks a usable Parse method
SVE0007 | SealedEnv | Error | Primary constructor parameter lacks [EnvKey]
SVE0008 | SealedEnv | Error | Member name conflicts with a generated member
SVE0009 | SealedEnv | Error | [SealedEnv] prefix has leading or trailing whitespace
SVE0010 | SealedEnv | Error | Carrier has no primary constructor
SVE0011 | SealedEnv | Error | Carrier mixes primary-constructor and partial-property bindings
SVE0012 | SealedEnv | Error | [EnvKey] property is not a get-only partial property
SVE0013 | SealedEnv | Error | Property carrier declares its own constructor
SVE0100 | SealedEnv | Warning | Carrier shape not yet supported

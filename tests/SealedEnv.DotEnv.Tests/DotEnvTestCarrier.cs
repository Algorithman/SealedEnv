// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.DotEnv.Tests
// Filename: DotEnvTestCarrier.cs
//
// Created:       2026-11-20
//
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using SealedEnv;

namespace SealedEnv.DotEnv.Tests;

public enum FileLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
///     The end-to-end carrier: its Load()/TryLoad() members are generated during
///     this build and bind from a real .env file on disk through
///     <see cref="DotEnvFileEnvProvider" /> — scalar, enum list, Uri (with a '#'
///     fragment surviving the unquoted comment rule) and a required string.
/// </summary>
[SealedEnv(Prefix = "ARR_")]
internal sealed partial record FileConfig(
    [EnvKey] int Port,
    [EnvKey] FileLogLevel[] Levels,
    [EnvKey] Uri Host,
    [EnvKey(Required = true)] string Db);

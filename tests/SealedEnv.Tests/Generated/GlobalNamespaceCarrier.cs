// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv.Tests
// Filename: GlobalNamespaceCarrier.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 13:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

// No namespace: exercises the generator's global-namespace emission path
// (generated partial without a namespace declaration).

using SealedEnv;

[SealedEnv(Prefix = "GLOBAL_")]
internal sealed partial record GlobalNamespaceCarrier([EnvKey] int Port);

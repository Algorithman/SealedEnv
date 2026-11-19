// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnvGenerator
// Filename: InternalsVisibleTo.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 12:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

// Unit tests for generator internals live in SealedEnv.Generator.Tests.
[assembly: InternalsVisibleTo("SealedEnv.Generator.Tests")]

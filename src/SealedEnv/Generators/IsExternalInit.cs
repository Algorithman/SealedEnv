// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnvGenerator
// Filename: IsExternalInit.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 12:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

// ReSharper disable once CheckNamespace

namespace System.Runtime.CompilerServices;

/// <summary>
///     Polyfill: records need <c>IsExternalInit</c> for their init accessors, and the
///     generator assembly targets netstandard2.0 where the type does not exist.
/// </summary>
internal static class IsExternalInit;

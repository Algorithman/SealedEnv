// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill: records need <c>IsExternalInit</c> for their init accessors, and the
/// generator assembly targets netstandard2.0 where the type does not exist.
/// </summary>
internal static class IsExternalInit;

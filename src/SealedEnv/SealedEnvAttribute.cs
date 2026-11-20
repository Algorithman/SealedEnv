// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv
// Filename: SealedEnvAttribute.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

namespace SealedEnv;

/// <summary>
///     Marks a record/class/struct as an environment-variable configuration type.
///     The source generator emits <c>Load()</c>/<c>TryLoad()</c> members that read
///     every member decorated with <see cref="EnvKeyAttribute" /> — either all
///     primary-constructor parameters, or all partial properties; a carrier mixes
///     neither.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SealedEnvAttribute : Attribute
{
    /// <summary>
    ///     Prefix prepended to every resolved key of the type (e.g. <c>"MYAPP_"</c>).
    ///     Keys default to <c>SCREAMING_SNAKE_CASE</c> of the parameter name.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    ///     Treats an empty environment value as unset (dotenv convention; avoids the
    ///     classic <c>PORT=""</c> footgun). <see langword="true" /> by default.
    /// </summary>
    public bool EmptyAsUnset { get; set; } = true;

    /// <summary>
    ///     Emits a <c>ConfigurationDocumentation</c> string member on the carrier:
    ///     a markdown table (Key / Type / Required / Default / Description, from
    ///     <see cref="EnvKeyAttribute.Description" />) for runtime help rendering or
    ///     dumping to a docs file. <see langword="false" /> by default.
    /// </summary>
    public bool GenerateDocumentation { get; set; }
}

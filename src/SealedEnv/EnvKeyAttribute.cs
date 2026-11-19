namespace SealedEnv;

/// <summary>
/// Declares a primary-constructor parameter as an environment-variable binding.
/// Without an explicit key the env var name is derived from the parameter name
/// (<c>DbConnection</c> → <c>DBCONNECTION</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class EnvKeyAttribute : Attribute
{
    public EnvKeyAttribute() { }

    /// <summary>
    /// Explicitly pins the environment key. Takes precedence over the derived name.
    /// </summary>
    public EnvKeyAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// The explicit environment key, or <see langword="null"/> to derive it from
    /// the parameter name.
    /// </summary>
    public string? Key { get; }

    /// <summary>Fails loading with <see cref="EnvErrorCode.Missing"/> when unset.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// Value used when the variable is unset, stored as a string and parsed by the
    /// same pipeline as env values (a malformed default is a compile-time diagnostic).
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Permits an empty string as a valid value (string-ish members only; an empty
    /// value still has to parse for typed members). Only meaningful when the type
    /// opts out of <see cref="SealedEnvAttribute.EmptyAsUnset"/>, or combined with it
    /// to let an explicit empty through for this member.
    /// </summary>
    public bool AllowEmpty { get; set; }

    /// <summary>Separator for list-valued members (default <c>','</c>).</summary>
    public char Separator { get; set; } = ',';

    /// <summary>Human-readable purpose of the variable; consumed by future doc export.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Custom parser type exposing <c>public static T Parse(string)</c>, for parameter
    /// types that do not implement <see cref="IParsable{T}"/>.
    /// </summary>
    public Type? Parser { get; set; }
}

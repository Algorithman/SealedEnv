namespace SealedEnv;

/// <summary>
/// Marks a record/class/struct as an environment-variable configuration type.
/// The source generator emits <c>Load()</c>/<c>TryLoad()</c> members that read
/// every primary-constructor parameter decorated with <see cref="EnvKeyAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class SealedEnvAttribute : Attribute
{
    /// <summary>
    /// Prefix prepended to every resolved key of the type (e.g. <c>"MYAPP_"</c>).
    /// Keys default to <c>SCREAMING_SNAKE_CASE</c> of the parameter name.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Treats an empty environment value as unset (dotenv convention; avoids the
    /// classic <c>PORT=""</c> footgun). <see langword="true"/> by default.
    /// </summary>
    public bool EmptyAsUnset { get; set; } = true;
}

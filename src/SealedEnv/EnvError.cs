namespace SealedEnv;

/// <summary>Why a single environment binding failed.</summary>
public enum EnvErrorCode
{
    /// <summary>The variable is not set and no usable default exists.</summary>
    Missing,

    /// <summary>The variable is set to an empty string where that is not allowed.</summary>
    Empty,

    /// <summary>The value does not parse as the member's type.</summary>
    Invalid,

    /// <summary>Two members resolve to the same key (compile-time only).</summary>
    Duplicate,
}

/// <summary>A single failed environment binding: which key, why, and for which member.</summary>
public sealed record EnvError(string Key, EnvErrorCode Code, string Message, string TypeName);

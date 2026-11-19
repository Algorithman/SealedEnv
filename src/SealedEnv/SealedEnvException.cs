// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SealedEnv
// Filename: SealedEnvException.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 11:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using System.Text;

namespace SealedEnv;

/// <summary>
///     Thrown by generated <c>Load()</c> members when any binding failed.
///     Aggregates <em>all</em> errors of one load — never fails fast — so a single
///     startup reports every bad variable at once.
/// </summary>
public sealed class SealedEnvException : Exception
{
    public SealedEnvException(IReadOnlyList<EnvError> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>Every binding that failed, in member declaration order.</summary>
    public IReadOnlyList<EnvError> Errors { get; }

    private static string FormatMessage(IReadOnlyList<EnvError> errors)
    {
        if (errors is null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one error is required.", nameof(errors));
        }

        var builder = new StringBuilder();
        builder
            .Append("SealedEnv configuration is invalid (")
            .Append(errors.Count)
            .Append(errors.Count == 1 ? " error):" : " errors):");

        foreach (var error in errors)
        {
            builder
                .AppendLine()
                .Append("  - ")
                .Append(error.Key)
                .Append(" [").Append(error.Code).Append(']')
                .Append(" (").Append(error.TypeName).Append(") ")
                .Append(error.Message);
        }

        return builder.ToString();
    }
}

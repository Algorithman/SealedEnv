using System.Text;

namespace SealedEnv.Generators;

/// <summary>
/// Derives the environment key from a parameter name.
/// </summary>
/// <remarks>
/// Pinned rule (the plan's §4 "decide and pin the acronym rule" question): underscores
/// are inserted ONLY at acronym-to-word boundaries — an uppercase letter that follows
/// an uppercase letter and precedes a lowercase one ("APIKey" → <c>API_KEY</c>,
/// "HTTPServer" → <c>HTTP_SERVER</c>). Everything else is uppercased verbatim, matching
/// the plan's own examples: "DbConnection" → <c>DBCONNECTION</c>, "DbUrl" → <c>DBURL</c>,
/// "ID" → <c>ID</c>, "Port" → <c>PORT</c>, "_port" → <c>PORT</c>. Leading underscores are
/// stripped (the common <c>_field</c> convention); all other characters pass through
/// unchanged, so "myKey" → <c>MYKEY</c>.
/// </remarks>
internal static class EnvKeyDerivation
{
    public static string Derive(string parameterName)
    {
        var start = 0;
        while (start < parameterName.Length && parameterName[start] == '_')
        {
            start++;
        }

        var name = parameterName.Substring(start);
        if (name.Length == 0)
        {
            return parameterName.ToUpperInvariant();
        }

        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current)
                && i > 0
                && char.IsUpper(name[i - 1])
                && i + 1 < name.Length
                && char.IsLower(name[i + 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }
}

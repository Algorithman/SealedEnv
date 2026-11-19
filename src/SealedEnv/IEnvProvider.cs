namespace SealedEnv;

/// <summary>
/// Lookup seam for environment variables. Generated <c>Load()</c>/<c>TryLoad()</c>
/// members resolve every key through this interface, so tests can substitute an
/// in-memory <see cref="DictionaryEnvProvider"/> instead of touching process state.
/// </summary>
public interface IEnvProvider
{
    /// <summary>
    /// Returns the value of <paramref name="key"/>, or <see langword="null"/> when unset.
    /// </summary>
    string? Get(string key);
}

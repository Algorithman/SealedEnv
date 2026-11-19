namespace SealedEnv.Tests.Loader;

/// <summary>
/// Harness around the real <see cref="SealedEnvLoader"/> signatures: one dictionary
/// provider, one shared error sink, thin typed wrappers with named default arguments
/// so tests read as value-in → value-out. Every wrapper passes all arguments
/// explicitly, so any signature drift fails compilation.
/// </summary>
internal sealed class TestEnv
{
    private readonly Dictionary<string, string?> _vars = [];
    private List<EnvError> _errors = []; // not readonly: the loader takes the sink by ref

    private TestEnv() { }

    public static TestEnv Empty() => new();

    public static TestEnv With(string key, string? value) => new TestEnv().Set(key, value);

    public TestEnv Set(string key, string? value)
    {
        _vars[key] = value;
        return this;
    }

    public List<EnvError> Errors => _errors;

    private IEnvProvider Provider => new DictionaryEnvProvider(_vars);

    /// <summary>The single recorded error, with a useful message when there are more.</summary>
    public EnvError SingleError
    {
        get
        {
            Assert.True(
                _errors.Count == 1,
                $"expected exactly one error, got {_errors.Count}: {string.Join("; ", _errors)}");
            return _errors[0];
        }
    }

    private static string Display(string key) => $"TestConfig.{key}";

    public int Int(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.Int(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public long Long(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.Long(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public double Double(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.Double(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public bool Bool(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.Bool(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public string String(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.String(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public string? NullableString(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.NullableString(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public TimeSpan TimeSpan(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.TimeSpan(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public Uri? Uri(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.Uri(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public Guid Guid(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true) =>
        SealedEnvLoader.Guid(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public T Enum<T>(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true)
        where T : struct, Enum =>
        SealedEnvLoader.Enum<T>(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public T Parsable<T>(string key, string? @default = null, bool required = false, bool allowEmpty = false, bool emptyAsUnset = true)
        where T : IParsable<T> =>
        SealedEnvLoader.Parsable<T>(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, ref _errors);

    public string[] StringArray(
        string key,
        char separator = ',',
        string? @default = null,
        bool required = false,
        bool allowEmpty = false,
        bool emptyAsUnset = true) =>
        SealedEnvLoader.StringArray(Provider, key, Display(key), @default, required, allowEmpty, emptyAsUnset, separator, ref _errors);
}

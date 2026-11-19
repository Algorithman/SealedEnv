namespace SealedEnv.Tests;

/// <summary>
/// The provider seam: <see cref="DictionaryEnvProvider"/> must be able to express
/// all three environment states a binding can encounter (absent, present,
/// present-but-empty, present-but-null), and <see cref="EnvironmentEnvProvider"/>
/// must be a thin pass-through to process state.
/// </summary>
public class ProvidersTests
{
    [Fact]
    public void Dictionary_provider_distinguishes_absent_from_present_but_empty()
    {
        var provider = new DictionaryEnvProvider(new Dictionary<string, string?>
        {
            ["PRESENT"] = "value",
            ["EMPTY"] = "",
            ["NULL"] = null,
        });

        Assert.Equal("value", provider.Get("PRESENT"));
        Assert.Equal(string.Empty, provider.Get("EMPTY")); // present-empty, NOT absent
        Assert.Null(provider.Get("NULL")); // explicit null ≡ absent
        Assert.Null(provider.Get("ABSENT"));
    }

    [Fact]
    public void Dictionary_provider_lookups_are_ordinal_and_case_sensitive()
    {
        // Real env vars are case-sensitive on Linux and case-insensitive on Windows.
        // The test seam pins case-sensitivity so tests behave identically everywhere.
        var provider = new DictionaryEnvProvider(new Dictionary<string, string?> { ["MYAPP_PORT"] = "8080" });

        Assert.Equal("8080", provider.Get("MYAPP_PORT"));
        Assert.Null(provider.Get("myapp_port"));
        Assert.Null(provider.Get("MyApp_Port"));
    }

    [Fact]
    public void Dictionary_provider_works_from_the_empty_state()
    {
        var provider = new DictionaryEnvProvider(new Dictionary<string, string?>());

        Assert.Null(provider.Get("ANYTHING"));
    }

    [Fact]
    public void Environment_provider_reads_process_state()
    {
        const string key = "SEALENVTESTS_PROVIDER_PROBE";
        try
        {
            Environment.SetEnvironmentVariable(key, "probe-value");
            Assert.Equal("probe-value", EnvironmentEnvProvider.Instance.Get(key));

            Environment.SetEnvironmentVariable(key, null);
            Assert.Null(EnvironmentEnvProvider.Instance.Get(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Environment_provider_instance_is_a_stable_singleton()
    {
        Assert.Same(EnvironmentEnvProvider.Instance, EnvironmentEnvProvider.Instance);
    }

    [Fact]
    public void Providers_interchange_behind_the_seam()
    {
        // The whole point of IEnvProvider: identical lookups, swappable backends.
        IEnvProvider fromEnvironment = EnvironmentEnvProvider.Instance;
        IEnvProvider fromDictionary = new DictionaryEnvProvider(
            new Dictionary<string, string?> { ["SEALENVTESTS_PROVIDER_PROBE"] = "same" });

        try
        {
            Environment.SetEnvironmentVariable("SEALENVTESTS_PROVIDER_PROBE", "same");

            Assert.Equal(fromEnvironment.Get("SEALENVTESTS_PROVIDER_PROBE"), fromDictionary.Get("SEALENVTESTS_PROVIDER_PROBE"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEALENVTESTS_PROVIDER_PROBE", null);
        }
    }
}

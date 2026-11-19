using System.Reflection;
using SealedEnv.Tests.Loader;

namespace SealedEnv.Tests;

/// <summary>
/// The attribute surface is a frozen contract (SealedEnv-PLAN.md §1): the generator
/// parses these classes, so any accidental signature change must break here first.
/// The carrier record at the bottom compiles against the whole surface — change a
/// property name or default and this file stops building.
/// </summary>
public class AttributeSurfaceTests
{
    [Fact]
    public void SealedEnv_defaults()
    {
        var attribute = new SealedEnvAttribute();

        Assert.Null(attribute.Prefix);
        Assert.True(attribute.EmptyAsUnset); // the PORT="" footgun is closed by default
    }

    [Fact]
    public void SealedEnv_properties_are_settable()
    {
        var attribute = new SealedEnvAttribute { Prefix = "MYAPP_", EmptyAsUnset = false };

        Assert.Equal("MYAPP_", attribute.Prefix);
        Assert.False(attribute.EmptyAsUnset);
    }

    [Fact]
    public void SealedEnv_targets_classes_and_structs_only()
    {
        var usage = typeof(SealedEnvAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void EnvKey_defaults()
    {
        var attribute = new EnvKeyAttribute();

        Assert.Null(attribute.Key); // no explicit key → derived from the parameter name
        Assert.False(attribute.Required);
        Assert.Null(attribute.Default);
        Assert.False(attribute.AllowEmpty);
        Assert.Equal(',', attribute.Separator);
        Assert.Null(attribute.Description);
        Assert.Null(attribute.Parser);
    }

    [Fact]
    public void EnvKey_explicit_key_constructor_pins_the_key()
    {
        var attribute = new EnvKeyAttribute("MYAPP_PORT");

        Assert.Equal("MYAPP_PORT", attribute.Key);
    }

    [Fact]
    public void EnvKey_every_property_is_assignable()
    {
        var attribute = new EnvKeyAttribute("MYAPP_PORT")
        {
            Required = true,
            Default = "8080",
            AllowEmpty = true,
            Separator = ';',
            Description = "TCP port to listen on",
            Parser = typeof(Port),
        };

        Assert.True(attribute.Required);
        Assert.Equal("8080", attribute.Default);
        Assert.True(attribute.AllowEmpty);
        Assert.Equal(';', attribute.Separator);
        Assert.Equal("TCP port to listen on", attribute.Description);
        Assert.Equal(typeof(Port), attribute.Parser);
    }

    [Fact]
    public void EnvKey_targets_parameters_only()
    {
        var usage = typeof(EnvKeyAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Parameter, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void The_frozen_surface_compiles_end_to_end_on_a_carrier()
    {
        // Compilation of this test project is the assertion: every named argument
        // below exists with this exact name, type and meaning.
        var carrier = new FrozenSurfaceCarrier(8080, "db", LogLevel.Info, ["a"]);

        Assert.Equal(8080, carrier.Port);
        Assert.Equal("db", carrier.DbConnection);
        Assert.Equal(LogLevel.Info, carrier.Level);
        Assert.Equal(["a"], carrier.Hosts);
    }
}

// Top-level on purpose: nested [SealedEnv] carriers are not supported yet (SVE0100).
// No Parser= here: that route now generates a real Custom<T> call, and Port's Parse
// is explicit-interface-only; the Parser property is pinned via EnvKey_every_property_is_assignable.
[SealedEnv(Prefix = "FROZEN_")]
internal sealed partial record FrozenSurfaceCarrier(
    [EnvKey("FROZEN_PORT", Default = "1", Required = false, AllowEmpty = false, Separator = ',', Description = "port")]
    int Port,
    [EnvKey(Required = true)]
    string DbConnection,
    [EnvKey]
    LogLevel Level,
    [EnvKey]
    string[] Hosts);

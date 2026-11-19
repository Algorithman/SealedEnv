using SealedEnv.Generators;

namespace SealedEnv.Generator.Tests;

/// <summary>
/// Pins the key-derivation rule decided in Phase 2: underscores ONLY at
/// acronym-to-word boundaries, everything else uppercased verbatim — matching the
/// plan's own examples (DbConnection → DBCONNECTION, DbUrl → DBURL, ID → ID).
/// </summary>
public class KeyDerivationTests
{
    [Theory]
    [InlineData("Port", "PORT")]
    [InlineData("DbConnection", "DBCONNECTION")] // plan §1 example: no word boundaries
    [InlineData("DbUrl", "DBURL")] // plan §4 example: Db is not treated as a word
    [InlineData("ID", "ID")] // plan §4 example: all-caps stays whole
    [InlineData("ShutdownTimeout", "SHUTDOWNTIMEOUT")]
    [InlineData("APIKey", "API_KEY")] // acronym followed by a word gets the boundary
    [InlineData("HTTPServer", "HTTP_SERVER")]
    [InlineData("IDCard", "ID_CARD")]
    [InlineData("_port", "PORT")] // leading underscore (private-field convention) stripped
    [InlineData("__connection", "CONNECTION")]
    [InlineData("port", "PORT")] // lowercase input is uppercased
    [InlineData("myKey", "MYKEY")] // no acronym boundary → verbatim uppercase
    [InlineData("Db2Host", "DB2HOST")] // digits do not create boundaries
    public void Derives_keys_per_the_pinned_rule(string parameterName, string expected)
    {
        Assert.Equal(expected, EnvKeyDerivation.Derive(parameterName));
    }
}

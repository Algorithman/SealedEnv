namespace SealedEnv.Tests;

/// <summary>
/// The aggregate exception is the user-facing surface of a failed load: one line
/// per error, key + code + type + cause, and the full list preserved on the object.
/// </summary>
public class SealedEnvExceptionTests
{
    private static readonly EnvError InvalidPort =
        new("MYAPP_PORT", EnvErrorCode.Invalid, "Environment variable 'MYAPP_PORT' has value \"abc\".", "int");

    private static readonly EnvError MissingDb =
        new("MYAPP_DBCONNECTION", EnvErrorCode.Missing, "Environment variable 'MYAPP_DBCONNECTION' is not set.", "string");

    [Fact]
    public void Errors_are_preserved_by_reference_in_order()
    {
        var errors = new List<EnvError> { InvalidPort, MissingDb };

        var exception = new SealedEnvException(errors);

        Assert.Same(errors, exception.Errors);
    }

    [Fact]
    public void Message_has_one_line_per_error_with_key_code_and_type()
    {
        var exception = new SealedEnvException([InvalidPort, MissingDb]);

        Assert.StartsWith("SealedEnv configuration is invalid (2 errors):", exception.Message);
        Assert.Contains("  - MYAPP_PORT [Invalid] (int) ", exception.Message);
        Assert.Contains("  - MYAPP_DBCONNECTION [Missing] (string) ", exception.Message);
        Assert.Contains("has value \"abc\"", exception.Message);
    }

    [Fact]
    public void A_single_error_is_reported_in_the_singular()
    {
        var exception = new SealedEnvException([InvalidPort]);

        Assert.Contains("(1 error):", exception.Message);
        Assert.DoesNotContain("errors", exception.Message);
    }

    [Fact]
    public void Error_records_are_value_comparable()
    {
        // Records: two errors with the same payload are interchangeable, which keeps
        // generator snapshot tests and future golden-error fixtures simple.
        Assert.Equal(InvalidPort, new EnvError("MYAPP_PORT", EnvErrorCode.Invalid, InvalidPort.Message, "int"));
        Assert.NotEqual(InvalidPort, MissingDb);

        var (key, code, message, typeName) = InvalidPort;
        Assert.Equal("MYAPP_PORT", key);
        Assert.Equal(EnvErrorCode.Invalid, code);
        Assert.Equal("int", typeName);
    }

    [Fact]
    public void An_empty_error_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new SealedEnvException(new List<EnvError>()));
    }

    [Fact]
    public void A_null_error_list_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SealedEnvException(null!));
    }
}

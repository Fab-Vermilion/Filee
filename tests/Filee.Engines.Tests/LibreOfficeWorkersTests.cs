using Filee.Engines.Office;

namespace Filee.Engines.Tests;

public sealed class LibreOfficeWorkersTests
{
    [Fact]
    public void ReadWorkers_DefaultsToTwoWhenEnvUnsetOrInvalid()
    {
        var previous = Environment.GetEnvironmentVariable("FILEE_LO_WORKERS");
        try
        {
            Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", null);
            Assert.Equal(2, LibreOfficeConverter.ReadWorkers());
            Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", "not-a-number");
            Assert.Equal(2, LibreOfficeConverter.ReadWorkers());
            Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", "99");
            Assert.Equal(2, LibreOfficeConverter.ReadWorkers());
        }
        finally { Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", previous); }
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("4", 4)]
    public void ReadWorkers_HonoursValidOverride(string value, int expected)
    {
        var previous = Environment.GetEnvironmentVariable("FILEE_LO_WORKERS");
        try
        {
            Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", value);
            Assert.Equal(expected, LibreOfficeConverter.ReadWorkers());
        }
        finally { Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", previous); }
    }
}

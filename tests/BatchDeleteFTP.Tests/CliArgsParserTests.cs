using BatchDeleteFTP.Helpers;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class CliArgsParserTests
{
    [Fact]
    public void Parse_EmptyArgs_RunsGuiMode()
    {
        var opts = CliOptions.Parse(System.Array.Empty<string>());
        Assert.False(opts.IsConsoleMode);
        Assert.False(opts.IsPreviewOnly);
    }

    [Theory]
    [InlineData("--auto")]
    [InlineData("--console")]
    [InlineData("/auto")]
    public void Parse_AutoFlag_EnablesConsoleExecutionMode(string arg)
    {
        var opts = CliOptions.Parse(new[] { arg });
        Assert.True(opts.IsConsoleMode);
        Assert.False(opts.IsPreviewOnly);
    }

    [Fact]
    public void Parse_PreviewFlag_EnablesConsolePreviewMode()
    {
        var opts = CliOptions.Parse(new[] { "--preview" });
        Assert.True(opts.IsConsoleMode);
        Assert.True(opts.IsPreviewOnly);
    }
}

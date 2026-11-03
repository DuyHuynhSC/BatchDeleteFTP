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
    [Theory]
    [InlineData("--all")]
    [InlineData("--all-profiles")]
    [InlineData("/all")]
    [InlineData("/all-profiles")]
    public void Parse_AllProfilesFlag_EnablesIsAllProfiles(string arg)
    {
        var opts = CliOptions.Parse(new[] { "--preview", arg });
        Assert.True(opts.IsConsoleMode);
        Assert.True(opts.IsPreviewOnly);
        Assert.True(opts.IsAllProfiles);
    }

    [Theory]
    [InlineData("--send-email")]
    [InlineData("--email")]
    [InlineData("/send-email")]
    public void Parse_SendEmailFlag_EnablesSendEmailIfCandidatesFound(string arg)
    {
        var opts = CliOptions.Parse(new[] { "--preview", "--all", arg });
        Assert.True(opts.IsConsoleMode);
        Assert.True(opts.IsPreviewOnly);
        Assert.True(opts.IsAllProfiles);
        Assert.True(opts.SendEmailIfCandidatesFound);
    }
}
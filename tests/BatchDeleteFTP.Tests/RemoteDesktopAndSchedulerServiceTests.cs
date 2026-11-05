using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class RemoteDesktopAndSchedulerServiceTests
{
    [Theory]
    [InlineData("Administrator", "FUJINET.vn", "FUJINET.vn\\Administrator")]
    [InlineData("User01", "WORKGROUP", "User01")]
    [InlineData("User01", "", "User01")]
    [InlineData("CORP\\Admin", "FUJINET.vn", "CORP\\Admin")]
    [InlineData("admin@domain.com", "FUJINET.vn", "admin@domain.com")]
    public void FormatUsername_FormatsProperlyForDomainAndWorkgroup(string user, string domain, string expected)
    {
        string actual = RemoteDesktopService.FormatUsername(user, domain);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildCliCommand_IncludesAllFlagsCorrectly()
    {
        string cmd = TaskSchedulerService.BuildCliCommand("--auto", allProfiles: true, sendEmail: true);
        Assert.Contains("--auto", cmd);
        Assert.Contains("--all", cmd);
        Assert.Contains("--send-email", cmd);
    }

    [Fact]
    public void BuildSchtasksCreateCommand_GeneratesValidSchtasksSyntax()
    {
        string schtasksCmd = TaskSchedulerService.BuildSchtasksCreateCommand("MyTask", "23:00", "--preview", allProfiles: true, sendEmail: false);
        Assert.StartsWith("schtasks /create /tn \"MyTask\"", schtasksCmd);
        Assert.Contains("/sc daily /st 23:00 /f", schtasksCmd);
        Assert.Contains("--preview", schtasksCmd);
        Assert.Contains("--all", schtasksCmd);
        Assert.DoesNotContain("--send-email", schtasksCmd);
    }
}

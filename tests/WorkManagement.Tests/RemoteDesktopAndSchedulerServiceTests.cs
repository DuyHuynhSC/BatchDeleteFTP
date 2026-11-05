using WorkManagement.Services;
using Xunit;

namespace WorkManagement.Tests;

public class RemoteDesktopAndSchedulerServiceTests
{
    [Theory]
    [InlineData("Administrator", "D2-SVR15-6016", "D2-SVR15-6016\\Administrator")]
    [InlineData("User01", "D2-VM-7023", "D2-VM-7023\\User01")]
    [InlineData("User01", "", "User01")]
    [InlineData("D2-SVR\\Admin", "D2-SVR", "D2-SVR\\Admin")]
    [InlineData("admin@domain.com", "D2-SVR", "admin@domain.com")]
    public void FormatUsername_FormatsProperlyForMachineNameAndUser(string user, string machineName, string expected)
    {
        string actual = RemoteDesktopService.FormatUsername(user, machineName);
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

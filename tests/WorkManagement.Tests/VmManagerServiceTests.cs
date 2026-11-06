using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorkManagement.Models;
using WorkManagement.Services;
using Xunit;

namespace WorkManagement.Tests;

public class VmManagerServiceTests
{
    [Fact]
    public void LoadAll_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid()}.json");
        try
        {
            var service = new VmManagerService(tempFile);
            var list = service.LoadAll();

            Assert.NotNull(list);
            Assert.Empty(list);
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SaveAll_And_LoadAll_PreservesAllVmFields()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid()}.json");
        try
        {
            var service = new VmManagerService(tempFile);
            var testList = new List<VirtualMachineEntry>
            {
                new()
                {
                    No = "99",
                    Ip = "192.168.1.100",
                    MachineName = "TEST-SRV",
                    DomainOrWorkgroup = "CORP.LOCAL",
                    Os = "Windows Server 2022",
                    Username = "AdminUser",
                    Password = "SecretPassword123!",
                    Tns = "TEST_TNS",
                    DatabaseName = "TEST_DB",
                    OracleVersion = "19c",
                    Tester = "Tester1",
                    Notes = "Ghi chú kiểm thử",
                    ExpirationDate = "2030/01/01"
                }
            };

            service.SaveAll(testList);

            var reloaded = service.LoadAll();
            Assert.Single(reloaded);
            var item = reloaded[0];
            Assert.Equal("99", item.No);
            Assert.Equal("192.168.1.100", item.Ip);
            Assert.Equal("TEST-SRV", item.MachineName);
            Assert.Equal("CORP.LOCAL", item.DomainOrWorkgroup);
            Assert.Equal("Windows Server 2022", item.Os);
            Assert.Equal("AdminUser", item.Username);
            Assert.Equal("SecretPassword123!", item.Password);
            Assert.Equal("TEST_TNS", item.Tns);
            Assert.Equal("TEST_DB", item.DatabaseName);
            Assert.Equal("19c", item.OracleVersion);
            Assert.Equal("Tester1", item.Tester);
            Assert.Equal("Ghi chú kiểm thử", item.Notes);
            Assert.Equal("2030/01/01", item.ExpirationDate);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ClearAll_EmptiesList()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"vm_clear_test_{Guid.NewGuid()}.json");
        try
        {
            var service = new VmManagerService(tempFile);
            var testList = new List<VirtualMachineEntry>
            {
                new() { Ip = "10.0.0.1" }
            };
            service.SaveAll(testList);
            Assert.NotEmpty(service.LoadAll());

            service.ClearAll();

            var afterClear = service.LoadAll();
            Assert.Empty(afterClear);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void IsSelected_DefaultFalse_CanBeToggled_AndClonePreservesIt()
    {
        var entry = new VirtualMachineEntry { Ip = "10.0.0.1", IsSelected = false };
        Assert.False(entry.IsSelected);

        entry.IsSelected = true;
        Assert.True(entry.IsSelected);

        var cloned = entry.Clone();
        Assert.True(cloned.IsSelected);
        Assert.Equal("10.0.0.1", cloned.Ip);
    }

    [Fact]
    public void SaveAll_IgnoresIsSelected_WhenSavedToJson()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"vm_json_test_{Guid.NewGuid()}.json");
        try
        {
            var service = new VmManagerService(tempFile);
            var testList = new List<VirtualMachineEntry>
            {
                new() { Ip = "192.168.1.50", IsSelected = true }
            };

            service.SaveAll(testList);

            string json = File.ReadAllText(tempFile);
            Assert.DoesNotContain("\"IsSelected\"", json);

            var reloaded = service.LoadAll();
            Assert.Single(reloaded);
            Assert.False(reloaded[0].IsSelected);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

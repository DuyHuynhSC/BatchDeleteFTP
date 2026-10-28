using System;
using BatchDeleteFTP.Helpers;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class DateFilterRuleTests
{
    [Fact]
    public void ShouldDelete_WhenSystemDateIsGreaterThanUploadDatePlusOne_ReturnsTrue()
    {
        var uploadDate = new DateTime(2026, 10, 26, 23, 50, 0);
        var systemDate = new DateTime(2026, 10, 28, 08, 15, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.True(result);
    }

    [Fact]
    public void ShouldDelete_WhenSystemDateEqualsUploadDatePlusOne_ReturnsFalse()
    {
        var uploadDate = new DateTime(2026, 10, 27, 01, 00, 0);
        var systemDate = new DateTime(2026, 10, 28, 22, 00, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.False(result);
    }

    [Fact]
    public void ShouldDelete_WhenUploadedToday_ReturnsFalse()
    {
        var uploadDate = new DateTime(2026, 10, 28, 10, 00, 0);
        var systemDate = new DateTime(2026, 10, 28, 15, 00, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.False(result);
    }
}

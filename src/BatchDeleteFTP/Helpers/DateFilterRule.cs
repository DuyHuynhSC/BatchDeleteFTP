using System;

namespace BatchDeleteFTP.Helpers;

public static class DateFilterRule
{
    public static bool ShouldDelete(DateTime fileModifiedDate, DateTime currentSystemDate, int retentionDays = 1)
    {
        return currentSystemDate.Date > fileModifiedDate.Date.AddDays(retentionDays);
    }
}

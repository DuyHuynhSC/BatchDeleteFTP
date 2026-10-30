using System;
using System.Collections.Generic;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class EmailNotificationServiceTests
{
    [Fact]
    public void ParseRecipientEmails_SplitsMultiDelimitersAndDeduplicates()
    {
        string raw = " alice@company.com ; bob@company.com, charlie@company.com \r\n ALICE@company.com ; invalid-email ; dave@sub.domain.vn ";
        var parsed = EmailNotificationService.ParseRecipientEmails(raw);

        Assert.Equal(4, parsed.Count);
        Assert.Equal(new[]
        {
            "alice@company.com",
            "bob@company.com",
            "charlie@company.com",
            "dave@sub.domain.vn"
        }, parsed);
    }

    [Fact]
    public void BuildPreviewHtmlBody_ContainsProfileAndCandidateDetails()
    {
        var logger = new AppLogger();
        var service = new EmailNotificationService(logger);
        var settings = new FtpSettings
        {
            SelectedProfileName = "PROD_SITE",
            Host = "ftp.example.com",
            Port = 6021,
            RetentionDays = 7,
            RemotePaths = new List<string> { "/Data/In", "/Data/Out" }
        };

        var candidates = new List<FtpFileCandidate>
        {
            new()
            {
                FullPath = "/Data/In/order_01.csv",
                FileName = "order_01.csv",
                ModifiedDate = new DateTime(2026, 10, 15, 8, 30, 0),
                Size = 2048,
                Status = "Chờ xử lý",
                IsSelected = true
            },
            new()
            {
                FullPath = "/Data/Out/ignored.csv",
                FileName = "ignored.csv",
                ModifiedDate = new DateTime(2026, 10, 16, 9, 0, 0),
                Size = 1024,
                Status = "Bỏ qua",
                IsSelected = false
            }
        };

        string html = service.BuildPreviewHtmlBody(settings, candidates, new DateTime(2026, 10, 30, 11, 30, 0));

        Assert.Contains("PROD_SITE", html);
        Assert.Contains("ftp.example.com:6021", html);
        Assert.Contains("/Data/In/order_01.csv", html);
        Assert.Contains("2,048", html);
        Assert.DoesNotContain("/Data/Out/ignored.csv", html);
    }

    [Fact]
    public void ResolveSenderEmail_InfersDomainFromSmtpHostWhenUserHasNoDomain()
    {
        var settings = new FtpSettings
        {
            SmtpHost = "mail.fujinet.net",
            SmtpUser = "duy-hd",
            SenderEmail = ""
        };

        string resolved = EmailNotificationService.ResolveSenderEmail(settings);
        Assert.Equal("duy-hd@fujinet.net", resolved);
    }
}

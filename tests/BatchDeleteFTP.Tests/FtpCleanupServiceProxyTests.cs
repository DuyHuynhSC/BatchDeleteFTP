using System;
using System.Net;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using FluentFTP;
using FluentFTP.Proxy.AsyncProxy;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class FtpCleanupServiceProxyTests
{
    [Fact]
    public void CreateClient_WhenUseProxyIsTrue_ReturnsHttp11ProxyClient()
    {
        var logger = new AppLogger();
        var service = new FtpCleanupService(logger);

        var settings = new FtpSettings
        {
            Host = "ftp.fujinet.net",
            Port = 6021,
            Username = "UCD-MOROZOFF",
            Password = "test",
            UseProxy = true,
            ProxyHost = "prox2.fujinet.vn",
            ProxyPort = 8080,
            ProxyUser = "user1",
            ProxyPassword = "pass1"
        };

        using var client = service.CreateClient(settings);

        Assert.IsType<AsyncFtpClientHttp11Proxy>(client);
        Assert.Equal(FtpEncryptionMode.Implicit, client.Config.EncryptionMode);
        Assert.True(client.Config.ValidateAnyCertificate);
    }

    [Fact]
    public void CreateClient_WhenUseProxyIsFalse_ReturnsStandardAsyncFtpClient()
    {
        var logger = new AppLogger();
        var service = new FtpCleanupService(logger);

        var settings = new FtpSettings
        {
            Host = "ftp.fujinet.net",
            Port = 6021,
            Username = "UCD-MOROZOFF",
            Password = "test",
            UseProxy = false
        };

        using var client = service.CreateClient(settings);

        Assert.IsNotType<AsyncFtpClientHttp11Proxy>(client);
        Assert.Equal(FtpEncryptionMode.Implicit, client.Config.EncryptionMode);
    }
}

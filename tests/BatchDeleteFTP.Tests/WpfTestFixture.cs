using System;
using System.Windows;
using Xunit;

namespace BatchDeleteFTP.Tests;

[CollectionDefinition("WpfApp", DisableParallelization = true)]
public class WpfAppCollection
{
}

public static class WpfTestHelper
{
    private static readonly object AppLock = new();

    public static void EnsureApplication()
    {
        lock (AppLock)
        {
            if (Application.Current == null)
            {
                try
                {
                    var app = new App();
                    app.InitializeComponent();
                }
                catch (InvalidOperationException)
                {
                    // Ignore if another thread initialized it concurrently
                }
            }
        }
    }
}

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Testing.AspNetCore;

/// <summary>
/// Provides a mutable in-memory host environment for ASP.NET Core tests.
/// </summary>
public sealed class TestHostEnvironment(string applicationName) : IHostEnvironment
{
    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string EnvironmentName { get; set; } = Environments.Production;

    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string ApplicationName { get; set; } = applicationName;

    /// <summary>
    /// Gets or sets the content root path.
    /// </summary>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// Gets or sets the content root file provider.
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

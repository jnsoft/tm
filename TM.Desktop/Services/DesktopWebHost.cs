using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TM.Desktop.ViewModels;
using TM.Services;

namespace TM.Desktop.Services;

public sealed class DesktopWebHost : IAsyncDisposable
{
    private readonly WebApplication application;
    public DesktopSessionSecurity Security { get; }

    private DesktopWebHost(WebApplication application, DesktopSessionSecurity security)
    {
        this.application = application;
        Security = security;
    }

    public static async Task<DesktopWebHost> StartAsync(string contentRoot, CancellationToken cancellationToken = default,
        IProjectFileDialogs? dialogs = null, INativeClipboard? clipboard = null, IFileToolDialogs? fileDialogs = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DesktopWebHost).Assembly.FullName,
            ContentRootPath = contentRoot,
            EnvironmentName = "Production"
        });
        // Bootstrap credentials and document paths must not enter HTTP request logs.
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.Limits.MaxRequestBodySize = 1024 * 1024;
            options.AddServerHeader = false;
        });
        builder.Services.AddRazorPages().AddMvcOptions(options =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "tm-antiforgery-" + Guid.NewGuid().ToString("N");
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
        });
        builder.Services.AddSingleton<DesktopSessionSecurity>();
        builder.Services.AddSingleton<ProjectCryptoService>();
        builder.Services.AddSingleton<ProjectStore>();
        builder.Services.AddSingleton<ShellViewModel>();
        IProjectFileDialogs projectDialogs = dialogs ?? new PhotinoProjectFileDialogs();
        builder.Services.AddSingleton(projectDialogs);
        builder.Services.AddSingleton<IFileToolDialogs>(fileDialogs ?? projectDialogs as IFileToolDialogs ?? new PhotinoProjectFileDialogs());
        builder.Services.AddSingleton<FileUtilityService>();
        builder.Services.AddSingleton<PasswordFileService>();
        builder.Services.AddSingleton<DocumentFileService>();
        builder.Services.AddSingleton<DocumentHmacService>();
        builder.Services.AddSingleton<PublicKeyFileService>();
        builder.Services.AddSingleton<DocumentSignatureService>();
        builder.Services.AddSingleton<DocumentCertificateService>();
        builder.Services.AddSingleton<DocumentTransferService>();
        builder.Services.AddSingleton<FileToolsService>();
        builder.Services.AddSingleton<INativeClipboard>(clipboard ?? CreateClipboard());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ExpiringClipboardService>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ExpiringClipboardService>());
        builder.Services.AddSingleton<WorkspaceService>();
        WebApplication app = builder.Build();
        DesktopSessionSecurity security = app.Services.GetRequiredService<DesktopSessionSecurity>();
        app.Use((context, next) => security.InvokeAsync(context, next));
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("The operation could not be completed.");
        }));
        app.UseStaticFiles();
        app.MapRazorPages();
        try
        {
            await app.StartAsync(cancellationToken);
            IServer server = app.Services.GetRequiredService<IServer>();
            string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("The local server did not bind an address.");
            security.SetOrigin(new Uri(address));
            return new DesktopWebHost(app, security);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        using CancellationTokenSource shutdown = new(TimeSpan.FromSeconds(5));
        try { await application.StopAsync(shutdown.Token); }
        finally { await application.DisposeAsync(); }
    }

    private static INativeClipboard CreateClipboard() => OperatingSystem.IsWindows()
        ? new WindowsNativeClipboard()
        : new UnsupportedNativeClipboard(OperatingSystem.IsMacOS() ? "macOS" : "Linux");
}

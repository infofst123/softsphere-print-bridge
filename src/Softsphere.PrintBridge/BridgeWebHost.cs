using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Softsphere.PrintBridge;

public static class BridgeWebHost
{
    public static WebApplication Build(SettingsStore settingsStore, PrinterService printerService)
    {
        var builder = WebApplication.CreateSlimBuilder();
        var certificate = LocalCertificateManager.GetOrCreate(settingsStore);
        builder.WebHost.ConfigureKestrel(options =>
        {
            // HTTPS is required because the public Odoo website is also served over HTTPS.
            options.ListenLocalhost(
                settingsStore.Current.Port,
                listenOptions => listenOptions.UseHttps(certificate)
            );
        });
        builder.Services.AddSingleton(settingsStore);
        builder.Services.AddSingleton(printerService);

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-API-Key";
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
                context.Response.Headers["Vary"] = "Origin";
            }
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            if (context.Request.Path.Equals("/api/health", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var suppliedKey = context.Request.Headers["X-API-Key"].ToString();
            if (!KeysMatch(suppliedKey, settingsStore.Current.ApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Invalid API key." });
                return;
            }
            await next();
        });

        app.MapGet("/api/health", () => Results.Ok(new
        {
            success = true,
            service = "Softsphere Print Bridge",
            version = typeof(BridgeWebHost).Assembly.GetName().Version?.ToString(),
        }));
        app.MapGet("/api/printers", () => Results.Ok(printerService.GetInstalledPrinters()));
        app.MapPost("/api/print-ticket", (PrintTicketRequest request) =>
        {
            if (request.Ticket is null && string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new PrintOperationResult(false, "Ticket content is required."));
            }
            var result = printerService.PrintTicket(request);
            return result.Success ? Results.Ok(result) : Results.Problem(result.Message, statusCode: 500);
        });
        app.MapPost("/api/open-cash-drawer", () =>
        {
            var result = printerService.OpenCashDrawer();
            return result.Success ? Results.Ok(result) : Results.Problem(result.Message, statusCode: 500);
        });
        return app;
    }

    private static bool KeysMatch(string supplied, string configured)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
        var configuredBytes = Encoding.UTF8.GetBytes(configured ?? string.Empty);
        return suppliedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}

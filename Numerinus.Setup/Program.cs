// ═══════════════════════════════════════════════════════════════════════════════
// Numerinus.Setup — client onboarding reference project
//
// PURPOSE
//   This file shows exactly how to wire Numerinus into any .NET Generic Host
//   application (console, ASP.NET Core, Worker Service, etc.).
//
// QUICK START
//   1. Set your license key (never hard-code it):
//        dotnet user-secrets set "Numerinus:LicenseKey" "NMFIN-..."
//   2. Run:
//        dotnet run
//
// WHAT THIS WIRES UP
//   ┌─ AddNumerinusLicense()  ─ license validation, caching, startup check
//   ├─ AddNumerinusFinance()  ─ all finance epics (TVM, Loan & Interest, Amortization)
//   ├─ NumerinusTelemetry     ─ optional usage telemetry (opt-in, off by default)
//   └─ DemoRunner             ─ calls every epic so you can verify everything works
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Numerinus.Setup;
using Numerinus.Finance;
using Numerinus.Core;
using Numerinus.Core.Telemetry;

// ── Build the Generic Host ────────────────────────────────────────────────────

var host = Host.CreateDefaultBuilder(args)

    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        // appsettings.json ships with safe placeholder values (no secrets).
        // The real LicenseKey arrives via user-secrets (dev) or environment
        // variables / secret manager (CI/production).
        //
        // Override order (highest wins):
        //   appsettings.json  →  appsettings.{env}.json  →  user-secrets  →  env vars
        cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
           .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json",
                        optional: true, reloadOnChange: false)
           .AddEnvironmentVariables()             // Numerinus__LicenseKey=...  works here
           .AddUserSecrets<Program>(optional: true); // dotnet user-secrets (dev only)
    })

    .ConfigureLogging((ctx, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
    })

    .ConfigureServices((ctx, services) =>
    {
        // ── 1. Numerinus license validation ───────────────────────────────────
        //
        // Reads from appsettings.json section "Numerinus".
        // NumerinusLicenseStartupService validates the key at startup and throws
        // NumerinusLicenseException if the key is invalid, expired, or the machine
        // slot is exceeded — issues surface immediately rather than at first use.
        services.AddNumerinusLicense();

        // ── 2. Numerinus.Finance epics ────────────────────────────────────────
        //
        // Registers all three finance epics as singletons behind interfaces:
        //   ITimeValueOfMoney  — PV, FV, NPV, annuities, perpetuities, …
        //   ILoanAndInterest   — EMI, tenure, total cost, compound interest, …
        //   IAmortization      — full schedule, balance at period, span-batch
        //
        // Inject whichever interfaces your own services need. You do not have to
        // use all three.
        services.AddNumerinusFinance();

        // ── 3. Telemetry
        //
        // Telemetry is DISABLED by default (appsettings.json: Enabled = false).
        // To enable, set "NumerinusTelemetry:Enabled": true in appsettings.json
        // or pass the env variable: NumerinusTelemetry__Enabled=true
        //
        // The LicenseKey is shared from the Numerinus section so you only
        // need to set it in one place.
        var telemetryOpts = ctx.Configuration
                               .GetSection("NumerinusTelemetry")
                               .Get<TelemetryOptions>()
                           ?? new TelemetryOptions();

        if (string.IsNullOrWhiteSpace(telemetryOpts.LicenseKey))
            telemetryOpts.LicenseKey = ctx.Configuration["Numerinus:LicenseKey"] ?? string.Empty;

        NumerinusTelemetry.Initialize(telemetryOpts);

        // ── 4. Demo verification (remove this in your own application) ───────
        //
        // DemoRunner calls each finance epic once and logs the results so you
        // can confirm the license and DI wiring work correctly.
        // Delete this line — and DemoRunner.cs — when you integrate Numerinus
        // into your own IHostedService / BackgroundService / controller.
        services.AddHostedService<DemoRunner>();
    })

    .Build();

await host.RunAsync();


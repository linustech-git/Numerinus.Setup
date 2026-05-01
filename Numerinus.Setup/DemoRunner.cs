// ═══════════════════════════════════════════════════════════════════════════════
// DemoRunner — verifies the full Numerinus stack is wired correctly
//
// This IHostedService is injected with the three finance interfaces and makes
// one sample call per epic so you can confirm your license and DI setup work
// end-to-end. Replace or extend this class with your own business logic.
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Numerinus.Finance.Epics.Amortization;
using Numerinus.Finance.Epics.Amortization.Models;
using Numerinus.Finance.Epics.LoanAndInterest;
using Numerinus.Finance.Epics.TimeValueOfMoney;
using Numerinus.Finance.Epics.LoanAndInterest.Models;
using Numerinus.Finance.SharedEnums;
using Numerinus.Finance.Epics.TimeValueOfMoney.Models;

namespace Numerinus.Setup;

/// <summary>
/// Runs once at startup, exercises all three Numerinus.Finance epics, and then
/// signals the host to shut down.  Replace the body with your own logic.
/// </summary>
internal sealed class DemoRunner(
    ITimeValueOfMoney tvm,
    ILoanAndInterest  lai,
    IAmortization     amort,
    ILogger<DemoRunner> logger,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await RunDemoAsync(ct);
        }
        finally
        {
            // Stop the generic host after the demo completes (console app pattern).
            // Remove this line in a long-running service (Worker Service / ASP.NET Core).
            lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    // ── Demo ──────────────────────────────────────────────────────────────────

    private Task RunDemoAsync(CancellationToken ct)
    {
        Banner("Numerinus.Setup — configuration demo");

        // ── 1. Time Value of Money ────────────────────────────────────────────
        Section("1. ITimeValueOfMoney");

        // Present value of $10 000 due in 3 years at 8 % annually
        var pvResult = tvm.CalculatePresentValue(
            new PresentValueRequest(
                FutureValue:   10_000m,
                AnnualRate:    0.08,
                TimeYears:     3,
                Mode:          PvCompoundingMode.Annual,
                Timing:        PaymentTiming.EndOfPeriod,
                DecimalPlaces: 2));

        logger.LogInformation(
            "PV of $10 000 in 3 yr @ 8% annual = {PV:C}", pvResult);

        // NPV of a simple project: invest $5 000 today, receive $2 000/yr for 3 yr @ 10%
        var npvResult = tvm.CalculateNPV(new NpvRequest(
            CashFlows: [
                new CashFlow(2_000m, 1),
                new CashFlow(2_000m, 2),
                new CashFlow(2_000m, 3),
            ],
            AnnualRate:        0.10,
            InitialInvestment: 5_000m,
            Mode:              PvCompoundingMode.Annual,
            DecimalPlaces:     2));

        logger.LogInformation(
            "NPV of 3×$2 000 @ 10% — initial $5 000 = {NPV:C}", npvResult.NetPresentValue);

        // ── 2. Loan and Interest ──────────────────────────────────────────────
        Section("2. ILoanAndInterest");

        // Monthly EMI for a $200 000 home loan at 7.5 % over 20 years (240 months)
        var emiRequest = new EmiRequest(
            Principal:    200_000m,
            AnnualRate:   0.075,
            TenureMonths: 240,
            Frequency:    PvCompoundingMode.Monthly,
            DecimalPlaces: 2);

        decimal emi = lai.CalculateEmi(emiRequest);
        logger.LogInformation("Monthly EMI: $200k @ 7.5% / 20 yr = {EMI:C}", emi);

        // Total cost of the same loan
        var totalCost = lai.CalculateTotalCost(new TotalCostRequest(
            Principal:    200_000m,
            AnnualRate:   0.075,
            TenureMonths: 240,
            Frequency:    PvCompoundingMode.Monthly,
            DecimalPlaces: 2));

        logger.LogInformation(
            "Total payable: {Total:C}  |  Total interest: {Interest:C}",
            totalCost.TotalPayable, totalCost.TotalInterest);

        // ── 3. Amortization ───────────────────────────────────────────────────
        Section("3. IAmortization");

        // Full amortization schedule for a $50 000 car loan at 9 % / 48 months
        AmortizationRow[] schedule = amort.GenerateSchedule(new AmortizationRequest(
            Principal:    50_000m,
            AnnualRate:   0.09,
            TenureMonths: 48,
            Frequency:    PvCompoundingMode.Monthly,
            DecimalPlaces: 2));

        logger.LogInformation(
            "Amortization schedule: {Periods} periods, EMI = {EMI:C}",
            schedule.Length, schedule[0].Payment);

        logger.LogInformation(
            "Period 1  — Interest: {Int:C}  Principal: {Pr:C}  Balance: {Bal:C}",
            schedule[0].InterestComponent,
            schedule[0].PrincipalComponent,
            schedule[0].ClosingBalance);

        logger.LogInformation(
            "Period 24 — Interest: {Int:C}  Principal: {Pr:C}  Balance: {Bal:C}",
            schedule[23].InterestComponent,
            schedule[23].PrincipalComponent,
            schedule[23].ClosingBalance);

        Banner("Demo complete — your Numerinus setup is working correctly!");
        return Task.CompletedTask;
    }

    // ── Console helpers ───────────────────────────────────────────────────────

    private void Banner(string text)
    {
        var line = new string('═', text.Length + 4);
        logger.LogInformation("{Line}", line);
        logger.LogInformation("  {Text}", text);
        logger.LogInformation("{Line}", line);
    }

    private void Section(string title) =>
        logger.LogInformation("\n── {Title} ──", title);
}

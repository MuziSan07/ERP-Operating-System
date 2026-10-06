using Erpos.Domain.Entities;

namespace Erpos.Application.Payroll;

/// <summary>
/// Salaried-individual income tax (Income Tax Ordinance, Division I Part I First Schedule) and the monthly
/// withholding under section 149, which spreads the projected annual tax over the remaining months of the tax year.
/// </summary>
public static class PakistanTax
{
    /// <summary>Tax years run July–June and are named by the ending year (July 2026 → tax year 2027).</summary>
    public static int TaxYearOf(int year, int month) => month >= 7 ? year + 1 : year;

    /// <summary>Months left in the tax year including this one (July = 12, June = 1).</summary>
    public static int RemainingMonths(int month) => month >= 7 ? 12 - (month - 7) : 6 - month + 1;

    public static decimal AnnualTax(decimal annualTaxableIncome, TaxYear table)
    {
        if (annualTaxableIncome <= 0) return 0;
        var slab = table.Slabs.OrderBy(s => s.From)
            .LastOrDefault(s => annualTaxableIncome > s.From);
        if (slab == null) return 0;

        var tax = slab.FixedTax + slab.Rate * (annualTaxableIncome - slab.From);
        if (table.SurchargeThreshold is { } threshold && annualTaxableIncome > threshold)
            tax += tax * table.SurchargeRate;
        return Math.Round(tax, 0, MidpointRounding.AwayFromZero);
    }

    /// <param name="currentMonthTaxable">Taxable income of this month.</param>
    /// <param name="priorTaxable">Taxable income already paid earlier in this tax year.</param>
    /// <param name="priorTax">Tax already withheld earlier in this tax year.</param>
    public static (decimal MonthTax, decimal ProjectedIncome, decimal ProjectedTax) MonthlyWithholding(
        int month, decimal currentMonthTaxable, decimal priorTaxable, decimal priorTax, TaxYear table)
    {
        var remaining = RemainingMonths(month);
        var projected = priorTaxable + currentMonthTaxable * remaining;
        var annual = AnnualTax(projected, table);
        var monthTax = Math.Max(0, (annual - priorTax) / remaining);
        return (Math.Round(monthTax, 0, MidpointRounding.AwayFromZero), projected, annual);
    }
}

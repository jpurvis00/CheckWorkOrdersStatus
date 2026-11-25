using WorkOrderDailyCheck.Data;

namespace WorkOrderDailyCheck.Checks;

/// <summary>
/// Checks from the CheckWorkOrderCosts program. The "cost difference greater than 5%"
/// check (issued material cost vs item standard cost) is intentionally left out.
/// </summary>
public static class CostChecks
{
    public static List<string> Run(WorkOrderData data, CheckRules rules)
    {
        var errors = new List<string>();
        errors.AddRange(CheckMissingMaterialCost(data.CostIssues));
        errors.AddRange(CheckTimeCardCosts(data.TimeCards, rules));
        errors.AddRange(CheckTotals(data, rules));
        return errors;
    }

    public static IEnumerable<string> CheckMissingMaterialCost(List<CostLedgerRow> issues) =>
        issues
            .Where(i => i.TotalMatCost == 0)
            .Select(i => $"Material issue is missing cost: component {i.ComponentItemNo}, lot {i.LotNo} " +
                         $"(issue {i.TransactionDocNo}/{i.TransactionDocLineNo}).");

    public static IEnumerable<string> CheckTimeCardCosts(List<TimeCardEntry> timeCards, CheckRules rules)
    {
        foreach (var t in timeCards)
        {
            var type = t.TcReportingType?.Trim().ToUpperInvariant();
            var where = $"step {t.StepId}, work center {t.WorkCenterNo} (record {t.RecordNo}, line {t.LineNo})";

            if (type is "SETUP" or "TEAR_DOWN")
            {
                if (t.TotalLaborCost != 0 || t.TotalFohCost != 0)
                    yield return $"{type} entry has a cost (labor {Money(t.TotalLaborCost)}, OH {Money(t.TotalFohCost)}): {where}.";
                if (t.GoodQty != 0 || t.ScrapQty != 0)
                    yield return $"{type} entry has quantities recorded (good {StatusChecks.Fmt(t.GoodQty)}, scrap {StatusChecks.Fmt(t.ScrapQty)}), which is not allowed: {where}.";
            }
            else if (type == "RUN"
                     && t.TotalLaborCost != 0
                     && rules.NoLaborWorkCenters.Any(w => string.Equals(w, t.WorkCenterNo?.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                yield return $"Work center {t.WorkCenterNo} should have no labor cost but has {Money(t.TotalLaborCost)}: {where}.";
            }
        }
    }

    /// <summary>
    /// The completion cost (summed over all completions) must equal what went into the WO:
    ///   material = issued material
    ///   labor    = issued labor + time card labor
    ///   OH       = issued OH + time card OH
    ///   subcon   = issued subcon + PO receipt subcon
    /// </summary>
    public static IEnumerable<string> CheckTotals(WorkOrderData d, CheckRules rules)
    {
        decimal Sum(IEnumerable<decimal> values) => Math.Round(values.Sum(), 4);
        // Completions are stored as negative values, so compare absolute amounts.
        decimal Completed(Func<CostLedgerRow, decimal> f) => d.Completions.Sum(c => Math.Round(Math.Abs(f(c)), 4));

        var checks = new (string Name, decimal Expected, decimal Completed, string Detail)[]
        {
            ("Material",
                Sum(d.CostIssues.Select(i => i.TotalMatCost)),
                Completed(c => c.TotalMatCost),
                "issued material"),
            ("Labor",
                Sum(d.CostIssues.Select(i => i.TotalLaborCost)) + Sum(d.TimeCards.Select(t => t.TotalLaborCost)),
                Completed(c => c.TotalLaborCost),
                $"issued {Money(Sum(d.CostIssues.Select(i => i.TotalLaborCost)))} + time cards {Money(Sum(d.TimeCards.Select(t => t.TotalLaborCost)))}"),
            ("OH",
                Sum(d.CostIssues.Select(i => i.TotalFohCost)) + Sum(d.TimeCards.Select(t => t.TotalFohCost)),
                Completed(c => c.TotalFohCost),
                $"issued {Money(Sum(d.CostIssues.Select(i => i.TotalFohCost)))} + time cards {Money(Sum(d.TimeCards.Select(t => t.TotalFohCost)))}"),
            ("Subcontracting",
                Sum(d.CostIssues.Select(i => i.TotalScCost)) + Sum(d.PoReceipts.Select(p => p.TotalScCost)),
                Completed(c => c.TotalScCost),
                $"issued {Money(Sum(d.CostIssues.Select(i => i.TotalScCost)))} + PO receipts {Money(Sum(d.PoReceipts.Select(p => p.TotalScCost)))}"),
        };

        foreach (var (name, expected, completed, detail) in checks)
        {
            if (Math.Abs(completed - expected) > rules.CostTolerance)
                yield return $"{name} costs do not match: completion {Money(completed)} vs {Money(expected)} ({detail}).";
        }
    }

    internal static string Money(decimal d) => d.ToString("#,##0.00##");
}

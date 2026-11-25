using WorkOrderDailyCheck.Data;

namespace WorkOrderDailyCheck.Checks;

/// <summary>
/// Checks from the CheckWorkOrdersStatus program. Every problem found is reported
/// (the old program stopped at the first one in some checks).
/// </summary>
public static class StatusChecks
{
    public static List<string> Run(WorkOrderData data, CheckRules rules)
    {
        var errors = new List<string>();
        errors.AddRange(CheckStepQuantities(data.TimeCards));
        errors.AddRange(CheckIssueBalances(data.IssueBalances));
        errors.AddRange(CheckIssueLotNumbers(data.IssueLots));
        errors.AddRange(CheckTimeCardHours(data.TimeCards, rules));
        return errors;
    }

    /// <summary>
    /// The good quantity reported on each routing step must equal the completed
    /// quantity, which is reported on step 0.
    /// </summary>
    public static IEnumerable<string> CheckStepQuantities(List<TimeCardEntry> timeCards)
    {
        if (timeCards.Count == 0)
            yield break;

        var byStep = timeCards
            .GroupBy(t => t.StepId)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.GoodQty));

        if (!byStep.TryGetValue(0, out var completedQty))
        {
            yield return "No completion (step 0) time card entry was found, so step quantities can't be compared.";
            yield break;
        }

        foreach (var (step, qty) in byStep)
        {
            if (step != 0 && qty != completedQty)
                yield return $"Step {step} good qty {Fmt(qty)} does not match the completed qty {Fmt(completedQty)}.";
        }
    }

    /// <summary>Every BOM item must be fully issued (balance 0).</summary>
    public static IEnumerable<string> CheckIssueBalances(List<IssueBalance> balances) =>
        balances
            .Where(b => b.BalanceQty != 0)
            .Select(b => $"Issue balance is not 0 on step {b.StepId}: balance {Fmt(b.BalanceQty)}.");

    /// <summary>An issue with no lot number means nothing valid was in inventory to issue.</summary>
    public static IEnumerable<string> CheckIssueLotNumbers(List<IssueLot> issues) =>
        issues
            .Where(i => string.IsNullOrWhiteSpace(i.LotNo))
            .Select(i => $"Item {i.ItemNo} was issued with no lot number (check inventory status and re-issue).");

    /// <summary>Shifts are 8 hours, so longer entries are wrong unless the work center runs unattended.</summary>
    public static IEnumerable<string> CheckTimeCardHours(List<TimeCardEntry> timeCards, CheckRules rules) =>
        timeCards
            .Where(t => t.WorkedHours > rules.MaxTimeCardHours
                        && !rules.LongRunWorkCenters.Any(w => string.Equals(w, t.WorkCenterNo?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(t => $"Time card entry over {Fmt(rules.MaxTimeCardHours)} hours: {Fmt(t.WorkedHours)} hrs on step {t.StepId}, " +
                         $"work center {t.WorkCenterNo} (record {t.RecordNo}, line {t.LineNo}).");

    internal static string Fmt(decimal d) => d.ToString("0.####");
}

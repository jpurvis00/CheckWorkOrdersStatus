using WorkOrderDailyCheck.Data;

namespace WorkOrderDailyCheck.Checks;

public sealed class WorkOrderResult
{
    public required ClosedWorkOrder WorkOrder { get; init; }
    public List<string> StatusErrors { get; } = new();
    public List<string> CostErrors { get; } = new();
    /// <summary>Set when the work order could not be checked at all (e.g. a query failed).</summary>
    public string? RunError { get; set; }

    public bool StatusOk => RunError is null && StatusErrors.Count == 0;
    public bool CostOk => RunError is null && CostErrors.Count == 0;
    public bool AllOk => StatusOk && CostOk;
}

public static class WorkOrderChecker
{
    public static WorkOrderResult Check(WorkOrderData data, CheckRules rules)
    {
        var result = new WorkOrderResult { WorkOrder = data.WorkOrder };
        result.StatusErrors.AddRange(StatusChecks.Run(data, rules));
        result.CostErrors.AddRange(CostChecks.Run(data, rules));
        return result;
    }
}

namespace WorkOrderDailyCheck.Data;

// Column names map automatically with Dapper's MatchNamesWithUnderscores
// (WORK_ORDER_NO -> WorkOrderNo). Quantities are decimal so a fractional
// Oracle NUMBER can never fail the mapping.

/// <summary>Row from z_closed_jobs_nightly.</summary>
public sealed class ClosedWorkOrder
{
    public string WorkOrderNo { get; set; } = "";
    public string? ItemNo { get; set; }
    public string? ItemDescription { get; set; }
    public decimal WoQty { get; set; }
    public DateTime CloseDate { get; set; }
}

/// <summary>Row from mfg_tc_detail (used by both the status and the cost checks).</summary>
public sealed class TimeCardEntry
{
    public string WorkOrderNo { get; set; } = "";
    public long? RecordNo { get; set; }
    public long? LineNo { get; set; }
    public int StepId { get; set; }
    public string? TcReportingType { get; set; }
    public decimal GoodQty { get; set; }
    public decimal ScrapQty { get; set; }
    public decimal WorkedHours { get; set; }
    public string? WorkCenterNo { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalFohCost { get; set; }
}

/// <summary>Row from V_WO_BOM_BALANCE_QTY.</summary>
public sealed class IssueBalance
{
    public string WorkOrderNo { get; set; } = "";
    public int StepId { get; set; }
    public decimal BalanceQty { get; set; }
}

/// <summary>Row from V_WO_ISSUES.</summary>
public sealed class IssueLot
{
    public string WorkOrderNo { get; set; } = "";
    public string? ItemNo { get; set; }
    public string? LotNo { get; set; }
}

/// <summary>Row from v_wo_cost_ledger (WO_ISSUES, PO_RECEIPT or COMPLETION).</summary>
public sealed class CostLedgerRow
{
    public string WorkOrderNo { get; set; } = "";
    public string? TransactionDocType { get; set; }
    public long? TransactionDocNo { get; set; }
    public long? TransactionDocLineNo { get; set; }
    public string? ComponentItemNo { get; set; }
    public string? LotNo { get; set; }
    public decimal TotalMatCost { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalFohCost { get; set; }
    public decimal TotalScCost { get; set; }
}

/// <summary>Everything the checks need for one work order.</summary>
public sealed class WorkOrderData
{
    public required ClosedWorkOrder WorkOrder { get; init; }
    public List<TimeCardEntry> TimeCards { get; init; } = new();
    public List<IssueBalance> IssueBalances { get; init; } = new();
    public List<IssueLot> IssueLots { get; init; } = new();
    public List<CostLedgerRow> CostIssues { get; init; } = new();
    public List<CostLedgerRow> PoReceipts { get; init; } = new();
    public List<CostLedgerRow> Completions { get; init; } = new();
}

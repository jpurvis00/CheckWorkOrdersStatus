using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace WorkOrderDailyCheck.Data;

public interface IErpRepository : IDisposable
{
    List<ClosedWorkOrder> GetClosedWorkOrders(int daysBack);
    WorkOrderData GetWorkOrderData(ClosedWorkOrder workOrder);
}

/// <summary>
/// Read-only access to the ERP. Every statement goes through <see cref="Query{T}"/>,
/// which refuses anything that is not a single SELECT, so this program cannot
/// change the database. All values are passed as bind parameters.
/// </summary>
public sealed class ErpRepository : IErpRepository
{
    private static readonly Regex SelectOnly = new(@"^\s*(select|with)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Forbidden = new(
        @"\b(insert|update|delete|merge|drop|alter|truncate|create|grant|revoke|commit|rollback|execute|exec|call|begin|declare|lock)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDbConnection _connection;

    static ErpRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        // Bind :name parameters by name (ODP.NET binds by position by default).
        OracleConfiguration.BindByName = true;
    }

    public ErpRepository(string connectionString)
    {
        _connection = new OracleConnection(connectionString);
        _connection.Open();
    }

    internal static void EnsureReadOnly(string sql)
    {
        if (!SelectOnly.IsMatch(sql) || sql.Contains(';') || Forbidden.IsMatch(sql))
            throw new InvalidOperationException("Only single SELECT statements are allowed: " + sql);
    }

    private List<T> Query<T>(string sql, object? param = null)
    {
        EnsureReadOnly(sql);
        return _connection.Query<T>(sql, param, commandTimeout: 120).ToList();
    }

    public List<ClosedWorkOrder> GetClosedWorkOrders(int daysBack) => Query<ClosedWorkOrder>(
        @"select work_order_no, item_no, item_description, wo_qty, close_date
            from z_closed_jobs_nightly
           where close_date >= trunc(sysdate) - :days
             and close_date <  trunc(sysdate) - :days + 1
           order by work_order_no",
        new { days = daysBack });

    public WorkOrderData GetWorkOrderData(ClosedWorkOrder workOrder)
    {
        var wo = new { wo = workOrder.WorkOrderNo };
        return new WorkOrderData
        {
            WorkOrder = workOrder,
            TimeCards = Query<TimeCardEntry>(
                @"select work_order_no, record_no, line_no, step_id, tc_reporting_type, good_qty, scrap_qty,
                         worked_hours, work_center_no, total_labor_cost, total_foh_cost
                    from mfg_tc_detail where work_order_no = :wo", wo),
            IssueBalances = Query<IssueBalance>(
                "select work_order_no, step_id, balance_qty from v_wo_bom_balance_qty where work_order_no = :wo", wo),
            IssueLots = Query<IssueLot>(
                "select work_order_no, item_no, lot_no from v_wo_issues where work_order_no = :wo", wo),
            CostIssues = Ledger(workOrder.WorkOrderNo, "WO_ISSUES"),
            PoReceipts = Ledger(workOrder.WorkOrderNo, "PO_RECEIPT"),
            Completions = Ledger(workOrder.WorkOrderNo, "COMPLETION"),
        };
    }

    private List<CostLedgerRow> Ledger(string workOrderNo, string docType) => Query<CostLedgerRow>(
        @"select work_order_no, transaction_doc_type, transaction_doc_no, transaction_doc_line_no,
                 component_item_no, lot_no, total_mat_cost, total_labor_cost, total_foh_cost, total_sc_cost
            from v_wo_cost_ledger
           where work_order_no = :wo and transaction_doc_type = :docType",
        new { wo = workOrderNo, docType });

    public void Dispose() => _connection.Dispose();
}

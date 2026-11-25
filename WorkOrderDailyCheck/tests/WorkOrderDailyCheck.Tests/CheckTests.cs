using ClosedXML.Excel;
using WorkOrderDailyCheck.Checks;
using WorkOrderDailyCheck.Data;
using WorkOrderDailyCheck.Output;
using Xunit;

namespace WorkOrderDailyCheck.Tests;

public class CheckTests
{
    private static readonly CheckRules Rules = new();

    private static ClosedWorkOrder Wo(string no = "906156") => new()
    {
        WorkOrderNo = no, ItemNo = "ABC-1", ItemDescription = "Widget", WoQty = 100, CloseDate = new DateTime(2026, 11, 24)
    };

    private static TimeCardEntry Tc(int step, decimal good, string type = "RUN", decimal hours = 1, string wc = "LATHE 1",
                                    decimal labor = 0, decimal oh = 0) =>
        new() { WorkOrderNo = "906156", StepId = step, GoodQty = good, TcReportingType = type, WorkedHours = hours,
                WorkCenterNo = wc, TotalLaborCost = labor, TotalFohCost = oh, RecordNo = 1, LineNo = 1 };

    /// <summary>A work order where every check passes.</summary>
    private static WorkOrderData GoodWo() => new()
    {
        WorkOrder = Wo(),
        TimeCards = { Tc(10, 60, labor: 30, oh: 10), Tc(10, 40, labor: 20, oh: 5), Tc(20, 100, labor: 10, oh: 5), Tc(0, 100, "COMPLETION") },
        IssueBalances = { new() { StepId = 10, BalanceQty = 0 } },
        IssueLots = { new() { ItemNo = "RAW-1", LotNo = "L1" } },
        CostIssues = { new() { ComponentItemNo = "RAW-1", LotNo = "L1", TotalMatCost = 250, TotalLaborCost = 5, TotalFohCost = 2, TotalScCost = 0 } },
        PoReceipts = { new() { TotalScCost = 12 } },
        Completions =
        {
            new() { TotalMatCost = -150, TotalLaborCost = -40, TotalFohCost = -12, TotalScCost = -6 },
            new() { TotalMatCost = -100, TotalLaborCost = -25, TotalFohCost = -10, TotalScCost = -6 },
        },
    };

    [Fact]
    public void Good_work_order_passes_both_checks()
    {
        var r = WorkOrderChecker.Check(GoodWo(), Rules);
        Assert.Empty(r.StatusErrors);
        Assert.Empty(r.CostErrors);
        Assert.True(r.AllOk);
    }

    [Fact]
    public void Step_quantity_mismatch_is_reported()
    {
        var d = GoodWo();
        d.TimeCards.Add(Tc(30, 90));
        var errors = StatusChecks.Run(d, Rules);
        Assert.Single(errors);
        Assert.Contains("Step 30 good qty 90", errors[0]);
    }

    [Fact]
    public void Missing_step_zero_is_an_error_not_a_crash()
    {
        var errors = StatusChecks.CheckStepQuantities(new() { Tc(10, 5) }).ToList();
        Assert.Single(errors);
        Assert.Contains("step 0", errors[0]);
    }

    [Fact]
    public void All_balances_and_missing_lots_are_reported_not_just_the_first()
    {
        var d = GoodWo();
        d.IssueBalances.Add(new() { StepId = 20, BalanceQty = 3 });
        d.IssueBalances.Add(new() { StepId = 30, BalanceQty = -1 });
        d.IssueLots.Add(new() { ItemNo = "RAW-2", LotNo = null });
        d.IssueLots.Add(new() { ItemNo = "RAW-3", LotNo = " " });
        Assert.Equal(4, StatusChecks.Run(d, Rules).Count);
    }

    [Fact]
    public void Time_cards_over_eight_hours_are_reported_except_unattended_machine()
    {
        var d = GoodWo();
        d.TimeCards.Add(Tc(10, 0, hours: 9.5m, wc: "LATHE 2"));
        d.TimeCards.Add(Tc(10, 0, hours: 20m, wc: "OTTO TSUGAMI B0385"));
        var errors = StatusChecks.Run(d, Rules);
        Assert.Single(errors);
        Assert.Contains("9.5 hrs", errors[0]);
    }

    [Fact]
    public void Status_problem_does_not_affect_cost_result()
    {
        var d = GoodWo();
        d.IssueLots.Add(new() { ItemNo = "RAW-2", LotNo = null });
        var r = WorkOrderChecker.Check(d, Rules);
        Assert.False(r.StatusOk);
        Assert.True(r.CostOk);
    }

    [Fact]
    public void Material_issue_without_cost_is_reported()
    {
        var d = GoodWo();
        d.CostIssues.Add(new() { ComponentItemNo = "RAW-9", LotNo = "L9", TotalMatCost = 0 });
        var errors = CostChecks.Run(d, Rules);
        Assert.Single(errors);
        Assert.Contains("RAW-9", errors[0]);
    }

    [Fact]
    public void Setup_and_teardown_with_cost_or_qty_are_reported()
    {
        var errors = CostChecks.CheckTimeCardCosts(new()
        {
            Tc(10, 0, "SETUP", labor: 5),
            Tc(10, 3, "TEAR_DOWN"),
            Tc(10, 0, "SETUP"),
        }, Rules).ToList();
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Labor_on_no_labor_work_center_is_reported()
    {
        var errors = CostChecks.CheckTimeCardCosts(new() { Tc(10, 5, wc: "PREPACK", labor: 1) }, Rules).ToList();
        Assert.Single(errors);
        Assert.Contains("PREPACK", errors[0]);
    }

    [Theory]
    [InlineData("Material")]
    [InlineData("Labor")]
    [InlineData("OH")]
    [InlineData("Subcontracting")]
    public void Totals_that_do_not_match_are_reported(string which)
    {
        var d = GoodWo();
        var c = d.Completions[0];
        switch (which)
        {
            case "Material": c.TotalMatCost -= 1; break;
            case "Labor": c.TotalLaborCost -= 1; break;
            case "OH": c.TotalFohCost -= 1; break;
            case "Subcontracting": c.TotalScCost -= 1; break;
        }
        var errors = CostChecks.CheckTotals(d, Rules).ToList();
        Assert.Single(errors);
        Assert.StartsWith(which, errors[0]);
    }

    [Fact]
    public void Differences_within_fifty_cents_are_ignored()
    {
        var d = GoodWo();
        d.Completions[0].TotalMatCost -= 0.49m;
        Assert.Empty(CostChecks.CheckTotals(d, Rules));
    }

    [Theory]
    [InlineData("select 1 from dual")]
    [InlineData("  WITH x as (select 1 from dual) select * from x")]
    public void Read_only_guard_allows_selects(string sql) => ErpRepository.EnsureReadOnly(sql);

    [Theory]
    [InlineData("update wo set x = 1")]
    [InlineData("select 1 from dual; delete from wo")]
    [InlineData("begin delete from wo; end")]
    [InlineData("with x as (select 1 from dual) delete from wo")]
    public void Read_only_guard_blocks_anything_else(string sql) =>
        Assert.Throws<InvalidOperationException>(() => ErpRepository.EnsureReadOnly(sql));
}

public class ExcelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wodc_" + Guid.NewGuid().ToString("N"));
    public ExcelTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private static WorkOrderResult Result(string no, bool costErr = false, bool statusErr = false)
    {
        var r = new WorkOrderResult
        {
            WorkOrder = new() { WorkOrderNo = no, ItemNo = "ITEM", ItemDescription = "Desc", WoQty = 10, CloseDate = new DateTime(2026, 11, 24) }
        };
        if (costErr) r.CostErrors.Add("bad cost");
        if (statusErr) r.StatusErrors.Add("bad status");
        return r;
    }

    [Fact]
    public void Creates_workbook_appends_rows_and_skips_duplicates()
    {
        var settings = new ExcelSettings { Path = Path.Combine(_dir, "log.xlsx"), SaveRetries = 0 };

        var first = ExcelLog.Append(new[] { Result("1001"), Result("1002", costErr: true), Result("1003", statusErr: true) }, settings, _dir);
        Assert.Equal(3, first.RowsAdded);
        Assert.Null(first.Warning);

        var second = ExcelLog.Append(new[] { Result("1003"), Result("1004") }, settings, _dir);
        Assert.Equal(1, second.RowsAdded);
        Assert.Equal(new[] { "1003" }, second.AlreadyInSheet);

        using var wb = new XLWorkbook(settings.Path);
        var ws = wb.Worksheet(1);
        Assert.Equal("WO #", ws.Cell(1, 1).GetString());
        Assert.Equal(5, ws.LastRowUsed()!.RowNumber());
        Assert.Equal(1001, ws.Cell(2, 1).GetDouble());
        Assert.Equal(new DateTime(2026, 11, 24), ws.Cell(2, 5).GetDateTime());
        // 1001 all good
        Assert.Equal("✓", ws.Cell(2, 6).GetString());
        Assert.Equal("✓", ws.Cell(2, 7).GetString());
        // 1002 cost problem -> cost blank, status checked
        Assert.Equal("", ws.Cell(3, 6).GetString());
        Assert.Equal("✓", ws.Cell(3, 7).GetString());
        // 1003 status problem -> status blank, cost checked
        Assert.Equal("✓", ws.Cell(4, 6).GetString());
        Assert.Equal("", ws.Cell(4, 7).GetString());
        Assert.Equal("1004", ws.Cell(5, 1).GetFormattedString());
    }

    [Fact]
    public void Appends_after_existing_user_rows_in_existing_workbook()
    {
        var path = Path.Combine(_dir, "existing.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("My Sheet");
            ws.Cell(1, 1).Value = "WO #";
            ws.Cell(2, 1).Value = 900001;
            ws.Cell(2, 3).Value = "typed by hand";
            wb.SaveAs(path);
        }

        var outcome = ExcelLog.Append(new[] { Result("900001"), Result("900002") }, new ExcelSettings { Path = path }, _dir);
        Assert.Equal(1, outcome.RowsAdded);

        using var check = new XLWorkbook(path);
        var sheet = check.Worksheet("My Sheet");
        Assert.Equal("typed by hand", sheet.Cell(2, 3).GetString());
        Assert.Equal(900002, sheet.Cell(3, 1).GetDouble());
    }

    [Fact]
    public void Unreachable_folder_falls_back_and_warns()
    {
        var settings = new ExcelSettings { Path = Path.Combine(_dir, "missing", "log.xlsx"), FallbackFolder = "fb" };
        var outcome = ExcelLog.Append(new[] { Result("1") }, settings, _dir);
        Assert.NotNull(outcome.Warning);
        Assert.StartsWith(Path.Combine(_dir, "fb"), outcome.SavedTo);
        Assert.True(File.Exists(outcome.SavedTo));
    }

    [Fact]
    public void Email_body_lists_problems_and_encodes_text()
    {
        var bad = Result("1002", costErr: true);
        bad.WorkOrder.ItemDescription = "<b>Bolt</b> & nut";
        var s = new RunSummary { DateChecked = new DateTime(2026, 11, 24), Results = new[] { Result("1001"), bad } };
        Assert.Equal("WO check 11/24/2026: 1 of 2 need attention", s.Subject);
        var html = Report.Html(s, "✓");
        Assert.Contains("Cost:</b> bad cost", html);
        Assert.Contains("&lt;b&gt;Bolt&lt;/b&gt; &amp; nut", html);
    }
}

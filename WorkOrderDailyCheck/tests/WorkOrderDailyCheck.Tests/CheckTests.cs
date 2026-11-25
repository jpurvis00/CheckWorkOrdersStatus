using Microsoft.Extensions.Configuration;
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

public class ReportTests
{
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
    public void Daily_email_lists_problems_and_encodes_text()
    {
        var bad = Result("1002", costErr: true);
        bad.WorkOrder.ItemDescription = "<b>Bolt</b> & nut";
        var s = new RunSummary { DateChecked = new DateTime(2026, 11, 24), Results = new[] { Result("1001"), bad } };
        Assert.Equal("WO check 11/24/2026: 1 of 2 need attention", s.Subject);
        var html = Report.Html(s);
        Assert.Contains("Work order check for WOs closed Tuesday, November 24, 2026", html);
        Assert.Contains("Cost:</b> bad cost", html);
        Assert.Contains("&lt;b&gt;Bolt&lt;/b&gt; &amp; nut", html);
        Assert.DoesNotContain("pread", html);   // no spreadsheet lines
    }

    [Fact]
    public void Recheck_subject_names_the_work_orders()
    {
        var ok = new RunSummary { DateChecked = DateTime.Today, Results = new[] { Result("906156") }, RequestedWorkOrders = new[] { "906156" } };
        Assert.Equal("WO recheck 906156: OK", ok.Subject);
        Assert.Contains("Work order recheck: 906156", Report.Html(ok));

        var bad = new RunSummary { DateChecked = DateTime.Today, Results = new[] { Result("906156", statusErr: true) }, RequestedWorkOrders = new[] { "906156" } };
        Assert.Equal("WO recheck 906156: needs attention", bad.Subject);
    }

    [Fact]
    public void Work_order_not_in_closed_jobs_is_noted()
    {
        var r = new WorkOrderResult { WorkOrder = new() { WorkOrderNo = "777", FoundInClosedJobs = false } };
        var s = new RunSummary { DateChecked = DateTime.Today, Results = new[] { r }, RequestedWorkOrders = new[] { "777" } };
        Assert.Contains("Not in z_closed_jobs_nightly", Report.Text(s));
    }
}

public class OptionsTests
{
    [Fact]
    public void Defaults_to_one_day_back_with_email()
    {
        var o = Options.Parse(Array.Empty<string>());
        Assert.Equal(1, o.Days);
        Assert.False(o.IsWorkOrderRun);
        Assert.False(o.NoEmail);
    }

    [Fact]
    public void Days_flag_and_bare_number_both_work()
    {
        Assert.Equal(3, Options.Parse(new[] { "--days", "3" }).Days);
        Assert.Equal(2, Options.Parse(new[] { "2" }).Days);
    }

    [Fact]
    public void Wo_accepts_repeats_and_comma_lists()
    {
        var o = Options.Parse(new[] { "--wo", "906156", "--WO", "906157,906158", "--wo", "906156", "--email" });
        Assert.Equal(new[] { "906156", "906157", "906158" }, o.WorkOrders);
        Assert.True(o.IsWorkOrderRun);
        Assert.True(o.Email);
    }

    [Theory]
    [InlineData("--wo", "1", "--days", "2")]
    [InlineData("--wo")]
    [InlineData("--days", "0")]
    [InlineData("--bogus")]
    [InlineData("--email", "--no-email")]
    public void Bad_arguments_are_rejected(params string[] args) =>
        Assert.Throws<ArgumentException>(() => Options.Parse(args));

    [Fact]
    public void Old_no_excel_flag_is_still_accepted() =>
        Assert.Equal(1, Options.Parse(new[] { "--days", "1", "--no-excel" }).Days);
}

public class ConfigAndGraphTests
{
    private static AppSettings Load(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, json);
        try
        {
            return AppSettings.Load(new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddJsonFile(path).Build());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Accepts_old_ConnectionStrings_Default_layout_and_single_string_To()
    {
        var s = Load("""
        { // comment
          "ConnectionStrings": { "Default": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=h)(PORT=1521))(CONNECT_DATA=(SID=x)));User Id=u;Password=p;" },
          "Email": { "Provider": "Graph", "To": "a@x.com; b@x.com", "From": "f@x.com" }
        }
        """);
        Assert.StartsWith("Data Source=(DESCRIPTION=", s.ConnectionString);
        Assert.Equal(new[] { "a@x.com", "b@x.com" }, s.Email.To);
        Assert.Equal("Graph", s.Email.Provider);
    }

    [Fact]
    public void To_as_list_still_works_and_missing_connection_string_is_a_clear_error()
    {
        Assert.Equal(new[] { "a@x.com" }, Load("""{ "ConnectionString": "x", "Email": { "To": [ "a@x.com" ] } }""").Email.To);
        var ex = Assert.Throws<InvalidOperationException>(() => Load("""{ "Email": { } }"""));
        Assert.Contains("ConnectionStrings", ex.Message);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Req, string Body)> Calls { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls.Add((r, r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct)));
            return r.RequestUri!.Host.StartsWith("login")
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"access_token":"tok"}""") }
                : new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
        }
    }

    [Fact]
    public void Graph_gets_token_then_sends_as_From_mailbox()
    {
        var settings = new EmailSettings
        {
            Provider = "Graph", From = "inventory-sync@example.com", To = { "jeff@example.com" },
            Graph = new() { TenantId = "tid", ClientId = "cid", ClientSecret = "sec" },
        };
        var handler = new FakeHandler();
        GraphMailer.Send(settings, "Subj", "<p>hi</p>", handler);

        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal("https://login.microsoftonline.com/tid/oauth2/v2.0/token", handler.Calls[0].Req.RequestUri!.ToString());
        Assert.Contains("grant_type=client_credentials", handler.Calls[0].Body);
        Assert.Equal("https://graph.microsoft.com/v1.0/users/inventory-sync%40example.com/sendMail", handler.Calls[1].Req.RequestUri!.AbsoluteUri);
        Assert.Equal("tok", handler.Calls[1].Req.Headers.Authorization!.Parameter);
        var sent = System.Text.Json.Nodes.JsonNode.Parse(handler.Calls[1].Body)!["message"]!;
        Assert.Equal("jeff@example.com", sent["toRecipients"]![0]!["emailAddress"]!["address"]!.GetValue<string>());
        Assert.Null(sent["attachments"]);
    }
}

using System.Net;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WorkOrderDailyCheck.Checks;

namespace WorkOrderDailyCheck.Output;

public sealed class RunSummary
{
    public required DateTime DateChecked { get; init; }
    public required IReadOnlyList<WorkOrderResult> Results { get; init; }
    public ExcelOutcome? Excel { get; init; }
    /// <summary>Set when the whole run failed (e.g. could not connect to the database).</summary>
    public string? FatalError { get; init; }

    public int ProblemCount => Results.Count(r => !r.AllOk);

    public string Subject => FatalError is not null
        ? $"WO check {DateChecked:M/d/yyyy}: FAILED to run"
        : Results.Count == 0
            ? $"WO check {DateChecked:M/d/yyyy}: no closed work orders"
            : ProblemCount == 0
                ? $"WO check {DateChecked:M/d/yyyy}: all {Results.Count} OK"
                : $"WO check {DateChecked:M/d/yyyy}: {ProblemCount} of {Results.Count} need attention";
}

public static class Report
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Html(RunSummary s, string checkMark)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#222\">");
        sb.Append($"<h2 style=\"margin:0 0 8px\">Work order check for WOs closed {s.DateChecked:dddd, MMMM d, yyyy}</h2>");

        if (s.FatalError is not null)
        {
            sb.Append("<p style=\"color:#b00020\"><b>The check could not run.</b></p>");
            sb.Append($"<pre style=\"background:#f6f6f6;padding:8px\">{E(s.FatalError)}</pre>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        if (s.Results.Count == 0)
            sb.Append("<p>No work orders were closed on this day.</p>");
        else
            sb.Append($"<p>{s.Results.Count} work order(s) checked: <b>{s.Results.Count - s.ProblemCount} OK</b>, " +
                      $"<b style=\"color:{(s.ProblemCount > 0 ? "#b00020" : "#222")}\">{s.ProblemCount} need attention</b>.</p>");

        if (s.Excel is not null)
        {
            if (s.Excel.Warning is not null)
                sb.Append($"<p style=\"color:#b00020\"><b>Spreadsheet:</b> {E(s.Excel.Warning)}</p>");
            else if (s.Excel.SavedTo is not null)
                sb.Append($"<p>Spreadsheet: {s.Excel.RowsAdded} row(s) added to {E(s.Excel.SavedTo)}.</p>");
            if (s.Excel.AlreadyInSheet.Count > 0)
                sb.Append($"<p>Already in the spreadsheet, not added again: {E(string.Join(", ", s.Excel.AlreadyInSheet))}.</p>");
        }

        // Problems first, so they are the first thing read.
        var problems = s.Results.Where(r => !r.AllOk).ToList();
        if (problems.Count > 0)
        {
            sb.Append("<h3 style=\"margin:16px 0 4px\">Issues</h3>");
            foreach (var r in problems)
            {
                var wo = r.WorkOrder;
                sb.Append($"<p style=\"margin:10px 0 2px\"><b>WO {E(wo.WorkOrderNo)}</b> &ndash; {E(wo.ItemNo)} {E(wo.ItemDescription)}</p><ul style=\"margin:0\">");
                if (r.RunError is not null)
                    sb.Append($"<li><b>Could not be checked:</b> {E(r.RunError)}</li>");
                foreach (var e in r.StatusErrors) sb.Append($"<li><b>Status:</b> {E(e)}</li>");
                foreach (var e in r.CostErrors) sb.Append($"<li><b>Cost:</b> {E(e)}</li>");
                sb.Append("</ul>");
            }
        }

        if (s.Results.Count > 0)
        {
            const string th = "style=\"text-align:left;padding:4px 8px;border-bottom:2px solid #999\"";
            const string td = "style=\"padding:3px 8px;border-bottom:1px solid #ddd\"";
            const string tdc = "style=\"padding:3px 8px;border-bottom:1px solid #ddd;text-align:center\"";
            sb.Append("<h3 style=\"margin:16px 0 4px\">All work orders</h3>");
            sb.Append("<table style=\"border-collapse:collapse;font-size:13px\">");
            sb.Append($"<tr><th {th}>WO #</th><th {th}>Item</th><th {th}>Description</th><th {th}>Qty</th>" +
                      $"<th {th}>Completed</th><th {th}>Cost</th><th {th}>Status</th></tr>");
            foreach (var r in s.Results)
            {
                var wo = r.WorkOrder;
                string Mark(bool ok) => ok ? E(checkMark) : "<span style=\"color:#b00020;font-weight:bold\">&#10007;</span>";
                sb.Append($"<tr><td {td}>{E(wo.WorkOrderNo)}</td><td {td}>{E(wo.ItemNo)}</td><td {td}>{E(wo.ItemDescription)}</td>" +
                          $"<td {td}>{wo.WoQty:0.##}</td><td {td}>{wo.CloseDate:M/d/yyyy}</td>" +
                          $"<td {tdc}>{Mark(r.CostOk)}</td><td {tdc}>{Mark(r.StatusOk)}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<p style=\"color:#777;font-size:12px;margin-top:16px\">Cost differences against item standard cost (the 5% check) are not reported.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static string Text(RunSummary s)
    {
        var sb = new StringBuilder();
        sb.AppendLine(s.Subject);
        sb.AppendLine(new string('=', s.Subject.Length));
        if (s.FatalError is not null)
        {
            sb.AppendLine("The check could not run:");
            sb.AppendLine(s.FatalError);
            return sb.ToString();
        }
        if (s.Excel?.Warning is not null) sb.AppendLine("Spreadsheet: " + s.Excel.Warning);
        else if (s.Excel?.SavedTo is not null) sb.AppendLine($"Spreadsheet: {s.Excel.RowsAdded} row(s) added to {s.Excel.SavedTo}");
        sb.AppendLine();
        foreach (var r in s.Results)
        {
            var wo = r.WorkOrder;
            sb.AppendLine($"WO {wo.WorkOrderNo}  {wo.ItemNo}  {wo.ItemDescription}  qty {wo.WoQty:0.##}  " +
                          $"cost {(r.CostOk ? "OK" : "ISSUE")}  status {(r.StatusOk ? "OK" : "ISSUE")}");
            if (r.RunError is not null) sb.AppendLine("    Could not be checked: " + r.RunError);
            foreach (var e in r.StatusErrors) sb.AppendLine("    Status: " + e);
            foreach (var e in r.CostErrors) sb.AppendLine("    Cost: " + e);
        }
        return sb.ToString();
    }

    public static void Send(RunSummary s, EmailSettings settings, string checkMark)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || settings.To.Count == 0 || string.IsNullOrWhiteSpace(settings.From))
            throw new InvalidOperationException("Email is enabled but Email:SmtpHost, Email:From or Email:To is not set in appsettings.json.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        foreach (var to in settings.To) message.To.Add(MailboxAddress.Parse(to));
        message.Subject = s.Subject;

        var body = new BodyBuilder { HtmlBody = Html(s, checkMark), TextBody = Text(s) };
        if (settings.AttachWorkbook && s.Excel?.SavedTo is { } path && File.Exists(path))
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            body.Attachments.Add(Path.GetFileName(path), ms.ToArray());
        }
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        var security = Enum.TryParse<SecureSocketOptions>(settings.Security, true, out var opt) ? opt : SecureSocketOptions.Auto;
        client.Connect(settings.SmtpHost, settings.SmtpPort, security);
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Authenticate(settings.Username, settings.Password);
        client.Send(message);
        client.Disconnect(true);
    }
}

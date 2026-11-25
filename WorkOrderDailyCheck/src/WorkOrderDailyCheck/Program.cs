using Microsoft.Extensions.Configuration;
using WorkOrderDailyCheck;
using WorkOrderDailyCheck.Checks;
using WorkOrderDailyCheck.Data;
using WorkOrderDailyCheck.Output;

// Usage: WorkOrderDailyCheck [days] [--days N] [--no-email] [--no-excel] [--config path]
//   days  how many days back to check (default 1 = yesterday, the normal daily run)
// Exit codes: 0 = ran, everything OK; 1 = ran, some WOs need attention; 2 = run failed.

var baseDir = AppContext.BaseDirectory;   // Task Scheduler starts in System32, so never rely on the current directory.
var options = Options.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: WorkOrderDailyCheck [days] [--days N] [--no-email] [--no-excel] [--config path]");
    return 0;
}

var config = new ConfigurationBuilder()
    .SetBasePath(baseDir)
    .AddJsonFile(options.ConfigPath ?? "appsettings.json", optional: false)
    .AddEnvironmentVariables("WODC_")   // e.g. WODC_Email__Password, WODC_ConnectionString
    .Build();
var settings = AppSettings.Load(config);

var logFolder = Path.IsPathRooted(settings.LogFolder) ? settings.LogFolder : Path.Combine(baseDir, settings.LogFolder);
Directory.CreateDirectory(logFolder);
var logFile = Path.Combine(logFolder, $"run_{DateTime.Now:yyyy-MM}.log");
void Log(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
    Console.WriteLine(line);
    try { File.AppendAllText(logFile, line + Environment.NewLine); } catch { /* logging must never stop the run */ }
}

var dateChecked = DateTime.Today.AddDays(-options.Days);
Log($"Checking work orders closed {dateChecked:yyyy-MM-dd} ({options.Days} day(s) back).");

RunSummary summary;
try
{
    var results = new List<WorkOrderResult>();
    using (var erp = new ErpRepository(settings.ConnectionString))
    {
        var closed = erp.GetClosedWorkOrders(options.Days);
        Log($"{closed.Count} closed work order(s) found.");

        foreach (var wo in closed)
        {
            try
            {
                results.Add(WorkOrderChecker.Check(erp.GetWorkOrderData(wo), settings.Rules));
            }
            catch (Exception ex)
            {
                Log($"WO {wo.WorkOrderNo}: could not be checked: {ex}");
                results.Add(new WorkOrderResult { WorkOrder = wo, RunError = ex.Message });
            }
        }
    }

    ExcelOutcome? excel = null;
    if (settings.Excel.Enabled && !options.NoExcel)
    {
        try
        {
            excel = ExcelLog.Append(results, settings.Excel, baseDir, Log);
            Log($"Spreadsheet: {excel.RowsAdded} row(s) added to {excel.SavedTo}. {excel.Warning}");
        }
        catch (Exception ex)
        {
            Log("Spreadsheet update failed: " + ex);
            excel = new ExcelOutcome { Warning = "The spreadsheet could not be updated: " + ex.Message };
        }
    }

    summary = new RunSummary { DateChecked = dateChecked, Results = results, Excel = excel };
}
catch (Exception ex)
{
    Log("Run failed: " + ex);
    summary = new RunSummary { DateChecked = dateChecked, Results = Array.Empty<WorkOrderResult>(), FatalError = Messages(ex) + Environment.NewLine + $"Full details are in {logFile}." };
}

var report = Report.Text(summary);
Console.WriteLine();
Console.WriteLine(report);
try { File.WriteAllText(Path.Combine(logFolder, $"report_{dateChecked:yyyy-MM-dd}.txt"), report); } catch { }

if (settings.Email.Enabled && !options.NoEmail)
{
    try
    {
        Report.Send(summary, settings.Email, settings.Excel.CheckMark);
        Log($"Email sent to {string.Join(", ", settings.Email.To)}.");
    }
    catch (Exception ex)
    {
        Log("Email failed: " + ex);
        return 2;
    }
}

return summary.FatalError is not null ? 2 : summary.ProblemCount > 0 ? 1 : 0;

static string Messages(Exception ex)
{
    var parts = new List<string>();
    for (Exception? e = ex; e is not null; e = e.InnerException) parts.Add(e.Message);
    return string.Join(Environment.NewLine, parts.Distinct());
}

internal sealed record Options(int Days, bool NoEmail, bool NoExcel, string? ConfigPath, bool ShowHelp)
{
    public static Options Parse(string[] args)
    {
        int days = 1;
        bool noEmail = false, noExcel = false, help = false;
        string? config = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--days" when i + 1 < args.Length: days = ParseDays(args[++i]); break;
                case "--no-email": noEmail = true; break;
                case "--no-excel": noExcel = true; break;
                case "--config" when i + 1 < args.Length: config = args[++i]; break;
                case "-h" or "--help" or "/?": help = true; break;
                default: days = ParseDays(args[i]); break;
            }
        }
        return new Options(days, noEmail, noExcel, config, help);
    }

    private static int ParseDays(string s) =>
        int.TryParse(s, out var d) && d > 0 ? d : throw new ArgumentException($"'{s}' is not a valid number of days (must be 1 or more).");
}

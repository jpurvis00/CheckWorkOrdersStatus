using Microsoft.Extensions.Configuration;
using WorkOrderDailyCheck;
using WorkOrderDailyCheck.Checks;
using WorkOrderDailyCheck.Data;
using WorkOrderDailyCheck.Output;

// Exit codes: 0 = ran, everything OK; 1 = ran, some WOs need attention; 2 = run failed.

var baseDir = AppContext.BaseDirectory;   // Task Scheduler starts in System32, so never rely on the current directory.
Options options;
try
{
    options = Options.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Options.Usage);
    return 2;
}
if (options.ShowHelp)
{
    Console.WriteLine(Options.Usage);
    return 0;
}

var config = new ConfigurationBuilder()
    .SetBasePath(baseDir)
    .AddJsonFile(options.ConfigPath ?? "appsettings.json", optional: false)
    .AddEnvironmentVariables("WODC_")   // e.g. WODC_Email__Graph__ClientSecret, WODC_ConnectionString
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
Log(options.IsWorkOrderRun
    ? $"Rechecking work order(s) {string.Join(", ", options.WorkOrders)}."
    : $"Checking work orders closed {dateChecked:yyyy-MM-dd} ({options.Days} day(s) back).");

RunSummary summary;
try
{
    var results = new List<WorkOrderResult>();
    using (var erp = new ErpRepository(settings.ConnectionString))
    {
        List<ClosedWorkOrder> workOrders;
        if (options.IsWorkOrderRun)
        {
            workOrders = options.WorkOrders.Select(erp.GetWorkOrder).ToList();
        }
        else
        {
            workOrders = erp.GetClosedWorkOrders(options.Days);
            Log($"{workOrders.Count} closed work order(s) found.");
        }

        foreach (var wo in workOrders)
        {
            try
            {
                var data = erp.GetWorkOrderData(wo);
                if (!wo.FoundInClosedJobs && !data.HasAnyActivity)
                    results.Add(new WorkOrderResult
                    {
                        WorkOrder = wo,
                        RunError = "No work order with this number was found (no closed-job record, time cards, issues or completions).",
                    });
                else
                    results.Add(WorkOrderChecker.Check(data, settings.Rules));
            }
            catch (Exception ex)
            {
                Log($"WO {wo.WorkOrderNo}: could not be checked: {ex}");
                results.Add(new WorkOrderResult { WorkOrder = wo, RunError = ex.Message });
            }
        }
    }

    summary = new RunSummary { DateChecked = dateChecked, Results = results, RequestedWorkOrders = options.WorkOrders };
}
catch (Exception ex)
{
    Log("Run failed: " + ex);
    summary = new RunSummary
    {
        DateChecked = dateChecked,
        Results = Array.Empty<WorkOrderResult>(),
        RequestedWorkOrders = options.WorkOrders,
        FatalError = Messages(ex) + Environment.NewLine + $"Full details are in {logFile}.",
    };
}

var report = Report.Text(summary);
Console.WriteLine();
Console.WriteLine(report);
var reportName = options.IsWorkOrderRun
    ? $"report_WO_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"
    : $"report_{dateChecked:yyyy-MM-dd}.txt";
try { File.WriteAllText(Path.Combine(logFolder, reportName), report); } catch { }

// The daily run emails unless --no-email; a --wo recheck only prints, unless --email.
var sendEmail = settings.Email.Enabled && (options.IsWorkOrderRun ? options.Email : !options.NoEmail);
if (sendEmail)
{
    try
    {
        Report.Send(summary, settings.Email);
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

internal sealed record Options(int Days, IReadOnlyList<string> WorkOrders, bool Email, bool NoEmail, string? ConfigPath, bool ShowHelp)
{
    public bool IsWorkOrderRun => WorkOrders.Count > 0;

    public const string Usage = """
        Usage:
          WorkOrderDailyCheck [--days N] [--no-email]
              Check every WO closed N days ago (default 1 = yesterday) and email the results.
              This is what the scheduled task runs.

          WorkOrderDailyCheck --wo 906156 [--wo 906157 | --wo 906156,906157] [--email]
              Recheck specific work orders, whenever they closed. Prints the results;
              add --email to also send them.

        Other options:
          --config file.json   use a different settings file (default appsettings.json next to the exe)
          --help               show this help
        """;

    public static Options Parse(string[] args)
    {
        int days = 1;
        bool daysGiven = false, email = false, noEmail = false, help = false;
        var workOrders = new List<string>();
        string? config = null;

        string Next(ref int i, string flag) =>
            i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{flag} needs a value.");

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--days": days = ParseDays(Next(ref i, "--days")); daysGiven = true; break;
                case "--wo":
                    workOrders.AddRange(Next(ref i, "--wo")
                        .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--email": email = true; break;
                case "--no-email": noEmail = true; break;
                case "--no-excel": break;   // the spreadsheet was removed; still accepted so old command lines keep working
                case "--config": config = Next(ref i, "--config"); break;
                case "-h" or "--help" or "/?": help = true; break;
                default:
                    if (int.TryParse(args[i], out _)) { days = ParseDays(args[i]); daysGiven = true; break; }
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        if (workOrders.Count > 0 && daysGiven)
            throw new ArgumentException("Use either --wo or --days, not both.");
        if (email && noEmail)
            throw new ArgumentException("Use either --email or --no-email, not both.");

        return new Options(days, workOrders.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), email, noEmail, config, help);
    }

    private static int ParseDays(string s) =>
        int.TryParse(s, out var d) && d > 0 ? d : throw new ArgumentException($"'{s}' is not a valid number of days (must be 1 or more).");
}

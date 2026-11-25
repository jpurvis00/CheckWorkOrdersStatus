namespace WorkOrderDailyCheck;

public sealed class AppSettings
{
    public string ConnectionString { get; set; } = "";
    public CheckRules Rules { get; set; } = new();
    public ExcelSettings Excel { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    /// <summary>Folder for the daily text report and run log. Relative paths are next to the exe.</summary>
    public string LogFolder { get; set; } = "logs";
}

public sealed class CheckRules
{
    /// <summary>Time card entries longer than this are reported (status check).</summary>
    public decimal MaxTimeCardHours { get; set; } = 8.0m;
    /// <summary>Work centers allowed to run longer than MaxTimeCardHours (unattended machines).</summary>
    public List<string> LongRunWorkCenters { get; set; } = new() { "OTTO TSUGAMI B0385" };
    /// <summary>Work centers whose RUN entries must carry no labor cost (cost check).</summary>
    public List<string> NoLaborWorkCenters { get; set; } = new() { "OTTO TSUGAMI S206", "PREPACK" };
    /// <summary>Allowed difference between completion cost and the summed costs, for rounding.</summary>
    public decimal CostTolerance { get; set; } = 0.50m;
}

public sealed class ExcelSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>The workbook to append to. Use a UNC path (\\server\share\...) when the task runs logged off.</summary>
    public string Path { get; set; } = @"P:\jeff\CheckStatus\WorkOrderChecks.xlsx";
    /// <summary>Worksheet to append to; blank = first worksheet.</summary>
    public string SheetName { get; set; } = "";
    public string CheckMark { get; set; } = "✓";
    /// <summary>Write a short list of problems in column H.</summary>
    public bool WriteNotesColumn { get; set; } = true;
    /// <summary>Used if the workbook folder cannot be reached. Relative paths are next to the exe.</summary>
    public string FallbackFolder { get; set; } = "output";
    /// <summary>How many times to retry saving when the workbook is open in Excel, and the wait between tries.</summary>
    public int SaveRetries { get; set; } = 5;
    public int SaveRetryDelaySeconds { get; set; } = 60;
}

public sealed class EmailSettings
{
    public bool Enabled { get; set; } = true;
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    /// <summary>Auto, None, StartTls, StartTlsWhenAvailable or SslOnConnect.</summary>
    public string Security { get; set; } = "Auto";
    /// <summary>Leave blank for an unauthenticated internal relay.</summary>
    public string Username { get; set; } = "";
    /// <summary>Better set through the WODC_Email__Password environment variable than stored here.</summary>
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public List<string> To { get; set; } = new();
    public bool AttachWorkbook { get; set; } = true;
}

using ClosedXML.Excel;
using WorkOrderDailyCheck.Checks;

namespace WorkOrderDailyCheck.Output;

public sealed class ExcelOutcome
{
    public string? SavedTo { get; set; }
    public int RowsAdded { get; set; }
    public List<string> AlreadyInSheet { get; } = new();
    /// <summary>Set when the workbook could not be saved to its normal location.</summary>
    public string? Warning { get; set; }
}

/// <summary>
/// Appends one row per work order to the tracking workbook:
///   A WO #  B Item  C Description  D WO Qty  E Date Completed  F Cost OK  G Status OK  [H Notes]
/// A check mark goes in F/G only when that check found no errors. Work orders already
/// in column A are skipped, so re-running a day does not add duplicate rows.
/// </summary>
public static class ExcelLog
{
    private static readonly string[] Headers =
        { "WO #", "Item", "Description", "WO Qty", "Date Completed", "Cost OK", "Status OK", "Notes" };

    public static ExcelOutcome Append(IReadOnlyList<WorkOrderResult> results, ExcelSettings settings, string baseDir,
                                      Action<string>? log = null)
    {
        var outcome = new ExcelOutcome();
        var path = settings.Path;

        if (!Directory.Exists(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))))
        {
            var fallback = FallbackPath(settings, baseDir, path);
            outcome.Warning = $"The folder for {path} could not be reached, so the workbook was saved to {fallback} instead. " +
                              "Copy these rows into your workbook.";
            path = fallback;
        }

        using var workbook = Open(path);
        var sheet = string.IsNullOrWhiteSpace(settings.SheetName)
            ? workbook.Worksheets.FirstOrDefault() ?? workbook.AddWorksheet("Work Orders")
            : workbook.Worksheets.TryGetWorksheet(settings.SheetName, out var ws) ? ws : workbook.AddWorksheet(settings.SheetName);

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
        {
            WriteHeader(sheet, settings.WriteNotesColumn);
            lastRow = 1;
        }

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = 1; r <= lastRow; r++)
            existing.Add(sheet.Cell(r, 1).GetFormattedString().Trim());

        var row = lastRow;
        foreach (var result in results)
        {
            var wo = result.WorkOrder;
            if (existing.Contains(wo.WorkOrderNo.Trim()))
            {
                outcome.AlreadyInSheet.Add(wo.WorkOrderNo);
                continue;
            }

            row++;
            if (long.TryParse(wo.WorkOrderNo.Trim(), out var woNumber))
                sheet.Cell(row, 1).Value = woNumber;
            else
                sheet.Cell(row, 1).Value = wo.WorkOrderNo.Trim();
            sheet.Cell(row, 2).Value = wo.ItemNo?.Trim() ?? "";
            sheet.Cell(row, 3).Value = wo.ItemDescription?.Trim() ?? "";
            sheet.Cell(row, 4).Value = wo.WoQty;
            sheet.Cell(row, 5).Value = wo.CloseDate.Date;
            sheet.Cell(row, 5).Style.DateFormat.Format = "m/d/yyyy";
            sheet.Cell(row, 6).Value = result.CostOk ? settings.CheckMark : "";
            sheet.Cell(row, 7).Value = result.StatusOk ? settings.CheckMark : "";
            sheet.Range(row, 6, row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (settings.WriteNotesColumn)
                sheet.Cell(row, 8).Value = Notes(result);

            existing.Add(wo.WorkOrderNo.Trim());
            outcome.RowsAdded++;
        }

        outcome.SavedTo = Save(workbook, path, settings, baseDir, outcome, log);
        return outcome;
    }

    internal static string Notes(WorkOrderResult r)
    {
        if (r.RunError is not null) return "Could not be checked: " + r.RunError;
        var parts = new List<string>();
        if (r.CostErrors.Count > 0) parts.Add($"Cost: {r.CostErrors.Count} issue(s)");
        if (r.StatusErrors.Count > 0) parts.Add($"Status: {r.StatusErrors.Count} issue(s)");
        return parts.Count == 0 ? "" : string.Join("; ", parts) + " - see email";
    }

    private static XLWorkbook Open(string path)
    {
        if (!File.Exists(path))
            return new XLWorkbook();

        // Read with FileShare.ReadWrite so this works even while the file is open in Excel.
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var ms = new MemoryStream();
        fs.CopyTo(ms);
        ms.Position = 0;
        return new XLWorkbook(ms);
    }

    private static void WriteHeader(IXLWorksheet sheet, bool notes)
    {
        var count = notes ? Headers.Length : Headers.Length - 1;
        for (var c = 0; c < count; c++)
            sheet.Cell(1, c + 1).Value = Headers[c];
        sheet.Range(1, 1, 1, count).Style.Font.Bold = true;
        sheet.Column(3).Width = 40;
        sheet.Column(5).Width = 15;
        if (notes) sheet.Column(8).Width = 45;
    }

    private static string Save(XLWorkbook workbook, string path, ExcelSettings settings, string baseDir,
                               ExcelOutcome outcome, Action<string>? log)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                workbook.SaveAs(path);
                return path;
            }
            catch (IOException ex) when (attempt <= settings.SaveRetries)
            {
                log?.Invoke($"Could not save {path} (attempt {attempt}): {ex.Message}. Retrying in {settings.SaveRetryDelaySeconds}s.");
                Thread.Sleep(TimeSpan.FromSeconds(settings.SaveRetryDelaySeconds));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                var fallback = FallbackPath(settings, baseDir, path);
                workbook.SaveAs(fallback);
                outcome.Warning = $"{path} could not be saved ({ex.Message}; probably open in Excel). " +
                                  $"Today's rows were saved to {fallback} instead. Copy them into your workbook.";
                return fallback;
            }
        }
    }

    private static string FallbackPath(ExcelSettings settings, string baseDir, string originalPath)
    {
        var folder = System.IO.Path.IsPathRooted(settings.FallbackFolder)
            ? settings.FallbackFolder
            : System.IO.Path.Combine(baseDir, settings.FallbackFolder);
        Directory.CreateDirectory(folder);
        var name = System.IO.Path.GetFileNameWithoutExtension(originalPath);
        return System.IO.Path.Combine(folder, $"{name}_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx");
    }
}

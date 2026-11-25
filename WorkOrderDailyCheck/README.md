# WorkOrderDailyCheck

One program that replaces both **CheckWorkOrdersStatus** and **CheckWorkOrderCosts**. Run it every
day and it:

1. Reads the work orders closed yesterday from `z_closed_jobs_nightly` (or `N` days back).
2. Runs the **status** checks and the **cost** checks on each one.
3. Appends one row per work order to the tracking spreadsheet: WO #, Item, Description, WO Qty,
   Date Completed, **Cost OK**, **Status OK** (and a short Notes column). A check mark is written
   only when that check found no errors. A WO already in the sheet is not added again, so a day
   can be safely re-run.
4. Emails you the results: problems first, then every WO with its cost/status marks. The
   spreadsheet is attached.

It only ever **reads** from the database: every statement must be a single `SELECT`, or the
program refuses to run it, and all values are bind parameters.

## Checks

**Status** (from CheckWorkOrdersStatus). All errors are reported:
- The good qty on every routing step equals the completed qty (step 0).
- No issue balance left on any BOM line (`V_WO_BOM_BALANCE_QTY`).
- Every issued item has a lot number (`V_WO_ISSUES`).
- No time card entry over 8 hours, except work center `OTTO TSUGAMI B0385`.

**Cost** (from CheckWorkOrderCosts). All errors are reported except the
"cost difference greater than 5%" (standard cost) check, which is left out:
- No material issue with a zero cost.
- SETUP / TEAR_DOWN time card entries have no cost and no quantities.
- RUN entries on `OTTO TSUGAMI S206` / `PREPACK` have no labor cost.
- Completion material / labor / OH / subcontract cost matches issues + time cards + PO receipts
  (within $0.50).

The work-center lists, the 8-hour limit and the $0.50 tolerance live in the `Rules` section of
`appsettings.json`.

### Changes from the old programs
- **The 8-hour time card check now actually runs.** In the old status program the time cards were
  loaded into a local copy of the list, so the check always looked at an empty list and never
  reported anything. Expect it to start flagging entries.
- A WO with no step-0 completion entry is reported instead of crashing the program.
- Balance and lot-number checks list every bad line, not only the first.
- Errors say which step, item, lot or work center is the problem.
- Queries use bind parameters instead of building SQL strings by hand.

## Setup (Windows PC or server on the company network)

The program has to run on a machine that can reach the Oracle database, the `P:` share and your
mail server.

1. Build it (needs the .NET 8 SDK), or use the prebuilt zip:
   ```
   dotnet publish src/WorkOrderDailyCheck -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o C:\Tools\WorkOrderDailyCheck
   ```
   This gives a single `WorkOrderDailyCheck.exe`, so .NET doesn't need to be installed on the machine.
2. Copy `appsettings.example.json` to `appsettings.json` next to the exe and fill in:
   - `ConnectionStrings:Default` (or `ConnectionString`): the same one the old programs use. A
     read-only Oracle user is best.
   - `Excel:Path`: your workbook. If the task will run while you're logged off, use the UNC path
     (run `net use` to see what `P:` maps to), because drive letters aren't mapped then. In JSON
     every backslash is doubled, so `\\server\share\file.xlsx` is written
     `"\\\\server\\share\\file.xlsx"`.
   - `Email`: either `"Provider": "Graph"` (Microsoft 365 app registration with the **Mail.Send**
     application permission; `From` must be a real mailbox) or `"Provider": "Smtp"` with a mail
     server. Keep secrets out of the file with the environment variables
     `WODC_Email__Graph__ClientSecret` or `WODC_Email__Password`.
3. Try it by hand, without sending email or touching the spreadsheet:
   ```
   WorkOrderDailyCheck.exe --days 1 --no-email --no-excel
   ```
4. Schedule it for 6:00 PM every day (elevated PowerShell):
   ```
   .\scripts\Register-DailyTask.ps1 -ExePath "C:\Tools\WorkOrderDailyCheck\WorkOrderDailyCheck.exe"
   ```

## Usage

```
WorkOrderDailyCheck [days] [--days N] [--no-email] [--no-excel] [--config file.json]
```
`days` defaults to 1 (yesterday). Exit code is 0 when everything is OK, 1 when some WOs need
attention, 2 when the run failed. A failed run still sends an email saying why.

Logs and a plain-text copy of each day's report are written to `logs\` next to the exe.

If the workbook is open in Excel when the job runs, the program retries for a few minutes. Then
it saves a copy to `output\` next to the exe and the email tells you where it went.

## Tests

```
dotnet test
```

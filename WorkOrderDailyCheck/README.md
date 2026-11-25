# WorkOrderDailyCheck

One program that replaces both **CheckWorkOrdersStatus** and **CheckWorkOrderCosts**. Run it every
day and it:

1. Reads the work orders closed yesterday from `z_closed_jobs_nightly` (or `N` days back).
2. Runs the **status** checks and the **cost** checks on each one.
3. Emails you the results: problems first, then every WO with a cost and a status mark. A check
   mark means that check found no errors.

You can also recheck specific work orders at any time with `--wo` (see Usage).

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

The program has to run on a machine that can reach the Oracle database and send email.

1. Build it (needs the .NET 8 SDK), or use the prebuilt zip:
   ```
   dotnet publish src/WorkOrderDailyCheck -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o C:\Tools\WorkOrderDailyCheck
   ```
   This gives a single `WorkOrderDailyCheck.exe`, so .NET doesn't need to be installed on the machine.
2. Copy `appsettings.example.json` to `appsettings.json` next to the exe and fill in:
   - `ConnectionStrings:Default` (or `ConnectionString`): the same one the old programs use. A
     read-only Oracle user is best.
   - `Email`: either `"Provider": "Graph"` (Microsoft 365 app registration with the **Mail.Send**
     application permission; `From` must be a real mailbox) or `"Provider": "Smtp"` with a mail
     server. Keep secrets out of the file with the environment variables
     `WODC_Email__Graph__ClientSecret` or `WODC_Email__Password`.
3. Try it by hand without sending email:
   ```
   WorkOrderDailyCheck.exe --days 1 --no-email
   ```
4. Schedule it for 6:00 PM every day (elevated PowerShell). The task runs `--days 1`.
   ```
   Get-ChildItem C:\Tools\WorkOrderDailyCheck | Unblock-File
   .\scripts\Register-DailyTask.ps1 -ExePath "C:\Tools\WorkOrderDailyCheck\WorkOrderDailyCheck.exe"
   ```
   `Unblock-File` is needed when the files came from a downloaded zip; otherwise PowerShell
   refuses to run the unsigned script.

## Usage

**Daily check** (what the scheduled task runs):
```
WorkOrderDailyCheck.exe [--days N] [--no-email]
```
Checks every WO closed `N` days ago (default 1 = yesterday) and emails the results. Use
`--days 3` to redo the check for three days ago.

**Recheck specific work orders**, whenever they closed:
```
WorkOrderDailyCheck.exe --wo 906156
WorkOrderDailyCheck.exe --wo 906156,906157 --email
```
Prints the results in the console. Add `--email` to also send them in the same format as the
daily email. A WO that isn't in `z_closed_jobs_nightly` is still checked, but the item details
are blank. A number with no activity at all is reported as not found. `--wo` and `--days` can't
be used together.

Exit code is 0 when everything is OK, 1 when some WOs need attention, 2 when the run failed. A
failed daily run still sends an email saying why.

Logs and a plain-text copy of each report are written to `logs\` next to the exe.

## Tests

```
dotnet test
```


using DataAccessLibrary;
using DataAccessLibrary.Models;
using WorkOrderStatus;

internal static class CheckTimeCard
{
    public static void CheckTimeCardEntriesOverEightHours(OracleCrud oracleConnection, string workOrderNo, List<TimeCardEntriesModel> timeCardEntries)
    {
        if (CheckTimeCardEntries.CheckTimeCardEntriesOverEightHours(timeCardEntries) == true)
        {
            ShowUserMessage.DisplayMessage($"There is a time entry that is over 8 hours.  Please manually review WO: {workOrderNo}.");
        }
    }
}
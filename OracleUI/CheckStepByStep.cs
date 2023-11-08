
using DataAccessLibrary;
using DataAccessLibrary.Models;

internal class CheckStepByStep
{
    public static void CheckStepByStepTimeCardEntries(OracleCrud oracleConnection, string workOrderNo, List<TimeCardEntriesModel> timeCardEntries)
    {
        timeCardEntries = oracleConnection.GetTimeCardEntriesForWorkOrder(workOrderNo);

        if (WorkOrderStatus.CheckStepByStep.CheckStepByStepMatches(timeCardEntries) == false)
        {
            ShowUserMessage.DisplayMessage("The quantities in your completed steps and the completed quantity do not match.  Please check the WO.");
        }


    }
}
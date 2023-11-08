
using DataAccessLibrary;
using WorkOrderStatus;

internal static class CheckWOIssues
{
    public static void CheckWOIssuesBalances(OracleCrud oracleConnection, string workOrderNo)
    {
        /* Look at the items that are in the BOM of the parent item.  Check to see if the remaining
         * balance to issue is 0.  If it's not 0, return a message to check the WO issues.
         */
        var workOrderIssuesBalances = oracleConnection.GetWorkOrderItemIssuesBalance(workOrderNo);

        var (balanceIsZero, message) = CheckWorkOrderIssuesBalance.CheckBalancesOnWorkOrder(workOrderIssuesBalances);

        if (balanceIsZero == false)
        {
            ShowUserMessage.DisplayMessage(message);
        }
    }

    public static void CheckWOIssuesLotNos(OracleCrud oracleConnection, string workOrderNo)
    {
        var WorkOrderLotNumbers = oracleConnection.GetWorkOrderIssuesLotNumbers(workOrderNo);

        if (CheckWOLotNos.CheckWOIssuesLotNos(WorkOrderLotNumbers) == false)
        {
            ShowUserMessage.DisplayMessage("There is an issue with no lot number(it is probably missing).  Please check your item issues.");
        }
    }
}
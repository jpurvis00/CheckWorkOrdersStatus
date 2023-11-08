
using DataAccessLibrary.Models;

namespace WorkOrderStatus
{
    public static class CheckWorkOrderIssuesBalance
    {
        public static (bool, string) CheckBalancesOnWorkOrder(List<WorkOrderIssuesBalanceModel> workOrderIssuesBalances)
        {
            string errorMessage = string.Empty;

            foreach (var workOrderIssue in workOrderIssuesBalances)
            {
                if(workOrderIssue.Balance_Qty != 0)
                {
                    errorMessage = $"The balance quantity is not 0. WO: {workOrderIssue.Work_Order_No}  Step: {workOrderIssue.Step_Id}  Balance: {workOrderIssue.Balance_Qty}.";
                    return (false, errorMessage);
                }
            }

            return (true, "All balances look good.");
        }
    }
}

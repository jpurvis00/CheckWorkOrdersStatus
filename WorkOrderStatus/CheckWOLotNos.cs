
using DataAccessLibrary.Models;

namespace WorkOrderStatus
{
    public static class CheckWOLotNos
    {
        public static bool CheckWOIssuesLotNos(List<WorkOrderIssuesWithLotNosModel> workOrderLotNumbers)
        {
            /* If the lot no is null or empty, the item did not issue from a valid lot no in inventory.
             * This usually indicates that there is a problem with the inventory status for that item.
             * This must be manually fixed and then re-issued to the WO.
             */
            foreach (var workOrder in workOrderLotNumbers)
            {
                if(string.IsNullOrEmpty(workOrder.Lot_No))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

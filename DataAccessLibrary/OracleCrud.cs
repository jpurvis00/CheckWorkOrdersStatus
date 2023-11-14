
using DataAccessLibrary.Models;

namespace DataAccessLibrary
{
    public class OracleCrud
    {
        private readonly string _connectionString;
        private OracleDataAccess _db = new OracleDataAccess();

        public OracleCrud(string connectionString)
        {
            _connectionString = connectionString; 
        }

        public List<ClosedWorkOrderModel> GetClosedWorkOrders()
        {
            string sql = "select * from z_closed_jobs_nightly where close_date = TRUNC(SYSDATE - 1)";

            return _db.LoadData<ClosedWorkOrderModel, dynamic>(sql, new { }, _connectionString);
        }

        public List<TimeCardEntriesModel> GetTimeCardEntriesForWorkOrder(string workOrder)
        {
            string sql = $"select * from mfg_tc_detail where work_order_no = '{workOrder}'";

            return _db.LoadData<TimeCardEntriesModel, dynamic>(sql, new { }, _connectionString);
            /* I can't get the below to work for some reason.  I believe it has to do with oracle giving an error
             * when the above sql wo # is not eclosed in single quotes.  I could only get results with the solution above.
             */
            //return _db.LoadData<TimeCardEntriesModel, dynamic>(sql, new { workOrder = workOrder }, _connectionString).ToList();
        }

        public List<WorkOrderIssuesBalanceModel> GetWorkOrderItemIssuesBalance(string workOrder)
        {
            string sql = $"select WORK_ORDER_NO, STEP_ID, BALANCE_QTY from V_WO_BOM_BALANCE_QTY where WORK_ORDER_NO = '{workOrder}'";

            return _db.LoadData<WorkOrderIssuesBalanceModel, dynamic>(sql, new { }, _connectionString);
        }

        public List<WorkOrderIssuesWithLotNosModel> GetWorkOrderIssuesLotNumbers(string workOrder)
        {
            string sql = $"select WORK_ORDER_NO, ITEM_NO, LOT_NO from V_WO_ISSUES where WORK_ORDER_NO = '{workOrder}'";

            return _db.LoadData<WorkOrderIssuesWithLotNosModel, dynamic>(sql, new { }, _connectionString);
        }
    }
}

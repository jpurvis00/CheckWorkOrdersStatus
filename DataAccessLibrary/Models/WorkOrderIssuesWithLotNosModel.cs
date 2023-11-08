using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLibrary.Models
{
    public class WorkOrderIssuesWithLotNosModel
    {
        public string Work_Order_No { get; set; }
        public string Item_No { get; set; }
        public string Lot_No { get; set; }
    }
}

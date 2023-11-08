using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLibrary.Models
{
    public class ClosedWorkOrderModel
    {
        public string Work_Order_No { get; set; }
        public string Item_No { get; set; }
        public string Item_Description { get; set; }
        public int Wo_Qty { get; set; }
        public DateTime Close_Date { get; set; }
    }
}

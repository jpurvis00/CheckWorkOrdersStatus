
using DataAccessLibrary.Models;

namespace WorkOrderStatus
{
    public class CheckTimeCardEntries
    {
        public static bool CheckTimeCardEntriesOverEightHours(List<TimeCardEntriesModel> timeCards)
        {
            /* Timecard entries should not be over 8 hours since employees shifts are only 8 hours long.
             * The one exception is the machine below. It can run 24 hours with no intervention from an 
             * employee.  If the worked time is over 8 hours, we must manually fix.
             */
            foreach (var timeCardEntry in timeCards)
            {
                if (timeCardEntry.Worked_Hours > 8.0 && string.Equals(timeCardEntry.Work_Center_No, "OTTO TSUGAMI B0385") == false)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

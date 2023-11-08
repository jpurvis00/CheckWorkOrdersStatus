
using DataAccessLibrary.Models;

namespace WorkOrderStatus
{
    public static class CheckStepByStep
    {
        public static bool CheckStepByStepMatches(List<TimeCardEntriesModel> completedSteps)
        {
            /* Pulls all the completed steps for the WO's routing. */
            foreach (var entry in completedSteps)
            {
                Console.WriteLine($"WO: {entry.Work_Order_No} Record No: {entry.Record_No} " +
                    $"Line No: {entry.Line_No} Step Id: {entry.Step_Id} Good qty: {entry.Good_Qty} " +
                    $"Scrdap Qty = {entry.Scrap_Qty} Reporting type: {entry.Tc_Reporting_Type}");
            }
            
            var stepIdDictionary = CreateStepDictionary(completedSteps);
            
            return CompareStepQuantitiesToCompletedQuantities(stepIdDictionary);
        }

        private static Dictionary<int, int> CreateStepDictionary(List<TimeCardEntriesModel> completedSteps)
        {
            /* I am using a dictionary to congregate steps that are the same.  The number of steps can vary
             * by WO. I am adding the completed quantity from all like steps together. Since I do not know 
             * when to stop counting and move onto the next step, I create a dictionary with a key for each step
             * since they are unique.  We then add the completed qty from each step to the value to get the 
             * total completed for that step.
             */
            Dictionary<int, int> stepId = new Dictionary<int, int>();

            foreach (var entry in completedSteps)
            {
                if (stepId.ContainsKey(entry.Step_Id) == false)
                {
                    stepId.Add(entry.Step_Id, entry.Good_Qty);
                }
                else
                {
                    stepId[entry.Step_Id] += entry.Good_Qty;
                }
            }

            return stepId;
        }

        private static bool CompareStepQuantitiesToCompletedQuantities(Dictionary<int, int> stepIdDictionary)
        {
            /* Completion steps always have the value of 0. This values represents the total number of items
             * created from the WO.  Each step must match that value.  If it doesn't, we need to know so we
             * can check the WO manually.
             */
            foreach (var key in stepIdDictionary)
            {
                if (key.Value != stepIdDictionary[0])
                {
                    return false;
                }
            }

            return true;
        }
    }
}

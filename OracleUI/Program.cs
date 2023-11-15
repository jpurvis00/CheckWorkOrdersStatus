
using Microsoft.Extensions.Configuration;
using DataAccessLibrary;
using DataAccessLibrary.Models;

internal class Program
{
    static List<TimeCardEntriesModel> timeCardEntries = new List<TimeCardEntriesModel>();

    private static void Main(string[] args)
    {
        OracleCrud oracleConnection = new OracleCrud(GetConnectionString());

        var dayToCheck = GetDayToCheckClosedWorkOrders.GetDayToCheck();
        var closedWorkOrders = oracleConnection.GetClosedWorkOrders(dayToCheck);

        foreach (var workOrder in closedWorkOrders)
        {
            Console.WriteLine($"***** WO: {workOrder.Work_Order_No} *****");

            CheckStepByStep.CheckStepByStepTimeCardEntries(oracleConnection, workOrder.Work_Order_No, timeCardEntries);

            CheckWOIssues.CheckWOIssuesBalances(oracleConnection, workOrder.Work_Order_No);

            CheckWOIssues.CheckWOIssuesLotNos(oracleConnection, workOrder.Work_Order_No);

            CheckTimeCard.CheckTimeCardEntriesOverEightHours(oracleConnection, workOrder.Work_Order_No, timeCardEntries);

            Console.WriteLine("**********************\n");
        }

        Console.WriteLine("Finished processing work orders.");
        Console.ReadLine();
    }

    private static string GetConnectionString(string connectionStringName = "Default")
    {
        string output = "";

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json");

        var config = builder.Build();

        output = config.GetConnectionString(connectionStringName);

        return output;
    }
}
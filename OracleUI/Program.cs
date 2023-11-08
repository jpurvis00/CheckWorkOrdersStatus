
using Microsoft.Extensions.Configuration;
using DataAccessLibrary;
using DataAccessLibrary.Models;

internal class Program
{
    static List<TimeCardEntriesModel> timeCardEntries = new List<TimeCardEntriesModel>();

    private static void Main(string[] args)
    {
        OracleCrud oracleConnection = new OracleCrud(GetConnectionString());

        var closedWorkOrders = oracleConnection.GetClosedWorkOrders();
       
        foreach(var workOrder in closedWorkOrders)
        {
            CheckStepByStep.CheckStepByStepTimeCardEntries(oracleConnection, workOrder.Work_Order_No, timeCardEntries);

            CheckWOIssues.CheckWOIssuesBalances(oracleConnection, workOrder.Work_Order_No);

            CheckWOIssues.CheckWOIssuesLotNos(oracleConnection, workOrder.Work_Order_No);

            CheckTimeCard.CheckTimeCardEntriesOverEightHours(oracleConnection, workOrder.Work_Order_No, timeCardEntries);
        }
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
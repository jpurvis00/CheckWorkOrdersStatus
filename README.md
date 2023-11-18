# CheckWorkOrdersStatus
Grabs a list of Work Orders completed yesterday and checks them for any errors.  This is accomplished by connecting to an oracle database and then 
using the Dapper ORM to retrieve the info from the db and map it to our class models for processing.

The following items are checked from WO's.
 1. Check that the quantities from completed routing steps match the WO completed quantity.
 2. Checks that there is not a balance on the items that have been issued. A balance indicates that there is still items to be issued.
 3. Checks the issued items to make sure they have a lot no assigned to them. A missing lot no indicates that there was no available items to issue to the WO.
 4. Checks to see if there are any time card entries from employees that are over 8 hours long.

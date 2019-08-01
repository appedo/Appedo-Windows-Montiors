using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("RESILEO_WINDOWS_AGENT_v2")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Resileo")]
[assembly: AssemblyProduct("Resileo")]
[assembly: AssemblyCopyright("Resileo@2017")]
[assembly: AssemblyTrademark("Resileo")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(true)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("d4434645-a1e6-4c68-b0e5-da030295d832")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("2.0.47.0")]
[assembly: AssemblyFileVersion("2.0.47.0")]
//Fixes Done
//V2.0.47 Added ICMP server status log for every 2 hours to ensure the system is running as expected. When the runICMP changed from true to false or vice versa, automatically system will start the service without requiring to restart the windows agent service.
//V2.0.46 Added ICMP Test for specified server in pingservers.txt file
//V2.0.45 Added logical disk measurement for all the drives. previously we were collecting only for total now it will collect for all available drives.  --16-Apr-2019
//V2.0.44 Added MSSQL wait status  --29-Mar-2019
//V2.0.43 For multiple database, slow query is not returning the db name -- fixed, lock query changed to get the client address, status and command.  --28-Mar-2019
//V2.0.42 SLA was not verified for counters with process name and it is not part of the top process.  --14-Feb-2019
//V2.0.41 For metric with process name parent validation was missing to check the existance of process and due to that non failed condiation is validating as part of sla - Additional validation implemented to check the existance of the process  --24-Jul-2018
//V2.0.40 Log added for the receiving the response from server when ever new request is intiated from agent --27-Jun-2018
//V2.0.39 Metrics are captured for those process that are configured in the config file. sla for process name introduced --18-Jun-2018
//V2.0.38 When not available exception message added along with countervalue --02-Jun-2018
//V2.0.37 when process that is getting collected, get killed or not available, sending counter value as zero for all the instances that is associated with category process --02-Jun-2018
//V2.0.36 added open and close connection before exeution. based on the error received at Mannai client on 17-May-2018
//V2.0.35 DB lock query simplified based on Kaushik feedback. 
//V2.0.34 Added the lock queries. configuration file added a key for dbName. this is must to get the locked queries. <add key="SqlDbName" value="master" />
//V2.0.33 Commented the exception added to the counter list to avoid counter_type going with string instead of values - problem observed on12-Aug-2017 when we run along with stack trace.

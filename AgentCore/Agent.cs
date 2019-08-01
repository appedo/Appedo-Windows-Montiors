using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Management;
using System.Configuration;
using Microsoft.Win32;
using System.DirectoryServices;
using System.Net.NetworkInformation;
using System.IO;
using System.Threading.Tasks;

namespace AgentCore
{
    public class ProcessMetricClass
    {
        public string counter_id { get; set; }
        public PerformanceCounter pcnter { get; set; }
    }
    public class processMetricList
    {
        public string counter_id { get; set; }
        public string process_metric_name { get; set; }
        public string instance_name { get; set; }
    }

    public class sqlLockDataClass
    {
        public string db_name { get; set; }
        public string connect_time { get; set; }
        public string client_net_address { get; set; }
        public int wait_duration_ms { get; set; }
        public int request_session_id { get; set; }
        public int blocking_session_id { get; set; }
        public string command { get; set; }
        public string status { get; set; }
        public string resource_type { get; set; }
        public string request_query { get; set; }
        public string blocking_query { get; set; }
        public string request_mode { get; set; }
    }

    public class sqlWaitStatClass
    {
        public string appedo_received_on { get; set; }
        public string db_name { get; set; }
        public string status { get; set; }
        public int count { get; set; }
    }

    public class Agent
    {
        #region The private fields

        private Utility _constants = Utility.GetInstance();
        private Dictionary<string, PerformanceCounter> _countersWindows = new Dictionary<string, PerformanceCounter>();
        private Dictionary<string, PerformanceCounter> _countersMSSQL = new Dictionary<string, PerformanceCounter>();
        private Dictionary<string, PerformanceCounter> _countersMSIIS = new Dictionary<string, PerformanceCounter>();
        private List<ProcessMetricClass> _metricProcess = new List<ProcessMetricClass>();
        private List<ProcessMetricClass> _metricLogicalDisk = new List<ProcessMetricClass>();
        private List<ProcessMetricClass> _metricTopProcess = new List<ProcessMetricClass>();
        private List<processMetricList> listProcessMetrics = new List<processMetricList>();
        //        private Dictionary<string, string> _networkCounters = new Dictionary<string, string>();
        private List<SLASetNew> _slaCountersWin = new List<SLASetNew>();
        private List<SLASetNew> _slaCountersMSSQL = new List<SLASetNew>();
        private List<SLASetNew> _slaCountersMSIIS = new List<SLASetNew>();
        private Dictionary<string, List<PerformanceCounter>> CountersAllInstance = new Dictionary<string, List<PerformanceCounter>>();
        private string _path = string.Empty;
        private string _dataSendUrl = string.Empty;
        private string _slaDataSendUrl = string.Empty;
        private string _counterValueWindows = string.Empty;
        private string _counterValueMSSQL = string.Empty;
        private string _counterValueMSIIS = string.Empty;
        private volatile bool _isFirstCounterValue = true;
        private bool _isFirstCounterMSIIS = true;
        private bool _isFirstCounterMSSQL = true;
        private string _responseStr = string.Empty;
        private Thread _doWorkThread;
//        private string APPEDO_QRY = "/* APPEDO */ ";
        public static string _uuid = string.Empty;
        private string _guid_mssql = string.Empty;
        private string _guid_msiis = string.Empty;
        private string _guid_windows = string.Empty;
        private bool _isMSSQLExist = false;
        private bool _isMSIISAppExist = false;
        private string _mssqlRunStatus = "RUNNING";
        private string _msiisRunStatus = "RUNNING";
        private string _windowsRunStatus = "RUNNING";
        private List<dftCnterList> dftCntSet = new List<dftCnterList>();
        CountersDetailNew respCounterSet = null;
        private SqlConnection _dbConnection = null;
        private bool _mssqlConnectionException = false;
        private bool _debugMode = false;
 //       private bool _getThreadDetails = false;
        private string _processNames = string.Empty;
        private string _processNameBfrMod = string.Empty;
        private string _iisVersion = string.Empty;
        private string _mssqlVersion = string.Empty;
        private int _mssqlMjrVer = 0;
        private List<string> _mssqlInstance = new List<string>();
        public static bool _runCond = true;
        private Thread threadTopProcess;
//        private List<string> _topProcessCnterId = new List<string>();
        private Hashtable _topProcessCnterId = new Hashtable();
        private string _connectionString = string.Empty;
        private bool _runICMPTest = false;
        #endregion
  
        #region The constructor

        /// <summary>
        /// Agent constructor. Depend on type it work.
        /// </summary>
        /// <param name="guid">Guid from appedo</param>
        /// <param name="type">WINDOWS, MSSQL, MSIIS</param>
        public Agent(string cmd)
        {
            if (cmd == "start")
            {
                while (_uuid == string.Empty)
                {
                    getUUID();
                    if (_uuid == string.Empty)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent()\tNot getting System UUID, hence will retry in 10 sec");
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent()\tNot getting System UUID, hence will retry in 10 sec");
                        Thread.Sleep(10000);
                    }
                }
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tAgent()\tAgent running in" + Environment.OSVersion);
                setDebugMode();
                //            setThreadMode();
                Thread tSQD = new Thread(StartQueueDrainer);
                tSQD.Start();
                _runICMPTest =  Convert.ToBoolean(ConfigurationManager.AppSettings["RunICMPTest"]);
                if (_runICMPTest)
                {
                    Thread ICMPTest = new Thread(PingService);
                    ICMPTest.Start();
                }
            }
        }
        public void setDebugMode()
        {

            ConfigurationManager.RefreshSection("appSettings");
            bool chMade = _debugMode;

            bool res = bool.TryParse(ConfigurationManager.AppSettings["debugmode"], out _debugMode);
            if (!res)
            {
                if (chMade != _debugMode)
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tAgent()\tdebug mode set as " + ConfigurationManager.AppSettings["debugmode"] + "and it is invalid , hence set with false");
            }
            else
            {
                if (chMade != _debugMode)
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tAgent()\tdebug mode is set as " + _debugMode);
            }
        }

        public void setProcessNames()
        {
            ConfigurationManager.RefreshSection("appSettings");
            bool runStatusBefore = _runICMPTest;
            _runICMPTest = Convert.ToBoolean(ConfigurationManager.AppSettings["RunICMPTest"]);
            if (_runICMPTest && runStatusBefore != _runICMPTest)
            {
                Thread ICMPTest = new Thread(PingService);
                ICMPTest.Start();
            }
            string processNames = _processNameBfrMod;
            bool processChanged = false;
            _processNameBfrMod = System.Configuration.ConfigurationManager.AppSettings["processNames"];
            if (processNames != _processNameBfrMod) processChanged = true;

            if (processChanged)
            {
                _processNames = _processNameBfrMod;
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetProcessNames()\tNew Process metrics are " + _processNameBfrMod);

                //if (_isMSSQLExist)
                //{
                //    if (!_processNames.Contains("sqlservr"))
                //        if (_processNames == string.Empty) _processNames = "sqlservr";
                //        else
                //            _processNames += ",sqlservr";
                //}
                //else
                //    if (_processNames.Contains("sqlservr"))
                //        _processNames = _processNames.Replace("sqlservr,", "").Replace("sqlservr", "");

                //if (_isMSIISAppExist)
                //{
                //    if (!_processNames.Contains("inetinfo"))
                //        if (_processNames == string.Empty) _processNames = "inetinfo";
                //        else _processNames += ",inetinfo";

                //    if (!_processNames.Contains("w3wp"))
                //        _processNames += ",w3wp";

                //}
                //else
                //{
                //    if (_processNames.Contains("w3wp"))
                //        _processNames = _processNames.Replace("w3wp,", "").Replace("w3wp", "");

                //    if (_processNames.Contains("inetinfo"))
                //        _processNames = _processNames.Replace("inetinfo,", "").Replace("inetinfo", "");
                //}
                if (_processNames.Count() > 0 && listProcessMetrics.Count() > 0)
                {
                    _metricProcess.Clear();
                    setProcessInstances(listProcessMetrics);
                }
            }
        }

        public void StartQueueDrainer()
        {
            DataFileHandler.dataQueueWriteToFile();
            DataFileHandler.notifnQWriteToFile();
            DataFileHandler.debugQWriteToFile();
            ExceptionHandler.LogErrors();
        }
        public void getUUID()
        {
            try
            {
                ManagementScope Scope = null;
                ObjectQuery Query = new ObjectQuery("SELECT  UUID FROM Win32_ComputerSystemProduct");
                ManagementObjectSearcher Searcher = new ManagementObjectSearcher(Scope, Query);
                foreach (ManagementObject WmiObject in Searcher.Get())
                {
                    _uuid = WmiObject["UUID"].ToString();
                }
                Searcher = null;
                Scope = null;
                Query = null;
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetUUID()\tSystem UUID : " + _uuid);
            }
            catch (Exception e)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetUUID()\tException Received.  " + e.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\t" + "getUUID()\tException Received\t" + e.Message +"\t"+e.StackTrace);
            }

        }

        public void getConfigurations()
        {
            string pageContent = string.Empty;
            while (pageContent == string.Empty)
            {
                //UnifiedAgentFirstRequest
                pageContent = _constants.GetPageContent(_path = GetPath() + "/getConfigurationsV2", string.Format("uuid={0}&command=UnifiedAgentFirstRequest&eid={1}", _uuid, System.Configuration.ConfigurationManager.AppSettings["eid"]));
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetConfigurations()\t" + pageContent);
                //string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                //pageContent = System.IO.File.ReadAllText(baseDir + @"\PageContentResponse.txt");
                if (_debugMode)
                {
                    DataFileHandler.sendToDebugQ("Received Response - GetConfiguration");
                    DataFileHandler.sendToDebugQ(pageContent);
                }

                if (pageContent == string.Empty || pageContent.ToUpper().Replace(" ", "").Contains("CONTACTADMINISTRATOR") || pageContent.ToUpper().Contains("INPROGRESS"))
                {
                    if (pageContent == string.Empty ||pageContent.ToUpper().Contains("INPROGRESS")  )
                    {
                        if (pageContent==string.Empty)
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent()\tget pageContent response is empty ; Cause might be Server not responding -- hence will retry in 10 sec ");
                        else
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent()\tServer is yet to complete the process -- hence will retry in 10 sec ");
                        Thread.Sleep(10000);
                        pageContent = string.Empty;
                    }
                    else
                    {
                        respCounterSet = Utility.GetInstance().Deserialize<CountersDetailNew>(pageContent); //new code 03-May-2017
                        if (respCounterSet.message != string.Empty && respCounterSet.message != null)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tsetAgent()\t" + respCounterSet.message + ", hence service stopped");
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tsetAgent()\t" + respCounterSet.message + ", hence service Stopped");
                            Thread.Sleep(10000);
                            Environment.Exit(1);
                        }
                    }
                }
            }
            //Deserialize received response to CountersDetail object.
            // CountersDetail detail = Utility.GetInstance().Deserialize<CountersDetail>(pageContent); //Old Code before 03-May-2017
            respCounterSet = Utility.GetInstance().Deserialize<CountersDetailNew>(pageContent); //new code 03-May-2017
            setGUID(respCounterSet);
        }
        public void setAgent()
        {
//            DateTime startTime = DateTime.Now;
            ServicePointManager.ServerCertificateValidationCallback = ((sender, certificate, chain, sslPolicyErrors) => true);
            isMSSQLExists(); // set the global variable _isMSSQLExist
            isMSIISAppRunning(); //Set the global variable _isMSIISAppExist
            try
            {
                _dataSendUrl = GetPath() + "/collectCountersV2";
                getConfigurations(); //gets the configuration details from server
                try
                {
                    if (_guid_windows != string.Empty)
                        SetWindowsCntList(respCounterSet);
                    if (_guid_mssql != string.Empty && _isMSSQLExist)
                        SetMSSQLCntList(respCounterSet);
                    if (_guid_msiis != string.Empty && _isMSIISAppExist)
                        SetMSIISCntList(respCounterSet);

                    if ((_isMSIISAppExist && _guid_msiis == string.Empty) || (_isMSSQLExist && _guid_mssql == string.Empty) || _guid_windows == string.Empty)
                    {
                        getAllPerformanceCounter();
                    }
                }
                catch (Exception ex)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical" + Environment.MachineName + "\tsetAgent()\tException Received from second try"+ ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tsetAgent()\tException Received from second try\t" + ex.Message + "\t" + ex.StackTrace);
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical" + Environment.MachineName + "\tsetAgent()\tException Received from first try" + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tsetAgent()\tException Received from first try\t" + ex.Message + "\t" + ex.StackTrace);
            }
        }

        public void setGUID(CountersDetailNew respCnt)
        {
            if (respCnt != null)
            {
                if (respCnt.WINDOWS != null)
                    _guid_windows = respCnt.WINDOWS.guid != null ? respCnt.WINDOWS.guid : string.Empty;
                if (respCnt.MSIIS != null)
                {
                    _guid_msiis = respCnt.MSIIS.guid != null ? respCnt.MSIIS.guid : string.Empty;
                    if (!_isMSIISAppExist && _guid_msiis != string.Empty)
                    {
                        _responseStr =_constants.GetPageContent(_path = GetPath() + "/getConfigurationsV2", string.Format("guid={0}&command=PROCESSNOTRUNNING", _guid_msiis));
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetAgent()\tMSIIS NOT RUNNING, guid received, Message sent to Server, Metrics will not be collected");
                        _guid_msiis = string.Empty;
                    }
                }
                if (respCnt.MSSQL != null)
                {
                    _guid_mssql = respCnt.MSSQL.guid != null ? respCnt.MSSQL.guid : string.Empty;
                    if (!_isMSSQLExist && _guid_mssql != string.Empty)
                    {
                        _responseStr = _constants.GetPageContent(_path = GetPath() + "/getConfigurationsV2", string.Format("guid={0}&command=PROCESSNOTRUNNING", _guid_mssql));
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetAgent()\tMSSQL NOT RUNNING, guid received, Message sent to Server, Metrics will not be collected ");
                        _guid_mssql = string.Empty;
                    }
                }
            }
            else
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + " \tsetAgent() respCounterSet object is null and this is very rare Event. Service Stopped, Try after some time");
                Environment.Exit(1);
            }

        }
        #endregion

        #region  The public methods

        /// <summary>
        /// Start agent to collect and send counter value.
        /// </summary>
        public void StartAgent()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tStartAgent()\tStarting Agents....");
            _doWorkThread = new Thread(new ThreadStart(setAgent));
            _doWorkThread.Start();
            //setAgent();
        }
        public void StopAgent()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tStopAgent()\tStopping Agents and will stop in 15 Sec......");
            Thread.Sleep(15000);
            _runCond = false;
        }
        #region checkMSSQLAvailablity & MIIS
        public void isMSSQLExists()
        {
            try
            {
                Process[] sqlServer = Process.GetProcessesByName("sqlservr");
                if (sqlServer.Count() > 0)
                {
                    _isMSSQLExist = true;
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetAgent()\tMSSQL Exists, hence collecting their Metrics");
                    //            RegistryKey RK = Registry.local.OpenSubKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\MICROSOFT\Microsoft SQL Server");
                    RegistryView registryView = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
                    using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
                    {
                        RegistryKey instanceKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL", false);
                        if (instanceKey != null)
                        {
                            if (instanceKey.GetValueNames().Count() > 0)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tisMSSQLExists()\tMSSQL has " + instanceKey.GetValueNames().Count() + " instances ");
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tisMSSQLExists()\tMSSQL Instance Names are ");
                                foreach (var instanceName in instanceKey.GetValueNames())
                                {
                                    _mssqlInstance.Add(instanceName);
                                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tisMSSQLExists()\t" + Environment.MachineName + @"\" + instanceName);
                                }
                            }
                        }
                    }
                    ConnectDataBase();
                }
                else
                {
                    _isMSSQLExist = false;
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + " \tsetAgent()\tMSSQL does not Exist or Service not Running, hence Metrics are not collected, System will check for its availability at pre scheduled time");
                }
                sqlServer = null;
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tisMSSQLExists()\texception received " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tisMSSQLExists()\texception received\t" + ex.Message + "\t" + ex.StackTrace);
            }
        }
        public void isMSIISAppRunning()
        {
            try
            {
                Process[] processIIS = Process.GetProcessesByName("w3wp");
                if (processIIS.Count() > 0)
                    _isMSIISAppExist = true;
                else
                {
                    processIIS = Process.GetProcessesByName("inetinfo");
                    if (processIIS.Count() > 0) _isMSIISAppExist = true;
                }
                if (_isMSIISAppExist)
                {
                    try
                    {
                        RegistryKey iisKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\InetStp");
                        _iisVersion = iisKey.GetValue("SetupString").ToString();

                        var iis = new DirectoryEntry("IIS://" + Environment.MachineName + "/w3svc");
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tisMSIISAppRunning()\tIIS Version " + _iisVersion);
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tisMSIISAppRunning()\tSite Name - Server State");
                        foreach (DirectoryEntry site in iis.Children)
                        {
                            if (site.SchemaClassName.ToLower() == "iiswebserver")
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tisMSIISAppRunning()\t" + site.Name + " - " + site.Properties["ServerState"].Value);
                            }
                        }
                        iis.Close();
                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tisMSIISAppRunning()\tException in getting name of running IIS applicaiton " + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tisMSIISAppRunning()\tException in getting name of running IIS applicaiton\t" + ex.Message + "\t" + ex.StackTrace);
                    }
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetAgent()\tMSIIS Application Running, hence collecting their Metrics");

                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tisMSIISAppRunning()\texception received " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tisMSIISAppRunning()\texception received\t" + ex.Message + "\t" + ex.StackTrace);
            }
        }

        #endregion
        #region CheckPreviousVersionForMigration
        public string getOldGuid(string modType)
        {
            List<string> whiteLabel = new List<string>();
            whiteLabel.Add("APPEDO"); whiteLabel.Add("AppDiagnos");
            Process[] preVer;
            string oldGuid = string.Empty;
            foreach(string wl in whiteLabel)
            {
                preVer = Process.GetProcessesByName(wl + "_"+modType+"_AGENT");
                foreach (Process ver in preVer)
                {
                    string fileName = ver.MainModule.FileName;
                    string filePath = fileName.Substring(0, fileName.IndexOf(ver.MainModule.ModuleName));
                    ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                    configMap.ExeConfigFilename = filePath + ver.MainModule.ModuleName + ".config";
                    Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
                    if (modType=="WINDOWS")
                        oldGuid = config.AppSettings.Settings["guid"].Value;
                    else if (modType == "MSSQL")
                        oldGuid = config.AppSettings.Settings["guid"].Value;
                    else
                        oldGuid = config.AppSettings.Settings["guid"].Value;
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetOldGuid()-"+modType+"\tOld Version "+ver.ProcessName+" "+ ver.MainModule.FileVersionInfo.FileVersion+" Running. Version upgraded to Latest. Running version will be uninstalled. ");
                    ver.Kill();
                    new Thread(() =>
                    {
                        try
                        {
                            Stopwatch timeToGetPC = new Stopwatch();
                            timeToGetPC.Start();
                            string unInstallPcode = GetProductCode(ver.ProcessName);

                            //Uninsallation of agent 
                            System.Diagnostics.Process process = new System.Diagnostics.Process();
                            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                            startInfo.FileName = "cmd.exe";
                            startInfo.UseShellExecute = false;
                            startInfo.Arguments = "/C msiexec.exe /quiet /X " + unInstallPcode;
                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                            timeToGetPC.Stop();
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetOldGuid()- " + modType + "\tTime taken to get the product code and uninstallation of " + ver.ProcessName + " PID " + ver.Id + " is " + timeToGetPC.ElapsedMilliseconds + " ms");
                        }
                        catch(Exception ex)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetOldGuid()- " + modType + "\tOld Version " + ver.ProcessName + " " + ver.MainModule.FileVersionInfo.FileVersion + " uninstallation failed. "+ex.Message);
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetOldGuid()-" + modType + "\tOld Version " + ver.ProcessName + " " + ver.MainModule.FileVersionInfo.FileVersion + " uninstallation failed. " + ex.Message+ ex.StackTrace);
                        }
                    }).Start();
                }
            }
            return oldGuid;
        }

        public string GetProductCode(string productName)
        {
            //getting the product code takes some time and hence made as separate thread
            string query = string.Format("select * from Win32_Product where Name='{0}'", productName);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject product in searcher.Get())
                {
                    return product["IdentifyingNumber"].ToString();
                }
            }
            return null;
        }
        #endregion
        #region defaultCounterSetting
        public class dftCnterList
        {
            public dftCnterList(string category, string counterName)
            {
                this.category = category;
                this.counterName = counterName;
            }
            public string category { get; set; }
            public string counterName { get; set; }
        }
        // for the below counters in the list, is_selected will be set to true
        public void setDftCntersList()
        {
            dftCntSet.Add(new dftCnterList("ASP.NET Applications", "Pipeline Instance Count"));
            dftCntSet.Add(new dftCnterList("ASP.NET Applications", "Requests/Sec"));
            dftCntSet.Add(new dftCnterList("ASP.NET Applications", "Sessions Active"));
            dftCntSet.Add(new dftCnterList("ASP.NET Applications", "Transactions/Sec"));
            dftCntSet.Add(new dftCnterList("ASP.NET Applications", "Errors Total/Sec"));
            dftCntSet.Add(new dftCnterList("ASP.NET", "Application Running"));
            dftCntSet.Add(new dftCnterList("ASP.NET", "Requests Queued"));
            dftCntSet.Add(new dftCnterList("ASP.NET", "Request Wait Time"));
            dftCntSet.Add(new dftCnterList("ASP.NET", "Worker Process Running"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Errors/Sec"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Request Execution Time"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Request Wait Time"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Request Queued"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Request/Sec"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Sessions Current"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Transactions/Sec"));
            dftCntSet.Add(new dftCnterList("Active Server Pages", "Transactions Pending"));
            dftCntSet.Add(new dftCnterList("APP_POOL_WAS", "Current Application Pool State"));
            dftCntSet.Add(new dftCnterList("APP_POOL_WAS", "Current Worker Process"));
            dftCntSet.Add(new dftCnterList("APP_POOL_WAS", "Recent Worker Process Failure"));
            dftCntSet.Add(new dftCnterList("Web Service", "Current Connections"));
            dftCntSet.Add(new dftCnterList("Web Service", "Bytes Total/sec"));
            dftCntSet.Add(new dftCnterList("Web Service", "Get Requests/sec"));
            dftCntSet.Add(new dftCnterList("Web Service", "Post Requests/sec"));
            dftCntSet.Add(new dftCnterList(".NET CLR Memory", "# Bytes in all Heaps"));
            dftCntSet.Add(new dftCnterList(".NET CLR Memory", "# Total Reserved Bytes"));
            dftCntSet.Add(new dftCnterList(".NET CLR Memory", "Allocated Bytes/Sec"));
            dftCntSet.Add(new dftCnterList(".NET CLR LocksAndThreads", "# of current recognized threads"));
            dftCntSet.Add(new dftCnterList(".NET CLR LocksAndThreads", "Current Queue Length"));
            dftCntSet.Add(new dftCnterList(".NET CLR LocksAndThreads", "Queue Length / sec"));
            dftCntSet.Add(new dftCnterList(".NET CLR LocksAndThreads", "rate of recognized threads / sec"));
            dftCntSet.Add(new dftCnterList(".NET CLR Exceptions", "# of Exceps Thrown / sec"));
            dftCntSet.Add(new dftCnterList("Processor", "% Processor Time"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "% Free Space"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "Free Megabytes"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "Current Disk Queue Length"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "% Disk Time"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "Disk Transfers/sec"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "Avg. Disk sec/Transfer"));
            dftCntSet.Add(new dftCnterList("LogicalDisk", "Avg. Disk Bytes/Transfer"));
            dftCntSet.Add(new dftCnterList("System", "Context Switches/sec"));
            dftCntSet.Add(new dftCnterList("System", "Processor Queue Length"));
            dftCntSet.Add(new dftCnterList("Memory", "Page Faults/sec"));
            dftCntSet.Add(new dftCnterList("Memory", "Pages/sec"));
            dftCntSet.Add(new dftCnterList("Memory", "Available MBytes"));
            dftCntSet.Add(new dftCnterList("Process", "% Processor Time"));
            dftCntSet.Add(new dftCnterList("Process", "ID Process"));
            dftCntSet.Add(new dftCnterList("Process", "I/O Data Bytes/Sec"));
            dftCntSet.Add(new dftCnterList("Process", "Private Bytes"));
            dftCntSet.Add(new dftCnterList("Process", "Thread Count"));
            dftCntSet.Add(new dftCnterList("Thread", "% Processor Time"));
            dftCntSet.Add(new dftCnterList("Thread", "ID Process"));
            dftCntSet.Add(new dftCnterList("Thread", "ID Thread"));
            dftCntSet.Add(new dftCnterList("Thread", "Context Switches/Sec"));
            dftCntSet.Add(new dftCnterList("Thread", "Thread State"));
            dftCntSet.Add(new dftCnterList("Thread", "Thread Wait Reason"));
            //for setting MSSQL default counter set for each available instance
            foreach (string mssqlInstance in _mssqlInstance)
            {
                string newInstance = string.Empty;
                if (mssqlInstance != "MSSQLSERVER")
                    newInstance = "MSSQL$" + mssqlInstance;
                else newInstance = "SQLSERVER";

                dftCntSet.Add(new dftCnterList(newInstance + ":Locks", "Average Wait Time (ms)"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Locks", "Lock Waits/sec"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Locks", "Number of Deadlocks/sec"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Locks", "Lock Timeouts/sec"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Latches", "Average Latch Wait Time (ms)"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Latches", "Latch Waits/sec"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Buffer Manager", "Page life expectancy"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Buffer Manager", "Buffer cache hit ratio"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Databases", "Active Transactions"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Databases", "Transactions/sec"));
                dftCntSet.Add(new dftCnterList(newInstance + ":General Statistics", "HTTP Authenticated Requests"));
                dftCntSet.Add(new dftCnterList(newInstance + ":General Statistics", "Processes blocked"));
                dftCntSet.Add(new dftCnterList(newInstance + ":General Statistics", "User Connections"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Memory Manager", "Database Cache Memory (KB)"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Memory Manager", "Free Memory (KB)"));
                dftCntSet.Add(new dftCnterList(newInstance + ":Memory Manager", "Target Server Memory (KB)"));
            }
        }

        #endregion
        #region getAllPerformanceCounter
        /// <summary>
        /// Collection of Windows, MSSQL, MSSIIS Performance Counter and sent to apm server
        /// </summary>
        public void getAllPerformanceCounter()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tStarted");
            Stopwatch timerAllPC = new Stopwatch();
            timerAllPC.Start();
            setDftCntersList();
            StringBuilder cntCommon = new StringBuilder();
            StringBuilder cntCollecMSSQL = new StringBuilder();
            StringBuilder cntCollecMSIIS = new StringBuilder();
            StringBuilder cntCollecWindows = new StringBuilder();
            string eid;
            eid = ConfigurationManager.AppSettings["eid"];
            try
            {
                PerformanceCounterCategory[] categories = PerformanceCounterCategory.GetCategories();
                string modType;
                cntCollecMSSQL.Append("{\"command\":\"MSSQL\",\"uuid\":\"").Append(_uuid).Append("\",\"oldGuid\":\"").Append(getOldGuid("MSSQL")).Append("\",\"eid\":\"").Append(eid).Append("\",\"version\":\"").Append(_mssqlVersion).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",\"counterSet\":");
                cntCollecMSIIS.Append("{\"command\":\"MSIIS\",\"uuid\":\"").Append(_uuid).Append("\",\"oldGuid\":\"").Append(getOldGuid("MSIIS")).Append("\",\"eid\":\"").Append(eid).Append("\",\"version\":\"").Append(_iisVersion).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",\"counterSet\":");
                cntCollecWindows.Append("{\"command\":\"WINDOWS\",\"uuid\":\"").Append(_uuid).Append("\",\"oldGuid\":\"").Append(getOldGuid("WINDOWS")).Append("\",\"eid\":\"").Append(eid).Append("\",\"version\":\"").Append(Environment.OSVersion.ToString()).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",\"counterSet\":");
                cntCollecMSSQL.Append("{\"counterData\":[");
                cntCollecMSIIS.Append("{ \"counterData\":[");
                cntCollecWindows.Append("{\"counterData\":[");
                string desc, unit;
                int cntCategory = 0;
                foreach (PerformanceCounterCategory category in categories)
                //                System.Threading.Tasks.Parallel.ForEach(categories, category => //this is for parrallel threading 
                {
                    bool skipCategory = false;
                    if (category.CategoryName.Contains("MSSQL") || category.CategoryName.Contains("SQL") || category.CategoryName.Contains("database"))
                    {
                        modType = "MSSQL";
                        cntCommon = cntCollecMSSQL;
                        if (_guid_mssql != string.Empty || !_isMSSQLExist) { skipCategory = true; }

                    }
                    else if (category.CategoryName.Contains(".NET") || category.CategoryName.Contains("HTTP") || category.CategoryName.Contains("Active Server Pages") || category.CategoryName.Contains("Web Service")
                            || category.CategoryName.Contains("APP_POOL") || category.CategoryName.StartsWith("Internet"))
                    {
                        modType = "MSIIS";
                        cntCommon = cntCollecMSIIS;
                        if (_guid_msiis != string.Empty || !_isMSIISAppExist) { skipCategory = true; }
                    }
                    else
                    {
                        modType = "WINDOWS";
                        cntCommon = cntCollecWindows;
                        if (_guid_windows != string.Empty) { skipCategory = true; }
                    }
                    if (!skipCategory)
                    {
                        string insName = string.Empty, hasIns = "f", qryIns = "FALSE";
                        string[] insNames;
                        string categoryType = category.CategoryType.ToString();

                        if (category.InstanceExists("_Total") && categoryType == "MultiInstance") { insName = "_Total"; }
                        else if (category.InstanceExists("_Global_") && categoryType == "MultiInstance") { insName = "_Global_"; }
                        else if (category.InstanceExists("__Total__") && categoryType == "MultiInstance") { insName = "__Total__"; }
                        else if (categoryType == "MultiInstance" && category.CategoryName != "LogicalDisk")
                        {
                            insNames = category.GetInstanceNames();
                            if (insNames.Length > 0) { insName = insNames[0]; }
                        }
                        else { insName = string.Empty; }


                      PerformanceCounter[] counterNames = null;
                        try
                        {
                            if (categoryType == "MultiInstance" && insName != string.Empty)
                            {
                                hasIns = "t";
                                qryIns = "TRUE";
                                counterNames = category.GetCounters(insName);
                            }
                            else if (categoryType == "SingleInstance")
                            {
                                counterNames = category.GetCounters();
                            }
                            else { counterNames = null; }
                        }
                        catch (Exception ex)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tcategoryName :" + category.CategoryName + " :: instanceName :" + insName + " has exception " + ex.Message);
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tcategoryName :" + category.CategoryName + " :: instanceName :" + insName + " has exception\t" + ex.Message + "\t" + ex.StackTrace);
                        }
                        int cntCounters = 0;
                        string counterType = string.Empty, isSelected = "f", statusCnter = "f";
                        Boolean skipCounter = false;

                        if (counterNames != null)
                        {
                            //if (insName != "_Total" && insName != "_Global_" && insName != "__Total__") { insName = string.Empty; }
                            //else { insName=string.Empty; }

                            foreach (var counter in counterNames)
                            {
                                try
                                {
                                    counterType = counter.CounterType.ToString();
                                    skipCounter = false;
                                }
                                catch (Exception ex)
                                {
                                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tcategoryName :" + category.CategoryName + " :: instanceName :" + insName + "::counterName :" + counter.CounterName + "(" + counterNames.Length + ")counters has no valid counter type. " + ex.Message);
                                    skipCounter = true;
                                }
                                if (!skipCounter)
                                {
                                    if (counter.CounterHelp.Contains("\\") || counter.CounterHelp.Contains("\""))
                                    {
                                        desc = counter.CounterHelp.Replace("\\", "-");
                                        desc = desc.Replace("\"", " ");
                                    }
                                    else { desc = counter.CounterHelp; }

                                    // to set the default selected counters
                                    bool isExist = dftCntSet.Any(x => x.category.ToUpper() == category.CategoryName.ToUpper() && x.counterName.ToUpper() == counter.CounterName.ToUpper());
                                    if (isExist)
                                    {
                                        isSelected = "t";
                                        if (category.CategoryName == "APP_POOL_WAS" && counter.CounterName == "Current Application Pool State") { statusCnter = "t"; }
                                        else if (category.CategoryName == "Process" && counter.CounterName == "ID Process") { statusCnter = "t"; }
                                        else if (category.CategoryName == "Thread" && (counter.CounterName == "ID Process" || counter.CounterName == "ID Thread")) { statusCnter = "t"; }
                                    }
                                    else { statusCnter = "f"; isSelected = "f"; }
                                    // to get the units of measurement -- This takes considerable time to capture information.
                                    if (counter.CounterName.ToString().Contains("%") || counter.CounterHelp.ToUpper().Contains("PERCENTAGE")) { unit = "%"; }
                                    else if (counter.CounterType.ToString().StartsWith("RateOfCountsPerSecond")) { unit = "CountPerSec"; }
                                    else if (counter.CounterName.ToUpper().Contains("RATIO")) { unit = "ratio"; }
                                    else if (counter.CounterHelp.Contains("nanoseconds")) { unit = "nanoseconds"; }
                                    else if (counter.CounterHelp.Contains("microseconds")) { unit = "microseconds"; }
                                    else if (counter.CounterHelp.Contains("milliseconds")) { unit = "milliseconds"; }
                                    else if (counter.CounterHelp.Contains(" seconds")) { unit = "seconds"; }
                                    else if (counter.CounterHelp.Contains("Megabytes") || (counter.CounterName.ToUpper().Contains("MEGABYTES"))) { unit = "MB"; }
                                    else if (counter.CounterHelp.Contains("KB") || counter.CounterHelp.Contains("kilobytes") || (counter.CounterName.ToUpper().Contains("KB"))) { unit = "KB"; }
                                    else if (counter.CounterHelp.Contains("number of bytes") || counter.CounterHelp.Contains("byte size") || counter.CounterHelp.Contains("in bytes")) { unit = "bytes"; }
                                    else { unit = "number"; }

                                    cntCommon.Append("{\"category\":\"");
                                    cntCommon.Append(category.CategoryName);
                                    cntCommon.Append("\",\"counter_name\":\"");
                                    cntCommon.Append(counter.CounterName);
                                    cntCommon.Append("\",\"has_instance\":\"");
                                    cntCommon.Append(hasIns);
                                    cntCommon.Append("\",\"instance_name\":\"");
                                    cntCommon.Append(insName);
                                    cntCommon.Append("\",\"unit\":\"");
                                    cntCommon.Append(unit);
                                    cntCommon.Append("\",\"is_selected\":\"");
                                    cntCommon.Append(isSelected);
                                    cntCommon.Append("\",\"is_static_counter\":\"");
                                    cntCommon.Append(statusCnter);
                                    cntCommon.Append("\",\"query_string\":\"");
                                    cntCommon.Append(qryIns).Append(",").Append(category.CategoryName).Append(",").Append(counter.CounterName).Append(",").Append(insName);
                                    cntCommon.Append("\",\"counter_description\":\"");
                                    cntCommon.Append(desc).Append("\"},");
                                    if (_debugMode)
                                    {
                                        DataFileHandler.sendToDebugQ(modType + "\t" + qryIns + "\t" + category.CategoryName + "\t" + counter.CounterName + "\t" + insName);
                                    }
                                }
                                else
                                {
                                    break; // to skip the for each loop
                                }
                                cntCounters++;
                            }
                        }
                        else
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tcategoryName :" + category.CategoryName + " :: instanceName :" + insName + "  has no instances available");
                        }
                        if (modType == "MSSQL") { cntCollecMSSQL = cntCommon; }
                        else if (modType == "MSIIS") { cntCollecMSIIS = cntCommon; }
                        else { cntCollecWindows = cntCommon; }
                    } //skipCategory if condition end
                    cntCategory++;
                } // for each category loop ends
            }
            catch (Exception e)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tException in getting performance categories. " + e.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tException in getting performance categories.\t" + e.Message);
            }
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tcompleted, sending data to server..... ");

            cntCollecMSSQL.Append("]}}");
            cntCollecMSIIS.Append("]}}");
            cntCollecWindows.Append("]}}");
            cntCollecMSSQL = cntCollecMSSQL.Replace(",]}}", "]}}");
            cntCollecMSIIS = cntCollecMSIIS.Replace(",]}}", "]}}");
            cntCollecWindows = cntCollecWindows.Replace(",]}}", "]}}");

            ////Sending the data to server for further processing
            //StringBuilder data = new StringBuilder();
            //DataFileHandler.sendToNotifnQ("GuidWindows" + _guid_windows.ToString());
            //if (_debugMode)
            //    DataFileHandler.sendToDebugQ(cntCollecWindows.ToString());

            if (_guid_windows == string.Empty || _guid_windows == null)
            {
                //new Thread(() =>
                //{
                    string responseStr = string.Empty;
                    CountersDetailNew respCnt = new CountersDetailNew();
                    string cntSet = cntCollecWindows.ToString();
                    if (_debugMode)
                        DataFileHandler.sendToDebugQ(cntSet.ToString());
                    Stopwatch mswinresp = new Stopwatch();
                    mswinresp.Start();
                    int i = 0;
                    while (i < 5 && responseStr == string.Empty)
                    {
                        responseStr = _constants.GetPageContent(_dataSendUrl, cntSet);
                        if (_debugMode)
                        {
                            DataFileHandler.sendToDebugQ("Received Response - Windows");
                            DataFileHandler.sendToDebugQ(responseStr);
                        }
                        i++;
                        if (responseStr == null || responseStr == String.Empty)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tNo response from server, will try after 10 sec.");
                            Thread.Sleep(10000);
                        }
                    }
                    mswinresp.Stop();
                    if (responseStr != string.Empty && responseStr != null)
                    {
                        respCnt = Utility.GetInstance().Deserialize<CountersDetailNew>(responseStr); //new code 03-May-2017
                        if (respCnt.WINDOWS != null && respCnt.WINDOWS.guid != null && respCnt.WINDOWS.guid != string.Empty)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tWindows Data Sent, Windows GUID " + respCnt.WINDOWS.guid + " Time taken to get response is " + mswinresp.ElapsedMilliseconds + " ms");
                            setGUID(respCnt);
                            SetWindowsCntList(respCnt);
                        }
                        else
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tEmpty GUID received from server contact Administrator");
                    }
                    else
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tNo response from server, tried 5 times with 10 sec delay. Collection stopped for Windows");

                    responseStr = string.Empty;
                    cntCollecWindows.Clear();
                    respCnt = null;
                //}).Start();
            }
            if ((_guid_mssql == string.Empty || _guid_mssql == null) && _isMSSQLExist)
            {
                //new Thread(() =>
                //    {
                        string responseStr = string.Empty;
                        CountersDetailNew respCnt = new CountersDetailNew();
                        string cntSet = cntCollecMSSQL.ToString();
                        if (_debugMode)
                            DataFileHandler.sendToDebugQ(cntSet.ToString());
                        Stopwatch mssqlresp = new Stopwatch();
                        mssqlresp.Start();
                        int i = 0;
                        while (i < 5 && responseStr == string.Empty)
                        {
                            responseStr = _constants.GetPageContent(_dataSendUrl, cntSet.ToString());
                            if (_debugMode)
                            {
                                DataFileHandler.sendToDebugQ("Received Response - MSSQL");
                                DataFileHandler.sendToDebugQ(responseStr);
                            }
                            i++;
                            if (responseStr == null || responseStr == String.Empty)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()-MSIIS\tNo response from server, will try after 10 sec.");
                                Thread.Sleep(10000);
                            }
                        }
                        mssqlresp.Stop();
                        if (responseStr != string.Empty && responseStr != null)
                        {
                            respCnt = Utility.GetInstance().Deserialize<CountersDetailNew>(responseStr); //new code 03-May-2017
                            if (respCnt.MSSQL != null && respCnt.MSSQL.guid != null && respCnt.MSSQL.guid != string.Empty)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tMSSQL Data Sent, MSSQL GUID is " + respCnt.MSSQL.guid + ". Time taken to get response is " + mssqlresp.ElapsedMilliseconds + " ms");
                                setGUID(respCnt);
                                SetMSSQLCntList(respCnt);
                            }
                            else
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter() -MSSQL\tEmpty GUID received from server contact Administrator");
                        }
                        else
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter() - MSSQL\tNo response from server, tried 5 times with 10 sec delay. Collection stopped for MSSQL");
                        responseStr = string.Empty;
                        cntCollecMSSQL.Clear();
                        respCnt = null;
                    //}).Start();
            }
            if ((_guid_msiis == string.Empty || _guid_msiis == null) && _isMSIISAppExist)
            {
                //new Thread(() =>
                //{
                    string responseStr = string.Empty;
                    CountersDetailNew respCnt = new CountersDetailNew();
                    string cntSet = cntCollecMSIIS.ToString();
                    if (_debugMode)
                        DataFileHandler.sendToDebugQ(cntSet.ToString());
                    Stopwatch msiisresp = new Stopwatch();
                    msiisresp.Start();
                    int i = 0;
                    while (i < 5 && responseStr == string.Empty)
                    {
                        responseStr = _constants.GetPageContent(_dataSendUrl, cntSet.ToString());
                        if (_debugMode)
                        {
                            DataFileHandler.sendToDebugQ("Received Response - MSIIS");
                            DataFileHandler.sendToDebugQ(responseStr);
                        }
                        i++;
                        if (responseStr == null || responseStr == String.Empty)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter() - MSIIS\tNo response from server, will try after 10 sec.");
                            Thread.Sleep(10000);
                        }
                    }
                    //_responseStr = "Data Queued";
                    msiisresp.Stop();
                    if (responseStr != string.Empty && responseStr != null)
                    {
                        respCnt = Utility.GetInstance().Deserialize<CountersDetailNew>(responseStr); //new code 03-May-2017
                        if (respCnt.MSIIS != null && respCnt.MSIIS.guid != null && respCnt.MSIIS.guid != string.Empty)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tMSIIS Data Sent, MSIIS GUID is " + respCnt.MSIIS.guid + ". Time taken to get response is " + msiisresp.ElapsedMilliseconds + " ms");
                            setGUID(respCnt);
                            SetMSIISCntList(respCnt);
                        }
                        else
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter() -MSIIS\tEmpty GUID received from server contact Administrator");
                    }
                    else
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter() - MSIIS\tNo response from server, tried 5 times with 10 sec delay. Collection stopped for MSIIS");
                    responseStr = string.Empty;
                    cntCollecMSIIS.Clear();
                    respCnt = null;
                //}).Start();
            }
            cntCommon.Clear();
            timerAllPC.Stop();
            if (_debugMode)
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetAllPerformanceCounter()\tTime Taken to complete performance Counter Collection :" + timerAllPC.ElapsedMilliseconds + " ms");
            timerAllPC = null;
        }
        #endregion
        public processClass setMetricCounters(decimal metricValue, string key, string exception, string instanceName)
        {
            processClass processData = new processClass();
            processData.counter_type = key.ToString();
            processData.process_name = instanceName;
            processData.counter_value = metricValue;
            processData.exception = exception;
            //processData.process_id = string.Empty;
            //processData.thread_count = 0;
            //processData.handle_count = 0 ;
            return processData;
        }
        #endregion

        #region The private methods

        /// <summary>
        /// It collect and send counters values as well as sla breach counter set to appedo collector.
        /// </summary>
        private void CollectAndSendCounterWindows()
        {
            try
            {
                //DateTime startTime = DateTime.Now;
                //string dt = startTime.GetDateTimeFormats()[58];
                _counterValueWindows = getCounterValues(_countersWindows, _slaCountersWin, "WINDOWS");

                //If there is any counters value breach.
                if (_slaCountersWin.Count > 0)
                {
                    try
                    {
                        List<SLASetNew> slaCounterList = _slaCountersWin.FindAll(f => f.is_breach == true);
                        if (slaCounterList.Count > 0)
                        {
                            StringBuilder dataSLA = new StringBuilder();
                            dataSLA.Append("metrics###{\"mod_type\":\"WINDOWS\",\"type\":\"SLASet\",\"guid\":\"").Append(_guid_windows).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                            dataSLA.Append("\"SLASet\":");
                            dataSLA.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(slaCounterList)));
                            dataSLA.Append("}");
//                            _responseStr = _constants.GetPageContent(_dataSendUrl, data.ToString());
//                            _responseStr = "{\"success\": true, \"failure\": false, \"message\": \"Data sent.\"}";
                            DataFileHandler.sendToDataQueue(dataSLA.ToString());
                            _slaCountersWin.ForEach(f => f.is_breach = false);
                            dataSLA = null;
                        }
                        slaCounterList = null;
                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterWindows()" + "\t" + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterWindows()" + "\t" + ex.Message +"\t"+ ex.StackTrace);
                    }
                }

                //If It is first time sending data to Appedo collector, it will log data to our reference
                //&& respCounterSet.Windows.message=="Data queued" to be added after correction
                if (_isFirstCounterValue )
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendCounterWindows()\tFirstRequest Sucessfully Sent " +  _counterValueWindows.ToString());
                    _isFirstCounterValue = false;
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterWindows()\tException received. " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterWindows()\tException received" + "\t" + ex.Message +"\t"+ex.StackTrace);
            }
        }
        private string getCounterValues(Dictionary<string, PerformanceCounter> counterSet, List<SLASetNew> slaCounterSet, string modType)
        {
            Stopwatch timegetCountersValueWindows = new Stopwatch();
            timegetCountersValueWindows.Start();
            StringBuilder dataCounter = new StringBuilder();
            StringBuilder dataProcessThread = new StringBuilder();
            string guid;
            guid = modType == "MSSQL" ? _guid_mssql : modType == "MSIIS" ? _guid_msiis : _guid_windows;
            dataCounter.Append("metrics###{\"mod_type\":\"").Append(modType).Append("\",\"type\":").Append("\"MetricSet\"").Append(",\"guid\":\"").Append(guid).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\"");
            List<processClass> MetricSet = new List<processClass>();
//            dataCounter.Append("\"CounterSet\":[");
            
            decimal counterValues = 0;
            SLASetNew slaBreachWin = null;

            //Get all counter one by one
            int i = 0;
            foreach (string key in counterSet.Keys)
            {
                try
                {
                    i++;
                    //Get counter value.
                    int core = 1;
                    if (counterSet[key].CategoryName == "Process" && counterSet[key].CounterName =="% Processor Time")
                        core = Environment.ProcessorCount;
                    counterValues = Convert.ToDecimal(counterSet[key].NextValue())/core;
                    MetricSet.Add(setMetricCounters(counterValues, key.ToString(), string.Empty, counterSet[key].InstanceName == null ? string.Empty : counterSet[key].InstanceName));

                    //                    if (slaCounterSet != null && (slaBreachWin = slaCounterSet.Find(f => f.counter_id.ToString() == key && f.process_name == string.Empty)) != null )
                    string InstanceName = counterSet[key].InstanceName == null ? string.Empty : counterSet[key].InstanceName;
                    if (slaCounterSet != null && (slaBreachWin = slaCounterSet.Find(f => f.counter_id.ToString() == key && f.process_name == InstanceName)) != null)
                    {
                        slaBreachWin.received_value = counterValues;
                       
                        if (slaBreachWin.is_above)
                        {
                            if (counterValues >= slaBreachWin.critical_threshold_value)
                            {
                                slaBreachWin.is_breach = true;
                                slaBreachWin.breached_severity = "CRITICAL";
                            }
                            else if (counterValues >= slaBreachWin.warning_threshold_value)
                            {
                                slaBreachWin.is_breach = true;
                                slaBreachWin.breached_severity = "WARNING";
                            }
                        }
                        else
                        {
                            if (counterValues <= slaBreachWin.critical_threshold_value)
                            {
                                slaBreachWin.is_breach = true;
                                slaBreachWin.breached_severity = "CRITICAL";
                            }
                            else if (counterValues <= slaBreachWin.warning_threshold_value)
                            {
                                slaBreachWin.is_breach = true;
                                slaBreachWin.breached_severity = "WARNING";
                            }
                        }
                    } //First If
                } //try
                catch (Exception ex)
                {
                    string category = counterSet[key].CategoryName==null?string.Empty:counterSet[key].CategoryName;
                    string counter = counterSet[key].CounterName==null?string.Empty:counterSet[key].CounterName;
                    string instance = counterSet[key].InstanceName==null?string.Empty:counterSet[key].InstanceName;
                    MetricSet.Add(setMetricCounters(0, key.ToString(), string.Empty, instance));
                    //                    MetricSet.Add(setMetricCounters(0, key.ToString(), ex.Message, instance));
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - "+modType+"\tException received with "+category+"-"+counter+"-"+instance +": "+ ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType + "\tException received with " + category + "-" + counter + "-" + instance + ": " + ex.Message + "\t" + ex.StackTrace);
                    if (modType == "MSSQL") isMSSQLExists();
                    else if (modType == "MSIIS") isMSIISAppRunning();
                }

            }
            if (_metricLogicalDisk.Count > 0 && modType == "WINDOWS")
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType + "-Process Collection\tSLA Verification " + _metricLogicalDisk.Count + " verification starts");
                Stopwatch timeProcessCnter = new Stopwatch();
                timeProcessCnter.Start();
                i = 0; int procCount = 1;
                foreach (ProcessMetricClass key in _metricLogicalDisk)
                {
                    try
                    {
                        i++;
                        //Get counter value.
                        if (key.pcnter.CounterName == "% Processor Time") procCount = Environment.ProcessorCount;
                        else procCount = 1;

                        counterValues = Convert.ToDecimal(key.pcnter.NextValue() / procCount);
                        MetricSet.Add(setMetricCounters(counterValues, key.counter_id, string.Empty, key.pcnter.InstanceName == null ? string.Empty : key.pcnter.InstanceName));
                        if (slaCounterSet != null && (slaBreachWin = slaCounterSet.Find(f => f.counter_id.ToString() == key.counter_id && f.process_name == key.pcnter.InstanceName)) != null)
                        {
                            slaBreachWin.received_value = counterValues;
                            if (slaBreachWin.is_above)
                            {
                                if (counterValues >= slaBreachWin.critical_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "CRITICAL";
                                }
                                else if (counterValues >= slaBreachWin.warning_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "WARNING";
                                }
                            }
                            else
                            {
                                if (counterValues <= slaBreachWin.critical_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "CRITICAL";
                                }
                                else if (counterValues <= slaBreachWin.warning_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "WARNING";
                                }
                            }
                        } //First If
                        Thread.Sleep(2); //To avoid cpu bursting
                    } //try
                    catch (Exception ex)
                    {
                        string category = key.pcnter.CategoryName == null ? string.Empty : key.pcnter.CategoryName;
                        string counter = key.pcnter.CounterName == null ? string.Empty : key.pcnter.CounterName;
                        string instance = key.pcnter.InstanceName == null ? string.Empty : key.pcnter.InstanceName;
                        MetricSet.Add(setMetricCounters(0, key.counter_id, ex.Message, instance));
                        if (slaCounterSet != null && (slaBreachWin = slaCounterSet.Find(f => f.counter_id.ToString() == key.counter_id && f.process_name == key.pcnter.InstanceName)) != null)
                        {
                            slaBreachWin.is_breach = true;
                            slaBreachWin.breached_severity = "CRITICAL";
                            slaBreachWin.received_value = 0;
                        }
                        //MetricSet.Add(setMetricCounters(counterValues, key.ToString(), ex.Message, instance));
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType + "-Logical Disk Collection\tException received with " + category + "-" + counter + "-" + instance + ": " + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType + "-Logical Disk Collection\tException received with " + category + "-" + counter + "-" + instance + ": " + ex.Message + "\t" + ex.StackTrace);
                    }

                }
                timeProcessCnter.Stop();
                if (_debugMode)
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetCounterValues() " + modType + "\ttime taken to get logical disk counters\t" + timeProcessCnter.ElapsedMilliseconds + " ms");
            }
            if (_metricProcess.Count > 0 && modType=="WINDOWS")
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType + "-Process Collection\tSLA Verification " + _metricProcess.Count + " verification starts");
                Stopwatch timeProcessCnter = new Stopwatch();
                timeProcessCnter.Start();
                i = 0; int procCount = 1;
                foreach (ProcessMetricClass key in _metricProcess)
                {
                    try
                    {
                        i++;
                        //Get counter value.
                        if (key.pcnter.CounterName=="% Processor Time") procCount=Environment.ProcessorCount;
                        else procCount=1;

                        counterValues = Convert.ToDecimal(key.pcnter.NextValue()/procCount);
                        MetricSet.Add(setMetricCounters(counterValues, key.counter_id, string.Empty, key.pcnter.InstanceName == null ? string.Empty : key.pcnter.InstanceName));
                        if (slaCounterSet != null && (slaBreachWin = slaCounterSet.Find(f => f.counter_id.ToString() == key.counter_id && f.process_name == key.pcnter.InstanceName)) != null)
                        {
                            slaBreachWin.received_value = counterValues;
                            if (slaBreachWin.is_above)
                            {
                                if (counterValues >= slaBreachWin.critical_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "CRITICAL";
                                }
                                else if (counterValues >= slaBreachWin.warning_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "WARNING";
                                }
                            }
                            else
                            {
                                if (counterValues <= slaBreachWin.critical_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "CRITICAL";
                                }
                                else if (counterValues <= slaBreachWin.warning_threshold_value)
                                {
                                    slaBreachWin.is_breach = true;
                                    slaBreachWin.breached_severity = "WARNING";
                                }
                            }
                        } //First If
                        Thread.Sleep(2); //To avoid cpu bursting
                    } //try
                    catch (Exception ex)
                    {
                        string category = key.pcnter.CategoryName == null ? string.Empty : key.pcnter.CategoryName;
                        string counter = key.pcnter.CounterName == null ? string.Empty : key.pcnter.CounterName;
                        string instance = key.pcnter.InstanceName == null ? string.Empty : key.pcnter.InstanceName;
                        MetricSet.Add(setMetricCounters(0, key.counter_id, ex.Message, instance));
                        if (slaCounterSet != null && (slaBreachWin = slaCounterSet.Find(f => f.counter_id.ToString() == key.counter_id && f.process_name == key.pcnter.InstanceName)) != null)
                        {
                            slaBreachWin.is_breach = true;
                            slaBreachWin.breached_severity = "CRITICAL";
                            slaBreachWin.received_value = 0;
                        }
                        //MetricSet.Add(setMetricCounters(counterValues, key.ToString(), ex.Message, instance));
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType +"-Process Collection\tException received with " + category + "-" + counter + "-" + instance + ": " + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetCounterValues() - " + modType + "-Process Collection\tException received with " + category + "-" + counter + "-" + instance + ": " + ex.Message + "\t" + ex.StackTrace);
                        if (modType == "MSSQL") isMSSQLExists();
                        else if (modType == "MSIIS") isMSIISAppRunning();
                    }

                }
                timeProcessCnter.Stop();
                if (_debugMode)
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetCounterValues() " + modType + "\ttime taken to get process counters\t" + timeProcessCnter.ElapsedMilliseconds + " ms");
            }
            if (MetricSet.Count() > 0)
            {
                dataCounter.Append(",\"MetricSet\":");
                dataCounter.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(MetricSet)));
            }
            if (dataProcessThread.Length > 0)
                dataCounter.Append(dataProcessThread.ToString());

            dataCounter.Append("}");
            DataFileHandler.sendToDataQueue(dataCounter.ToString());

            slaBreachWin = null;
            timegetCountersValueWindows.Stop();
            if (_debugMode)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetCounterValues() " + modType + "\ttime taken to get all counters\t" + timegetCountersValueWindows.ElapsedMilliseconds + " ms");
            }
            //return data.ToString();
            return dataCounter.ToString();
        }
 
        /// <summary>
        /// It collect and send counters values as well as sla breach counter set to appedo collector.
        /// </summary>
        private void CollectAndSendCounterMSIIS()
        {
            try
            {
                //DateTime startTime = DateTime.Now;
                //string dt = startTime.GetDateTimeFormats()[58];
//                _counterValueMSIIS = getCountersValueMSIIS();
                _counterValueMSIIS = getCounterValues(_countersMSIIS, _slaCountersMSIIS, "MSIIS");
                //If there is any counters value breach.
                if (_slaCountersMSIIS.Count > 0)
                {
                    try
                    {
                        List<SLASetNew> slaCounterList = _slaCountersMSIIS.FindAll(f => f.is_breach == true);
                        if (slaCounterList.Count > 0)
                        {
                            StringBuilder dataSLA = new StringBuilder();
                            dataSLA.Append("metrics###{\"mod_type\":\"MSIIS\",\"type\":\"SLASet\",\"guid\":\"").Append(_guid_msiis).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                            dataSLA.Append("\"SLASet\":");
                            dataSLA.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(slaCounterList)));
                            dataSLA.Append("}");
//                             _responseStr = _constants.GetPageContent(_dataSendUrl, data.ToString());
                            //_responseStr = "{\"success\": true, \"failure\": false, \"message\": \"Data sent.\"}";
                            DataFileHandler.sendToDataQueue(dataSLA.ToString());
                            _slaCountersMSIIS.ForEach(f => f.is_breach = false);
                            dataSLA = null;
                        }
                        slaCounterList = null;
                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSIIS()" + "\t" + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSIIS()" + "\t" + ex.Message + "\t"+ex.StackTrace);
                    }
                }
                //If It is first time sending data to Appedo collector, it will log data to our reference
                //&& respCounterSet.Windows.message=="Data queued" to be added after correction
                if (_isFirstCounterMSIIS)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendCounterMSIIS()\tFirstRequest Sucessfully Sent " + _counterValueMSIIS);
                    _isFirstCounterMSIIS = false;
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSIIS()\tException received, check error log for details. " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSIIS()\texception received" + "\t" + ex.Message +"\t"+ex.StackTrace);
            }
        }
 
        /// <summary>
        /// It collect and send counters values as well as sla breach counter set to appedo collector.
        /// </summary>
        private void CollectAndSendCounterMSSQL()
        {
            try
            {
                _counterValueMSSQL = getCounterValues(_countersMSSQL, _slaCountersMSSQL, "MSSQL");

                //If there is any counters value breach.
                if (_slaCountersMSSQL.Count > 0)
                {
                    try
                    {
                        List<SLASetNew> slaCounterList = _slaCountersMSSQL.FindAll(f => f.is_breach == true);
                        if (slaCounterList.Count > 0)
                        {
                            StringBuilder dataSLA = new StringBuilder();
                            dataSLA.Append("metrics###{\"mod_type\":\"MSSQL\",\"type\":\"SLASet\",\"guid\":\"").Append(_guid_mssql).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                            dataSLA.Append("\"SLASet\":");
                            dataSLA.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(slaCounterList)));
                            dataSLA.Append("}");
                            DataFileHandler.sendToDataQueue(dataSLA.ToString());
                            _slaCountersMSSQL.ForEach(f => f.is_breach = false);
                            dataSLA = null;
                        }
                        slaCounterList = null;
                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSSQL()" + "\t" + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSSQL()" + "\t" + ex.Message);
                    }
                }

                //getting slow query //&& _mssqlMjrVer >= 10
                if (_mssqlConnectionException)
                    isMSSQLExists();

                if (!_mssqlConnectionException )
                {
                    CollectAndSendSqlSlowQuery(_dbConnection);
                    CollectAndSendSqlSlowProcedure(_dbConnection);
                    CollectAndSendSqlLockQueries(_dbConnection);
                    CollectAndSendSqlWaitStat(_dbConnection);
                }
                else
                {
                    if (_mssqlConnectionException && _isFirstCounterMSSQL)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlSlowQuery()\tMSSQL Connection could not be established, please check the notification & error log for details");
                    }
                    else if (_isFirstCounterMSSQL)
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlSlowQuery()\tMSSQL Version must be MSSQL 2008 and above, hence slow query and procedure will not be collected");

                }

                //If It is first time sending data to Appedo collector, it will log data to our reference
                //&& respCounterSet.Windows.message=="Data queued" to be added after correction
                if (_isFirstCounterMSSQL)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendCounterMSSQL()\tFirstRequest Sucessfully Sent. " + _counterValueMSSQL);
                    _isFirstCounterMSSQL = false;
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSSQL()\tException received. " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendCounterMSSQL()\tException received. " + "\t" + ex.Message);
            }
        }
 
        /// <summary>
        /// It collect and send SqlSlowQuery set to appedo collector.
        /// </summary>
        private void CollectAndSendSqlSlowQuery(SqlConnection con)
        {
            Stopwatch sq = new Stopwatch();
            sq.Start();
            //DateTime startTime = DateTime.Now;
            //string dt = startTime.GetDateTimeFormats()[58];
            try
            {
                string query = @"declare @var1 varchar(100);set @var1 = SYSDATETIMEOFFSET();set @var1= SUBSTRING(@var1,CHARINDEX('+',@var1,1),Len(@var1)); SELECT TOP 20 Convert(varchar(100),qs.creation_time,126)+@var1 cached_time, Convert(varchar(100),qs.last_execution_time,126)+@var1 last_execution_time, (qs.total_worker_time/qs.execution_count)/1000 AS [avg_cpu_time_ms], qs.execution_count, qs.total_elapsed_time/1000 tot_elp_time_ms , SUBSTRING(st.text, (qs.statement_start_offset/2)+1,((CASE qs.statement_end_offset  WHEN -1 THEN DATALENGTH(st.text) ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) + 1) AS statement_text, db_name(CONVERT(INT, dep.value)) db_name FROM sys.dm_exec_query_stats AS qs  CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st CROSS APPLY sys.dm_exec_plan_attributes(qs.plan_handle) dep WHERE last_execution_time >= DATEADD(SECOND,-20,getdate()) AND last_execution_time <= getDATE() and db_name(CONVERT(int, dep.value)) NOT IN ('master','model','msdb') AND dep.attribute = N'dbid' ORDER BY tot_elp_time_ms desc";
                //con.Open();
                SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();

                SqlDataAdapter adapter = new SqlDataAdapter();
                adapter.SelectCommand = new SqlCommand(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                connection.Close();
                if (table.Rows.Count > 0)
                {
                    List<SlowQuery> coll = new List<SlowQuery>();

                    foreach (DataRow queryRow in table.Rows)
                    {
                        SlowQuery que = new SlowQuery();
                        que.query = queryRow["statement_text"].ToString().Replace("'", "''");
                        que.calls = Convert.ToInt32(queryRow["execution_count"]);
                        que.duration_ms = Convert.ToInt32(queryRow["tot_elp_time_ms"]);
                        que.avg_cpu_time_ms = Convert.ToInt32(queryRow["avg_cpu_time_ms"]);
                        que.cached_time = Convert.ToString(queryRow["cached_time"]);
                        que.last_execution_time = (Convert.ToDateTime(queryRow["last_execution_time"])).ToString("yyyy-MM-ddTHH:mm:ss.fffzzzzzz"); 
                        que.total_rows = 0; //Convert.ToInt32(queryRow["total_rows"]); Not working in SQL 2008 10.0.16 version hence removed this feature.
                        que.db_name = Convert.ToString(queryRow["db_name"]);
                        coll.Add(que);
                    }
                    if (table.Rows.Count > 0)
                    {
                        StringBuilder data = new StringBuilder();
                        data.Append("metrics###{\"mod_type\":\"MSSQL\",\"type\":\"SlowQuerySet\",\"guid\":\"").Append(_guid_mssql).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                        data.Append("\"SlowQuerySet\":");
                        data.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(coll)));
                        data.Append("}");
                        DataFileHandler.sendToDataQueue(data.ToString());
                    }
                    sq.Stop();
                    if (_debugMode)
                    {
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlSlowQuery()\tRows retrieved " + table.Rows.Count.ToString() + " rows");
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlSlowQuery()\tTime Taken " + sq.ElapsedMilliseconds + " ms");
                    }
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlSlowQuery()\tException Received " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ()+"\tCritical\t"+Environment.MachineName+"\tCollectAndSendSqlSlowQuery()\tException Received\t"+ ex.Message + ex.StackTrace);
                isMSSQLExists();
            }
        }

        /// <summary>
        /// It collect and send SqlSlowProcedure sent to queue .
        /// </summary>
        private void CollectAndSendSqlSlowProcedure(SqlConnection con)
        {
            Stopwatch sq = new Stopwatch();
            sq.Start();

            try
            {
                string query = @"declare @var1 varchar(100);set @var1 = SYSDATETIMEOFFSET();set @var1= SUBSTRING(@var1,CHARINDEX('+',@var1,1),Len(@var1));SELECT TOP 20 dbs.name db_name, obj.name procedure_name,  Convert(varchar(100),cached_time,126)+@var1 cached_time, Convert(varchar(100),last_execution_time,126)+@var1 last_execution_time,  (total_worker_time/execution_count)/1000 AS [avg_cpu_time_ms], execution_count, qs.total_elapsed_time/1000 tot_elp_time_ms, st.text statement_text FROM sys.dm_exec_procedure_stats AS qs join sys.databases dbs on dbs.database_id=qs.database_id and dbs.name not in ('model','msdb', 'master') join sys.objects obj on obj.object_id=qs.object_id and is_ms_shipped=0 CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st WHERE last_execution_time >= DATEADD(SECOND,-20,getdate()) AND last_execution_time <= getDATE() ORDER BY qs.total_elapsed_time DESC";
                //con.Open();
                //SqlDataAdapter adapter = new SqlDataAdapter(query, con.ConnectionString);
                //DataTable table = new DataTable();
                SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();

                SqlDataAdapter adapter = new SqlDataAdapter();
                adapter.SelectCommand = new SqlCommand(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                connection.Close();

               //               con.Close();
                if (table.Rows.Count > 0)
                {
                    List<SlowProcedure> coll = new List<SlowProcedure>();
                    foreach (DataRow queryRow in table.Rows)
                    {
                        SlowProcedure que = new SlowProcedure();
                        que.procedure = queryRow["statement_text"].ToString().Replace("'", "''");
                        que.calls = Convert.ToInt32(queryRow["execution_count"]);
                        que.duration_ms = Convert.ToInt32(queryRow["tot_elp_time_ms"]);
                        que.avg_cpu_time_ms = Convert.ToInt32(queryRow["avg_cpu_time_ms"]);
                        que.cached_time = queryRow["cached_time"].ToString();
                        que.last_execution_time = (Convert.ToDateTime(queryRow["last_execution_time"])).ToString("yyyy-MM-ddTHH:mm:ss.fffzzzzzz");
                        que.db_name = queryRow["db_name"].ToString();
                        que.procedure_name = queryRow["procedure_name"].ToString();
                        coll.Add(que);
                    }
                    if (table.Rows.Count > 0)
                    {
                        StringBuilder data = new StringBuilder();
                        data.Append("metrics###{\"mod_type\":\"MSSQL\",\"type\":\"SlowProcSet\",\"guid\":\"").Append(_guid_mssql).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                        data.Append("\"SlowProcSet\":");
                        data.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(coll)));
                        data.Append("}");
                        //_responseStr = _constants.GetPageContent(_dataSendUrl, data.ToString());
                        DataFileHandler.sendToDataQueue(data.ToString());
                    }
                    sq.Stop();
                    if (_debugMode)
                    {
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlSlowProcedure()\tRows retrieved " + table.Rows.Count.ToString() + " rows");
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlSlowProcedure()\tTime Taken " + sq.ElapsedMilliseconds + " ms");
                    }
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlSlowProcedure()\tException Received\t" + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlSlowProcedure()\tException Received\t" + ex.Message + ex.StackTrace);
                isMSSQLExists();
            }
        }

        private void CollectAndSendSqlLockQueries(SqlConnection con)
        {
            Stopwatch sq = new Stopwatch();
            sq.Start();

            try
            {
// old code                string query = @"SELECT db.name db_name, ec1.connect_time, ec1.client_net_address, wt.wait_duration_ms, tl.request_session_id, wt.blocking_session_id, OBJECT_NAME(p.OBJECT_ID) blocked_object_name,tl.resource_type, h1.TEXT AS request_query,h2.TEXT AS blocking_query,tl.request_mode FROM sys.dm_tran_locks AS tl INNER JOIN sys.databases db ON db.database_id = tl.resource_database_id INNER JOIN sys.dm_os_waiting_tasks AS wt ON tl.lock_owner_address = wt.resource_address INNER JOIN sys.partitions AS p ON p.hobt_id = tl.resource_associated_entity_id INNER JOIN sys.dm_exec_connections ec1 ON ec1.session_id = tl.request_session_id INNER JOIN sys.dm_exec_connections ec2 ON ec2.session_id = wt.blocking_session_id CROSS APPLY sys.dm_exec_sql_text(ec1.most_recent_sql_handle) AS h1 CROSS APPLY sys.dm_exec_sql_text(ec2.most_recent_sql_handle) AS h2";
                string query = @"SELECT  start_time connect_time ,der.session_id request_session_id ,db_name(database_id) as db_name ,conn.client_net_address ,der.command command ,sql_text.text as request_query ,status ,blocking_session_id ,wait_time wait_duration_ms,wait_type request_mode ,wait_resource resource_type FROM sys.dm_exec_requests as der CROSS APPLY sys.dm_exec_sql_text(sql_handle) as sql_text JOIN sys.dm_exec_connections as conn ON conn.session_id = der.session_id WHERE blocking_session_id != 0 AND db_name(database_id) NOT IN ('master','model','msdb')";
                //con.Open();
                //SqlDataAdapter adapter = new SqlDataAdapter(query, con.ConnectionString);
                //DataTable table = new DataTable();
                //adapter.Fill(table);
                //con.Close();
                SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();

                SqlDataAdapter adapter = new SqlDataAdapter();
                adapter.SelectCommand = new SqlCommand(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                connection.Close();

                if (table.Rows.Count > 0)
                {
                    List<sqlLockDataClass> coll = new List<sqlLockDataClass>();
                    foreach (DataRow queryRow in table.Rows)
                    {
                        sqlLockDataClass que = new sqlLockDataClass();
                        que.command = queryRow["command"].ToString();
                        que.status = queryRow["status"].ToString();
                        que.blocking_query = string.Empty;
                        que.blocking_session_id = Convert.ToInt32(queryRow["blocking_session_id"]);
                        que.client_net_address = queryRow["client_net_address"].ToString();
                        que.connect_time = (Convert.ToDateTime(queryRow["connect_time"])).ToString("yyyy-MM-ddTHH:mm:ss.fffzzzzzz");
                        que.db_name = queryRow["db_name"].ToString();
                        que.request_mode = queryRow["request_mode"].ToString();
                        que.request_query = queryRow["request_query"].ToString().Replace("'", "''");
                        que.request_session_id = Convert.ToInt32(queryRow["request_session_id"]);
                        que.resource_type = queryRow["resource_type"].ToString();
                        que.wait_duration_ms = Convert.ToInt32(queryRow["wait_duration_ms"]);
                        coll.Add(que);
                    }
                    if (table.Rows.Count > 0)
                    {
                        StringBuilder data = new StringBuilder();
                        data.Append("metrics###{\"mod_type\":\"MSSQL\",\"type\":\"LockSet\",\"guid\":\"").Append(_guid_mssql).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                        data.Append("\"LockSet\":");
                        data.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(coll)));
                        data.Append("}");
                        DataFileHandler.sendToDataQueue(data.ToString());
                    }
                    sq.Stop();
                    if (_debugMode)
                    {
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlLockQueries()\tRows retrieved " + table.Rows.Count.ToString() + " rows");
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlLockQueries()\tTime Taken " + sq.ElapsedMilliseconds + " ms");
                    }
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlLockQueries()\tException Received\t" + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlLockQueries()\tException Received\t" + ex.Message + ex.StackTrace);
                isMSSQLExists();
            }
        }

        private void CollectAndSendSqlWaitStat(SqlConnection con)
        {
            Stopwatch sq = new Stopwatch();
            sq.Start();

            try
            {
                string query = @"SELECT db_name(database_id) as dbname, status, count(*) as count FROM sys.dm_exec_requests WHERE db_name(database_id) NOT IN ('master','model','msdb') AND status NOT IN ('background', 'sleeping') AND blocking_session_id IS NULL GROUP BY db_name(database_id),status UNION SELECT db_name(database_id) as dbname, 'locked', count(*) as count FROM sys.dm_exec_requests WHERE db_name(database_id) NOT IN ('master','model','msdb') AND status ='suspended' AND blocking_session_id IS NOT NULL GROUP BY db_name(database_id),status ORDER BY 1,2";
                SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();
                SqlDataAdapter adapter = new SqlDataAdapter();
                adapter.SelectCommand = new SqlCommand(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                connection.Close();

                if (table.Rows.Count > 0)
                {
                    List<sqlWaitStatClass> coll = new List<sqlWaitStatClass>();
                    foreach (DataRow queryRow in table.Rows)
                    {
                        sqlWaitStatClass que = new sqlWaitStatClass();
                        que.appedo_received_on = DataFileHandler.getDTTZ();
                        que.db_name = queryRow["dbname"].ToString();
                        que.status = queryRow["status"].ToString();
                        que.count = Convert.ToInt32(queryRow["count"]);
                        coll.Add(que);
                    }
                    if (table.Rows.Count > 0)
                    {
                        StringBuilder data = new StringBuilder();
                        data.Append("metrics###{\"mod_type\":\"MSSQL\",\"type\":\"WaitStat\",\"guid\":\"").Append(_guid_mssql).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                        data.Append("\"WaitStat\":");
                        data.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(coll)));
                        data.Append("}");
                        DataFileHandler.sendToDataQueue(data.ToString());
                    }
                    sq.Stop();
                    if (_debugMode)
                    {
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlWaitStat()\tRows retrieved " + table.Rows.Count.ToString() + " rows");
                        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tCollectAndSendSqlWaitStat()\tTime Taken " + sq.ElapsedMilliseconds + " ms");
                    }
                }
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlWaitStat()\tException Received\t" + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tCollectAndSendSqlWaitStat()\tException Received\t" + ex.Message + ex.StackTrace);
                isMSSQLExists();
            }
        }
        /// <summary>
        /// To config counters set. Response from Appedo collector contains set of Metrics that have been selected by user.
        /// Separate thread for each of the Metric Set
        /// </summary>
        /// <param name="details">List of counters</param>
        private void SetWindowsCntList(CountersDetailNew resp)
        {
            if (_guid_windows == string.Empty)
                if (resp.WINDOWS.message == String.Empty || resp.WINDOWS.message == null)
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetAgent() - Windows\tguid not received from server, hence Metrics will not be collected ");
                else
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent() - Windows\t" + resp.WINDOWS.message + ", hence Metrics will not be collected ");
            else
            {
                Thread tWindows = new Thread(() => WindowsSetCounterThread(resp));
                tWindows.Start();
            }
        }
        private void SetMSSQLCntList(CountersDetailNew resp)
        {
            if (_guid_mssql == string.Empty)
                if (resp.MSSQL.message == String.Empty || resp.MSSQL.message == null)
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetAgent() - MSSQL \tguid not received from server, hence Metrics will not be collected ");
                else
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent() - MSSQL\t" + resp.MSSQL.message + ", hence Metrics will not be collected ");
            else
            {
                Thread tMSSQL = new Thread(() => MSSQLSetCounterThread(resp));
                tMSSQL.Start();
            }
        }
        private void SetMSIISCntList(CountersDetailNew resp)
        {
            if (_guid_msiis == string.Empty)
            {
                if (resp.MSIIS.message == String.Empty || resp.MSIIS.message == null)
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent() - MSIIS\tguid not received from server, hence Metrics will not be collected ");
                else
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetAgent() - MSIIS\t" + resp.MSIIS.message + ", hence Metrics will not be collected ");
            }
            else
            {
                Thread tMSIIS = new Thread(() => MSIISSetCounterThread(resp));
                tMSIIS.Start();
            }
        }

        public void WindowsSetCounterThread(CountersDetailNew winCounterSet)
        {
            try
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread()\tStarted ");
                Stopwatch setCntThreadTimer = new Stopwatch();
                setCntThreadTimer.Start();

                if (_windowsRunStatus.ToUpper().Trim() == "RESTART")
                {
                    try
                    {
                        if (threadTopProcess != null)
                        {
                            if (threadTopProcess.IsAlive)
                            {
                                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread() - TopProcessCollectionThread\tStopping the thread.....");
                                threadTopProcess.Abort();
                                while (threadTopProcess.IsAlive)
                                {
                                    Thread.Sleep(5000); //Waiting for thread to be aborted.
                                }
                                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread() - TopProcessCollectionThread\tStopped");
                            }
                        }
                    }
                    catch
                    {

                    }
                }
                _topProcessCnterId.Clear(); //clearing the topProcess counter collection
                if (_debugMode)
                {
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread()\tStarted and setting in progress ....");
                }
                if (winCounterSet.WINDOWS != null && winCounterSet.WINDOWS.counters != null)
                {
                    _countersWindows.Clear();
                    //Collect child counter set
                    ParentCounterList parentCounterList = new ParentCounterList();
                    parentCounterList.ParentCounter = new List<ParentCounter>();

                    String[] counterSplit = null;
                    //Store counter detail.
                    foreach (counterSetNew counterMetric in winCounterSet.WINDOWS.counters)
                    {
                        try
                        {
                            counterSplit = counterMetric.query.Split(',');
                            
                            // Counter has child counter and it doesn't have that list of child. Collect all child counters. System will not collect child for process and Thread
                            //if (counterSplit[0].ToString().ToUpper() == "TRUE" && counterSplit[1].ToString() != "Process" && counterSplit[1].ToString() != "Thread")
                            //{
                            //    parentCounterList.ParentCounter.Add(GetChdCounter(counterMetric.counter_id.ToString(), counterSplit[1].ToString(), counterSplit[2].ToString()));
                            //}

                            //Counter has child counter and it has child counter name.
                            if (counterSplit[1].ToString() == "LogicalDisk")
                            {
                                setLogicalDiskInstances(counterSplit[2].ToString(), counterMetric.counter_id.ToString());
                            }
                            if (counterSplit[0].ToString().ToUpper() == "TRUE" && counterSplit[3].Trim() != string.Empty && (counterSplit[1].ToString() != "Process" || (counterSplit[1].ToString()=="Process" && counterSplit[3].Trim() == "_Total"))  && counterSplit[1].ToString() != "Thread")
                            {
                                if (counterSplit[1].ToString() == "Process"  && counterSplit[2].Trim().ToString() != "% Processor Time" && counterSplit[2].Trim().ToString() != "Private Bytes")
                                {
                                    processMetricList pList = new processMetricList();
                                    pList.counter_id = counterMetric.counter_id.ToString();
                                    pList.process_metric_name = counterSplit[2].Trim().ToString();
                                    pList.instance_name = counterSplit[3].Trim().ToString();
                                    listProcessMetrics.Add(pList);
                                }
                                else if (counterSplit[1].ToString() == "Process" && counterSplit[2].Trim().ToString() == "% Processor Time")
                                {
                                    _topProcessCnterId.Add("% Processor Time", counterMetric.counter_id);
                                }
                                else if (counterSplit[1].ToString() == "Process" && counterSplit[2].Trim().ToString() == "Private Bytes")
                                {
                                    _topProcessCnterId.Add("Private Bytes", counterMetric.counter_id);
                                }
                                PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString(), counterSplit[3].Trim().ToString());
                                counter.NextValue();
                                _countersWindows.Add(counterMetric.counter_id.ToString(), counter);
                            }
                            else if (counterSplit[0].ToString().ToUpper() == "FALSE" && counterSplit[1].ToString() != "Process" && counterSplit[1].ToString() != "Thread")
                            {
                                // Counter has Instance-Name
                                if (counterSplit[3].Trim() != string.Empty)
                                {
                                    PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString(), counterSplit[3].Trim().ToString());
                                    counter.NextValue();
                                    _countersWindows.Add(counterMetric.counter_id.ToString(), counter);
                                }
                                // Counter does't have Instance-Name
                                else
                                {
                                    PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString());
                                    counter.NextValue();
                                    _countersWindows.Add(counterMetric.counter_id.ToString(), counter);
                                }
                            
                            }
                        }
                        catch (Exception ex)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tWindowsSetCounterThread()\tCategory: " + counterSplit[1] + ":: Counter Name: " + counterSplit[2] + ":: Instance Name: " + counterSplit[3] + "\t" + ex.Message);
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tWindowsSetCounterThread()\tCategory: " + counterSplit[1] + ":: Counter Name: " + counterSplit[2] + ":: Instance Name: " + counterSplit[3] + "\t" + ex.Message +ex.StackTrace) ;
                        }
                    }
                    if (winCounterSet.WINDOWS != null && winCounterSet.WINDOWS.SLA != null)
                        if (winCounterSet.WINDOWS.SLA.Length > 0) {
                            _slaCountersWin.Clear();
                            _slaCountersWin = new List<SLASetNew>(winCounterSet.WINDOWS.SLA);
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread() - SLA Setting\tSLA set "+ _slaCountersWin.ToString());
                        }
                        else
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread() - SLA Setting\tSLA not set for any of the counters");
                }

                setCntThreadTimer.Stop();
                if (_debugMode)
                {
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread()\tWindows CounterSetting Completed in " + setCntThreadTimer.ElapsedMilliseconds + " ms");
                }
                setCntThreadTimer = null;
                if (_windowsRunStatus.ToUpper().Trim() == "RESTART")
                {
                    _windowsRunStatus = "RUNNING";
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread()\tWindows Counter Restarted and startedCollecting Data");
                }

                bool enableTopProcess = false;
                bool res =  bool.TryParse(ConfigurationManager.AppSettings["enableTopProcessMemory"],out enableTopProcess);
                if (!res)
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tWindowsSetCounterThread() \tenableTopProcessMemory must be either true/false, hence going with default which is false");

                if (_topProcessCnterId.Count > 0 && enableTopProcess)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread() - TopProcessCollectionThread\tStarted...");
                    threadTopProcess = new Thread(() => collectTopProcess());
                    threadTopProcess.Start();
                }
                else if (!enableTopProcess)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tWindowsSetCounterThread() - TopProcessCollectionThread\tTopProcess Collection is disabled, hence not capturing the top processes");
                }
                else
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tWindowsSetCounterThread() - TopProcessCollectionThread\tUnder process category, % Processor Time or Private Bytes either of the one must be selected for top process collection. currently none selected");

                }
                collectWindows();
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\t"+ex.Message+"\t"+ex.StackTrace);
            }
        }
        private void collectTopProcess()
        {
            //sets the _metricTopProcess list for each of the hash table entity
            setTopProcessAllCnters();
            while (_runCond && _metricTopProcess.Count>0)
            {
                Stopwatch gtpc = new Stopwatch();
                gtpc.Start();
                if (getTopProcessCounters()) //true means ran with exception and need to reset the process instances for the next run
                    setTopProcessAllCnters();

                gtpc.Stop();
                int elapsedTime = Convert.ToInt32(gtpc.ElapsedMilliseconds);
                if (elapsedTime < 20000)
                    Thread.Sleep(20000 - elapsedTime);
            }
        }
        private void setTopProcessAllCnters()
        {
            _metricTopProcess.Clear();
            foreach (DictionaryEntry de in _topProcessCnterId)
            {
                setTopProcess(de.Key.ToString(), de.Value.ToString());
            }
        }

        private bool getTopProcessCounters()
        {
            bool reset = false;
            if (_metricTopProcess.Count()>0)
            {
                Stopwatch timegetCountersValueWindows = new Stopwatch();
                timegetCountersValueWindows.Start();
                StringBuilder dataCounter = new StringBuilder();
                dataCounter.Append("metrics###{\"mod_type\":\"").Append("WINDOWS").Append("\",\"type\":").Append("\"MetricSet\"").Append(",\"guid\":\"").Append(_guid_windows).Append("\",\"datetime\":\"").Append(DataFileHandler.getDTTZ()).Append("\",");
                dataCounter.Append("\"MetricSet\":");
                List<processClass> MetricSet = new List<processClass>();
                decimal counterValues = 0;

                //Get all counter one by one
                foreach (ProcessMetricClass topProcess in _metricTopProcess)
                {
                    if (topProcess.pcnter.InstanceName != "_Total")
                    {
                        try
                        {
                            //Get counter value.
                            int core = 1;
                            if (topProcess.pcnter.CounterName == "% Processor Time")
                                core = Environment.ProcessorCount;
                            counterValues = Convert.ToDecimal(topProcess.pcnter.NextValue()) / core;
                            if (counterValues != 0)
                                MetricSet.Add(setMetricCounters(counterValues, topProcess.counter_id, string.Empty, topProcess.pcnter.InstanceName == null ? string.Empty : topProcess.pcnter.InstanceName));
                            Thread.Sleep(2); //to avoid cpu bursting
                        } //try
                        catch
                        {
                            reset = true;
                            //DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetTopProcessCounters() - TopProcessCollection\tException received with " + topProcess.pcnter.CounterName + " " + topProcess.pcnter.InstanceName + ":" + ex.Message);
                            //ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tgetTopProcessCounters() - TopProcessCollection\tException received with " + topProcess.pcnter.CounterName + " " + topProcess.pcnter.InstanceName + ":" + ex.StackTrace);
                        }
                    }
                }
                timegetCountersValueWindows.Stop();
                if (_debugMode)
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tgetTopProcessCounters()\tElapsedTime to collect all process counters " + timegetCountersValueWindows.ElapsedMilliseconds + " ms");
                
                dataCounter.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(MetricSet)));
                dataCounter.Append("}");
                DataFileHandler.sendToDataQueue(dataCounter.ToString());
                dataCounter.Clear();
                MetricSet = null;
                timegetCountersValueWindows = null;
            }
            return reset;
        }
        public void setLogicalDiskInstances(string metricName, string counterId)
        {
            Stopwatch timeSetPrIn = new Stopwatch();
            timeSetPrIn.Start();
            PerformanceCounterCategory categoryProcess = new PerformanceCounterCategory("LogicalDisk");
            string[] instances = categoryProcess.GetInstanceNames();
            foreach (string ins in instances)
            {
                try
                {
                    ProcessMetricClass pmc = new ProcessMetricClass();
                    PerformanceCounter cnt = new PerformanceCounter("LogicalDisk", metricName, ins);
                    cnt.NextValue();
                    pmc.counter_id = counterId;
                    pmc.pcnter = cnt;
                    _metricLogicalDisk.Add(pmc);
                }
                catch (Exception ex)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances() Exception for logical Disk Category" + metricName + "-" + ins + "\t" + ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances() Exception for Logical Disk Category " + metricName + "-" + ins + "\t" + ex.Message + "\t" + ex.StackTrace);
                }
            }
        }

        public void setProcessInstances(List<processMetricList> processList)
        {
            Stopwatch timeSetPrIn = new Stopwatch();
            timeSetPrIn.Start();
            PerformanceCounterCategory categoryProcess = new PerformanceCounterCategory("Process");
            string[] instances = categoryProcess.GetInstanceNames();
            string[] processNames = _processNames.Split(',');
            foreach (processMetricList pml in processList)
            {
                foreach (string pn in processNames)
                {
                    string[] filteredInstances = instances.Where(e => e.Contains(pn.Trim())).ToArray();
                    if (filteredInstances.Count() ==0)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances()\tProcess Name " + pn + " does not exist.");
                        ProcessMetricClass pmc = new ProcessMetricClass();
                        PerformanceCounter cnt = new PerformanceCounter("Process", pml.process_metric_name, pn);
                        pmc.counter_id = pml.counter_id;
                        pmc.pcnter = cnt;
                        _metricProcess.Add(pmc);
                    }
                    foreach (string fi in filteredInstances)
                    {
                        try
                        {
                            ProcessMetricClass pmc = new ProcessMetricClass();
                            PerformanceCounter cnt = new PerformanceCounter("Process", pml.process_metric_name, fi);
                            cnt.NextValue();
                            pmc.counter_id = pml.counter_id;
                            pmc.pcnter = cnt;
                            _metricProcess.Add(pmc);
                        }
                        catch(Exception ex)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances() Exception for process name " + pn + "-" + pml.process_metric_name + "- " + fi + "\t" + ex.Message);
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances() Exception for process name " + pn +"-"+ pml.process_metric_name+ "- " + fi + "\t" + ex.Message + "\t" + ex.StackTrace);
                        }
                    }
                }
                if (pml.instance_name != string.Empty)
                {
                    try
                    {
                        ProcessMetricClass pmc = new ProcessMetricClass();
                        PerformanceCounter cnt = new PerformanceCounter("Process", pml.process_metric_name, pml.instance_name);
                        cnt.NextValue();
                        pmc.counter_id = pml.counter_id;
                        pmc.pcnter = cnt;
                        _metricProcess.Add(pmc);
                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances() Exception for counter name " + pml.process_metric_name + "-" + pml.instance_name + "\t" + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetProcessInstances() Exception for counter name " + pml.process_metric_name + "-" + pml.instance_name + "\t" + ex.Message + "\t" + ex.StackTrace);
                    }
                }
            }
            timeSetPrIn.Stop();
            if (_debugMode)
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetProcessInstances()\tTime Taken to SetProcessCnters\t" + timeSetPrIn.ElapsedMilliseconds + " ms");
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetProcessInstances()\tCurrent Process Instances are set and ready for collection");

        }
        public void MSIISSetCounterThread(CountersDetailNew winCounterSet)
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSIISSetCounterThread()\tStarted ");
            Stopwatch setCntThreadTimer = new Stopwatch();
            setCntThreadTimer.Start();
            if (_debugMode)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + " \tMSIISSetCounterThread() \tStarted and setting in progress ....");
            }
            if (winCounterSet.MSIIS !=null && winCounterSet.MSIIS.counters != null)
            {
                _countersMSIIS.Clear();

                //Collect child counter set
                ParentCounterList parentCounterList = new ParentCounterList();
                parentCounterList.ParentCounter = new List<ParentCounter>();

                string instances = System.Configuration.ConfigurationManager.AppSettings["process"];
                
                String[] counterSplit = null;
                //Store counter detail.
                foreach (counterSetNew counterMetric in winCounterSet.MSIIS.counters)
                {
                    try
                    {
                        counterSplit = counterMetric.query.Split(',');

                        // Counter has child counter and it doesn't have that list of child. Collect all child counters. System will not collect child for process and Thread
                        if (counterSplit[0].ToString().ToUpper() == "TRUE" && counterSplit[1].ToString() != "Process" && counterSplit[1].ToString() != "Thread")
                        {
                            parentCounterList.ParentCounter.Add(GetChdCounter(counterMetric.counter_id.ToString(), counterSplit[1].ToString(), counterSplit[2].ToString()));
                        }

                        //Counter has child counter and it has child counter name.
                        if (counterSplit[0].ToString().ToUpper() == "TRUE" && counterSplit[3].Trim() != string.Empty)
                        {
                            PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString(), counterSplit[3].Trim().ToString());
                            counter.NextValue();
                            _countersMSIIS.Add(counterMetric.counter_id.ToString(), counter);
                            //collection top 10 process that consumes max processor time Applicable only for Processor/% Processor Time(_total) to be done later
                        }
                        else if (counterSplit[0].ToString().ToUpper() == "FALSE")
                        {
                            // Counter have Instance-Name
                            if (counterSplit[3].Trim() != string.Empty)
                            {
                                PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString(), counterSplit[3].Trim().ToString());
                                counter.NextValue();
                                _countersMSIIS.Add(counterMetric.counter_id.ToString(), counter);
                            }
                            // Counter does't have Instance-Name
                            else
                            {
                                PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString());
                                counter.NextValue();
                                _countersMSIIS.Add(counterMetric.counter_id.ToString(), counter);
                            }
                        }
                       
                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tMSIISSetCounterThread()\tCategory: " + counterSplit[1] + ":: Counter Name: " + counterSplit[2] + ":: Instance Name: " + counterSplit[3] + "\t" + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tMSIISSetCounterThread()\tCategory: " + counterSplit[1] + ":: Counter Name: " + counterSplit[2] + ":: Instance Name: " + counterSplit[3] + "\t" + ex.Message);
                    }
                }
                if (winCounterSet.MSIIS != null && winCounterSet.MSIIS.SLA != null)
                {
                    if (winCounterSet.MSIIS.SLA.Length > 0) { _slaCountersMSIIS.Clear(); _slaCountersMSIIS = new List<SLASetNew>(winCounterSet.MSIIS.SLA); }
                }
            }
            setCntThreadTimer.Stop();
            if (_debugMode)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSIISSetCounterThread()\tMSIIS CounterSetting Completed in " + setCntThreadTimer.ElapsedMilliseconds + " ms");
            }
            setCntThreadTimer = null;
            if (_msiisRunStatus.ToUpper().Trim() == "RESTART")  
            {
                _msiisRunStatus = "RUNNING";
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSIISSetCounterThread()\tMSIIS Counter Restarted and startedCollecting Data...");
            }
            collectMSIIS();
        }

        public void MSSQLSetCounterThread(CountersDetailNew winCounterSet)
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSSQLSetCounterThread()\tStarted ");
            Stopwatch setCntThreadTimer = new Stopwatch();
            setCntThreadTimer.Start();
            if (_debugMode)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSSQLSetCounterThread()\tStarted and setting in progress ....");
            }
            if (winCounterSet.MSSQL != null && winCounterSet.MSSQL.counters != null)
            {
                _countersMSSQL.Clear();

                //Collect child counter set
                ParentCounterList parentCounterList = new ParentCounterList();
                parentCounterList.ParentCounter = new List<ParentCounter>();

                string instances = System.Configuration.ConfigurationManager.AppSettings["process"];

                String[] counterSplit = null;
                //Store counter detail.
                foreach (counterSetNew counterMetric in winCounterSet.MSSQL.counters)
                {
                    try
                    {
                        counterSplit = counterMetric.query.Split(',');

                        // Counter has child counter and it doesn't have that list of child. Collect all child counters. System will not collect child for process and Thread
                        if (counterSplit[0].ToString().ToUpper() == "TRUE" && counterSplit[1].ToString() != "Process" && counterSplit[1].ToString() != "Thread")
                        {
                            parentCounterList.ParentCounter.Add(GetChdCounter(counterMetric.counter_id.ToString(), counterSplit[1].ToString(), counterSplit[2].ToString()));
                        }

                        //Counter has child counter and it has child counter name.
                        if (counterSplit[0].ToString().ToUpper() == "TRUE" && counterSplit[3].Trim() != string.Empty)
                        {
                            PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString(), counterSplit[3].Trim().ToString());
                            counter.NextValue();
                            _countersMSSQL.Add(counterMetric.counter_id.ToString(), counter);
                            //collection top 10 process that consumes max processor time Applicable only for Processor/% Processor Time(_total) to be done later
                        }
                        else if (counterSplit[0].ToString().ToUpper() == "FALSE")
                        {
                            // Counter have Instance-Name
                            if (counterSplit[3].Trim() != string.Empty)
                            {
                                PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString(), counterSplit[3].Trim().ToString());
                                counter.NextValue();
                                _countersMSSQL.Add(counterMetric.counter_id.ToString(), counter);
                            }
                            // Counter does't have Instance-Name
                            else
                            {
                                PerformanceCounter counter = new PerformanceCounter(counterSplit[1].Trim().ToString(), counterSplit[2].Trim().ToString());
                                counter.NextValue();
                            _countersMSSQL.Add(counterMetric.counter_id.ToString(), counter);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tMSSQLSetCounterThread()\tCategory: " + counterSplit[1] + ":: Counter Name: " + counterSplit[2] + ":: Instance Name: " + counterSplit[3] + "\t" + ex.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tMSSQLSetCounterThread()\tCategory: " + counterSplit[1] + ":: Counter Name: " + counterSplit[2] + ":: Instance Name: " + counterSplit[3] + "\t" + ex.Message);
                    }
                }
                if (winCounterSet.MSSQL != null && winCounterSet.MSSQL.SLA != null)
                {
                    if (winCounterSet.MSSQL.SLA.Length > 0) { _slaCountersMSSQL.Clear(); _slaCountersMSSQL = new List<SLASetNew>(winCounterSet.MSSQL.SLA); }
                }
            }
            setCntThreadTimer.Stop();
            if (_debugMode)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSSQLSetCounterThread()\tMSSQL CounterSetting Completed in " + setCntThreadTimer.ElapsedMilliseconds + " ms");
            }
            setCntThreadTimer = null;
            if (_mssqlRunStatus.ToUpper().Trim() == "RESTART")
            {
                _mssqlRunStatus = "RUNNING";
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tMSSQLSetCounterThread()\tMSSQL Counter Restarted and startedCollecting Data...");
            }
//            _dbConnection = ConnectDataBase();
            collectMSSQL();
        }
        
        private void collectMSSQL()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSSQL()\tStarted ");
            long endtime;
            Int32 sleepTime;
            Int32 timerFreq = 20000; // in millisec
            bool runCond = true;
            CountersDetailNew changedCounterDetail = null;
            int i = 0; //to stop the multiple time entries while the counter is stopped & after restart
            int j = 0; // to check the mssql exist or not every one hour

            while (_runCond && runCond)
            {
                Stopwatch timeCollection = new Stopwatch();
                timeCollection.Start(); 
                j++;
                if (j > 180 )
                {
                    isMSSQLExists();
                    j = 0;
                }
                try
                {
                    Stopwatch stw = new Stopwatch();
                    stw.Start();
                    Stopwatch respTime = new Stopwatch();
                    if (_isMSSQLExist)
                    {
                        try
                        {
                            respTime.Start();
                            _mssqlRunStatus = _constants.GetPageContent(_path = GetPath() + "/getConfigurationsV2", string.Format("guid={0}&command=getUIDStatus", _guid_mssql));
                            //string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            //_mssqlRunStatus = (System.IO.File.ReadAllText(baseDir + @"\mssqlStatus.txt"));
                            respTime.Stop();
                            if (_debugMode)
                            {
                                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSSQL()\tgetWindowsStatus command MSSQL url response Time\t" + respTime.ElapsedMilliseconds + " ms");
                            }
                        }
                        catch (Exception e)
                        {
                            _mssqlRunStatus = string.Empty;
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectMSSQL()\tgetWindowsStatus command MSSSQL url not responding, proceeding with old setting\t" + e.Message);
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectMSSQL()\tgetWindowsStatus command MSSQL  url not responding,, proceeding with old setting\t" + e.Message + "\t" + e.StackTrace);
                        }
                        if (_mssqlRunStatus == string.Empty) _mssqlRunStatus = "{\"success\": true,\"failure\": false}";
                        changedCounterDetail = Utility.GetInstance().Deserialize<CountersDetailNew>(_mssqlRunStatus);
                        if (changedCounterDetail.message == null)
                        { _mssqlRunStatus = "RUNNING"; }
                        else
                        {
                            if (changedCounterDetail.message.ToUpper().Trim() == "DELETED")
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + " \tcollectMSSQL()\tMSSQL deleted from the card, hence collection of data will be stopped, To recreate restart the agent");
                                runCond = false;
                            }
                        }
                        if (changedCounterDetail.MSSQL != null && changedCounterDetail.MSSQL.message != null)
                        {
                            if (changedCounterDetail.MSSQL.message.ToUpper() == "STOP")
                            {
                                _mssqlRunStatus = "STOP";
                            }
                            else if (changedCounterDetail.MSSQL.message.ToUpper() == "RESTART")
                            {
                                if (changedCounterDetail.MSSQL.counters == null && changedCounterDetail.MSSQL.SLA == null)
                                {
                                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectMSSQL()\tRestart Command Received, Not received counter set or SLA set, hence Restart command ignored ");
                                    _mssqlRunStatus = "RUNNING";
                                }
                                else
                                {
                                    _mssqlRunStatus = "RESTART";
                                }
                            }
                            else { _mssqlRunStatus = "RUNNING"; }
                        }
                        else { _mssqlRunStatus = "RUNNING"; }

                        if (_mssqlRunStatus != "STOP" && _mssqlRunStatus != "RESTART")
                        {
                            if (i == 1)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSSQL()\tMSSQL Metric Collections Started ..... ");
                                i = 0;
                            }
                                CollectAndSendCounterMSSQL();
                        }
                        else if (_mssqlRunStatus.ToUpper().Trim() == "RESTART")
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSSQL()\tRestart Command Received, Restarting.....");
                            runCond = false;
                        }
                        else
                        {
                            if (i == 0)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSSQL()\tStop Command Received, Metric Collection Stopped");
                                i++;
                            }
                        }
                    }
                    timeCollection.Stop();
                    stw.Stop();
                    endtime = stw.ElapsedMilliseconds;
                    sleepTime = timerFreq - Convert.ToInt32(endtime);
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(sleepTime);
                    }
                }
                catch (Exception ex)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tcollectMSSQL()\tException Received, check errorlog for stack trace\t" + ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tcollectMSSQL()\tException Received\t" + ex.Message +"\t"+ex.StackTrace);
                }
                if (_debugMode)
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSSQL()\tTime Taken to complete collection of Metrics\t" + timeCollection.ElapsedMilliseconds + " ms");
            }
            if (_mssqlRunStatus == "RESTART")
            {
                _isFirstCounterMSSQL = true;
                MSSQLSetCounterThread(changedCounterDetail);
            }
        }
        
        private void collectMSIIS()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSIIS()\tStarted ");
            long endtime;
            Int32 sleepTime;
            Int32 timerFreq = 20000; // in millisec
            bool runCond = true;
            CountersDetailNew changedCounterDetail = null;
            int i = 0; //to stop the multiple time entries while the counter is stopped
            int j = 0; //to check the existance of MSIIS every one hour
            while (_runCond && runCond)
            {
                Stopwatch timeCollection = new Stopwatch();
                timeCollection.Start();
                j++;
                if (j > 180)
                {
                    isMSIISAppRunning();
                    j = 0;
                }

                try
                {
                    Stopwatch stw = new Stopwatch();
                    stw.Start();
                    Stopwatch respTime = new Stopwatch();
                    if (_isMSIISAppExist)
                    { 
                        try
                        {
                            respTime.Start();
                            _msiisRunStatus = _constants.GetPageContent(_path = GetPath() + "/getConfigurationsV2", string.Format("guid={0}&command=getUIDStatus", _guid_msiis));
                            //string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            //_msiisRunStatus = (System.IO.File.ReadAllText(baseDir + @"\msiisStatus.txt"));
                            respTime.Stop();
                            if (_debugMode)
                            {
                                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSIIS()\tgetWindowsStatus command MSIISurl response Time :" + respTime.ElapsedMilliseconds + " ms");
                            }
                        }
                        catch (Exception ex)
                        {
                            _msiisRunStatus = string.Empty; respTime = null;
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectMSIIS()\tgetWindowsStatus command MSIIS url not responding, proceeding with old setting\t" + ex.Message);
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectMSIIS()\tgetWindowsStatus command MSIIS  url not responding,, proceeding with old setting\t" + ex.Message+"\t"+ex.StackTrace);
                        }
                        if (_msiisRunStatus == string.Empty) _msiisRunStatus = "{\"success\": true,\"failure\": false}";
                        changedCounterDetail = Utility.GetInstance().Deserialize<CountersDetailNew>(_msiisRunStatus);
                        if (changedCounterDetail.message == null)
                        { _msiisRunStatus = "RUNNING"; }
                        else
                        {
                            if (changedCounterDetail.message.ToUpper().Trim() == "DELETED")
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + " \tcollectMSIIS()\tMSIIS deleted from the card, hence collection of data will be stopped, To recreate restart the agent");
                                runCond = false;
                            }
                        }
                        if (changedCounterDetail.MSIIS != null && changedCounterDetail.MSIIS.message != null)
                        {
                            if (changedCounterDetail.MSIIS.message.ToUpper() == "STOP")
                            {
                                _msiisRunStatus = "STOP";
                            }
                            else if (changedCounterDetail.MSIIS.message.ToUpper() == "RESTART")
                            {
                                if (changedCounterDetail.MSIIS.counters == null && changedCounterDetail.MSIIS.SLA == null)
                                {
                                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectMSIIS()\tRestart Command Received, Not received Metric set or SLA set, hence Restart command ignored ");
                                    _msiisRunStatus = "RUNNING";
                                }
                                else { _msiisRunStatus = "RESTART"; }
                            }
                            else { _mssqlRunStatus = "RUNNING"; }
                        }

                        if (_msiisRunStatus != "STOP" && _msiisRunStatus != "RESTART")
                        {
                            if (i == 1)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSIIS()\tMSIIS Metric Collections Started ..... ");
                                i = 0;
                            }
                            CollectAndSendCounterMSIIS();
                        }
                        else if (_msiisRunStatus == "RESTART")
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSIIS()\tRestart Command Received, Restarting.....");
                            runCond = false;
                        }
                        else
                        {
                            if (i == 0)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSIIS()\tStop Command Received, Metric Collection Stopped");
                                i++;
                            }
                        }
                    }
                    stw.Stop();
                    timeCollection.Stop();
                    endtime = stw.ElapsedMilliseconds;
                    sleepTime = timerFreq - Convert.ToInt32(endtime);
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(sleepTime);
                    }
                } //try end
                catch (Exception ex)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tcollectMSIIS()\tException Received, check error log for details\t" + ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tcollectMSIIS()\tException Received\t" + ex.Message + "\t" + ex.StackTrace);
                }
                if (_debugMode)
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectMSIIS()\tTime Taken to complete collection of Metrics\t" + timeCollection.ElapsedMilliseconds + " ms");
            } //while end
            if (_msiisRunStatus == "RESTART")
            {
                _isFirstCounterMSIIS = true;
                MSIISSetCounterThread(changedCounterDetail);
            }
        }
        private void collectWindows()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectWindows()\tStarted ");
            long endtime;
            Int32 sleepTime;
            Int32 timerFreq = 20000; // in millisec
            bool runCond = true;
            CountersDetailNew changedCounterDetail = null;
            int i = 0; //to stop the multiple time entries while the counter is stopped
            while (_runCond && runCond)
            {
                Stopwatch timeCollection = new Stopwatch();
                timeCollection.Start();
                setDebugMode();
//                setThreadMode();
                if (listProcessMetrics.Count()>0)
                    setProcessNames();
                try
                {
                    Stopwatch stw = new Stopwatch();
                    stw.Start();
                    Stopwatch respTime = new Stopwatch();
                    try
                    {
                        respTime.Start();
                        _windowsRunStatus = _constants.GetPageContent(_path = GetPath() + "/getConfigurationsV2", string.Format("guid={0}&command=getUIDStatus", _guid_windows));
                        //string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        //_windowsRunStatus = (System.IO.File.ReadAllText(baseDir + @"\winStatus.txt"));
                        respTime.Stop();
                        if (_debugMode)
                        {
                            DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectWindows()\tgetWindowsStatus url response Time :" + respTime.ElapsedMilliseconds + " ms");
                        }
                    }
                    catch (Exception e)
                    {
                        _windowsRunStatus = string.Empty; respTime = null;
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectWindows()\tgetWindowsStatus url not responding, proceeding with old setting\t" + e.Message);
                        ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tcollectWindows()\tgetWindowsStatus url not responding,, proceeding with old setting\t" + e.Message + "\t"+e.StackTrace);
                    }
                    if (_windowsRunStatus == string.Empty) _windowsRunStatus = "{\"success\": true,\"failure\": false}";
                    changedCounterDetail = Utility.GetInstance().Deserialize<CountersDetailNew>(_windowsRunStatus);
                    if (changedCounterDetail.message == null)
                    { _windowsRunStatus = "RUNNING"; }
                    else
                    {
                        if (changedCounterDetail.message.ToUpper().Trim() == "DELETED")
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + " \tcollectWindows()\tWindows deleted from the card, hence collection of data will be stopped, To recreate restart the agent");
                            runCond = false;
                        }
                    }
                    if (changedCounterDetail.WINDOWS != null && changedCounterDetail.WINDOWS.message != null)
                    {
                        if (changedCounterDetail.WINDOWS.message.ToUpper() == "STOP")
                        {
                            _windowsRunStatus = "STOP";
                        }
                        else if (changedCounterDetail.WINDOWS.message.ToUpper() == "RESTART")
                        {
                            if (changedCounterDetail.WINDOWS.counters == null && changedCounterDetail.WINDOWS.SLA == null)
                            {
                                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + " \tcollectWindows()\tRestart Command Received, Not received counter set or SLA set, hence Restart command ignored ");
                                _windowsRunStatus = "RUNNING";
                            }
                            else
                            {
                                _windowsRunStatus = "RESTART";
                            }
                        }
                        else { _windowsRunStatus = "RUNNING"; }
                    }

                    if (_windowsRunStatus != "STOP" && _windowsRunStatus != "RESTART") 
                    {
                        if (i == 1)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectWindows()\tWindows Metric Collections Started ..... ");
                            i = 0;
                        }
                        CollectAndSendCounterWindows(); 
                    }
                    else if (_windowsRunStatus == "RESTART") 
                    {
                        DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectWindows()\tRestart Command Received, Restarting....." );
                        runCond = false;
                    }
                    else
                    {
                        if (i == 0)
                        {
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectWindows()\tStop Command Received, Metric Collection Stopped");
                            i++;
                        }
                    }
                    stw.Stop();
                    timeCollection.Stop();
                    endtime = stw.ElapsedMilliseconds;
                    sleepTime = timerFreq - Convert.ToInt32(endtime);
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(sleepTime);
                    }
                }
                catch (Exception ex)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tcollectWindows()\tException Received, check error log for details\t" + ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tcollectWindows() \tException Received\t" + ex.Message+"\t"+ex.StackTrace);
                }
                if (_debugMode)
                    DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tcollectWindows()\tTime Taken to complete collection of Metrics\t" + timeCollection.ElapsedMilliseconds + " ms");
            }
            if (_windowsRunStatus == "RESTART")
            {
                _isFirstCounterValue = true;
                WindowsSetCounterThread(changedCounterDetail);
            }
        }

        /// <summary>
        /// to collect the top consuming processor and memory. 
        /// on getting any exception while collecting values, need to reset the counter collection
        /// </summary>
        /// <param name="processor_counter_id"></param>
        /// <param name="memory_counter_id"></param>
        public void setTopProcess(string counterName, string counter_id)
        {
            Stopwatch timeSetPrIn = new Stopwatch();
            timeSetPrIn.Start();
            PerformanceCounterCategory categoryProcess = new PerformanceCounterCategory("Process");
            string[] instances = categoryProcess.GetInstanceNames();
            foreach (string inst in instances)
            {
                try
                {
                    ProcessMetricClass pmc = new ProcessMetricClass();
                    PerformanceCounter cnt = new PerformanceCounter("Process", counterName, inst);
                    cnt.NextValue();
                    pmc.counter_id = counter_id;
                    pmc.pcnter = cnt;
                    _metricTopProcess.Add(pmc);
                    Thread.Sleep(2); //to avoid cpu bursting while collecting process details
                }
                catch
                {
                    //DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetTopProcess() \tInstance "+inst+" getting exception, this instance will not be collected "+ ex.Message);
                    //ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tsetTopProcess()\tInstance " + inst + " getting exception, this instance will not be collected " + ex.Message + "\t" + ex.StackTrace);
                }
            }
            timeSetPrIn.Stop();
            if (_debugMode)
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetTopProcess()\tTime Taken to SetTopProcessCnters\t" + timeSetPrIn.ElapsedMilliseconds + " ms");
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tsetTopProcess()\tCurrent Running Process (" + _metricTopProcess.Count().ToString() + ") Instances are set for " + counterName + " and ready for collection");
            categoryProcess = null;
            instances = null;
            timeSetPrIn = null;
        }
        /// <summary>
        ///  Get path from config file. This path is used to send data to Appedo collector.
        /// </summary>
        /// <returns>Appedo collector path</returns>
        private string GetPath()
        {
            return string.Format("{0}://{1}:{2}/{3}", System.Configuration.ConfigurationManager.AppSettings["protocol"], System.Configuration.ConfigurationManager.AppSettings["server"], System.Configuration.ConfigurationManager.AppSettings["port"], System.Configuration.ConfigurationManager.AppSettings["path"]);
        }

        /// <summary>
        /// Get all child counters for given counter
        /// </summary>
        /// <param name="counterid">Parent counter id</param>
        /// <param name="Category">Parent counter category</param>
        /// <param name="name">Parent counter name</param>
        /// <returns></returns>
        private ParentCounter GetChdCounter(string counterid, string Category, string name)
        {
            DateTime startTime = DateTime.Now;
            ParentCounter parentCounter = new ParentCounter();
            parentCounter.ParentCounterId = counterid;
            parentCounter.ChildCounterDetail = new List<ChildCounterDetail>();


            PerformanceCounterCategory category = new PerformanceCounterCategory(Category);
            StringBuilder strInstance = new StringBuilder();

            //Get all child counter one by one and store detail.
            foreach (string instancename in category.GetInstanceNames())
            {
                ChildCounterDetail child = new ChildCounterDetail();
                child.Name = HttpUtility.UrlEncode(name);
                child.HasInstace = false;
                child.InstanceName = HttpUtility.UrlEncode(instancename);
                child.Category = HttpUtility.UrlEncode(Category);
                parentCounter.ChildCounterDetail.Add(child);
            }
            if (ExceptionHandler.DebugEnabled) ExceptionHandler.LogDebugMessage(startTime, System.Reflection.MethodBase.GetCurrentMethod().Name);
            return parentCounter;
        }

        /// <summary>
        /// Get all network interfaces
        /// </summary>
        /// <returns></returns>
        private List<string> GetNetworkInterface()
        {
            PerformanceCounterCategory cat = new PerformanceCounterCategory("Network Interface");
            return cat.GetInstanceNames().ToList();
        }

        /// <summary>
        /// Get Bytes Sent/sec
        /// </summary>
        /// <returns></returns>
        private double GetBytesSent()
        {
            DateTime startTime = DateTime.Now;
            double total = 0;
            List<string> interfaces = GetNetworkInterface();
            foreach (string interfaceName in interfaces)
            {
                PerformanceCounter conter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", interfaceName);
                conter.NextValue();
                total += conter.NextValue();
            }
            if (ExceptionHandler.DebugEnabled) ExceptionHandler.LogDebugMessage(startTime, System.Reflection.MethodBase.GetCurrentMethod().Name);
            return total;
        }

        /// <summary>
        /// Get Bytes Received/sec
        /// </summary>
        /// <returns></returns>
        private double GetBytesReceived()
        {
            DateTime startTime = DateTime.Now;
            double total = 0;
            List<string> interfaces = GetNetworkInterface();
            foreach (string interfaceName in interfaces)
            {
                PerformanceCounter conter = new PerformanceCounter("Network Interface", "Bytes Received/sec", interfaceName);
                conter.NextValue();
                total += conter.NextValue();
            }
            if (ExceptionHandler.DebugEnabled) ExceptionHandler.LogDebugMessage(startTime, System.Reflection.MethodBase.GetCurrentMethod().Name);
            return total;
        }

        /// <summary>
        /// Establish connection to mssql database server.
        /// </summary>
        /// <returns>Connection detail</returns>
        private SqlConnection ConnectDataBase()
        {
            DateTime startTime = DateTime.Now;
            SqlConnection con = null;
            string sqlServer = System.Configuration.ConfigurationManager.AppSettings["SqlServer"];
            if (sqlServer != string.Empty)
            {
                try
                {
                    string test = string.Format("Server={0};Database={1};User Id={2};Password={3};Trusted_Connection={4}",
                       sqlServer,
                       System.Configuration.ConfigurationManager.AppSettings["SqlDbName"],
                       System.Configuration.ConfigurationManager.AppSettings["SqlUserId"],
                       System.Configuration.ConfigurationManager.AppSettings["SqlPassword"],
                       System.Configuration.ConfigurationManager.AppSettings["SqlTrustedConnection"]);
                    con = new SqlConnection(test);
                    _connectionString = test;
                    con.Open();
                    _mssqlVersion = con.ServerVersion.ToString();
                    string[] sqlVersion = _mssqlVersion.Split('.');
                    _mssqlMjrVer = Convert.ToInt32(sqlVersion[0]);
                    _mssqlConnectionException = false;
                    con.Close();
                    _dbConnection = con;
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tConnectDataBase()\tMSSQL Version " + _mssqlVersion + " SQL database connection to " + sqlServer + " is established successfully");
                }
                catch (Exception ex)
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tConnectDataBase()\tFalied to Connect to " + sqlServer + ". Check event log for exact nature of failure. " + ex.Message);
                    ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tConnectDataBase()\tFalied to Connect to " + sqlServer + ". Check event log for exact nature of failure. " + ex.Message + "\t" + ex.StackTrace);
                    con = null;
                    _mssqlConnectionException = true;
                }
            }
            else
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tConnectDataBase()\tFalied to Connect as SQL SERVER is not configured in the configuration file");
                con = null;
                _isMSSQLExist = false;
            }
            return con;
        }
        #region GetProcess
        /// <summary>
        /// Getting process information
        /// </summary>
        /// <returns></returns>
/*        private string getProcessThreadInstances(string strCategory, string dt, string key, string counterName, string tp)
        {
            Stopwatch gpti = new Stopwatch();
            gpti.Start();
            processClass cntValues = new processClass();
            List<processClass> processList = new List<processClass>();
            List<threadClass> threadList = new List<threadClass>();
            List<processClass> topProcessList = new List<processClass>();

            StringBuilder dataSet = new StringBuilder();
            string[] instances;
            Process[] runningProcesses = null;
            if (tp == string.Empty)
            {
                instances = _processNames.Split(',');
                foreach (string instance in instances)
                {
                    runningProcesses = Process.GetProcessesByName(instance);
                    foreach (Process process in runningProcesses)
                    {
                        decimal cntValue = 0;
                        try
                        {
                            PerformanceCounter cnt = new PerformanceCounter(strCategory, counterName, process.ProcessName);
                            cntValue = Convert.ToDecimal(cnt.NextValue());
                            processList.Add(setMetric(process, cntValue, key, string.Empty));
                            //if (_getThreadDetails && counterName == "% Processor Time")
                            //    threadList = threadList.Concat(collectThreadDetails(process, key)).ToList();
                        }
                        catch (Exception ex)
                        {
                            processList.Add(setMetric(process, cntValue, key, ex.Message));
                            ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + " \tException in" + strCategory + "," + counterName + "," + process.ProcessName + "\t" + ex.Message + "\t" + ex.StackTrace);
                        }
                    }
                }
            }
            if (counterName == "% Processor Time")
            {
                runningProcesses = Process.GetProcesses();
                DataFileHandler.sendToNotifnQ("Total no. of running processes " + runningProcesses.Count());
                foreach (Process process in runningProcesses)
                {
                    decimal cntValue = 0;
                    try
                    {
                        PerformanceCounter cnt = new PerformanceCounter(strCategory, counterName, process.ProcessName);
                        cntValue = Convert.ToDecimal(cnt.NextValue());
                        if (cntValue > 0)
                            topProcessList.Add(setMetric(process, cntValue, key, string.Empty));
                    }
                    catch (Exception ex)
                    {
                        topProcessList.Add(setMetric(process, cntValue, key, ex.Message));
                    }
                }
            }
            if (processList.Count() > 0)
            {
                dataSet.Append(processList.Count() > 0 ? "," : "");
                dataSet.Append("\"Process-").Append(counterName).Append("\":");
                dataSet.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(processList)));
            }
            if (topProcessList.Count() > 0)
            {
                dataSet.Append(processList.Count() > 0 ? "," : "");
                dataSet.Append("\"TopProcess-").Append(counterName).Append("\":");
                dataSet.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(topProcessList)));
            }
            if (threadList.Count() > 0)
            {
                dataSet.Append(threadList.Count() > 0 ? "," : "");
                dataSet.Append("\"Thread-").Append(counterName).Append("\":");
                dataSet.Append(ASCIIEncoding.ASCII.GetString(_constants.Serialize(threadList)));
            }
            gpti.Stop();
            if (_debugMode)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + " \tgetProcessThreadInstances() \tTime taken to getcounterValue for all process of " + strCategory + "," + counterName + "\t" + gpti.ElapsedMilliseconds + " ms");
            }
            processList = null;
            topProcessList = null;
            threadList = null;
            return dataSet.ToString();
        }
*/
        public processClass setMetric(Process processDetails, decimal metricValue, string key, string exception)
        {
            processClass processData = new processClass();
            processData.counter_type = key.ToString();
            processData.process_name = processDetails.ProcessName.ToString();
            processData.counter_value = metricValue;
            processData.exception = string.Empty;
            //processData.process_id = processDetails.Id.ToString();
            //processData.thread_count = processDetails.Threads.Count;
            //processData.handle_count = processDetails.HandleCount;
            processData.exception = exception;
            return processData;
        }

        //public List<threadClass> collectThreadDetails(Process processDetails, string key)
        //{
        //    Stopwatch timeThreadColl = new Stopwatch();
        //    timeThreadColl.Start();
        //    List<threadClass> processThreadColl = new List<threadClass>();
        //    ProcessThreadCollection threadColl = processDetails.Threads;

        //    foreach (ProcessThread thread in threadColl)
        //    {
        //       threadClass threadEntity = new threadClass();
        //       threadEntity.counter_id = key.ToString();
        //       threadEntity.process_id = processDetails.Id;
        //       threadEntity.process_name = processDetails.ProcessName;
        //       threadEntity.thread_id = thread.Id;
        //       threadEntity.thread_state = thread.ThreadState.ToString();
        //       try { threadEntity.thread_wait_reason = thread.WaitReason.ToString(); }
        //       catch { threadEntity.thread_wait_reason = string.Empty; }
        //       try { DateTime st = thread.StartTime; threadEntity.start_time = st.GetDateTimeFormats()[58]; threadEntity.elapsed_time =Convert.ToInt32(DateTime.Now.Subtract(st).TotalMilliseconds); }
        //       catch { threadEntity.start_time = string.Empty; }
        //       try { threadEntity.total_processor_time = thread.TotalProcessorTime.TotalMilliseconds.ToString(); }
        //       catch { threadEntity.total_processor_time = string.Empty; }
        //       processThreadColl.Add(threadEntity);
        //   }
        //    timeThreadColl.Stop();
        //    if (_debugMode)
        //        DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + " \tcollectThreadDetails() \tTime Taken to collect All thread for process " + processDetails.ProcessName + "\t" + timeThreadColl.ElapsedMilliseconds + " ms");
        //    threadColl = null;
        //    return processThreadColl;
        //}


        #endregion
        #endregion

        #region  ICMP TEST - for servers 
        private void PingService()
        {
            try
            {
                string filePath = AppDomain.CurrentDomain.BaseDirectory;
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo" + Environment.MachineName + "\tPingService()\tChecking the availability of pingservers.txt file in " + filePath+" Ping result will be published once in 2 hours for all servers in the log. Currently scheduled for every minute." );
                if (File.Exists(filePath + @"\pingservers.txt"))
                {
                    List<string> serverColl = File.ReadAllLines(filePath + @"\pingservers.txt").ToList<string>();
                    serverColl.RemoveAt(0);
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo" + Environment.MachineName + "\tPingService()\tICMP Service started for " + serverColl.Count + " servers");
                    foreach (string server in serverColl)
                    {
                        Task task = Task.Run(() =>
                        {
                            int cnt = 0;
                            while (_runICMPTest)
                            {
                                Ping pngServer = new Ping();
                                var pingResult = pngServer.Send(server);
                                if (cnt == 0)
                                {
                                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo" + Environment.MachineName + "\tPingService()\tping result for server " + server + " is " + pingResult.Status.ToString());
                                }
                                cnt++;
                                if(cnt == 2 * 60 * 60)
                                {
                                    cnt = 0;
                                }
                                Thread.Sleep(1000);
                            }
                            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo" + Environment.MachineName + "\tPingService()\tICMP Service stopped for server " + server);
                        });
                    }
                }
                else
                {
                    DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tError" + Environment.MachineName + "\tPingService()\tfile pingservers.txt does not exist in " + filePath);
                }
            } 
            catch (Exception ex)
            {
                DataFileHandler.sendToDebugQ(DataFileHandler.getDTTZ() + "\tCritical" + Environment.MachineName + "\tPingService()\tPing service returned with exception " + ex.Message + " trace " + ex.StackTrace);
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tCritical" + Environment.MachineName + "\tPingService()\tPing service returned with exception " + ex.Message + " trace "+ ex.StackTrace);
            }
        }
        #endregion
    }
}

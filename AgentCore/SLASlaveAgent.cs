using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AgentCore
{
    /// <summary>
    /// It is used to create SLASlave agent. It executes VBS file. It notify availability of SLASlave agent to Appedo.
    /// </summary>
    public class SLASlaveAgent
    {
        #region The private fields

        private Utility constants = Utility.GetInstance();
        private Dictionary<string, PerformanceCounter> Counters = new Dictionary<string, PerformanceCounter>();
        private Dictionary<string, List<PerformanceCounter>> CountersAllInstance = new Dictionary<string, List<PerformanceCounter>>();
        private int _port = 0;
        private int _frequency = 10000;
        private string _type = string.Empty;
        private string _slaveuserid = string.Empty;
        private string _osType = string.Empty;
        private string _status = string.Empty;
        private string _slaveVersion = string.Empty;
        private string _path = string.Empty;
        private string _dataSendUrl = string.Empty;
        private string _counterValue = string.Empty;
        private string _responseStr = string.Empty;
        private Thread _doWorkThread = null;
        private Thread _slaListenerThread = null;
        private string _scriptName = string.Empty;
        private TcpListener _serverSocket = null;
        private Dictionary<string, string> _postData = new Dictionary<string, string>();
        private string _pageContent = string.Empty;

        #endregion

        #region The public methods

        /// <summary>
        /// Used to create a SLASlave agent instance.
        /// </summary>
        /// <param name="type">Type from config file.</param>
        /// <param name="slaveuserid">slaveuserid from config file.</param>
        /// <param name="port">Port from config file. Listener port for SLASlave agent.</param>
        /// <param name="frequency">Frequency from config file. Availability notification frequency.</param>
        public SLASlaveAgent(string type, string slaveuserid, string port, string frequency)
        {

            ServicePointManager.ServerCertificateValidationCallback = ((sender, certificate, chain, sslPolicyErrors) => true);
            _frequency = Convert.ToInt32(frequency);
            _port = int.Parse(port);
            _serverSocket = new TcpListener(_port);

            _type = type;
            _slaveuserid = slaveuserid;
            _slaveVersion = constants.ExecutingAssemblyVersion;
            _osType = "Windows";
            _status = "active";

        }

        /// <summary>
        /// Used to send first request to appedo. It will try until it succeed.
        /// </summary>
        public void StartSLASlaveAgent()
        {

            _postData.Add("command", "slaveuserid");
            _postData.Add("msg", "{\"slaveuserid\":\"" + _slaveuserid + "\"}");

            while (true)
            {
                //It will send notification to appedo by updateslaveStatus with command and message.
                _pageContent = constants.GetPageContent(_path = GetPath() + "/updateslaveStatus", _postData);

                //Response from appedo for updateslaveStatus request contain "success":true message.
                if (_pageContent.ToLower() == "{\"success\":true}".ToLower())
                {

                    SLASlaveInfo info = new SLASlaveInfo();
                    info.success = true;
                    info.agent_type = _type;
                    info.mac = constants.MacAddress;
                    info.slaveuserid = _slaveuserid;
                    info.ipaddress = constants.LocalIPAddress;
                    info.os_type = _osType;
                    info.operating_system = constants.OSName;
                    info.os_version = constants.OSVersion;
                    info.is_active = true;
                    info.remarks = "";
                    info.status = _status;
                    info.slave_version = _slaveVersion;
                    _postData["command"] = "SLAVEDETAILS";
                    _postData["msg"] = Encoding.ASCII.GetString(Utility.GetInstance().Serialize(info));
                    ExceptionHandler.WritetoEventLog("First request sent successfully");
                }
                else
                {
                    ExceptionHandler.WritetoEventLog(_pageContent);
                }

                //It will send notification to appedo by updateslaveStatus with command and message. Message contains slave agent information.
                _pageContent = constants.GetPageContent(_path = GetPath() + "/updateslaveStatus", _postData);

                //Response from appedo for updateslaveStatus request contain "success":true message.
                if (_pageContent.ToLower() == "{\"success\":true}".ToLower())
                {
                    try
                    {
                        _doWorkThread = new Thread(new ThreadStart(Notify));
                        _doWorkThread.Start();

                        _serverSocket.Start();
                        _slaListenerThread = new Thread(new ThreadStart(SLASlaveListener));
                        _slaListenerThread.Start();
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
                    }

                    break;
                }
                else
                {
                    ExceptionHandler.WritetoEventLog(_pageContent);
                }
                Thread.Sleep(5000);
            }
        }

        #endregion

        #region The private methods

        /// <summary>
        /// Send status to appedo in frequent interval.
        /// </summary>
        private void Notify()
        {
            try
            {
                Status status = new Status();
                status.mac = constants.MacAddress;
                status.operating_system = constants.OSName;
                status.os_version = constants.OSVersion;
                status.status = "active";
                _postData["command"] = "SLAVESTATUS";
                _postData["msg"] = Encoding.ASCII.GetString(Utility.GetInstance().Serialize(status));
                while (true)
                {
                    try
                    {
                        _pageContent = constants.GetPageContent(_path = GetPath() + "/updateslaveStatus", _postData);
                        Thread.Sleep(_frequency);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
            }
        }

        /// <summary>
        /// Here port is listening to receive command from appedo and execute VBS from appedo.
        /// </summary>
        private void SLASlaveListener()
        {
            try
            {
                while ((true))
                {
                    ExceptionHandler.WritetoEventLog("TcpListener listen on " + _port.ToString());
                    Trasport controller = new Trasport(_serverSocket.AcceptTcpClient());
                    ExceptionHandler.WritetoEventLog("Request received");
                    new Thread(() =>
                    {
                        try
                        {
                            TrasportData data = controller.Receive();
                            switch (data.Operation.ToLower())
                            {

                                case "test":
                                    {
                                        controller.Send(new TrasportData("ok", string.Empty, null));
                                    }
                                    break;

                                    // Executes VBS
                                case "action":
                                    {
                                        ExceptionHandler.WritetoEventLog("action request received");
                                        string filePath = System.IO.Path.GetTempPath() + "\\" + DateTime.Now.Ticks.ToString() + ".vbs";
                                        string secIP = data.Header["secondaryip"];
                                        string secPort = data.Header["secondaryport"];
                                        data.Save(filePath);
                                        ExceptionHandler.WritetoEventLog("File saved on " + filePath);
                                        StringBuilder result = new StringBuilder();
                                        try
                                        {
                                            //secondaryip is na(not applicable)
                                            if (secIP.ToLower() == "na")
                                            {
                                                try
                                                {
                                                    StringBuilder output = new StringBuilder();
                                                    StringBuilder errorOutput = new StringBuilder();
                                                    DateTime startTime = new DateTime();
                                                    DateTime endTime = new DateTime();
                                                    bool IsSuccess = true;
                                                    ExecuteScript(filePath, ref startTime, ref endTime, ref output, ref errorOutput, ref IsSuccess);

                                                    Dictionary<string, string> headers = new Dictionary<string, string>();
                                                    headers.Add("os", constants.OSName);
                                                    headers.Add("actionstarttime", Utility.GetInstance().GetEpochTime(startTime));
                                                    headers.Add("startoffset", DateTimeOffset.Now.Offset.TotalMinutes.ToString());
                                                    headers.Add("actionendtime", Utility.GetInstance().GetEpochTime(endTime));
                                                    headers.Add("endoffset", DateTimeOffset.Now.Offset.TotalMinutes.ToString());
                                                    headers.Add("error", "");
                                                    headers.Add("log", DateTime.Now.Ticks.ToString() + ".log");

                                                    //Script executed successfully.
                                                    if (IsSuccess == true)
                                                    {
                                                        controller.Send(new TrasportData("OK", output.ToString(), headers));

                                                    }
                                                    //Unable to execute script.
                                                    else
                                                    {
                                                        headers["error"] = "Unable to execute given script";
                                                        controller.Send(new TrasportData("ERROR", errorOutput.ToString(), headers));
                                                    }

                                                    //Delete script file after execution if script file exists.
                                                    if (File.Exists(filePath)) File.Delete(filePath);
                                                }
                                                catch (Exception ex)
                                                {
                                                    ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
                                                }

                                            }

                                            //Response contains secondaryip.
                                            else
                                            {
                                                try
                                                {
                                                    Trasport chiled = new Trasport(secIP, secPort);
                                                    Dictionary<string, string> header = new Dictionary<string, string>();
                                                    header.Add("secondaryip", "na");
                                                    header.Add("secondaryport", "na");
                                                    chiled.Send(new TrasportData("action", header, filePath));
                                                    TrasportData chiledResult = chiled.Receive();
                                                    controller.Send(chiledResult);
                                                    if (File.Exists(filePath)) File.Delete(filePath);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Dictionary<string, string> headers = new Dictionary<string, string>();
                                                    headers.Add("os", constants.OSName);
                                                    headers.Add("actionstarttime", Utility.GetInstance().GetEpochTime(DateTime.Now));
                                                    headers.Add("startoffset", DateTimeOffset.Now.Offset.TotalMinutes.ToString());
                                                    headers.Add("actionendtime", Utility.GetInstance().GetEpochTime(DateTime.Now));
                                                    headers.Add("endoffset", DateTimeOffset.Now.Offset.TotalMinutes.ToString());
                                                    headers.Add("error", ex.Message.ToString());
                                                    headers.Add("log", "sample.log");
                                                    controller.Send(new TrasportData("ERROR", ex.Message.ToString(), headers));

                                                }
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
                                        }
                                    }
                                    break;
                            }
                            controller.Close();
                        }
                        catch (Exception ex)
                        {
                            ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
                        }
                    }).Start();
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(ex.StackTrace + ex.Message);
            }
        }

        /// <summary>
        /// Get path to send data to appedo from config file.
        /// </summary>
        /// <returns></returns>
        private string GetPath()
        {
            return string.Format("{0}://{1}:{2}/{3}", System.Configuration.ConfigurationManager.AppSettings["protocol"], System.Configuration.ConfigurationManager.AppSettings["server"], System.Configuration.ConfigurationManager.AppSettings["port"], System.Configuration.ConfigurationManager.AppSettings["path"]);
        }

        /// <summary>
        /// To execute VBS script.
        /// </summary>
        /// <param name="path">Script file path</param>
        /// <param name="startTime">Script execution start time. ref type parameter</param>
        /// <param name="endTime">Script execution end time. ref type parameter</param>
        /// <param name="output">Script execution output<. ref type parameter/param>
        /// <param name="ErrorOutput">Script execution error output. ref type parameter</param>
        /// <param name="isSuccess"></param>
        private void ExecuteScript(string path, ref DateTime startTime, ref DateTime endTime, ref StringBuilder output, ref StringBuilder ErrorOutput, ref bool isSuccess)
        {
            StringBuilder result = new StringBuilder();
            startTime = DateTime.Now;
            try
            {
                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = "cscript.exe";
                process.StartInfo.Arguments = path;
                process.Start();

                string str;

                //Read output console text.
                while ((str = process.StandardOutput.ReadLine()) != null)
                {
                    output.AppendLine(str);
                }

                result.AppendLine().AppendLine();

                //Read error output console text.
                while ((str = process.StandardError.ReadLine()) != null)
                {
                    isSuccess = false;
                    ErrorOutput.AppendLine(str);
                }
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                ErrorOutput.Append(ex.Message);
                isSuccess = false;
            }
            endTime = DateTime.Now;

        }

        #endregion
    }
}
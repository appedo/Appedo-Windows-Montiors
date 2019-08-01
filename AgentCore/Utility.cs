﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace AgentCore
{
    /// <summary>
    /// It is single instance class. Used to provide commonly used utility functions
    /// </summary>
    
    public class Utility
    {
        #region The private fields

        private string _executingAssplyFolder = string.Empty;
        private string _executingAssplyVersion = string.Empty;
        private string _macAddresses = string.Empty;
        private string _osName = string.Empty;
        private string _osVersion = string.Empty;
        private string _ipAddress = string.Empty;

        #endregion

        #region  The public property

        //Get local mac address
        public string MacAddress
        {
            get
            {
                if (_macAddresses == string.Empty)
                {
                    foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus == OperationalStatus.Up)
                        {
                            _macAddresses += nic.GetPhysicalAddress().ToString();
                            break;
                        }
                    }
                }
                return _macAddresses;
            }
            private set { }

        }

        // Get local OS name
        public string OSName
        {
            get
            {
                if (_osName == string.Empty)
                {
                    _osName = new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName;
                }
                return _osName;
            }
            private set { }

        }

        //Get local OS version
        public string OSVersion
        {
            get
            {
                if (_osVersion == string.Empty)
                {
                    _osVersion = new Microsoft.VisualBasic.Devices.ComputerInfo().OSVersion;
                }
                return _osVersion;
            }
            private set { }

        }

        // Get executing application version
        public string ExecutingAssemblyVersion
        {
            get
            {
                if (_executingAssplyVersion == string.Empty)
                {
                    _executingAssplyVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
                    _executingAssplyVersion = _executingAssplyVersion.Remove(_executingAssplyVersion.Length - 2);
                }
                return _executingAssplyVersion;
            }
            private set { }
        }

        //Get executing application file path 
        public string ExecutingAssemblyLocation
        {
            get
            {
                if (_executingAssplyFolder == string.Empty)
                {
                    _executingAssplyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                }
                return _executingAssplyFolder;
            }
            private set { }
        }

        //Get Local IP address
        public string LocalIPAddress
        {
            get
            {
                if (_ipAddress == string.Empty)
                {
                    IPHostEntry host;
                    host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (IPAddress ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            _ipAddress = ip.ToString();
                            break;
                        }
                    }
                }
                return _ipAddress;
            }
            private set { }
        }

        #endregion

        #region The static fields and methods

        private static Utility _instance;
        public static Utility GetInstance()
        {
            if (_instance == null)
            {
                _instance = new Utility();
            }
            return _instance;
        }

        #endregion

        #region The public methods

        //Call http request
        public string GetPageContent(string Url)
        {

            HttpWebRequest WebRequestObject = (HttpWebRequest)HttpWebRequest.Create(Url);
            WebResponse Response = WebRequestObject.GetResponse();
            Stream WebStream = Response.GetResponseStream();
            StreamReader objReader = new StreamReader(WebStream);
            string PageContent = objReader.ReadToEnd();
            objReader.Close();
            WebStream.Close();
            Response.Close();
            return PageContent;
        }

        //Call http request with post data
        public string GetPageContent(string Url, string data)
        {
            string PageContent = string.Empty;
            HttpWebRequest WebRequestObject = null;
            try
            {
                WebRequestObject = (HttpWebRequest)HttpWebRequest.Create(Url);
                WebRequestObject.Method = "POST";
                byte[] databytes = ASCIIEncoding.ASCII.GetBytes(data);

                WebRequestObject.ContentLength = databytes.Length;
                using (Stream stream = WebRequestObject.GetRequestStream())
                {
                    stream.Write(databytes, 0, databytes.Length);
                }

                WebResponse Response = WebRequestObject.GetResponse();
                Stream WebStream = Response.GetResponseStream();
                StreamReader objReader = new StreamReader(WebStream);
                PageContent = objReader.ReadToEnd();
                objReader.Close();
                WebStream.Close();
                Response.Close();
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(DateTime.Now.ToString() + "\t" + "GetPageContent() " + Url+ "\t"+ex.Message);
            }
            finally
            {
                WebRequestObject = null;
            }
            return PageContent;
        }

        //Call http request with headers
        public string GetPageContent(string Url, Dictionary<string, string> headers)
        {
            string PageContent = string.Empty;
            HttpWebRequest WebRequestObject = null;
            string data = string.Empty;
            try
            {
                WebRequestObject = (HttpWebRequest)HttpWebRequest.Create(Url);
                WebRequestObject.Method = "POST";
                WebRequestObject.ContentLength = data.Length;
                foreach (string key in headers.Keys)
                {
                    WebRequestObject.Headers.Add(key, headers[key]);
                }
                using (Stream stream = WebRequestObject.GetRequestStream())
                {
                    stream.Write(ASCIIEncoding.ASCII.GetBytes(data), 0, data.Length);
                }

                WebResponse Response = WebRequestObject.GetResponse();
                Stream WebStream = Response.GetResponseStream();
                StreamReader objReader = new StreamReader(WebStream);
                PageContent = objReader.ReadToEnd();
                objReader.Close();
                WebStream.Close();
                Response.Close();
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(DateTime.Now.ToString() + "\t" + "GetPageContent() " + Url + "\t" + ex.Message);
            }
            finally
            {
                WebRequestObject = null;
            }
            return PageContent;
        }

        /// <summary>
        /// Deserialise string into object.
        /// </summary>
        /// <param name="responseStr">Response string from Appedo server</param>
        /// <returns>Deserialised object</returns>
        public CountersDetail GetCountersDetail(string responseStr)
        {
            CountersDetail res = Deserialize<CountersDetail>(responseStr);
            return res;
        }

        public CountersDetailNew GetCountersDetailV1(string responseStr )
        {
            CountersDetailNew res = Deserialize<CountersDetailNew>(responseStr);
            return res;
        }

        /// <summary>
        /// Deserialize utility function. Deserialize Json string into object
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="json">Json string</param>
        /// <returns>Converted object</returns>
        public T Deserialize<T>(string json)
        {
            DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(T));

            using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(json)))
            {
                T result = (T)deserializer.ReadObject(stream);
                return result;
            }
        }

        /// <summary>
        /// Convert key and value pair into post data.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public string GetPostData(Dictionary<string, string> data)
        {
            StringBuilder str = new StringBuilder();
            int count = data.Count;

            //Get all keys one by one
            foreach (string postData in data.Keys)
            {

                str.Append(HttpUtility.UrlEncode(postData)).Append("=").Append(HttpUtility.UrlEncode(data[postData]));

                //if key is not last element in collection. it will not append '&' char in last key and value pair.
                if (count != 1) str.Append("&");
                count--;
            }
            return str.ToString();
        }

        /// <summary>
        /// Serialise utility function. Serialise object string into Json.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public byte[] Serialize<T>(T obj)
        {
            MemoryStream stream1 = new MemoryStream();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            ser.WriteObject(stream1, obj);
            stream1.Seek(0, SeekOrigin.Begin);
            return stream1.ToArray();
        }

        /// <summary>
        /// To get EpochTime from DateTime
        /// </summary>
        /// <param name="date">DateTime value to convert to EpochTime</param>
        /// <returns>EpochTime in milliseconds</returns>
        public string GetEpochTime(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalMilliseconds).ToString();
        }

        #endregion

    }

    /// <summary>
    /// Data model to store selected monitor counter and sla breach info. It is act as DataContract between the SLA agent to Appedo.
    /// </summary>
    [DataContract]
    public class CountersDetail
    {
        [DataMember(Name = "success")]
        public bool success { get; set; }

        [DataMember(Name = "failure")]
        public bool failure { get; set; }

        [DataMember(Name = "MonitorCounterSet")]
        public MonitorCounter[] MonitorCounterSet { get; set; }

        [DataMember(Name = "SlaCounterSet")]
        public SlaCounter[] SlaCounterSet { get; set; }
    }

    /// <summary>
    /// Data model to store counter query and counter id. It is act as DataContract between the SLA agent to Appedo.
    /// </summary>
    [DataContract]
    public class MonitorCounter
    {
        [DataMember(Name = "counter_id")]
        public int counter_id { get; set; }

        [DataMember(Name = "query")]
        public string query { get; set; }
    }

    /// <summary>
    /// Data model to store sla breach counter info. It is act as DataContract between the SLA agent to Appedo.
    /// </summary>
    [DataContract]
    public class SlaCounter
    {
        private bool isBreach = false;
        private decimal receivedValue = 0;
        private string guid = string.Empty;
        private string sla_breach_severity = string.Empty;

        [DataMember(Name = "slaid")]
        public int slaid { get; set; }

        [DataMember(Name = "userid")]
        public int userid { get; set; }

        [DataMember(Name = "counterid")]
        public int counterid { get; set; }

        [DataMember(Name = "countertempid")]
        public int countertempid { get; set; }

        [DataMember(Name = "isabovethreshold")]
        public bool isabovethreshold { get; set; }

        [DataMember(Name = "warning_threshold_value")]
        public decimal warningthresholdvalue { get; set; }

        [DataMember(Name = "critical_threshold_value")]
        public decimal criticalthresholdvalue { get; set; }

        [DataMember(Name = "received_value")]
        public decimal received_value { get { return receivedValue; } set { receivedValue = value; } }

        [DataMember(Name = "guid")]
        public string Guid { get { return guid; } set { guid = value; } }

        [DataMember(Name = "breached_severity")]
        public string Sla_breach_severity { get { return sla_breach_severity; } set { sla_breach_severity = value; } }

        public bool IsBreach { get { return isBreach; } set { isBreach = value; } }
    }

    /// <summary>
    /// Data model to store list of counter info. It is act as DataContract between the SLA agent to Appedo.

    /// </summary>
    [DataContract]
    public class ParentCounterList
    {
        [DataMember(Name = "parentcounter")]
        public List<ParentCounter> ParentCounter { get; set; }
    }

    /// <summary>
    /// Data model to store list of child counter info. It is act as DataContract between the SLA agent to Appedo.
    /// </summary>
    [DataContract]
    public class ParentCounter
    {
        [DataMember(Name = "parentcounterid")]
        public string ParentCounterId { get; set; }

        [DataMember(Name = "childcounterdetail")]
        public List<ChildCounterDetail> ChildCounterDetail { get; set; }
    }

    /// <summary>
    /// Data model to store child counter info. It is act as DataContract between the SLA agent to Appedo.
    /// </summary>
    [DataContract]
    public class ChildCounterDetail
    {
        public bool HasInstace { get; set; }

        [DataMember(Name = "category")]
        public string Category { get; set; }

        public string Name { get; set; }

        [DataMember(Name = "countername")]
        public string CounterName { get { return Name + "-" + InstanceName; } set { } }

        [DataMember(Name = "instancename")]
        public string InstanceName { get; set; }

        [DataMember(Name = "query")]
        public string query
        {
            get
            {
                return new StringBuilder().Append(HasInstace.ToString().ToUpper()).Append(",").Append(Category).Append(",").Append(Name).Append(",").Append(InstanceName).ToString();
            }
            set
            {
                string[] val = value.Split(',');
                Regex regex = new Regex("(.*),(.*),(.*),(.*)");
                Match match = null;
                match = regex.Match(value);
                if (val.Length > 1)
                {
                    HasInstace = Convert.ToBoolean(match.Groups[1].Value);
                    Name = match.Groups[2].Value;
                    InstanceName = match.Groups[3].Value;
                }
                else
                {
                    HasInstace = false;
                    Name = string.Empty;
                    InstanceName = string.Empty;
                }
            }
        }
    }

    [DataContract]
    public class SlowQuery
    {
        [DataMember(Name = "query")]
        public string query { get; set; }
        [DataMember(Name = "calls")]
        public int calls { get; set; }
        [DataMember(Name = "duration_ms")]
        public int duration_ms { get; set; }
        [DataMember(Name = "cached_time")]
        public string cached_time { get; set; }
        [DataMember(Name = "last_execution_time")]
        public string last_execution_time { get; set; }
        [DataMember(Name = "avg_cpu_time_ms")]
        public int avg_cpu_time_ms { get; set; }
        [DataMember(Name = "total_rows")]
        public int total_rows { get; set; }
        [DataMember(Name = "db_name")]
        public string db_name { get; set; }
    }

    [DataContract]
    public class SlowProcedure
    {
        [DataMember(Name = "procedure")]
        public string procedure { get; set; }
        [DataMember(Name = "calls")]
        public int calls { get; set; }
        [DataMember(Name = "duration_ms")]
        public int duration_ms { get; set; }
        [DataMember(Name = "cached_time")]
        public string cached_time { get; set; }
        [DataMember(Name = "last_execution_time")]
        public string last_execution_time { get; set; }
        [DataMember(Name = "avg_cpu_time_ms")]
        public int avg_cpu_time_ms { get; set; }
        [DataMember(Name = "db_name")]
        public string db_name { get; set; }
        [DataMember(Name = "procedure_name")]
        public string procedure_name { get; set; }
    }
    [DataContract]
    public class SlowQueryCollection
    {
        [DataMember(Name = "1001")]
        public string Guid { get; set; }
        [DataMember(Name = "slowQueries")]
        public List<SlowQuery> SlowQueryList { get; set; }
    }

    [DataContract]
    public class CountersDetailNew
    {
        [DataMember(Name = "success")]
        public bool success { get; set; }

        [DataMember(Name = "failure")]
        public bool failure { get; set; }

        [DataMember(Name = "message")]
        public string message { get; set; }

        [DataMember(Name = "WINDOWS")]
        public counterCollec WINDOWS { get; set; }

        [DataMember(Name = "MSSQL")]
        public counterCollec MSSQL { get; set; }

        [DataMember(Name = "MSIIS")]
        public counterCollec MSIIS { get; set; }
    }

    [DataContract]
    public class counterCollec
    {
        [DataMember(Name = "guid")]
        public string guid { get; set; }

        [DataMember(Name = "message")]
        public string message { get; set; }

        [DataMember(Name = "counters")]
        public counterSetNew[] counters { get; set; }

        [DataMember(Name = "SLA")]
        public SLASetNew[] SLA { get; set; }
    }

    [DataContract]
    public class counterSetNew
    {
        [DataMember(Name = "counter_id")]
        public string counter_id { get; set; }

        [DataMember(Name = "query")]
        public string query { get; set; }

        [DataMember(Name = "executiontype")]
        public string executiontype { get; set; }

        [DataMember(Name = "isdelta")]
        public bool isdelta { get; set; }

        [DataMember(Name = "isTopProcess")]
        public bool isTopProcess { get; set; }

        [DataMember(Name = "isStaticCounter")]
        public bool isStaticCounter { get; set; }
    }

    [DataContract]
    public class SLASetNew
    {
        private bool isBreach = false;
        private decimal receivedValue = 0;
        private string guid = string.Empty;
        private string sla_breach_severity = string.Empty;

        [DataMember(Name = "counter_id")]
        public string counter_id { get; set; }

        [DataMember(Name = "process_name")]
        public string process_name { get; set; }

        [DataMember(Name = "sla_id")]
        public string sla_id { get; set; }

        [DataMember(Name = "warning_threshold_value")]
        public int warning_threshold_value { get; set; }

        [DataMember(Name = "critical_threshold_value")]
        public int critical_threshold_value { get; set; }

        [DataMember(Name = "percentage_calculation")]
        public bool percentage_calculation { get; set; }

        [DataMember(Name = "is_above")]
        public bool is_above { get; set; }

        [DataMember(Name = "received_value")]
        public decimal received_value { get { return receivedValue; } set { receivedValue = value; } }

        [DataMember(Name = "breached_severity")]
        public string breached_severity { get { return sla_breach_severity; } set { sla_breach_severity = value; } }

        public bool is_breach { get { return isBreach; } set { isBreach = value; } }
    }

    public class processClass
    {
        [DataMember(Name = "counter_type")]
        public string counter_type { get; set; }

        [DataMember(Name = "process_name")]
        public string process_name { get; set; }

        [DataMember(Name = "counter_value")]
        public decimal counter_value { get; set; }

        [DataMember(Name = "exception")]
        public string exception { get; set; }

        //[DataMember(Name = "process_id")]
        //public string process_id { get; set; }

        //[DataMember(Name = "thread_count")]
        //public int thread_count { get; set; }

        //[DataMember(Name = "handle_count")]
        //public int handle_count { get; set; }
    }
    public class threadClass
    {
        [DataMember(Name = "counter_id")]
        public string counter_id { get; set; }

        [DataMember(Name = "process_id")]
        public int process_id { get; set; }

        [DataMember(Name = "process_name")]
        public string process_name { get; set; }

        [DataMember(Name = "thread_id")]
        public int thread_id { get; set; }

        [DataMember(Name = "thread_state")]
        public string thread_state { get; set; }

        [DataMember(Name = "thread_wait_reason")]
        public string thread_wait_reason { get; set; }

        [DataMember(Name = "total_processor_time")]
        public string total_processor_time { get; set; }
        
        [DataMember(Name = "start_time")]
        public string start_time { get; set; }

        [DataMember(Name = "elapsed_time")]
        public int elapsed_time { get; set; }
    }


    
}
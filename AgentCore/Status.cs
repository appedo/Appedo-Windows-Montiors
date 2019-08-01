using System;
using System.Runtime.Serialization;

namespace AgentCore
{
    /// <summary>
    /// Data model to store system info and status. It is act as DataContract between SLA agent to Appedo.
    /// </summary>
    [DataContract]
    [Serializable]
    class Status
    {
        [DataMember(Name = "mac")]
        public string mac { get; set; }

        [DataMember(Name = "os")]
        public string operating_system { get; set; }

        [DataMember(Name = "osversion")]
        public string os_version { get; set; }

        [DataMember(Name = "status")]
        public string status { get; set; }
    }
}

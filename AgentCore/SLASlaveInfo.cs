using System;
using System.Runtime.Serialization;

namespace AgentCore
{
    /// <summary>
    /// Data model to store SLA Slave info. It is act as DataContract between SLA agent to Appedo.
    /// </summary>
    [DataContract]
    [Serializable]
    class SLASlaveInfo
    {
        [DataMember(Name = "success")]
        public bool success { get; set; }

        [DataMember(Name = "agent_type")]
        public string agent_type { get; set; }

        [DataMember(Name = "slaveuserid")]
        public string slaveuserid { get; set; }

        [DataMember(Name = "mac")]
        public string mac { get; set; }

        [DataMember(Name = "ipaddress")]
        public string ipaddress { get; set; }

        [DataMember(Name = "os_type")]
        public string os_type { get; set; }

        [DataMember(Name = "operating_system")]
        public string operating_system { get; set; }

        [DataMember(Name = "os_version")]
        public string os_version { get; set; }

        [DataMember(Name = "is_active")]
        public bool is_active { get; set; }

        [DataMember(Name = "remarks")]
        public string remarks { get; set; }

        [DataMember(Name = "status")]
        public string status { get; set; }

        [DataMember(Name = "slave_version")]
        public string slave_version { get; set; }

    }
}

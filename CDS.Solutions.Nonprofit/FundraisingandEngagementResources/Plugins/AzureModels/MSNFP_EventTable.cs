using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_EventTable
    {
        [DataMember]
        public Guid eventtableid { get; set; }
        [DataMember]
        public string identifier { get; set; }
        [DataMember]
        public int? tablecapacity { get; set; }
        [DataMember]
        public string tablenumber { get; set; }
        [DataMember]
        public Guid? eventid { get; set; }
        [DataMember]
        public Guid? eventticketid { get; set; }
        [DataMember]
        public DateTime? createdon { get; set; }
        [DataMember]
        public DateTime? syncdate { get; set; }
        [DataMember]
        public bool deleted { get; set; }
        [DataMember]
        public DateTime? deleteddate { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
    }
}
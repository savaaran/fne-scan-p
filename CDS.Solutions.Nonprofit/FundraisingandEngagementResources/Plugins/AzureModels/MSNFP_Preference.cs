using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Preference
    {
        [DataMember]
        public Guid preferenceid { get; set; }
        [DataMember]
        public Guid? preferencecategoryid { get; set; }
        [DataMember]
        public string name { get; set; }
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Appeal
    {
        [DataMember]
        public Guid? msnfp_appealid { get; set; }
        [DataMember]
        public string msnfp_title { get; set; }
        [DataMember]
        public string msnfp_appealname { get; set; }
        [DataMember]
        public string msnfp_appealcode { get; set; }
        [DataMember]
        public Guid? statuscodeid { get; set; }
        [DataMember]
        public int? statuscodeoptionsetid { get; set; }
        [DataMember]
        public string statuscodeidname { get; set; }
        [DataMember]
        public Guid? statecodeid { get; set; }
        [DataMember]
        public int? statecodeoptionsetid { get; set; }
        [DataMember]
        public string statecodeidname { get; set; }
        [DataMember]
        public bool? msnfp_isdeleted { get; set; }
        [DataMember]
        public DateTime? msnfp_isdeleteddate { get; set; }
        [DataMember]
        public DateTime? msnfp_syncdate { get; set; }
    }
}

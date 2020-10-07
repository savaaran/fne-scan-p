using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_BankRunSchedule
    {
        [DataMember]
        public Guid BankRunScheduleId { get; set; }
        [DataMember]
        public Guid? BankRunId { get; set; }
        [DataMember]
        public Guid? PaymentScheduleId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
    }
}

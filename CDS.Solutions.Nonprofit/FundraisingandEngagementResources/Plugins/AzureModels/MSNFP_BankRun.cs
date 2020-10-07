using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_BankRun
    {
        [DataMember]
        public Guid BankRunId { get; set; }
        [DataMember]
        public Guid? PaymentProcessorId { get; set; }
        [DataMember]
        public Guid? AccountToCreditId { get; set; }
        [DataMember]
        public DateTime? StartDate { get; set; }
        [DataMember]
        public DateTime? EndDate { get; set; }
        [DataMember]
        public DateTime? DateToBeProcessed { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public int? BankRunStatus { get; set; }
        [DataMember]
        public int? FileCreationNumber { get; set; }
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

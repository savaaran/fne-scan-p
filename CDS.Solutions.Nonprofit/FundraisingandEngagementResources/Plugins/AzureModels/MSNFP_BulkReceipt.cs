using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_BulkReceipt
    {
        [DataMember]
        public Guid BulkReceiptId { get; set; }
        [DataMember]
        public DateTime? CompletedOn { get; set; }
        [DataMember]
        public DateTime? GenerateReceipt { get; set; }
        [DataMember]
        public DateTime? EmailSend { get; set; }
        [DataMember]
        public DateTime? ReversedOn { get; set; }
        [DataMember]
        public float? ReceiptsGenereated { get; set; }
        [DataMember]
        public float? TransactionsIncluded { get; set; }
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

using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_ReceiptLog
    {
        [DataMember]
        public Guid ReceiptLogId { get; set; }
        [DataMember]
        public string EntryBy { get; set; }
        [DataMember]
        public string EntryReason { get; set; }
        [DataMember]
        public string ReceiptNumber { get; set; }
        [DataMember]
        public Guid? ReceiptStackId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }

        [DataMember]
        public virtual MSNFP_ReceiptStack ReceiptStack { get; set; }
    }
}

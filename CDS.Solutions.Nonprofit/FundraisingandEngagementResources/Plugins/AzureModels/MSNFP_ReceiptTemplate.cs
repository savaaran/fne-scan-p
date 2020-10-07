using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_ReceiptTemplate
    {
        [DataMember]
        public Guid ReceiptTemplateId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public string DisplayName { get; set; }
        [DataMember]
        public string HTMLReceipt { get; set; }
        [DataMember]
        public string HTMLAcknowledgement { get; set; }
        [DataMember]
        public string HeaderImage { get; set; }
        [DataMember]
        public string FooterImage { get; set; }
        [DataMember]
        public string SignatureImage { get; set; }
        [DataMember]
        public int? PreferredLanguage { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public int? TemplateTypeCode { get; set; }
        [DataMember]
        public Guid? OwningBusinessUnitId { get; set; }
    }
}

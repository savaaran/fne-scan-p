using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_EventProduct
    {
        [DataMember]
        public Guid EventProductId { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public decimal? AmountReceipted { get; set; }
        [DataMember]
        public decimal? AmountNonReceiptable { get; set; }
        [DataMember]
        public Guid? EventId { get; set; }
        [DataMember]
        public int? MaxProducts { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int? ValAvailable { get; set; }
        [DataMember]
        public int? Quantity { get; set; }
        [DataMember]
        public bool? RestrictPerRegistration { get; set; }
        [DataMember]
        public int? ValSold { get; set; }
        [DataMember]
        public decimal? AmountTax { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public decimal? SumSold { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
    }
}

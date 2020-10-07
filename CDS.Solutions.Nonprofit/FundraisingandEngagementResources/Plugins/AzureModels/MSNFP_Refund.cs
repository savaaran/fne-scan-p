using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Refund
    {
        [DataMember]
        public Guid RefundId { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public decimal? AmountReceipted { get; set; }
        [DataMember]
        public decimal? AmountMembership { get; set; }
        [DataMember]
        public decimal? RefAmountMembership { get; set; }
        [DataMember]
        public decimal? AmountNonReceiptable { get; set; }
        [DataMember]
        public decimal? RefAmountNonreceiptable { get; set; }
        [DataMember]
        public decimal? RefAmountReceipted { get; set; }
        [DataMember]
        public decimal? AmountTax { get; set; }
        [DataMember]
        public decimal? RefAmountTax { get; set; }
        [DataMember]
        public string ChequeNumber { get; set; }
        [DataMember]
        public Guid? TransactionId { get; set; }
        [DataMember]
        public Guid? PaymentId { get; set; }
        [DataMember]
        public DateTime? BookDate { get; set; }
        [DataMember]
        public DateTime? ReceivedDate { get; set; }
        [DataMember]
        public int? RefundTypeCode { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public decimal? RefAmount { get; set; }
        [DataMember]
        public string TransactionIdentifier { get; set; }
        [DataMember]
        public string TransactionResult { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }

        [DataMember]
        public virtual MSNFP_Transaction Transaction { get; set; }
    }
}

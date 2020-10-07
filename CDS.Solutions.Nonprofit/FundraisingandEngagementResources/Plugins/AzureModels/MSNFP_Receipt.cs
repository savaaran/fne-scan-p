using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Receipt
    {
        [DataMember]
        public Guid ReceiptId { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public decimal? ExpectedTaxCredit { get; set; }
        [DataMember]
        public double? GeneratedorPrinted { get; set; }
        [DataMember]
        public DateTime? LastDonationDate { get; set; }
        [DataMember]
        public decimal? AmountNonReceiptable { get; set; }
        [DataMember]
        public int? TransactionCount { get; set; }
        [DataMember]
        public int? PreferredLanguageCode { get; set; }
        [DataMember]
        public string ReceiptNumber { get; set; }
        [DataMember]
        public int? ReceiptGeneration { get; set; }
        [DataMember]
        public DateTime? ReceiptIssueDate { get; set; }
        [DataMember]
        public Guid? ReceiptStackId { get; set; }
        [DataMember]
        public string ReceiptStatus { get; set; }
        [DataMember]
        public decimal? AmountReceipted { get; set; }
        [DataMember]
        public Guid? PaymentScheduleId { get; set; }
        [DataMember]
        public Guid? ReplacesReceiptId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
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
        public DateTime? Printed { get; set; }
        [DataMember]
        public Guid? BulkReceiptId { get; set; }
        [DataMember]
        public int? DeliveryCode { get; set; }
        [DataMember]
        public int? EmailDeliveryStatusCode { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }

        [DataMember]
        public virtual MSNFP_PaymentSchedule PaymentSchedule { get; set; }
        [DataMember]
        public virtual MSNFP_ReceiptStack ReceiptStack { get; set; }
        [DataMember]
        public virtual MSNFP_Receipt ReplacesReceipt { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Receipt> InverseReplacesReceipt { get; set; }
    }
}

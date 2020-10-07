using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Transaction
    {
        [DataMember]
        public Guid TransactionId { get; set; }
        [DataMember]
        public decimal? AmountReceipted { get; set; }
        [DataMember]
        public decimal? AmountMembership { get; set; }
        [DataMember]
        public decimal? AmountNonReceiptable { get; set; }
        [DataMember]
        public decimal? AmountTax { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public decimal? RefAmountReceipted { get; set; }
        [DataMember]
        public decimal? RefAmountMembership { get; set; }
        [DataMember]
        public decimal? RefAmountNonreceiptable { get; set; }
        [DataMember]
        public decimal? RefAmountTax { get; set; }
        [DataMember]
        public decimal? RefAmount { get; set; }
        [DataMember]
        public decimal? AmountTransfer { get; set; }
        [DataMember]
        public decimal? GaAmountClaimed { get; set; }
        [DataMember]
        public int? Anonymous { get; set; }

        [DataMember]
        public Guid? AppealId { get; set; }
        [DataMember]
        public string Appraiser { get; set; }
        [DataMember]
        public string BillingCity { get; set; }
        [DataMember]
        public string BillingCountry { get; set; }
        [DataMember]
        public string BillingLine1 { get; set; }
        [DataMember]
        public string BillingLine2 { get; set; }
        [DataMember]
        public string BillingLine3 { get; set; }
        [DataMember]
        public string BillingPostalCode { get; set; }
        [DataMember]
        public string BillingStateorProvince { get; set; }
        [DataMember]
        public Guid? OriginatingCampaignId { get; set; }
        [DataMember]
        public Guid? CampaignPageId { get; set; }
        [DataMember]
        public int? CcBrandCode { get; set; }
        [DataMember]
        public bool? ChargeonCreate { get; set; }
        [DataMember]
        public string ChequeNumber { get; set; }
        [DataMember]
        public DateTime? ChequeWireDate { get; set; }

        [DataMember]
        public Guid? ConstituentId { get; set; }
        [DataMember]
        public int? CurrentRetry { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public DateTime? BookDate { get; set; }
        [DataMember]
        public DateTime? DateRefunded { get; set; }
        [DataMember]
        public int? GaDeliveryCode { get; set; }
        [DataMember]
        public DateTime? ReceivedDate { get; set; }

        [DataMember]
        public string Emailaddress1 { get; set; }
        [DataMember]
        public Guid? EventId { get; set; }
        [DataMember]
        public Guid? EventPackageId { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public Guid? GaReturnId { get; set; }
        [DataMember]
        public int? GaApplicableCode { get; set; }
        [DataMember]
        public Guid? GiftBatchId { get; set; }
        [DataMember]
        public string TransactionDescription { get; set; }
        [DataMember]
        public int? DataEntrySource { get; set; }
        [DataMember]
        public int? PaymentTypeCode { get; set; }
        [DataMember]
        public DateTime? LastFailedRetry { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public Guid? MembershipId { get; set; }
        [DataMember]
        public Guid? MembershipInstanceId { get; set; }
        [DataMember]
        public string MobilePhone { get; set; }
        [DataMember]
        public DateTime? NextFailedRetry { get; set; }
        [DataMember]
        public string OrganizationName { get; set; }
        [DataMember]
        public Guid? PackageId { get; set; }
        [DataMember]
        public Guid? TaxReceiptId { get; set; }
        [DataMember]
        public int? ReceiptPreferenceCode { get; set; }
        [DataMember]
        public Guid? DonorCommitmentId { get; set; }
        [DataMember]
        public DateTime? ReturnedDate { get; set; }
        [DataMember]
        public string Telephone1 { get; set; }
        [DataMember]
        public string Telephone2 { get; set; }
        [DataMember]
        public string ThirdPartyReceipt { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public Guid? TransactionBatchId { get; set; }
        [DataMember]
        public string DataEntryReference { get; set; }
        [DataMember]
        public string InvoiceIdentifier { get; set; }
        [DataMember]
        public string TransactionFraudCode { get; set; }
        [DataMember]
        public string TransactionIdentifier { get; set; }
        [DataMember]
        public string TransactionNumber { get; set; }
        [DataMember]
        public string TransactionResult { get; set; }
        [DataMember]
        public string TributeName { get; set; }
        [DataMember]
        public int? TributeCode { get; set; }
        [DataMember]
        public string TributeAcknowledgement { get; set; }
        [DataMember]
        public Guid? TributeId { get; set; }
        [DataMember]
        public string TributeMessage { get; set; }
        [DataMember]
        public DateTime? ValidationDate { get; set; }
        [DataMember]
        public bool? ValidationPerformed { get; set; }
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
        public Guid? ConfigurationId { get; set; }
        [DataMember]
        public Guid? PaymentProcessorId { get; set; }
        [DataMember]
        public Guid? TransactionPaymentScheduleId { get; set; }
        [DataMember]
        public Guid? TransactionPaymentMethodId { get; set; }
        [DataMember]
        public Guid? DonationPageId { get; set; }
        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }
        [DataMember]
        public Guid? BulkReceiptId { get; set; }
        [DataMember]
        public Guid? OwningBusinessUnitId { get; set; }

        [DataMember]
        public DateTime? DepositDate { get; set; }


        [DataMember]
        public virtual MSNFP_Configuration Configuration { get; set; }
        [DataMember]
        public virtual MSNFP_Event Event { get; set; }
        [DataMember]
        public virtual MSNFP_PaymentMethod TransactionPaymentMethod { get; set; } 
        [DataMember]
        public virtual MSNFP_PaymentSchedule TransactionPaymentSchedule { get; set; } 
        [DataMember]
        public virtual ICollection<MSNFP_Refund> Refund { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Response> Response { get; set; }

        [DataMember]
        public bool? EmployerMatches { get; set; }

        [DataMember]
        public int? TypeCode { get; set; }

    }
}

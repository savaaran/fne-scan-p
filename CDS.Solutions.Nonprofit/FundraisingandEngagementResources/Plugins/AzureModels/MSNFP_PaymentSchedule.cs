using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_PaymentSchedule
    {
        [DataMember]
        public Guid PaymentScheduleId { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }


        [DataMember]
        public Guid? DesignationId { get; set; }

        [DataMember]
        public Guid? PaymentMethodId { get; set; }

        [DataMember]
        public Guid? AppealId { get; set; }

        [DataMember]
        public Guid? OriginatingCampaignId { get; set; }

        [DataMember]
        public Guid? CampaignPageId { get; set; }

        [DataMember]
        public Guid? ConfigurationId { get; set; }

        [DataMember]
        public Guid? ConstituentId { get; set; }


        [DataMember]
        public Guid? DonationPageId { get; set; }

        [DataMember]
        public Guid? EventId { get; set; }


        [DataMember]
        public Guid? EventPackageId { get; set; }

        [DataMember]
        public Guid? GiftBatchId { get; set; }

        [DataMember]
        public Guid? MembershipCategoryId { get; set; } 

        [DataMember]
        public Guid? MembershipId { get; set; } // Membership Instance

        [DataMember]
        public Guid? PackageId { get; set; }

        [DataMember]
        public Guid? TaxReceiptId { get; set; }

        [DataMember]
        public Guid? TributeId { get; set; }

        [DataMember]
        public Guid? TransactionBatchId { get; set; }

        [DataMember]
        public Guid? PaymentProcessorId { get; set; }

        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }


                     

        [DataMember]
        public decimal? AmountReceipted { get; set; }

        [DataMember]
        public decimal? AmountMembership { get; set; }

        [DataMember]
        public decimal? AmountNonReceiptable { get; set; }

        [DataMember]
        public decimal? AmountTax { get; set; }

        [DataMember]
        public decimal? RecurringAmount { get; set; }

        [DataMember]
        public DateTime? FirstPaymentDate { get; set; }

        [DataMember]
        public int? FrequencyInterval { get; set; }

        [DataMember]
        public int? FrequencyStartCode { get; set; }

        [DataMember]
        public DateTime? NextPaymentDate { get; set; }

        [DataMember]
        public int? Frequency { get; set; }

        [DataMember]
        public int? CancelationCode { get; set; }

        [DataMember]
        public string CancellationNote { get; set; }

        [DataMember]
        public DateTime? CancelledOn { get; set; }

        [DataMember]
        public DateTime? EndonDate { get; set; }

        [DataMember]
        public DateTime? LastPaymentDate { get; set; }

        [DataMember]
        public int? ScheduleTypeCode { get; set; }

        [DataMember]
        public int? Anonymity { get; set; }

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
        public int? CcBrandCode { get; set; }

        [DataMember]
        public bool? ChargeonCreate { get; set; }



        [DataMember]
        public DateTime? BookDate { get; set; }

        [DataMember]
        public int? GaDeliveryCode { get; set; }

        [DataMember]
        public DateTime? DepositDate { get; set; }

        [DataMember]
        public string EmailAddress1 { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public string TransactionDescription { get; set; }

        [DataMember]
        public int? DataEntrySource { get; set; }

        [DataMember]
        public int? PaymentTypeCode { get; set; }

        [DataMember]
        public string MobilePhone { get; set; }

        [DataMember]
        public string OrganizationName { get; set; }

        [DataMember]
        public int? ReceiptPreferenceCode { get; set; }

        [DataMember]
        public string Telephone1 { get; set; }

        [DataMember]
        public string Telephone2 { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string DataEntryReference { get; set; }

        [DataMember]
        public string InvoiceIdentifier { get; set; }

        [DataMember]
        public string TransactionFraudCode { get; set; }

        [DataMember]
        public string TransactionIdentifier { get; set; }

        [DataMember]
        public string TransactionResult { get; set; }

        [DataMember]
        public int? TributeCode { get; set; }

        [DataMember]
        public string TributeAcknowledgement { get; set; }

        [DataMember]
        public string TributeMessage { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }


        [DataMember]
        public virtual TransactionCurrency TransactionCurrency { get; set; }
        [DataMember]
        public virtual MSNFP_PaymentProcessor PaymentProcessor { get; set; }
        [DataMember]
        public virtual MSNFP_Configuration Configuration { get; set; }
        [DataMember]
        public virtual MSNFP_Event Event { get; set; }
        [DataMember]
        public virtual MSNFP_EventPackage EventPackage { get; set; }
        [DataMember]
        public virtual MSNFP_PaymentMethod PaymentMethod { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Receipt> Receipt { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Response> Response { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Transaction> Transaction { get; set; }


        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }

    }
}

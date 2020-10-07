using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_EventPackage
    {
        [DataMember]
        public Guid EventPackageId { get; set; }
        [DataMember]
        public decimal? AmountReceipted { get; set; }
        [DataMember]
        public decimal? AmountNonReceiptable { get; set; }
        [DataMember]
        public decimal? AmountTax { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public decimal? RefAmountReceipted { get; set; }
        [DataMember]
        public decimal? RefAmountNonreceiptable { get; set; }
        [DataMember]
        public decimal? RefAmountTax { get; set; }
        [DataMember]
        public decimal? RefAmount { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string Emailaddress1 { get; set; }
        [DataMember]
        public string Telephone1 { get; set; }
        [DataMember]
        public string Telephone2 { get; set; }
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
        public Guid? CampaignId { get; set; }
        [DataMember]
        public Guid? PackageId { get; set; }
        [DataMember]
        public Guid? Appealid { get; set; }
        [DataMember]
        public Guid? EventId { get; set; }
        [DataMember]
        public string ChequeNumber { get; set; }
        [DataMember]
        public DateTime? ChequeWireDate { get; set; }
        [DataMember]
        public Guid? ConfigurationId { get; set; }
        [DataMember]
        public Guid? ConstituentId { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public DateTime? Date { get; set; }
        [DataMember]
        public DateTime? DateRefunded { get; set; }
        [DataMember]
        public int? DataEntrySource { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public int? CcBrandCode { get; set; }
        [DataMember]
        public string OrganizationName { get; set; }
        [DataMember]
        public Guid? PaymentmethodId { get; set; }
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
        public string ThirdPartyReceipt { get; set; }
        [DataMember]
        public int? SumDonations { get; set; }
        [DataMember]
        public int? SumProducts { get; set; }
        [DataMember]
        public int? SumSponsorships { get; set; }
        [DataMember]
        public int? SumTickets { get; set; }
        [DataMember]
        public int? SumRegistrations { get; set; }
        [DataMember]
        public decimal? ValDonations { get; set; }
        [DataMember]
        public decimal? ValProducts { get; set; }
        [DataMember]
        public decimal? ValSponsorships { get; set; }
        [DataMember]
        public decimal? ValTickets { get; set; }
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

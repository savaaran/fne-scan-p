using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace Plugins.AzureModels
{
     [DataContract]
    public class MSNFP_Event
    {
        [DataMember]
        public Guid EventId { get; set; }
        [DataMember]
        public decimal? Goal { get; set; }
        [DataMember]
        public int? Capacity { get; set; }
        [DataMember]
        public string Coordinator { get; set; }
        [DataMember]
        public string TimeAndDate { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public string Location { get; set; }
        [DataMember]
        public string Sponsorship { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public int? EventTypeCode { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public string MapLine1 { get; set; }
        [DataMember]
        public string MapLine2 { get; set; }
        [DataMember]
        public string MapLine3 { get; set; }
        [DataMember]
        public string MapCity { get; set; }
        [DataMember]
        public string MapStateOrProvince { get; set; }
        [DataMember]
        public string MapPostalCode { get; set; }
        [DataMember]
        public string MapCountry { get; set; }
        [DataMember]
        public DateTime? ProposedEnd { get; set; }
        [DataMember]
        public DateTime? ProposedStart { get; set; }
        [DataMember]
        public bool? ShowMap { get; set; }
        [DataMember]
        public bool? FreeEvent { get; set; }
        [DataMember]
        public string LargeImage { get; set; }
        [DataMember]
        public string SmallImage { get; set; }
        [DataMember]
        public decimal? CostAmount { get; set; }
        [DataMember]
        public int? CostPercentage { get; set; }
        [DataMember]
        public string ExternalUrl { get; set; }
        [DataMember]
        public bool? ForceRedirect { get; set; }
        [DataMember]
        public int? ForceRedirectTiming { get; set; }
        [DataMember]
        public string HomePageUrl { get; set; }
        [DataMember]
        public string InvoiceMessage { get; set; }
        [DataMember]
        public string LabelLanguageCode { get; set; }
        [DataMember]
        public DateTime? LastPublished { get; set; }
        [DataMember]
        public DateTime? MadeVisible { get; set; }
        [DataMember]
        public string PaymentNotice { get; set; }
        [DataMember]
        public bool? Removed { get; set; }
        [DataMember]
        public bool? SelectCurrency { get; set; }
        [DataMember]
        public bool? SetAcceptNotice { get; set; }
        [DataMember]
        public bool? SetCoverCosts { get; set; }
        [DataMember]
        public bool? SetSignUp { get; set; }
        [DataMember]
        public bool? ShowApple { get; set; }
        [DataMember]
        public bool? ShowCompany { get; set; }
        [DataMember]
        public bool? ShowCoverCosts { get; set; }
        [DataMember]
        public bool? ShowGoogle { get; set; }
        [DataMember]
        public bool? ShowInvoice { get; set; }
        [DataMember]
        public bool? ShowPayPal { get; set; }
        [DataMember]
        public bool? ShowCreditCard { get; set; }
        [DataMember]
        public string ThankYou { get; set; }
        [DataMember]
        public bool? Visible { get; set; }
        [DataMember]
        public bool? ShowGiftAid { get; set; }
        [DataMember]
        public DateTime? RemovedOn { get; set; }
        [DataMember]
        public Guid? PaymentProcessorId { get; set; }
        [DataMember]
        public Guid? CampaignId { get; set; }
        [DataMember]
        public Guid? AppealId { get; set; }
        [DataMember]
        public Guid? PackageId { get; set; }
        [DataMember]     
        public Guid? DesignationId { get; set; }
        [DataMember]
        public Guid? Fundid { get; set; }
        [DataMember]
        public Guid? ConfigurationId { get; set; }
        [DataMember]
        public Guid? VenueId { get; set; }
        [DataMember]
        public Guid? TeamOwnerId { get; set; }
        [DataMember]
        public Guid? TermsOfReferenceId { get; set; }
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

        [DataMember]
        public virtual MSNFP_PaymentProcessor PaymentProcessor { get; set; }
        [DataMember]
        public virtual MSNFP_Configuration Configuration { get; set; }
        [DataMember]
        public virtual MSNFP_TermsofReference TermsOfReference { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_EventDonations> EventDonation { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_EventPackage> EventPackage { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_EventSponsor> EventSponsor { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_PaymentSchedule> PaymentSchedule { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Registration> Registration { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_EventSponsor> SponsorshipNavigation { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Ticket> Ticket { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Transaction> Transaction { get; set; }
    }
}

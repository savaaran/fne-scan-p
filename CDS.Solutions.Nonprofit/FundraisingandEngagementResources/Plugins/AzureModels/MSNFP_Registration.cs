using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Registration
    {
        [DataMember]
        public Guid RegistrationId { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string Email { get; set; }
        [DataMember]
        public string Telephone { get; set; }
        [DataMember]
        public string Address_Line1 { get; set; }
        [DataMember]
        public string Address_Line2 { get; set; }
        [DataMember]
        public string Address_City { get; set; }
        [DataMember]
        public string Address_Province { get; set; }
        [DataMember]
        public string Address_PostalCode { get; set; }
        [DataMember]
        public string Address_Country { get; set; }
        [DataMember]
        public Guid? TableId { get; set; }
        [DataMember]
        public string Team { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public DateTime? Date { get; set; }
        [DataMember]
        public Guid? EventId { get; set; }
        [DataMember]
        public Guid? EventPackageId { get; set; }
        [DataMember]
        public Guid? TicketId { get; set; }
        [DataMember]
        public string GroupNotes { get; set; }
        [DataMember]
        public Guid? EventTicketId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public string msnfp_Firstname { get; set; }
        [DataMember]
        public string msnfp_LastName { get; set; }
        [DataMember]
        public string msnfp_Emailaddress1 { get; set; }
        [DataMember]
        public string msnfp_Telephone1 { get; set; }
        [DataMember]
        public string msnfp_Billing_City { get; set; }
        [DataMember]
        public string msnfp_Billing_Country { get; set; }
        [DataMember]
        public string msnfp_Billing_Line1 { get; set; }
        [DataMember]
        public string msnfp_Billing_Line2 { get; set; }
        [DataMember]
        public string msnfp_Billing_Line3 { get; set; }
        [DataMember]
        public string msnfp_Billing_Postalcode { get; set; }
        [DataMember]
        public string msnfp_Billing_StateorProvince { get; set; }
        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }


        //[DataMember]
        //public Guid msnfp_registrationid { get; set; }
        //[DataMember]
        //public Guid? msnfp_customerid { get; set; }
        //[DataMember]
        //public DateTime? msnfp_date { get; set; }
        //[DataMember]
        //public Guid? msnfp_EventId { get; set; }
        //[DataMember]
        //public Guid? msnfp_EventPackageId { get; set; }
        //[DataMember]
        //public Guid? msnfp_TicketId { get; set; }
        //[DataMember]
        //public string msnfp_GroupNotes { get; set; }
        //[DataMember]
        //public Guid? msnfp_EventTicketId { get; set; }
        //[DataMember]
        //public string msnfp_Identifier { get; set; }
        //[DataMember]
        //public string msnfp_Firstname { get; set; }
        //[DataMember]
        //public string msnfp_LastName { get; set; }
        //[DataMember]
        //public string msnfp_Emailaddress1 { get; set; }
        //[DataMember]
        //public string msnfp_Telephone1 { get; set; }
        //[DataMember]
        //public string msnfp_Billing_City { get; set; }
        //[DataMember]
        //public string msnfp_Billing_Country { get; set; }
        //[DataMember]
        //public string msnfp_Billing_Line1 { get; set; }
        //[DataMember]
        //public string msnfp_Billing_Line2 { get; set; }
        //[DataMember]
        //public string msnfp_Billing_Line3 { get; set; }
        //[DataMember]
        //public string msnfp_Billing_Postalcode { get; set; }
        //[DataMember]
        //public string msnfp_Billing_StateorProvince { get; set; }

        //[DataMember]
        //public DateTime? CreatedOn { get; set; }
        //[DataMember]
        //public DateTime? SyncDate { get; set; }
        //[DataMember]
        //public bool Deleted { get; set; }
        //[DataMember]
        //public DateTime? DeletedDate { get; set; }
        //[DataMember]
        //public Guid? TransactionCurrencyId { get; set; }


        //[DataMember]
        // public string msnfp_title { get; set; }
        // [DataMember]
        // public Guid? msnfp_contactid { get; set; }
        // [DataMember]
        // public string msnfp_contactidname { get; set; }
        // [DataMember]
        // public Guid? msnfp_fundid { get; set; }
        // [DataMember]
        // public string msnfp_fundidname { get; set; }
        // [DataMember]
        // public Guid? msnfp_appealid { get; set; }
        // [DataMember]
        // public string msnfp_appealidname { get; set; }
        // [DataMember]
        // public Guid? msnfp_packageid { get; set; }
        // [DataMember]
        // public string msnfp_packageidname { get; set; }
        // [DataMember]
        // public Guid? msnfp_eventid { get; set; }
        // [DataMember]
        // public string msnfp_eventidname { get; set; }
        // [DataMember]
        // public Guid? msnfp_currencyid { get; set; }
        // [DataMember]
        // public string msnfp_currencyidname { get; set; }
        // [DataMember]
        // public decimal msnfp_advantage { get; set; }
        // [DataMember]
        // public decimal msnfp_amount { get; set; }
        // [DataMember]
        // public DateTime? msnfp_date { get; set; }
        // [DataMember]
        // public DateTime? msnfp_daterefunded { get; set; }
        // [DataMember]
        // public Guid? msnfp_eventpackageid { get; set; }
        // [DataMember]
        // public string msnfp_eventpackageidname { get; set; }
        // [DataMember]
        // public string msnfp_identifier { get; set; }
        // [DataMember]
        // public int msnfp_registrations { get; set; }
        // [DataMember]
        // public Guid? msnfp_ticketid { get; set; }
        // [DataMember]
        // public string msnfp_ticketidname { get; set; }
        // [DataMember]
        // public decimal msnfp_ticketprice { get; set; }
        // [DataMember]
        // public decimal msnfp_totalrefunded { get; set; }
        // [DataMember]
        // public Guid? msnfp_campaignid { get; set; }
        // [DataMember]
        // public string msnfp_campaignidname { get; set; }
        // [DataMember]
        // public Guid? statuscodeid { get; set; }
        // [DataMember]
        // public int statuscodeoptionsetid { get; set; }
        // [DataMember]
        // public string statuscodeidname { get; set; }
        // [DataMember]
        // public Guid? statecodeid { get; set; }
        // [DataMember]
        // public int statecodeoptionsetid { get; set; }
        // [DataMember]
        // public string statecodeidname { get; set; }
        // [DataMember]
        // public bool msnfp_isdeleted { get; set; }
        // [DataMember]
        // public DateTime? msnfp_isdeleteddate { get; set; }
        // [DataMember]
        // public DateTime? msnfp_syncdate { get; set; }
        //[DataMember]
        //public bool msnfp_customfund { get; set; }
        //[DataMember]
        //public string msnfp_isocurrencycode { get; set; }
        //[DataMember]
        //public Guid? ownerid { get; set; }
        //[DataMember]c
        //public int? msnfp_cctypeoptionsetid { get; set; }
        //[DataMember]
        //public int? msnfp_gifttypeoptionsetid { get; set; }
    }
}

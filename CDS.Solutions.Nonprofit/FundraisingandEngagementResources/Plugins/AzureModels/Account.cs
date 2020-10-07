using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class Account
    {
        [DataMember]
        public Guid AccountId { get; set; }
        [DataMember]
        public Guid? Address1_AddressId { get; set; }
        [DataMember]
        public int? Address1_AddressTypeCode { get; set; }
        [DataMember]
        public string Address1_City { get; set; }
        [DataMember]
        public string Address1_Country { get; set; }
        [DataMember]
        public string Address1_County { get; set; }
        [DataMember]
        public float? Address1_Latitude { get; set; }
        [DataMember]
        public string Address1_Line1 { get; set; }
        [DataMember]
        public string Address1_Line2 { get; set; }
        [DataMember]
        public string Address1_Line3 { get; set; }
        [DataMember]
        public float? Address1_Longitude { get; set; }
        [DataMember]
        public string Address1_Name { get; set; }
        [DataMember]
        public string Address1_PostalCode { get; set; }
        [DataMember]
        public string Address1_PostOfficeBox { get; set; }
        [DataMember]
        public string Address1_StateOrProvince { get; set; }
        [DataMember]
        public Guid? Address2_AddressId { get; set; }
        [DataMember]
        public int? Address2_AddressTypeCode { get; set; }
        [DataMember]
        public string Address2_City { get; set; }
        [DataMember]
        public string Address2_Country { get; set; }
        [DataMember]
        public string Address2_County { get; set; }
        [DataMember]
        public float? Address2_Latitude { get; set; }
        [DataMember]
        public string Address2_Line1 { get; set; }
        [DataMember]
        public string Address2_Line2 { get; set; }
        [DataMember]
        public string Address2_Line3 { get; set; }
        [DataMember]
        public float? Address2_Longitude { get; set; }
        [DataMember]
        public string Address2_Name { get; set; }
        [DataMember]
        public string Address2_PostalCode { get; set; }
        [DataMember]
        public string Address2_PostOfficeBox { get; set; }
        [DataMember]
        public string Address2_StateOrProvince { get; set; }
        [DataMember]
        public bool? DoNotBulkEMail { get; set; }
        [DataMember]
        public bool? DoNotBulkPostalMail { get; set; }
        [DataMember]
        public bool? DoNotEmail { get; set; }
        [DataMember]
        public bool? DoNotFax { get; set; }
        [DataMember]
        public bool? DoNotPhone { get; set; }
        [DataMember]
        public bool? DoNotPostalMail { get; set; }
        [DataMember]
        public bool? DoNotSendMM { get; set; }
        [DataMember]
        public string EmailAddress1 { get; set; }
        [DataMember]
        public string EmailAddress2 { get; set; }
        [DataMember]
        public string EmailAddress3 { get; set; }
        [DataMember]
        public Guid? MasterId { get; set; }
        [DataMember]
        public Guid? OwningBusinessUnitId { get; set; }
        [DataMember]
        public int? msnfp_Anonymity { get; set; }
        [DataMember]
        public int? msnfp_Count_LifetimeTransactions { get; set; }
        [DataMember]
        public Guid? msnfp_GivingLevelId { get; set; }
        [DataMember]
        public DateTime? msnfp_LastEventPackageDate { get; set; }
        [DataMember]
        public Guid? msnfp_LastEventPackageId { get; set; }
        [DataMember]
        public DateTime? msnfp_LastTransactionDate { get; set; }
        [DataMember]
        public Guid? msnfp_LastTransactionId { get; set; }
        [DataMember]
        public int? msnfp_PreferredLanguageCode { get; set; }
        [DataMember]
        public Guid? msnfp_PrimaryMembershipId { get; set; }
        [DataMember]
        public int? msnfp_ReceiptPreferenceCode { get; set; }
        [DataMember]
        public decimal? msnfp_Sum_LifetimeTransactions { get; set; }
        [DataMember]
        public int? msnfp_Telephone1TypeCode { get; set; }
        [DataMember]
        public int? msnfp_Telephone2TypeCode { get; set; }
        [DataMember]
        public int? msnfp_Telephone3TypeCode { get; set; }
        [DataMember]
        public bool? msnfp_Vip { get; set; }
        [DataMember]
        public bool? Merged { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public Guid? ParentAccountId { get; set; }
        [DataMember]
        public string Telephone1 { get; set; }
        [DataMember]
        public string Telephone2 { get; set; }
        [DataMember]
        public string Telephone3 { get; set; }
        [DataMember]
        public string WebSiteURL { get; set; }
        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }
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
        public int? msnfp_accounttype { get; set; }
        [DataMember]
        public int? msnfp_year0_category { get; set; }
        [DataMember]
        public decimal? msnfp_year0_giving { get; set; }
        [DataMember]
        public int? msnfp_year1_category { get; set; }
        [DataMember]
        public decimal? msnfp_year1_giving { get; set; }
        [DataMember]
        public int? msnfp_year2_category { get; set; }
        [DataMember]
        public decimal? msnfp_year2_giving { get; set; }
        [DataMember]
        public int? msnfp_year3_category { get; set; }
        [DataMember]
        public decimal? msnfp_year3_giving { get; set; }
        [DataMember]
        public int? msnfp_year4_category { get; set; }
        [DataMember]
        public decimal? msnfp_year4_giving { get; set; }
        [DataMember]
        public decimal? msnfp_lifetimegivingsum { get; set; }

    }
}

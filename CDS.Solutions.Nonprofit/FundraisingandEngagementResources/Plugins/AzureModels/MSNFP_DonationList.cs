using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_DonationList
    {
        [DataMember]
        public Guid DonationListId { get; set; }
        //[DataMember]
        //public Guid? CreatedBy { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        //[DataMember]
        //public int? ImportSequenceNumber { get; set; }
        [DataMember]
        public string DonationListName { get; set; }
        [DataMember]
        public string ExternalURL { get; set; }
        [DataMember]
        public DateTime? LastPublished { get; set; }
        [DataMember]
        public DateTime? MadeVisible { get; set; }
        [DataMember]
        public int? MaxHeight { get; set; }
        [DataMember]
        public int? MaxWidth{ get; set; }
        [DataMember]
        public string Message { get; set; }
        [DataMember]
        public Guid? ParentListId { get; set; }
        [DataMember]
        public bool? Removed { get; set; }
        [DataMember]
        public DateTime? RemovedOn { get; set; }
        [DataMember]
        public string SmallImage { get; set; }
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public bool? Visible { get; set; }

        [DataMember] public Guid? ConfigurationId { get; set; } 

        //[DataMember]
        //public Guid? ModifiedBy { get; set; }
        //[DataMember]
        //public DateTime? ModifiedOn { get; set; }
        //[DataMember]
        //public DateTime? RecordCreatedOn { get; set; }
        //[DataMember]
        //public Guid? Owner{ get; set; }
        //[DataMember]
        //public Guid? OwningBusinessUnit { get; set; }
        //[DataMember]
        //public Guid? OwningTeam { get; set; }
        //[DataMember]
        //public Guid? OwningUser { get; set; }
        [DataMember]
        public int? Status { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
        //[DataMember]
        //public int? TimeZoneRuleVersionNumber{ get; set; }
        //[DataMember]
        //public int? UTCConversionTimeZoneCode { get; set; }
        //[DataMember]
        //public Int64? VersionNumber { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }

        public virtual MSNFP_DonationList ParentList { get; set; }

        public virtual ICollection<MSNFP_DonationList> ParentLists { get; set; }
        public virtual ICollection<MSNFP_PageOrder> FromDonationList_PageOrders { get; set; }
        public virtual ICollection<MSNFP_PageOrder> ToDonationList_PageOrders { get; set; }

        //[DataMember]
        //public virtual msnfp_Configuration Configuration { get; set; }
        //[DataMember]
        //public virtual msnfp_TermsofReference TermsOfReference { get; set; }
        //[DataMember]
        //public virtual ICollection<msnfp_CampaignPageAction> CampaignPageAction { get; set; }
        //[DataMember]
        //public virtual ICollection<MSNFP_PaymentSchedule> PaymentSchedule { get; set; }
        //[DataMember]
        //public virtual ICollection<msnfp_RelatedImage> RelatedImage { get; set; }
    }
}

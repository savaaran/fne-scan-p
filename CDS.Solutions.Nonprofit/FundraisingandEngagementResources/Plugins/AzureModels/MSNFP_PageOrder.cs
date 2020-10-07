using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_PageOrder
    {
        [DataMember]
        public Guid PageOrderId { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public Guid? FromDonationListId { get; set; }
        [DataMember]
        public Guid? FromDonationPageId { get; set; }
        [DataMember]
        public int? Order { get; set; }
        [DataMember]
        public DateTime? OrderDate { get; set; }
        [DataMember]
        public int? Status { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public Guid? ToDonationListId { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }

        [DataMember]
        public virtual MSNFP_DonationList FromDonationList { get; set; }
        [DataMember]
        public virtual MSNFP_DonationList ToDonationList { get; set; }

    }
}

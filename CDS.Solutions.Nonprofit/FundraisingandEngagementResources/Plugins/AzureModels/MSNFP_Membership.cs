using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Membership
    {
        [DataMember]
        public Guid MembershipId { get; set; }
        [DataMember]
        public Guid? Customer { get; set; }
        [DataMember]
        public DateTime? StartDate { get; set; }
        [DataMember]
        public DateTime? EndDate { get; set; }
        [DataMember]
        public Guid? MembershipCategoryId { get; set; }
        [DataMember]
        public bool? Primary { get; set; }
        [DataMember]
        public string Name { get; set; }
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
        public virtual MSNFP_MembershipCategory MembershipCategory { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_MembershipOrder> MembershipOrder { get; set; }
    }
}

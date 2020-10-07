using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_MembershipOrder
    {
        [DataMember]
        public Guid MembershipOrderId { get; set; }
        [DataMember]
        public Guid? FromMembershipCategoryId { get; set; }
        [DataMember]
        public int? Order { get; set; }
        [DataMember]
        public DateTime? OrderDate { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public Guid? ToMembershipGroupId { get; set; }
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
        public virtual MSNFP_MembershipCategory FromMembershipCategory { get; set; }
        [DataMember]
        public virtual MSNFP_MembershipGroup ToMembershipGroup { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_MembershipCategory
    {
        [DataMember]
        public Guid MembershipCategoryId { get; set; }
        [DataMember]
        public decimal? AmountMembership { get; set; }
        [DataMember]
        public decimal? AmountTax { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public DateTime? GoodWillDate { get; set; }
        [DataMember]
        public int? MembershipDuration { get; set; }
        [DataMember]
        public DateTime? RenewalDate { get; set; }
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
        public Guid? TransactionCurrencyId { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }

        [DataMember]
        public virtual ICollection<MSNFP_Membership> Membership { get; set; }
    }
}

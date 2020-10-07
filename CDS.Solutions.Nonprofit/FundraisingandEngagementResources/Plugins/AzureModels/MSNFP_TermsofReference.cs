using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_TermsofReference
    {
        [DataMember]
        public Guid TermsOfReferenceId { get; set; }
        [DataMember]
        public string CcvMessage { get; set; }
        [DataMember]
        public string CoverCostsMessage { get; set; }
        [DataMember]
        public string FailureMessage { get; set; }
        [DataMember]
        public string Footer { get; set; }
        [DataMember]
        public string GiftAidAcceptence { get; set; }
        [DataMember]
        public string GiftAidDeclaration { get; set; }
        [DataMember]
        public string GiftAidDetails { get; set; }
        [DataMember]
        public string PrivacyPolicy { get; set; }
        [DataMember]
        public string PrivacyUrl { get; set; }
        [DataMember]
        public bool? ShowPrivacy { get; set; }
        [DataMember]
        public bool? ShowTermsConditions { get; set; }
        [DataMember]
        public string Signup { get; set; }
        [DataMember]
        public string TermsConditions { get; set; }
        [DataMember]
        public string TermsConditionsUrl { get; set; }
        [DataMember]
        public string Identifier { get; set; }
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
        public virtual ICollection<MSNFP_Event> Event { get; set; }
    }
}

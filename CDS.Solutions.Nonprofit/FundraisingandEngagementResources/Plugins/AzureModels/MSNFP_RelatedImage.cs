using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_RelatedImage
    {
        [DataMember]
        public Guid RelatedImageId { get; set; }
        [DataMember]
        public Guid? CampaignPageId { get; set; }
        [DataMember] 
        public Guid? ImageId { get; set; }
        [DataMember]
        public string SmallImage { get; set; }
        [DataMember]
        public DateTime? LastPublished { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
    }
}

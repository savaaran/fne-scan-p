using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_GiftAidDeclaration
    {
        [DataMember]
        public Guid GiftAidDeclarationId { get; set; }

        [DataMember]
        public Guid? CustomerId { get; set; }

        [DataMember]
        public int? CustomerIdType { get; set; }

        [DataMember]
        public DateTime? DeclarationDate { get; set; }

        [DataMember]
        public int? DeclarationDelivered { get; set; }

        [DataMember]
        public string GiftAidDeclarationHtml { get; set; }

        [DataMember]
        public string Identifier { get; set; }

        [DataMember]
        public DateTime? Updated { get; set; }

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
    }
}

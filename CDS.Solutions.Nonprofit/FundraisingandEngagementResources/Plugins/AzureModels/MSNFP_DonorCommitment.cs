using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_DonorCommitment
    {
        [DataMember]
        public Guid? DonorCommitmentId { get; set; }
        [DataMember]
        public decimal? TotalAmount { get; set; }
        [DataMember]
        public Guid? AppealId { get; set; }
        [DataMember]
        public Guid? PackageId { get; set; }
        [DataMember]
        public Guid? CampaignId { get; set; }

        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }

        [DataMember]
        public DateTime? BookDate { get; set; }
        [DataMember]
        public decimal? TotalAmountBalance { get; set; }

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
    }
}

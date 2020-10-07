using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_EventSponsor
    {
        [DataMember]
        public Guid EventSponsorId { get; set; }

        [DataMember]
        public Guid? EventId { get; set; }

        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }

        [DataMember]
        public string LargeImage { get; set; }

        [DataMember]
        public int? Order { get; set; }

        [DataMember]
        public DateTime? OrderDate { get; set; }

        [DataMember]
        public string SponsorTitle { get; set; }

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


        
    }
}

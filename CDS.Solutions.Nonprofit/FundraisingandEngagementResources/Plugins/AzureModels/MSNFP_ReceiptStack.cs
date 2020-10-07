using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_ReceiptStack
    {
        [DataMember]
        public Guid ReceiptStackId { get; set; }
        [DataMember]
        public Guid? ConfigurationId { get; set; }
        [DataMember]
        public double? CurrentRange { get; set; }
        [DataMember]
        public int? NumberRange { get; set; }
        [DataMember]
        public string Prefix { get; set; }
        [DataMember]
        public int? ReceiptYear { get; set; }
        [DataMember]
        public double? StartingRange { get; set; }
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
        public virtual MSNFP_Configuration Configuration { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Receipt> Receipt { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_ReceiptLog> ReceiptLog { get; set; }
    }
}

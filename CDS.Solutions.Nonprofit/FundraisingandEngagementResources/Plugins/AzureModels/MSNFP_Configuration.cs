using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Configuration
    {
        [DataMember]
        public Guid ConfigurationId { get; set; }
        [DataMember]
        public string AddressAuth1 { get; set; }
        [DataMember]
        public string AddressAuth2 { get; set; }
        [DataMember]
        public string AzureWebApiUrl { get; set; }
        [DataMember]
        public int? BankRunPregeneratedBy { get; set; }
        [DataMember]
        public string CharityTitle { get; set; }
        [DataMember]
        public string AzureWebApp { get; set; }
        [DataMember]
        public int? ScheMaxRetries { get; set; }
        [DataMember]
        public int? ScheRecurrenceStart { get; set; }
        [DataMember]
        public int? ScheRetryinterval { get; set; }
        [DataMember]
        public Guid? TeamOwnerId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public Guid? PaymentProcessorId { get; set; }
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
        public bool? DefaultConfiguration { get; set; }

        [DataMember]
        public virtual MSNFP_PaymentProcessor PaymentProcessor { get; set; } 
        [DataMember]
        public virtual ICollection<MSNFP_Event> Event { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_EventPackage> EventPackage { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_PaymentSchedule> PaymentSchedule { get; set; } 
        [DataMember]
        public virtual ICollection<MSNFP_ReceiptStack> ReceiptStack { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Transaction> Transaction { get; set; }





    }
}

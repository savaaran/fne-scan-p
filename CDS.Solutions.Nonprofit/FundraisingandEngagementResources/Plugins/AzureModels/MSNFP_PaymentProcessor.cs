using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_PaymentProcessor
    {
        [DataMember]
        public Guid PaymentProcessorId { get; set; }
        [DataMember]
        public string Name { get; set; }
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
        public int PaymentGatewayType { get; set; }


        [DataMember]
        public string IsoCurrencyCode { get; set; }
        [DataMember]
        public string AuthToken { get; set; }

        [DataMember]
        public string AdyenMerchantAccount { get; set; }
        [DataMember]
        public string AdyenUsername { get; set; }
        [DataMember]
        public string AdyenPassword { get; set; }
        [DataMember]
        public string AdyenUrl { get; set; }
        [DataMember]
        public string AdyenCheckoutUrl { get; set; }

        [DataMember]
        public string IatsAgentCode { get; set; }
        [DataMember]
        public string IatsPassword { get; set; }
        [DataMember]
        public string IatsIpAddress { get; set; }

        [DataMember]
        public string MonerisStoreId { get; set; }
        [DataMember]
        public string MonerisApiKey { get; set; }
        [DataMember]
        public bool? MonerisTestMode { get; set; }

        [DataMember]
        public string StripeServiceKey { get; set; }
        [DataMember]
        public string StripeCustomerId { get; set; }
        [DataMember]
        public string StripeCardId { get; set; }

        [DataMember]
        public string WorldPayServiceKey { get; set; }

        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }

        [DataMember]
        public int? BankRunFileFormat { get; set; }
        [DataMember]
        public string ScotiabankCustomerNumber { get; set; }
        [DataMember]
        public string OriginatorShortName { get; set; }
        [DataMember]
        public string OriginatorLongName { get; set; }
        [DataMember]
        public string BmoOriginatorId { get; set; }

        [DataMember]
        public string AbaRemitterName { get; set; }
        [DataMember]
        public string AbaUserName { get; set; }
        [DataMember]
        public string AbaUserNumber { get; set; }

        [DataMember]
        public virtual ICollection<MSNFP_Configuration> Configuration { get; set; } 
        [DataMember]
        public virtual ICollection<MSNFP_PaymentMethod> PaymentMethod { get; set; } 
    }
}

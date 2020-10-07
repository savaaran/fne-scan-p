using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class TransactionCurrency
    {
        [DataMember]
        public Guid TransactionCurrencyId { get; set; }

        [DataMember]
        public string CurrencyName { get; set; }

        [DataMember]
        public string CurrencySymbol { get; set; }

        [DataMember]
        public string IsoCurrencyCode { get; set; }
        
        [DataMember]
        public int? StatusCode { get; set; }

        [DataMember]
        public int? StateCode { get; set; }

        [DataMember]
        public bool? Deleted { get; set; }

        [DataMember]
        public DateTime? DeletedDate { get; set; }

        [DataMember]
        public DateTime? SyncDate { get; set; }

        [DataMember]
        public decimal? ExchangeRate { get; set; }

        [DataMember]
        public bool? IsBase { get; set; }

        [DataMember]
        public DateTime? CreatedOn { get; set; }
    }
}

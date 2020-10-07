using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Payment
    {
        [DataMember]
        public Guid paymentid { get; set; }

        [DataMember]
        public Guid? paymentprocessorid { get; set; }

        [DataMember]
        public Guid? paymentmethodid { get; set; }

        [DataMember]
        public Guid? eventpackageid { get; set; }

        [DataMember]
        public Guid? customerid { get; set; }

        [DataMember]
        public decimal? amount { get; set; }

        [DataMember]
        public decimal? AmountRefunded { get; set; }

        [DataMember]
        public string transactionfraudcode { get; set; }

        [DataMember]
        public string transactionidentifier { get; set; }

        [DataMember]
        public string transactionresult { get; set; }

        [DataMember]
        public string chequenumber { get; set; }

        [DataMember]
        public string name { get; set; }

        [DataMember]
        public int? paymenttype { get; set; }

        [DataMember]
        public int? ccbrandcodepayment { get; set; }

        [DataMember]
        public string invoiceidentifier { get; set; }

        [DataMember]
        public Guid? responseid { get; set; }

        [DataMember]
        public decimal? AmountBalance { get; set; }

        [DataMember]
        public Guid? configurationid { get; set; }

        [DataMember]
        public DateTime? daterefunded { get; set; }

        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? statuscode { get; set; }

        [DataMember]
        public DateTime? createdon { get; set; }
        [DataMember]
        public DateTime? syncdate { get; set; }
        [DataMember]
        public bool deleted { get; set; }
        [DataMember]
        public DateTime? deleteddate { get; set; }

    }
}

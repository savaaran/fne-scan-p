using System;
using System.Runtime.Serialization;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_Ticket
    {
        [DataMember]
        public Guid TicketId { get; set; }
        [DataMember]
        public decimal? AmountReceipted { get; set; }
        [DataMember]
        public decimal? AmountNonreceiptable { get; set; }
        [DataMember]
        public decimal? AmountTax { get; set; }
        [DataMember]
        public decimal? Amount { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public string GroupNotes { get; set; }
        [DataMember]
        public DateTime? Date { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public Guid? EventId { get; set; }
        [DataMember]
        public Guid? EventPackageId { get; set; }
        [DataMember]
        public int? RegistrationsPerTicket { get; set; }
        [DataMember]
        public Guid? EventTicketId { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public Guid? TransactionCurrencyId { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
    }
}

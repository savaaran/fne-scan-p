using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.AzureModels
{
    [DataContract]
    public class MSNFP_PaymentMethod
    {
        [DataMember]
        public Guid PaymentMethodId { get; set; }
        [DataMember]
        public string AuthToken { get; set; }
        [DataMember]
        public int? CcBrandCode { get; set; }
        [DataMember]
        public DateTime? CcExpDate { get; set; }
        [DataMember]
        public string BillingCity { get; set; }
        [DataMember]
        public string BillingCountry { get; set; }
        [DataMember]
        public string CcLast4 { get; set; }
        [DataMember]
        public string CcExpMmYy { get; set; }
        [DataMember]
        public Guid? CustomerId { get; set; }
        [DataMember]
        public int? CustomerIdType { get; set; }
        [DataMember]
        public string Emailaddress1 { get; set; }
        [DataMember]
        public string BankName { get; set; }
        [DataMember]
        public string BankActNumber { get; set; }
        [DataMember]
        public string BankActRtNumber { get; set; }
        [DataMember]
        public int? BankTypeCode { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string Identifier { get; set; }
        [DataMember]
        public bool? IsReusable { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string NameOnFile { get; set; }
        [DataMember]
        public Guid? PaymentProcessorId { get; set; }
        [DataMember]
        public string Telephone1 { get; set; }
        [DataMember]
        public string BillingState { get; set; }
        [DataMember]
        public string BillingLine1 { get; set; }
        [DataMember]
        public string BillingLine2 { get; set; }
        [DataMember]
        public string BillingLine3 { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string BillingPostalCode { get; set; }
        [DataMember]
        public DateTime? SyncDate { get; set; }
        [DataMember]
        public bool? Deleted { get; set; }
        [DataMember]
        public DateTime? DeletedDate { get; set; }
        [DataMember]
        public DateTime? CreatedOn { get; set; }
        [DataMember]
        public string StripeCustomerId { get; set; }
        [DataMember]
        public string AbaFinancialInstitutionName { get; set; }
        [DataMember]
        public int? StateCode { get; set; }
        [DataMember]
        public int? StatusCode { get; set; }
        [DataMember]
        public int? Type { get; set; }
        [DataMember]
        public string NameAsItAppearsOnTheAccount { get; set; }


        [DataMember]
        public virtual MSNFP_PaymentProcessor PaymentProcessor { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_EventPackage> EventPackage { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_PaymentSchedule> PaymentSchedule { get; set; }
        [DataMember]
        public virtual ICollection<MSNFP_Transaction> Transaction { get; set; }
    }
}

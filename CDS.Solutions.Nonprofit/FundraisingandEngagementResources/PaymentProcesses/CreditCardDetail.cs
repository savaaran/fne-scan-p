/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;

namespace Plugins.PaymentProcesses
{
    public class CreditCardDetail
    {
        public string CCNumber { get; set; }
        public string ExpDate { get; set; }
        public string CVV { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Note { get; set; }
        public string CryptType { get; set; }
        public string CustomerId { get; set; }
        public string ProcessingCountryCode { get; set; }
        public bool StatusCheck { get; set; }
        public string Identifier { get; set; }
        public string InvoiceNumber {get;set;}
        public string CVV2 { get; set; }
        public string DataKey { get; set; }
        public string TransactionID { get; set; }
        public string Amount { get; set; }
        public string TxnNumber { get; set; }
        public string receiptID { get; set; }

        //useful for iATS process
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool Recurring { get; set; }
        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
    }
}

using Newtonsoft.Json;
using FundraisingandEngagement.StripeWebPayment.Model;

namespace FundraisingandEngagement.StripeWebPayment.Service
{
    public class StripeTokenCreateOptions
    {
        [JsonProperty("customer")]
        public string CustomerId { get; set; }

        [JsonProperty("card")]
        public StripeCreditCardOptions Card { get; set; }

        [JsonProperty("bank_account")]
        public BankAccountOptions BankAccount { get; set; }
    }
}
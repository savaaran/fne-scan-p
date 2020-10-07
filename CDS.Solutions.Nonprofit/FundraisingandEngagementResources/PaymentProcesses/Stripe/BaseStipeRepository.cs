using FundraisingandEngagement.StripeIntegration.Model;
using FundraisingandEngagement.StripeWebPayment.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace FundraisingandEngagement.StripeIntegration.Helpers
{
    public class BaseStipeRepository
    {
        public T Get<T>(string url, string apiKey)
        {
            using (HttpClient httpClient = new HttpClient())
                return this.ResponseHelper<T>(httpClient.SendAsync(this.GetRequestMessage(url, HttpMethod.Get, apiKey, (string)null)).Result);
        }

        public T Create<T>(T stripeObject, string url, string apiKey) where T : StripeEntityWithId
        {
            Type type = typeof(T);
            string contentstring = string.Empty;
            if (type.Equals(typeof(StripeCustomer)))
                contentstring = this.GetStripeCustomerString((object)stripeObject as StripeCustomer);
            if (type.Equals(typeof(StripeCharge)))
                contentstring = this.GetStripeChargeString((object)stripeObject as StripeCharge);
            if (type.Equals(typeof(StripeCard)))
                contentstring = this.GetStripeChargeString((object)stripeObject as StripeCard);
            using (HttpClient httpClient = new HttpClient())
            {
                //https://docs.microsoft.com/en-us/archive/blogs/jpsanders/asp-net-do-not-use-task-result-in-main-context
                var task = httpClient.SendAsync(this.GetRequestMessage(url, HttpMethod.Post, apiKey, contentstring));
                task.Wait();
                return this.ResponseHelper<T>(task.Result);
            }
        }

        private string GetStripeCustomerString(StripeCustomer customer)
        {
            return string.Format("description={0}&email={1}", customer.Description != null ? (object)customer.Description.Replace(' ', '+') : (object)string.Empty, (object)customer.Email);
        }

        private string GetStripeChargeString(StripeCharge charge)
        {
            return string.Format("description={0}&currency={1}&amount={2}&customer={3}&source={4}", (object)(charge.Description != null ? charge.Description.Replace(' ', '+') : string.Empty), (object)charge.Currency, (object)charge.Amount, (object)charge.Customer.Id, (object)charge.Source.Id);
        }

        private string GetStripeChargeString(StripeCard card)
        {
            return string.Format("source={0}", (object)card.SourceToken);
        }

        private T ResponseHelper<T>(HttpResponseMessage response)
        {
            string result = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(JObject.Parse(result)["error"][(object)"message"].ToString());
            return Mapper<T>.MapFromJson(this.BuildResponseData(response, result), (string)null);
        }

        private HttpRequestMessage GetRequestMessage(
          string url,
          HttpMethod method,
          string apiKey,
          string contentstring = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(method, new Uri(url));
            httpRequestMessage.Headers.Add("Authorization", string.Format("Bearer {0}", (object)apiKey));
            if (method == HttpMethod.Post)
                httpRequestMessage.Content = (HttpContent)new StringContent(contentstring, Encoding.UTF8, "application/x-www-form-urlencoded");
            return httpRequestMessage;
        }

        private StripeResponse BuildResponseData(
          HttpResponseMessage response,
          string responseText)
        {
            return new StripeResponse()
            {
                RequestId = response.Headers.Contains("Request-Id") ? response.Headers.GetValues("Request-Id").First<string>() : "n/a",
                RequestDate = Convert.ToDateTime(response.Headers.GetValues("Date").First<string>(), (IFormatProvider)CultureInfo.InvariantCulture),
                ResponseJson = responseText
            };
        }
    }
}

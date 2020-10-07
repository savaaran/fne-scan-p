using FundraisingandEngagement.StripeWebPayment.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FundraisingandEngagement.StripeIntegration.Model
{
    public static class Mapper<T>
    {
        public static List<T> MapCollectionFromJson(
          string json,
          string token = "data",
          StripeResponse stripeResponse = null)
        {
            return JObject.Parse(json).SelectToken(token).Select<JToken, T>((Func<JToken, T>)(tkn => Mapper<T>.MapFromJson(tkn.ToString(), (string)null, stripeResponse))).ToList<T>();
        }

        public static List<T> MapCollectionFromJson(StripeResponse stripeResponse, string token = "data")
        {
            return Mapper<T>.MapCollectionFromJson(stripeResponse.ResponseJson, token, stripeResponse);
        }

        public static T MapFromJson(string json, string parentToken = null, StripeResponse stripeResponse = null)
        {
            T obj = JsonConvert.DeserializeObject<T>(string.IsNullOrEmpty(parentToken) ? json : JObject.Parse(json).SelectToken(parentToken).ToString());
            Mapper<T>.applyStripeResponse(json, stripeResponse, (object)obj);
            return obj;
        }

        public static T MapFromJson(StripeResponse stripeResponse, string parentToken = null)
        {
            return Mapper<T>.MapFromJson(stripeResponse.ResponseJson, parentToken, stripeResponse);
        }

        private static void applyStripeResponse(string json, StripeResponse stripeResponse, object obj)
        {
            if (stripeResponse == null)
                return;
            foreach (PropertyInfo runtimeProperty in obj.GetType().GetRuntimeProperties())
            {
                if (runtimeProperty.Name == "StripeResponse")
                    runtimeProperty.SetValue(obj, (object)stripeResponse);
            }
            stripeResponse.ObjectJson = json;
        }
    }
}

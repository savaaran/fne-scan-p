using FundraisingandEngagement.StripeIntegration.Model;
using FundraisingandEngagement.StripeWebPayment.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace FundraisingandEngagement.StripeIntegration.Helpers
{
    internal class SourceConverter : JsonConverter
  {
    public override bool CanWrite
    {
      get
      {
        return false;
      }
    }

    public override bool CanConvert(Type objectType)
    {
      throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      JObject.FromObject(value).WriteTo(writer);
    }

    public override object ReadJson(
      JsonReader reader,
      Type objectType,
      object existingValue,
      JsonSerializer serializer)
    {
      JObject jobject = JObject.Load(reader);
      Source source1 = new Source();
      source1.Id = jobject.SelectToken("id").ToString();
      Source source2 = source1;
      if (jobject.SelectToken("object")?.ToString() == "card")
      {
        source2.Type = SourceType.Card;
        source2.Card = Mapper<StripeCard>.MapFromJson(jobject.ToString(), (string) null, (StripeResponse) null);
      }
      if (jobject.SelectToken("deleted")?.ToString() == "true")
      {
        source2.Type = SourceType.Deleted;
        source2.Deleted = Mapper<StripeDeleted>.MapFromJson(jobject.ToString(), (string) null, (StripeResponse) null);
      }
      return (object) source2;
    }
  }
}

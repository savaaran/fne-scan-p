using System.Threading;
using System.Threading.Tasks;
using FundraisingandEngagement.StripeIntegration.Model;
using FundraisingandEngagement.StripeWebPayment.Model;

namespace FundraisingandEngagement.StripeWebPayment.Service
{
    public class StripeTokenService : StripeService
    {
        public StripeTokenService(string apiKey = null) : base(apiKey) { }



        //Sync
        public virtual StripeToken Create(StripeTokenCreateOptions createOptions, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeToken>.MapFromJson(
                Requestor.PostString(this.ApplyAllParameters(createOptions, Urls.Tokens, false),
                SetupRequestOptions(requestOptions))
            );
        }

        public virtual StripeToken Get(string tokenId, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeToken>.MapFromJson(
                Requestor.GetString($"{Urls.Tokens}/{tokenId}",
                SetupRequestOptions(requestOptions))
            );
        }



        //Async
        public virtual async Task<StripeToken> CreateAsync(StripeTokenCreateOptions createOptions, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeToken>.MapFromJson(
                await Requestor.PostStringAsync(this.ApplyAllParameters(createOptions, Urls.Tokens, false),
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }

        public virtual async Task<StripeToken> GetAsync(string tokenId, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeToken>.MapFromJson(
                await Requestor.GetStringAsync($"{Urls.Tokens}/{tokenId}",
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }
    }

    public abstract class StripeService
    {
        public string ApiKey { get; set; }

        protected StripeService(string apiKey)
        {
            ApiKey = apiKey;
        }

        protected StripeRequestOptions SetupRequestOptions(StripeRequestOptions requestOptions)
        {
            if (requestOptions == null) requestOptions = new StripeRequestOptions();

            if (!string.IsNullOrEmpty(ApiKey))
                requestOptions.ApiKey = ApiKey;

            return requestOptions;
        }
    }

    public class StripeRequestOptions
    {
        public string ApiKey { get; set; }
        public string StripeConnectAccountId { get; set; }
        public string IdempotencyKey { get; set; }
    }
}

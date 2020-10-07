using FundraisingandEngagement.StripeIntegration.Helpers;
using FundraisingandEngagement.StripeIntegration.Model;
using FundraisingandEngagement.StripeWebPayment.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FundraisingandEngagement.StripeWebPayment.Service
{
    public class StripeCustomerService : StripeService
    {
        public StripeCustomerService(string apiKey = null) : base(apiKey) { }

        public bool ExpandDefaultSource { get; set; }
        public bool ExpandDefaultCustomerBankAccount { get; set; }

        //Sync
        public virtual StripeCustomer Create(StripeCustomerCreateOptions createOptions, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeCustomer>.MapFromJson(
                Requestor.PostString(this.ApplyAllParameters(createOptions, Urls.Customers, false),
                SetupRequestOptions(requestOptions))
            );
        }

        public virtual StripeCustomer Get(string customerId, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeCustomer>.MapFromJson(
                Requestor.GetString(this.ApplyAllParameters(null, $"{Urls.Customers}/{customerId}", false),
                SetupRequestOptions(requestOptions))
            );
        }

        public virtual StripeCustomer Update(string customerId, StripeCustomerUpdateOptions updateOptions, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeCustomer>.MapFromJson(
                Requestor.PostString(this.ApplyAllParameters(updateOptions, $"{Urls.Customers}/{customerId}", false),
                SetupRequestOptions(requestOptions))
            );
        }

        public virtual StripeDeleted Delete(string customerId, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeDeleted>.MapFromJson(
                Requestor.Delete($"{Urls.Customers}/{customerId}",
                SetupRequestOptions(requestOptions))
             );
        }

        public virtual IEnumerable<StripeCustomer> List(StripeCustomerListOptions listOptions = null, StripeRequestOptions requestOptions = null)
        {
            return Mapper<StripeCustomer>.MapCollectionFromJson(
                Requestor.GetString(this.ApplyAllParameters(listOptions, Urls.Customers, true),
                SetupRequestOptions(requestOptions))
            );
        }

        //Async
        public virtual async Task<StripeCustomer> CreateAsync(StripeCustomerCreateOptions createOptions, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeCustomer>.MapFromJson(
                await Requestor.PostStringAsync(this.ApplyAllParameters(createOptions, Urls.Customers, false),
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }

        public virtual async Task<StripeCustomer> GetAsync(string customerId, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeCustomer>.MapFromJson(
                await Requestor.GetStringAsync(this.ApplyAllParameters(null, $"{Urls.Customers}/{customerId}", false),
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }

        public virtual async Task<StripeCustomer> UpdateAsync(string customerId, StripeCustomerUpdateOptions updateOptions, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeCustomer>.MapFromJson(
                await Requestor.PostStringAsync(this.ApplyAllParameters(updateOptions, $"{Urls.Customers}/{customerId}", false),
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }

        public virtual async Task<StripeDeleted> DeleteAsync(string customerId, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeDeleted>.MapFromJson(
                await Requestor.DeleteAsync($"{Urls.Customers}/{customerId}",
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }

        public virtual async Task<IEnumerable<StripeCustomer>> ListAsync(StripeCustomerListOptions listOptions = null, StripeRequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Mapper<StripeCustomer>.MapCollectionFromJson(
                await Requestor.GetStringAsync(this.ApplyAllParameters(listOptions, Urls.Customers, true),
                SetupRequestOptions(requestOptions),
                cancellationToken)
            );
        }
    }

    internal class CustomerService
    {
        private string customerStripeUrl = "https://api.stripe.com/v1/customers";
        //private IOrganizationService service;
        //private SettingsModel settings;
        private BaseStipeRepository stipeRepository;

        public CustomerService()
        {
            //this.service = service;
            //this.settings = settings;
            this.stipeRepository = new BaseStipeRepository();
        }

        public StripeCustomer GetStripeCustomer(string custName, string custEmail, string strToken)
        {
            string customerNameAttribute = custName;
            string email = custEmail;
            return this.CreateCustomer(customerNameAttribute, email, strToken);
        }

        private StripeCustomer CreateCustomer(string customerName, string customerEmail, string strToken)
        {
            return this.stipeRepository.Create<StripeCustomer>(new StripeCustomer()
            {
                Email = customerEmail,
                Description = customerName
            }, this.customerStripeUrl, strToken);
        }
    }
}

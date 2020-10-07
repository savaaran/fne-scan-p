using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Plugins.Common
{
    public static class AutoSoftCredit
    {
        public static Entity CreateSoftCredit(Entity originalDonation, ColumnSet donationColumnnsToCopy, EntityReference softCreditCustomer, IOrganizationService service, ITracingService tracingService)
        {
            tracingService.Trace("Creating a Soft Credit.");
            tracingService.Trace("Original Donation:" + originalDonation.Id);

            // start with a full copy of the original donation
            Entity softCredit =
                service.Retrieve(originalDonation.LogicalName, originalDonation.Id, donationColumnnsToCopy);

            // TGet rid of the original donation's id here
            softCredit.Id = Guid.Empty;
            softCredit.Attributes.Remove("msnfp_transactionid");

            // Make change the type, customer etc... to make this a soft credit
            softCredit["msnfp_typecode"] = new OptionSetValue(844060001); // type = Credit

            // The soft credit's Customer/Donor is specified by the softCreditCustomer parameter 
            // (this should be either the Constituent or the Solicitor from the original donation)
            softCredit["msnfp_customerid"] = softCreditCustomer;

            // The soft credit's related Customer/Donor is the orignal donation's Donor/Customer
            softCredit["msnfp_relatedcustomerid"] = originalDonation.GetAttributeValue<EntityReference>("msnfp_customerid");
            tracingService.Trace("Soft Credit customer:" + softCreditCustomer.Id);

            // The soft credit is a child of the orignal donation
            softCredit["msnfp_parenttransactionid"] = new EntityReference(originalDonation.LogicalName, originalDonation.Id);
            tracingService.Trace("Soft Credit Parent Transaction:" + originalDonation.Id);

            // null out the Constituent and Solicitor fields
            softCredit["msnfp_relatedconstituentid"] = null;
            softCredit["msnfp_solicitorid"] = null;

            return softCredit;
        }
    }
}

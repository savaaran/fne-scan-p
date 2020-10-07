/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Client;
using Plugins.PaymentProcesses;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk.Messages;

namespace Plugins
{
    public class TransactionGiftUpdate : PluginBase
    {
        private readonly string preImageAlias = "transaction";

        private readonly string postImageAlias = "transaction";

        public TransactionGiftUpdate(string unsecure, string secure)
            : base(typeof(TransactionGiftUpdate))
        {
        }

        public ITracingService tracingService { get; set; }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            tracingService = localContext.TracingService;

            Entity preImageEntity = (context.PreEntityImages != null && context.PreEntityImages.Contains(this.preImageAlias)) ? context.PreEntityImages[this.preImageAlias] : null;
            Entity postImageEntity = (context.PostEntityImages != null && context.PostEntityImages.Contains(this.postImageAlias)) ? context.PostEntityImages[this.postImageAlias] : null;
            Entity target = context.InputParameters.Contains("Target") ? (Entity)context.InputParameters["Target"] : null;

            Guid currentUserID = context.InitiatingUserId;

            localContext.TracingService.Trace("got Transaction record as Primary entity.");

            Utilities util = new Utilities();
            Guid newDonorCommitmentId = Guid.Empty;
            Guid prevDonorCommitmentId = Guid.Empty;

            //get user record
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            localContext.TracingService.Trace("Retrieving values.");

            if (!postImageEntity.Contains("msnfp_configurationid") ||
                postImageEntity.GetAttributeValue<EntityReference>("msnfp_configurationid") == null)
            {
                throw new Exception("No configuration record found on this record (" + postImageEntity.LogicalName +
                                    ", id:" + postImageEntity.Id.ToString() +
                                    "). Please ensure the record has a configuration record attached.");
            }


            if (postImageEntity.Contains("msnfp_donorcommitmentid"))
            {
                newDonorCommitmentId = postImageEntity.Contains("msnfp_donorcommitmentid") ? ((EntityReference)postImageEntity["msnfp_donorcommitmentid"]).Id : Guid.Empty;
                localContext.TracingService.Trace("Transaction: newDonorCommitmentId: " + newDonorCommitmentId.ToString());

                localContext.TracingService.Trace("Transaction: Updating donor commitment");
                util.UpdateDonorCommitmentBalance(orgSvcContext, service, ((EntityReference)postImageEntity["msnfp_donorcommitmentid"]), 0);
            }

            if (preImageEntity.Contains("msnfp_donorcommitmentid"))
            {
                prevDonorCommitmentId = preImageEntity.Contains("msnfp_donorcommitmentid") ? ((EntityReference)preImageEntity["msnfp_donorcommitmentid"]).Id : Guid.Empty;
                localContext.TracingService.Trace("Transaction: prevDonorCommitmentId: " + prevDonorCommitmentId.ToString());

                localContext.TracingService.Trace("Transaction: Updating donor commitment");
                util.UpdateDonorCommitmentBalance(orgSvcContext, service, ((EntityReference)preImageEntity["msnfp_donorcommitmentid"]), 1);
            }


            if (postImageEntity.Contains("statuscode"))
            {
                localContext.TracingService.Trace("Transaction: statuscode found");
                if (((OptionSetValue)postImageEntity["statuscode"]).Value == 844060003 || ((OptionSetValue)postImageEntity["statuscode"]).Value == 844060004) // failed or refund
                {
                    if (preImageEntity.Contains("msnfp_donorcommitmentid"))
                        util.UpdateDonorCommitmentBalance(orgSvcContext, service, ((EntityReference)preImageEntity["msnfp_donorcommitmentid"]), 1);
                }
            }
        }
    }
}
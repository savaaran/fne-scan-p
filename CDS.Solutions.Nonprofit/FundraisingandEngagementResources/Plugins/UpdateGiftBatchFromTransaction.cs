/*************************************************************************
* © Microsoft. All rights reserved.
*/

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Plugins.PaymentProcesses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugins
{
    public class UpdateGiftBatchFromTransaction : PluginBase
    {
        private readonly string preImageAlias = "transaction";

        private readonly string postImageAlias = "transaction";

        public UpdateGiftBatchFromTransaction(string unsecure, string secure)
            : base(typeof(UpdateGiftBatchFromTransaction))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

            Entity preImageEntity = (context.PreEntityImages != null && context.PreEntityImages.Contains(this.preImageAlias)) ? context.PreEntityImages[this.preImageAlias] : null;
            Entity postImageEntity = (context.PostEntityImages != null && context.PostEntityImages.Contains(this.postImageAlias)) ? context.PostEntityImages[this.postImageAlias] : null;

            localContext.TracingService.Trace("got Transaction record as Primary entity.");

            Utilities util = new Utilities();

            if (string.Equals(context.MessageName, "update", StringComparison.CurrentCultureIgnoreCase))
            {
                // check to see if this transaction was just added or removed to a Gift Batch, or if it has been enabled/disabled
                // in all cases, we may need to recalcalcualte Gift Batch totals
                if (preImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid") != postImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid") ||
                    preImageEntity.GetAttributeValue<OptionSetValue>("statecode") != postImageEntity.GetAttributeValue<OptionSetValue>("statecode"))
                {
                    if (preImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid") != null)
                    {
                        util.RecalculateGiftBatch(preImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid"), service, localContext.TracingService);
                    }

                    if (postImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid") != null)
                    {
                        util.RecalculateGiftBatch(postImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid"), service, localContext.TracingService);
                    }
                }
            }

            else if (string.Equals(context.MessageName, "delete", StringComparison.CurrentCultureIgnoreCase))
            {
                // if the transaction is being deleted and it was part of a gift batch, recalculate to remove it's totals from the gift batch
                if (preImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid") != null)
                {
                    util.RecalculateGiftBatch(preImageEntity.GetAttributeValue<EntityReference>("msnfp_giftbatchid"), service, localContext.TracingService);
                }

            }
        }
    }
}

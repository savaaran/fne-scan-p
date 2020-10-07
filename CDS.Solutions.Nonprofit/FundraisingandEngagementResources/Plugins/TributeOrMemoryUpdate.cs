using Microsoft.Xrm.Sdk;
using System;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Plugins.PaymentProcesses;

namespace Plugins
{
    public class TributeOrMemoryUpdate : PluginBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TributeOrMemoryUpdate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public TributeOrMemoryUpdate(string unsecure, string secure)
            : base(typeof(TributeOrMemoryUpdate))
        {
            // TODO: Implement your custom configuration handling.
        }


        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            localContext.TracingService.Trace("---------Triggered TributeOrMemoryUpdate.cs---------");

            // ********************************* PLUGIN'S PREPARATION *******************************************
            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);


            // ********************************* PLUGIN'S VALIDATION ********************************************
            if (context.Depth > 1)
            {
                localContext.TracingService.Trace(
                    "Context.depth > 1 => Exiting Plugin. context.Depth: " + context.Depth);
                return;
            }

            string messageName = context.MessageName;

            // Get the Configuration Record (Either from the User or from the Default Configuration Record
            Entity configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            Entity targetRecord = null;
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                targetRecord = (Entity)context.InputParameters["Target"];
                if (targetRecord == null)
                    throw new InvalidPluginExecutionException("'Target' is null. Exiting plugin.");

                if (targetRecord.GetAttributeValue<EntityReference>("msnfp_duplicatetributeid") != null)
                {
                    EntityReference duplicateTributeRef =
                        targetRecord.GetAttributeValue<EntityReference>("msnfp_duplicatetributeid");

                    // get a reference to the Transactions associated with this Tribute
                    QueryByAttribute transactionQuery = new QueryByAttribute("msnfp_transaction");
                    transactionQuery.AddAttributeValue("msnfp_tributeid", duplicateTributeRef.Id);
                    transactionQuery.ColumnSet = new ColumnSet("msnfp_transactionid");
                    var results = service.RetrieveMultiple(transactionQuery);

                    if (results != null && results.Entities != null)
                    {
                        localContext.TracingService.Trace("Found " + results.Entities.Count +
                                                          " Transactions for Duplicate Tribute.");
                        foreach (var curTransaction in results.Entities)
                        {
                            localContext.TracingService.Trace("Updating Transaction:" + curTransaction.Id);
                            // replace the duplicate tribute with the target tribute
                            curTransaction["msnfp_tributeid"] =
                                new EntityReference(targetRecord.LogicalName, targetRecord.Id);
                            service.Update(curTransaction);
                            localContext.TracingService.Trace("Transaction Updated.");
                        }
                    }

                    // disable the duplicate
                    Entity duplicateTributeToUpdate = new Entity(duplicateTributeRef.LogicalName, duplicateTributeRef.Id);
                    duplicateTributeToUpdate["statecode"] = new OptionSetValue(1);
                    duplicateTributeToUpdate["statuscode"] = new OptionSetValue(2);
                    service.Update(duplicateTributeToUpdate);

                    // clear the Duplicate Transaction field
                    Entity tributeToUpdate = new Entity(targetRecord.LogicalName, targetRecord.Id);
                    tributeToUpdate["msnfp_duplicatetributeid"] = null;
                    service.Update(tributeToUpdate);
                    localContext.TracingService.Trace("Cleared Duplicate Field");
                }
            }
        }
    }
}

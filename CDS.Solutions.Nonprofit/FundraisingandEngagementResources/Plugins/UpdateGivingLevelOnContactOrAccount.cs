/*************************************************************************
* © Microsoft. All rights reserved.
*/

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Plugins.AzureModels;
using System;
using System.Activities;
using System.Activities.Debugger;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflows
{
    public class UpdateGivingLevelOnContactOrAccount : CodeActivity
    {
        private ITracingService tracingService;
        protected override void Execute(CodeActivityContext executionContext)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            // Create the tracing service
            tracingService = executionContext.GetExtension<ITracingService>();

            tracingService.Trace("Executing UpdateGivingLevelOnContactOrAccount..");

            // Create the context
            IWorkflowContext workflowContext = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(null);

            tracingService.Trace($"Workflow context {workflowContext == null}");

            EntityReference targetCustomerReference = new EntityReference(workflowContext.PrimaryEntityName, workflowContext.PrimaryEntityId);

            if (targetCustomerReference == null)
                throw new InvalidPluginExecutionException($"Target not found in workflwo..");

            // Primary entity has to be contact or account
            if (!(targetCustomerReference.LogicalName.ToLower() == "account" || targetCustomerReference.LogicalName.ToLower() == "contact"))
            {
                throw new InvalidPluginExecutionException($"Valid primary entity values account / contact. Current entity : {targetCustomerReference.LogicalName}");
            }

            decimal completedTransactionsAmount = GetCompletedTransactionsTotalAmount(service, targetCustomerReference);

            if (completedTransactionsAmount > 0)
            {
                Entity givingLevel = GetGivingLevel(service, completedTransactionsAmount);

                Guid currentGivingLevelInstanceId = CreateGivingLevelInstance(service, givingLevel, targetCustomerReference);

                if (givingLevel != null && currentGivingLevelInstanceId != Guid.Empty)
                    service.Update(new Entity
                    {
                        Id = targetCustomerReference.Id,
                        LogicalName = targetCustomerReference.LogicalName,
                        Attributes =
                    {
                        new KeyValuePair<string, object>("msnfp_givinglevelid",new EntityReference("msnfp_givinglevelinstance",currentGivingLevelInstanceId))
                    }
                    });
            }
        }

        /// <summary>
        /// Create primary giving level instance
        /// </summary>
        /// <param name="service">Service</param>
        /// <param name="givingLevel">Giving Level</param>
        /// <param name="targetCustomerReference">Target Customer Reference</param>
        private Guid CreateGivingLevelInstance(IOrganizationService service, Entity givingLevel, EntityReference targetCustomerReference)
        {
            if (givingLevel == null)
                return Guid.Empty;

            Entity givingLevelInstance = new Entity("msnfp_givinglevelinstance");
            givingLevelInstance["msnfp_givinglevelid"] = givingLevel.ToEntityReference();
            givingLevelInstance["msnfp_customerid"] = targetCustomerReference;
            givingLevelInstance["msnfp_primary"] = true;
            givingLevelInstance["msnfp_name"] = givingLevel.GetAttributeValue<string>("msnfp_identifier");
            givingLevelInstance["msnfp_identifier"] = givingLevel.GetAttributeValue<string>("msnfp_identifier");
            return service.Create(givingLevelInstance);
        }

        /// <summary>
        /// Get giving level based on total amount
        /// </summary>
        /// <param name="service">Service</param>
        /// <param name="completedTransactionsAmount">TransactionAmount</param>
        /// <returns></returns>
        private Entity GetGivingLevel(IOrganizationService service, decimal? completedTransactionsAmount)
        {
            if (!completedTransactionsAmount.HasValue)
                return null;

            QueryExpression queryExpression = new QueryExpression("msnfp_givinglevel");
            queryExpression.ColumnSet = new ColumnSet("msnfp_givinglevelid", "msnfp_identifier");
            queryExpression.NoLock = true;
            queryExpression.Criteria.AddCondition(new ConditionExpression("statuscode", ConditionOperator.Equal, 1));
            queryExpression.Criteria.AddCondition(new ConditionExpression("msnfp_amount_from", ConditionOperator.LessEqual, completedTransactionsAmount.Value));
            queryExpression.Criteria.AddCondition(new ConditionExpression("msnfp_amount_to", ConditionOperator.GreaterEqual, completedTransactionsAmount.Value));

            EntityCollection givingLevels = service.RetrieveMultiple(queryExpression);

            return givingLevels.Entities.FirstOrDefault();
        }

        /// <summary>
        /// Gets aggegate total amoun of all completed transactions for the customer
        /// </summary>
        /// <param name="service">Service</param>
        /// <param name="targetCustomerReference">CustomerReference</param>
        /// <returns></returns>
        private decimal GetCompletedTransactionsTotalAmount(IOrganizationService service, EntityReference targetCustomerReference)
        {
            // build aggregate query
            string fetchXml = $@"<fetch no-lock='true' aggregate='true' >
                                  <entity name='msnfp_transaction' >
                                    <attribute name='msnfp_amount' alias='TotalAmount' aggregate='sum' />
                                    <filter>
                                      <condition attribute='statuscode' operator='eq' value='844060000' />
                                      <condition attribute='msnfp_customerid' operator='eq' value='{targetCustomerReference.Id}' />
                                    </filter>
                                  </entity>
                                </fetch>";

            Entity aggregate = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();

            tracingService.Trace($"{fetchXml}");

            if (aggregate != null && aggregate.GetAttributeValue<AliasedValue>("TotalAmount") != null
                    && aggregate.GetAttributeValue<AliasedValue>("TotalAmount").Value != null
                    && ((Money)aggregate.GetAttributeValue<AliasedValue>("TotalAmount").Value) != null)
            {
                tracingService.Trace($"{((Money)(aggregate.GetAttributeValue<AliasedValue>("TotalAmount").Value)).Value}");
                return ((Money)(aggregate.GetAttributeValue<AliasedValue>("TotalAmount").Value)).Value;
            }

            return 0;
        }
    }
}

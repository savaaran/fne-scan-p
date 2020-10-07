using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;

namespace Plugins
{
    public class DesignatedCreditCreate : PluginBase
    {
        public DesignatedCreditCreate(string unsecure, string secure)
            : base(typeof(DesignatedCreditCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            localContext.TracingService.Trace("---------Triggered DesignatedCreditCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            string messageName = context.MessageName;

            if (context.Depth > 1)
            {
                localContext.TracingService.Trace("Context.depth > 1. Exiting Plugin.");
                return;
            }
            else
            {
                localContext.TracingService.Trace("Context.depth 0 = " + context.Depth);
            }

            Entity targetEntity;
            if (context.InputParameters["Target"] is Entity)
            {
                targetEntity = (Entity)context.InputParameters["Target"];

                Entity entityToUpdate = new Entity(targetEntity.LogicalName);
                entityToUpdate.Id = targetEntity.Id;
                EntityReference designationRef = targetEntity.GetAttributeValue<EntityReference>("msnfp_designatedcredit_designationid");
                if (designationRef != null)
                {
                    Entity designation = service.Retrieve(designationRef.LogicalName, designationRef.Id,
                        new Microsoft.Xrm.Sdk.Query.ColumnSet("msnfp_name"));
                    string designationName = designation.GetAttributeValue<string>("msnfp_name");
                    string entityName = designationName + "-$" +
                                        targetEntity.GetAttributeValue<Money>("msnfp_amount").Value;
                    entityToUpdate["msnfp_name"] = entityName;
                    service.Update(entityToUpdate);
                }
            }
        }
    }
}

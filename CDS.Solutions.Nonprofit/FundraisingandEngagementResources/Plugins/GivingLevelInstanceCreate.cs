using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugins
{
    public class GivingLevelInstanceCreate : PluginBase
    {
        public ITracingService tracingService { get; set; }

        public GivingLevelInstanceCreate(string unsecure, string secure) : base(typeof(GivingLevelInstanceCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            tracingService = localContext.TracingService;

            tracingService.Trace("---------Triggered GivingLevelInstanceCreate.cs---------");
            IPluginExecutionContext context = localContext.PluginExecutionContext;

            // Other places invoking giving level instance directly and setting it up as needed
            // This plugin should only be run in case a giving level instance is manually created
            if (context.Depth > 1)
            {
                tracingService.Trace($"Context depth >1 : {context.Depth}");
                return;
            }



            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity givingLevelInstance = (Entity)context.InputParameters["Target"];


                if (string.Equals(context.MessageName, "update", StringComparison.InvariantCultureIgnoreCase) && context.PostEntityImages.ContainsKey("postImage"))
                {
                    givingLevelInstance = context.PostEntityImages["postImage"];
                }

                IOrganizationService service = localContext.OrganizationService;
                ProcessGivingLevelInstance(service, givingLevelInstance.ToEntityReference(), givingLevelInstance.GetAttributeValue<EntityReference>("msnfp_customerid"), givingLevelInstance.GetAttributeValue<bool>("msnfp_primary"));
            }
        }

        private void ProcessGivingLevelInstance(IOrganizationService service, EntityReference givingLevelInstanceRef, EntityReference customerReference, bool isPrimary)
        {
            if (customerReference != null && isPrimary)
            {
                Entity donor = service.Retrieve(customerReference.LogicalName, customerReference.Id, new ColumnSet("msnfp_givinglevelid"));

                // Get all other primary giving level instances for the customer
                EntityCollection givingLevelInstances = service.RetrieveMultiple(new QueryExpression(givingLevelInstanceRef.LogicalName)
                {
                    ColumnSet = new ColumnSet("msnfp_givinglevelinstanceid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("msnfp_customerid",ConditionOperator.Equal, customerReference.Id),
                            new ConditionExpression("msnfp_primary",ConditionOperator.Equal,true),
                            new ConditionExpression("msnfp_givinglevelinstanceid",ConditionOperator.NotEqual, givingLevelInstanceRef.Id)
                        }
                    }
                });

                // Update only if giving level instance on donor doesn't match the current one
                if (donor.GetAttributeValue<EntityReference>("msnfp_givinglevelid") == null || (donor.GetAttributeValue<EntityReference>("msnfp_givinglevelid").Id != givingLevelInstanceRef.Id))
                {
                    service.Update(new Entity(customerReference.LogicalName, customerReference.Id)
                    {
                        Attributes =
                        {
                            new KeyValuePair<string, object>("msnfp_givinglevelid",givingLevelInstanceRef)
                        }
                    });
                }

                // Update other giving level instances to primary false
                givingLevelInstances.Entities.ToList().ForEach(gi =>
                {
                    gi["msnfp_primary"] = false;

                    service.Update(gi);
                });
            }
            else
            {
                tracingService.Trace("No processing done as either it is not primary or no customer on it");
            }
        }
    }
}

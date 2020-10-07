using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugins.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;

namespace Plugins
{
    public class RegistrationSecurityStatusCreate : CodeActivity
    {
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Entered RegistrationSecurityStatusCreate Activity");
            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(null);

            Guid entityId = context.PrimaryEntityId;
            string logicalName = context.PrimaryEntityName;
            Entity checkinRecord = new Entity(logicalName, entityId);
            string securityCode = Utilities.RandomString(4);
            checkinRecord["msnfp_name"] = securityCode;
            checkinRecord["msnfp_securitycode"] = securityCode;
            service.Update(checkinRecord);
        }
    }
}

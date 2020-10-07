using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using Plugins.PaymentProcesses;

namespace Workflows
{
    public class CreateReceiptForEventPackage : CodeActivity
    {
        [ReferenceTarget("msnfp_receipt")]
        [Output("Receipt")]
        public OutArgument<EntityReference> Receipt { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            // Create the tracing service
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            // Create the context
            IWorkflowContext _context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(null);

            try
            {
                tracingService.Trace("Entering CreateReceiptForEventPackage");

                OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

                Guid eventPackageId = _context.PrimaryEntityId;
                Entity eventPackage = service.Retrieve(_context.PrimaryEntityName, _context.PrimaryEntityId,
                new ColumnSet("msnfp_amount", "msnfp_amount_receipted", "msnfp_ref_amount_nonreceiptable",
                        "msnfp_configurationid", "msnfp_taxreceiptid", "transactioncurrencyid", "msnfp_customerid",
                        "ownerid", "statuscode"));

                EntityReference configRef = GetConfiguration(eventPackage, _context, service, tracingService);

                EntityReference previousReceipt = GetPreviousReceipt(eventPackage, tracingService);

                int currentYear = DateTime.Now.Year;
                tracingService.Trace("currentYear:" + currentYear);

                decimal totalAmount = eventPackage.GetAttributeValue<Money>("msnfp_amount") != null ? eventPackage.GetAttributeValue<Money>("msnfp_amount").Value : 0;
                tracingService.Trace("totalAmount:" + totalAmount);
                decimal totalAmountReceipted = eventPackage.GetAttributeValue<Money>("msnfp_amount_receipted") != null ? eventPackage.GetAttributeValue<Money>("msnfp_amount_receipted").Value : 0;
                tracingService.Trace("totalAmountReceipted:" + totalAmountReceipted);
                decimal totalAmountNonReceiptable = eventPackage.GetAttributeValue<Money>("msnfp_ref_amount_nonreceiptable") != null ? eventPackage.GetAttributeValue<Money>("msnfp_ref_amount_nonreceiptable").Value : 0;
                tracingService.Trace("totalAmountNonReceiptable:" + totalAmountNonReceiptable);

                Entity receiptStack = GetReceiptStack(configRef.Id, currentYear, tracingService, service);

                // create a new Receipt record
                Guid receiptId = CreateNewReceiptRecord(receiptStack, totalAmountReceipted, 0,
                    totalAmountNonReceiptable, 0, totalAmount, eventPackage, null, previousReceipt,
                    tracingService, service);

                // update the Payment Schedule with the new Receipt
                AssociateReceiptToEventPackage(receiptId, eventPackage, tracingService, service);

                tracingService.Trace("Done.");
                Receipt.Set(executionContext, new EntityReference("msnfp_receipt", receiptId));

            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault> e)
            {
                tracingService.Trace("Workflow Exception: {0}", e.ToString());
                throw;
            }

        }

        private static EntityReference GetConfiguration(Entity EventPackage, IWorkflowContext _context, IOrganizationService service, ITracingService tracingService)
        {
            EntityReference configRef = null;
            // get the configuration from the event package itself
            tracingService.Trace("Getting Configuration from Event Package.");
            configRef = EventPackage.GetAttributeValue<EntityReference>("msnfp_configurationid");

            if (configRef == null)
            {
                tracingService.Trace("Getting Configuration from User.");
                // get the configuration from the user
                Guid userId = _context.InitiatingUserId;
                Entity user = service.Retrieve("systemuser", userId, new ColumnSet("msnfp_configurationid"));
                if (user == null) throw new Exception("User Not Found. Aborting");
                if (user.GetAttributeValue<EntityReference>("msnfp_configurationid") == null)
                    throw new Exception("User does not have a Configuration record. Aborting.");
                configRef = user.GetAttributeValue<EntityReference>("msnfp_configurationid");
            }
            tracingService.Trace("Got Configuration id:" + (configRef != null ? configRef.Id.ToString() : "nothing found."));
            return configRef;
        }

        private static Guid CreateNewReceiptRecord(Entity receiptStack, decimal totalAmountReceipted,
    decimal totalAmountMembership, decimal totalAmountNonReceiptable, int matchingTransactionCount,
    decimal totalAmount, Entity eventPackage, DateTime? lastDonationDate, EntityReference previousReceiptRef,
    ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Generating new Receipt record");

            Entity receipt = new Entity("msnfp_receipt");
            receipt["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", receiptStack.Id);
            receipt["msnfp_amount_receipted"] = new Money(totalAmountReceipted);
            
            //Money totalAmountNonReceiptableMoney = new Money();
            //if (!totalAmountMembership.HasValue && !totalAmountNonReceiptable.HasValue)
            //{
            //    totalAmountNonReceiptableMoney = null;
            //}
            //else
            //{
            //    totalAmountNonReceiptableMoney =
            //        new Money((totalAmountMembership ?? 0) + (totalAmountNonReceiptable ?? 0));
            //}

            //receipt["msnfp_amount_nonreceiptable"] = totalAmountNonReceiptableMoney;

            receipt["msnfp_amount_nonreceiptable"] = new Money(totalAmountMembership + totalAmountNonReceiptable);
            receipt["msnfp_receiptgeneration"] = new OptionSetValue(844060000); //System Generated
            receipt["msnfp_receiptissuedate"] = DateTime.Now;
            receipt["msnfp_transactioncount"] = matchingTransactionCount;
            receipt["msnfp_amount"] = new Money(totalAmount);
            receipt["transactioncurrencyid"] = eventPackage.GetAttributeValue<EntityReference>("transactioncurrencyid");
            if (eventPackage.GetAttributeValue<EntityReference>("msnfp_customerid") != null)
            {
                receipt["msnfp_customerid"] = eventPackage.GetAttributeValue<EntityReference>("msnfp_customerid");
            }

            receipt["msnfp_lastdonationdate"] = lastDonationDate;
            receipt["ownerid"] = eventPackage.GetAttributeValue<EntityReference>("ownerid");

            OptionSetValue eventPackageStatusReason = eventPackage.GetAttributeValue<OptionSetValue>("statuscode");
            if (eventPackageStatusReason.Value == 844060000) // Completed
            {
                tracingService.Trace("Event Package is Completed. Setting Receipt Status to Issued.");
                receipt["statuscode"] = new OptionSetValue(1); // Issued
            }
            else
            {
                tracingService.Trace("Event Package is NOT Completed. Setting Receipt Status to Void (Payment Failed).");
                receipt["statuscode"] = new OptionSetValue(844060002); // Void (Payment Failed)
            }

            receipt["msnfp_receiptgeneration"] = new OptionSetValue(844060000);
            receipt["msnfp_eventcount"] = 1;

            Guid receiptId;
            if (previousReceiptRef != null)
            {
                Entity previousReceipt = service.Retrieve(previousReceiptRef.LogicalName, previousReceiptRef.Id,
                    new ColumnSet("msnfp_generatedorprinted"));

                receipt["msnfp_generatedorprinted"] =
                    previousReceipt.GetAttributeValue<double>("msnfp_generatedorprinted") + Convert.ToDouble(1);
                tracingService.Trace("Updating Previous Receipt.");
                service.Update(receipt);
                tracingService.Trace("Receipt Updated. Id:" + previousReceiptRef.Id);
                receiptId = previousReceiptRef.Id;
            }
            else
            {
                receipt["msnfp_generatedorprinted"] = Convert.ToDouble(1);

                tracingService.Trace("Saving new Receipt.");
                receiptId = service.Create(receipt);
                tracingService.Trace("Receipt Created. Id:" + receiptId);
            }

            return receiptId;
        }

        private static EntityReference GetPreviousReceipt(Entity eventPackage, ITracingService tracingService)
        {
            tracingService.Trace("Looking for Previous Receipt (if any)");
            var previousReceipt = eventPackage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");

            if (previousReceipt != null)
                tracingService.Trace("Previous Receipt Found. Id:" + previousReceipt.Id);
            else
                tracingService.Trace("No Previous Receipt Found.");

            return previousReceipt;
        }

        // the receipt stack must have a Configuration reccord matching the Event Package AND the year must match the current year
        private static Entity GetReceiptStack(Guid configurationId, int currentYear, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Getting Receipt Stack for year:" + currentYear.ToString());
            Entity receiptStack = null;
            int receiptYearVal =
                Utilities.GetOptionsSetValueForLabel(service, "msnfp_receiptstack", "msnfp_receiptyear", currentYear.ToString());
            tracingService.Trace("receiptYearVal:" + receiptYearVal);

            QueryByAttribute receiptStackQuery = new QueryByAttribute("msnfp_receiptstack");
            receiptStackQuery.ColumnSet = new ColumnSet("msnfp_configurationid", "msnfp_receiptyear", "statecode");
            receiptStackQuery.AddAttributeValue("msnfp_configurationid", configurationId);
            receiptStackQuery.AddAttributeValue("msnfp_receiptyear", receiptYearVal);
            receiptStackQuery.AddAttributeValue("statecode", 0);
            var result = service.RetrieveMultiple(receiptStackQuery);
            if (result != null && result.Entities != null)
            {
                receiptStack = result.Entities[0];
            }

            if (receiptStack != null)
                tracingService.Trace("Found Receipt Stack Id:" + receiptStack.Id);
            else
                tracingService.Trace("No Receipt Stack found.");


            return receiptStack;
        }

        private string GetReceiptIdentifier(Entity receiptStack, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Entering GetReceiptIdentifier().");
            string newReceiptNumber = string.Empty;
            string prefix = string.Empty;
            double currentRange = 0;
            int numberRange = 0;

            if (receiptStack != null)
            {
                tracingService.Trace("Obtaining prefix, current range and number range from Receipt Stack.");
                prefix = receiptStack.GetAttributeValue<string>("msnfp_prefix");
                currentRange = receiptStack.GetAttributeValue<double>("msnfp_currentrange");
                numberRange = receiptStack.GetAttributeValue<OptionSetValue>("msnfp_numberrange") != null ? receiptStack.GetAttributeValue<OptionSetValue>("msnfp_numberrange").Value : 0;

                if (numberRange == 844060006)//six digit
                {
                    tracingService.Trace("Number range : 6 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(6, '0');
                }
                else if (numberRange == 844060008)//eight digit
                {
                    tracingService.Trace("Number range : 8 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(8, '0');
                }
                else if (numberRange == 844060010)//ten digit
                {
                    tracingService.Trace("Number range : 10 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(10, '0');
                }
                else
                {
                    tracingService.Trace("Receipt number range unknown. msnfp_numberrange: " + numberRange.ToString());
                }

                // Update the receipt stacks current number by 1.
                tracingService.Trace("Now update the receipt stacks current number by 1.");
                Entity receiptStackToUpdate = new Entity(receiptStack.LogicalName, receiptStack.Id);
                receiptStackToUpdate["msnfp_currentrange"] = currentRange + 1;
                service.Update(receiptStackToUpdate);
                tracingService.Trace("Updated Receipt Stack current range to: " + (currentRange + 1).ToString());
            }
            else
            {
                tracingService.Trace("No receipt stack found.");
            }

            tracingService.Trace("Exiting SetReceiptIdentifier().");
            return newReceiptNumber;
        }

        private static void AssociateReceiptToEventPackage(Guid receiptId, Entity eventPackage, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Associating new Receipt to Event Package");
            Entity eventPackageToUpdate = new Entity(eventPackage.LogicalName, eventPackage.Id);
            eventPackageToUpdate["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", receiptId);
            service.Update(eventPackageToUpdate);
            tracingService.Trace("Done Associating new Receipt to Event Package");
        }

    }
}
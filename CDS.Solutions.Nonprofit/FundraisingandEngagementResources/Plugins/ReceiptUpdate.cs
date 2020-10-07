using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using System.Collections.Generic;
using Plugins.PaymentProcesses;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class ReceiptUpdate : PluginBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReceiptUpdate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public ReceiptUpdate(string unsecure, string secure)
            : base(typeof(ReceiptUpdate))
        {
            // TODO: Implement your custom configuration handling.
        }

        /// <summary>
        /// Main entry point for he business logic that the plug-in is to execute.
        /// </summary>
        /// <param name="localContext">The <see cref="LocalPluginContext"/> which contains the
        /// <see cref="IPluginExecutionContext"/>,
        /// <see cref="IOrganizationService"/>
        /// and <see cref="ITracingService"/>
        /// </param>
        /// <remarks>
        /// For improved performance, Microsoft Dynamics CRM caches plug-in instances.
        /// The plug-in's Execute method should be written to be stateless as the constructor
        /// is not called for every invocation of the plug-in. Also, multiple system threads
        /// could execute the plug-in at the same time. All per invocation state information
        /// is stored in the context. This means that you should not use global variables in plug-ins.
        /// </remarks>
        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            localContext.TracingService.Trace("---------Triggered ReceiptUpdate.cs---------");

            if (context.Depth > 2)
            {
                localContext.TracingService.Trace("Context.depth > 2. Exiting Plugin.");
                return;
            }

            Utilities Utilities = new Utilities();

            localContext.TracingService.Trace("---------Entering ReceiptUpdate.cs Main Function---------");

            ColumnSet cols = new ColumnSet("msnfp_receiptid", "msnfp_generatedorprinted", "msnfp_amount_nonreceiptable", "msnfp_receiptnumber", "msnfp_receiptissuedate", "msnfp_amount_receipted", "msnfp_receiptgeneration", "msnfp_receiptstackid", "msnfp_receiptstatus", "msnfp_replacesreceiptid", "statuscode", "modifiedby", "msnfp_customerid", "msnfp_lastdonationdate");
            Entity primaryReceipt = service.Retrieve("msnfp_receipt", context.PrimaryEntityId, cols);

            localContext.TracingService.Trace("Retrieved primary receipt. Id:" + primaryReceipt.Id + ", Number" + primaryReceipt.GetAttributeValue<string>("msnfp_receiptnumber"));

            string statusText = Utilities.GetOptionSetValueLabel("msnfp_receipt", "statuscode", ((OptionSetValue)primaryReceipt["statuscode"]).Value, service);
            string receiptNumber = primaryReceipt.Contains("msnfp_receiptnumber") ? (string)primaryReceipt["msnfp_receiptnumber"] : string.Empty;

            if (primaryReceipt.Contains("statuscode")
                && ((OptionSetValue)primaryReceipt["statuscode"]).Value == 844060000) // ------------------------Void------------------------
            {
                localContext.TracingService.Trace("Statuscode : Void");

                primaryReceipt["msnfp_receiptstatus"] = "Receipt Voided";

                Entity receiptLog = new Entity("msnfp_receiptlog");

                if (primaryReceipt.Contains("msnfp_receiptstackid"))
                {
                    receiptLog["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", ((EntityReference)primaryReceipt["msnfp_receiptstackid"]).Id);
                }

                primaryReceipt["msnfp_identifier"] = receiptNumber + " - " + statusText;

                receiptLog["msnfp_receiptnumber"] = primaryReceipt.Contains("msnfp_receiptnumber") ? (string)primaryReceipt["msnfp_receiptnumber"] : string.Empty;
                receiptLog["msnfp_entryreason"] = "RECEIPT VOIDED";
                receiptLog["msnfp_entryby"] = ((EntityReference)primaryReceipt["modifiedby"]).Name;

                // If we don't do this, we will get multiple log entries. However we need the update fields to trigger this plugin:
                if (context.Depth < 2)
                {
                    service.Create(receiptLog);
                    localContext.TracingService.Trace("Receipt log created as Void.");
                }
                else
                {
                    localContext.TracingService.Trace("Receipt log not created due to context.depth >= 2.");
                }

            }
            else if (primaryReceipt.Contains("statuscode")
                && ((OptionSetValue)primaryReceipt["statuscode"]).Value == 844060001)// ------------------------Void (Reissued)------------------------
            {
                localContext.TracingService.Trace("Statuscode : Void (Reissued)");

                Entity newReceipt = new Entity("msnfp_receipt");
                string newReceiptNumber = string.Empty;
                string prefix = string.Empty;
                double currentRange = 0;
                int numberRange = 0;
                Entity receiptLog = new Entity("msnfp_receiptlog");

                List<Entity> giftList = (from g in orgSvcContext.CreateQuery("msnfp_transaction")
                                         where ((EntityReference)g["msnfp_taxreceiptid"]).Id == primaryReceipt.Id
                                         && (((OptionSetValue)g["statuscode"]).Value == 844060000 //Completed
                                         || ((OptionSetValue)g["statuscode"]).Value == 844060004  //Refunded
                                         || ((OptionSetValue)g["statuscode"]).Value == 1)         //Active
                                         select g).ToList();
                localContext.TracingService.Trace(giftList.Count + " Transactions");

                List<Entity> eventPackageList = (from ev in orgSvcContext.CreateQuery("msnfp_eventpackage")
                                                 where ((EntityReference)ev["msnfp_taxreceiptid"]).Id == primaryReceipt.Id
                                                 && ((OptionSetValue)ev["statuscode"]).Value == 844060000 // completed
                                                 select ev).ToList();
                localContext.TracingService.Trace(eventPackageList.Count + " Event Packages");


                // This updates the receipt on the payment schedule (if applicable):
                List<Entity> paymentScheduleList = (from ps in orgSvcContext.CreateQuery("msnfp_paymentschedule")
                                                    where ((EntityReference)ps["msnfp_taxreceiptid"]).Id == primaryReceipt.Id
                                                     && (((OptionSetValue)ps["statuscode"]).Value == 844060000 //Completed
                                                     || ((OptionSetValue)ps["statuscode"]).Value == 844060004  //Refunded
                                                     || ((OptionSetValue)ps["statuscode"]).Value == 1)         //Active
                                                    select ps).ToList();
                localContext.TracingService.Trace(paymentScheduleList.Count + " Payment Schedules");

                if (giftList.Count > 0 || eventPackageList.Count > 0)
                {
                    localContext.TracingService.Trace("Got related transactions (" + giftList.Count + ") for this receipt.");
                    localContext.TracingService.Trace("Got related event packages (" + eventPackageList.Count + ") for this receipt.");
                    localContext.TracingService.Trace("Got related payment schedule(s) (" + paymentScheduleList.Count + ") for this receipt.");

                    decimal totalHeader = 0;
                    decimal receiptableAmount = 0;
                    decimal amountMembership = 0;
                    decimal amountnonreceiptable = 0;

                    Entity receiptStack = null;
                    if (primaryReceipt.Contains("msnfp_receiptstackid"))
                        receiptStack = service.Retrieve("msnfp_receiptstack", ((EntityReference)primaryReceipt["msnfp_receiptstackid"]).Id, new ColumnSet("msnfp_receiptstackid", "msnfp_prefix", "msnfp_currentrange", "msnfp_numberrange"));

                    if (receiptStack != null)
                    {
                        localContext.TracingService.Trace("Receipt stack available.");

                        newReceipt["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", receiptStack.Id);

                        prefix = receiptStack.Contains("msnfp_prefix") ? (string)receiptStack["msnfp_prefix"] : string.Empty;
                        currentRange = receiptStack.Contains("msnfp_currentrange") ? (double)receiptStack["msnfp_currentrange"] : 0;
                        numberRange = receiptStack.Contains("msnfp_numberrange") ? ((OptionSetValue)receiptStack["msnfp_numberrange"]).Value : 0;

                        if (numberRange == 844060006)//six digit
                        {
                            localContext.TracingService.Trace("number range : 6 digit");
                            newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(6, '0');
                        }
                        else if (numberRange == 844060008)//eight digit
                        {
                            localContext.TracingService.Trace("number range : 8 digit");
                            newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(8, '0');
                        }
                        else if (numberRange == 844060010)//ten digit
                        {
                            localContext.TracingService.Trace("number range : 10 digit");
                            newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(10, '0');
                        }

                        localContext.TracingService.Trace("receiptNumber : " + newReceiptNumber);
                        receiptLog["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", receiptStack.Id);

                        double generatedOrPrinted = primaryReceipt.Contains("msnfp_generatedorprinted") ? (double)primaryReceipt["msnfp_generatedorprinted"] : 0;
                        newReceipt["msnfp_generatedorprinted"] = Convert.ToDouble(generatedOrPrinted);

                        newReceipt["msnfp_receiptnumber"] = newReceiptNumber;
                        newReceipt["msnfp_identifier"] = newReceiptNumber;

                        newReceipt["msnfp_receiptgeneration"] = new OptionSetValue(844060000);//System Generated
                        newReceipt["msnfp_replacesreceiptid"] = new EntityReference("msnfp_receipt", primaryReceipt.Id);

                        if (giftList != null)
                        {
                            if(giftList.Count > 0)
                            {
                                foreach (var item in giftList)
                                {
                                    if (item.Contains("msnfp_amount_receipted"))
                                        receiptableAmount += ((Money)item["msnfp_amount_receipted"]).Value;

                                    if (item.Contains("msnfp_amount_membership"))
                                        amountMembership += ((Money)item["msnfp_amount_membership"]).Value;

                                    if (item.Contains("msnfp_amount_nonreceiptable"))
                                        amountnonreceiptable += ((Money)item["msnfp_amount_nonreceiptable"]).Value;

                                    if (item.Contains("msnfp_amount"))
                                        totalHeader += ((Money)item["msnfp_amount"]).Value;
                                }
                            }
                        }

                        if (eventPackageList != null)
                        {
                            if(eventPackageList.Count > 0)
                            {
                                foreach (var item in giftList)
                                {
                                    if (item.Contains("msnfp_amount_receipted"))
                                        receiptableAmount += ((Money)item["msnfp_amount_receipted"]).Value;

                                    if (item.Contains("msnfp_amount_membership"))
                                        amountMembership += ((Money)item["msnfp_amount_membership"]).Value;

                                    if (item.Contains("msnfp_amount_nonreceiptable"))
                                        amountnonreceiptable += ((Money)item["msnfp_amount_nonreceiptable"]).Value;

                                    if (item.Contains("msnfp_amount"))
                                        totalHeader += ((Money)item["msnfp_amount"]).Value;
                                }
                            }
                        }

                        newReceipt["msnfp_amount_receipted"] = new Money(receiptableAmount);
                        newReceipt["msnfp_amount_nonreceiptable"] = new Money(amountMembership + amountnonreceiptable);

                        string oldReceiptIssueDate = string.Empty;

                        if (primaryReceipt.Contains("msnfp_receiptissuedate"))
                            oldReceiptIssueDate = ((DateTime)primaryReceipt["msnfp_receiptissuedate"]).ToShortDateString();

                        newReceipt["msnfp_receiptstatus"] = "This receipt replaces receipt " + receiptNumber + " issued on " + oldReceiptIssueDate;

                        if (primaryReceipt.Contains("msnfp_lastdonationdate"))
                        {
                            newReceipt["msnfp_lastdonationdate"] = (DateTime)primaryReceipt["msnfp_lastdonationdate"];
                        }

                        newReceipt["msnfp_receiptissuedate"] = DateTime.Now;

                        newReceipt["msnfp_transactioncount"] = giftList.Count;
                        newReceipt["msnfp_eventcount"] = eventPackageList.Count;
                        newReceipt["msnfp_amount"] = new Money(totalHeader);
                        newReceipt["statuscode"] = new OptionSetValue(1);//Issued

                        if (primaryReceipt.Contains("msnfp_customerid"))
                        {
                            string customerType = ((EntityReference)primaryReceipt["msnfp_customerid"]).LogicalName;
                            Guid customerId = ((EntityReference)primaryReceipt["msnfp_customerid"]).Id;

                            newReceipt["msnfp_customerid"] = new EntityReference(customerType, customerId);
                        }

                        Guid newReceiptID = service.Create(newReceipt);

                        if (newReceiptID != null)
                        {
                            // Update the receipt stacks current number by 1.
                            localContext.TracingService.Trace("Receipt created successfully. Update the receipt stacks current number by 1.");
                            receiptStack["msnfp_currentrange"] = currentRange + 1;
                            service.Update(receiptStack);
                            localContext.TracingService.Trace("Updated Receipt Stack current range to: " + (currentRange + 1).ToString());
                        }

                        localContext.TracingService.Trace("new receipt created");

                        primaryReceipt["msnfp_identifier"] = receiptNumber + " - Reissued";
                        //primaryReceipt["msnfp_title"] = receiptNumber + " - " + statusText;
                        primaryReceipt["msnfp_receiptstatus"] = "Void";

                        receiptLog["msnfp_receiptnumber"] = primaryReceipt.Contains("msnfp_receiptnumber") ? (string)primaryReceipt["msnfp_receiptnumber"] : string.Empty;
                        receiptLog["msnfp_entryreason"] = "RECEIPT REPLACED BY RECEIPT " + newReceiptNumber;
                        //receiptLog["msnfp_entryreason"] = "This cancels and replaces " + receiptNumber;
                        receiptLog["msnfp_entryby"] = ((EntityReference)primaryReceipt["modifiedby"]).Name;

                        service.Create(receiptLog);

                        localContext.TracingService.Trace("Receipt log created as Void (Reissued).");

                        if (giftList != null)
                        {
                            foreach (var item in giftList)
                            {
                                Entity gift = service.Retrieve("msnfp_transaction", item.Id,
                                    new ColumnSet("msnfp_taxreceiptid"));
                                if (gift != null)
                                {
                                    gift["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                    //gift["msnfp_isupdated"] = false;
                                    //gift["msnfp_isrefunded"] = false;
                                    service.Update(gift);

                                    localContext.TracingService.Trace("Updated gift with new receipt.");
                                }
                            }
                        }

                        if (eventPackageList != null)
                        {
                            foreach (var item in eventPackageList)
                            {
                                Entity gift = service.Retrieve("msnfp_eventpackage", item.Id,
                                    new ColumnSet("msnfp_taxreceiptid"));
                                if (gift != null)
                                {
                                    gift["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                    //gift["msnfp_isupdated"] = false;
                                    //gift["msnfp_isrefunded"] = false;
                                    service.Update(gift);

                                    localContext.TracingService.Trace("Updated event package with new receipt.");
                                }
                            }
                        }

                        foreach (var item in paymentScheduleList)
                        {
                            Entity gift = service.Retrieve("msnfp_paymentschedule", item.Id, new ColumnSet("msnfp_taxreceiptid"));
                            if (gift != null)
                            {
                                gift["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                service.Update(gift);

                                localContext.TracingService.Trace("Updated payment schedule with new receipt.");
                            }
                        }
                    }
                }
            }
            else if (primaryReceipt.Contains("statuscode")
                && ((OptionSetValue)primaryReceipt["statuscode"]).Value == 844060002)// ------------------------Void (Payment Failed)------------------------
            {
                localContext.TracingService.Trace("statuscode : Void (Payment Failed)");

                primaryReceipt["msnfp_receiptstatus"] = "Receipt Voided";

                Entity receiptLog = new Entity("msnfp_receiptlog");

                if (primaryReceipt.Contains("msnfp_receiptstackid"))
                    receiptLog["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", ((EntityReference)primaryReceipt["msnfp_receiptstackid"]).Id);

                primaryReceipt["msnfp_identifier"] = receiptNumber + " - " + statusText;

                receiptLog["msnfp_receiptnumber"] = primaryReceipt.Contains("msnfp_receiptnumber") ? (string)primaryReceipt["msnfp_receiptnumber"] : string.Empty;
                receiptLog["msnfp_entryreason"] = "PAYMENT FAILED RECEIPT VOIDED";
                receiptLog["msnfp_entryby"] = ((EntityReference)primaryReceipt["modifiedby"]).Name;

                service.Create(receiptLog);

                localContext.TracingService.Trace("receipt log created as Void (Payment Failed).");
            }

            service.Update(primaryReceipt);


        }
    }
}

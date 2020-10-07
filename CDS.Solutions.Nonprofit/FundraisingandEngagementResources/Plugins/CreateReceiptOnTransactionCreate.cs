using System;
using System.Linq;
using System.Activities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Plugins.PaymentProcesses;

namespace Workflows
{

    public sealed class CreateReceiptOnTransactionCreate : CodeActivity
    {
        private bool IsTraced = false;

        // If your activity returns a value, derive from CodeActivity<TResult>
        // and return the value from the Execute method.
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
                OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

                Guid giftID = _context.PrimaryEntityId;
                Entity gift = service.Retrieve(_context.PrimaryEntityName, _context.PrimaryEntityId, new ColumnSet("msnfp_transactionid", "msnfp_taxreceiptid", "msnfp_customerid", "statuscode", "msnfp_bookdate", "msnfp_configurationid", "msnfp_amount_receipted", "msnfp_amount", "msnfp_amount_membership", "msnfp_amount_nonreceiptable", "msnfp_thirdpartyreceipt", "transactioncurrencyid", "ownerid", "msnfp_receiptpreferencecode"));
                decimal amount = decimal.Zero;
                decimal amountMembership = decimal.Zero;
                decimal amountnonreceiptable = decimal.Zero;

                tracingService.Trace("Entering CreateReceiptOnTransactionCreate.cs");
                if (IsTraced)
                    tracingService.Trace("Transaction Id : " + giftID.ToString());

                if (gift.Contains("msnfp_thirdpartyreceipt"))
                {
                    if (IsTraced)
                        tracingService.Trace("Transaction contains msnfp_thirdpartyreceipt");

                    Entity receipt = new Entity("msnfp_receipt");

                    receipt["msnfp_receipt"] = (string)gift["msnfp_thirdpartyreceipt"];
                    receipt["msnfp_title"] = (string)gift["msnfp_thirdpartyreceipt"];
                    receipt["msnfp_receiptgeneration"] = new OptionSetValue(844060002);//Third Party

                    amount = gift.Contains("msnfp_amount_receipted") ? ((Money)gift["msnfp_amount_receipted"]).Value : decimal.Zero;
                    amountMembership = gift.Contains("msnfp_amount_membership") ? ((Money)gift["msnfp_amount_membership"]).Value : decimal.Zero;
                    amountnonreceiptable = gift.Contains("msnfp_amount_nonreceiptable") ? ((Money)gift["msnfp_amount_nonreceiptable"]).Value : decimal.Zero;

                    if (IsTraced)
                        tracingService.Trace("got membership amount and non-receiptable amount.");

                    if (IsTraced)
                        tracingService.Trace("amount " + amount.ToString() + " Membership Amount: " + amountMembership.ToString() + " Non-receiptable : " + amountnonreceiptable.ToString());

                    receipt["msnfp_amount_receipted"] = gift["msnfp_amount_receipted"];
                    receipt["msnfp_amount_nonreceiptable"] = new Money(amountMembership + amountnonreceiptable);

                    string ownerType = ((EntityReference)gift["ownerid"]).LogicalName;
                    Guid ownerID = ((EntityReference)gift["ownerid"]).Id;

                    receipt["ownerid"] = new EntityReference(ownerType, ownerID);
                    receipt["statuscode"] = new OptionSetValue(1);//Issued

                    Guid thirdPartyReceiptID = service.Create(receipt);

                    if (IsTraced)
                        tracingService.Trace("receipt created based on third party receipt number.");

                    gift["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", thirdPartyReceiptID);
                    gift["msnfp_previousreceiptid"] = new EntityReference("msnfp_receipt", thirdPartyReceiptID);
                    service.Update(gift);

                    if (IsTraced)
                        tracingService.Trace("Transaction updated with the receipt created wtih third party receipt number");
                }
                else
                {
                    if (gift.Contains("msnfp_receiptpreferencecode") && gift["msnfp_receiptpreferencecode"] != null)
                    {
                        if (IsTraced)
                            tracingService.Trace("Transaction contains receipt preference");

                        int receiptpreference = ((OptionSetValue)gift["msnfp_receiptpreferencecode"]).Value;

                        if (receiptpreference != 844060000)//do not receipt
                        {
                            if (IsTraced)
                                tracingService.Trace("Transaction receipt preference is NOT 'DO NOT RECEIPT', value: " + receiptpreference.ToString());

                            if (gift.Contains("msnfp_taxreceiptid"))
                            {
                                if (IsTraced)
                                    tracingService.Trace("Transaction contains receipt");

                                Guid receiptID = ((EntityReference)gift["msnfp_taxreceiptid"]).Id;
                                Entity receipt = service.Retrieve("msnfp_receipt", receiptID, new ColumnSet("msnfp_generatedorprinted", "statuscode"));
                                if (receipt != null)
                                {
                                    // select all gift where receipt = 
                                    if (IsTraced)
                                        tracingService.Trace("receipt retrieve");

                                    double generatedOrPrinted = receipt.Contains("msnfp_generatedorprinted") ? (double)receipt["msnfp_generatedorprinted"] : 0;
                                    receipt["msnfp_generatedorprinted"] = generatedOrPrinted + 1;

                                    if (gift.Contains("statuscode") && ((OptionSetValue)gift["statuscode"]).Value != 844060000//not equals to Completed
                                        && ((OptionSetValue)receipt["statuscode"]).Value == 1)//Issued
                                    {
                                        receipt["statuscode"] = new OptionSetValue(844060002);//Void (Payment Failed)
                                    }
                                    service.Update(receipt);

                                    // Now set the identifier:
                                    SetReceiptIdentifier(receipt.Id, tracingService, service);

                                    if (IsTraced)
                                        tracingService.Trace("receipt updated");
                                }
                            }
                            else
                            {
                                if (IsTraced)
                                    tracingService.Trace("Transaction does not contains receipt");

                                if (((OptionSetValue)gift["statuscode"]).Value == 844060000)//refund also check
                                {
                                    if (IsTraced)
                                        tracingService.Trace("Transaction is completed");

                                    Entity receipt = new Entity("msnfp_receipt");

                                    int giftYear = gift.Contains("msnfp_bookdate") ? ((DateTime)gift["msnfp_bookdate"]).Year : 0;

                                    if (IsTraced)
                                        tracingService.Trace("Gift Year : " + giftYear.ToString());

                                    if (giftYear != 0)
                                    {
                                        int receiptYearValue = Utilities.GetOptionsSetValueForLabel(service, "msnfp_receiptstack", "msnfp_receiptyear", giftYear.ToString());

                                        if (IsTraced)
                                            tracingService.Trace("receipt year value: " + receiptYearValue.ToString());

                                        Entity receiptStack = (from a in orgSvcContext.CreateQuery("msnfp_receiptstack")
                                                               where ((EntityReference)a["msnfp_configurationid"]).Id == ((EntityReference)gift["msnfp_configurationid"]).Id
                                                               && a["msnfp_receiptyear"] == new OptionSetValue(receiptYearValue)
                                                               select a).FirstOrDefault();

                                        if (receiptStack != null)
                                        {
                                            if (IsTraced)
                                                tracingService.Trace("receipt stake available.");

                                            receipt["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", receiptStack.Id);


                                            amount = gift.Contains("msnfp_amount_receipted") ? ((Money)gift["msnfp_amount_receipted"]).Value : decimal.Zero;
                                            amountMembership = gift.Contains("msnfp_amount_membership") ? ((Money)gift["msnfp_amount_membership"]).Value : decimal.Zero;
                                            amountnonreceiptable = gift.Contains("msnfp_amount_nonreceiptable") ? ((Money)gift["msnfp_amount_nonreceiptable"]).Value : decimal.Zero;

                                            if (IsTraced)
                                                tracingService.Trace("got membership amount and non-receiptable amount.");

                                            if (IsTraced)
                                                tracingService.Trace("amount " + amount.ToString() + " Membership Amount: " + amountMembership.ToString() + " Non-receiptable : " + amountnonreceiptable.ToString());

                                            receipt["msnfp_amount_receipted"] = gift["msnfp_amount_receipted"];
                                            receipt["msnfp_amount_nonreceiptable"] = new Money(amountMembership + amountnonreceiptable);
                                            receipt["msnfp_generatedorprinted"] = Convert.ToDouble(1);
                                            receipt["msnfp_receiptgeneration"] = new OptionSetValue(844060000);//System Generated
                                            receipt["msnfp_receiptissuedate"] = DateTime.Now;

                                            receipt["msnfp_transactioncount"] = 1;
                                            receipt["msnfp_amount"] = gift["msnfp_amount"];

                                            if (gift.Contains("transactioncurrencyid"))
                                                receipt["transactioncurrencyid"] = new EntityReference("transactioncurrency", ((EntityReference)gift["transactioncurrencyid"]).Id);

                                            if (gift.Contains("msnfp_customerid"))
                                            {
                                                string customerType = ((EntityReference)gift["msnfp_customerid"]).LogicalName;
                                                Guid customerId = ((EntityReference)gift["msnfp_customerid"]).Id;

                                                receipt["msnfp_customerid"] = new EntityReference(customerType, customerId);
                                            }

                                            string ownerType = ((EntityReference)gift["ownerid"]).LogicalName;
                                            Guid ownerID = ((EntityReference)gift["ownerid"]).Id;

                                            receipt["ownerid"] = new EntityReference(ownerType, ownerID);
                                            receipt["statuscode"] = new OptionSetValue(1);//Issued

                                            Guid newReceiptID = service.Create(receipt);

                                            // Now set the identifier:
                                            if (newReceiptID != null)
                                            {
                                                SetReceiptIdentifier(newReceiptID, tracingService, service);
                                            }

                                            if (IsTraced)
                                                tracingService.Trace("receipt created");

                                            gift["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                            gift["msnfp_previousreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                            //gift["msnfp_isupdated"] = false;
                                            //gift["msnfp_isrefunded"] = false;
                                            service.Update(gift);

                                            if (IsTraced)
                                                tracingService.Trace("Transaction Updated");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (IsTraced)
                            tracingService.Trace("Transaction does not contains receipt preference");

                        if (!gift.Contains("msnfp_taxreceiptid"))
                        {
                            if (IsTraced)
                                tracingService.Trace("Transaction does not contains receipt");

                            if (((OptionSetValue)gift["statuscode"]).Value == 844060000)
                            {
                                if (IsTraced)
                                    tracingService.Trace("Transaction is completed");

                                Entity receipt = new Entity("msnfp_receipt");

                                int giftYear = gift.Contains("msnfp_bookdate") ? ((DateTime)gift["msnfp_bookdate"]).Year : 0;

                                if (IsTraced)
                                    tracingService.Trace("Gift Year : " + giftYear.ToString());

                                if (giftYear != 0)
                                {
                                    int receiptYearValue = Utilities.GetOptionsSetValueForLabel(service, "msnfp_receiptstack", "msnfp_receiptyear", giftYear.ToString());

                                    if (IsTraced)
                                        tracingService.Trace("receipt year value: " + receiptYearValue.ToString());

                                    Entity receiptStack = (from a in orgSvcContext.CreateQuery("msnfp_receiptstack")
                                                           where ((EntityReference)a["msnfp_configurationid"]).Id == ((EntityReference)gift["msnfp_configurationid"]).Id
                                                           && a["msnfp_receiptyear"] == new OptionSetValue(receiptYearValue)
                                                           select a).FirstOrDefault();

                                    if (receiptStack != null)
                                    {
                                        if (IsTraced)
                                            tracingService.Trace("receipt stake available.");

                                        receipt["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", receiptStack.Id);


                                        amount = gift.Contains("msnfp_amount_receipted") ? ((Money)gift["msnfp_amount_receipted"]).Value : decimal.Zero;
                                        amountMembership = gift.Contains("msnfp_amount_membership") ? ((Money)gift["msnfp_amount_membership"]).Value : decimal.Zero;
                                        amountnonreceiptable = gift.Contains("msnfp_amount_nonreceiptable") ? ((Money)gift["msnfp_amount_nonreceiptable"]).Value : decimal.Zero;

                                        if (IsTraced)
                                            tracingService.Trace("got membership amount and non-receiptable amount.");

                                        if (IsTraced)
                                            tracingService.Trace("amount " + amount.ToString() + " Membership Amount: " + amountMembership.ToString() + " Non-receiptable : " + amountnonreceiptable.ToString());

                                        receipt["msnfp_amount_receipted"] = gift["msnfp_amount_receipted"];
                                        receipt["msnfp_amount_nonreceiptable"] = new Money(amountMembership + amountnonreceiptable);
                                        receipt["msnfp_generatedorprinted"] = Convert.ToDouble(1);
                                        receipt["msnfp_receiptgeneration"] = new OptionSetValue(844060000);//System Generated
                                        receipt["msnfp_receiptissuedate"] = DateTime.Now;

                                        receipt["msnfp_transactioncount"] = 1;
                                        receipt["msnfp_amount"] = gift["msnfp_amount"];

                                        if (gift.Contains("transactioncurrencyid"))
                                            receipt["transactioncurrencyid"] = new EntityReference("transactioncurrency", ((EntityReference)gift["transactioncurrencyid"]).Id);

                                        if (gift.Contains("msnfp_customerid"))
                                        {
                                            string customerType = ((EntityReference)gift["msnfp_customerid"]).LogicalName;
                                            Guid customerId = ((EntityReference)gift["msnfp_customerid"]).Id;

                                            receipt["msnfp_customerid"] = new EntityReference(customerType, customerId);
                                        }

                                        string ownerType = ((EntityReference)gift["ownerid"]).LogicalName;
                                        Guid ownerID = ((EntityReference)gift["ownerid"]).Id;

                                        receipt["ownerid"] = new EntityReference(ownerType, ownerID);
                                        receipt["statuscode"] = new OptionSetValue(1);//Issued

                                        Guid newReceiptID = service.Create(receipt);

                                        // Now set the identifier:
                                        if (newReceiptID != null)
                                        {
                                            SetReceiptIdentifier(newReceiptID, tracingService, service);
                                        }

                                        if (IsTraced)
                                            tracingService.Trace("receipt created");

                                        gift["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                        gift["msnfp_previousreceiptid"] = new EntityReference("msnfp_receipt", newReceiptID);
                                        //gift["msnfp_isupdated"] = false;
                                        //gift["msnfp_isrefunded"] = false;
                                        service.Update(gift);

                                        if (IsTraced)
                                            tracingService.Trace("Transaction Updated");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault> e)
            {
                if (IsTraced)
                    tracingService.Trace("Workflow Exception: {0}", e.ToString());
                throw;
            }

            if (IsTraced)
                throw new Exception("Tracing enabled. Set it to false to remove this message.");
        }


        #region Set Receipt Unique Identifier (This is also in PostReceiptCreate.cs)
        private void SetReceiptIdentifier(Guid receiptID, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Entering SetReceiptIdentifier().");
            Entity receiptStack = null;
            Guid? receiptStackId = null;
            string newReceiptNumber = string.Empty;
            string prefix = string.Empty;
            double currentRange = 0;
            int numberRange = 0;
            ColumnSet receiptCols = new ColumnSet("msnfp_receiptid", "msnfp_identifier", "msnfp_receiptstackid");
            Entity receiptToModify = service.Retrieve("msnfp_receipt", receiptID, receiptCols);
            tracingService.Trace("Found receipt with id: " + receiptID.ToString());

            if (receiptToModify.Contains("msnfp_identifier"))
            {
                tracingService.Trace("Found receipt identifier: " + (string)receiptToModify["msnfp_identifier"]);
                // If there is a value already, abort:
                if (receiptToModify["msnfp_identifier"] != null || ((string)receiptToModify["msnfp_identifier"]).Length > 0)
                {
                    tracingService.Trace("Receipt already has identfier. Exiting SetReceiptIdentifier().");
                    return;
                }
            }

            if (receiptToModify.Contains("msnfp_receiptstackid"))
            {
                receiptStackId = receiptToModify.GetAttributeValue<EntityReference>("msnfp_receiptstackid").Id;
                //receiptStack = service.Retrieve("msnfp_receiptstack", ((EntityReference)receiptToModify["msnfp_receiptstackid"]).Id, new ColumnSet("msnfp_receiptstackid", "msnfp_prefix", "msnfp_currentrange", "msnfp_numberrange"));
                tracingService.Trace("Found receipt stack.");
            }
            else
            {
                tracingService.Trace("No receipt stack found.");
            }

            if (receiptStackId != null)
            {
                // first, lock the Receipt Stack to prevent duplicate numbers
                tracingService.Trace("Locking Receipt Stack record Id:" + receiptStackId);
                Entity receiptStackToLock = new Entity("msnfp_receiptstack", receiptStackId.Value);
                receiptStackToLock["msnfp_locked"] = true;
                service.Update(receiptStackToLock);
                tracingService.Trace("Receipt Stack Record Locked.");

                // now we can proceed to get the new receipt identitifer
                receiptStack = service.Retrieve("msnfp_receiptstack", ((EntityReference)receiptToModify["msnfp_receiptstackid"]).Id, new ColumnSet("msnfp_receiptstackid", "msnfp_prefix", "msnfp_currentrange", "msnfp_numberrange"));


                tracingService.Trace("Obtaining prefix, current range and number range.");
                prefix = receiptStack.Contains("msnfp_prefix") ? (string)receiptStack["msnfp_prefix"] : string.Empty;
                currentRange = receiptStack.Contains("msnfp_currentrange") ? (double)receiptStack["msnfp_currentrange"] : 0;
                numberRange = receiptStack.Contains("msnfp_numberrange") ? ((OptionSetValue)receiptStack["msnfp_numberrange"]).Value : 0;

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

                tracingService.Trace("Receipt Number: " + newReceiptNumber);

                receiptToModify["msnfp_receiptnumber"] = newReceiptNumber;
                receiptToModify["msnfp_identifier"] = newReceiptNumber;

                tracingService.Trace("Updating Receipt.");
                service.Update(receiptToModify);
                tracingService.Trace("Receipt Updated");

                // Update the receipt stacks current number by 1.
                tracingService.Trace("Now update the receipt stacks current number by 1.");
                Entity receiptStackToUpdate = new Entity("msnfp_receiptstack", receiptStackId.Value);
                receiptStackToUpdate["msnfp_currentrange"] = currentRange + 1;
                receiptStackToUpdate["msnfp_locked"] = false;
                service.Update(receiptStackToUpdate);
                tracingService.Trace("Updated Receipt Stack current range to: " + (currentRange + 1).ToString());
            }
            else
            {
                tracingService.Trace("No receipt stack found.");
            }

            tracingService.Trace("Exiting SetReceiptIdentifier().");

        }
        #endregion
    }
}

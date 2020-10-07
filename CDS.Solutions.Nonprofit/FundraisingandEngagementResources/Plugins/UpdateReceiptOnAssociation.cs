/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;

namespace Plugins
{
    public class UpdateReceiptOnAssociation : PluginBase
    {
        public UpdateReceiptOnAssociation(string unsecure, string secure)
            : base(typeof(UpdateReceiptOnAssociation))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered UpdateReceiptOnAssociation.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

            // Note the queried record is used for updates while the entity reference is used for associations/dissassociations (due to the two different target types):
            Entity queriedEntityRecord = null;
            EntityReference targetIncomingRecord;
            string messageName = context.MessageName;
            string relationshipName = string.Empty;
            Entity transactionRecordOnUpdate = null;

            // This can prevent unnessary looping (when an update of a field triggers an update command and that command update a field that then triggers the same command again):
            if (context.Depth > 1)
            {
                localContext.TracingService.Trace("Context depth > 1. Exiting.");
                return;
            }

            if (context.InputParameters.Contains("Target"))
            {
                // This is the associate/dissassociate path:
                if (context.InputParameters["Target"] is EntityReference)
                {
                    localContext.TracingService.Trace("Message Name: " + messageName);
                    // We only do this on associate/disassociate:
                    if (context.MessageName.ToLower() == "associate" || context.MessageName.ToLower() == "disassociate")
                    {
                        // Get the “Relationship” Key from context
                        if (context.InputParameters.Contains("Relationship"))
                        {
                            // Get the Relationship name for which this plugin fired
                            relationshipName = ((Relationship)context.InputParameters["Relationship"]).SchemaName;
                            localContext.TracingService.Trace("Relationship found: " + relationshipName);
                        }

                        // Check the "Relationship Name" with the one for receipts/transactions (msnfp_Receipt_msnfp_Transaction):
                        if (relationshipName.ToLower() != "msnfp_receipt_msnfp_transaction")
                        {
                            localContext.TracingService.Trace("Not correct relationship (triggered by " + relationshipName + ", looking for msnfp_receipt_msnfp_transaction). Exiting.");
                            return;
                        }

                        localContext.TracingService.Trace("---------Entering UpdateReceiptOnAssociation.cs Main Function---------");

                        // Note that this can be either the receipt or the transaction, so we need to be careful with field updating:
                        targetIncomingRecord = (EntityReference)context.InputParameters["Target"];
                        localContext.TracingService.Trace("targetIncomingRecord Logical Name: " + targetIncomingRecord.LogicalName.ToString());

                        Guid currentUserID = context.InitiatingUserId;
                        Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

                        if (user == null)
                        {
                            throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
                        }

                        // If it is a receipt, we need to get the associated transactions (proceed as normal):
                        if (targetIncomingRecord.LogicalName.ToLower().ToString() == "msnfp_receipt")
                        {
                            localContext.TracingService.Trace("Get associated transaction record from the incoming receipt entity.");
                            UpdateReceiptAmountForThisTransaction(targetIncomingRecord.Id, localContext, service, context);
                        }
                        // Else, if it is a transaction we need to get the receipt that is being added (get receipt from transaction):
                        else if (targetIncomingRecord.LogicalName.ToLower().ToString() == "msnfp_transaction")
                        {
                            localContext.TracingService.Trace("Get associated receipt record from the incoming transaction entity.");

                            ColumnSet transCols = new ColumnSet("msnfp_transactionid", "msnfp_amount_receipted", "msnfp_amount_membership", "msnfp_amount_nonreceiptable", "msnfp_amount", "transactioncurrencyid", "msnfp_customerid", "ownerid", "msnfp_taxreceiptid", "msnfp_previousreceiptid");

                            queriedEntityRecord = service.Retrieve("msnfp_transaction", ((EntityReference)targetIncomingRecord).Id, transCols);

                            if (queriedEntityRecord.Contains("msnfp_taxreceiptid"))
                            {
                                localContext.TracingService.Trace("Transaction contains receipt with id: " + ((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id.ToString());
                                UpdateReceiptAmountForThisTransaction(((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id, localContext, service, context);
                            }
                            else
                            {
                                // This can happen on removal of the receipt on the manual donation page:
                                localContext.TracingService.Trace("No receipt found. Attempting to use msnfp_previousreceiptid.");

                                if (queriedEntityRecord.Contains("msnfp_previousreceiptid"))
                                {
                                    localContext.TracingService.Trace("Transaction contains previous receipt with id: " + ((EntityReference)queriedEntityRecord["msnfp_previousreceiptid"]).Id.ToString());
                                    UpdateReceiptAmountForThisTransaction(((EntityReference)queriedEntityRecord["msnfp_previousreceiptid"]).Id, localContext, service, context);
                                }
                                else
                                {
                                    localContext.TracingService.Trace("No msnfp_previousreceiptid found. Exiting workflow.");
                                }
                            }
                        }
                    }
                }
                // This is the update of transaction path. Note that update it is not a reference it is the entity itself:
                else if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("Message Name: " + messageName);
                    // We only do this on associate/disassociate:
                    if (context.MessageName.ToLower() == "update")
                    {
                        localContext.TracingService.Trace("---------Entering UpdateReceiptOnAssociation.cs Main Function---------");

                        // Note that this can be either the receipt or the transaction, so we need to be careful with field updating:
                        transactionRecordOnUpdate = (Entity)context.InputParameters["Target"];
                        localContext.TracingService.Trace("transactionRecordOnUpdate Logical Name: " + transactionRecordOnUpdate.LogicalName.ToString());

                        Guid currentUserID = context.InitiatingUserId;
                        Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

                        if (user == null)
                        {
                            throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
                        }


                        if (transactionRecordOnUpdate.LogicalName.ToLower().ToString() == "msnfp_transaction")
                        {
                            localContext.TracingService.Trace("Get associated receipt record from the incoming transaction entity (transactionRecordOnUpdate).");

                            ColumnSet transCols = new ColumnSet("msnfp_transactionid", "msnfp_amount_receipted", "msnfp_amount_membership", "msnfp_amount_nonreceiptable", "msnfp_amount", "transactioncurrencyid", "msnfp_customerid", "ownerid", "msnfp_taxreceiptid", "msnfp_previousreceiptid");

                            queriedEntityRecord = service.Retrieve("msnfp_transaction", context.PrimaryEntityId, transCols);

                            localContext.TracingService.Trace("Retrieved Transaction.");
                            if (queriedEntityRecord.Contains("msnfp_taxreceiptid"))
                            {
                                localContext.TracingService.Trace("Retrieved receipt.");
                                localContext.TracingService.Trace("Transaction contains receipt with id: " + ((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id.ToString());

                                // If there is a previous receipt and this new one is NOT the same as the old, we need to update BOTH receipts:
                                if (queriedEntityRecord.Contains("msnfp_previousreceiptid"))
                                {
                                    // If they do not equal each other, there are two receipts being modified (the one the transaction is being removed from and the one it is added to):
                                    if (((EntityReference)queriedEntityRecord["msnfp_previousreceiptid"]).Id != ((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id)
                                    {
                                        localContext.TracingService.Trace("Old Receipt and New Receipt are different. Updating both.");
                                        Guid oldReceiptID = ((EntityReference)queriedEntityRecord["msnfp_previousreceiptid"]).Id;
                                        Guid newReceiptID = ((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id;

                                        // Update Both (May need to check saving here):
                                        localContext.TracingService.Trace("Updating Old Receipt ID: " + oldReceiptID);
                                        UpdateReceiptAmountForThisTransaction(oldReceiptID, localContext, service, context);
                                        localContext.TracingService.Trace("Finished Updating Old Receipt. Now Update the New Receipt ID: " + newReceiptID);
                                        UpdateReceiptAmountForThisTransaction(newReceiptID, localContext, service, context);
                                    }
                                    // If they are the same we just update the one as per usual:
                                    else
                                    {
                                        UpdateReceiptAmountForThisTransaction(((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id, localContext, service, context);
                                    }
                                }
                                else
                                {
                                    // Otherwise if there is no previous we just update this as per usual:
                                    UpdateReceiptAmountForThisTransaction(((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id, localContext, service, context);
                                }
                            }
                            else
                            {
                                // This can happen on removal of the receipt on the manual donation page:
                                localContext.TracingService.Trace("No receipt found. Attempting to use msnfp_previousreceiptid.");

                                if (queriedEntityRecord.Contains("msnfp_previousreceiptid"))
                                {
                                    localContext.TracingService.Trace("Transaction contains previous receipt with id: " + ((EntityReference)queriedEntityRecord["msnfp_previousreceiptid"]).Id.ToString());
                                    UpdateReceiptAmountForThisTransaction(((EntityReference)queriedEntityRecord["msnfp_previousreceiptid"]).Id, localContext, service, context);
                                }
                                else
                                {
                                    localContext.TracingService.Trace("No msnfp_previousreceiptid found. Exiting workflow.");
                                }
                            }
                        }
                    }
                }

                localContext.TracingService.Trace("---------Exiting UpdateReceiptOnAssociation.cs---------");
            }
        }

        private void UpdateReceiptAmountForThisTransaction(Guid receiptRecordID, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("Entering UpdateReceiptAmountForThisTransaction()");
            Entity receiptToUpdate;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            // Get the most recent data for this receipt:
            ColumnSet cols = new ColumnSet("msnfp_receiptid", "createdon", "msnfp_customerid", "msnfp_expectedtaxcredit", "msnfp_generatedorprinted", "msnfp_lastdonationdate", "msnfp_amount_nonreceiptable", "msnfp_transactioncount", "msnfp_preferredlanguagecode", "msnfp_receiptnumber", "msnfp_receiptgeneration", "msnfp_receiptissuedate", "msnfp_receiptstackid", "msnfp_receiptstatus", "msnfp_amount_receipted", "msnfp_paymentscheduleid", "msnfp_replacesreceiptid", "msnfp_identifier", "msnfp_amount");

            receiptToUpdate = service.Retrieve("msnfp_receipt", receiptRecordID, cols);


            // First, we wipe out the amounts as we are starting from scratch:
            localContext.TracingService.Trace("Old msnfp_amount_receipted: " + ((Money)receiptToUpdate["msnfp_amount_receipted"]).Value);
            receiptToUpdate["msnfp_amount_receipted"] = new Money(0);

            localContext.TracingService.Trace("Old msnfp_amount_nonreceiptable: " + ((Money)receiptToUpdate["msnfp_amount_nonreceiptable"]).Value);
            receiptToUpdate["msnfp_amount_nonreceiptable"] = new Money(0);

            localContext.TracingService.Trace("Old msnfp_amount: " + ((Money)receiptToUpdate["msnfp_amount"]).Value);
            receiptToUpdate["msnfp_amount"] = new Money(0);

            receiptToUpdate["msnfp_transactioncount"] = 0;

            // Get all associated transaction related to this receipt:
            var allRelatedTransactions = (from t in orgSvcContext.CreateQuery("msnfp_transaction")
                                          where ((EntityReference)t["msnfp_taxreceiptid"]).Id == receiptRecordID
                                          select t).ToList();


            // For each related transaction:
            foreach (Entity transactionRecord in allRelatedTransactions)
            {
                // Get its data and add it to the receipt:
                decimal amount = decimal.Zero;
                decimal amountReceipted = decimal.Zero;
                decimal amountMembership = decimal.Zero;
                decimal amountnonreceiptable = decimal.Zero;

                localContext.TracingService.Trace("------------------");
                localContext.TracingService.Trace("Processing Transaction ID: " + ((Guid)transactionRecord["msnfp_transactionid"]).ToString());

                amount = transactionRecord.Contains("msnfp_amount") ? ((Money)transactionRecord["msnfp_amount"]).Value : decimal.Zero;
                amountReceipted = transactionRecord.Contains("msnfp_amount_receipted") ? ((Money)transactionRecord["msnfp_amount_receipted"]).Value : decimal.Zero;
                amountMembership = transactionRecord.Contains("msnfp_amount_membership") ? ((Money)transactionRecord["msnfp_amount_membership"]).Value : decimal.Zero;
                amountnonreceiptable = transactionRecord.Contains("msnfp_amount_nonreceiptable") ? ((Money)transactionRecord["msnfp_amount_nonreceiptable"]).Value : decimal.Zero;

                localContext.TracingService.Trace("Got membership amount and non-receiptable amount.");

                localContext.TracingService.Trace("Amount Receipted " + amountReceipted.ToString() + " Membership Amount: " + amountMembership.ToString() + " Non-receiptable : " + amountnonreceiptable.ToString());

                localContext.TracingService.Trace("Old msnfp_amount_receipted: " + ((Money)receiptToUpdate["msnfp_amount_receipted"]).Value);

                // Note here we need to save the amounts as new Money:
                receiptToUpdate["msnfp_amount_receipted"] = new Money(((Money)receiptToUpdate["msnfp_amount_receipted"]).Value + new Money(amountReceipted).Value);
                localContext.TracingService.Trace("New msnfp_amount_receipted: " + ((Money)receiptToUpdate["msnfp_amount_receipted"]).Value);

                localContext.TracingService.Trace("Old msnfp_amount_nonreceiptable: " + ((Money)receiptToUpdate["msnfp_amount_nonreceiptable"]).Value);
                receiptToUpdate["msnfp_amount_nonreceiptable"] = new Money(((Money)receiptToUpdate["msnfp_amount_nonreceiptable"]).Value + (new Money(amountMembership + amountnonreceiptable)).Value);
                localContext.TracingService.Trace("New msnfp_amount_nonreceiptable: " + ((Money)receiptToUpdate["msnfp_amount_nonreceiptable"]).Value);

                localContext.TracingService.Trace("Old msnfp_amount: " + ((Money)receiptToUpdate["msnfp_amount"]).Value);
                receiptToUpdate["msnfp_amount"] = new Money(((Money)receiptToUpdate["msnfp_amount"]).Value + (new Money(amount)).Value);
                localContext.TracingService.Trace("New msnfp_amount: " + ((Money)receiptToUpdate["msnfp_amount"]).Value);


                receiptToUpdate["msnfp_generatedorprinted"] = Convert.ToDouble(1);
                receiptToUpdate["msnfp_receiptgeneration"] = new OptionSetValue(844060000);//System Generated
                receiptToUpdate["msnfp_receiptissuedate"] = DateTime.Now;

                localContext.TracingService.Trace("Getting transaction count.");

                localContext.TracingService.Trace("Old msnfp_transactioncount: " + (int)receiptToUpdate["msnfp_transactioncount"]);
                receiptToUpdate["msnfp_transactioncount"] = ((int)receiptToUpdate["msnfp_transactioncount"]) + 1;
                localContext.TracingService.Trace("New msnfp_transactioncount: " + (int)receiptToUpdate["msnfp_transactioncount"]);


                if (transactionRecord.Contains("transactioncurrencyid"))
                    receiptToUpdate["transactioncurrencyid"] = new EntityReference("transactioncurrency", ((EntityReference)transactionRecord["transactioncurrencyid"]).Id);

                if (transactionRecord.Contains("msnfp_customerid"))
                {
                    string customerType = ((EntityReference)transactionRecord["msnfp_customerid"]).LogicalName;
                    Guid customerId = ((EntityReference)transactionRecord["msnfp_customerid"]).Id;

                    receiptToUpdate["msnfp_customerid"] = new EntityReference(customerType, customerId);
                }

                string ownerType = ((EntityReference)transactionRecord["ownerid"]).LogicalName;
                Guid ownerID = ((EntityReference)transactionRecord["ownerid"]).Id;

                receiptToUpdate["ownerid"] = new EntityReference(ownerType, ownerID);
                receiptToUpdate["statuscode"] = new OptionSetValue(1);//Issued

                // We assign the receipt to the old record (if there is a value):
                if (transactionRecord.Contains("msnfp_taxreceiptid"))
                {
                    localContext.TracingService.Trace("Replace old receipt with this one.");
                    if (transactionRecord.Contains("msnfp_previousreceiptid"))
                    {
                        localContext.TracingService.Trace("Old Previous Receipt ID: " + ((EntityReference)transactionRecord["msnfp_previousreceiptid"]).Id.ToString());
                    }
                    transactionRecord["msnfp_previousreceiptid"] = new EntityReference("msnfp_receipt", ((EntityReference)transactionRecord["msnfp_taxreceiptid"]).Id);
                    localContext.TracingService.Trace("Updated Previous Receipt ID: " + ((EntityReference)transactionRecord["msnfp_previousreceiptid"]).Id.ToString());

                    localContext.TracingService.Trace("Saving Transaction.");

                    if (!orgSvcContext.IsAttached(transactionRecord))
                    {
                        orgSvcContext.Attach(transactionRecord);
                    }

                    orgSvcContext.UpdateObject(transactionRecord);
                    orgSvcContext.SaveChanges();

                    //service.Update(transactionRecord);
                    localContext.TracingService.Trace("Transaction Updated.");
                }

                localContext.TracingService.Trace("------------------");
            }

            // Now save and exit:
            localContext.TracingService.Trace("Saving Receipt.");
            service.Update(receiptToUpdate);

            localContext.TracingService.Trace("Receipt updated.");

            localContext.TracingService.Trace("Exiting UpdateReceiptAmountForThisTransaction()");
        }
    }
}
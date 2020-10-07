/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using Plugins.PaymentProcesses;
using Microsoft.Xrm.Sdk.Query;
using Moneris;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using FundraisingandEngagement.StripeWebPayment.Model;
using System.Text.RegularExpressions;
using FundraisingandEngagement.StripeWebPayment.Service;
using FundraisingandEngagement.StripeIntegration.Helpers;
using Plugins.AzureModels;
using System.Xml;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;
using System.Runtime.CompilerServices;
using Plugins.Common;
using Utilities = Plugins.PaymentProcesses.Utilities;

namespace Plugins
{
    public class TransactionGiftCreate : PluginBase
    {
        public static Guid ContactGivingLevelWorkflowId = Guid.Parse("EAAE076C-DB57-4979-A479-CC17B83CE705");

        public static Guid AccountGivingLevelWorkflowId = Guid.Parse("810C634A-2F4C-45B7-BFCC-C4FAAE315970");

        public TransactionGiftCreate(string unsecure, string secure)
            : base(typeof(TransactionGiftCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered TransactionGiftCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;
            string messageName = context.MessageName;

            if (context.Depth > 1 && !CheckExecutionPipeLine(context))
            {
                localContext.TracingService.Trace("Context.depth = " + context.Depth + ". Exiting Plugin.");

                var curContext = context;
                while (curContext != null)
                {
                    localContext.TracingService.Trace(curContext.PrimaryEntityName + ", " + curContext.Depth);
                    curContext = curContext.ParentContext;
                }

                return;
            }
            else
            {
                localContext.TracingService.Trace("Context.depth = " + context.Depth);
            }

            // The retrieved transaction
            Entity giftTransaction;
            // The target transaction. Note target is used in Azure sync.
            Entity targetTransaction = null;

            Entity configurationRecord = null;
            configurationRecord = Utilities.GetConfigurationRecordByMessageName(context, service, localContext.TracingService);

            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));
            Utilities util = new Utilities();

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            if (context.InputParameters.Contains("Target"))
            {
                localContext.TracingService.Trace("---------Entering TransactionGiftCreate.cs Main Function---------");
                localContext.TracingService.Trace("Message Name: " + messageName);

                if (context.InputParameters["Target"] is Entity)
                {
                    targetTransaction = (Entity)context.InputParameters["Target"];
                    Guid ownerId = Guid.Empty;

                    ColumnSet cols = ReturnTransactionColumnSet();
                    giftTransaction = service.Retrieve("msnfp_transaction", targetTransaction.Id, cols);

                    if (giftTransaction == null)
                    {
                        throw new ArgumentNullException("msnfp_transactionid");
                    }


                    if (giftTransaction.Contains("msnfp_name") && giftTransaction.Contains("statuscode"))
                    {
                        localContext.TracingService.Trace("Transaction identifier (msnfp_name): " + (string)giftTransaction["msnfp_name"]);
                        localContext.TracingService.Trace("Transaction status reason (statuscode): " + ((OptionSetValue)giftTransaction["statuscode"]).Value.ToString());
                    }

                    #region Pre-Payment Operations - Setting Deposit Date, Owner from Donation/Campaign page (if applicable)
                    if (messageName == "Create")
                    {
                        // Set the deposit date if null:
                        if (!targetTransaction.Contains("msnfp_depositdate") || targetTransaction.GetAttributeValue<DateTime?>("msnfp_depositdate") == null)
                        {
                            localContext.TracingService.Trace("contains msnfp_depositdate");
                            Entity recordToUpdate = new Entity(targetTransaction.LogicalName, targetTransaction.Id);
                            // for azure sync
                            targetTransaction["msnfp_depositdate"] = targetTransaction["msnfp_receiveddate"]
                            = recordToUpdate["msnfp_depositdate"] = recordToUpdate["msnfp_receiveddate"] = DateTime.Now;
                            service.Update(recordToUpdate);
                        }

                        //// Get the ownerid from the owning business unit for donation page records imported donation page:
                        //if (giftTransaction.Contains("msnfp_donationpageid") || giftTransaction.Contains("msnfp_campaignpageid"))
                        //{
                        //    // If it is a campaign page donation, get that team owner:
                        //    if (giftTransaction.Contains("msnfp_campaignpageid") && giftTransaction["msnfp_campaignpageid"] != null)
                        //    {
                        //        localContext.TracingService.Trace("Updating transaction ownerid based on the campaign page 'Set Owning Team' field.");

                        //        // Query for the field:
                        //        ColumnSet campaignpagecols;
                        //        campaignpagecols = new ColumnSet("msnfp_campaignpageid", "msnfp_teamownerid");

                        //        Entity campaignPageRecord = service.Retrieve("msnfp_campaignpage", ((EntityReference)giftTransaction["msnfp_campaignpageid"]).Id, campaignpagecols);
                        //        localContext.TracingService.Trace("Owner id: " + ((EntityReference)campaignPageRecord["msnfp_teamownerid"]).Id.ToString());

                        //        // Attempt to set the owner id:
                        //        giftTransaction["ownerid"] = new EntityReference("team", ((EntityReference)campaignPageRecord["msnfp_teamownerid"]).Id);
                        //        ownerId = ((EntityReference)campaignPageRecord["msnfp_teamownerid"]).Id;
                        //    }
                        //    // If it is a donation page donation, get that team owner:
                        //    else if (giftTransaction.Contains("msnfp_donationpageid") && giftTransaction["msnfp_donationpageid"] != null)
                        //    {
                        //        localContext.TracingService.Trace("Updating transaction ownerid based on the donation page 'Set Owning Team' field.");

                        //        // Query for the field:
                        //        ColumnSet donationpagecols;
                        //        donationpagecols = new ColumnSet("msnfp_donationpageid", "msnfp_teamownerid");

                        //        Entity donationPageRecord = service.Retrieve("msnfp_donationpage", ((EntityReference)giftTransaction["msnfp_donationpageid"]).Id, donationpagecols);
                        //        localContext.TracingService.Trace("Owner id: " + ((EntityReference)donationPageRecord["msnfp_teamownerid"]).Id.ToString());

                        //        // Attempt to set the owner id:
                        //        giftTransaction["ownerid"] = new EntityReference("team", ((EntityReference)donationPageRecord["msnfp_teamownerid"]).Id);
                        //        ownerId = ((EntityReference)donationPageRecord["msnfp_teamownerid"]).Id;
                        //    }

                        //    try
                        //    {
                        //        // Updating transaction record
                        //        service.Update(giftTransaction);

                        //        // Retrieving newly updated transaction record
                        //        giftTransaction = service.Retrieve("msnfp_transaction", giftTransaction.Id, cols);
                        //    }
                        //    catch (Exception ownerupdateError)
                        //    {
                        //        localContext.TracingService.Trace("Failed to update the ownerid field: " + ownerupdateError.Message.ToString());
                        //    }
                        //}
                        //else
                        //{
                        //    localContext.TracingService.Trace("Not a newly created donation/campaign page transaction. Skipping owning team assignment.");
                        //}


                    }
                    #endregion


                    #region Payment Processing (if applicable). This section is where Moneris/Stripe processing occurs.
                    // Get the payment method:
                    Entity paymentMethod;
                    if (giftTransaction.Contains("msnfp_transaction_paymentmethodid"))
                    {
                        paymentMethod = service.Retrieve("msnfp_paymentmethod", ((EntityReference)giftTransaction["msnfp_transaction_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode", "msnfp_customerid" }));

                        if (paymentMethod != null)
                        {
                            localContext.TracingService.Trace("Obtained payment method for this transaction.");
                            Entity paymentProcessor = null;
                            if (paymentMethod.Contains("msnfp_paymentprocessorid"))
                            {
                                localContext.TracingService.Trace("Getting payment processor for transaction.");
                                paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)paymentMethod["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_paymentgatewaytype" }));

                                if (paymentProcessor.Contains("msnfp_paymentgatewaytype"))
                                {
                                    localContext.TracingService.Trace("Obtained payment gateway for this transaction.");
                                    // If it is complete, not processed already and has a payment method attached. This is the default entry for Create messages:     
                                    if (giftTransaction.Contains("msnfp_chargeoncreate") && !giftTransaction.Contains("msnfp_transactionidentifier") && messageName == "Create")
                                    {
                                        // Only charge on create if set to true and status == completed and message == create. If it is not create, the update method will not work:
                                        if ((bool)giftTransaction["msnfp_chargeoncreate"] && (((OptionSetValue)giftTransaction["statuscode"]).Value == 844060000))
                                        {
                                            // Moneris:
                                            if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                            {
                                                // If it is a reusable payment and the transaction has a parent, use the vault:
                                                if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                {
                                                    processMonerisVaultTransaction(giftTransaction, localContext, service);
                                                }
                                                // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                else
                                                {
                                                    processMonerisOneTimeTransaction(giftTransaction, localContext, service);
                                                }
                                            }
                                            // Stripe:
                                            else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                            {
                                                // If it is a reusable payment and the transaction has a parent:
                                                if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                {
                                                    processStripeTransaction(configurationRecord, giftTransaction, localContext, service, false);
                                                }
                                                // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                else
                                                {
                                                    processStripeTransaction(configurationRecord, giftTransaction, localContext, service, true);
                                                }
                                            }
                                            // iATS:
                                            else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                            {
                                                // If it is a reusable payment and the transaction has a parent:
                                                if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                {
                                                    localContext.TracingService.Trace("iATS Proccess. Reusable=True");
                                                    ProcessiATSTransaction(configurationRecord, giftTransaction, localContext, service, false);
                                                }
                                                // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                else
                                                {
                                                    localContext.TracingService.Trace("iATS Proccess. Reusable=false");
                                                    ProcessiATSTransaction(configurationRecord, giftTransaction, localContext, service, true);
                                                }
                                            }
                                            else
                                            {
                                                localContext.TracingService.Trace("((OptionSetValue)paymentProcessor[msnfp_paymentgatewaytype]).Value" + ((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value.ToString());
                                            }
                                        }
                                        else
                                        {
                                            if (giftTransaction.Contains("msnfp_chargeoncreate"))
                                            {
                                                localContext.TracingService.Trace("msnfp_chargeoncreate = " + (bool)giftTransaction["msnfp_chargeoncreate"]);
                                            }
                                        }
                                    }
                                    // When updating a record that failed/declined previously, we check for the msnfp_transactionresult:
                                    else if (!string.IsNullOrEmpty(giftTransaction.GetAttributeValue<string>("msnfp_transactionresult")))
                                    {
                                        localContext.TracingService.Trace("Transaction Result = " + (string)giftTransaction["msnfp_transactionresult"]);
                                        localContext.TracingService.Trace("Status Code = " + ((OptionSetValue)giftTransaction["statuscode"]).Value.ToString());
                                        // Only if status == Completed (it is changed when "Process" is clicked) and message == update and transaction had the value previously of Failed:
                                        if (giftTransaction.GetAttributeValue<OptionSetValue>("statuscode").Value == 844060000
                                                && messageName.ToLower() == "update"
                                                && (giftTransaction.GetAttributeValue<string>("msnfp_transactionresult").ToLower().Contains("failed")
                                                    || giftTransaction.GetAttributeValue<string>("msnfp_transactionresult").ToLower().Contains("declined")
                                                        || giftTransaction.GetAttributeValue<string>("msnfp_transactionresult").ToLower().Contains("reject")))
                                        {
                                            localContext.TracingService.Trace("Attempting to retry failed transaction");
                                            // Moneris:
                                            if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                            {
                                                // If it is a reusable payment and the transaction has a parent, use the vault:
                                                if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                {
                                                    processMonerisVaultTransaction(giftTransaction, localContext, service);
                                                }
                                                // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                else
                                                {
                                                    processMonerisOneTimeTransaction(giftTransaction, localContext, service);
                                                }
                                            }
                                            // Stripe:
                                            else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                            {
                                                // If it is a reusable payment and the transaction has a parent:
                                                if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                {
                                                    processStripeTransaction(configurationRecord, giftTransaction, localContext, service, false);
                                                }
                                                // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                else
                                                {
                                                    processStripeTransaction(configurationRecord, giftTransaction, localContext, service, true);
                                                }
                                            }
                                            // iATS:
                                            else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                            {
                                                // If it is a reusable payment and the transaction has a parent:
                                                if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                {
                                                    localContext.TracingService.Trace("iATS Proccess. Reusable=True");
                                                    ProcessiATSTransaction(configurationRecord, giftTransaction, localContext, service, false);
                                                }
                                                // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                else
                                                {
                                                    localContext.TracingService.Trace("iATS Proccess. Reusable=false");
                                                    ProcessiATSTransaction(configurationRecord, giftTransaction, localContext, service, true);
                                                }
                                            }
                                            else
                                            {
                                                localContext.TracingService.Trace("((OptionSetValue)paymentProcessor[msnfp_paymentgatewaytype]).Value" + ((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value.ToString());
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (giftTransaction.Contains("msnfp_chargeoncreate"))
                                        {
                                            localContext.TracingService.Trace("msnfp_chargeoncreate = " + (bool)giftTransaction["msnfp_chargeoncreate"]);
                                        }
                                        if (giftTransaction.Contains("msnfp_transactionidentifier"))
                                        {
                                            localContext.TracingService.Trace("msnfp_transactionidentifier = " + (string)giftTransaction["msnfp_transactionidentifier"]);
                                        }
                                        localContext.TracingService.Trace("No payment processed.");
                                    }
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("There is no payment processor. No payment processed.");
                            }
                        }
                    }
                    #endregion


                    #region Post Payment Opertations - Populate donor field (if applicable), updating donor commitment balance, create designation credit, update event totals, updating receipts, pledge matching, etc.
                    if (messageName == "Create")
                    {
                        PopultateDonorFieldsOnTransactionAndPaymentMethod(ref giftTransaction, ownerId, localContext, service);

                        // Updating donor commitment balance:
                        if (targetTransaction.Contains("msnfp_donorcommitmentid"))
                        {
                            util.UpdateDonorCommitmentBalance(orgSvcContext, service, ((EntityReference)targetTransaction["msnfp_donorcommitmentid"]), 0);
                        }

                        // Adding Designated Credit for Primary Designation
                        CreateDesignatedCredit(localContext, service, targetTransaction);

                        // Add Tribute record, if needed
                        Guid? tributeId = CreateTributeRecord(localContext, targetTransaction, service);
                        if (tributeId.HasValue)
                        {
                            // a tribute record was created, update the copy of the transaction used for syncing to azure
                            targetTransaction["msnfp_tributeid"] = new EntityReference("msnfp_tributeormemory", tributeId.Value);
                        }

                        // Updating Event Package Totals
                        UpdateEventPackageDonationTotals(targetTransaction, orgSvcContext, service, localContext);

                        // Updating Event Totals
                        UpdateEventTotals(targetTransaction, orgSvcContext, service, localContext);

                        ReceiptUtilities.UpdateReceipt(context, service, localContext.TracingService);

                        #region Create Auto Soft Credits

                        CreateAutoSoftCredits(targetTransaction, configurationRecord, service, localContext.TracingService);

                        #endregion

                        AddOrUpdateThisRecordWithAzure(targetTransaction, configurationRecord, localContext, service, context);

                        // See if there is a pledge match for this donor:
                        if (giftTransaction.Contains("msnfp_customerid"))
                        {
                            localContext.TracingService.Trace("See if this donor has any pledge matches.");
                            // Here we get all pledge matches for this customer:
                            List<Entity> pledgeMatchingList = (from c in orgSvcContext.CreateQuery("msnfp_pledgematch")
                                                               where ((EntityReference)c["msnfp_customerfromid"]).Id == ((EntityReference)giftTransaction["msnfp_customerid"]).Id
                                                               select c).ToList();

                            localContext.TracingService.Trace("Pledge Matches found: " + pledgeMatchingList.Count.ToString());
                            // If there is any, we generate another donor commitment for the "Apply the pledge to" lookup and make the donor of this record the constituent on the pledge:
                            if (pledgeMatchingList != null)
                            {
                                foreach (Entity pledgeMatchRecord in pledgeMatchingList)
                                {
                                    localContext.TracingService.Trace("Pledge Match ID to process next: " + pledgeMatchRecord.Id.ToString());
                                    // Add the pledge match for this pledge match record:
                                    AddPledgeFromPledgeMatchRecord(giftTransaction, pledgeMatchRecord, localContext, service, context);
                                }
                            }
                        }
                    }
                    else if (messageName == "Update")
                    {
                        if (!giftTransaction.Contains("msnfp_depositdate") || giftTransaction.GetAttributeValue<DateTime?>("msnfp_depositdate") == null)
                        {
                            DateTime depositDate = DateTime.Now;
                            // for azure sync
                            giftTransaction["msnfp_depositdate"] = depositDate;

                            // update the record
                            localContext.TracingService.Trace("contains depositdate 2");
                            Entity recordToUpdate = new Entity(targetTransaction.LogicalName, targetTransaction.Id);
                            recordToUpdate["msnfp_depositdate"] = depositDate;
                            service.Update(recordToUpdate);
                        }

                        // Updating Event Package Totals
                        UpdateEventPackageDonationTotals(giftTransaction, orgSvcContext, service, localContext);

                        // Updating Event Totals
                        UpdateEventTotals(giftTransaction, orgSvcContext, service, localContext);

                        ReceiptUtilities.UpdateReceipt(context, service, localContext.TracingService);
                        AddOrUpdateThisRecordWithAzure(giftTransaction, configurationRecord, localContext, service, context);
                        // Update the gift's receipt (if applicable)
                        localContext.TracingService.Trace("Update the transaction receipt (if appicable).");
                        UpdateTransactionReceiptStatus(giftTransaction.Id, localContext, service);
                    }

                    // Transaction is completed
                    if (giftTransaction.Attributes.ContainsKey("statuscode") && giftTransaction.GetAttributeValue<OptionSetValue>("statuscode").Value == 844060000 && giftTransaction.Attributes.ContainsKey("msnfp_customerid"))
                    {
                        localContext.TracingService.Trace("Transaction completed - initiate Giving level logic..");
                        ProcessGivingLevelInstance(service, giftTransaction.GetAttributeValue<EntityReference>("msnfp_customerid"), context, localContext.TracingService);
                    }

                    // Now we see if this has a related pledge schedule that needs to be updated to completed (once all pledges are completed):
                    if (giftTransaction.Contains("msnfp_transaction_paymentscheduleid"))
                    {
                        // If so, we check and see if that pledge schedule (check msnfp_scheduletypecode for value 844060003) has anymore related pledges not fulfilled. 
                        // If not, it is set to completed status:
                        AttemptToSetPledgeScheduleToCompleted(((EntityReference)giftTransaction["msnfp_transaction_paymentscheduleid"]).Id, ((OptionSetValue)giftTransaction["statuscode"]).Value, localContext, service, orgSvcContext);


                        // Also on create ensure that the parent payment schedule is set to the correct card type:
                        if (messageName == "Create")
                        {
                            SetParentPaymentScheduleCardTypeToChilds(giftTransaction, service, orgSvcContext, localContext);
                        }
                    }


                    // Modify Last Donation and Last Donation Date fields
                    if (giftTransaction != null && giftTransaction.Contains("msnfp_customerid") && giftTransaction["msnfp_customerid"] != null)
                    {
                        PopulateMostRecentGiftDataToDonor(service, orgSvcContext, localContext, giftTransaction, messageName);
                    }

                    // Modify the primary membership on the customer (if applicable): 
                    if (giftTransaction.Contains("msnfp_membershipinstanceid"))
                    {
                        UpdateCustomerPrimaryMembership(giftTransaction, localContext, service, orgSvcContext);
                    }

                    //SetYearlySummaryAmountsToCustomer(service, orgSvcContext, localContext, targetTransaction.Id);

                    #endregion

                    Utilities.UpdateHouseholdOnRecord(service, giftTransaction, "msnfp_householdid", "msnfp_customerid");
                }

                // Delete if the message is delete:
                if (messageName == "Delete")
                {
                    ReceiptUtilities.UpdateReceipt(context, service, localContext.TracingService);

                    ColumnSet cols = ReturnTransactionColumnSet();
                    targetTransaction = service.Retrieve("msnfp_transaction", ((EntityReference)context.InputParameters["Target"]).Id, cols);
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(targetTransaction, configurationRecord, localContext, service, context);

                    // Modify Last Donation and Last Donation Date fields
                    if (targetTransaction != null && targetTransaction.Contains("msnfp_customerid") && targetTransaction["msnfp_customerid"] != null)
                    {
                        PopulateMostRecentGiftDataToDonor(service, orgSvcContext, localContext, targetTransaction, messageName);
                    }
                }

                if (targetTransaction != null)
                {
                    _ = Common.Utilities.CallYearlyGivingServiceAsync(targetTransaction.Id,
                        targetTransaction.LogicalName, configurationRecord.Id, service, localContext.TracingService);
                }


                localContext.TracingService.Trace("---------Exiting TransactionGiftCreate.cs---------");
            }
        }

        private void CreateAutoSoftCredits(Entity targetTransaction, Entity config, IOrganizationService service, ITracingService tracingService)
        {
            // only proceed if the current Transaction is of type Donation and if the AutoCreateSoftCredit option is set to true in the config record.
            bool autoSoftCreditConfigValue = service
                .Retrieve(config.LogicalName, config.Id, new ColumnSet("msnfp_autocreate_softcredit"))
                .GetAttributeValue<bool>("msnfp_autocreate_softcredit");
            int transactionTypeCode = targetTransaction.GetAttributeValue<OptionSetValue>("msnfp_typecode").Value;

            if (autoSoftCreditConfigValue == true && transactionTypeCode == 844060000)
            {
                ColumnSet columnsToCopy = ReturnTransactionColumnSet();
                if (targetTransaction.GetAttributeValue<EntityReference>("msnfp_relatedconstituentid") != null)
                {
                    Entity softCreditWithConstituent = AutoSoftCredit.CreateSoftCredit(targetTransaction, columnsToCopy,
                        targetTransaction.GetAttributeValue<EntityReference>("msnfp_relatedconstituentid"), service, tracingService);
                    service.Create(softCreditWithConstituent);
                }

                if (targetTransaction.GetAttributeValue<EntityReference>("msnfp_solicitorid") != null)
                {
                    Entity softCreditWithSolicitor = AutoSoftCredit.CreateSoftCredit(targetTransaction, columnsToCopy,
                        targetTransaction.GetAttributeValue<EntityReference>("msnfp_solicitorid"), service, tracingService);
                    service.Create(softCreditWithSolicitor);
                }
            }
        }


        private bool CheckExecutionPipeLine(IPluginExecutionContext context)
        {
            // Depth will be > 1
            // when donation import creates or updates transaction, or a soft credit is created.

            bool parentContextNotNull = context.ParentContext != null;
            bool donationImportClause = (context.ParentContext.PrimaryEntityName == "msnfp_donationimport") || (context.ParentContext.ParentContext != null && context.ParentContext.ParentContext.PrimaryEntityName == "msnfp_donationimport");
            bool transactionClause = string.Compare(context.MessageName, "create", StringComparison.CurrentCultureIgnoreCase) == 0 && context.ParentContext.PrimaryEntityName == "msnfp_transaction";
            bool refundClause = context.ParentContext.ParentContext != null && context.ParentContext.ParentContext.PrimaryEntityName == "msnfp_refund";
            return parentContextNotNull & (donationImportClause || transactionClause || refundClause);
        }

        private ColumnSet ReturnTransactionColumnSet()
        {
            ColumnSet transactionCols;
            transactionCols = new ColumnSet("msnfp_transactionid", "msnfp_name", "msnfp_transaction_paymentmethodid", "msnfp_amount", "statuscode", "msnfp_chargeoncreate", "msnfp_customerid", "msnfp_transactionidentifier", "msnfp_transactionnumber", "msnfp_paymenttypecode", "msnfp_firstname", "msnfp_lastname", "msnfp_billing_line1", "msnfp_billing_line2", "msnfp_billing_line3", "msnfp_billing_city", "msnfp_billing_stateorprovince", "msnfp_billing_country", "msnfp_billing_postalcode", "msnfp_chequenumber", "msnfp_chequewiredate", "msnfp_transactionresult", "msnfp_relatedconstituentid", "msnfp_transactionfraudcode", "msnfp_amount_receipted", "msnfp_bookdate", "msnfp_daterefunded", "msnfp_originatingcampaignid", "msnfp_appealid", "msnfp_anonymous", "msnfp_dataentrysource", "msnfp_telephone1", "msnfp_telephone2", "msnfp_emailaddress1", "msnfp_organizationname", "msnfp_transactiondescription", "msnfp_appraiser", "msnfp_transaction_paymentscheduleid", "msnfp_packageid", "msnfp_invoiceidentifier", "msnfp_configurationid", "msnfp_eventid", "msnfp_eventpackageid", "msnfp_amount_membership", "msnfp_ref_amount_membership", "msnfp_amount_transfer", "msnfp_currentretry", "msnfp_nextfailedretry", "msnfp_receiveddate", "msnfp_lastfailedretry", "msnfp_validationdate", "msnfp_validationperformed", "msnfp_amount", "statuscode", "statecode", "msnfp_amount_nonreceiptable", "msnfp_amount_tax", "msnfp_ref_amount_receipted", "msnfp_ref_amount", "msnfp_ref_amount_nonreceiptable", "msnfp_ref_amount_tax", "msnfp_ga_amount_claimed", "msnfp_ccbrandcode", "msnfp_ga_deliverycode", "msnfp_ga_returnid", "msnfp_ga_applicablecode", "msnfp_giftbatchid", "msnfp_membershipinstanceid", "msnfp_membershipcategoryid", "msnfp_mobilephone", "msnfp_taxreceiptid", "msnfp_receiptpreferencecode", "msnfp_donorcommitmentid", "msnfp_returneddate", "msnfp_thirdpartyreceipt", "msnfp_dataentryreference", "msnfp_tributecode", "msnfp_tributeacknowledgement", "msnfp_tributeid", "msnfp_tributemessage", "createdon", "msnfp_paymentprocessorid", "transactioncurrencyid", "msnfp_depositdate", "owningbusinessunit", "msnfp_designationid", "msnfp_typecode");
            return transactionCols;
        }

        /// <summary>
        /// Process giving level instances due to the current transaction
        /// </summary>
        /// <param name="service">Service</param>
        /// <param name="postImageEntity"></param>
        /// <param name="context"></param>
        private void ProcessGivingLevelInstance(IOrganizationService service, EntityReference customerRef, IPluginExecutionContext context, ITracingService tracingService)
        {
            Entity configuration = Utilities.GetConfigurationRecordByUser(context, service, tracingService);

            // Check for giving level calculation
            if (configuration != null && configuration.Attributes.ContainsKey("msnfp_givinglevelcalculation"))
            {
                int givingLevelCalculation = configuration.GetAttributeValue<OptionSetValue>("msnfp_givinglevelcalculation").Value;

                DateTime? startdate = null;
                DateTime? endDate = null;
                if (givingLevelCalculation == (int)Utilities.GivingLevelCalculation.CurrentCalendar)
                {
                    startdate = new DateTime(DateTime.UtcNow.Year, 1, 1);

                    endDate = new DateTime(DateTime.UtcNow.Year, 12, 31);
                }
                else if (givingLevelCalculation == (int)Utilities.GivingLevelCalculation.CurrentFiscalYear)
                {
                    startdate = new DateTime(DateTime.UtcNow.Year - 1, 3, 15);
                    endDate = new DateTime(DateTime.UtcNow.Year, 3, 14);
                }
                else if (givingLevelCalculation == (int)Utilities.GivingLevelCalculation.YearToDate)
                {
                    startdate = new DateTime(DateTime.UtcNow.Year, 1, 1);
                    endDate = DateTime.UtcNow.Date;
                }

                tracingService.Trace($"Fetching giving instance for : contact {customerRef.Id}");

                FilterExpression filterExpression = new FilterExpression();
                filterExpression.AddCondition(new ConditionExpression("msnfp_customerid", ConditionOperator.Equal, customerRef.Id));
                filterExpression.AddCondition(new ConditionExpression("msnfp_primary", ConditionOperator.Equal, true));

                if (startdate.HasValue && endDate.HasValue)
                {
                    filterExpression.AddCondition(new ConditionExpression("createdon", ConditionOperator.OnOrAfter, startdate.Value));
                    filterExpression.AddCondition(new ConditionExpression("createdon", ConditionOperator.OnOrBefore, endDate.Value));
                }

                IEnumerable<Entity> givingLevelInstanceList = service.RetrieveMultiple(new QueryExpression("msnfp_givinglevelinstance")
                {
                    NoLock = true,
                    ColumnSet = new ColumnSet("msnfp_givinglevelinstanceid"),
                    Criteria = filterExpression
                }).Entities.AsEnumerable();

                tracingService.Trace($"RetrieveMultiple giving instance count {givingLevelInstanceList.Count()}");


                List<OrganizationRequest> organizationRequests = new List<OrganizationRequest>();
                organizationRequests.AddRange(givingLevelInstanceList.Select(g => new UpdateRequest
                {
                    Target = new Entity
                    {
                        LogicalName = g.LogicalName,
                        Id = g.Id,
                        Attributes =
                        {
                            new KeyValuePair<string, object>("msnfp_primary",false)
                        }
                    }
                }));

                // Call workflow
                ExecuteWorkflowRequest executeWorkflowRequest = new ExecuteWorkflowRequest();
                executeWorkflowRequest.EntityId = customerRef.Id;

                if (string.Equals(customerRef.LogicalName, "contact", StringComparison.OrdinalIgnoreCase))
                {
                    executeWorkflowRequest.WorkflowId = ContactGivingLevelWorkflowId;
                }
                else
                {
                    executeWorkflowRequest.WorkflowId = AccountGivingLevelWorkflowId;
                }

                ExecuteMultipleRequest executeMultipleRequest = new ExecuteMultipleRequest()
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = false,
                        ReturnResponses = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                organizationRequests.Add(executeWorkflowRequest);

                // Batch requests and execute
                while (organizationRequests.Any())
                {
                    IEnumerable<OrganizationRequest> subSet = organizationRequests.Take(25);
                    organizationRequests = organizationRequests.Skip(25).ToList();
                    executeMultipleRequest.Requests.AddRange(subSet);

                    ExecuteMultipleResponse executeMultipleResponse = (ExecuteMultipleResponse)service.Execute(executeMultipleRequest);

                    executeMultipleRequest.Requests = new OrganizationRequestCollection();

                    if (executeMultipleResponse.IsFaulted)
                    {
                        tracingService.Trace($"An error has occurred : {executeMultipleResponse.Responses.FirstOrDefault(w => w.Fault != null).Fault.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// This function will set the customer id if it is not already set by preforming a query for them based on the various inputted fields (email then firstname + lastname + address, etc). This only happens if the customer if field is not set. It will also assign this found value to the associated payment method if there is one found for the transaction.
        /// </summary>
        /// <param name="giftTransaction">The transaction to use for the querying and assigning of the customer. This will modify the entered transaction record itself.</param>
        /// <param name="ownerId">The owner for this transaction (previously found on the parent campaign/donation page if applicable)</param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        private void PopultateDonorFieldsOnTransactionAndPaymentMethod(ref Entity giftTransaction, Guid ownerId, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Validating donor - start.");

            // No donor set - attempting to find donor based on contact/account info provided:
            if (!giftTransaction.Contains("msnfp_customerid"))
            {
                Entity organization = null;
                Guid organizationID = Guid.Empty;
                bool organizationExistYN = false;
                Entity contact = null;
                Guid contactID = Guid.Empty;
                bool contactExistYN = false;
                bool transactionUpdatedYN = false;


                // Account validation start
                localContext.TracingService.Trace("Validating Organization Name.");

                string tOrganizationName = giftTransaction.Contains("msnfp_organizationname") ? (string)giftTransaction["msnfp_organizationname"] : string.Empty;

                if (!string.IsNullOrEmpty(tOrganizationName))
                {
                    localContext.TracingService.Trace("Organization Name: " + tOrganizationName + ".");

                    ColumnSet accountCol1List = new ColumnSet("name");
                    List<Entity> accountCount = new List<Entity>();
                    QueryExpression accountQuery = new QueryExpression("account");
                    accountQuery.ColumnSet = accountCol1List;
                    accountQuery.Criteria = new FilterExpression();
                    accountQuery.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
                    accountQuery.Criteria.FilterOperator = LogicalOperator.And;

                    FilterExpression aFilter = accountQuery.Criteria.AddFilter(LogicalOperator.And);
                    aFilter.AddCondition("name", ConditionOperator.Equal, tOrganizationName);
                    accountCount = service.RetrieveMultiple(accountQuery).Entities.ToList();

                    if (accountCount.Count > 0)
                    {
                        localContext.TracingService.Trace("Account found.");
                        organization = accountCount.FirstOrDefault();

                        // Setting Donor
                        giftTransaction["msnfp_customerid"] = new EntityReference("account", organization.Id);

                        organizationID = organization.Id;
                        organizationExistYN = true;
                        transactionUpdatedYN = true;
                    }
                    else
                    {
                        localContext.TracingService.Trace("No account found, creating new record.");

                        // Creating new account record
                        organization = new Entity("account");
                        organization["name"] = tOrganizationName;

                        if (ownerId != Guid.Empty)
                        {
                            // Attempt to set the owner id:
                            organization["ownerid"] = new EntityReference("team", ownerId);
                            localContext.TracingService.Trace("account ownerid: " + ownerId.ToString());
                        }
                        else
                        {
                            localContext.TracingService.Trace("account ownerid not found");
                        }

                        organizationID = service.Create(organization);
                        localContext.TracingService.Trace("Account created and set as Donor.");

                        if (organizationID != Guid.Empty)
                        {
                            giftTransaction["msnfp_customerid"] = new EntityReference("account", organizationID);
                            organizationExistYN = true;
                            transactionUpdatedYN = true;
                        }
                    }
                }

                localContext.TracingService.Trace("Account validation completed.");


                // Contact validation start
                localContext.TracingService.Trace("Validating Contact.");

                string firstName = giftTransaction.Contains("msnfp_firstname") ? (string)giftTransaction["msnfp_firstname"] : string.Empty;
                string lastName = giftTransaction.Contains("msnfp_lastname") ? (string)giftTransaction["msnfp_lastname"] : string.Empty;

                if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
                {
                    localContext.TracingService.Trace("First Name: " + firstName + " Last Name: " + lastName + ".");

                    string tEmail = giftTransaction.Contains("msnfp_emailaddress1") ? (string)giftTransaction["msnfp_emailaddress1"] : string.Empty;
                    string tFirstName = giftTransaction.Contains("msnfp_firstname") ? (string)giftTransaction["msnfp_firstname"] : string.Empty;
                    string tLastName = giftTransaction.Contains("msnfp_lastname") ? (string)giftTransaction["msnfp_lastname"] : string.Empty;
                    string tPostalCode = giftTransaction.Contains("msnfp_billing_postalcode") ? (string)giftTransaction["msnfp_billing_postalcode"] : string.Empty;
                    string tCity = giftTransaction.Contains("msnfp_billing_city") ? (string)giftTransaction["msnfp_billing_city"] : string.Empty;
                    string tLine1 = giftTransaction.Contains("msnfp_billing_line1") ? (string)giftTransaction["msnfp_billing_line1"] : string.Empty;
                    string tLine2 = giftTransaction.Contains("msnfp_billing_line2") ? (string)giftTransaction["msnfp_billing_line2"] : string.Empty;
                    string tLine3 = giftTransaction.Contains("msnfp_billing_line3") ? (string)giftTransaction["msnfp_billing_line3"] : string.Empty;
                    string tStateProvince = giftTransaction.Contains("msnfp_billing_stateorprovince") ? (string)giftTransaction["msnfp_billing_stateorprovince"] : string.Empty;
                    string tCountry = giftTransaction.Contains("msnfp_billing_country") ? (string)giftTransaction["msnfp_billing_country"] : string.Empty;
                    string tTelephone = giftTransaction.Contains("msnfp_telephone1") ? (string)giftTransaction["msnfp_telephone1"] : string.Empty;
                    string tAltTelephone = giftTransaction.Contains("msnfp_telephone2") ? (string)giftTransaction["msnfp_telephone2"] : string.Empty;
                    string tMobile = giftTransaction.Contains("msnfp_mobilephone") ? (string)giftTransaction["msnfp_mobilephone"] : string.Empty;


                    ColumnSet col1List = new ColumnSet("contactid", "firstname", "lastname", "middlename", "firstname", "birthdate", "emailaddress1", "emailaddress2", "emailaddress3", "telephone1", "mobilephone", "gendercode", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode");

                    List<Entity> ecount = new List<Entity>();
                    QueryExpression quer = new QueryExpression("contact");
                    quer.ColumnSet = col1List;
                    quer.Criteria = new FilterExpression();
                    quer.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
                    quer.Criteria.FilterOperator = LogicalOperator.And;

                    if (!string.IsNullOrEmpty(tEmail) && !string.IsNullOrEmpty(tFirstName) && !string.IsNullOrEmpty(tLastName))
                    {
                        FilterExpression childFilter = quer.Criteria.AddFilter(LogicalOperator.Or);
                        childFilter.AddCondition("emailaddress1", ConditionOperator.Equal, tEmail);
                        childFilter.AddCondition("emailaddress2", ConditionOperator.Equal, tEmail);
                        childFilter.AddCondition("emailaddress3", ConditionOperator.Equal, tEmail);

                        FilterExpression filter = quer.Criteria.AddFilter(LogicalOperator.And);
                        filter.AddCondition("firstname", ConditionOperator.BeginsWith, tFirstName.Substring(0, 1));
                        filter.AddCondition("lastname", ConditionOperator.BeginsWith, tLastName);

                        ecount = service.RetrieveMultiple(quer).Entities.ToList();
                    }

                    if (ecount.Count > 0)
                    {
                        localContext.TracingService.Trace("Contact by email found.");
                        contact = ecount.FirstOrDefault();
                    }
                    else
                    {
                        localContext.TracingService.Trace("No Contact found by email.");

                        ecount = new List<Entity>();
                        FilterExpression filter2 = new FilterExpression();
                        if (!string.IsNullOrEmpty(tLastName) && !string.IsNullOrEmpty(tPostalCode) && !string.IsNullOrEmpty(tCity)
                            && !string.IsNullOrEmpty(tFirstName) && !string.IsNullOrEmpty(tLine1))
                        {
                            filter2.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, tLastName));
                            filter2.Conditions.Add(new ConditionExpression("address1_postalcode", ConditionOperator.Equal, tPostalCode));
                            filter2.Conditions.Add(new ConditionExpression("address1_city", ConditionOperator.Equal, tCity));
                            filter2.Conditions.Add(new ConditionExpression("address1_line1", ConditionOperator.Equal, tLine1));
                            filter2.Conditions.Add(new ConditionExpression("firstname", ConditionOperator.BeginsWith, tFirstName.Substring(0, 1)));
                            filter2.Conditions.Add(new ConditionExpression("statecode", ConditionOperator.Equal, 0));
                            filter2.FilterOperator = LogicalOperator.And;

                            quer.Criteria = filter2;

                            ecount = service.RetrieveMultiple(quer).Entities.ToList();
                        }

                        if (ecount.Count > 0)
                            contact = ecount.FirstOrDefault();
                        else
                        {
                            ecount = new List<Entity>();
                            FilterExpression filter3 = new FilterExpression();
                            if (!string.IsNullOrEmpty(tLastName) && !string.IsNullOrEmpty(tLine1) && !string.IsNullOrEmpty(tPostalCode)
                                && !string.IsNullOrEmpty(tFirstName))
                            {
                                filter3.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, tLastName));
                                filter3.Conditions.Add(new ConditionExpression("address1_line1", ConditionOperator.Equal, tLine1));
                                filter3.Conditions.Add(new ConditionExpression("address1_postalcode", ConditionOperator.Equal, tPostalCode));
                                filter3.Conditions.Add(new ConditionExpression("firstname", ConditionOperator.BeginsWith, tFirstName.Substring(0, 1)));
                                filter3.Conditions.Add(new ConditionExpression("statecode", ConditionOperator.Equal, 0));
                                filter3.FilterOperator = LogicalOperator.And;

                                quer.Criteria = filter3;

                                ecount = service.RetrieveMultiple(quer).Entities.ToList();
                            }

                            if (ecount.Count > 0)
                                contact = ecount.FirstOrDefault();
                        }
                    }

                    if (contact != null)
                    {
                        contactID = contact.Id;
                        localContext.TracingService.Trace("Found customer based on search criteria and set as Donor.");
                        contactExistYN = true;
                    }
                    else
                    {
                        // Creating new contact record - start
                        contact = new Entity("contact");
                        contact["lastname"] = tLastName;
                        contact["firstname"] = tFirstName;
                        contact["emailaddress1"] = tEmail;
                        contact["telephone1"] = tTelephone;
                        contact["telephone2"] = tAltTelephone;
                        contact["mobilephone"] = tMobile;
                        contact["address1_line1"] = tLine1;
                        contact["address1_line2"] = tLine2;
                        contact["address1_line3"] = tLine3;
                        contact["address1_city"] = tCity;
                        contact["address1_stateorprovince"] = tStateProvince;
                        contact["address1_country"] = tCountry;
                        contact["address1_postalcode"] = tPostalCode;

                        if (ownerId != Guid.Empty)
                        {
                            // Attempt to set the owner id:
                            contact["ownerid"] = new EntityReference("team", ownerId);
                            localContext.TracingService.Trace("contact ownerid: " + ownerId.ToString());
                        }
                        else
                        {
                            localContext.TracingService.Trace("contact ownerid not found");
                        }

                        contactID = service.Create(contact);
                        contactExistYN = true;
                    }

                }
                // Contact validation end
                localContext.TracingService.Trace("Contact validation completed.");

                if (contactExistYN && contactID != Guid.Empty)
                {
                    localContext.TracingService.Trace("Assigning Contact.");
                    if (organizationExistYN)
                    {
                        // Donor set, setting Related Constituent
                        if (!giftTransaction.Contains("msnfp_relatedconstituentid"))
                        {
                            giftTransaction["msnfp_relatedconstituentid"] = new EntityReference("contact", contactID);
                            transactionUpdatedYN = true;
                        }

                        //// Setting Related Constituent in Payment Schedule - Online Only
                        //if (giftTransaction.Contains("msnfp_donationpageid") || giftTransaction.Contains("msnfp_campaignpageid"))
                        //{
                        //    if (giftTransaction.Contains("msnfp_transaction_paymentscheduleid"))
                        //    {
                        //        try
                        //        {
                        //            // Get the payment schedule:
                        //            Entity pSchedule;
                        //            ColumnSet psScheduleCols;
                        //            psScheduleCols = new ColumnSet("msnfp_paymentscheduleid", "msnfp_constituentid", "msnfp_customerid");
                        //            pSchedule = service.Retrieve("msnfp_paymentschedule", ((EntityReference)giftTransaction["msnfp_transaction_paymentscheduleid"]).Id, psScheduleCols);


                        //            if (organizationID != Guid.Empty)
                        //            {
                        //                pSchedule["msnfp_customerid"] = new EntityReference("account", organizationID);
                        //                pSchedule["msnfp_constituentid"] = new EntityReference("contact", contactID);
                        //            }
                        //            else
                        //            {
                        //                pSchedule["msnfp_customerid"] = new EntityReference("contact", contactID);
                        //            }

                        //            service.Update(pSchedule);
                        //            localContext.TracingService.Trace("Update of Payment Schedule complete - Constituent - Contact.");
                        //        }
                        //        catch (Exception e)
                        //        {
                        //            localContext.TracingService.Trace("Update of Payment Schedule - Constituent - Contact - Error: " + e.Message.ToString());
                        //        }
                        //    }
                        //}
                    }
                    else
                    {
                        // No Organization exist, setting Contact as Donor
                        giftTransaction["msnfp_customerid"] = new EntityReference("contact", contactID);
                        transactionUpdatedYN = true;

                        //// Setting Related Constituent in Payment Schedule - Online Only
                        //if (giftTransaction.Contains("msnfp_donationpageid") || giftTransaction.Contains("msnfp_campaignpageid"))
                        //{
                        //    if (giftTransaction.Contains("msnfp_transaction_paymentscheduleid"))
                        //    {
                        //        try
                        //        {
                        //            // Get the payment schedule:
                        //            Entity pSchedule;
                        //            ColumnSet psScheduleCols;
                        //            psScheduleCols = new ColumnSet("msnfp_paymentscheduleid", "msnfp_customerid");
                        //            pSchedule = service.Retrieve("msnfp_paymentschedule", ((EntityReference)giftTransaction["msnfp_transaction_paymentscheduleid"]).Id, psScheduleCols);

                        //            pSchedule["msnfp_customerid"] = new EntityReference("contact", contactID);

                        //            service.Update(pSchedule);
                        //            localContext.TracingService.Trace("Update of Payment Schedule complete - Constituent - Contact.");
                        //        }
                        //        catch (Exception e)
                        //        {
                        //            localContext.TracingService.Trace("Update of Payment Schedule - Constituent - Contact - Error: " + e.Message.ToString());
                        //        }
                        //    }
                        //}
                    }
                }

                localContext.TracingService.Trace("Updating transaction record.");

                if (transactionUpdatedYN)
                {
                    localContext.TracingService.Trace("transactionUpdatedYN");
                    // updating transaction record
                    service.Update(giftTransaction);
                    // retrieving newly updated transaction record
                    ColumnSet cols = ReturnTransactionColumnSet();
                    giftTransaction = service.Retrieve("msnfp_transaction", giftTransaction.Id, cols);

                    localContext.TracingService.Trace("Transaction record updated.");
                }
            }

            if (giftTransaction.Contains("msnfp_transaction_paymentmethodid") && giftTransaction.GetAttributeValue<EntityReference>("msnfp_transaction_paymentmethodid") != null)
            {
                Entity paymentMethod_CustCheck = service.Retrieve("msnfp_paymentmethod", giftTransaction.GetAttributeValue<EntityReference>("msnfp_transaction_paymentmethodid").Id, new ColumnSet("msnfp_customerid"));

                // Copy the customer to the Payment Method
                if ((!paymentMethod_CustCheck.Contains("msnfp_customerid") || paymentMethod_CustCheck.GetAttributeValue<EntityReference>("msnfp_customerid") == null)
                    && giftTransaction.GetAttributeValue<EntityReference>("msnfp_customerid") != null)
                {
                    localContext.TracingService.Trace("Copying Customer from Transaction to Payment Method");
                    Entity paymentMethodToUpdate = new Entity(paymentMethod_CustCheck.LogicalName, paymentMethod_CustCheck.Id);
                    paymentMethodToUpdate["msnfp_customerid"] = giftTransaction["msnfp_customerid"];
                    try
                    {
                        service.Update(paymentMethodToUpdate);
                        localContext.TracingService.Trace("Copied Customer (id:" + ((EntityReference)giftTransaction["msnfp_customerid"]).Id + ") from Transaction to Payment Method");
                    }
                    catch (Exception ex)
                    {
                        localContext.TracingService.Trace("Could not Copy Customer to Payment Method:");
                        localContext.TracingService.Trace("Exception:" + ex.Message);
                        if (ex.InnerException != null)
                        {
                            localContext.TracingService.Trace("Inner Exception:" + ex.InnerException.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a tribute or memory record (if applicable) and updates the lookup for this on the transaction. Only creates a new Tribute record if the Tribute name and code fields have values.
        /// </summary>
        /// <param name="localContext"></param>
        /// <param name="targetTransaction">The transaction record that will be used to generate and associate the new tribute or memory record.</param>
        /// <param name="service"></param>
        /// <returns></returns>
        private static Guid? CreateTributeRecord(LocalPluginContext localContext, Entity targetTransaction, IOrganizationService service)
        {
            Guid? tributeId = null;
            // only create a new Tribute record if the Tribute name and code fields have values
            if (targetTransaction.GetAttributeValue<string>("msnfp_tributename") != null &&
                targetTransaction.GetAttributeValue<OptionSetValue>("msnfp_tributecode") != null)
            {
                localContext.TracingService.Trace("Creating new Tribute Record.");
                string tributeName = targetTransaction.GetAttributeValue<string>("msnfp_tributename");
                int tributeCodeVal = targetTransaction.GetAttributeValue<OptionSetValue>("msnfp_tributecode").Value;
                localContext.TracingService.Trace("Tribute Name:" + tributeName);
                localContext.TracingService.Trace("Tribute Code:" + tributeCodeVal);
                Entity tribute = new Entity("msnfp_tributeormemory");
                tribute["msnfp_name"] = tributeName;
                tribute["msnfp_identifier"] = tributeName;
                tribute["msnfp_tributeormemorytypecode"] = new OptionSetValue(tributeCodeVal);
                tributeId = service.Create(tribute);
                localContext.TracingService.Trace("Tribute Record Created.");

                localContext.TracingService.Trace("Updating Transaction with the Tribute'.");
                Entity transactionToUpdate = new Entity(targetTransaction.LogicalName, targetTransaction.Id);
                transactionToUpdate["msnfp_tributeid"] = new EntityReference(tribute.LogicalName, tributeId.Value);
                service.Update(transactionToUpdate);
                localContext.TracingService.Trace("Transaction Updated Initial Tribute");
            }

            return tributeId;
        }


        /// <summary>
        /// If the transaction has a msnfp_designationid, it will create a designated credit associated to both the transaction and designation for that amount.
        /// </summary>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <param name="targetTransaction">The transaction used to check for the designation. If it has one, it will create a designated credit.</param>
        private static void CreateDesignatedCredit(LocalPluginContext localContext, IOrganizationService service, Entity targetTransaction)
        {
            // Only proceed if the Primary Designation has been selected
            if (targetTransaction.Contains("msnfp_designationid") && targetTransaction["msnfp_designationid"] != null)
            {
                localContext.TracingService.Trace("Creating Designated Credit");
                EntityReference primaryDesignationRef = targetTransaction.GetAttributeValue<EntityReference>("msnfp_designationid");
                Entity primaryDesignation = service.Retrieve(primaryDesignationRef.LogicalName, primaryDesignationRef.Id, new ColumnSet("msnfp_name"));
                localContext.TracingService.Trace("Primary Designation ID:" + primaryDesignationRef.Id);
                localContext.TracingService.Trace("Primary Designation Name:" + primaryDesignation.GetAttributeValue<string>("msnfp_name"));
                Money amount = targetTransaction.GetAttributeValue<Money>("msnfp_amount") != null ? targetTransaction.GetAttributeValue<Money>("msnfp_amount") : new Money(0);
                DateTime bookDate = targetTransaction.GetAttributeValue<DateTime>("msnfp_bookdate");
                DateTime receivedDate = targetTransaction.GetAttributeValue<DateTime>("msnfp_receiveddate");

                // Create a new Designated Credit
                Entity designatedCredit = new Entity("msnfp_designatedcredit");
                designatedCredit["msnfp_transactionid"] = new EntityReference(targetTransaction.LogicalName, targetTransaction.Id);
                designatedCredit["msnfp_designatiedcredit_designationid"] = new EntityReference(primaryDesignation.LogicalName, primaryDesignation.Id);

                if (amount.Value > 0)
                {
                    designatedCredit["msnfp_name"] = primaryDesignation.GetAttributeValue<string>("msnfp_name") + "-$" + amount.Value;
                    designatedCredit["msnfp_amount"] = amount;
                }
                else
                {
                    designatedCredit["msnfp_name"] = primaryDesignation.GetAttributeValue<string>("msnfp_name");
                }
                if (bookDate != null && bookDate != default(DateTime))
                {
                    designatedCredit["msnfp_bookdate"] = bookDate;
                }
                if (receivedDate != null && receivedDate != default(DateTime))
                {
                    designatedCredit["msnfp_receiveddate"] = receivedDate;
                }
                service.Create(designatedCredit);
                localContext.TracingService.Trace("Created Designated Credit");
            }
        }

        private void SetParentPaymentScheduleCardTypeToChilds(Entity giftTransaction, IOrganizationService service, OrganizationServiceContext orgSvcContext, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------Entering SetParentPaymentScheduleCardTypeToChilds()---------");

            if (giftTransaction.Contains("msnfp_transaction_paymentscheduleid") && giftTransaction["msnfp_transaction_paymentscheduleid"] != null && giftTransaction.Contains("msnfp_ccbrandcode") && giftTransaction["msnfp_ccbrandcode"] != null)
            {
                try
                {
                    localContext.TracingService.Trace("Updating parent payment schedule (" + ((EntityReference)giftTransaction["msnfp_transaction_paymentscheduleid"]).Id.ToString() + ") card brand with transaction information.");
                    // Get the payment schedule:
                    Entity parentPaymentSchedule;
                    ColumnSet pscols;
                    pscols = new ColumnSet("msnfp_paymentscheduleid", "msnfp_ccbrandcode");
                    parentPaymentSchedule = service.Retrieve("msnfp_paymentschedule", ((EntityReference)giftTransaction["msnfp_transaction_paymentscheduleid"]).Id, pscols);
                    localContext.TracingService.Trace("Card Brand Code = " + ((OptionSetValue)giftTransaction["msnfp_ccbrandcode"]).Value.ToString());

                    // Update the card type to be that of the new child:
                    parentPaymentSchedule["msnfp_ccbrandcode"] = (OptionSetValue)giftTransaction["msnfp_ccbrandcode"];
                    service.Update(parentPaymentSchedule);
                    localContext.TracingService.Trace("Update of Payment Schedule complete.");
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace("Error: " + e.Message.ToString());
                }
            }

            localContext.TracingService.Trace("---------Exiting SetParentPaymentScheduleCardTypeToChilds()---------");
        }

        private void PopulateMostRecentGiftDataToDonor(IOrganizationService organizationService, OrganizationServiceContext orgSvcContext, LocalPluginContext localContext, Entity giftTransaction, string messageName)
        {
            localContext.TracingService.Trace("----- Populating The Most Recent Gift To the according Donor -----");

            var donor = giftTransaction.GetAttributeValue<EntityReference>("msnfp_customerid");

            if (donor == null)
                return;

            // Check if donor is a contact or account
            if (donor.LogicalName == "contact" || donor.LogicalName == "account")
            {
                // Get the contact entity
                Entity donorEntity = organizationService.Retrieve(donor.LogicalName, donor.Id, new ColumnSet(new String[] { "msnfp_lasttransactionid" }));


                if (string.Compare(messageName, "Delete", true) == 0)
                {
                    localContext.TracingService.Trace("Message is Delete. Locating the previous Transaction.");
                    localContext.TracingService.Trace("Current TransactionId=" + giftTransaction.Id);
                    Entity mostRecentGift = (from c in orgSvcContext.CreateQuery("msnfp_transaction")
                                             where ((EntityReference)c["msnfp_customerid"]).Id == donor.Id
                                                   && (Guid)c["msnfp_transactionid"] != giftTransaction.Id
                                             orderby c["msnfp_bookdate"] descending, c["createdon"] descending
                                             select c).FirstOrDefault();

                    if (mostRecentGift != null)
                    {
                        donorEntity["msnfp_lasttransactionid"] =
                            new EntityReference(mostRecentGift.LogicalName, mostRecentGift.Id);
                        donorEntity["msnfp_lasttransactiondate"] = (DateTime)mostRecentGift["msnfp_bookdate"];
                        organizationService.Update(donorEntity);
                    }
                }
                else if (donorEntity.Contains("msnfp_lasttransactionid") && donorEntity["msnfp_lasttransactionid"] != null
                ) // If this field has data => get the most recent by Date and populate values to donor 
                // Exclude soft credits
                {
                    Entity mostRecentGift = (from c in orgSvcContext.CreateQuery("msnfp_transaction")
                                             where ((EntityReference)c["msnfp_customerid"]).Id == donor.Id &&
                                             (c.GetAttributeValue<OptionSetValue>("msnfp_typecode") == null ||
                                                (c.GetAttributeValue<OptionSetValue>("msnfp_typecode") != null && c.GetAttributeValue<OptionSetValue>("msnfp_typecode").Value != 844060001))
                                             orderby c["msnfp_bookdate"] descending, c["createdon"] descending
                                             select c).FirstOrDefault();
                    if (mostRecentGift != null)
                    {
                        donorEntity["msnfp_lasttransactionid"] =
                        new EntityReference(mostRecentGift.LogicalName, mostRecentGift.Id);
                        if (mostRecentGift.Attributes.ContainsKey("msnfp_bookdate"))
                            donorEntity["msnfp_lasttransactiondate"] = mostRecentGift.GetAttributeValue<DateTime>("msnfp_bookdate");
                        organizationService.Update(donorEntity);
                    }
                }
                // If this field has no data => no need to check but populate values to donor
                // Exclude soft credits
                else if(giftTransaction.GetAttributeValue<OptionSetValue>("msnfp_typecode") ==null || giftTransaction.GetAttributeValue<OptionSetValue>("msnfp_typecode").Value!= 844060001)
                {
                    

                    donorEntity["msnfp_lasttransactionid"] = new EntityReference(giftTransaction.LogicalName, giftTransaction.Id);
                    if (giftTransaction.Attributes.ContainsKey("msnfp_bookdate"))
                        donorEntity["msnfp_lasttransactiondate"] = giftTransaction.GetAttributeValue<DateTime>("msnfp_bookdate");
                    organizationService.Update(donorEntity);
                }
            }
            localContext.TracingService.Trace("----- Finished Populating The Most Recent Gift To the according Donor -----");
        }

        #region Stripe - Single Transaction API Processing.
        private void processStripeTransaction(Entity configurationRecord, Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service, Boolean singleTransactionYN)
        {
            string orderResponse = string.Empty;
            string currency = string.Empty;
            Entity creditCard = null;
            string stripeCardBrand = "";
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;
            Entity customer = null;
            Entity paymentProcessor = null;
            decimal donationAmount = decimal.Zero;
            bool newCreditCardYN = false;
            string order_id = Guid.NewGuid().ToString();
            string responseText = "";

            if (giftTransaction.Contains("transactioncurrencyid") && giftTransaction["transactioncurrencyid"] != null)
            {
                Entity transactionCurrency = service.Retrieve("transactioncurrency", ((EntityReference)giftTransaction["transactioncurrencyid"]).Id, new ColumnSet("isocurrencycode"));

                if (transactionCurrency != null)
                    currency = transactionCurrency.Contains("isocurrencycode") ? (string)transactionCurrency["isocurrencycode"] : string.Empty;
            }

            int retryInterval = configurationRecord.Contains("msnfp_sche_retryinterval") ? (int)configurationRecord["msnfp_sche_retryinterval"] : 0;

            try
            {
                StripeCustomer stripeCustomer = null;
                string cardId = null;

                BaseStipeRepository baseStipeRepository = new BaseStipeRepository();

                // Get the payment method:
                creditCard = getPaymentMethodForTransaction(giftTransaction, localContext, service);

                // Ensure this is a credit card:
                if (creditCard.Contains("msnfp_type"))
                {
                    // Credit Card = 844060000:
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                    {
                        localContext.TracingService.Trace("processStripeTransaction - Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                        //removePaymentMethod(creditCard, localContext, service);
                        if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                        {
                            setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                        }
                        return;
                    }
                }

                // Ensure the essential fields are completed:
                if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
                {
                    localContext.TracingService.Trace("processStripeTransaction - Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                    removePaymentMethod(creditCard, localContext, service);
                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    return;
                }

                // Get the payment processor:
                paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, giftTransaction, localContext, service);

                string secretKey = paymentProcessor["msnfp_stripeservicekey"].ToString();
                //localContext.TracingService.Trace("processStripeTransaction - secretKey-" + secretKey);

                StripeConfiguration.SetApiKey(secretKey);

                // retrieving customer
                if (giftTransaction.Contains("msnfp_customerid"))
                {
                    customerType = ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName;
                    customerId = ((EntityReference)giftTransaction["msnfp_customerid"]).Id;
                    if (customerType == "account")
                        customer = service.Retrieve("account", customerId, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "telephone3", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));
                    else
                        customer = service.Retrieve("contact", customerId, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "mobilephone", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));

                }

                //Stripe Customer ID contains customer Id and Authtoken contains card id.
                if (creditCard.Contains("msnfp_stripecustomerid") && creditCard["msnfp_stripecustomerid"] != null && creditCard.Contains("msnfp_authtoken") && creditCard["msnfp_authtoken"] != null)
                {
                    localContext.TracingService.Trace("processStripeTransaction - Existing Card use");
                    string stripeCustomerId = creditCard["msnfp_stripecustomerid"].ToString();
                    cardId = creditCard["msnfp_authtoken"].ToString();
                    // Set the card brand from the credit card for the transaction:
                    int? cardBrandInt = ((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value;
                    if (cardBrandInt != null)
                    {
                        switch (cardBrandInt)
                        {
                            case 844060001:
                                stripeCardBrand = "MasterCard";
                                break;
                            case 844060000:
                                stripeCardBrand = "Visa";
                                break;
                            case 844060004:
                                stripeCardBrand = "American Express";
                                break;
                            case 844060008:
                                stripeCardBrand = "Discover";
                                break;
                            case 844060005:
                                stripeCardBrand = "Diners Club";
                                break;
                            case 844060009:
                                stripeCardBrand = "UnionPay";
                                break;
                            case 844060006:
                                stripeCardBrand = "JCB";
                                break;
                            default:
                                // Unknown:
                                stripeCardBrand = "Unknown";
                                break;
                        }
                    }

                    StripeConfiguration.SetApiKey(secretKey);

                    #region Retrieve Customer
                    var custService = new StripeCustomerService();
                    stripeCustomer = custService.Get(stripeCustomerId);
                    #endregion
                }
                else
                {
                    localContext.TracingService.Trace("processStripeTransaction - New Card use");

                    newCreditCardYN = true;

                    string custName = customer.LogicalName == "account" ? customer["name"].ToString() : (customer["firstname"].ToString() + customer["lastname"].ToString());
                    string custEmail = customer.Contains("emailaddress1") ? customer["emailaddress1"].ToString() : string.Empty;

                    localContext.TracingService.Trace("processStripeTransaction - extracting customer info - done");
                    stripeCustomer = new CustomerService().GetStripeCustomer(custName, custEmail, secretKey);
                    localContext.TracingService.Trace("processStripeTransaction - obtained stripeCustomer");

                    var myToken = new StripeTokenCreateOptions();
                    string expMMYY = creditCard.Contains("msnfp_ccexpmmyy") ? creditCard["msnfp_ccexpmmyy"].ToString() : string.Empty;

                    myToken.Card = new StripeCreditCardOptions()
                    {
                        Number = creditCard["msnfp_cclast4"].ToString(),
                        ExpirationYear = expMMYY.Substring(expMMYY.Length - 2),
                        ExpirationMonth = expMMYY.Substring(0, expMMYY.Length - 2)
                    };

                    var tokenService = new StripeTokenService();
                    StripeToken stripeTokenFinal = tokenService.Create(myToken);

                    StripeCard stripeCardObj = new StripeCard();
                    stripeCardObj.SourceToken = stripeTokenFinal.Id;
                    string url = string.Format("https://api.stripe.com/v1/customers/{0}/sources", (object)stripeCustomer.Id);
                    StripeCard stripeCard = baseStipeRepository.Create<StripeCard>(stripeCardObj, url, secretKey);
                    if (string.IsNullOrEmpty(stripeCard.Id))
                        throw new Exception("processStripeTransaction - Unable to add card to customer");
                    cardId = stripeCard.Id;
                    stripeCardBrand = stripeCard.Brand;

                    localContext.TracingService.Trace("processStripeTransaction - Card Id- " + cardId);

                    MaskStripeCreditCard(localContext, creditCard, cardId, stripeCardBrand, stripeCustomer.Id);
                }

                if (giftTransaction.Contains("msnfp_amount"))
                    donationAmount = ((Money)giftTransaction["msnfp_amount"]).Value;

                int chargeAmount = Convert.ToInt32((donationAmount * 100).ToString().Split('.')[0]);
                StripeCharge stripeObject = new StripeCharge();
                stripeObject.Amount = chargeAmount;
                stripeObject.Currency = string.IsNullOrEmpty(currency) ? "CAD" : currency;
                stripeObject.Customer = stripeCustomer;

                Source source = new Source();
                source.Id = cardId;
                stripeObject.Source = source;
                stripeObject.Description = giftTransaction.Contains("msnfp_invoiceidentifier") ? (string)giftTransaction["msnfp_invoiceidentifier"] : order_id; //string.Empty;                

                StripeCharge stripePayment = baseStipeRepository.Create<StripeCharge>(stripeObject, "https://api.stripe.com/v1/charges", secretKey);

                //localContext.TracingService.Trace("processStripeTransaction - msnfp_transactionid : " + giftTransaction["msnfp_transactionid"].ToString());

                Entity response = new Entity("msnfp_response");
                response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", ((Guid)giftTransaction["msnfp_transactionid"]));
                response["msnfp_identifier"] = "Response for " + (string)giftTransaction["msnfp_name"];
                //donationObj.TransactionId = (Guid)giftTransaction["msnfp_transactionid"];


                //response["msnfp_responseaction"] = new OptionSetValue(84406001);//Gateway Run

                if (!string.IsNullOrEmpty(stripePayment.FailureMessage))
                {
                    response["msnfp_response"] = "FAILED";
                    giftTransaction["statuscode"] = new OptionSetValue(844060003);

                    giftTransaction["msnfp_lastfailedretry"] = DateTime.Now.ToLocalTime().Date;
                    giftTransaction["msnfp_currentretry"] = 0;
                    giftTransaction["msnfp_nextfailedretry"] = DateTime.Now.AddDays(retryInterval).ToLocalTime().Date;
                    giftTransaction["msnfp_transactionresult"] = "FAILED";
                }
                else
                {
                    localContext.TracingService.Trace("processStripeOneTimeTransaction - stripePayment.Id : " + stripePayment.Id);

                    if (stripePayment != null)
                    {
                        localContext.TracingService.Trace("processStripeOneTimeTransaction - stripePayment.InvoiceId : " + stripePayment.InvoiceId);
                        localContext.TracingService.Trace("processStripeOneTimeTransaction - stripePayment.Status : " + stripePayment.Status.ToString());

                        if (giftTransaction.Contains("msnfp_parenttransactionid"))
                            response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", ((EntityReference)giftTransaction["msnfp_parenttransactionid"]).Id);

                        if (stripePayment.Status.Equals("succeeded"))
                        {
                            responseText += "---------Start Stripe Response---------" + System.Environment.NewLine;
                            responseText += "TransAmount = " + stripePayment.Status + System.Environment.NewLine;
                            responseText += "TransAmount = " + donationAmount + System.Environment.NewLine;
                            responseText += "Auth Token = " + cardId + System.Environment.NewLine;
                            responseText += "---------End Stripe Response---------";

                            localContext.TracingService.Trace("processStripeTransaction - Got successful response from Stripe payment gateway.");
                            response["msnfp_response"] = responseText;

                            giftTransaction["msnfp_transactionresult"] = stripePayment.Status;
                            giftTransaction["msnfp_transactionidentifier"] = stripePayment.Id;
                            //primaryDonationPledge["msnfp_transactionstatus"] = stripePayment.Status;
                            giftTransaction["statuscode"] = new OptionSetValue(844060000);

                            // Set the card type based on the Stripe response code:
                            if (stripeCardBrand != null)
                            {
                                localContext.TracingService.Trace("Card Type Response Code = " + stripeCardBrand);
                                switch (stripeCardBrand)
                                {
                                    case "MasterCard":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060001);
                                        break;
                                    case "Visa":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060000);
                                        break;
                                    case "American Express":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060004);
                                        break;
                                    case "Discover":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060008);
                                        break;
                                    case "Diners Club":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060005);
                                        break;
                                    case "UnionPay":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060009);
                                        break;
                                    case "JCB":
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                                        break;
                                    default:
                                        // Unknown:
                                        giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060010);
                                        break;
                                }
                            }

                            // updating customer record
                            if (customer != null)
                            {
                                customer["msnfp_lasttransactionid"] = new EntityReference("msnfp_transaction", ((Guid)giftTransaction["msnfp_transactionid"]));
                                customer["msnfp_lasttransactiondate"] = giftTransaction.Contains("createdon") ? (DateTime)giftTransaction["createdon"] : DateTime.MinValue;
                                service.Update(customer);
                            }

                            localContext.TracingService.Trace("processStripeTransaction - Updated Transaction Record.");
                        }
                        else
                        {
                            localContext.TracingService.Trace("processStripeTransaction - Got failure response from payment gateway.");
                            response["msnfp_response"] = stripePayment.StripeResponse.ToString();
                            giftTransaction["statuscode"] = new OptionSetValue(844060003);
                            localContext.TracingService.Trace("processStripeTransaction - Status code updated to failed");

                            giftTransaction["msnfp_lastfailedretry"] = DateTime.Now.ToLocalTime().Date;
                            giftTransaction["msnfp_currentretry"] = 0;
                            giftTransaction["msnfp_nextfailedretry"] = DateTime.Now.AddDays(retryInterval).ToLocalTime().Date;
                            giftTransaction["msnfp_transactionidentifier"] = stripePayment.Id;
                            giftTransaction["msnfp_transactionresult"] = stripePayment.Status;

                            localContext.TracingService.Trace("Gateway Response Message." + stripePayment.Status);
                        }
                    }
                }

                // assigning invoice identifier
                giftTransaction["msnfp_invoiceidentifier"] = order_id;


                // creating response record
                Guid responseGUID = service.Create(response);

                if (responseGUID != null)
                    giftTransaction["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);
            }
            catch (Exception e)
            {
                //Console.WriteLine("processStripeTransaction - Error message: " + e.Message);

                localContext.TracingService.Trace("processStripeTransaction - error : " + e.Message);
                giftTransaction["statuscode"] = new OptionSetValue(844060003);
                localContext.TracingService.Trace("processStripeTransaction - Status code updated to failed");

                giftTransaction["msnfp_lastfailedretry"] = DateTime.Now.ToLocalTime().Date;
                giftTransaction["msnfp_currentretry"] = 0;
                giftTransaction["msnfp_nextfailedretry"] = DateTime.Now.AddDays(retryInterval).ToLocalTime().Date;
                giftTransaction["msnfp_transactionresult"] = "FAILED";
                //primaryDonationPledge["msnfp_creditcardid"] = null;
                //payment fails remove credit card from parent as well
                if (giftTransaction.Contains("msnfp_parenttransactionid") && giftTransaction["msnfp_parenttransactionid"] != null)
                {
                    localContext.TracingService.Trace("processStripeTransaction - payment fails remove credit card from parent as well");
                    Entity parentTransaction = service.Retrieve("msnfp_transaction", ((EntityReference)giftTransaction["msnfp_transactionid"]).Id, new ColumnSet("msnfp_transaction_paymentmethodid"));
                    if (parentTransaction != null && parentTransaction.Contains("msnfp_transaction_paymentmethodid") && parentTransaction["msnfp_transaction_paymentmethodid"] != null)
                    {
                        parentTransaction["msnfp_transaction_paymentmethodid"] = null;
                        localContext.TracingService.Trace("msnfp_transaction_paymentmethodid");
                        service.Update(parentTransaction);
                        localContext.TracingService.Trace("processStripeTransaction - parent gift updated. removed Credit card");
                    }
                }
            }

            if (singleTransactionYN) // single transaction
            {
                // removing the payment transaction lookup value on the gift transaction
                giftTransaction["msnfp_transaction_paymentmethodid"] = null;

                // new credit card - removing payment method
                if (newCreditCardYN)
                    removePaymentMethod(creditCard, localContext, service);
            }

            localContext.TracingService.Trace("stripe");
            service.Update(giftTransaction);
            localContext.TracingService.Trace("processStripeTransaction - Entity Updated.");

        }

        #endregion

        #region iATS- Single Transaction API processing
        private void ProcessiATSTransaction(Entity configurationRecord, Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service, Boolean singleTransactionYN)
        {

            string orderResponse = string.Empty;
            string currency = string.Empty;
            Entity creditCard = null;
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;
            Entity customer = null;
            Entity paymentProcessor = null;
            decimal donationAmount = decimal.Zero;
            string order_id = Guid.NewGuid().ToString();
            string responseText = "";
            string agentCode = string.Empty;
            string agentPassword = string.Empty;
            XmlDocument _xmlDoc = null;
            string cardId = null;
            bool newCreditCardYN = false;

            if (giftTransaction.Contains("transactioncurrencyid") && giftTransaction["transactioncurrencyid"] != null)
            {
                localContext.TracingService.Trace("Getting transaction currency.");
                Entity transactionCurrency = service.Retrieve("transactioncurrency", ((EntityReference)giftTransaction["transactioncurrencyid"]).Id, new ColumnSet("isocurrencycode"));

                if (transactionCurrency != null)
                    currency = transactionCurrency.Contains("isocurrencycode") ? (string)transactionCurrency["isocurrencycode"] : string.Empty;
            }

            int retryInterval = configurationRecord.Contains("msnfp_sche_retryinterval") ? (int)configurationRecord["msnfp_sche_retryinterval"] : 0;

            try
            {
                // Get the payment method:
                creditCard = getPaymentMethodForTransaction(giftTransaction, localContext, service);
                localContext.TracingService.Trace("Payment method retrieved");

                // Get the payment processor:
                paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, giftTransaction, localContext, service);
                localContext.TracingService.Trace("Payment processor retrieved.");

                if (paymentProcessor != null)
                {
                    agentCode = paymentProcessor.GetAttributeValue<string>("msnfp_iatsagentcode");
                    agentPassword = paymentProcessor.GetAttributeValue<string>("msnfp_iatspassword");
                }

                // retrieving customer
                if (giftTransaction.Contains("msnfp_customerid"))
                {
                    customerType = ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName;
                    customerId = ((EntityReference)giftTransaction["msnfp_customerid"]).Id;
                    if (customerType == "account")
                        customer = service.Retrieve("account", customerId, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "telephone3", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));
                    else
                        customer = service.Retrieve("contact", customerId, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "mobilephone", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));

                }
                if (giftTransaction.Contains("msnfp_amount"))
                    donationAmount = ((Money)giftTransaction["msnfp_amount"]).Value;

                //Credit card payment
                if (creditCard.Contains("msnfp_type") && ((OptionSetValue)creditCard["msnfp_type"]).Value == 844060000)
                {
                    localContext.TracingService.Trace("iATS credit card payment.");

                    // Ensure the essential fields are completed:
                    if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
                    {
                        localContext.TracingService.Trace("processiATSTransaction - Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                        removePaymentMethod(creditCard, localContext, service);
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                        return;
                    }

                    //For Existing credit card
                    if (creditCard.Contains("msnfp_authtoken"))
                        cardId = creditCard["msnfp_authtoken"] as string;
                    else  //For new credit caard
                    {
                        localContext.TracingService.Trace("Create new customer for iATS payment.");
                        newCreditCardYN = true;
                        string expMMYY = creditCard.Contains("msnfp_ccexpmmyy") ? creditCard.GetAttributeValue<string>("msnfp_ccexpmmyy") : string.Empty;
                        string yr = expMMYY.Substring(expMMYY.Length - 2);
                        string mth = expMMYY.Substring(0, expMMYY.Length - 2);
                        expMMYY = mth + "/" + yr;

                        string cardNum = creditCard.Contains("msnfp_cclast4") ? creditCard.GetAttributeValue<string>("msnfp_cclast4") : string.Empty;

                        CreateCreditCardCustomerCode objCreate = new CreateCreditCardCustomerCode();
                        objCreate.lastName = customer.LogicalName == "contact" ? customer.GetAttributeValue<string>("lastname") : string.Empty;
                        objCreate.firstName = customer.LogicalName == "account" ? customer.GetAttributeValue<string>("name") : customer.GetAttributeValue<string>("firstname");
                        objCreate.agentCode = agentCode;
                        objCreate.password = agentPassword;
                        objCreate.beginDate = DateTime.Today;
                        objCreate.endDate = DateTime.Today.AddDays(1);
                        objCreate.country = customer.GetAttributeValue<string>("address1_country");
                        objCreate.creditCardExpiry = expMMYY;
                        objCreate.creditCardNum = cardNum;
                        objCreate.recurring = false;
                        objCreate.address = customer.GetAttributeValue<string>("address1_line1");
                        objCreate.city = customer.GetAttributeValue<string>("address1_city");
                        objCreate.zipCode = customer.GetAttributeValue<string>("address1_postalcode");
                        objCreate.state = customer.GetAttributeValue<string>("address1_stateorprovince");
                        objCreate.email = customer.GetAttributeValue<string>("emailaddress1");
                        objCreate.creditCardCustomerName = customer.LogicalName == "account" ? customer.GetAttributeValue<string>("name") : customer.GetAttributeValue<string>("firstname") + " " + customer.GetAttributeValue<string>("lastname");
                        //objCreate.mop = "VISA";
                        //objCreate.customerIPAddress = "123.0.0.3";

                        XmlDocument xmlDocCustCode = iATSProcess.CreateCreditCardCustomerCode(objCreate);
                        localContext.TracingService.Trace(xmlDocCustCode.InnerXml);
                        XmlNodeList xnListCC = xmlDocCustCode.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.

                        foreach (XmlNode item in xnListCC)
                        {

                            string authResult = item.InnerText;
                            localContext.TracingService.Trace("Auth Result- " + item.InnerText);
                            if (authResult.Contains("OK"))
                            {
                                cardId = xmlDocCustCode.GetElementsByTagName("CUSTOMERCODE")[0].InnerText;
                                localContext.TracingService.Trace("Auth Token- " + cardId);
                            }

                        }

                        localContext.TracingService.Trace("Mask the credit card.");
                        MaskStripeCreditCard(localContext, creditCard, cardId, null, null);

                    }

                    //CreditCard token present
                    if (!string.IsNullOrEmpty(cardId))
                    {
                        localContext.TracingService.Trace("Payment Method is Credit Card.");

                        ProcessCreditCardWithCustomerCode obj = new ProcessCreditCardWithCustomerCode();

                        obj.agentCode = agentCode;
                        obj.password = agentPassword;
                        obj.customerCode = cardId;
                        obj.invoiceNum = giftTransaction.Contains("msnfp_invoiceidentifier") ? (string)giftTransaction["msnfp_invoiceidentifier"] : order_id; //string.Empty;    
                        obj.total = string.Format("{0:0.00}", donationAmount);
                        localContext.TracingService.Trace("Donation Amount : " + string.Format("{0:0.00}", donationAmount));
                        obj.comment = "Debited by Dynamics 365 on " + DateTime.Now.ToString();
                        _xmlDoc = iATSProcess.ProcessCreditCardWithCustomerCode(obj);

                        localContext.TracingService.Trace("Process complete to Payment with Credit Card.");
                    }
                }
                //Bank account payment
                else if (creditCard.Contains("msnfp_type") && ((OptionSetValue)creditCard["msnfp_type"]).Value == 844060001)
                {
                    localContext.TracingService.Trace("iATS Bank Account payment.");

                    // Ensure the essential fields are completed:
                    if (!creditCard.Contains("msnfp_bankactnumber"))
                    {
                        localContext.TracingService.Trace("processiATSTransaction - Not a completed bank account. Missing bank account number");
                        removePaymentMethod(creditCard, localContext, service);
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                        return;
                    }

                    if (creditCard.Contains("msnfp_authtoken"))
                        cardId = creditCard["msnfp_authtoken"] as string;
                    else
                    {

                        CreateACHEFTCustomerCode objCreate = new CreateACHEFTCustomerCode();
                        objCreate.lastName = customer.LogicalName == "contact" ? customer.GetAttributeValue<string>("lastname") : string.Empty;
                        objCreate.firstName = customer.LogicalName == "account" ? customer.GetAttributeValue<string>("name") : customer.GetAttributeValue<string>("firstname");
                        objCreate.agentCode = agentCode;
                        objCreate.password = agentPassword;
                        objCreate.beginDate = DateTime.Today;
                        objCreate.endDate = DateTime.Today.AddDays(1);
                        objCreate.country = customer.GetAttributeValue<string>("address1_country");
                        objCreate.accountNum = creditCard.GetAttributeValue<string>("msnfp_bankactnumber");
                        objCreate.recurring = false;
                        objCreate.address = customer.GetAttributeValue<string>("address1_line1");
                        objCreate.city = customer.GetAttributeValue<string>("address1_city");
                        objCreate.zipCode = customer.GetAttributeValue<string>("address1_postalcode");
                        objCreate.state = customer.GetAttributeValue<string>("address1_stateorprovince");
                        objCreate.email = customer.GetAttributeValue<string>("emailaddress1");

                        XmlDocument xmlDocCustCode = iATSProcess.CreateACHEFTCustomerCode(objCreate);

                        XmlNodeList xnListCC = xmlDocCustCode.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.

                        foreach (XmlNode item in xnListCC)
                        {

                            string authResult = item.InnerText;
                            localContext.TracingService.Trace("Auth Result- " + item.InnerText);
                            if (authResult.Contains("OK"))
                            {
                                cardId = xmlDocCustCode.GetElementsByTagName("CUSTOMERCODE")[0].InnerText;
                            }
                        }
                    }

                    //Customer token present
                    if (!string.IsNullOrEmpty(cardId))
                    {
                        localContext.TracingService.Trace("Payment Method is Bank Account.");

                        ProcessACHEFTWithCustomerCode obj = new ProcessACHEFTWithCustomerCode();

                        obj.agentCode = agentCode;
                        obj.password = agentPassword;
                        obj.customerCode = cardId;
                        obj.invoiceNum = giftTransaction.Contains("msnfp_invoiceidentifier") ? (string)giftTransaction["msnfp_invoiceidentifier"] : order_id; //string.Empty;    
                        obj.total = string.Format("{0:0.00}", donationAmount);
                        localContext.TracingService.Trace("Donation Amount : " + string.Format("{0:0.00}", donationAmount));
                        obj.comment = "Debited by Dynamics 365 on " + DateTime.Now.ToString();
                        _xmlDoc = iATSProcess.ProcessACHEFTWithCustomerCode(obj);

                        localContext.TracingService.Trace("Process complete to Payment with bank account.");
                    }

                }


                if (_xmlDoc != null)
                {
                    XmlNodeList xnList = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.

                    Entity response = new Entity("msnfp_response");
                    response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", ((Guid)giftTransaction["msnfp_transactionid"]));
                    response["msnfp_identifier"] = "Response for " + (string)giftTransaction["msnfp_name"];

                    foreach (XmlNode item in xnList)
                    {
                        response["msnfp_response"] = item.InnerText;

                        string authResult = item.InnerText;

                        if (authResult.Contains("OK"))
                        {
                            localContext.TracingService.Trace("Got successful response from iATS payment gateway.");

                            if (giftTransaction.Contains("msnfp_parenttransactionid"))
                                response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", ((EntityReference)giftTransaction["msnfp_parenttransactionid"]).Id);

                            responseText += "---------Start iATS Response---------" + System.Environment.NewLine;
                            responseText += "TransStatus = " + item.InnerText + System.Environment.NewLine;
                            responseText += "TransAmount = " + donationAmount + System.Environment.NewLine;
                            responseText += "Auth Token = " + cardId + System.Environment.NewLine;
                            responseText += "---------End iATS Response---------";

                            localContext.TracingService.Trace("processiATSTransaction - Got successful response from iATS payment gateway.");
                            response["msnfp_response"] = responseText;

                            giftTransaction["msnfp_transactionresult"] = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;
                            giftTransaction["msnfp_transactionidentifier"] = _xmlDoc.GetElementsByTagName("TRANSACTIONID")[0].InnerText;
                            giftTransaction["statuscode"] = new OptionSetValue(844060000);

                            // updating customer record
                            if (customer != null)
                            {
                                customer["msnfp_lasttransactionid"] = new EntityReference("msnfp_transaction", ((Guid)giftTransaction["msnfp_transactionid"]));
                                customer["msnfp_lasttransactiondate"] = giftTransaction.Contains("createdon") ? (DateTime)giftTransaction["createdon"] : DateTime.MinValue;
                                service.Update(customer);
                            }

                            localContext.TracingService.Trace("processStripeTransaction - Updated Transaction Record.");
                        }
                        else
                        {
                            localContext.TracingService.Trace("Got failure response from iATS payment gateway.");
                            giftTransaction["statuscode"] = new OptionSetValue(844060003);
                            localContext.TracingService.Trace("Status code updated to failed");

                            giftTransaction["msnfp_lastfailedretry"] = DateTime.Now.ToLocalTime().Date;
                            giftTransaction["msnfp_currentretry"] = 0;
                            giftTransaction["msnfp_nextfailedretry"] = DateTime.Now.AddDays(retryInterval).ToLocalTime().Date;

                            giftTransaction["msnfp_transactionresult"] = "FAILED";
                            giftTransaction["msnfp_transactionidentifier"] = _xmlDoc.GetElementsByTagName("TRANSACTIONID")[0].InnerText;
                            giftTransaction["msnfp_transactionresult"] = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;

                            localContext.TracingService.Trace("Gateway Response Message." + _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText);
                        }
                    }
                    // assigning invoice identifier
                    giftTransaction["msnfp_invoiceidentifier"] = order_id;


                    // creating response record
                    Guid responseGUID = service.Create(response);

                    if (responseGUID != null)
                        giftTransaction["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);
                }

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("processiATSTransaction - error : " + e.Message);
                giftTransaction["statuscode"] = new OptionSetValue(844060003);
                localContext.TracingService.Trace("processiATSTransaction - Status code updated to failed");

                giftTransaction["msnfp_lastfailedretry"] = DateTime.Now.ToLocalTime().Date;
                giftTransaction["msnfp_currentretry"] = 0;
                giftTransaction["msnfp_nextfailedretry"] = DateTime.Now.AddDays(retryInterval).ToLocalTime().Date;
                giftTransaction["msnfp_transactionresult"] = "FAILED";
                //primaryDonationPledge["msnfp_creditcardid"] = null;
                //payment fails remove credit card from parent as well
                if (giftTransaction.Contains("msnfp_parenttransactionid") && giftTransaction["msnfp_parenttransactionid"] != null)
                {
                    localContext.TracingService.Trace("processiATSTransaction - payment fails remove credit card from parent as well");
                    Entity parentTransaction = service.Retrieve("msnfp_transaction", ((EntityReference)giftTransaction["msnfp_transactionid"]).Id, new ColumnSet("msnfp_transaction_paymentmethodid"));
                    if (parentTransaction != null && parentTransaction.Contains("msnfp_transaction_paymentmethodid") && parentTransaction["msnfp_transaction_paymentmethodid"] != null)
                    {
                        parentTransaction["msnfp_transaction_paymentmethodid"] = null;
                        localContext.TracingService.Trace("msnfp_transaction_paymentmethodid 2");
                        service.Update(parentTransaction);
                        localContext.TracingService.Trace("processiATSTransaction - parent gift updated. removed Credit card");
                    }
                }
            }

            if (singleTransactionYN) // single transaction
            {
                // removing the payment transaction lookup value on the gift transaction
                giftTransaction["msnfp_transaction_paymentmethodid"] = null;

                // new credit card - removing payment method
                if (newCreditCardYN)
                    removePaymentMethod(creditCard, localContext, service);
            }
            localContext.TracingService.Trace("iats");
            service.Update(giftTransaction);
            localContext.TracingService.Trace("processiATSTransaction - Entity Updated.");
        }

        #endregion


        #region Moneris - One Time Payment API Processing.
        /// <summary>
        /// Using the given transaction's payment method, attempt to do a one time Moneris transaction and storing the response. This will not keep the payment method after completion.
        /// </summary>
        /// <param name="giftTransaction"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private string processMonerisOneTimeTransaction(Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service)
        {
            // This will be the response code from the Moneris payment. This is used when adding a moneris vault profile to ensure AVS/CVD validation occured.
            string returnResponseCode = "";
            localContext.TracingService.Trace("Entering processMonerisOneTimeTransaction().");
            Entity creditCard = null;
            Entity paymentProcessor = null;
            localContext.TracingService.Trace("Gathering transaction data from target id.");

            // Get the payment method:
            creditCard = getPaymentMethodForTransaction(giftTransaction, localContext, service);

            // Ensure this is a credit card:
            if (creditCard.Contains("msnfp_type"))
            {
                // Credit Card = 844060000:
                if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                    {
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    }
                    return returnResponseCode;
                }
            }

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                removePaymentMethod(creditCard, localContext, service);

                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                return returnResponseCode;
            }

            // Get the payment processor:
            paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, giftTransaction, localContext, service);


            // Fill in payment data:
            localContext.TracingService.Trace("Put gathered payment information into purchase object.");
            string order_id = Guid.NewGuid().ToString();
            string store_id = (string)paymentProcessor["msnfp_storeid"];
            string api_token = (string)paymentProcessor["msnfp_apikey"];
            string amount = ((Money)giftTransaction["msnfp_amount"]).Value.ToString();
            string pan = (string)creditCard["msnfp_cclast4"];
            string expdate = (string)creditCard["msnfp_ccexpmmyy"]; //"2001"; //YYMM format from payment method. Note this is the OPPOSITE of most cards.
            string crypt = "7"; // SSL Site
            string processing_country_code = "CA";
            bool status_check = false;

            // Get the correct expiry date format. Since normal cards are MMYY and Moneris uses YYMM we need to flip the values before sending.
            string firstTwo = expdate.Substring(0, 2);
            string lastTwo = expdate.Substring(2, 2);

            localContext.TracingService.Trace("Old Expiry format (MMYY):" + expdate);
            expdate = lastTwo + firstTwo;
            localContext.TracingService.Trace("Moneris Expiry format (YYMM):" + expdate);

            // Debugging Note: When testing CVD or AVS, you must only use the Visa test card numbers 4242424242424242 or 4005554444444403, and the amounts described in the Simulator eFraud Response Codes Table. 
            // See here for more: https://developer.moneris.com/Documentation/NA/E-Commerce%20Solutions/API/Purchase?lang=dotnet
            // https://developer.moneris.com/Documentation/NA/E-Commerce%20Solutions/API/~/link.aspx?_id=96891BFCE34F4C7FB2BA6DDF6BA4EC0C&_z=z

            localContext.TracingService.Trace("Creating Moneris purchase object.");
            Purchase purchase = new Purchase();
            purchase.SetOrderId(order_id);
            purchase.SetAmount(amount);
            purchase.SetPan(pan);
            purchase.SetExpDate(expdate); //YYMM format
            purchase.SetCryptType(crypt);
            purchase.SetDynamicDescriptor("2134565");

            // Address Verification Service (optional depending on settings):
            localContext.TracingService.Trace("Check for AVS Validation.");
            AvsInfo avsCheck = new AvsInfo();
            if (creditCard.Contains("msnfp_ccbrandcode"))
            {
                // Note: AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).
                if ((((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060000) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060002) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060001) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060003) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060008) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060004))
                {
                    if (paymentProcessor.Contains("msnfp_avsvalidation"))
                    {
                        if ((bool)paymentProcessor["msnfp_avsvalidation"])
                        {
                            localContext.TracingService.Trace("AVS Validation = True");
                            if (giftTransaction.Contains("msnfp_customerid"))
                            {
                                try
                                {
                                    localContext.TracingService.Trace("Entering address information for AVS validation.");
                                    avsCheck = AssignAVSValidationFieldsFromPaymentMethod(giftTransaction, creditCard, avsCheck, localContext, service);
                                    purchase.SetAvsInfo(avsCheck);
                                }
                                catch
                                {
                                    localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                                    throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)giftTransaction["msnfp_customerid"]).Id);
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("No Donor. Exiting plugin.");
                                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                                throw new ArgumentNullException("msnfp_customerid");
                            }

                        }
                        else
                        {
                            localContext.TracingService.Trace("AVS Validation = False");
                        }
                    }
                }
                else
                {
                    localContext.TracingService.Trace("Could not do AVS Validation as the card type is not supported. AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).");
                    localContext.TracingService.Trace("Payment Card type: " + ((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value.ToString());
                }
            }
            else
            {
                localContext.TracingService.Trace("Could not do AVS Validation as the card type is not assigned.");
            }


            localContext.TracingService.Trace("Creating HttpsPostRequest object.");
            HttpsPostRequest mpgReq = new HttpsPostRequest();
            try
            {
                mpgReq.SetProcCountryCode(processing_country_code);
                // Set the test mode from the payment processor variable:
                if (paymentProcessor.Contains("msnfp_testmode"))
                {
                    if ((bool)paymentProcessor["msnfp_testmode"])
                    {
                        localContext.TracingService.Trace("Test Mode is Enabled.");
                        mpgReq.SetTestMode(true);
                    }
                    else
                    {
                        localContext.TracingService.Trace("Test Mode is Disabled.");
                        mpgReq.SetTestMode(false);
                    }
                }
                else
                {
                    localContext.TracingService.Trace("Test Mode not set. Defaulting to test mode enabled.");
                    mpgReq.SetTestMode(true);
                }
                mpgReq.SetStoreId(store_id);
                mpgReq.SetApiToken(api_token);
                mpgReq.SetTransaction(purchase);
                mpgReq.SetStatusCheck(status_check);
                localContext.TracingService.Trace("Sending Moneris HttpsPostRequest.");
                mpgReq.Send(); // Send the data to Moneris for processing. Here is where the card is charged.
                localContext.TracingService.Trace("HttpsPostRequest sent successfully!");
            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("HttpsPostRequest Error: " + e.ToString());
                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                removePaymentMethod(creditCard, localContext, service);
                return returnResponseCode;
            }

            try
            {
                Receipt receipt = mpgReq.GetReceipt();
                string responseText = "";

                // Log the data in the trace log:
                localContext.TracingService.Trace("---------Moneris Response---------");
                localContext.TracingService.Trace("CardType = " + receipt.GetCardType());
                localContext.TracingService.Trace("TransAmount = " + receipt.GetTransAmount());
                localContext.TracingService.Trace("TxnNumber = " + receipt.GetTxnNumber());
                localContext.TracingService.Trace("ReceiptId = " + receipt.GetReceiptId());
                localContext.TracingService.Trace("TransType = " + receipt.GetTransType());
                localContext.TracingService.Trace("ReferenceNum = " + receipt.GetReferenceNum());
                localContext.TracingService.Trace("ResponseCode = " + receipt.GetResponseCode());
                localContext.TracingService.Trace("ISO = " + receipt.GetISO());
                localContext.TracingService.Trace("BankTotals = " + receipt.GetBankTotals());
                localContext.TracingService.Trace("Message = " + receipt.GetMessage());
                localContext.TracingService.Trace("AuthCode = " + receipt.GetAuthCode());
                localContext.TracingService.Trace("Complete = " + receipt.GetComplete());
                localContext.TracingService.Trace("TransDate = " + receipt.GetTransDate());
                localContext.TracingService.Trace("TransTime = " + receipt.GetTransTime());
                localContext.TracingService.Trace("Ticket = " + receipt.GetTicket());
                localContext.TracingService.Trace("TimedOut = " + receipt.GetTimedOut());
                localContext.TracingService.Trace("Avs Response = " + receipt.GetAvsResultCode());
                localContext.TracingService.Trace("Cvd Response = " + receipt.GetCvdResultCode());
                localContext.TracingService.Trace("ITD Response = " + receipt.GetITDResponse());
                localContext.TracingService.Trace("IsVisaDebit = " + receipt.GetIsVisaDebit());
                localContext.TracingService.Trace("---------End Moneris Response---------");

                // Data dump the same response into the response entity record:
                responseText += "---------Moneris Response---------" + System.Environment.NewLine;
                responseText += "CardType = " + receipt.GetCardType() + System.Environment.NewLine;
                responseText += "TransAmount = " + receipt.GetTransAmount() + System.Environment.NewLine;
                responseText += "TxnNumber = " + receipt.GetTxnNumber() + System.Environment.NewLine;
                responseText += "ReceiptId = " + receipt.GetReceiptId() + System.Environment.NewLine;
                responseText += "TransType = " + receipt.GetTransType() + System.Environment.NewLine;
                responseText += "ReferenceNum = " + receipt.GetReferenceNum() + System.Environment.NewLine;
                responseText += "ResponseCode = " + receipt.GetResponseCode() + System.Environment.NewLine;
                responseText += "ISO = " + receipt.GetISO() + System.Environment.NewLine;
                responseText += "BankTotals = " + receipt.GetBankTotals() + System.Environment.NewLine;
                responseText += "Message = " + receipt.GetMessage() + System.Environment.NewLine;
                responseText += "AuthCode = " + receipt.GetAuthCode() + System.Environment.NewLine;
                responseText += "Complete = " + receipt.GetComplete() + System.Environment.NewLine;
                responseText += "TransDate = " + receipt.GetTransDate() + System.Environment.NewLine;
                responseText += "TransTime = " + receipt.GetTransTime() + System.Environment.NewLine;
                responseText += "Ticket = " + receipt.GetTicket() + System.Environment.NewLine;
                responseText += "TimedOut = " + receipt.GetTimedOut() + System.Environment.NewLine;
                responseText += "Avs Response = " + receipt.GetAvsResultCode() + System.Environment.NewLine;
                responseText += "Cvd Response = " + receipt.GetCvdResultCode() + System.Environment.NewLine;
                responseText += "ITD Response = " + receipt.GetITDResponse() + System.Environment.NewLine;
                responseText += "IsVisaDebit = " + receipt.GetIsVisaDebit() + System.Environment.NewLine;
                responseText += "---------End Moneris Response---------";

                // Check the response. If it is approved, we set the status to completed on the gift transaction and remove the payment method:
                // If it is less than 50 it is APPROVED. If it is greater than or equal to 50 it is DECLINED. If it is null the transaction encountered an error:
                if (receipt.GetResponseCode() != null)
                {
                    int responsCode;
                    if (int.TryParse(receipt.GetResponseCode(), out responsCode))
                    {
                        if (responsCode < 50)
                        {
                            // Set the transaction to completed:
                            setStatusCodeOnTransaction(giftTransaction, 844060000, localContext, service);

                            // Remove the payment method (if applicable):
                            removePaymentMethod(creditCard, localContext, service);
                        }
                        else
                        {
                            // Set the transaction to failed:
                            setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);

                            // Remove the payment method (if applicable):
                            removePaymentMethod(creditCard, localContext, service);
                        }
                    }
                    else
                    {
                        localContext.TracingService.Trace("Error: Response code is not a number = " + receipt.GetResponseCode());
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                        removePaymentMethod(creditCard, localContext, service);
                        return returnResponseCode;
                    }
                }

                localContext.TracingService.Trace("Creating response record with response: " + receipt.GetMessage());

                // Create response record/associate to gift.
                Entity responseRecord = new Entity("msnfp_response");
                responseRecord["msnfp_identifier"] = "Response for " + (string)giftTransaction["msnfp_name"];
                responseRecord["msnfp_response"] = responseText;
                responseRecord["msnfp_transactionid"] = new EntityReference("msnfp_transaction", (Guid)giftTransaction["msnfp_transactionid"]);
                Guid responseGUID = service.Create(responseRecord);

                // Now associate that to the transaction entity:
                if (responseGUID != null)
                {
                    localContext.TracingService.Trace("Response created (" + responseGUID + "). Linking response record to transaction.");
                    giftTransaction["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);

                    // If it is less than 50 it is APPROVED. If it is greater than or equal to 50 it is DECLINED. If it is null the transaction encountered an error:
                    if (receipt.GetResponseCode() != null)
                    {
                        int responsCode;
                        if (int.TryParse(receipt.GetResponseCode(), out responsCode))
                        {
                            if (responsCode < 50)
                            {
                                // Set the transaction ID:
                                localContext.TracingService.Trace("Setting msnfp_transactionidentifier = " + receipt.GetReferenceNum());
                                localContext.TracingService.Trace("Setting msnfp_transactionnumber = " + receipt.GetTxnNumber());
                                localContext.TracingService.Trace("Setting order_id = " + order_id);

                                giftTransaction["msnfp_transactionidentifier"] = receipt.GetReferenceNum();
                                giftTransaction["msnfp_transactionnumber"] = receipt.GetTxnNumber();
                                giftTransaction["msnfp_invoiceidentifier"] = order_id;
                                giftTransaction["msnfp_transactionresult"] = "Approved - " + responsCode;

                                // Set the card type based on the Moneris response code:
                                if (receipt.GetCardType() != null)
                                {
                                    localContext.TracingService.Trace("Card Type Response Code = " + receipt.GetCardType());
                                    switch (receipt.GetCardType())
                                    {
                                        case "M":
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060001);
                                            break;
                                        case "V":
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060000);
                                            break;
                                        case "AX":
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060004);
                                            break;
                                        case "NO":
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060008);
                                            break;
                                        case "D":
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060007);
                                            break;
                                        case "DC": // Diners Club
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060005);
                                            break;
                                        case "C1": // JCB
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                                            break;
                                        case "JCB": // JCB - Old
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                                            break;
                                        default:
                                            // Unknown:
                                            giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060010);
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                giftTransaction["msnfp_transactionresult"] = "Declined - " + responsCode;
                            }
                        }
                    }

                    // We remove the lookup on the gift transaction as well (if applicable):
                    try
                    {
                        creditCard = service.Retrieve("msnfp_paymentmethod", ((EntityReference)giftTransaction["msnfp_transaction_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode" }));
                        if (creditCard == null)
                        {
                            localContext.TracingService.Trace("Clear Payment Method lookup on this transaction.");
                            giftTransaction["msnfp_transaction_paymentmethodid"] = null;
                        }

                    }
                    catch (Exception ex)
                    {
                        localContext.TracingService.Trace("Could not find Payment Method. Clear Payment Method lookup on this transaction record.");
                        giftTransaction["msnfp_transaction_paymentmethodid"] = null;
                    }
                    localContext.TracingService.Trace("paymentmethod 3");
                    service.Update(giftTransaction);
                }

                localContext.TracingService.Trace("Setting return response code: " + receipt.GetResponseCode());
                returnResponseCode = receipt.GetResponseCode();

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Receipt Error: " + e.ToString());
                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                removePaymentMethod(creditCard, localContext, service);
            }

            return returnResponseCode;
        }
        #endregion


        #region Moneris - Recurring Vault Payment API Processing.
        private void processMonerisVaultTransaction(Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering processMonerisVaultTransaction().");
            Entity creditCard = null;
            Entity paymentProcessor = null;
            localContext.TracingService.Trace("Gathering transaction data from target id.");

            // Get the payment method:
            creditCard = getPaymentMethodForTransaction(giftTransaction, localContext, service);

            // Ensure this is a credit card:
            if (creditCard.Contains("msnfp_type"))
            {
                // Credit Card = 844060000:
                if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                    {
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    }
                    return;
                }
            }

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                removePaymentMethod(creditCard, localContext, service);

                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                return;
            }

            // Get the payment processor:
            paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, giftTransaction, localContext, service);

            // If they do not have a vault id, we need to add them to the vault. Otherwise we continue below:
            if (!creditCard.Contains("msnfp_authtoken") || creditCard["msnfp_authtoken"] == null)
            {
                localContext.TracingService.Trace("No data id found for customer. Attempting to process the payment and if successful create a new Moneris Vault profile with this transaction.");

                // Here we charge the card and do the AVS/CVD validation (CVD validation cannot be done when adding a new profile, so we do the transaction first):
                string responseCodeString = processMonerisOneTimeTransaction(giftTransaction, localContext, service);

                int responsCode;
                if (int.TryParse(responseCodeString, out responsCode))
                {
                    if (responsCode < 50)
                    {
                        localContext.TracingService.Trace("Response was Approved. Now add to vault.");
                        // It completed successfully, add this customer information to the vault (note that this DOES NOT charge the card):
                        addMonerisVaultProfile(giftTransaction, localContext, service);

                    }
                    else
                    {
                        // Otherwise, something went wrong so we exit:
                        localContext.TracingService.Trace("Response code: " + responseCodeString + ". Please check payment details. Exiting plugin.");
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                        return;
                    }
                }
            }
            else if (creditCard.Contains("msnfp_authtoken"))
            {
                localContext.TracingService.Trace("Data id found for customer.");
                localContext.TracingService.Trace("Data id: " + (string)creditCard["msnfp_authtoken"]);

                // Get the customer name:
                string cust_id = ((EntityReference)giftTransaction["msnfp_customerid"]).Id.ToString();

                // Fill in payment data:
                localContext.TracingService.Trace("Put gathered payment information into purchase object.");
                string order_id = Guid.NewGuid().ToString();
                string store_id = (string)paymentProcessor["msnfp_storeid"];
                string api_token = (string)paymentProcessor["msnfp_apikey"];
                string amount = ((Money)giftTransaction["msnfp_amount"]).Value.ToString();
                string processing_country_code = "CA";
                bool status_check = false;

                // Vault specific data:
                string data_key = (string)creditCard["msnfp_authtoken"];

                string crypt_type = "7"; // SSL
                string descriptor = "Created in Dynamics 365 on " + DateTime.UtcNow + "(UTC)";

                localContext.TracingService.Trace("Creating ResPurchaseCC object.");

                ResPurchaseCC resPurchaseCC = new ResPurchaseCC();
                resPurchaseCC.SetDataKey(data_key);
                resPurchaseCC.SetOrderId(order_id);
                resPurchaseCC.SetCustId(cust_id);
                resPurchaseCC.SetAmount(amount);
                resPurchaseCC.SetCryptType(crypt_type);
                resPurchaseCC.SetDynamicDescriptor(descriptor);

                // Address Verification Service (optional depending on settings):
                localContext.TracingService.Trace("Check for AVS Validation.");
                AvsInfo avsCheck = new AvsInfo();
                if (creditCard.Contains("msnfp_ccbrandcode"))
                {
                    // Note: AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).
                    if ((((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060000) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060002) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060001) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060003) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060008) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060004))
                    {
                        if (paymentProcessor.Contains("msnfp_avsvalidation"))
                        {
                            if ((bool)paymentProcessor["msnfp_avsvalidation"])
                            {
                                localContext.TracingService.Trace("AVS Validation = True");
                                if (giftTransaction.Contains("msnfp_customerid"))
                                {
                                    try
                                    {
                                        localContext.TracingService.Trace("Entering address information for AVS validation.");
                                        avsCheck = AssignAVSValidationFieldsFromPaymentMethod(giftTransaction, creditCard, avsCheck, localContext, service);
                                        resPurchaseCC.SetAvsInfo(avsCheck);
                                    }
                                    catch
                                    {
                                        localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                                        throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)giftTransaction["msnfp_customerid"]).Id);
                                    }
                                }
                                else
                                {
                                    localContext.TracingService.Trace("No Donor. Exiting plugin.");
                                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                                    throw new ArgumentNullException("msnfp_customerid");
                                }

                            }
                            else
                            {
                                localContext.TracingService.Trace("AVS Validation = False");
                            }
                        }
                    }
                    else
                    {
                        localContext.TracingService.Trace("Could not do AVS Validation as the card type is not supported. AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).");
                        localContext.TracingService.Trace("Payment Card type: " + ((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value.ToString());
                    }
                }
                else
                {
                    localContext.TracingService.Trace("Could not do AVS Validation as the card type is not assigned.");
                }


                HttpsPostRequest mpgReq = new HttpsPostRequest();
                mpgReq.SetProcCountryCode(processing_country_code);

                // Set the test mode from the payment processor variable:
                if (paymentProcessor.Contains("msnfp_testmode"))
                {
                    if ((bool)paymentProcessor["msnfp_testmode"])
                    {
                        localContext.TracingService.Trace("Test Mode is Enabled.");
                        mpgReq.SetTestMode(true);
                    }
                    else
                    {
                        localContext.TracingService.Trace("Test Mode is Disabled.");
                        mpgReq.SetTestMode(false);
                    }
                }
                else
                {
                    localContext.TracingService.Trace("Test Mode not set. Defaulting to test mode enabled.");
                    mpgReq.SetTestMode(true);
                }

                mpgReq.SetStoreId(store_id);
                mpgReq.SetApiToken(api_token);
                mpgReq.SetTransaction(resPurchaseCC);
                mpgReq.SetStatusCheck(status_check);

                localContext.TracingService.Trace("Sending request.");
                mpgReq.Send();
                localContext.TracingService.Trace("Request sent successfully.");

                try
                {
                    Receipt receipt = mpgReq.GetReceipt();
                    string responseText = "";

                    // Log the data in the trace log:
                    localContext.TracingService.Trace("---------Moneris Response---------");
                    localContext.TracingService.Trace("DataKey = " + receipt.GetDataKey()); // This key is stored in the Authorization Token field.
                    localContext.TracingService.Trace("ReceiptId = " + receipt.GetReceiptId());
                    localContext.TracingService.Trace("ReferenceNum = " + receipt.GetReferenceNum());
                    localContext.TracingService.Trace("ResponseCode = " + receipt.GetResponseCode());
                    localContext.TracingService.Trace("AuthCode = " + receipt.GetAuthCode());
                    localContext.TracingService.Trace("Message = " + receipt.GetMessage());
                    localContext.TracingService.Trace("TransDate = " + receipt.GetTransDate());
                    localContext.TracingService.Trace("TransTime = " + receipt.GetTransTime());
                    localContext.TracingService.Trace("TransType = " + receipt.GetTransType());
                    localContext.TracingService.Trace("Complete = " + receipt.GetComplete());
                    localContext.TracingService.Trace("TransAmount = " + receipt.GetTransAmount());
                    localContext.TracingService.Trace("CardType = " + receipt.GetCardType());
                    localContext.TracingService.Trace("TxnNumber = " + receipt.GetTxnNumber());
                    localContext.TracingService.Trace("TimedOut = " + receipt.GetTimedOut());
                    localContext.TracingService.Trace("ResSuccess = " + receipt.GetResSuccess());
                    localContext.TracingService.Trace("PaymentType = " + receipt.GetPaymentType());
                    localContext.TracingService.Trace("IsVisaDebit = " + receipt.GetIsVisaDebit());
                    localContext.TracingService.Trace("Avs Response = " + receipt.GetAvsResultCode());
                    localContext.TracingService.Trace("Cvd Response = " + receipt.GetCvdResultCode());

                    localContext.TracingService.Trace("---------Customer---------");
                    localContext.TracingService.Trace("Cust ID = " + receipt.GetResDataCustId());
                    localContext.TracingService.Trace("Phone = " + receipt.GetResDataPhone());
                    localContext.TracingService.Trace("Email = " + receipt.GetResDataEmail());
                    localContext.TracingService.Trace("Note = " + receipt.GetResDataNote());
                    localContext.TracingService.Trace("Masked Pan = " + receipt.GetResDataMaskedPan());
                    localContext.TracingService.Trace("Exp Date (YYMM) = " + receipt.GetResDataExpdate());
                    localContext.TracingService.Trace("Crypt Type = " + receipt.GetResDataCryptType());
                    localContext.TracingService.Trace("Avs Street Number = " + receipt.GetResDataAvsStreetNumber());
                    localContext.TracingService.Trace("Avs Street Name = " + receipt.GetResDataAvsStreetName());
                    localContext.TracingService.Trace("Avs Zipcode = " + receipt.GetResDataAvsZipcode());
                    localContext.TracingService.Trace("---------End Customer---------");
                    localContext.TracingService.Trace("---------End Moneris Response---------");

                    // Data dump the same response into the response entity record:
                    responseText += "---------Moneris Response---------" + System.Environment.NewLine;
                    responseText += "DataKey = " + receipt.GetDataKey() + System.Environment.NewLine;
                    responseText += "ReceiptId = " + receipt.GetReceiptId() + System.Environment.NewLine;
                    responseText += "ReferenceNum = " + receipt.GetReferenceNum() + System.Environment.NewLine;
                    responseText += "ResponseCode = " + receipt.GetResponseCode() + System.Environment.NewLine;
                    responseText += "AuthCode = " + receipt.GetAuthCode() + System.Environment.NewLine;
                    responseText += "Message = " + receipt.GetMessage() + System.Environment.NewLine;
                    responseText += "TransDate = " + receipt.GetTransDate() + System.Environment.NewLine;
                    responseText += "TransTime = " + receipt.GetTransTime() + System.Environment.NewLine;
                    responseText += "TransType = " + receipt.GetTransType() + System.Environment.NewLine;
                    responseText += "Complete = " + receipt.GetComplete() + System.Environment.NewLine;
                    responseText += "TransAmount = " + receipt.GetTransAmount() + System.Environment.NewLine;
                    responseText += "CardType = " + receipt.GetCardType() + System.Environment.NewLine;
                    responseText += "TxnNumber = " + receipt.GetTxnNumber() + System.Environment.NewLine;
                    responseText += "TimedOut = " + receipt.GetTimedOut() + System.Environment.NewLine;
                    responseText += "ResSuccess = " + receipt.GetResSuccess() + System.Environment.NewLine;
                    responseText += "PaymentType = " + receipt.GetPaymentType() + System.Environment.NewLine;
                    responseText += "IsVisaDebit = " + receipt.GetIsVisaDebit() + System.Environment.NewLine;
                    responseText += "Avs Response = " + receipt.GetAvsResultCode() + System.Environment.NewLine;
                    responseText += "Cvd Response = " + receipt.GetCvdResultCode() + System.Environment.NewLine;

                    responseText += "---------Customer---------" + System.Environment.NewLine;
                    responseText += "Cust ID = " + receipt.GetResDataCustId() + System.Environment.NewLine;
                    responseText += "Phone = " + receipt.GetResDataPhone() + System.Environment.NewLine;
                    responseText += "Email = " + receipt.GetResDataEmail() + System.Environment.NewLine;
                    responseText += "Note = " + receipt.GetResDataNote() + System.Environment.NewLine;
                    responseText += "Masked Pan = " + receipt.GetResDataMaskedPan() + System.Environment.NewLine;
                    responseText += "Exp Date (YYMM) = " + receipt.GetResDataExpdate() + System.Environment.NewLine;
                    responseText += "Crypt Type = " + receipt.GetResDataCryptType() + System.Environment.NewLine;
                    responseText += "Avs Street Number = " + receipt.GetResDataAvsStreetNumber() + System.Environment.NewLine;
                    responseText += "Avs Street Name = " + receipt.GetResDataAvsStreetName() + System.Environment.NewLine;
                    responseText += "Avs Zipcode = " + receipt.GetResDataAvsZipcode() + System.Environment.NewLine;
                    responseText += "---------End Customer---------" + System.Environment.NewLine;
                    responseText += "---------End Moneris Response---------" + System.Environment.NewLine;

                    localContext.TracingService.Trace("Creating response record with response: " + receipt.GetMessage());

                    // Check the response. If it is approved, we set the status to completed on the gift transaction:
                    // If it is less than 50 it is APPROVED. If it is greater than or equal to 50 it is DECLINED. If it is null the transaction encountered an error:
                    if (receipt.GetResponseCode() != null)
                    {
                        int responsCode;
                        if (int.TryParse(receipt.GetResponseCode(), out responsCode))
                        {
                            if (responsCode < 50)
                            {
                                // Set the transaction to completed:
                                setStatusCodeOnTransaction(giftTransaction, 844060000, localContext, service);
                            }
                            else
                            {
                                // Set the transaction to failed:
                                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("Error: Response code is not a number = " + receipt.GetResponseCode());
                            setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                            removePaymentMethod(creditCard, localContext, service);
                        }
                    }

                    // Create response record/associate to gift.
                    Entity responseRecord = new Entity("msnfp_response");
                    responseRecord["msnfp_identifier"] = "Response for " + (string)giftTransaction["msnfp_name"];
                    responseRecord["msnfp_response"] = responseText;
                    responseRecord["msnfp_transactionid"] = new EntityReference("msnfp_transaction", (Guid)giftTransaction["msnfp_transactionid"]);
                    Guid responseGUID = service.Create(responseRecord);

                    // Now associate that to the transaction entity:
                    if (responseGUID != null)
                    {
                        localContext.TracingService.Trace("Response created (" + responseGUID + "). Linking response record to transaction.");
                        giftTransaction["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);

                        // If it is less than 50 it is APPROVED. If it is greater than or equal to 50 it is DECLINED. If it is null the transaction encountered an error:
                        if (receipt.GetResponseCode() != null)
                        {
                            int responsCode;
                            if (int.TryParse(receipt.GetResponseCode(), out responsCode))
                            {
                                if (responsCode < 50)
                                {
                                    // Set the transaction ID:
                                    localContext.TracingService.Trace("Setting msnfp_transactionidentifier = " + receipt.GetReferenceNum());
                                    localContext.TracingService.Trace("Setting msnfp_transactionnumber = " + receipt.GetTxnNumber());
                                    localContext.TracingService.Trace("Setting order_id = " + order_id);

                                    giftTransaction["msnfp_transactionidentifier"] = receipt.GetReferenceNum();
                                    giftTransaction["msnfp_transactionnumber"] = receipt.GetTxnNumber();
                                    giftTransaction["msnfp_invoiceidentifier"] = order_id;
                                    giftTransaction["msnfp_transactionresult"] = "Approved - " + responsCode;

                                    // Set the card type based on the Moneris response code:
                                    if (receipt.GetCardType() != null)
                                    {
                                        localContext.TracingService.Trace("Card Type Response Code = " + receipt.GetCardType());
                                        switch (receipt.GetCardType())
                                        {
                                            case "M":
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060001);
                                                break;
                                            case "V":
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060000);
                                                break;
                                            case "AX":
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060004);
                                                break;
                                            case "NO":
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060008);
                                                break;
                                            case "D":
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060007);
                                                break;
                                            case "DC": // Diners Club
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060005);
                                                break;
                                            case "C1": // JCB
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                                                break;
                                            case "JCB": // JCB - Old
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                                                break;
                                            default:
                                                // Unknown:
                                                giftTransaction["msnfp_ccbrandcode"] = new OptionSetValue(844060010);
                                                break;
                                        }
                                    }
                                }
                                else if (responsCode > 50)
                                {
                                    giftTransaction["msnfp_transactionresult"] = "FAILED";
                                }
                            }
                        }

                        // We remove the lookup on the gift transaction as well (if applicable):
                        try
                        {
                            creditCard = service.Retrieve("msnfp_paymentmethod", ((EntityReference)giftTransaction["msnfp_transaction_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode" }));
                            if (creditCard == null)
                            {
                                localContext.TracingService.Trace("Clear Payment Method lookup on this transaction.");
                                giftTransaction["msnfp_transaction_paymentmethodid"] = null;
                            }

                        }
                        catch (Exception ex)
                        {
                            localContext.TracingService.Trace("Could not find Payment Method. Clear Payment Method lookup on this transaction record.");
                            localContext.TracingService.Trace(ex.ToString());
                            giftTransaction["msnfp_transaction_paymentmethodid"] = null;
                        }
                        localContext.TracingService.Trace("payment method 4");
                        service.Update(giftTransaction);
                    }
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace(e.ToString());
                }
            }
        }
        #endregion


        #region Moneris - First Time Vault Payment API Processing. This adds the profile so we can use the datakey in the future with processMonerisVaultTransaction.
        /// <summary>
        /// Add the customer/donor on the given transaction to the Moneris Vault with AVS validation (optional). This ONLY adds the customer with their credit card info (associated to the transaction) and does NOT charge the card.
        /// </summary>
        /// <param name="giftTransaction">The transaction entity with the associated customer information.</param>
        /// <param name="localContext">Used for trace logs.</param>
        /// <param name="service">Used for updating the payment information and retrieving records.</param>
        private void addMonerisVaultProfile(Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering addMonerisVaultProfile().");
            Entity creditCard = null;
            Entity paymentProcessor = null;
            localContext.TracingService.Trace("Gathering transaction data from target id.");

            // Get the payment method:
            creditCard = getPaymentMethodForTransaction(giftTransaction, localContext, service);

            // Ensure this is a credit card:
            if (creditCard.Contains("msnfp_type"))
            {
                // Credit Card = 844060000:
                if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                    {
                        setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    }
                    return;
                }
            }

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                removePaymentMethod(creditCard, localContext, service);

                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                return;
            }

            // Get the payment processor:
            paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, giftTransaction, localContext, service);


            // Fill in payment data:
            localContext.TracingService.Trace("Put gathered payment information into vault profile object.");
            string store_id = (string)paymentProcessor["msnfp_storeid"];
            string api_token = (string)paymentProcessor["msnfp_apikey"];
            string pan = (string)creditCard["msnfp_cclast4"];
            string expdate = (string)creditCard["msnfp_ccexpmmyy"]; //"2001"; //YYMM format from payment method. Note this is the OPPOSITE of most cards.
            string crypt = "7"; // SSL Site
            string processing_country_code = "CA";
            bool status_check = false;

            string firstTwo = expdate.Substring(0, 2);
            string lastTwo = expdate.Substring(2, 2);

            localContext.TracingService.Trace("Old Expiry format (MMYY):" + expdate);
            expdate = lastTwo + firstTwo;
            localContext.TracingService.Trace("Moneris Expiry format (YYMM):" + expdate);

            string phone = "";
            string email = "";
            string note = "Created in Dynamics 365 on " + DateTime.UtcNow + "(UTC)";
            string cust_id = ((EntityReference)giftTransaction["msnfp_customerid"]).Id.ToString();

            // Get the phone/email from the payment method:
            if (creditCard.Contains("msnfp_telephone1"))
            {
                phone = (string)creditCard["msnfp_telephone1"];
            }
            if (creditCard.Contains("msnfp_emailaddress1"))
            {
                email = (string)creditCard["msnfp_emailaddress1"];
            }

            ResAddCC resaddcc = new ResAddCC();
            resaddcc.SetPan(pan);
            resaddcc.SetExpDate(expdate);
            resaddcc.SetCryptType(crypt);
            resaddcc.SetCustId(cust_id);
            resaddcc.SetPhone(phone);
            resaddcc.SetEmail(email);
            resaddcc.SetNote(note);
            resaddcc.SetGetCardType("true");

            // Address Verification Service (optional depending on settings):
            AvsInfo avsCheck = new AvsInfo();
            localContext.TracingService.Trace("Check for AVS Validation.");
            if (creditCard.Contains("msnfp_ccbrandcode"))
            {
                // Note: AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).
                if ((((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060000) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060002) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060001) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060003) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060008) || (((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value == 844060004))
                {
                    if (paymentProcessor.Contains("msnfp_avsvalidation"))
                    {
                        if ((bool)paymentProcessor["msnfp_avsvalidation"])
                        {
                            localContext.TracingService.Trace("AVS Validation = True");
                            if (giftTransaction.Contains("msnfp_customerid"))
                            {
                                try
                                {
                                    localContext.TracingService.Trace("Entering address information for AVS validation.");
                                    avsCheck = AssignAVSValidationFieldsFromPaymentMethod(giftTransaction, creditCard, avsCheck, localContext, service);
                                    resaddcc.SetAvsInfo(avsCheck);
                                }
                                catch
                                {
                                    localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                                    throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)giftTransaction["msnfp_customerid"]).Id);
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("No Donor. Exiting plugin.");
                                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                                throw new ArgumentNullException("msnfp_customerid");
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("AVS Validation = False");
                        }
                    }
                }
                else
                {
                    localContext.TracingService.Trace("Could not do AVS Validation as the card type is not supported. AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).");
                    localContext.TracingService.Trace("Payment Card type: " + ((OptionSetValue)creditCard["msnfp_ccbrandcode"]).Value.ToString());
                }
            }
            else
            {
                localContext.TracingService.Trace("Could not do AVS Validation as the card type is not assigned.");
            }

            HttpsPostRequest mpgReq = new HttpsPostRequest();
            mpgReq.SetProcCountryCode(processing_country_code);

            // Set the test mode from the payment processor variable:
            if (paymentProcessor.Contains("msnfp_testmode"))
            {
                if ((bool)paymentProcessor["msnfp_testmode"])
                {
                    localContext.TracingService.Trace("Test Mode is Enabled.");
                    mpgReq.SetTestMode(true);
                }
                else
                {
                    localContext.TracingService.Trace("Test Mode is Disabled.");
                    mpgReq.SetTestMode(false);
                }
            }
            else
            {
                localContext.TracingService.Trace("Test Mode not set. Defaulting to test mode enabled.");
                mpgReq.SetTestMode(true);
            }

            mpgReq.SetStoreId(store_id);
            mpgReq.SetApiToken(api_token);
            mpgReq.SetTransaction(resaddcc);
            mpgReq.SetStatusCheck(status_check);

            localContext.TracingService.Trace("Attempting to create the new user profile in the Moneris Vault.");
            mpgReq.Send();

            try
            {
                Receipt receipt = mpgReq.GetReceipt();

                // Log the data in the trace log:
                localContext.TracingService.Trace("---------Moneris Response---------");
                localContext.TracingService.Trace("DataKey = " + receipt.GetDataKey());
                localContext.TracingService.Trace("ResponseCode = " + receipt.GetResponseCode());
                localContext.TracingService.Trace("Message = " + receipt.GetMessage());
                localContext.TracingService.Trace("TransDate = " + receipt.GetTransDate());
                localContext.TracingService.Trace("TransTime = " + receipt.GetTransTime());
                localContext.TracingService.Trace("Complete = " + receipt.GetComplete());
                localContext.TracingService.Trace("TimedOut = " + receipt.GetTimedOut());
                localContext.TracingService.Trace("ResSuccess = " + receipt.GetResSuccess());
                localContext.TracingService.Trace("PaymentType = " + receipt.GetPaymentType());
                localContext.TracingService.Trace("Cust ID = " + receipt.GetResDataCustId());
                localContext.TracingService.Trace("Phone = " + receipt.GetResDataPhone());
                localContext.TracingService.Trace("Email = " + receipt.GetResDataEmail());
                localContext.TracingService.Trace("Note = " + receipt.GetResDataNote());
                localContext.TracingService.Trace("MaskedPan = " + receipt.GetResDataMaskedPan());
                localContext.TracingService.Trace("Exp Date = " + receipt.GetResDataExpdate());
                localContext.TracingService.Trace("Crypt Type = " + receipt.GetResDataCryptType());
                localContext.TracingService.Trace("Avs Response = " + receipt.GetAvsResultCode());
                localContext.TracingService.Trace("Cvd Response = " + receipt.GetCvdResultCode());
                localContext.TracingService.Trace("Avs Street Number = " + receipt.GetResDataAvsStreetNumber());
                localContext.TracingService.Trace("Avs Street Name = " + receipt.GetResDataAvsStreetName());
                localContext.TracingService.Trace("Avs Zipcode = " + receipt.GetResDataAvsZipcode());
                localContext.TracingService.Trace("---------End Moneris Response---------");

                // Now we add the datakey from above into the auth token field on the payment method:
                try
                {
                    creditCard["msnfp_authtoken"] = receipt.GetDataKey();

                    if (receipt.GetDataKey().Length > 0)
                    {
                        creditCard["msnfp_cclast4"] = receipt.GetResDataMaskedPan();
                        localContext.TracingService.Trace("Masked Card Number and CVV");
                    }

                    service.Update(creditCard);
                    localContext.TracingService.Trace("Added token to payment method: " + creditCard["msnfp_authtoken"]);
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace("Error, could not assign data id to auth token. Data key: " + receipt.GetDataKey());
                    localContext.TracingService.Trace("Error: " + e.ToString());
                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    throw new ArgumentNullException("msnfp_authtoken");
                }

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error processing response from payment gateway. Exiting plugin.");
                localContext.TracingService.Trace("Error: " + e.ToString());
                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                throw new Exception("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.");
            }
        }
        #endregion

        #region Add or Update the Transaction with Azure
        private void AddOrUpdateThisRecordWithAzure(Entity giftTransaction, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Update the Transaction Record On Azure---------");

            string messageName = context.MessageName;
            string apiUrl = configurationRecord.GetAttributeValue<string>("msnfp_azure_webapiurl");

            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + giftTransaction["msnfp_transactionid"].ToString());

                MSNFP_Transaction donationObj = new MSNFP_Transaction();
                donationObj.TransactionId = (Guid)giftTransaction["msnfp_transactionid"];
                donationObj.Name = giftTransaction.Contains("msnfp_name") ? (string)giftTransaction["msnfp_name"] : string.Empty;
                localContext.TracingService.Trace("Title: " + donationObj.Name);

                if (giftTransaction.Contains("msnfp_customerid") && giftTransaction["msnfp_customerid"] != null)
                {
                    donationObj.CustomerId = ((EntityReference)giftTransaction["msnfp_customerid"]).Id;

                    // Set the CustomerIdType. 1 = Account, 2 = Contact:
                    if (((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName.ToLower() == "contact")
                    {
                        donationObj.CustomerIdType = 2;
                    }
                    else if (((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName.ToLower() == "account")
                    {
                        donationObj.CustomerIdType = 1;
                    }

                    localContext.TracingService.Trace("Got msnfp_customerid.");
                }
                else
                {
                    donationObj.CustomerId = null;
                    donationObj.CustomerIdType = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_customerid.");
                }

                if (giftTransaction.Contains("msnfp_firstname") && giftTransaction["msnfp_firstname"] != null)
                {
                    donationObj.FirstName = (string)giftTransaction["msnfp_firstname"];
                    localContext.TracingService.Trace("Got msnfp_firstname.");
                }
                else
                {
                    donationObj.FirstName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_firstname.");
                }

                if (giftTransaction.Contains("msnfp_lastname") && giftTransaction["msnfp_lastname"] != null)
                {
                    donationObj.LastName = (string)giftTransaction["msnfp_lastname"];
                    localContext.TracingService.Trace("Got msnfp_lastname.");
                }
                else
                {
                    donationObj.LastName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastname.");
                }

                if (giftTransaction.Contains("msnfp_billing_line1") && giftTransaction["msnfp_billing_line1"] != null)
                {
                    donationObj.BillingLine1 = (string)giftTransaction["msnfp_billing_line1"];
                    localContext.TracingService.Trace("Got msnfp_billing_line1.");
                }
                else
                {
                    donationObj.BillingLine1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line1.");
                }

                if (giftTransaction.Contains("msnfp_billing_line2") && giftTransaction["msnfp_billing_line2"] != null)
                {
                    donationObj.BillingLine2 = (string)giftTransaction["msnfp_billing_line2"];
                    localContext.TracingService.Trace("Got msnfp_billing_line2.");
                }
                else
                {
                    donationObj.BillingLine2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line2.");
                }

                if (giftTransaction.Contains("msnfp_billing_line3") && giftTransaction["msnfp_billing_line3"] != null)
                {
                    donationObj.BillingLine3 = (string)giftTransaction["msnfp_billing_line3"];
                    localContext.TracingService.Trace("Got msnfp_billing_line3.");
                }
                else
                {
                    donationObj.BillingLine3 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line3.");
                }

                if (giftTransaction.Contains("msnfp_billing_city") && giftTransaction["msnfp_billing_city"] != null)
                {
                    donationObj.BillingCity = (string)giftTransaction["msnfp_billing_city"];
                    localContext.TracingService.Trace("Got msnfp_billing_city.");
                }
                else
                {
                    donationObj.BillingCity = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_city.");
                }

                if (giftTransaction.Contains("msnfp_billing_stateorprovince") && giftTransaction["msnfp_billing_stateorprovince"] != null)
                {
                    donationObj.BillingStateorProvince = (string)giftTransaction["msnfp_billing_stateorprovince"];
                    localContext.TracingService.Trace("Got msnfp_billing_stateorprovince.");
                }
                else
                {
                    donationObj.BillingStateorProvince = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_stateorprovince.");
                }

                if (giftTransaction.Contains("msnfp_billing_country") && giftTransaction["msnfp_billing_country"] != null)
                {
                    donationObj.BillingCountry = (string)giftTransaction["msnfp_billing_country"];
                    localContext.TracingService.Trace("Got msnfp_billing_country");
                }
                else
                {
                    donationObj.BillingCountry = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_country.");
                }

                if (giftTransaction.Contains("msnfp_billing_postalcode") && giftTransaction["msnfp_billing_postalcode"] != null)
                {
                    donationObj.BillingPostalCode = (string)giftTransaction["msnfp_billing_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_billing_postalcode");
                }
                else
                {
                    donationObj.BillingPostalCode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_postalcode.");
                }

                if (giftTransaction.Contains("msnfp_chequenumber") && giftTransaction["msnfp_chequenumber"] != null)
                {
                    donationObj.ChequeNumber = (string)giftTransaction["msnfp_chequenumber"];
                    localContext.TracingService.Trace("Got msnfp_chequenumber");
                }
                else
                {
                    donationObj.ChequeNumber = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_chequenumber.");
                }

                if (giftTransaction.Contains("msnfp_chequewiredate") && giftTransaction["msnfp_chequewiredate"] != null)
                {
                    donationObj.ChequeWireDate = (DateTime)giftTransaction["msnfp_chequewiredate"];
                    localContext.TracingService.Trace("Got msnfp_chequewiredate");
                }
                else
                {
                    donationObj.ChequeWireDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_chequewiredate.");
                }

                if (giftTransaction.Contains("msnfp_transactionidentifier") && giftTransaction["msnfp_transactionidentifier"] != null)
                {
                    donationObj.TransactionIdentifier = (string)giftTransaction["msnfp_transactionidentifier"];
                    localContext.TracingService.Trace("Got msnfp_transactionidentifier.");
                }
                else
                {
                    donationObj.TransactionIdentifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionidentifier.");
                }

                if (giftTransaction.Contains("msnfp_transactionnumber") && giftTransaction["msnfp_transactionnumber"] != null)
                {
                    donationObj.TransactionNumber = (string)giftTransaction["msnfp_transactionnumber"];
                    localContext.TracingService.Trace("Got msnfp_transactionnumber.");
                }
                else
                {
                    donationObj.TransactionNumber = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionnumber.");
                }

                if (giftTransaction.Contains("msnfp_transactionresult") && giftTransaction["msnfp_transactionresult"] != null)
                {
                    donationObj.TransactionResult = (string)giftTransaction["msnfp_transactionresult"];
                    localContext.TracingService.Trace("Got msnfp_transactionresult.");
                }
                else
                {
                    donationObj.TransactionResult = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionresult.");
                }

                if (giftTransaction.Contains("msnfp_dataentryreference") && giftTransaction["msnfp_dataentryreference"] != null)
                {
                    donationObj.DataEntryReference = (string)giftTransaction["msnfp_dataentryreference"];
                    localContext.TracingService.Trace("Got msnfp_dataentryreference.");
                }
                else
                {
                    donationObj.DataEntryReference = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_dataentryreference.");
                }

                if (giftTransaction.Contains("msnfp_relatedconstituentid") && giftTransaction["msnfp_relatedconstituentid"] != null)
                {
                    donationObj.ConstituentId = ((EntityReference)giftTransaction["msnfp_relatedconstituentid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_relatedconstituentid.");
                }
                else
                {
                    donationObj.ConstituentId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_relatedconstituentid.");
                }

                if (giftTransaction.Contains("msnfp_membershipcategoryid") && giftTransaction["msnfp_membershipcategoryid"] != null)
                {
                    donationObj.MembershipId = ((EntityReference)giftTransaction["msnfp_membershipcategoryid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_membershipcategoryid.");
                }
                else
                {
                    donationObj.MembershipId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_membershipcategoryid.");
                }

                if (giftTransaction.Contains("msnfp_membershipinstanceid") && giftTransaction["msnfp_membershipinstanceid"] != null)
                {
                    donationObj.MembershipInstanceId = ((EntityReference)giftTransaction["msnfp_membershipinstanceid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_membershipinstanceid.");
                }
                else
                {
                    donationObj.MembershipInstanceId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_membershipinstanceid.");
                }

                //if (giftTransaction.Contains("msnfp_donationpageid") && giftTransaction["msnfp_donationpageid"] != null)
                //{
                //    donationObj.DonationPageId = ((EntityReference)giftTransaction["msnfp_donationpageid"]).Id;
                //    localContext.TracingService.Trace("Got msnfp_donationpageid.");
                //}
                //else
                //{
                //    donationObj.DonationPageId = null;
                //    localContext.TracingService.Trace("Did NOT find msnfp_donationpageid.");
                //}


                if (giftTransaction.Contains("msnfp_transactionfraudcode") && giftTransaction["msnfp_transactionfraudcode"] != null)
                {
                    donationObj.TransactionFraudCode = (string)giftTransaction["msnfp_transactionfraudcode"];
                    localContext.TracingService.Trace("Got msnfp_transactionfraudcode.");
                }
                else
                {
                    donationObj.TransactionFraudCode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionfraudcode.");
                }

                if (giftTransaction.Contains("msnfp_transaction_paymentmethodid") && giftTransaction["msnfp_transaction_paymentmethodid"] != null)
                {
                    donationObj.TransactionPaymentMethodId = ((EntityReference)giftTransaction["msnfp_transaction_paymentmethodid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_transaction_paymentmethodid");
                }
                else
                {
                    donationObj.TransactionPaymentMethodId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transaction_paymentmethodid");
                }


                if (giftTransaction.Contains("msnfp_amount_receipted") && giftTransaction["msnfp_amount_receipted"] != null)
                {
                    donationObj.AmountReceipted = ((Money)giftTransaction["msnfp_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_receipted");
                }
                else
                {
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_receipted");
                }

                if (giftTransaction.Contains("msnfp_amount_nonreceiptable") && giftTransaction["msnfp_amount_nonreceiptable"] != null)
                {
                    donationObj.AmountNonReceiptable = ((Money)giftTransaction["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable");
                }
                else
                {
                    donationObj.AmountNonReceiptable = null;
                }

                if (giftTransaction.Contains("msnfp_amount_tax") && giftTransaction["msnfp_amount_tax"] != null)
                {
                    donationObj.AmountTax = ((Money)giftTransaction["msnfp_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_tax");
                }
                else
                {
                    donationObj.AmountTax = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_tax");
                }

                if (giftTransaction.Contains("msnfp_bookdate") && giftTransaction["msnfp_bookdate"] != null)
                {
                    donationObj.BookDate = (DateTime)giftTransaction["msnfp_bookdate"];
                    localContext.TracingService.Trace("Got msnfp_bookdate");
                }
                else
                {
                    donationObj.BookDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bookdate");
                }

                if (giftTransaction.Contains("msnfp_daterefunded") && giftTransaction["msnfp_daterefunded"] != null)
                {
                    donationObj.DateRefunded = (DateTime)giftTransaction["msnfp_daterefunded"];
                    localContext.TracingService.Trace("Got msnfp_daterefunded");
                }
                else
                {
                    donationObj.DateRefunded = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_daterefunded");
                }

                if (giftTransaction.Contains("msnfp_originatingcampaignid") && giftTransaction["msnfp_originatingcampaignid"] != null)
                {
                    donationObj.OriginatingCampaignId = ((EntityReference)giftTransaction["msnfp_originatingcampaignid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_originatingcampaignid");
                }
                else
                {
                    donationObj.OriginatingCampaignId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_originatingcampaignid");
                }

                //if (giftTransaction.Contains("msnfp_campaignpageid") && giftTransaction["msnfp_campaignpageid"] != null)
                //{
                //    donationObj.CampaignPageId = ((EntityReference)giftTransaction["msnfp_campaignpageid"]).Id;
                //    localContext.TracingService.Trace("Got msnfp_campaignpageid");
                //}
                //else
                //{
                //    donationObj.CampaignPageId = null;
                //    localContext.TracingService.Trace("Did NOT find msnfp_campaignpageid");
                //}

                if (giftTransaction.Contains("msnfp_appealid") && giftTransaction["msnfp_appealid"] != null)
                {
                    donationObj.AppealId = ((EntityReference)giftTransaction["msnfp_appealid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_appealid");
                }
                else
                {
                    donationObj.AppealId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_appealid");
                }

                if (giftTransaction.Contains("msnfp_anonymous") && giftTransaction["msnfp_anonymous"] != null)
                {
                    donationObj.Anonymous = ((OptionSetValue)giftTransaction["msnfp_anonymous"]).Value;
                    localContext.TracingService.Trace("Got msnfp_anonymous: " + donationObj.Anonymous);
                }
                else
                {
                    donationObj.Anonymous = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_anonymous");
                }

                if (giftTransaction.Contains("msnfp_dataentrysource") && giftTransaction["msnfp_dataentrysource"] != null)
                {
                    donationObj.DataEntrySource = ((OptionSetValue)giftTransaction["msnfp_dataentrysource"]).Value;
                    localContext.TracingService.Trace("Got msnfp_dataentrysource");
                }
                else
                {
                    localContext.TracingService.Trace("Did NOT find msnfp_dataentrysource");
                }

                if (giftTransaction.Contains("msnfp_paymenttypecode") && giftTransaction["msnfp_paymenttypecode"] != null)
                {
                    donationObj.PaymentTypeCode = ((OptionSetValue)giftTransaction["msnfp_paymenttypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_paymenttypecode");
                }
                else
                {
                    donationObj.PaymentTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymenttypecode");
                }

                if (giftTransaction.Contains("msnfp_ccbrandcode") && giftTransaction["msnfp_ccbrandcode"] != null)
                {
                    donationObj.CcBrandCode = ((OptionSetValue)giftTransaction["msnfp_ccbrandcode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ccbrandcode");
                }
                else
                {
                    donationObj.CcBrandCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ccbrandcode");
                }

                if (giftTransaction.Contains("msnfp_ga_deliverycode") && giftTransaction["msnfp_ga_deliverycode"] != null)
                {
                    donationObj.GaDeliveryCode = ((OptionSetValue)giftTransaction["msnfp_ga_deliverycode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ga_deliverycode");
                }
                else
                {
                    donationObj.GaDeliveryCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ga_deliverycode");
                }

                if (giftTransaction.Contains("msnfp_tributecode") && giftTransaction["msnfp_tributecode"] != null)
                {
                    donationObj.TributeCode = ((OptionSetValue)giftTransaction["msnfp_tributecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_tributecode");
                }
                else
                {
                    donationObj.TributeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributecode");
                }

                if (giftTransaction.Contains("msnfp_tributename") && giftTransaction["msnfp_tributename"] != null)
                {
                    donationObj.TributeName = (string)giftTransaction["msnfp_tributename"];
                    localContext.TracingService.Trace("Got msnfp_tributename");
                }
                else
                {
                    donationObj.TributeName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributename");
                }

                if (giftTransaction.Contains("msnfp_ga_applicablecode") && giftTransaction["msnfp_ga_applicablecode"] != null)
                {
                    donationObj.GaApplicableCode = ((OptionSetValue)giftTransaction["msnfp_ga_applicablecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ga_applicablecode");
                }
                else
                {
                    donationObj.GaApplicableCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ga_applicablecode");
                }

                if (giftTransaction.Contains("msnfp_receiptpreferencecode") && giftTransaction["msnfp_receiptpreferencecode"] != null)
                {
                    donationObj.ReceiptPreferenceCode = ((OptionSetValue)giftTransaction["msnfp_receiptpreferencecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_receiptpreferencecode");
                }
                else
                {
                    donationObj.ReceiptPreferenceCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptpreferencecode");
                }

                if (giftTransaction.Contains("msnfp_ga_returnid") && giftTransaction["msnfp_ga_returnid"] != null)
                {
                    donationObj.GaReturnId = ((EntityReference)giftTransaction["msnfp_ga_returnid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_ga_returnid");
                }
                else
                {
                    donationObj.GaReturnId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ga_returnid");
                }

                if (giftTransaction.Contains("msnfp_donorcommitmentid") && giftTransaction["msnfp_donorcommitmentid"] != null)
                {
                    donationObj.DonorCommitmentId = ((EntityReference)giftTransaction["msnfp_donorcommitmentid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_donorcommitmentid");
                }
                else
                {
                    donationObj.DonorCommitmentId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_donorcommitmentid");
                }

                if (giftTransaction.Contains("msnfp_giftbatchid") && giftTransaction["msnfp_giftbatchid"] != null)
                {
                    donationObj.GiftBatchId = ((EntityReference)giftTransaction["msnfp_giftbatchid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_giftbatchid");
                }
                else
                {
                    donationObj.GiftBatchId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_giftbatchid");
                }

                if (giftTransaction.Contains("msnfp_telephone1") && giftTransaction["msnfp_telephone1"] != null)
                {
                    donationObj.Telephone1 = (string)giftTransaction["msnfp_telephone1"];
                    localContext.TracingService.Trace("Got msnfp_telephone1");
                }
                else
                {
                    donationObj.Telephone1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone1");
                }

                if (giftTransaction.Contains("msnfp_telephone2") && giftTransaction["msnfp_telephone2"] != null)
                {
                    donationObj.Telephone2 = (string)giftTransaction["msnfp_telephone2"];
                    localContext.TracingService.Trace("Got msnfp_telephone2");
                }
                else
                {
                    donationObj.Telephone2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone2");
                }

                if (giftTransaction.Contains("msnfp_tributeacknowledgement") && giftTransaction["msnfp_tributeacknowledgement"] != null)
                {
                    donationObj.TributeAcknowledgement = (string)giftTransaction["msnfp_tributeacknowledgement"];
                    localContext.TracingService.Trace("Got msnfp_tributeacknowledgement");
                }
                else
                {
                    donationObj.TributeAcknowledgement = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributeacknowledgement");
                }

                if (giftTransaction.Contains("msnfp_mobilephone") && giftTransaction["msnfp_mobilephone"] != null)
                {
                    donationObj.MobilePhone = (string)giftTransaction["msnfp_mobilephone"];
                    localContext.TracingService.Trace("Got msnfp_mobilephone");
                }
                else
                {
                    donationObj.MobilePhone = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_mobilephone");
                }

                if (giftTransaction.Contains("msnfp_thirdpartyreceipt") && giftTransaction["msnfp_thirdpartyreceipt"] != null)
                {
                    donationObj.ThirdPartyReceipt = (string)giftTransaction["msnfp_thirdpartyreceipt"];
                    localContext.TracingService.Trace("Got msnfp_thirdpartyreceipt");
                }
                else
                {
                    donationObj.ThirdPartyReceipt = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_thirdpartyreceipt");
                }

                if (giftTransaction.Contains("msnfp_emailaddress1") && giftTransaction["msnfp_emailaddress1"] != null)
                {
                    donationObj.Emailaddress1 = (string)giftTransaction["msnfp_emailaddress1"];
                    localContext.TracingService.Trace("Got msnfp_emailaddress1");
                }
                else
                {
                    donationObj.Emailaddress1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_emailaddress1");
                }

                if (giftTransaction.Contains("msnfp_organizationname") && giftTransaction["msnfp_organizationname"] != null)
                {
                    donationObj.OrganizationName = (string)giftTransaction["msnfp_organizationname"];
                    localContext.TracingService.Trace("Got msnfp_organizationname");
                }
                else
                {
                    donationObj.OrganizationName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_organizationname");
                }

                if (giftTransaction.Contains("msnfp_transactiondescription") && giftTransaction["msnfp_transactiondescription"] != null)
                {
                    donationObj.TransactionDescription = (string)giftTransaction["msnfp_transactiondescription"];
                    localContext.TracingService.Trace("Got msnfp_transactiondescription");
                }
                else
                {
                    donationObj.TransactionDescription = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactiondescription");
                }

                if (giftTransaction.Contains("msnfp_appraiser") && giftTransaction["msnfp_appraiser"] != null)
                {
                    donationObj.Appraiser = (string)giftTransaction["msnfp_appraiser"];
                    localContext.TracingService.Trace("Got msnfp_appraiser");
                }
                else
                {
                    donationObj.Appraiser = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_appraiser");
                }

                if (giftTransaction.Contains("msnfp_transaction_paymentscheduleid") && giftTransaction["msnfp_transaction_paymentscheduleid"] != null)
                {
                    donationObj.TransactionPaymentScheduleId = ((EntityReference)giftTransaction["msnfp_transaction_paymentscheduleid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_transaction_paymentscheduleid");
                }
                else
                {
                    donationObj.TransactionPaymentScheduleId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transaction_paymentscheduleid");
                }

                if (giftTransaction.Contains("msnfp_paymentprocessorid") && giftTransaction["msnfp_paymentprocessorid"] != null)
                {
                    donationObj.PaymentProcessorId = ((EntityReference)giftTransaction["msnfp_paymentprocessorid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentprocessorid");
                }
                else
                {
                    donationObj.PaymentProcessorId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentprocessorid");
                }

                if (giftTransaction.Contains("msnfp_packageid") && giftTransaction["msnfp_packageid"] != null)
                {
                    donationObj.PackageId = ((EntityReference)giftTransaction["msnfp_packageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_packageid");
                }
                else
                {
                    donationObj.PackageId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_packageid");
                }

                if (giftTransaction.Contains("msnfp_taxreceiptid") && giftTransaction["msnfp_taxreceiptid"] != null)
                {
                    donationObj.TaxReceiptId = ((EntityReference)giftTransaction["msnfp_taxreceiptid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_taxreceiptid");
                }
                else
                {
                    donationObj.TaxReceiptId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_taxreceiptid");
                }

                if (giftTransaction.Contains("msnfp_tributeid") && giftTransaction["msnfp_tributeid"] != null)
                {
                    donationObj.TributeId = ((EntityReference)giftTransaction["msnfp_tributeid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_tributeid");
                }
                else
                {
                    donationObj.TributeId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributeid");
                }

                if (giftTransaction.Contains("msnfp_invoiceidentifier") && giftTransaction["msnfp_invoiceidentifier"] != null)
                {
                    donationObj.InvoiceIdentifier = (string)giftTransaction["msnfp_invoiceidentifier"];
                    localContext.TracingService.Trace("Got msnfp_invoiceidentifier");
                }
                else
                {
                    donationObj.InvoiceIdentifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_invoiceidentifier");
                }

                if (giftTransaction.Contains("msnfp_tributemessage") && giftTransaction["msnfp_tributemessage"] != null)
                {
                    donationObj.TributeMessage = (string)giftTransaction["msnfp_tributemessage"];
                    localContext.TracingService.Trace("Got msnfp_tributemessage");
                }
                else
                {
                    donationObj.TributeMessage = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributemessage");
                }

                if (giftTransaction.Contains("msnfp_configurationid") && giftTransaction["msnfp_configurationid"] != null)
                {
                    donationObj.ConfigurationId = ((EntityReference)giftTransaction["msnfp_configurationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_configurationid");
                }
                else
                {
                    donationObj.ConfigurationId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_configurationid");
                }

                if (giftTransaction.Contains("msnfp_eventid") && giftTransaction["msnfp_eventid"] != null)
                {
                    donationObj.EventId = ((EntityReference)giftTransaction["msnfp_eventid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventid");
                }
                else
                {
                    donationObj.EventId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventid");
                }

                if (giftTransaction.Contains("msnfp_eventpackageid") && giftTransaction["msnfp_eventpackageid"] != null)
                {
                    donationObj.EventPackageId = ((EntityReference)giftTransaction["msnfp_eventpackageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventpackageid");
                }
                else
                {
                    donationObj.EventPackageId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventpackageid");
                }

                if (giftTransaction.Contains("msnfp_amount_membership") && giftTransaction["msnfp_amount_membership"] != null)
                {
                    donationObj.AmountMembership = ((Money)giftTransaction["msnfp_amount_membership"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_membership");
                }


                if (giftTransaction.Contains("msnfp_ref_amount_membership") && giftTransaction["msnfp_ref_amount_membership"] != null)
                {
                    donationObj.RefAmountMembership = ((Money)giftTransaction["msnfp_ref_amount_membership"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_membership");
                }

                if (giftTransaction.Contains("msnfp_ref_amount_nonreceiptable") && giftTransaction["msnfp_ref_amount_nonreceiptable"] != null)
                {
                    donationObj.RefAmountNonreceiptable = ((Money)giftTransaction["msnfp_ref_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_nonreceiptable");
                }
                else
                {
                    donationObj.RefAmountNonreceiptable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_nonreceiptable");
                }

                if (giftTransaction.Contains("msnfp_ref_amount_tax") && giftTransaction["msnfp_ref_amount_tax"] != null)
                {
                    donationObj.RefAmountTax = ((Money)giftTransaction["msnfp_ref_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_tax");
                }
                else
                {
                    donationObj.RefAmountTax = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_tax");
                }

                if (giftTransaction.Contains("msnfp_ref_amount") && giftTransaction["msnfp_ref_amount"] != null)
                {
                    donationObj.RefAmount = ((Money)giftTransaction["msnfp_ref_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount");
                }
                else
                {
                    donationObj.RefAmount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount");
                }

                if (giftTransaction.Contains("msnfp_amount_transfer") && giftTransaction["msnfp_amount_transfer"] != null)
                {
                    donationObj.AmountTransfer = ((Money)giftTransaction["msnfp_amount_transfer"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_transfer");
                }
                else
                {
                    donationObj.AmountTransfer = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_transfer");
                }

                if (giftTransaction.Contains("msnfp_ga_amount_claimed") && giftTransaction["msnfp_ga_amount_claimed"] != null)
                {
                    donationObj.GaAmountClaimed = ((Money)giftTransaction["msnfp_ga_amount_claimed"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ga_amount_claimed");
                }
                else
                {
                    donationObj.GaAmountClaimed = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ga_amount_claimed");
                }

                if (giftTransaction.Contains("msnfp_currentretry") && giftTransaction["msnfp_currentretry"] != null)
                {
                    donationObj.CurrentRetry = (int)giftTransaction["msnfp_currentretry"];
                    localContext.TracingService.Trace("Got msnfp_currentretry");
                }

                if (giftTransaction.Contains("msnfp_nextfailedretry") && giftTransaction["msnfp_nextfailedretry"] != null)
                {
                    donationObj.NextFailedRetry = (DateTime)giftTransaction["msnfp_nextfailedretry"];
                    localContext.TracingService.Trace("Got msnfp_nextfailedretry");
                }
                else
                {
                    donationObj.NextFailedRetry = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_nextfailedretry");
                }

                if (giftTransaction.Contains("msnfp_returneddate") && giftTransaction["msnfp_returneddate"] != null)
                {
                    donationObj.ReturnedDate = (DateTime)giftTransaction["msnfp_returneddate"];
                    localContext.TracingService.Trace("Got msnfp_returneddate");
                }
                else
                {
                    donationObj.ReturnedDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_returneddate");
                }

                if (giftTransaction.Contains("msnfp_receiveddate") && giftTransaction["msnfp_receiveddate"] != null)
                {
                    donationObj.ReceivedDate = (DateTime)giftTransaction["msnfp_receiveddate"];
                    localContext.TracingService.Trace("Got msnfp_receiveddate");
                }
                else
                {
                    donationObj.ReceivedDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiveddate");
                }

                if (giftTransaction.Contains("msnfp_lastfailedretry") && giftTransaction["msnfp_lastfailedretry"] != null)
                {
                    donationObj.LastFailedRetry = (DateTime)giftTransaction["msnfp_lastfailedretry"];
                    localContext.TracingService.Trace("Got msnfp_lastfailedretry");
                }
                else
                {
                    donationObj.LastFailedRetry = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastfailedretry");
                }

                if (giftTransaction.Contains("msnfp_validationdate") && giftTransaction["msnfp_validationdate"] != null)
                {
                    donationObj.ValidationDate = (DateTime)giftTransaction["msnfp_validationdate"];
                    localContext.TracingService.Trace("Got msnfp_ValidationDate");
                }
                else
                {
                    donationObj.ValidationDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ValidationDate");
                }

                if (giftTransaction.Contains("msnfp_validationperformed") && giftTransaction["msnfp_validationperformed"] != null)
                {
                    donationObj.ValidationPerformed = (bool)giftTransaction["msnfp_validationperformed"];
                    localContext.TracingService.Trace("Got msnfp_validationperformed");
                }
                else
                {
                    donationObj.ValidationPerformed = false;
                    localContext.TracingService.Trace("Did NOT find msnfp_validationperformed");
                }

                if (giftTransaction.Contains("msnfp_chargeoncreate") && giftTransaction["msnfp_chargeoncreate"] != null)
                {
                    donationObj.ChargeonCreate = (bool)giftTransaction["msnfp_chargeoncreate"];
                    localContext.TracingService.Trace("Got msnfp_chargeoncreate");
                }
                else
                {
                    donationObj.ChargeonCreate = false;
                    localContext.TracingService.Trace("Did NOT find msnfp_chargeoncreate");
                }

                if (giftTransaction.Contains("msnfp_amount") && giftTransaction["msnfp_amount"] != null)
                {
                    donationObj.Amount = ((Money)giftTransaction["msnfp_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount (total header)");
                }

                if (giftTransaction.Contains("msnfp_ref_amount_receipted") && giftTransaction["msnfp_ref_amount_receipted"] != null)
                {
                    donationObj.RefAmountReceipted = ((Money)giftTransaction["msnfp_ref_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_receipted");
                }
                else
                {
                    donationObj.RefAmountReceipted = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_receipted");
                }

                if (giftTransaction.Contains("transactioncurrencyid") && giftTransaction["transactioncurrencyid"] != null)
                {
                    donationObj.TransactionCurrencyId = ((EntityReference)giftTransaction["transactioncurrencyid"]).Id;
                    localContext.TracingService.Trace("Got TransactionCurrencyId.");
                }
                else
                {
                    donationObj.TransactionCurrencyId = null;
                    localContext.TracingService.Trace("Did NOT find TransactionCurrencyId.");
                }

                //if (giftTransaction.Contains("msnfp_bulkreceiptid") && giftTransaction["msnfp_bulkreceiptid"] != null)
                //{
                //    donationObj.BulkReceiptId = ((EntityReference)giftTransaction["msnfp_bulkreceiptid"]).Id;
                //    localContext.TracingService.Trace("Got BulkReceiptId.");
                //}
                //else
                //{
                //    donationObj.TransactionCurrencyId = null;
                //    localContext.TracingService.Trace("Did NOT find BulkReceiptId.");
                //}

                if (giftTransaction.Contains("owningbusinessunit") && giftTransaction["owningbusinessunit"] != null)
                {
                    donationObj.OwningBusinessUnitId = ((EntityReference)giftTransaction["owningbusinessunit"]).Id;
                    localContext.TracingService.Trace("Got OwningBusinessUnitId.");
                }
                else
                {
                    donationObj.OwningBusinessUnitId = null;
                    localContext.TracingService.Trace("Did NOT find OwningBusinessUnitId.");
                }

                if (giftTransaction.Contains("msnfp_depositdate") && giftTransaction["msnfp_depositdate"] != null)
                {
                    donationObj.DepositDate = (DateTime)giftTransaction["msnfp_depositdate"];
                    localContext.TracingService.Trace("Got DepositDate.");
                }
                else
                {
                    donationObj.DepositDate = null;
                    localContext.TracingService.Trace("Did NOT find DepositDate");
                }

                if (giftTransaction.Contains("msnfp_typecode") && giftTransaction["msnfp_typecode"] != null)
                {
                    donationObj.TypeCode = ((OptionSetValue)giftTransaction["msnfp_typecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_typecode");
                }
                else
                {
                    donationObj.TypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_typecode");
                }


                //if (giftTransaction.Contains("msnfp_employermatches") && giftTransaction["msnfp_employermatches"] != null)
                //{
                //    donationObj.EmployerMatches = (bool)giftTransaction["msnfp_employermatches"];
                //    localContext.TracingService.Trace("Got msnfp_employermatches.");
                //}
                //else
                //{
                //    donationObj.EmployerMatches = null;
                //    localContext.TracingService.Trace("Did NOT find msnfp_employermatches.");
                //}

                if (messageName == "Create")
                {
                    donationObj.CreatedOn = DateTime.UtcNow;
                }
                else if (giftTransaction.Contains("createdon") && giftTransaction["createdon"] != null)
                {
                    donationObj.CreatedOn = (DateTime)giftTransaction["createdon"];
                }
                else
                {
                    donationObj.CreatedOn = null;
                }

                donationObj.Response = new HashSet<MSNFP_Response>();
                donationObj.Refund = new HashSet<MSNFP_Refund>();

                donationObj.StatusCode = ((OptionSetValue)giftTransaction["statuscode"]).Value;
                donationObj.StateCode = ((OptionSetValue)giftTransaction["statecode"]).Value;

                if (messageName == "Delete")
                {
                    donationObj.Deleted = true;
                    donationObj.DeletedDate = DateTime.UtcNow;
                }
                else
                {
                    donationObj.Deleted = false;
                    donationObj.DeletedDate = null;
                }

                donationObj.SyncDate = DateTime.UtcNow;

                localContext.TracingService.Trace("JSON object created");

                if (messageName == "Create")
                {
                    apiUrl += "Transaction/CreateTransaction";
                }
                else if (messageName == "Update" || messageName == "Delete")
                {
                    apiUrl += "Transaction/UpdateTransaction";
                }

                MemoryStream ms = new MemoryStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Transaction));
                ser.WriteObject(ms, donationObj);
                byte[] json = ms.ToArray();
                ms.Close();

                var json1 = Encoding.UTF8.GetString(json, 0, json.Length);

                WebAPIClient client = new WebAPIClient();
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers["Padlock"] = (string)configurationRecord["msnfp_apipadlocktoken"];
                client.Encoding = UTF8Encoding.UTF8;

                localContext.TracingService.Trace("---------Preparing JSON---------");
                localContext.TracingService.Trace("Converted to json API URL : " + apiUrl);
                localContext.TracingService.Trace("JSON: " + json1);
                localContext.TracingService.Trace("---------End of Preparing JSON---------");
                localContext.TracingService.Trace("Sending data to Azure.");

                string fileContent = client.UploadString(apiUrl, json1);

                localContext.TracingService.Trace("Got response.");
                localContext.TracingService.Trace("Response: " + fileContent);
            }
        }

        #endregion


        #region Updating Event Package Totals
        private void UpdateEventPackageDonationTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventPackageDonationTotals---------");

            if (queriedEntityRecord.Contains("msnfp_eventpackageid"))
            {
                decimal valAmount = 0;
                Entity eventPackage = service.Retrieve("msnfp_eventpackage", ((EntityReference)queriedEntityRecord["msnfp_eventpackageid"]).Id, new ColumnSet(new string[] { "msnfp_eventpackageid", "msnfp_amount" }));

                List<Entity> donationList = (from a in orgSvcContext.CreateQuery("msnfp_transaction")
                                             where ((EntityReference)a["msnfp_eventpackageid"]).Id == eventPackage.Id
                                             && ((OptionSetValue)a["statuscode"]).Value == 844060000 //completed
                                             select a).ToList();

                foreach (Entity item in donationList)
                {
                    if (item.Contains("msnfp_amount") && item["msnfp_amount"] != null)
                        valAmount += ((Money)item["msnfp_amount"]).Value;
                }

                eventPackage["msnfp_sum_donations"] = donationList.Count(); // # donations
                eventPackage["msnfp_val_donations"] = new Money(valAmount); // $ donations

                //updating event package record
                service.Update(eventPackage);
            }
        }
        #endregion


        #region Updating Event Totals
        private void UpdateEventTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventTotals---------");

            if (queriedEntityRecord.Contains("msnfp_eventid"))
            {
                decimal valAmount = 0;
                Entity parentEvent = service.Retrieve("msnfp_event", ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id, new ColumnSet(new string[] { "msnfp_eventid", "msnfp_sum_donations", "msnfp_count_donations" }));

                List<Entity> donationList = (from a in orgSvcContext.CreateQuery("msnfp_transaction")
                                             where ((EntityReference)a["msnfp_eventid"]).Id == parentEvent.Id
                                             && ((OptionSetValue)a["statuscode"]).Value == 844060000 //completed
                                             select a).ToList();

                if (donationList.Count > 0)
                {
                    foreach (Entity item in donationList)
                    {
                        if (item.Contains("msnfp_amount") && item["msnfp_amount"] != null)
                            valAmount += ((Money)item["msnfp_amount"]).Value;
                    }
                }

                parentEvent["msnfp_count_donations"] = donationList.Count(); // # donations
                parentEvent["msnfp_sum_donations"] = new Money(valAmount); // $ donations

                decimal totalRevenue =
                    Utilities.CalculateEventTotalRevenue(parentEvent, service, orgSvcContext, localContext.TracingService);
                parentEvent["msnfp_sum_total"] = new Money(totalRevenue);

                // updating event record
                service.Update(parentEvent);
            }
        }
        #endregion



        #region Perform pledge matching (if applicable)
        private void AddPledgeFromPledgeMatchRecord(Entity giftRecord, Entity pledgeMatchRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            // If there is, we generate another donor commitment for the "Apply the pledge to" lookup and make the donor of this record the constituent on the pledge:
            localContext.TracingService.Trace("---------Entering AddPledgeFromPledgeMatchRecord()---------");

            if (pledgeMatchRecord.Contains("msnfp_appliestocode"))
            {
                // 844060000 == Pledge, which we don't want on TransactionGiftCreate (that is handled in PostDonorCommitmentCreate.cs).
                if (((OptionSetValue)pledgeMatchRecord["msnfp_appliestocode"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("msnfp_appliestocode applies to transaction create, value: " + ((OptionSetValue)pledgeMatchRecord["msnfp_appliestocode"]).Value.ToString());

                    // Default is 100%:
                    int percentage = pledgeMatchRecord.Contains("msnfp_percentage") ? (int)pledgeMatchRecord["msnfp_percentage"] : 100;
                    Money amount = (Money)giftRecord["msnfp_amount_receipted"];

                    // Calculate using the percentage:
                    Money mcrmAmount = new Money();
                    if (amount.Value == 0 || percentage == 0)
                        mcrmAmount.Value = 0;
                    else
                        mcrmAmount.Value = (amount.Value * percentage / 100);
                    localContext.TracingService.Trace("Commitment amount: " + mcrmAmount.Value.ToString());

                    // Now create the donor commitment record and apply all the field values from the original gift record:
                    var newDonorCommitment = new Entity("msnfp_donorcommitment");

                    if (pledgeMatchRecord.Contains("msnfp_customertoid"))
                    {
                        newDonorCommitment["msnfp_customerid"] = pledgeMatchRecord["msnfp_customertoid"]; //new EntityReference(donorEntity, donorGuid);
                        string customerType = ((EntityReference)pledgeMatchRecord["msnfp_customertoid"]).LogicalName;
                        Guid customerID = ((EntityReference)pledgeMatchRecord["msnfp_customertoid"]).Id;
                        Entity customer = null;
                        if (customerType == "contact")
                        {
                            customer = service.Retrieve(customerType, customerID, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "emailaddress1"));
                            newDonorCommitment["msnfp_firstname"] = customer.Contains("firstname") ? (string)customer["firstname"] : string.Empty;
                            newDonorCommitment["msnfp_lastname"] = customer.Contains("lastname") ? (string)customer["lastname"] : string.Empty;
                        }
                        else if (customerType == "account")
                        {
                            customer = service.Retrieve(customerType, customerID, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "emailaddress1"));
                        }

                        newDonorCommitment["msnfp_billing_line1"] = customer.Contains("address1_line1") ? (string)customer["address1_line1"] : string.Empty;
                        newDonorCommitment["msnfp_billing_line2"] = customer.Contains("address1_line2") ? (string)customer["address1_line2"] : string.Empty;
                        newDonorCommitment["msnfp_billing_line3"] = customer.Contains("address1_line3") ? (string)customer["address1_line3"] : string.Empty;
                        newDonorCommitment["msnfp_billing_city"] = customer.Contains("address1_city") ? (string)customer["address1_city"] : string.Empty;
                        newDonorCommitment["msnfp_billing_stateorprovince"] = customer.Contains("address1_stateorprovince") ? (string)customer["address1_stateorprovince"] : string.Empty;
                        newDonorCommitment["msnfp_billing_country"] = customer.Contains("address1_country") ? (string)customer["address1_country"] : string.Empty;
                        newDonorCommitment["msnfp_billing_postalcode"] = customer.Contains("address1_postalcode") ? (string)customer["address1_postalcode"] : string.Empty;
                        newDonorCommitment["msnfp_telephone1"] = customer.Contains("telephone1") ? (string)customer["telephone1"] : string.Empty;
                        newDonorCommitment["msnfp_emailaddress1"] = customer.Contains("emailaddress1") ? (string)customer["emailaddress1"] : string.Empty;
                    }

                    //newDonorCommitment["msnfp_name"] = "Pledge - " + Guid.NewGuid().ToString().ToUpper().Substring(0, 6); // Mirrors the web resource technique.
                    newDonorCommitment["msnfp_customerid"] = pledgeMatchRecord["msnfp_customertoid"];
                    newDonorCommitment["msnfp_totalamount"] = mcrmAmount;
                    newDonorCommitment["msnfp_totalamount_paid"] = new Money(0);
                    newDonorCommitment["msnfp_totalamount_balance"] = mcrmAmount;
                    newDonorCommitment["msnfp_bookdate"] = giftRecord.Contains("msnfp_bookdate") ? giftRecord["msnfp_bookdate"] : DateTime.Today;
                    newDonorCommitment["createdby"] = new EntityReference("systemuser", context.InitiatingUserId);


                    if (giftRecord.Contains("msnfp_originatingcampaignid"))
                    {
                        localContext.TracingService.Trace("Campaign ID: " + ((EntityReference)giftRecord["msnfp_originatingcampaignid"]).Id.ToString());
                        newDonorCommitment["msnfp_commitment_campaignid"] = new EntityReference("campaign", ((EntityReference)giftRecord["msnfp_originatingcampaignid"]).Id);
                    }

                    if (giftRecord.Contains("msnfp_appealid"))
                    {
                        localContext.TracingService.Trace("Appeal ID: " + ((EntityReference)giftRecord["msnfp_appealid"]).Id.ToString());
                        newDonorCommitment["msnfp_appealid"] = new EntityReference("msnfp_appeal", ((EntityReference)giftRecord["msnfp_appealid"]).Id);
                    }

                    if (giftRecord.Contains("msnfp_packageid"))
                    {
                        localContext.TracingService.Trace("Package ID: " + ((EntityReference)giftRecord["msnfp_packageid"]).Id.ToString());
                        newDonorCommitment["msnfp_packageid"] = new EntityReference("msnfp_package", ((EntityReference)giftRecord["msnfp_packageid"]).Id);
                    }

                    if (giftRecord.Contains("msnfp_designationid"))
                    {
                        localContext.TracingService.Trace("msnfp_designationid ID: " + ((EntityReference)giftRecord["msnfp_designationid"]).Id.ToString());
                        newDonorCommitment["msnfp_designationid"] = new EntityReference("msnfp_designation", ((EntityReference)giftRecord["msnfp_designationid"]).Id);

                        localContext.TracingService.Trace("msnfp_commitment_defaultdesignationid ID: " + ((EntityReference)giftRecord["msnfp_designationid"]).Id.ToString());
                        newDonorCommitment["msnfp_commitment_defaultdesignationid"] = new EntityReference("msnfp_designation", ((EntityReference)giftRecord["msnfp_designationid"]).Id);
                    }

                    newDonorCommitment["statuscode"] = new OptionSetValue(1); // Active

                    // Add the constituent (the donor who triggered this action):
                    if (giftRecord.Contains("msnfp_customerid"))
                    {
                        if (((EntityReference)giftRecord["msnfp_customerid"]).LogicalName == "contact")
                        {
                            localContext.TracingService.Trace("Constituent is a contact: " + ((EntityReference)giftRecord["msnfp_customerid"]).Id.ToString());
                            newDonorCommitment["msnfp_constituentid"] = new EntityReference("contact", ((EntityReference)giftRecord["msnfp_customerid"]).Id);
                        }
                    }

                    // Set the configuration record on the donor commitment:
                    if (giftRecord.Contains("msnfp_configurationid"))
                    {
                        localContext.TracingService.Trace("Configuration record id: " + ((EntityReference)giftRecord["msnfp_configurationid"]).Id.ToString());
                        newDonorCommitment["msnfp_configurationid"] = new EntityReference("msnfp_configuration", ((EntityReference)giftRecord["msnfp_configurationid"]).Id);
                    }

                    localContext.TracingService.Trace("Creating new donor commitment.");
                    service.Create(newDonorCommitment);
                    localContext.TracingService.Trace("New donor commitment created.");
                }
                else
                {
                    localContext.TracingService.Trace("This pledge match is not for Transactions, msnfp_appliestocode: " + ((OptionSetValue)pledgeMatchRecord["msnfp_appliestocode"]).Value.ToString());
                }
            }
            else
            {
                localContext.TracingService.Trace("No msnfp_appliestocode value found.");
            }
            localContext.TracingService.Trace("---------Exiting AddPledgeFromPledgeMatchRecord()---------");

        }
        #endregion


        #region Set Pledge Schedule to completed when all related pledges are completed.
        private void AttemptToSetPledgeScheduleToCompleted(Guid pledgeScheduleId, int statusCode, LocalPluginContext localContext, IOrganizationService service, OrganizationServiceContext orgSvcContext)
        {
            localContext.TracingService.Trace("---------Entering AttemptToSetPledgeScheduleToCompleted()---------");
            localContext.TracingService.Trace("Transaction status code: " + statusCode);
            // Get the schedule:
            ColumnSet cols;
            cols = new ColumnSet("msnfp_paymentscheduleid", "msnfp_name", "msnfp_scheduletypecode", "statuscode", "msnfp_totalamount_balance");

            Entity pledgeSchedule = service.Retrieve("msnfp_paymentschedule", pledgeScheduleId, cols);

            // Is this even a pledge schedule? If not we exit now (check msnfp_scheduletypecode for value 844060005).
            if (pledgeSchedule.Contains("msnfp_scheduletypecode"))
            {
                if (((OptionSetValue)pledgeSchedule["msnfp_scheduletypecode"]).Value == 844060005) // 844060005 == Pledge Schedule
                {
                    // It is a pledge schedule so continue.
                    localContext.TracingService.Trace("Parent Schedule is of type Pledge Schedule, checking to see if the parent is indeed complete.");
                }
                else
                {
                    // We quit as it is not a pledge schedule:
                    localContext.TracingService.Trace("Payment schedule id: " + pledgeScheduleId.ToString() + " is not a pledge schedule. Exiting function.");
                    return;
                }
            }
            else
            {
                // We quit as we don't know its type:
                localContext.TracingService.Trace("Could not find msnfp_scheduletypecode on payment schedule id: " + pledgeScheduleId.ToString() + ". Exiting function.");
                return;
            }

            // Get all donor commitments where the msnfp_parentscheduleid is the same as the pledgeScheduleId variable.
            // Here we specifically get the ones NOT equal to complete (844060000).
            List<Guid> donorCommitmentList = (from g in orgSvcContext.CreateQuery("msnfp_donorcommitment")
                                              where ((EntityReference)g["msnfp_parentscheduleid"]).Id == pledgeScheduleId
                                              && g["statuscode"] != new OptionSetValue(844060000)
                                              select g.Id).ToList();

            // If this list is length <= 1 (note that the current transaction is not included yet, so we check to see if it completed okay via the statusCode), then we are done with this pledge schedule as ALL child records are completed.
            if (donorCommitmentList.Count == 0 && statusCode == 844060000)
            {
                localContext.TracingService.Trace("Count is." + donorCommitmentList.Count);
                localContext.TracingService.Trace("Balance  is." + ((Money)pledgeSchedule["msnfp_totalamount_balance"]).Value);
                //if the total amount balance in payment schedule is m=not 0, the pledge schedule cannot be set as complete
                if (pledgeSchedule.Contains("msnfp_totalamount_balance") && ((Money)pledgeSchedule["msnfp_totalamount_balance"]).Value <= 0)
                {
                    // If they are all completed, we set the schedule to done:
                    localContext.TracingService.Trace("There are no active donor commitments for this pledge schedule. Setting status to Completed.");
                    pledgeSchedule["statuscode"] = new OptionSetValue(844060000);
                    service.Update(pledgeSchedule);
                    localContext.TracingService.Trace("Updated parent pledge schedule to Completed.");
                }
            }
            else
            {
                localContext.TracingService.Trace("There are " + donorCommitmentList.Count + " active donor commitments for this pledge schedule (" + pledgeScheduleId + "). Cannot set to completed yet.");
            }

            localContext.TracingService.Trace("---------Exiting AttemptToSetPledgeScheduleToCompleted()---------");
        }
        #endregion



        private void UpdateCustomerPrimaryMembership(Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service, OrganizationServiceContext orgSvcContext)
        {
            localContext.TracingService.Trace("---------Entering UpdateCustomerPrimaryMembership()---------");
            // If this is primary, update the customer accordingly:
            if (giftTransaction.Contains("msnfp_membershipinstanceid") && giftTransaction.Contains("msnfp_customerid"))
            {
                // Get the membership instance:
                ColumnSet cols;
                cols = new ColumnSet("msnfp_membershipid", "msnfp_primary", "msnfp_customer");

                Entity membershipInstance = service.Retrieve("msnfp_membership", ((EntityReference)giftTransaction["msnfp_membershipinstanceid"]).Id, cols);

                localContext.TracingService.Trace("Membership Instance: " + (Guid)membershipInstance["msnfp_membershipid"]);
                if (membershipInstance.Contains("msnfp_primary") && ((EntityReference)giftTransaction["msnfp_customerid"]).Id != Guid.Empty)
                {
                    if ((bool)membershipInstance["msnfp_primary"])
                    {
                        localContext.TracingService.Trace("Updating Primary Membership for Customer: " + ((EntityReference)giftTransaction["msnfp_customerid"]).Id);

                        // Get all primary membership instance ids:
                        List<Guid> primaryMembershipIds = (from s in orgSvcContext.CreateQuery("msnfp_membership")
                                                           where ((EntityReference)s["msnfp_customer"]).Id == ((EntityReference)giftTransaction["msnfp_customerid"]).Id
                                                           && (bool)s["msnfp_primary"] == true
                                                           select s.Id).ToList();

                        Entity thecustomer = null;
                        if (((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName.ToLower() == "contact")
                        {
                            // Set the donor to the transaction donor:
                            if (!membershipInstance.Contains("msnfp_customer"))
                            {
                                membershipInstance["msnfp_customer"] = new EntityReference("contact", ((EntityReference)giftTransaction["msnfp_customerid"]).Id);
                                service.Update(membershipInstance);
                            }
                            // Update the contact field:
                            ColumnSet customerCols = new ColumnSet("contactid", "msnfp_primarymembershipid");
                            thecustomer = service.Retrieve("contact", ((EntityReference)giftTransaction["msnfp_customerid"]).Id, customerCols);
                            thecustomer["msnfp_primarymembershipid"] = new EntityReference("msnfp_membership", ((EntityReference)giftTransaction["msnfp_membershipinstanceid"]).Id);
                            service.Update(thecustomer);
                            localContext.TracingService.Trace("Update Complete");

                            localContext.TracingService.Trace("Updating " + primaryMembershipIds.Count + " additional membership instance records to not primary.");
                            // Set all the others now to not primary:
                            foreach (Guid memid in primaryMembershipIds)
                            {
                                ColumnSet memcols = new ColumnSet("msnfp_membershipid", "msnfp_primary");
                                Entity curmembership = service.Retrieve("msnfp_membership", memid, memcols);
                                curmembership["msnfp_primary"] = false;
                                service.Update(curmembership);
                            }
                        }
                        else if (((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName.ToLower() == "account")
                        {
                            // Set the donor to the transaction donor:
                            if (!membershipInstance.Contains("msnfp_customer"))
                            {
                                membershipInstance["msnfp_customer"] = new EntityReference("account", ((EntityReference)giftTransaction["msnfp_customerid"]).Id);
                                service.Update(membershipInstance);
                            }
                            // Update the account field:
                            ColumnSet customerCols = new ColumnSet("accountid", "msnfp_primarymembershipid");
                            thecustomer = service.Retrieve("account", ((EntityReference)giftTransaction["msnfp_customerid"]).Id, customerCols);
                            thecustomer["msnfp_primarymembershipid"] = new EntityReference("msnfp_membership", ((EntityReference)giftTransaction["msnfp_membershipinstanceid"]).Id);
                            service.Update(thecustomer);
                            localContext.TracingService.Trace("Update Complete");

                            localContext.TracingService.Trace("Updating " + primaryMembershipIds.Count + " additional membership instance records to not primary.");
                            // Set all the others now to not primary:
                            foreach (Guid memid in primaryMembershipIds)
                            {
                                ColumnSet memcols = new ColumnSet("msnfp_membershipid", "msnfp_primary");
                                Entity curmembership = service.Retrieve("msnfp_membership", memid, memcols);
                                curmembership["msnfp_primary"] = false;
                                service.Update(curmembership);
                            }
                        }
                    }
                }
            }
            localContext.TracingService.Trace("---------Exiting UpdateCustomerPrimaryMembership()---------");
        }


        #region Helper Functions
        /// <summary>
        /// Delete the payment method. This is used after processing a one time transaction if "Is Resuable" (msnfp_IsReusable) on this payment method is set to false.
        /// </summary>
        /// <param name="paymentMethod">The payment method to be removed.</param>
        /// <param name="localContext">Used for tracing.</param>
        /// <param name="service">Used to delete the record.</param>
        private void removePaymentMethod(Entity paymentMethod, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("---------Attempting to delete payment method---------");
            if (paymentMethod == null)
            {
                localContext.TracingService.Trace("Payment Method does not exist, cannot remove.");
                return;
            }

            localContext.TracingService.Trace("Is Reusable Payment Method: " + (bool)paymentMethod["msnfp_isreusable"]);
            // Check and see if this is resuable, if so do not delete.
            if ((bool)paymentMethod["msnfp_isreusable"] == false)
            {
                localContext.TracingService.Trace("Payment Method is Not Reusable.");
                try
                {
                    localContext.TracingService.Trace("Deleting Payment Method Id: " + paymentMethod.Id);
                    service.Delete("msnfp_paymentmethod", paymentMethod.Id);
                    localContext.TracingService.Trace("Payment Method successfully removed. ");
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace("removePaymentMethod() Error: " + e.ToString());
                }
            }
            else
            {
                localContext.TracingService.Trace("Payment Method is Reusable. Ignoring Delete.");
            }
        }


        private void MaskStripeCreditCard(LocalPluginContext localContext, Entity primaryCreditCard, string cardId, string cardBrand, string customerId)
        {
            localContext.TracingService.Trace("Inside the method MaskStripeCreditCard. ");
            string updatedCCNumber = Regex.Replace(primaryCreditCard["msnfp_cclast4"].ToString(), "[0-9](?=[0-9]{4})", "X");
            primaryCreditCard["msnfp_cclast4"] = updatedCCNumber;

            // Set the card type based on the Stripe response code:
            if (cardBrand != null)
            {
                switch (cardBrand)
                {
                    case "MasterCard":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060001);
                        break;
                    case "Visa":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060000);
                        break;
                    case "American Express":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060004);
                        break;
                    case "Discover":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060008);
                        break;
                    case "Diners Club":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060005);
                        break;
                    case "UnionPay":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060009);
                        break;
                    case "JCB":
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                        break;
                    default:
                        // Unknown:
                        primaryCreditCard["msnfp_ccbrandcode"] = new OptionSetValue(844060010);
                        break;
                }
            }

            localContext.TracingService.Trace("CC Number : " + updatedCCNumber);

            primaryCreditCard["msnfp_authtoken"] = cardId;
            primaryCreditCard["msnfp_stripecustomerid"] = customerId;

            localContext.OrganizationService.Update(primaryCreditCard);

            localContext.TracingService.Trace("credit card record updated...MaskStripeCreditCard");
        }

        /// <summary>
        /// Used to quickly log and change the statuscode on a gift transaction. The benefit here over changing the code and triggering a service.Update() is the added logging and error handling.
        /// </summary>
        /// <param name="giftTransaction">The transaction to update.</param>
        /// <param name="statuscode">The status reason to update to. Status Reasons: Completed = 844060000, Failed = 844060003 </param>
        /// <param name="localContext">Used for tracing.</param>
        /// <param name="service">Used to delete the record.</param>
        private void setStatusCodeOnTransaction(Entity giftTransaction, int statuscode, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("---------Attempting to change transaction status.---------");
            if (giftTransaction == null)
            {
                localContext.TracingService.Trace("Transaction does not exist.");
                return;
            }

            try
            {
                localContext.TracingService.Trace("Set statuscode to: " + statuscode + " for transaction id: " + giftTransaction.Id);
                // Now set the transaction to statuscode completed:
                giftTransaction["statuscode"] = new OptionSetValue(statuscode);
                service.Update(giftTransaction);
                localContext.TracingService.Trace("Updated transaction status successfully.");
            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("setStatusCodeOnTransaction() Error: " + e.ToString());
            }
        }

        /// <summary>
        /// Get and return the payment method entity for the given transaction record.
        /// </summary>
        /// <param name="giftTransaction"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private Entity getPaymentMethodForTransaction(Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service)
        {
            Entity paymentMethodToReturn;
            // Get the payment method:
            if (giftTransaction.Contains("msnfp_transaction_paymentmethodid"))
            {
                paymentMethodToReturn = service.Retrieve("msnfp_paymentmethod", ((EntityReference)giftTransaction["msnfp_transaction_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_paymentmethodid", "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode", "msnfp_authtoken", "msnfp_telephone1", "msnfp_billing_line1", "msnfp_billing_postalcode", "msnfp_emailaddress1", "msnfp_stripecustomerid" }));
            }
            else
            {
                localContext.TracingService.Trace("No payment method (msnfp_transaction_paymentmethodid) on this transaction. Exiting plugin.");

                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                throw new ArgumentNullException("msnfp_transaction_paymentmethodid");
            }

            return paymentMethodToReturn;
        }


        /// <summary>
        /// Get and return the payment processor entity for the given payment method record.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="giftTransaction"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private Entity getPaymentProcessorForPaymentMethod(Entity paymentMethod, Entity giftTransaction, LocalPluginContext localContext, IOrganizationService service)
        {
            Entity paymentProcessorToReturn;
            // Get the payment processor:
            if (paymentMethod.Contains("msnfp_paymentprocessorid"))
            {
                paymentProcessorToReturn = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)paymentMethod["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_apikey", "msnfp_name", "msnfp_storeid", "msnfp_avsvalidation", "msnfp_cvdvalidation", "msnfp_testmode", "msnfp_stripeservicekey", "msnfp_iatsagentcode", "msnfp_iatspassword" }));
            }
            else
            {
                localContext.TracingService.Trace("No payment processor is assigned to this payment method. Exiting plugin.");
                removePaymentMethod(paymentMethod, localContext, service);
                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                throw new ArgumentNullException("msnfp_paymentprocessorid");
            }
            return paymentProcessorToReturn;
        }


        /// <summary>
        /// Assigns the address values from the payment method to the AVS Check (Moneris) and returns the assigned AvsInfo record.
        /// </summary>
        /// <param name="giftTransaction"></param>
        /// <param name="paymentMethod"></param>
        /// <param name="avsCheck"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private AvsInfo AssignAVSValidationFieldsFromPaymentMethod(Entity giftTransaction, Entity paymentMethod, AvsInfo avsCheck, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering AssignAVSValidationFieldsFromPaymentMethod().");
            try
            {
                // If the customer is missing any mandatory fields, immediately fail:
                if (!paymentMethod.Contains("msnfp_billing_line1") || !paymentMethod.Contains("msnfp_billing_postalcode"))
                {
                    localContext.TracingService.Trace("Donor (" + ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName + ") with id = " + ((EntityReference)giftTransaction["msnfp_customerid"]).Id + " is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    throw new Exception("Donor (" + ((EntityReference)giftTransaction["msnfp_customerid"]).LogicalName + ") with id = " + ((EntityReference)giftTransaction["msnfp_customerid"]).Id + " is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
                }

                // Getting the street number in this instance is not 100% reliable, as there could be no/bad data. In this case we should let the user know the data is incorrect.
                string[] address1Split = ((string)paymentMethod["msnfp_billing_line1"]).Split(' ');
                if (address1Split.Length <= 1)
                {
                    // Throw an error, as the field is not setup correctly:
                    localContext.TracingService.Trace("Could not split address for AVS Validation. Please ensure the Street 1 billing address on the payment method is in the form '123 Example Street'. Exiting plugin.");
                    setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                    throw new ArgumentNullException("msnfp_billing_line1");
                }

                string streetName = (string)paymentMethod["msnfp_billing_line1"];
                localContext.TracingService.Trace("Unformatted Street Name: " + streetName);

                // We need to remove the street number as they are seperated in the Moneris API post:
                streetName = streetName.Replace(address1Split[0], "").Trim(' ');

                localContext.TracingService.Trace("Formatted Street Name: " + streetName);
                localContext.TracingService.Trace("Formatted Street Number: " + address1Split[0]);

                avsCheck.SetAvsStreetNumber(address1Split[0]);
                avsCheck.SetAvsStreetName(streetName);
                avsCheck.SetAvsZipCode((string)paymentMethod["msnfp_billing_postalcode"]);
                // This is not required, but add it if available:
                if (paymentMethod.Contains("msnfp_emailaddress1"))
                {
                    avsCheck.SetAvsEmail((string)paymentMethod["msnfp_emailaddress1"]);
                }
                avsCheck.SetAvsShipMethod("G");

                if (paymentMethod.Contains("msnfp_telephone1"))
                {
                    avsCheck.SetAvsCustPhone((string)paymentMethod["msnfp_telephone1"]);
                }

                localContext.TracingService.Trace("Updated AVS Check variable successfully.");
            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("AssignAVSValidationFieldsFromPaymentMethod() Error: " + e.ToString());
                setStatusCodeOnTransaction(giftTransaction, 844060003, localContext, service);
                throw new Exception("AssignAVSValidationFieldsFromPaymentMethod() Error: " + e.ToString());
            }

            return avsCheck;
        }


        #region Update Receipt (If Applicable)
        /// <summary>
        /// Updates the status reason of the receipt (msnfp_taxreceiptid) of the given transaction id based on the transactions status reason (if applicable). If there is no receipt it will exit accordingly.
        /// </summary>
        /// <param name="transactionID">The transaction used to find the receipt to update (using msnfp_taxreceiptid lookup)</param>
        /// <param name="localContext">Used for trace logs.</param>
        /// <param name="service">Used for updating the receipt record.</param>
        private void UpdateTransactionReceiptStatus(Guid transactionID, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering UpdateTransactionReceiptStatus().");
            // Get the transaction and see if it has a receipt:
            ColumnSet transactionCols = new ColumnSet("msnfp_transactionid", "msnfp_taxreceiptid", "statuscode");
            Entity transactionToCheck = service.Retrieve("msnfp_transaction", transactionID, transactionCols);

            // If so, we see if it needs to be set to Void (Payment Failed) if the status of the gift is Failed:
            if (transactionToCheck.Contains("msnfp_taxreceiptid") && transactionToCheck.Contains("statuscode"))
            {
                localContext.TracingService.Trace("Transaction has a receipt.");
                // Get the transaction's receipt record:
                ColumnSet receiptCols = new ColumnSet("msnfp_receiptid", "statuscode");
                Entity receiptToCheck = service.Retrieve("msnfp_receipt", ((EntityReference)transactionToCheck["msnfp_taxreceiptid"]).Id, receiptCols);

                if (receiptToCheck.Contains("statuscode"))
                {
                    localContext.TracingService.Trace("Obtained Receipt, checking Transaction status reason.");
                    // Pending on the gifts status reason, we change the receipts accordingly 
                    // - If the Gift has a status of Failed, the receipt is to be set to Void (Payment Failed).
                    // - If the Gift has a status of Completed, the receipt is to be set to Issued.
                    if (((OptionSetValue)transactionToCheck["statuscode"]).Value == 844060000) // Completed
                    {
                        localContext.TracingService.Trace("Gift is Completed. Setting Receipt to Issued.");
                        receiptToCheck["statuscode"] = new OptionSetValue(1); // Issued
                    }
                    else if (((OptionSetValue)transactionToCheck["statuscode"]).Value == 844060003) // Failed
                    {
                        localContext.TracingService.Trace("Gift is set to Failed. Setting Receipt to Void (Payment Failed).");
                        receiptToCheck["statuscode"] = new OptionSetValue(844060002); // Void (Payment Failed)
                    }
                    localContext.TracingService.Trace("Saving receipt.");
                    service.Update(receiptToCheck);
                    localContext.TracingService.Trace("Saving complete.");
                }
            }
            else
            {
                localContext.TracingService.Trace("No receipt found.");
            }
            localContext.TracingService.Trace("Exiting UpdateTransactionReceiptStatus().");
        }
        #endregion


        #endregion
    }
}
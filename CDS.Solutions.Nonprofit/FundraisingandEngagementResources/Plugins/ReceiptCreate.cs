/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Plugins.PaymentProcesses;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using Plugins.AzureModels;

namespace Plugins
{
    public class ReceiptCreate : PluginBase
    {
        public ReceiptCreate(string unsecure, string secure)
            : base(typeof(ReceiptCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered ReceiptCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

            if (context.Depth > 2)
            {
                localContext.TracingService.Trace("Context.depth > 2. Exiting Plugin.");
                return;
            }

            Entity queriedEntityRecord = null;
            // Note target is used in Azure sync.
            Entity targetIncomingRecord;
            string messageName = context.MessageName;

            Entity configurationRecord = null;

            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            // Get the Configuration Record (Either from the User or from the Default Configuration Record
            configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering ReceiptCreate.cs Main Function---------");
                    localContext.TracingService.Trace("Message Name: " + messageName);

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_receipt", targetIncomingRecord.Id, GetColumnSet());
                    }

                    if (targetIncomingRecord != null)
                    {
                        // If the unique identifier hasn't been set, set it here:
                        SetReceiptIdentifier(targetIncomingRecord.Id, localContext, service, messageName);

                        // Sync this to Azure. Note we use the target here as we want all the columns:
                        if (messageName == "Create")
                        {
                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);
                        }
                        else if (messageName == "Update")
                        {
                            AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                        }
                    }
                    else
                    {
                        localContext.TracingService.Trace("Target record not found. Exiting workflow.");
                    }
                }

                // Delete if the message is delete. This is the primary delete entrypoint:
                if (messageName == "Delete")
                {
                    queriedEntityRecord = service.Retrieve("msnfp_receipt", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting ReceiptCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_receiptid","msnfp_identifier","msnfp_customerid","msnfp_expectedtaxcredit","msnfp_generatedorprinted","msnfp_lastdonationdate","msnfp_amount_nonreceiptable","msnfp_transactioncount","msnfp_preferredlanguagecode","msnfp_receiptnumber","msnfp_receiptgeneration","msnfp_receiptissuedate","msnfp_receiptstackid","msnfp_receiptstatus","msnfp_amount_receipted","msnfp_paymentscheduleid","msnfp_replacesreceiptid","msnfp_amount","transactioncurrencyid","msnfp_printed","msnfp_deliverycode","msnfp_emaildeliverystatuscode","statecode","statuscode","createdon");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Receipt"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_receiptid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_Receipt jsonDataObj = new MSNFP_Receipt();

                jsonDataObj.ReceiptId = (Guid)queriedEntityRecord["msnfp_receiptid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.Identifier = queriedEntityRecord.Contains("msnfp_identifier") ? (string)queriedEntityRecord["msnfp_identifier"] : string.Empty;
                localContext.TracingService.Trace("Title: " + jsonDataObj.Identifier);

                if (queriedEntityRecord.Contains("msnfp_customerid") && queriedEntityRecord["msnfp_customerid"] != null)
                {
                    jsonDataObj.CustomerId = ((EntityReference)queriedEntityRecord["msnfp_customerid"]).Id;

                    // Set the CustomerIdType. 1 = Account, 2 = Contact:
                    if (((EntityReference)queriedEntityRecord["msnfp_customerid"]).LogicalName.ToLower() == "contact")
                    {
                        jsonDataObj.CustomerIdType = 2;
                    }
                    else if (((EntityReference)queriedEntityRecord["msnfp_customerid"]).LogicalName.ToLower() == "account")
                    {
                        jsonDataObj.CustomerIdType = 1;
                    }

                    localContext.TracingService.Trace("Got msnfp_customerid.");
                }
                else
                {
                    jsonDataObj.CustomerId = null;
                    jsonDataObj.CustomerIdType = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_customerid.");
                }


                if (queriedEntityRecord.Contains("msnfp_expectedtaxcredit") && queriedEntityRecord["msnfp_expectedtaxcredit"] != null)
                {
                    jsonDataObj.ExpectedTaxCredit = ((Money)queriedEntityRecord["msnfp_expectedtaxcredit"]).Value;
                    localContext.TracingService.Trace("Got msnfp_expectedtaxcredit.");
                }
                else
                {
                    jsonDataObj.ExpectedTaxCredit = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_expectedtaxcredit.");
                }


                if (queriedEntityRecord.Contains("msnfp_generatedorprinted") && queriedEntityRecord["msnfp_generatedorprinted"] != null)
                {
                    jsonDataObj.GeneratedorPrinted = (double)queriedEntityRecord["msnfp_generatedorprinted"];
                    localContext.TracingService.Trace("Got msnfp_generatedorprinted.");
                }
                else
                {
                    jsonDataObj.GeneratedorPrinted = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_generatedorprinted.");
                }


                if (queriedEntityRecord.Contains("msnfp_lastdonationdate") && queriedEntityRecord["msnfp_lastdonationdate"] != null)
                {
                    jsonDataObj.LastDonationDate = (DateTime)queriedEntityRecord["msnfp_lastdonationdate"];
                    localContext.TracingService.Trace("Got msnfp_lastdonationdate.");
                }
                else
                {
                    jsonDataObj.LastDonationDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastdonationdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonReceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.AmountNonReceiptable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_nonreceiptable.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactioncount") && queriedEntityRecord["msnfp_transactioncount"] != null)
                {
                    jsonDataObj.TransactionCount = (int)queriedEntityRecord["msnfp_transactioncount"];
                    localContext.TracingService.Trace("Got msnfp_transactioncount.");
                }
                else
                {
                    jsonDataObj.TransactionCount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactioncount.");
                }


                if (queriedEntityRecord.Contains("msnfp_preferredlanguagecode") && queriedEntityRecord["msnfp_preferredlanguagecode"] != null)
                {
                    jsonDataObj.PreferredLanguageCode = ((OptionSetValue)queriedEntityRecord["msnfp_preferredlanguagecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_preferredlanguagecode.");
                }
                else
                {
                    jsonDataObj.PreferredLanguageCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_preferredlanguagecode.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptnumber") && queriedEntityRecord["msnfp_receiptnumber"] != null)
                {
                    jsonDataObj.ReceiptNumber = (string)queriedEntityRecord["msnfp_receiptnumber"];
                    localContext.TracingService.Trace("Got msnfp_receiptnumber.");
                }
                else
                {
                    jsonDataObj.ReceiptNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptnumber.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptgeneration") && queriedEntityRecord["msnfp_receiptgeneration"] != null)
                {
                    jsonDataObj.ReceiptGeneration = ((OptionSetValue)queriedEntityRecord["msnfp_receiptgeneration"]).Value;
                    localContext.TracingService.Trace("Got msnfp_receiptgeneration.");
                }
                else
                {
                    jsonDataObj.ReceiptGeneration = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptgeneration.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptissuedate") && queriedEntityRecord["msnfp_receiptissuedate"] != null)
                {
                    jsonDataObj.ReceiptIssueDate = (DateTime)queriedEntityRecord["msnfp_receiptissuedate"];
                    localContext.TracingService.Trace("Got msnfp_receiptissuedate.");
                }
                else
                {
                    jsonDataObj.ReceiptIssueDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptissuedate.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptstackid") && queriedEntityRecord["msnfp_receiptstackid"] != null)
                {
                    jsonDataObj.ReceiptStackId = ((EntityReference)queriedEntityRecord["msnfp_receiptstackid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_receiptstackid.");
                }
                else
                {
                    jsonDataObj.ReceiptStackId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptstackid.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptstatus") && queriedEntityRecord["msnfp_receiptstatus"] != null)
                {
                    jsonDataObj.ReceiptStatus = (string)queriedEntityRecord["msnfp_receiptstatus"];
                    localContext.TracingService.Trace("Got msnfp_receiptstatus.");
                }
                else
                {
                    jsonDataObj.ReceiptStatus = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptstatus.");
                }


                if (queriedEntityRecord.Contains("msnfp_amount_receipted") && queriedEntityRecord["msnfp_amount_receipted"] != null)
                {
                    jsonDataObj.AmountReceipted = ((Money)queriedEntityRecord["msnfp_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_receipted.");
                }
                else
                {
                    jsonDataObj.AmountReceipted = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_receipted.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentscheduleid") && queriedEntityRecord["msnfp_paymentscheduleid"] != null)
                {
                    jsonDataObj.PaymentScheduleId = ((EntityReference)queriedEntityRecord["msnfp_paymentscheduleid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentscheduleid.");
                }
                else
                {
                    jsonDataObj.PaymentScheduleId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentscheduleid.");
                }


                if (queriedEntityRecord.Contains("msnfp_replacesreceiptid") && queriedEntityRecord["msnfp_replacesreceiptid"] != null)
                {
                    jsonDataObj.ReplacesReceiptId = ((EntityReference)queriedEntityRecord["msnfp_replacesreceiptid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_replacesreceiptid.");
                }
                else
                {
                    jsonDataObj.ReplacesReceiptId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_replacesreceiptid.");
                }


                if (queriedEntityRecord.Contains("msnfp_replacesreceiptid") && queriedEntityRecord["msnfp_replacesreceiptid"] != null)
                {
                    jsonDataObj.ReplacesReceiptId = ((EntityReference)queriedEntityRecord["msnfp_replacesreceiptid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_replacesreceiptid.");
                }
                else
                {
                    jsonDataObj.ReplacesReceiptId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_replacesreceiptid.");
                }


                if (queriedEntityRecord.Contains("msnfp_amount") && queriedEntityRecord["msnfp_amount"] != null)
                {
                    jsonDataObj.Amount = ((Money)queriedEntityRecord["msnfp_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount.");
                }
                else
                {
                    jsonDataObj.Amount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount.");
                }

                if (queriedEntityRecord.Contains("transactioncurrencyid") && queriedEntityRecord["transactioncurrencyid"] != null)
                {
                    jsonDataObj.TransactionCurrencyId = ((EntityReference)queriedEntityRecord["transactioncurrencyid"]).Id;
                    localContext.TracingService.Trace("Got TransactionCurrencyId.");
                }
                else
                {
                    jsonDataObj.TransactionCurrencyId = null;
                    localContext.TracingService.Trace("Did NOT find TransactionCurrencyId.");
                }

                if (queriedEntityRecord.Contains("msnfp_printed") && queriedEntityRecord["msnfp_printed"] != null)
                {
                    jsonDataObj.Printed = (DateTime)queriedEntityRecord["msnfp_printed"];
                    localContext.TracingService.Trace("Got Printed.");
                }
                else
                {
                    jsonDataObj.Printed = null;
                    localContext.TracingService.Trace("Did NOT find Printed.");
                }

                if (queriedEntityRecord.Contains("msnfp_deliverycode") && queriedEntityRecord["msnfp_deliverycode"] != null)
                {
                    jsonDataObj.DeliveryCode = ((OptionSetValue)queriedEntityRecord["msnfp_deliverycode"]).Value;
                    localContext.TracingService.Trace("Got Delivery.");
                }
                else
                {
                    jsonDataObj.DeliveryCode = null;
                    localContext.TracingService.Trace("Did NOT find Delivery.");
                }

                if (queriedEntityRecord.Contains("msnfp_emaildeliverystatuscode") && queriedEntityRecord["msnfp_emaildeliverystatuscode"] != null)
                {
                    jsonDataObj.EmailDeliveryStatusCode = ((OptionSetValue)queriedEntityRecord["msnfp_emaildeliverystatuscode"]).Value;
                    localContext.TracingService.Trace("Got EmailDeliveryStatus.");
                }
                else
                {
                    jsonDataObj.EmailDeliveryStatusCode = null;
                    localContext.TracingService.Trace("Did NOT find EmailDeliveryStatus.");
                }

                if (queriedEntityRecord.Contains("statecode") && queriedEntityRecord["statecode"] != null)
                {
                    jsonDataObj.StateCode = ((OptionSetValue)queriedEntityRecord["statecode"]).Value;
                    localContext.TracingService.Trace("Got statecode.");
                }
                else
                {
                    jsonDataObj.StateCode = null;
                    localContext.TracingService.Trace("Did NOT find statecode.");
                }

                if (queriedEntityRecord.Contains("statuscode") && queriedEntityRecord["statuscode"] != null)
                {
                    jsonDataObj.StatusCode = ((OptionSetValue)queriedEntityRecord["statuscode"]).Value;
                    localContext.TracingService.Trace("Got statuscode.");
                }
                else
                {
                    jsonDataObj.StatusCode = null;
                    localContext.TracingService.Trace("Did NOT find statuscode.");
                }

                if (messageName == "Create")
                {
                    jsonDataObj.CreatedOn = DateTime.UtcNow;
                }
                else if (queriedEntityRecord.Contains("createdon") && queriedEntityRecord["createdon"] != null)
                {
                    jsonDataObj.CreatedOn = (DateTime)queriedEntityRecord["createdon"];
                }
                else
                {
                    jsonDataObj.CreatedOn = null;
                }

                jsonDataObj.SyncDate = DateTime.UtcNow;

                if (messageName == "Delete")
                {
                    jsonDataObj.Deleted = true;
                    jsonDataObj.DeletedDate = DateTime.UtcNow;
                }
                else
                {
                    jsonDataObj.Deleted = false;
                    jsonDataObj.DeletedDate = null;
                }

                jsonDataObj.PaymentSchedule = null;
                jsonDataObj.ReceiptStack = null;
                jsonDataObj.ReplacesReceipt = null;

                jsonDataObj.InverseReplacesReceipt = new HashSet<MSNFP_Receipt>();

                localContext.TracingService.Trace("JSON object created");

                if (messageName == "Create")
                {
                    apiUrl += entityName + "/Create" + entityName;
                }
                else if (messageName == "Update" || messageName == "Delete")
                {
                    apiUrl += entityName + "/Update" + entityName;
                }

                MemoryStream ms = new MemoryStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Receipt));
                ser.WriteObject(ms, jsonDataObj);
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
            else
            {
                localContext.TracingService.Trace("No API URL or Enable Portal Pages. Exiting workflow.");
            }
        }
        #endregion

        #region Set Receipt Unique Identifier (This is also in CreateReceiptOnGiftCreate.cs)
        private void SetReceiptIdentifier(Guid receiptID, LocalPluginContext localContext, IOrganizationService service, string messageName)
        {
            localContext.TracingService.Trace("Entering SetReceiptIdentifier().");
            Entity receiptStack = null;
            Guid? receiptStackId = null;
            string newReceiptNumber = string.Empty;
            string prefix = string.Empty;
            double currentRange = 0;
            int numberRange = 0;
            ColumnSet receiptCols = new ColumnSet("msnfp_receiptid", "msnfp_identifier", "msnfp_receiptstackid", "msnfp_receiptstatus");
            Entity receiptToModify = service.Retrieve("msnfp_receipt", receiptID, receiptCols);
            localContext.TracingService.Trace("Found receipt with id: " + receiptID.ToString());

            if (receiptToModify.Contains("msnfp_identifier") && messageName != "Create")
            {
                localContext.TracingService.Trace("Found receipt identifier: " + (string)receiptToModify["msnfp_identifier"]);
                // If there is a value already, abort:
                if (receiptToModify["msnfp_identifier"] != null || ((string)receiptToModify["msnfp_identifier"]).Length > 0)
                {
                    localContext.TracingService.Trace("Receipt already has identfier. Exiting SetReceiptIdentifier().");
                    return;
                }
            }

            if (receiptToModify.Contains("msnfp_receiptstackid"))
            {
                receiptStackId = receiptToModify.GetAttributeValue<EntityReference>("msnfp_receiptstackid").Id;
                localContext.TracingService.Trace("Found receipt stack.");
            }
            else
            {
                localContext.TracingService.Trace("No receipt stack found.");
            }

            if (receiptStackId != null)
            {
                // lock the Receipt Stack Entity before proceeding
                localContext.TracingService.Trace("Locking Receipt Stack record Id:" + receiptStackId);
                Entity receiptStackToLock = new Entity("msnfp_receiptstack", receiptStackId.Value);
                receiptStackToLock["msnfp_locked"] = true;
                service.Update(receiptStackToLock);
                localContext.TracingService.Trace("Receipt Stack record locked");

                // now we can proceed to get the new receipt identitifer
                receiptStack = service.Retrieve("msnfp_receiptstack", ((EntityReference)receiptToModify["msnfp_receiptstackid"]).Id, new ColumnSet("msnfp_receiptstackid", "msnfp_prefix", "msnfp_currentrange", "msnfp_numberrange"));

                localContext.TracingService.Trace("Obtaining prefix, current range and number range.");
                prefix = receiptStack.Contains("msnfp_prefix") ? (string)receiptStack["msnfp_prefix"] : string.Empty;
                currentRange = receiptStack.Contains("msnfp_currentrange") ? (double)receiptStack["msnfp_currentrange"] : 0;
                numberRange = receiptStack.Contains("msnfp_numberrange") ? ((OptionSetValue)receiptStack["msnfp_numberrange"]).Value : 0;

                if (numberRange == 844060000)//six digit
                {
                    localContext.TracingService.Trace("Number range : 6 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(6, '0');
                }
                else if (numberRange == 844060001)//eight digit
                {
                    localContext.TracingService.Trace("Number range : 8 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(8, '0');
                }
                else if (numberRange == 844060002)//ten digit
                {
                    localContext.TracingService.Trace("Number range : 10 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(10, '0');
                }
                else
                {
                    localContext.TracingService.Trace("Receipt number range unknown. msnfp_numberrange: " + numberRange.ToString());
                }

                localContext.TracingService.Trace("Receipt Number: " + newReceiptNumber);

                Entity receiptToUpdate = new Entity(receiptToModify.LogicalName, receiptToModify.Id);
                receiptToUpdate["msnfp_receiptnumber"] = newReceiptNumber;
                receiptToUpdate["msnfp_identifier"] = newReceiptNumber;

                if (messageName == "Create")
                {
                    if (!receiptToModify.Contains("msnfp_receiptstatus"))
                        receiptToUpdate["msnfp_receiptstatus"] = "Issued";
                }

                localContext.TracingService.Trace("Updating Receipt.");
                service.Update(receiptToUpdate);
                localContext.TracingService.Trace("Receipt Updated");

                // Update the receipt stacks current number by 1.
                localContext.TracingService.Trace("Now update the receipt stacks current number by 1.");
                Entity receiptStackToUpdate = new Entity("msnfp_receiptstack", receiptStackId.Value);
                receiptStackToUpdate["msnfp_currentrange"] = currentRange + 1;
                receiptStackToUpdate["msnfp_locked"] = false;
                service.Update(receiptStackToUpdate);
                localContext.TracingService.Trace("Updated Receipt Stack current range to: " + (currentRange + 1).ToString());
            }
            else
            {
                localContext.TracingService.Trace("No receipt stack found.");
            }

            localContext.TracingService.Trace("Exiting SetReceiptIdentifier().");

        }
        #endregion

    }
}

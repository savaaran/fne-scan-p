/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using Moneris;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using FundraisingandEngagement.StripeWebPayment.Model;
using FundraisingandEngagement.StripeWebPayment.Service;
using System.Xml;
using Plugins.PaymentProcesses;
using Plugins.AzureModels;

namespace Plugins
{
    public class RefundCreate : PluginBase
    {
        public RefundCreate(string unsecure, string secure)
            : base(typeof(RefundCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered RefundCreate.cs ---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Entity queriedEntityRecord = null;
            Entity configurationRecord = null;
            string messageName = context.MessageName;

            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            if (context.Depth > 1)
            {
                localContext.TracingService.Trace("Context.depth > 1. Exiting Plugin.");
                return;
            }
            // Get the Configuration Record (Either from the User or from the Default Configuration Record)
            configurationRecord = Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            // Note this is broken out as "context.InputParameters["Target"] is Entity" doesn't work for delete plugin calls:
            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering RefundCreate.cs Main Function---------");

                    Entity primaryRefund = (Entity)context.InputParameters["Target"];

                    if (primaryRefund.Contains("msnfp_transactionid"))
                    {
                        localContext.TracingService.Trace("Refund has an associated Transaction: " + ((EntityReference)primaryRefund["msnfp_transactionid"]).Id.ToString());

                        Guid giftID = ((EntityReference)primaryRefund["msnfp_transactionid"]).Id;

                        ColumnSet cols = new ColumnSet("msnfp_transactionid", "msnfp_customerid", "msnfp_amount", "msnfp_amount_receipted", "msnfp_paymenttypecode", "msnfp_amount_membership", "msnfp_amount_nonreceiptable", "msnfp_amount_tax", "msnfp_ref_amount", "msnfp_ref_amount_receipted", "msnfp_ref_amount_membership", "msnfp_ref_amount_nonreceiptable", "msnfp_ref_amount_tax", "msnfp_configurationid", "msnfp_transactionidentifier", "msnfp_transaction_paymentmethodid", "modifiedby", "statuscode", "msnfp_daterefunded", "msnfp_transactionresult", "msnfp_paymentprocessorid", "msnfp_invoiceidentifier", "msnfp_transactionnumber", "transactioncurrencyid");
                        Entity gift = service.Retrieve("msnfp_transaction", giftID, cols);

                        // This should always be the case on creates:
                        if (primaryRefund.Contains("msnfp_refundtypecode"))
                        {
                            Entity config = null;
                            if (gift.Contains("msnfp_configurationid"))
                            {
                                localContext.TracingService.Trace("Gift contains Configuration.");

                                config = (from c in orgSvcContext.CreateQuery("msnfp_configuration")
                                          where (Guid)c["msnfp_configurationid"] == ((EntityReference)gift["msnfp_configurationid"]).Id
                                          orderby c["createdon"] descending
                                          select c).FirstOrDefault();

                                localContext.TracingService.Trace("Configuration retrieved");
                            }
                            else
                            {
                                config = configurationRecord;
                            }

                            if (primaryRefund.Contains("msnfp_refundtypecode")
                                            && (((OptionSetValue)primaryRefund["msnfp_refundtypecode"]).Value == 844060002 || ((OptionSetValue)primaryRefund["msnfp_refundtypecode"]).Value == 844060003))//CreditCard or Bank (ACH)
                            {
                                localContext.TracingService.Trace("Refund type Credit Card or Bank Account. Refund Type: " + ((OptionSetValue)primaryRefund["msnfp_refundtypecode"]).Value.ToString());

                                // If the gift contains the payment processor, we use that:
                                if (gift.Contains("msnfp_paymentprocessorid"))
                                {
                                    localContext.TracingService.Trace("Getting the payment processor from the transaction record.");

                                    // Get the payment processor/gateway from the transaction:
                                    Entity paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)gift["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_paymentprocessorid", "msnfp_apikey", "msnfp_storeid", "msnfp_testmode", "msnfp_paymentgatewaytype", "msnfp_stripeservicekey", "msnfp_iatsagentcode", "msnfp_iatspassword" }));

                                    localContext.TracingService.Trace("Retrieved payment processor: " + ((Guid)paymentProcessor["msnfp_paymentprocessorid"]).ToString());

                                    // Moneris:
                                    if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                    {
                                        localContext.TracingService.Trace("Payment gateway Moneris.");
                                        RefundMonerisTransaction(gift, paymentProcessor, primaryRefund, config, localContext, service);
                                    }
                                    // Stripe
                                    else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                    {
                                        localContext.TracingService.Trace("Payment gateway Stripe.");
                                        RefundStripeTransaction(gift, paymentProcessor, primaryRefund, config, localContext, service);
                                    }
                                    // iATS
                                    else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                    {                                       
                                        localContext.TracingService.Trace("Payment gateway iATS.");
                                        RefundIatsTransaction(gift, paymentProcessor, primaryRefund, config, localContext, service);
                                    }
                                    // Refunding a credit/debit card without a payment gateway is not allowed. Throw error:
                                    else
                                    {
                                        localContext.TracingService.Trace("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");

                                        throw new Exception("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");
                                    }
                                }
                                // Since we are still attempting to use Debit/Credit see if there is a payment processor on the configuration:
                                else
                                {
                                    localContext.TracingService.Trace("No payment processor found on this transaction. Attempting to use Configuration Payment Processor.");

                                    if (config.Contains("msnfp_paymentprocessorid"))
                                    {
                                        localContext.TracingService.Trace("Getting the payment processor from the configuration record.");

                                        // Get the payment processor/gateway from the configuration. This is the same as above but uses the configuration instead of transactions payment processor:
                                        Entity paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)config["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_paymentprocessorid", "msnfp_apikey", "msnfp_storeid", "msnfp_testmode", "msnfp_paymentgatewaytype", "msnfp_stripeservicekey", "msnfp_iatsagentcode", "msnfp_iatspassword" }));

                                        localContext.TracingService.Trace("Retrieved payment processor: " + ((Guid)paymentProcessor["msnfp_paymentprocessorid"]).ToString());
                                        // Moneris:
                                        if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                        {
                                            localContext.TracingService.Trace("Payment gateway Moneris.");
                                            RefundMonerisTransaction(gift, paymentProcessor, primaryRefund, config, localContext, service);
                                        }
                                        // Stripe
                                        else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                        {
                                            localContext.TracingService.Trace("Payment gateway Stripe.");
                                            RefundStripeTransaction(gift, paymentProcessor, primaryRefund, config, localContext, service);
                                        }
                                        // iATS
                                        else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                        {
                                            localContext.TracingService.Trace("Payment gateway iATS.");
                                            RefundIatsTransaction(gift, paymentProcessor, primaryRefund, config, localContext, service);
                                        }
                                        // Refunding a credit/debit card without a payment gateway is not allowed. Throw error:
                                        else
                                        {
                                            localContext.TracingService.Trace("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");

                                            throw new Exception("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");
                                        }
                                    }
                                    else
                                    {
                                        // Refunding a credit/debit card without a payment processor is not allowed as we cannot use a gateway to refund the money. Throw error:
                                        localContext.TracingService.Trace("No payment processor found on this transaction or in the configuration. Failing refund. Aborting Plugin.");
                                        throw new Exception("Refund Failed: No payment processor found on this transaction or in the configuration. Please set the payment processor/gateway on the transaction or configuration, save and try the refund process again.");
                                    }
                                }
                            }
                            // Else the refund is not credit/debit so just refund/update the transaction record:
                            else
                            {
                                localContext.TracingService.Trace("Refund is not credit/debit, so we don't use a payment gateway and update just the refund/transaction records.");
                                SetTransactionAsRefunded(gift, primaryRefund, localContext, service);
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("Warning: No refund type code. This should not occur on create.");
                        }
                    }
                    else if (primaryRefund.Contains("msnfp_paymentid"))
                    {
                        localContext.TracingService.Trace("Refund has an associated Payment: " + ((EntityReference)primaryRefund["msnfp_paymentid"]).Id.ToString());

                        Guid paymentID = ((EntityReference)primaryRefund["msnfp_paymentid"]).Id;

                        ColumnSet cols = new ColumnSet("msnfp_paymentid", "msnfp_customerid", "msnfp_eventpackageid", "msnfp_amount", "msnfp_amount_balance", "msnfp_paymenttype", "msnfp_amount_refunded", "msnfp_configurationid", "msnfp_transactionidentifier", "msnfp_paymentmethodid", "modifiedby", "statuscode", "msnfp_daterefunded", "msnfp_transactionresult", "msnfp_paymentprocessorid", "msnfp_invoiceidentifier", "msnfp_transactionnumber", "transactioncurrencyid");
                        Entity payment = service.Retrieve("msnfp_payment", paymentID, cols);

                        // This should always be the case on creates:
                        if (primaryRefund.Contains("msnfp_refundtypecode"))
                        {
                            Entity config = null;
                            if (payment.Contains("msnfp_configurationid"))
                            {
                                localContext.TracingService.Trace("Payment contains Configuration.");

                                config = (from c in orgSvcContext.CreateQuery("msnfp_configuration")
                                          where (Guid)c["msnfp_configurationid"] == ((EntityReference)payment["msnfp_configurationid"]).Id
                                          orderby c["createdon"] descending
                                          select c).FirstOrDefault();

                                localContext.TracingService.Trace("Configuration retrieved");
                            }
                            else
                            {
                                config = configurationRecord;
                            }

                            if (primaryRefund.Contains("msnfp_refundtypecode")
                                            && (((OptionSetValue)primaryRefund["msnfp_refundtypecode"]).Value == 844060002 || ((OptionSetValue)primaryRefund["msnfp_refundtypecode"]).Value == 844060003))//CreditCard or Bank (ACH)
                            {
                                localContext.TracingService.Trace("Refund type Credit Card or Bank Account. Refund Type: " + ((OptionSetValue)primaryRefund["msnfp_refundtypecode"]).Value.ToString());

                                // If the payment contains the payment processor, we use that:
                                if (payment.Contains("msnfp_paymentprocessorid"))
                                {
                                    localContext.TracingService.Trace("Getting the payment processor from the payment record.");

                                    // Get the payment processor/gateway from the transaction:
                                    Entity paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)payment["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_paymentprocessorid", "msnfp_apikey", "msnfp_storeid", "msnfp_testmode", "msnfp_paymentgatewaytype", "msnfp_stripeservicekey", "msnfp_iatsagentcode", "msnfp_iatspassword" }));

                                    localContext.TracingService.Trace("Retrieved payment processor: " + ((Guid)paymentProcessor["msnfp_paymentprocessorid"]).ToString());

                                    // Moneris:
                                    if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                    {
                                        localContext.TracingService.Trace("Payment gateway Moneris.");
                                        RefundMonerisTransaction(payment, paymentProcessor, primaryRefund, config, localContext, service);
                                    }
                                    // Stripe
                                    else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                    {
                                        localContext.TracingService.Trace("Payment gateway Stripe.");
                                        RefundStripeTransaction(payment, paymentProcessor, primaryRefund, config, localContext, service);
                                    }
                                    // iATS
                                    else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                    {
                                        localContext.TracingService.Trace("Payment gateway iATS.");
                                        RefundIatsTransaction(payment, paymentProcessor, primaryRefund, config, localContext, service);
                                    }
                                    // Refunding a credit/debit card without a payment gateway is not allowed. Throw error:
                                    else
                                    {
                                        localContext.TracingService.Trace("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");

                                        throw new Exception("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");
                                    }
                                }
                                // Since we are still attempting to use Debit/Credit see if there is a payment processor on the configuration:
                                else
                                {
                                    localContext.TracingService.Trace("No payment processor found on this transaction. Attempting to use Configuration Payment Processor.");

                                    if (config.Contains("msnfp_paymentprocessorid"))
                                    {
                                        localContext.TracingService.Trace("Getting the payment processor from the configuration record.");

                                        // Get the payment processor/gateway from the configuration. This is the same as above but uses the configuration instead of transactions payment processor:
                                        Entity paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)config["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_paymentprocessorid", "msnfp_apikey", "msnfp_storeid", "msnfp_testmode", "msnfp_paymentgatewaytype", "msnfp_stripeservicekey", "msnfp_iatsagentcode", "msnfp_iatspassword" }));

                                        localContext.TracingService.Trace("Retrieved payment processor: " + ((Guid)paymentProcessor["msnfp_paymentprocessorid"]).ToString());
                                        // Moneris:
                                        if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                        {
                                            localContext.TracingService.Trace("Payment gateway Moneris.");
                                            RefundMonerisTransaction(payment, paymentProcessor, primaryRefund, config, localContext, service);
                                        }
                                        // Stripe
                                        else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                        {
                                            localContext.TracingService.Trace("Payment gateway Stripe.");
                                            RefundStripeTransaction(payment, paymentProcessor, primaryRefund, config, localContext, service);
                                        }
                                        // iATS
                                        else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                        {
                                            localContext.TracingService.Trace("Payment gateway iATS.");
                                            RefundIatsTransaction(payment, paymentProcessor, primaryRefund, config, localContext, service);
                                        }
                                        // Refunding a credit/debit card without a payment gateway is not allowed. Throw error:
                                        else
                                        {
                                            localContext.TracingService.Trace("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");

                                            throw new Exception("Refund Failed: No matching msnfp_paymentgatewaytype. Please refer to the payment processor and ensure a gateway type is selected, save and try the refund process again.");
                                        }
                                    }
                                    else
                                    {
                                        // Refunding a credit/debit card without a payment processor is not allowed as we cannot use a gateway to refund the money. Throw error:
                                        localContext.TracingService.Trace("No payment processor found on this transaction or in the configuration. Failing refund. Aborting Plugin.");
                                        throw new Exception("Refund Failed: No payment processor found on this transaction or in the configuration. Please set the payment processor/gateway on the transaction or configuration, save and try the refund process again.");
                                    }
                                }
                            }
                            // Else the refund is not credit/debit so just refund/update the transaction record:
                            else
                            {
                                localContext.TracingService.Trace("Refund is not credit/debit, so we don't use a payment gateway and update just the refund/transaction records.");
                                SetTransactionAsRefundedPayment(payment, primaryRefund, localContext, service);
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("Warning: No refund type code. This should not occur on create.");
                        }
                    }


                    // Update this on Azure:
                    if (messageName == "Update" || messageName == "Create")
                    {
                        if (messageName == "Update")
                        {
                            queriedEntityRecord = service.Retrieve("msnfp_refund", primaryRefund.Id, GetColumnSet());
                        }

                        if (primaryRefund != null)
                        {
                            // Sync this to Azure. Note we use the target here as we want all the columns:
                            if (messageName == "Create")
                            {
                                AddOrUpdateThisRecordWithAzure(primaryRefund, configurationRecord, localContext, service, context);
                            }
                            else if (messageName == "Update")
                            {
                                AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("Target record not found.");
                        }
                    }
                }

                // Delete if the message is delete. This is the primary delete entrypoint:
                if (messageName == "Delete")
                {
                    queriedEntityRecord = service.Retrieve("msnfp_refund", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting RefundCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_refundid","msnfp_identifier","msnfp_customerid","msnfp_amount_receipted","msnfp_amount_membership","msnfp_ref_amount_membership","msnfp_amount_nonreceiptable","msnfp_ref_amount_nonreceiptable","msnfp_ref_amount_receipted","msnfp_amount_tax","msnfp_ref_amount_tax","msnfp_chequenumber","msnfp_transactionid", "msnfp_paymentid","msnfp_bookdate","msnfp_receiveddate","msnfp_refundtypecode","msnfp_amount","msnfp_ref_amount","msnfp_transactionidentifier","msnfp_transactiionresult","transactioncurrencyid","statecode","statuscode","createdon");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Refund"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_refundid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_Refund jsonDataObj = new MSNFP_Refund();

                jsonDataObj.RefundId = (Guid)queriedEntityRecord["msnfp_refundid"];

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


                if (queriedEntityRecord.Contains("msnfp_amount_membership") && queriedEntityRecord["msnfp_amount_membership"] != null)
                {
                    jsonDataObj.AmountMembership = ((Money)queriedEntityRecord["msnfp_amount_membership"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_membership.");
                }
                else
                {
                    jsonDataObj.AmountMembership = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_membership.");
                }


                if (queriedEntityRecord.Contains("msnfp_ref_amount_membership") && queriedEntityRecord["msnfp_ref_amount_membership"] != null)
                {
                    jsonDataObj.RefAmountMembership = ((Money)queriedEntityRecord["msnfp_ref_amount_membership"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_membership.");
                }
                else
                {
                    jsonDataObj.RefAmountMembership = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_membership.");
                }


                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonReceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.AmountNonReceiptable = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_nonreceiptable.");
                }


                if (queriedEntityRecord.Contains("msnfp_ref_amount_nonreceiptable") && queriedEntityRecord["msnfp_ref_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.RefAmountNonreceiptable = ((Money)queriedEntityRecord["msnfp_ref_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.RefAmountNonreceiptable = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_nonreceiptable.");
                }


                if (queriedEntityRecord.Contains("msnfp_ref_amount_receipted") && queriedEntityRecord["msnfp_ref_amount_receipted"] != null)
                {
                    jsonDataObj.RefAmountReceipted = ((Money)queriedEntityRecord["msnfp_ref_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_receipted.");
                }
                else
                {
                    jsonDataObj.RefAmountReceipted = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_receipted.");
                }


                if (queriedEntityRecord.Contains("msnfp_amount_tax") && queriedEntityRecord["msnfp_amount_tax"] != null)
                {
                    jsonDataObj.AmountTax = ((Money)queriedEntityRecord["msnfp_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_tax.");
                }
                else
                {
                    jsonDataObj.AmountTax = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_tax.");
                }


                if (queriedEntityRecord.Contains("msnfp_ref_amount_tax") && queriedEntityRecord["msnfp_ref_amount_tax"] != null)
                {
                    jsonDataObj.RefAmountTax = ((Money)queriedEntityRecord["msnfp_ref_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_tax.");
                }
                else
                {
                    jsonDataObj.RefAmountTax = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_tax.");
                }


                if (queriedEntityRecord.Contains("msnfp_chequenumber") && queriedEntityRecord["msnfp_chequenumber"] != null)
                {
                    jsonDataObj.ChequeNumber = (string)queriedEntityRecord["msnfp_chequenumber"];
                    localContext.TracingService.Trace("Got msnfp_chequenumber.");
                }
                else
                {
                    jsonDataObj.ChequeNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_chequenumber.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactionid") && queriedEntityRecord["msnfp_transactionid"] != null)
                {
                    jsonDataObj.TransactionId = ((EntityReference)queriedEntityRecord["msnfp_transactionid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_transactionid.");
                }
                else
                {
                    jsonDataObj.TransactionId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionid.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentid") && queriedEntityRecord["msnfp_paymentid"] != null)
                {
                    jsonDataObj.PaymentId = ((EntityReference)queriedEntityRecord["msnfp_paymentid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentid.");
                }
                else
                {
                    jsonDataObj.PaymentId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentid.");
                }


                if (queriedEntityRecord.Contains("msnfp_bookdate") && queriedEntityRecord["msnfp_bookdate"] != null)
                {
                    jsonDataObj.BookDate = (DateTime)queriedEntityRecord["msnfp_bookdate"];
                    localContext.TracingService.Trace("Got msnfp_bookdate.");
                }
                else
                {
                    jsonDataObj.BookDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bookdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiveddate") && queriedEntityRecord["msnfp_receiveddate"] != null)
                {
                    jsonDataObj.ReceivedDate = (DateTime)queriedEntityRecord["msnfp_receiveddate"];
                    localContext.TracingService.Trace("Got msnfp_receiveddate.");
                }
                else
                {
                    jsonDataObj.ReceivedDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiveddate.");
                }


                if (queriedEntityRecord.Contains("msnfp_refundtypecode") && queriedEntityRecord["msnfp_refundtypecode"] != null)
                {
                    jsonDataObj.RefundTypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_refundtypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_refundtypecode.");
                }
                else
                {
                    jsonDataObj.RefundTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_refundtypecode.");
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


                if (queriedEntityRecord.Contains("msnfp_ref_amount") && queriedEntityRecord["msnfp_ref_amount"] != null)
                {
                    jsonDataObj.RefAmount = ((Money)queriedEntityRecord["msnfp_ref_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount.");
                }
                else
                {
                    jsonDataObj.RefAmount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactionidentifier") && queriedEntityRecord["msnfp_transactionidentifier"] != null)
                {
                    jsonDataObj.TransactionIdentifier = (string)queriedEntityRecord["msnfp_transactionidentifier"];
                    localContext.TracingService.Trace("Got msnfp_transactionidentifier.");
                }
                else
                {
                    jsonDataObj.TransactionIdentifier = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionidentifier.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactiionresult") && queriedEntityRecord["msnfp_transactiionresult"] != null)
                {
                    jsonDataObj.TransactionResult = (string)queriedEntityRecord["msnfp_transactiionresult"];
                    localContext.TracingService.Trace("Got msnfp_transactiionresult.");
                }
                else
                {
                    jsonDataObj.TransactionResult = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactiionresult.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Refund));
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


        #region Refund the gift using iATS.
        private void RefundIatsTransaction(Entity refundEntity, Entity paymentProcessor, Entity primaryRefund, Entity config, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("iATS refund starts");
            string agentCode = string.Empty;
            string agentPassword = string.Empty;

            try
            {
                CreditCardDetail transaction = new CreditCardDetail();

                int giftType = 0;

                if (refundEntity.LogicalName == "msnfp_transaction")
                    giftType = refundEntity.Contains("msnfp_paymenttypecode") ? ((OptionSetValue)refundEntity["msnfp_paymenttypecode"]).Value : 0;
                else if (refundEntity.LogicalName == "msnfp_payment")
                    giftType = refundEntity.Contains("msnfp_paymenttype") ? ((OptionSetValue)refundEntity["msnfp_paymenttype"]).Value : 0;

                localContext.TracingService.Trace("giftType : " + giftType);

                transaction.Identifier = refundEntity.Contains("msnfp_transactionidentifier") ? (string)refundEntity["msnfp_transactionidentifier"] : string.Empty;
                localContext.TracingService.Trace("Transaction Identifier : " + transaction.Identifier);

                transaction.Amount = string.Format("{0:0.00}", primaryRefund.Contains("msnfp_ref_amount") ? (((Money)primaryRefund["msnfp_ref_amount"]).Value) * -1 : decimal.Zero);
                localContext.TracingService.Trace("Refund Amount : " + transaction.Amount);

                if (paymentProcessor != null)
                {
                    agentCode = paymentProcessor.GetAttributeValue<string>("msnfp_iatsagentcode");
                    agentPassword = paymentProcessor.GetAttributeValue<string>("msnfp_iatspassword");
                }

                if (giftType == 844060002) // Credit or Debit Card
                {
                    localContext.TracingService.Trace("Credit Card");
                    Entity response = new Entity("msnfp_response");

                    if (refundEntity.LogicalName == "msnfp_transaction")
                        response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", refundEntity.Id);
                    else if (refundEntity.LogicalName == "msnfp_payment")
                        response["msnfp_paymentid"] = new EntityReference("msnfp_payment", refundEntity.Id);

                    if (refundEntity.Contains("msnfp_eventpackageid") && refundEntity["msnfp_eventpackageid"] != null)
                        response["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)refundEntity["msnfp_eventpackageid"]).Id);

                    localContext.TracingService.Trace("Refund Started");

                    #region Refund Process - Createing the iATS refund request.

                    ProcessCreditCardRefundWithTransactionId req = new ProcessCreditCardRefundWithTransactionId();
                    req.agentCode = agentCode;
                    req.password = agentPassword;
                    req.transactionId = transaction.Identifier;
                    req.total = transaction.Amount;
                    //req.customerIPAddress = "127.7.0.6";
                    XmlDocument _xmlDocRefund = iATSProcess.ProcessCreditCardRefundWithTransactionId(req);

                    #endregion

                    XmlNodeList xnList = _xmlDocRefund.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.

                    foreach (XmlNode item in xnList)
                    {

                        string authResult = item.InnerText;

                        if (authResult.Contains("OK"))
                        {
                            localContext.TracingService.Trace("Refund Success");
                            response["msnfp_response"] = _xmlDocRefund.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;

                            // Update the transaction as successfully refunded:
                            if (refundEntity.LogicalName == "msnfp_transaction")
                                SetTransactionAsRefunded(refundEntity, primaryRefund, localContext, service);
                            else if (refundEntity.LogicalName == "msnfp_payment")
                                SetTransactionAsRefundedPayment(refundEntity, primaryRefund, localContext, service);

                        }
                        else
                        {
                            response["msnfp_response"] = _xmlDocRefund.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;
                            localContext.TracingService.Trace("iATS refund process failure.");

                            refundEntity["msnfp_transactionresult"] = "Refund Failed.";
                        }
                    }
                    // creating response record
                    service.Create(response);
                }

                // updating transaction/payment record
                service.Update(refundEntity);
                localContext.TracingService.Trace("Updated successfully");

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in Refund", ex);
            }
        }

        #endregion


        #region Refund the gift using Stripe.
        private void RefundStripeTransaction(Entity refundEntity, Entity paymentProcessor, Entity primaryRefund, Entity config, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Start");

            try
            {
                CreditCardDetail transaction = new CreditCardDetail();
                int giftType = 0;

                if (refundEntity.LogicalName == "msnfp_transaction")
                    giftType = refundEntity.Contains("msnfp_paymenttypecode") ? ((OptionSetValue)refundEntity["msnfp_paymenttypecode"]).Value : 0;
                else if (refundEntity.LogicalName == "msnfp_payment")
                    giftType = refundEntity.Contains("msnfp_paymenttype") ? ((OptionSetValue)refundEntity["msnfp_paymenttype"]).Value : 0;

                localContext.TracingService.Trace("giftType : " + giftType);

                transaction.Identifier = refundEntity.Contains("msnfp_transactionidentifier") ? (string)refundEntity["msnfp_transactionidentifier"] : string.Empty;
                localContext.TracingService.Trace("Transaction Identifier : " + transaction.Identifier);

                transaction.Amount = string.Format("{0:0.00}", primaryRefund.Contains("msnfp_ref_amount") ? ((Money)primaryRefund["msnfp_ref_amount"]).Value : decimal.Zero);
                localContext.TracingService.Trace("Transaction Amount : " + transaction.Amount);

                string refundAmount = ((Convert.ToDecimal(transaction.Amount) * 100).ToString().Split('.')[0]);
                localContext.TracingService.Trace("Amount to be refunded : " + refundAmount);

                if (giftType == 844060002) // Credit or Debit Card
                {
                    localContext.TracingService.Trace("Credit Card");
                    Entity response = new Entity("msnfp_response");

                    if (refundEntity.LogicalName == "msnfp_transaction")
                        response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", refundEntity.Id);
                    else if (refundEntity.LogicalName == "msnfp_payment")
                    {
                        response["msnfp_paymentid"] = new EntityReference("msnfp_payment", refundEntity.Id);

                        if (refundEntity.Contains("msnfp_eventpackageid") && refundEntity["msnfp_eventpackageid"] != null)
                            response["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)refundEntity["msnfp_eventpackageid"]).Id);
                    }

                    localContext.TracingService.Trace("Refund Started");

                    // stripe service key
                    string secretKey = paymentProcessor["msnfp_stripeservicekey"].ToString();
                    StripeConfiguration.SetApiKey(secretKey);

                    #region Refund Process - Createing the stripe refund request.
                    var refundOptions = new StripeRefundCreateOptions
                    {
                        Amount = Convert.ToInt32(refundAmount),
                        Reason = StripeRefundReasons.RequestedByCustomer
                    };
                    localContext.TracingService.Trace("Transaction Amount : " + refundOptions.Amount);
                    var refundService = new StripeRefundService();
                    StripeRefund stripeRefundResponse = refundService.Create(transaction.Identifier, refundOptions);

                    #endregion

                    if (stripeRefundResponse != null)
                    {
                        localContext.TracingService.Trace("Find Stripe RefundResponse : " + stripeRefundResponse.Status);

                        if (stripeRefundResponse.Status == "succeeded")
                        {
                            localContext.TracingService.Trace("Refund Success");
                            response["msnfp_response"] = stripeRefundResponse.Status;

                            // Update the transaction as successfully refunded:
                            if (refundEntity.LogicalName == "msnfp_transaction")
                                SetTransactionAsRefunded(refundEntity, primaryRefund, localContext, service);
                            else if (refundEntity.LogicalName == "msnfp_payment")
                                SetTransactionAsRefundedPayment(refundEntity, primaryRefund, localContext, service);

                        }
                        else
                        {
                            response["msnfp_response"] = stripeRefundResponse.FailureReason;
                            localContext.TracingService.Trace("Stripe refund process failure.");

                            refundEntity["msnfp_transactionresult"] = "Refund Failed.";
                        }

                        // creating response record
                        service.Create(response);
                    }
                }

                // updating transaction/payment record
                service.Update(refundEntity);
                localContext.TracingService.Trace("Updated successfully");

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in Refund", ex);
            }
        }

        #endregion

        #region Refund the gift using iATS.



        // Awaiting models for iATS


        //private void RefundIatsTransaction(Entity refundEntity, Entity paymentProcessor, Entity primaryRefund, Entity config, LocalPluginContext localContext, IOrganizationService service)
        //{
        //    localContext.TracingService.Trace("iATS refund starts");
        //    string agentCode = string.Empty;
        //    string agentPassword = string.Empty;

        //    try
        //    {
        //        CreditCardDetail transaction = new CreditCardDetail();

        //        int giftType = 0;

        //        if (refundEntity.LogicalName == "msnfp_transaction")
        //            giftType = refundEntity.Contains("msnfp_paymenttypecode") ? ((OptionSetValue)refundEntity["msnfp_paymenttypecode"]).Value : 0;
        //        else if (refundEntity.LogicalName == "msnfp_payment")
        //            giftType = refundEntity.Contains("msnfp_paymenttype") ? ((OptionSetValue)refundEntity["msnfp_paymenttype"]).Value : 0;

        //        localContext.TracingService.Trace("giftType : " + giftType);

        //        transaction.Identifier = refundEntity.Contains("msnfp_transactionidentifier") ? (string)refundEntity["msnfp_transactionidentifier"] : string.Empty;
        //        localContext.TracingService.Trace("Transaction Identifier : " + transaction.Identifier);

        //        transaction.Amount = string.Format("{0:0.00}", primaryRefund.Contains("msnfp_ref_amount") ? (((Money)primaryRefund["msnfp_ref_amount"]).Value) * -1 : decimal.Zero);
        //        localContext.TracingService.Trace("Refund Amount : " + transaction.Amount);

        //        if (paymentProcessor != null)
        //        {
        //            agentCode = paymentProcessor.GetAttributeValue<string>("msnfp_iatsagentcode");
        //            agentPassword = paymentProcessor.GetAttributeValue<string>("msnfp_iatspassword");
        //        }

        //        if (giftType == 844060002) // Credit or Debit Card
        //        {
        //            localContext.TracingService.Trace("Credit Card");
        //            Entity response = new Entity("msnfp_response");

        //            if (refundEntity.LogicalName == "msnfp_transaction")
        //                response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", refundEntity.Id);
        //            else if (refundEntity.LogicalName == "msnfp_payment")
        //                response["msnfp_paymentid"] = new EntityReference("msnfp_payment", refundEntity.Id);


        //            localContext.TracingService.Trace("Refund Started");

        //            #region Refund Process - Createing the iATS refund request.

        //            ProcessCreditCardRefundWithTransactionId req = new ProcessCreditCardRefundWithTransactionId();
        //            req.agentCode = agentCode;
        //            req.password = agentPassword;
        //            req.transactionId = transaction.Identifier;
        //            req.total = transaction.Amount;
        //            //req.customerIPAddress = "127.7.0.6";
        //            XmlDocument _xmlDocRefund = iATSProcess.ProcessCreditCardRefundWithTransactionId(req);

        //            #endregion

        //            XmlNodeList xnList = _xmlDocRefund.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.

        //            foreach (XmlNode item in xnList)
        //            {

        //                string authResult = item.InnerText;

        //                if (authResult.Contains("OK"))
        //                {
        //                    localContext.TracingService.Trace("Refund Success");
        //                    response["msnfp_response"] = _xmlDocRefund.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;

        //                    // Update the transaction as successfully refunded:
        //                    if (refundEntity.LogicalName == "msnfp_transaction")
        //                        SetTransactionAsRefunded(refundEntity, primaryRefund, localContext, service);
        //                    else if (refundEntity.LogicalName == "msnfp_payment")
        //                        SetTransactionAsRefundedPayment(refundEntity, primaryRefund, localContext, service);

        //                }
        //                else
        //                {
        //                    response["msnfp_response"] = _xmlDocRefund.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;
        //                    localContext.TracingService.Trace("iATS refund process failure.");

        //                    refundEntity["msnfp_transactionresult"] = "Refund Failed.";
        //                }
        //            }
        //            // creating response record
        //            service.Create(response);
        //        }

        //        // updating transaction/payment record
        //        service.Update(refundEntity);
        //        localContext.TracingService.Trace("Updated successfully");

        //    }
        //    catch (Exception ex)
        //    {
        //        throw new InvalidPluginExecutionException("Error in Refund", ex);
        //    }
        //}

        #endregion

        #region Refund the gift using Moneris.
        private void RefundMonerisTransaction(Entity refundEntity, Entity paymentProcessor, Entity primaryRefund, Entity config, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering RefundMonerisTransaction().");

            CreditCardDetail transaction = new CreditCardDetail();
            if (refundEntity.Contains("msnfp_invoiceidentifier") && refundEntity.Contains("msnfp_transactionnumber"))
            {
                localContext.TracingService.Trace("Gift contains payment method.");

                // The order_id:
                transaction.Identifier = refundEntity.Contains("msnfp_invoiceidentifier") ? (string)refundEntity["msnfp_invoiceidentifier"] : string.Empty;

                // The Sequence Number:	
                transaction.TxnNumber = refundEntity.Contains("msnfp_transactionnumber") ? (string)refundEntity["msnfp_transactionnumber"] : string.Empty;
                localContext.TracingService.Trace("Transaction Identifier: " + transaction.Identifier);

                // For future, the old msnfp_total_header_refund is the new msnfp_ref_amount:
                transaction.Amount = string.Format("{0:0.00}", primaryRefund.Contains("msnfp_ref_amount") ? ((Money)primaryRefund["msnfp_ref_amount"]).Value : decimal.Zero);
                localContext.TracingService.Trace("Transaction Amount : " + string.Format("{0:0.00}", ((Money)primaryRefund["msnfp_ref_amount"]).Value));
                transaction.CryptType = Constants.CRYPTTYPE;

                localContext.TracingService.Trace("Attempting to refund Moneris purchase.");
                Receipt receipt = MonerisRefundPurchase(transaction, config, paymentProcessor, localContext);

                StringBuilder responseMessage = new StringBuilder();

                if (receipt != null)
                {
                    int responseCode = 0;
                    if (receipt.GetResponseCode() != "null")
                    {
                        responseCode = Convert.ToInt32(receipt.GetResponseCode());
                    }

                    Entity response = new Entity("msnfp_response");
                    responseMessage.Append("Response Code : " + receipt.GetResponseCode());
                    responseMessage.AppendLine();
                    responseMessage.Append("Response Message : " + receipt.GetMessage());
                    responseMessage.AppendLine();
                    responseMessage.Append("Response Complete : " + receipt.GetComplete());

                    response["msnfp_response"] = responseMessage.ToString();

                    if (refundEntity.LogicalName == "msnfp_transaction")
                        response["msnfp_transactionid"] = new EntityReference("msnfp_transaction", refundEntity.Id);
                    else if (refundEntity.LogicalName == "msnfp_payment")
                    {
                        response["msnfp_paymentid"] = new EntityReference("msnfp_payment", refundEntity.Id);

                        if (refundEntity.Contains("msnfp_eventpackageid") && refundEntity["msnfp_eventpackageid"] != null)
                            response["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)refundEntity["msnfp_eventpackageid"]).Id);
                    }

                    service.Create(response);


                    localContext.TracingService.Trace("Process Successful.");
                    localContext.TracingService.Trace("responseCode.ToString() == " + responseCode.ToString());
                    localContext.TracingService.Trace("receipt.GetResponseCode() == " + receipt.GetResponseCode());
                    localContext.TracingService.Trace("receipt.GetMessage() == " + receipt.GetMessage());
                    localContext.TracingService.Trace("receipt.GetComplete() == " + receipt.GetComplete());
                    if (responseCode != 0 && responseCode < 50)
                    {
                        // Update the transaction as successfully refunded:
                        if (refundEntity.LogicalName == "msnfp_transaction")
                            SetTransactionAsRefunded(refundEntity, primaryRefund, localContext, service);
                        else if (refundEntity.LogicalName == "msnfp_payment")
                            SetTransactionAsRefundedPayment(refundEntity, primaryRefund, localContext, service);

                        // Set the transaction identifier from Moneris on the primary refund:
                        if (receipt.GetReferenceNum() != null)
                        {
                            primaryRefund["msnfp_transactionidentifier"] = receipt.GetReferenceNum();
                            localContext.TracingService.Trace("Reference Number : " + receipt.GetReferenceNum());
                            service.Update(primaryRefund);
                            localContext.TracingService.Trace("Refund Updated.");
                        }
                    }
                    else if (responseCode == 0 || responseCode > 49)
                    {
                        //failed
                        refundEntity["msnfp_transactionresult"] = "Refund Payment Failed: " + responseCode;
                        localContext.TracingService.Trace("Updated Transaction Record's transaction result with 'Refund Payment Failed'. Response Code: " + responseCode);
                    }
                }
                else
                {
                    //failed
                    refundEntity["msnfp_transactionresult"] = "Refund Failed.";
                    localContext.TracingService.Trace("Receipt is null - MonerisRefundPurchase() did not complete successfully.");
                }

                service.Update(refundEntity);

                localContext.TracingService.Trace("Updated gift values.");
            }
        }
        #endregion

        #region Update the transaction record for a refund
        private void SetTransactionAsRefunded(Entity gift, Entity primaryRefund, LocalPluginContext localContext, IOrganizationService service)
        {
            gift["msnfp_transactionresult"] = "Refunded";
            gift["msnfp_daterefunded"] = DateTime.UtcNow;

            localContext.TracingService.Trace("Getting latest amounts from the refund.");

            localContext.TracingService.Trace("Now assign amount fields.");

            // Primary Refund Amounts
            localContext.TracingService.Trace("Primary Refund Amounts");
            decimal amount = primaryRefund.Contains("msnfp_amount_receipted") ? ((Money)primaryRefund["msnfp_amount_receipted"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amount = " + amount);

            decimal amountMembership = primaryRefund.Contains("msnfp_amount_membership") ? ((Money)primaryRefund["msnfp_amount_membership"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountMembership = " + amountMembership);

            decimal amountNonReceiptable = primaryRefund.Contains("msnfp_amount_nonreceiptable") ? ((Money)primaryRefund["msnfp_amount_nonreceiptable"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountNonReceiptable = " + amountNonReceiptable);

            decimal amountTax = primaryRefund.Contains("msnfp_amount_tax") ? ((Money)primaryRefund["msnfp_amount_tax"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountTax = " + amountTax);

            // Primary Refund Amount Receipted
            localContext.TracingService.Trace("Primary Refund Amount Receipted");
            decimal amountRefund = primaryRefund.Contains("msnfp_ref_amount_receipted") ? ((Money)primaryRefund["msnfp_ref_amount_receipted"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountRefund = " + amountRefund);

            decimal amountMembershipRefund = primaryRefund.Contains("msnfp_ref_amount_membership") ? ((Money)primaryRefund["msnfp_ref_amount_membership"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountMembershipRefund = " + amountMembershipRefund);

            decimal amountNonReceiptableRefund = primaryRefund.Contains("msnfp_ref_amount_nonreceiptable") ? ((Money)primaryRefund["msnfp_ref_amount_nonreceiptable"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountNonReceiptableRefund = " + amountNonReceiptableRefund);

            decimal amountTaxRefund = primaryRefund.Contains("msnfp_ref_amount_tax") ? ((Money)primaryRefund["msnfp_ref_amount_tax"]).Value : decimal.Zero;
            localContext.TracingService.Trace("amountTaxRefund = " + amountTaxRefund);

            decimal totalHeaderRefund = primaryRefund.Contains("msnfp_ref_amount") ? ((Money)primaryRefund["msnfp_ref_amount"]).Value : decimal.Zero;

            // Get the gift amounts:
            localContext.TracingService.Trace("Getting amounts from the transaction.");

            decimal giftAmountRefunded = gift.Contains("msnfp_ref_amount_receipted") ? ((Money)gift["msnfp_ref_amount_receipted"]).Value : decimal.Zero;
            localContext.TracingService.Trace("giftAmountRefunded = " + giftAmountRefunded);

            decimal giftAmountMembershipRefunded = gift.Contains("msnfp_ref_amount_membership") ? ((Money)gift["msnfp_ref_amount_membership"]).Value : decimal.Zero;
            localContext.TracingService.Trace("giftAmountMembershipRefunded = " + giftAmountMembershipRefunded);

            decimal giftAmountNonReceiptableRefunded = gift.Contains("msnfp_ref_amount_nonreceiptable") ? ((Money)gift["msnfp_ref_amount_nonreceiptable"]).Value : decimal.Zero;
            localContext.TracingService.Trace("giftAmountNonReceiptableRefunded = " + giftAmountNonReceiptableRefunded);

            decimal giftAmountTaxRefunded = gift.Contains("msnfp_ref_amount_tax") ? ((Money)gift["msnfp_ref_amount_tax"]).Value : decimal.Zero;
            localContext.TracingService.Trace("giftAmountTaxRefunded = " + giftAmountTaxRefunded);

            decimal giftTotalRefunded = gift.Contains("msnfp_ref_amount") ? ((Money)gift["msnfp_ref_amount"]).Value : decimal.Zero;
            localContext.TracingService.Trace("giftTotalRefunded = " + giftTotalRefunded);


            localContext.TracingService.Trace("Calculating the new amounts for the transaction (amount = amounts - refund amount) and setting refund fields.");

            gift["msnfp_amount_receipted"] = new Money(amount - amountRefund);
            gift["msnfp_amount_membership"] = new Money(amountMembership - amountMembershipRefund);
            gift["msnfp_amount_nonreceiptable"] = new Money(amountNonReceiptable - amountNonReceiptableRefund);
            gift["msnfp_amount_tax"] = new Money(amountTax - amountTaxRefund);

            gift["msnfp_ref_amount_receipted"] = new Money(giftAmountRefunded + amountRefund);
            gift["msnfp_ref_amount_membership"] = new Money(giftAmountMembershipRefunded + amountMembershipRefund);
            gift["msnfp_ref_amount_nonreceiptable"] = new Money(giftAmountNonReceiptableRefunded + amountNonReceiptableRefund);
            gift["msnfp_ref_amount"] = new Money(giftTotalRefunded + totalHeaderRefund);

            gift["msnfp_ref_amount_tax"] = new Money(giftAmountTaxRefunded + amountTaxRefund);

            localContext.TracingService.Trace("Getting amounts from the transaction.");

            gift["statuscode"] = new OptionSetValue(844060004);//Refund
            if (primaryRefund.Contains("msnfp_refunddate"))
            {
                gift["msnfp_daterefunded"] = primaryRefund["msnfp_refunddate"];
            }
            else
            {
                gift["msnfp_daterefunded"] = DateTime.UtcNow;
            }

            service.Update(gift);
        }
        #endregion



        #region Update the transaction record for a refund payment
        private void SetTransactionAsRefundedPayment(Entity payment, Entity primaryRefund, LocalPluginContext localContext, IOrganizationService service)
        {
            payment["msnfp_transactionresult"] = "Refunded";
            payment["msnfp_daterefunded"] = DateTime.UtcNow;

            localContext.TracingService.Trace("Getting latest amounts from the refund.");

            localContext.TracingService.Trace("Now assign amount fields.");

            decimal totalHeaderRefund = primaryRefund.Contains("msnfp_ref_amount") ? ((Money)primaryRefund["msnfp_ref_amount"]).Value : decimal.Zero;

            // Get the gift amounts:
            localContext.TracingService.Trace("Getting amounts from the transaction.");

            decimal paymentTotalRefunded = payment.Contains("msnfp_amount_refunded") ? ((Money)payment["msnfp_amount_refunded"]).Value : decimal.Zero;
            localContext.TracingService.Trace("paymentTotalRefunded = " + paymentTotalRefunded);


            decimal paymentTotalAmount = payment.Contains("msnfp_amount") ? ((Money)payment["msnfp_amount"]).Value : decimal.Zero;
            localContext.TracingService.Trace("paymentTotalAmount = " + paymentTotalAmount);

            localContext.TracingService.Trace("Calculating the new amounts for the transaction (amount = amounts - refund amount) and setting refund fields.");


            payment["msnfp_amount_refunded"] = new Money(paymentTotalRefunded + totalHeaderRefund);
            payment["msnfp_amount_balance"] = new Money(paymentTotalAmount - (paymentTotalRefunded + totalHeaderRefund));

            localContext.TracingService.Trace("Getting amounts from the transaction.");

            payment["statuscode"] = new OptionSetValue(844060004);//Refund
            if (primaryRefund.Contains("msnfp_refunddate"))
            {
                payment["msnfp_daterefunded"] = primaryRefund["msnfp_refunddate"];
            }
            else
            {
                payment["msnfp_daterefunded"] = DateTime.UtcNow;
            }

            service.Update(payment);
        }
        #endregion


        private Receipt MonerisRefundPurchase(CreditCardDetail CCD, Entity config, Entity paymentProcessor, LocalPluginContext localContext)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;

            localContext.TracingService.Trace("---------Entering RefundPurchase() ---------");
            string STOREID = paymentProcessor.Contains("msnfp_storeid") ? (string)paymentProcessor["msnfp_storeid"] : "store5";
            string APIKEY = paymentProcessor.Contains("msnfp_apikey") ? (string)paymentProcessor["msnfp_apikey"] : "yesguy";
            bool isTestMode = paymentProcessor.Contains("msnfp_testmode") ? (bool)paymentProcessor["msnfp_testmode"] : false; //false;

            localContext.TracingService.Trace("msnfp_storeid: " + STOREID);
            localContext.TracingService.Trace("msnfp_apikey: " + APIKEY);
            localContext.TracingService.Trace("msnfp_testmode: " + isTestMode);
            localContext.TracingService.Trace("CCD.TxnNumber: " + CCD.TxnNumber);
            localContext.TracingService.Trace("CCD.Identifier: " + CCD.Identifier);
            localContext.TracingService.Trace("CCD.Amount: " + CCD.Amount);

            Refund refund = new Refund();
            refund.SetTxnNumber(CCD.TxnNumber);
            refund.SetOrderId(CCD.Identifier);
            refund.SetAmount(CCD.Amount);
            refund.SetCryptType(CCD.CryptType);

            HttpsPostRequest mpgReq = new HttpsPostRequest();
            mpgReq.SetTestMode(isTestMode);
            mpgReq.SetStoreId(STOREID);
            mpgReq.SetApiToken(APIKEY);
            mpgReq.SetTransaction(refund);
            mpgReq.Send();

            Receipt receipt = null;
            try
            {
                localContext.TracingService.Trace("Obtained Receipt.");
                receipt = mpgReq.GetReceipt();
            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error: " + e.ToString());
                Console.WriteLine(e);
            }
            return receipt;
        }
    }
}

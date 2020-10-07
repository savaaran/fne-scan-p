/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using Moneris;
using FundraisingandEngagement.StripeWebPayment.Model;
using FundraisingandEngagement.StripeWebPayment.Service;
using FundraisingandEngagement.StripeIntegration.Helpers;
using Plugins.PaymentProcesses;
using Plugins.AzureModels;
using System.Text.RegularExpressions;
using System.Xml;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class PaymentCreate : PluginBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentCreate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public PaymentCreate(string unsecure, string secure)
            : base(typeof(PaymentCreate))
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
            localContext.TracingService.Trace("---------Triggered PaymentCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Entity paymentRecord;
            Entity targetRecord; // Note target is used in Azure sync.
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
                    localContext.TracingService.Trace("---------Entering PaymentCreate.cs Main Function---------");

                    targetRecord = (Entity)context.InputParameters["Target"];

                    if (targetRecord != null)
                    {
                        paymentRecord = service.Retrieve("msnfp_payment", targetRecord.Id, GetColumnSet());

                        if (paymentRecord == null)
                        {
                            throw new ArgumentNullException("msnfp_paymentid");
                        }

                        if (paymentRecord.Contains("msnfp_name") && paymentRecord.Contains("statuscode"))
                        {
                            localContext.TracingService.Trace("Payment name (msnfp_name): " + (string)paymentRecord["msnfp_name"]);
                            localContext.TracingService.Trace("Payment status reason (statuscode): " + ((OptionSetValue)paymentRecord["statuscode"]).Value.ToString());
                        }
                        else
                        {
                            localContext.TracingService.Trace("No payment name given.");
                        }


                        #region Payment Processing (if applicable). This section is where Moneris/Stripe processing occurs.

                        if (paymentRecord.Contains("statuscode") && paymentRecord.Contains("msnfp_paymenttype") && paymentRecord.Contains("msnfp_paymentmethodid"))
                        {
                            if (((OptionSetValue)paymentRecord["statuscode"]).Value == 844060000 && ((OptionSetValue)paymentRecord["msnfp_paymenttype"]).Value == 844060002) // status code completed, payment type: credit/debit card
                            {
                                // Get the payment method:
                                Entity paymentMethod;

                                paymentMethod = service.Retrieve("msnfp_paymentmethod", ((EntityReference)paymentRecord["msnfp_paymentmethodid"]).Id, new ColumnSet("msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode", "msnfp_customerid"));

                                if (paymentMethod != null)
                                {
                                    localContext.TracingService.Trace("Obtained payment method for this payment.");
                                    Entity paymentProcessor = null;
                                    if (paymentMethod.Contains("msnfp_paymentprocessorid"))
                                    {
                                        localContext.TracingService.Trace("Getting payment processor for transaction.");
                                        paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)paymentMethod["msnfp_paymentprocessorid"]).Id, new ColumnSet("msnfp_paymentgatewaytype"));

                                        if (paymentProcessor.Contains("msnfp_paymentgatewaytype"))
                                        {
                                            localContext.TracingService.Trace("Obtained payment gateway for this payment.");
                                            localContext.TracingService.Trace("Transaction identifier present- " + paymentRecord.Contains("msnfp_transactionidentifier").ToString());
                                            // If it is complete, not processed already and has a payment method attached. This is the default entry for Create messages:     
                                            if (!paymentRecord.Contains("msnfp_transactionidentifier") && messageName == "Create")
                                            {
                                                localContext.TracingService.Trace("Payment Gateway- " + ((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value.ToString());

                                                // Moneris:
                                                if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                                {
                                                    // If it is a reusable payment and the transaction has a parent, use the vault:
                                                    if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                    {
                                                        processMonerisVaultPayment(paymentRecord, localContext, service);
                                                    }
                                                    // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                    else
                                                    {
                                                        processMonerisOneTimePayment(paymentRecord, localContext, service);
                                                    }
                                                }
                                                // Stripe:
                                                else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                                {
                                                    // If it is a reusable payment and the transaction has a parent:
                                                    if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                    {
                                                        processStripePayment(configurationRecord, paymentRecord, localContext, service, false);
                                                    }
                                                    // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                    else
                                                    {
                                                        processStripePayment(configurationRecord, paymentRecord, localContext, service, true);
                                                    }
                                                }
                                                // iATS:
                                                else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                                {
                                                    // If it is a reusable payment:
                                                    if (paymentMethod.Contains("msnfp_isreusable") && (bool)paymentMethod["msnfp_isreusable"] == true)
                                                    {
                                                        localContext.TracingService.Trace("iATS Proccess. Reusable=True");
                                                        ProcessIatsPayment(configurationRecord, paymentRecord, localContext, service, false);
                                                    }
                                                    // Otherwise it is a single time transaction, use the one time purchase API and discard the payment method when finished:
                                                    else
                                                    {
                                                        localContext.TracingService.Trace("iATS Proccess. Reusable=false");
                                                        ProcessIatsPayment(configurationRecord, paymentRecord, localContext, service, true);
                                                    }
                                                }
                                                else
                                                {
                                                    localContext.TracingService.Trace("((OptionSetValue)paymentProcessor[msnfp_paymentgatewaytype]).Value" + ((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value.ToString());
                                                }

                                            }

                                        }
                                    }
                                    else
                                    {
                                        localContext.TracingService.Trace("There is no payment processor. No payment processed.");
                                    }
                                }
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("No payment type or payment method given.");
                        }

                        #endregion




                        if (messageName == "Create")
                        {
                            paymentRecord = service.Retrieve("msnfp_payment", targetRecord.Id, GetColumnSet());
                            UpdateEventPackageTotals(paymentRecord, orgSvcContext, service, localContext);
                            // Sync this to Azure. Note we use the target here as we want all the columns:
                            AddOrUpdateThisRecordWithAzure(paymentRecord, configurationRecord, localContext, service, context);
                        }
                        else if (messageName == "Update")
                        {
                            UpdateEventPackageTotals(paymentRecord, orgSvcContext, service, localContext);

                            // Sync this to Azure. Note we use the target here as we want all the columns:
                            AddOrUpdateThisRecordWithAzure(paymentRecord, configurationRecord, localContext, service, context);
                        }
                    }
                    else
                    {
                        localContext.TracingService.Trace("Target record not found. Exiting plugin.");
                    }
                }

                // Delete if the message is delete. This is the primary delete entrypoint:
                if (messageName == "Delete")
                {
                    targetRecord = service.Retrieve("msnfp_payment", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(targetRecord, configurationRecord, localContext, service, context);
                }


                localContext.TracingService.Trace("---------Exiting PaymentCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_eventpackageid", "statuscode", "msnfp_paymentid", "msnfp_customerid", "msnfp_amount", "msnfp_amount_refunded", "msnfp_name", "msnfp_transactionfraudcode", "msnfp_transactionidentifier", "msnfp_transactionresult", "msnfp_paymenttype", "msnfp_paymentprocessorid", "msnfp_paymentmethodid", "msnfp_ccbrandcodepayment", "msnfp_invoiceidentifier", "msnfp_responseid", "msnfp_amount_balance", "msnfp_configurationid", "msnfp_daterefunded", "msnfp_chequenumber", "statecode", "createdon");
        }

        #region iATS - Single Transaction API Processing.
        private void ProcessIatsPayment(Entity configurationRecord, Entity paymentRecord, LocalPluginContext localContext, IOrganizationService service, Boolean singleTransactionYN)
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


            if (paymentRecord.Contains("transactioncurrencyid") && paymentRecord["transactioncurrencyid"] != null)
            {
                Entity transactionCurrency = service.Retrieve("transactioncurrency", ((EntityReference)paymentRecord["transactioncurrencyid"]).Id, new ColumnSet("isocurrencycode"));

                if (transactionCurrency != null)
                    currency = transactionCurrency.Contains("isocurrencycode") ? (string)transactionCurrency["isocurrencycode"] : string.Empty;
            }

            int retryInterval = configurationRecord.Contains("msnfp_sche_retryinterval") ? (int)configurationRecord["msnfp_sche_retryinterval"] : 0;

            try
            {
                // Get the payment method:
                creditCard = getPaymentMethodForPayment(paymentRecord, localContext, service);
                localContext.TracingService.Trace("Payment method retrieved");

                // Ensure this is a credit card:
                if (creditCard.Contains("msnfp_type"))
                {
                    // Credit Card = 844060000:
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                    {
                        localContext.TracingService.Trace("processiATSPayment - Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                        //removePaymentMethod(creditCard, localContext, service);
                        if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                        {
                            setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                        }
                        return;
                    }
                }

                // Ensure the essential fields are completed:
                if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
                {
                    localContext.TracingService.Trace("processIatsPayment - Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                    removePaymentMethod(creditCard, localContext, service);
                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    return;
                }

                // Get the payment processor:
                paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, paymentRecord, localContext, service);
                localContext.TracingService.Trace("Payment processor retrieved.");

                if (paymentProcessor != null)
                {
                    agentCode = paymentProcessor.GetAttributeValue<string>("msnfp_iatsagentcode");
                    agentPassword = paymentProcessor.GetAttributeValue<string>("msnfp_iatspassword");
                }

                // retrieving customer
                if (paymentRecord.Contains("msnfp_customerid"))
                {
                    customerType = ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName;
                    customerId = ((EntityReference)paymentRecord["msnfp_customerid"]).Id;
                    if (customerType == "account")
                        customer = service.Retrieve("account", customerId, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "telephone3", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));
                    else
                        customer = service.Retrieve("contact", customerId, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "mobilephone", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));

                }

                if (paymentRecord.Contains("msnfp_amount"))
                    donationAmount = ((Money)paymentRecord["msnfp_amount"]).Value;
                localContext.TracingService.Trace("Donation Amount : " + donationAmount);

                //Credit card payment
                if (creditCard.Contains("msnfp_type") && ((OptionSetValue)creditCard["msnfp_type"]).Value == 844060000)
                {

                    localContext.TracingService.Trace("iATS credit card payment.");

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
                        obj.invoiceNum = paymentRecord.Contains("msnfp_missioninvoiceidentifier") ? (string)paymentRecord["msnfp_missioninvoiceidentifier"] : order_id; //string.Empty;    
                        obj.total = string.Format("{0:0.00}", donationAmount);
                        localContext.TracingService.Trace("Donation Amount : " + string.Format("{0:0.00}", donationAmount));
                        obj.comment = "Debited by Dynamics 365 on " + DateTime.Now.ToString();
                        _xmlDoc = iATSProcess.ProcessCreditCardWithCustomerCode(obj);

                        localContext.TracingService.Trace("Process complete to Payment with Credit Card.");
                    }
                }

                if (_xmlDoc != null)
                {
                    XmlNodeList xnList = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.

                    Entity response = new Entity("msnfp_response");
                    response["msnfp_paymentid"] = new EntityReference("msnfp_payment", ((Guid)paymentRecord["msnfp_paymentid"]));
                    response["msnfp_identifier"] = "Response for " + (string)paymentRecord["msnfp_name"];

                    if (paymentRecord.Contains("msnfp_eventpackageid") && paymentRecord["msnfp_eventpackageid"] != null)
                        response["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)paymentRecord["msnfp_eventpackageid"]).Id);

                    foreach (XmlNode item in xnList)
                    {
                        response["msnfp_response"] = item.InnerText;

                        string authResult = item.InnerText;

                        if (authResult.Contains("OK"))
                        {
                            localContext.TracingService.Trace("Got successful response from iATS payment gateway.");

                            responseText += "---------Start iATS Response---------" + System.Environment.NewLine;
                            responseText += "TransStatus = " + item.InnerText + System.Environment.NewLine;
                            responseText += "TransAmount = " + donationAmount + System.Environment.NewLine;
                            responseText += "Auth Token = " + cardId + System.Environment.NewLine;
                            responseText += "---------End iATS Response---------";

                            localContext.TracingService.Trace("processiATSTransaction - Got successful response from iATS payment gateway.");
                            response["msnfp_response"] = responseText;

                            paymentRecord["msnfp_transactionresult"] = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;
                            paymentRecord["msnfp_transactionidentifier"] = _xmlDoc.GetElementsByTagName("TRANSACTIONID")[0].InnerText;
                            paymentRecord["statuscode"] = new OptionSetValue(844060000);

                            //// updating customer record
                            //if (customer != null)
                            //{
                            //    customer["msnfp_lasttransactionid"] = new EntityReference("msnfp_transaction", ((Guid)giftTransaction["msnfp_transactionid"]));
                            //    customer["msnfp_lasttransactiondate"] = giftTransaction.Contains("createdon") ? (DateTime)giftTransaction["createdon"] : DateTime.MinValue;
                            //    service.Update(customer);
                            //}

                        }
                        else
                        {
                            localContext.TracingService.Trace("Got failure response from iATS payment gateway.");
                            response["msnfp_response"] = "FAILED";
                            localContext.TracingService.Trace("Status code updated to failed");
                            paymentRecord["statuscode"] = new OptionSetValue(844060003);
                            paymentRecord["msnfp_transactionresult"] = "FAILED";
                            localContext.TracingService.Trace("Gateway Response Message." + _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText);

                        }
                    }
                    // assigning invoice identifier
                    paymentRecord["msnfp_invoiceidentifier"] = order_id;


                    // creating response record
                    Guid responseGUID = service.Create(response);

                    if (responseGUID != null)
                        paymentRecord["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("processStripePayment - Error message: " + e.Message);

                localContext.TracingService.Trace("processStripePayment - error : " + e.Message);
                paymentRecord["statuscode"] = new OptionSetValue(844060003);
                localContext.TracingService.Trace("processStripePayment - Status code updated to failed");

                //giftTransaction["msnfp_lastfailedretry"] = DateTime.Now.ToLocalTime().Date;
                //giftTransaction["msnfp_currentretry"] = 0;
                //giftTransaction["msnfp_nextfailedretry"] = DateTime.Now.AddDays(retryInterval).ToLocalTime().Date;
                paymentRecord["msnfp_transactionresult"] = "FAILED";
                //primaryDonationPledge["msnfp_creditcardid"] = null;
                //payment fails remove credit card from parent as well
                //if (giftTransaction.Contains("msnfp_parenttransactionid") && giftTransaction["msnfp_parenttransactionid"] != null)
                //{
                //    localContext.TracingService.Trace("processStripePayment - payment fails remove credit card from parent as well");
                //    Entity parentTransaction = service.Retrieve("msnfp_transaction", ((EntityReference)giftTransaction["msnfp_transactionid"]).Id, new ColumnSet("msnfp_transaction_paymentmethodid"));
                //    if (parentTransaction != null && parentTransaction.Contains("msnfp_transaction_paymentmethodid") && parentTransaction["msnfp_transaction_paymentmethodid"] != null)
                //    {
                //        parentTransaction["msnfp_transaction_paymentmethodid"] = null;
                //        service.Update(parentTransaction);
                //        localContext.TracingService.Trace("processStripePayment - parent gift updated. removed Credit card");
                //    }
                //}
            }

            if (singleTransactionYN) // single transaction
            {
                // removing the payment transaction lookup value on the gift transaction
                paymentRecord["msnfp_paymentmethodid"] = null;

                // new credit card - removing payment method
                if (newCreditCardYN)
                    removePaymentMethod(creditCard, localContext, service);
            }

            service.Update(paymentRecord);
            localContext.TracingService.Trace("processIatsPayment - Entity Updated.");

        }

        #endregion

        #region Stripe - Single Transaction API Processing.
        private void processStripePayment(Entity configurationRecord, Entity paymentRecord, LocalPluginContext localContext, IOrganizationService service, Boolean singleTransactionYN)
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

            if (paymentRecord.Contains("transactioncurrencyid") && paymentRecord["transactioncurrencyid"] != null)
            {
                Entity transactionCurrency = service.Retrieve("transactioncurrency", ((EntityReference)paymentRecord["transactioncurrencyid"]).Id, new ColumnSet("isocurrencycode"));

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
                creditCard = getPaymentMethodForPayment(paymentRecord, localContext, service);

                // Ensure this is a credit card:
                if (creditCard.Contains("msnfp_type"))
                {
                    // Credit Card = 844060000:
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                    {
                        localContext.TracingService.Trace("processStripePayment - Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                        //removePaymentMethod(creditCard, localContext, service);
                        if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                        {
                            setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                        }
                        return;
                    }
                }

                // Ensure the essential fields are completed:
                if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
                {
                    localContext.TracingService.Trace("processStripePayment - Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                    removePaymentMethod(creditCard, localContext, service);
                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    return;
                }

                // Get the payment processor:
                paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, paymentRecord, localContext, service);

                string secretKey = paymentProcessor["msnfp_stripeservicekey"].ToString();
                //localContext.TracingService.Trace("processStripePayment - secretKey-" + secretKey);

                StripeConfiguration.SetApiKey(secretKey);

                // retrieving customer
                if (paymentRecord.Contains("msnfp_customerid"))
                {
                    customerType = ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName;
                    customerId = ((EntityReference)paymentRecord["msnfp_customerid"]).Id;
                    if (customerType == "account")
                        customer = service.Retrieve("account", customerId, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "telephone3", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));
                    else
                        customer = service.Retrieve("contact", customerId, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "mobilephone", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));

                }

                //Stripe Customer ID contains customer Id and Authtoken contains card id.
                if (creditCard.Contains("msnfp_stripecustomerid") && creditCard["msnfp_stripecustomerid"] != null && creditCard.Contains("msnfp_authtoken") && creditCard["msnfp_authtoken"] != null)
                {
                    localContext.TracingService.Trace("processStripePayment - Existing Card use");
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
                    localContext.TracingService.Trace("processStripePayment - New Card use");

                    newCreditCardYN = true;

                    string custName = customer.LogicalName == "account" ? customer["name"].ToString() : (customer["firstname"].ToString() + customer["lastname"].ToString());
                    string custEmail = customer.Contains("emailaddress1") ? customer["emailaddress1"].ToString() : string.Empty;

                    localContext.TracingService.Trace("processStripePayment - extracting customer info - done");
                    stripeCustomer = new CustomerService().GetStripeCustomer(custName, custEmail, secretKey);
                    localContext.TracingService.Trace("processStripePayment - obtained stripeCustomer");

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
                        throw new Exception("processStripePayment - Unable to add card to customer");
                    cardId = stripeCard.Id;
                    stripeCardBrand = stripeCard.Brand;

                    localContext.TracingService.Trace("processStripePayment - Card Id- " + cardId);

                    MaskStripeCreditCard(localContext, creditCard, cardId, stripeCardBrand, stripeCustomer.Id);
                }

                if (paymentRecord.Contains("msnfp_amount"))
                    donationAmount = ((Money)paymentRecord["msnfp_amount"]).Value;

                int chargeAmount = Convert.ToInt32((donationAmount * 100).ToString().Split('.')[0]);
                StripeCharge stripeObject = new StripeCharge();
                stripeObject.Amount = chargeAmount;
                stripeObject.Currency = string.IsNullOrEmpty(currency) ? "CAD" : currency;
                stripeObject.Customer = stripeCustomer;

                Source source = new Source();
                source.Id = cardId;
                stripeObject.Source = source;
                stripeObject.Description = paymentRecord.Contains("msnfp_invoiceidentifier") ? (string)paymentRecord["msnfp_invoiceidentifier"] : order_id; //string.Empty;                

                StripeCharge stripePayment = baseStipeRepository.Create<StripeCharge>(stripeObject, "https://api.stripe.com/v1/charges", secretKey);


                Entity response = new Entity("msnfp_response");
                response["msnfp_paymentid"] = new EntityReference("msnfp_payment", ((Guid)paymentRecord["msnfp_paymentid"]));
                response["msnfp_identifier"] = "Response for " + (string)paymentRecord["msnfp_name"];

                if (paymentRecord.Contains("msnfp_eventpackageid") && paymentRecord["msnfp_eventpackageid"] != null)
                    response["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)paymentRecord["msnfp_eventpackageid"]).Id);


                if (!string.IsNullOrEmpty(stripePayment.FailureMessage))
                {
                    response["msnfp_response"] = "FAILED";
                    paymentRecord["statuscode"] = new OptionSetValue(844060003);

                    paymentRecord["msnfp_transactionresult"] = "FAILED";
                }
                else
                {
                    localContext.TracingService.Trace("processStripeOneTimeTransaction - stripePayment.Id : " + stripePayment.Id);

                    if (stripePayment != null)
                    {
                        localContext.TracingService.Trace("processStripeOneTimeTransaction - stripePayment.InvoiceId : " + stripePayment.InvoiceId);
                        localContext.TracingService.Trace("processStripeOneTimeTransaction - stripePayment.Status : " + stripePayment.Status.ToString());

                        
                        if (stripePayment.Status.Equals("succeeded"))
                        {
                            responseText += "---------Start Stripe Response---------" + System.Environment.NewLine;
                            responseText += "TransAmount = " + stripePayment.Status + System.Environment.NewLine;
                            responseText += "TransAmount = " + donationAmount + System.Environment.NewLine;
                            responseText += "Auth Token = " + cardId + System.Environment.NewLine;
                            responseText += "---------End Stripe Response---------";

                            localContext.TracingService.Trace("processStripePayment - Got successful response from Stripe payment gateway.");
                            response["msnfp_response"] = responseText;

                            paymentRecord["msnfp_transactionresult"] = stripePayment.Status;
                            paymentRecord["msnfp_transactionidentifier"] = stripePayment.Id;
                            paymentRecord["statuscode"] = new OptionSetValue(844060000);

                            // Set the card type based on the Stripe response code:
                            if (stripeCardBrand != null)
                            {
                                localContext.TracingService.Trace("Card Type Response Code = " + stripeCardBrand);
                                switch (stripeCardBrand)
                                {
                                    case "MasterCard":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060001);
                                        break;
                                    case "Visa":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060000);
                                        break;
                                    case "American Express":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060004);
                                        break;
                                    case "Discover":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060008);
                                        break;
                                    case "Diners Club":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060005);
                                        break;
                                    case "UnionPay":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060009);
                                        break;
                                    case "JCB":
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060006);
                                        break;
                                    default:
                                        // Unknown:
                                        paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060010);
                                        break;
                                }
                            }

                            localContext.TracingService.Trace("processStripePayment - Updated Payment Record.");
                        }
                        else
                        {
                            localContext.TracingService.Trace("processStripePayment - Got failure response from payment gateway.");
                            response["msnfp_response"] = stripePayment.StripeResponse.ToString();
                            paymentRecord["statuscode"] = new OptionSetValue(844060003);
                            localContext.TracingService.Trace("processStripePayment - Status code updated to failed");

                            paymentRecord["msnfp_transactionidentifier"] = stripePayment.Id;
                            paymentRecord["msnfp_transactionresult"] = stripePayment.Status;

                            localContext.TracingService.Trace("Gateway Response Message." + stripePayment.Status);
                        }
                    }
                }

                // assigning invoice identifier
                paymentRecord["msnfp_invoiceidentifier"] = order_id;


                // creating response record
                Guid responseGUID = service.Create(response);

                if (responseGUID != null)
                    paymentRecord["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);
            }
            catch (Exception e)
            {
                //Console.WriteLine("processStripePayment - Error message: " + e.Message);

                localContext.TracingService.Trace("processStripePayment - error : " + e.Message);
                paymentRecord["statuscode"] = new OptionSetValue(844060003);
                localContext.TracingService.Trace("processStripePayment - Status code updated to failed");

                paymentRecord["msnfp_transactionresult"] = "FAILED";
            }

            if (singleTransactionYN) // single transaction
            {
                // removing the payment transaction lookup value on the gift transaction
                paymentRecord["msnfp_paymentmethodid"] = null;

                // new credit card - removing payment method
                if (newCreditCardYN)
                    removePaymentMethod(creditCard, localContext, service);
            }

            service.Update(paymentRecord);
            localContext.TracingService.Trace("processStripePayment - Entity Updated.");

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
        private string processMonerisOneTimePayment(Entity paymentRecord, LocalPluginContext localContext, IOrganizationService service)
        {
            // This will be the response code from the Moneris payment. This is used when adding a moneris vault profile to ensure AVS/CVD validation occured.
            string returnResponseCode = "";
            localContext.TracingService.Trace("Entering processMonerisOneTimePayment().");
            Entity creditCard = null;
            Entity paymentProcessor = null;
            localContext.TracingService.Trace("Gathering transaction data from target id.");

            // Get the payment method:
            creditCard = getPaymentMethodForPayment(paymentRecord, localContext, service);

            // Ensure this is a credit card:
            if (creditCard.Contains("msnfp_type"))
            {
                // Credit Card = 844060000:
                if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                    {
                        setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    }
                    return returnResponseCode;
                }
            }

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                removePaymentMethod(creditCard, localContext, service);

                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                return returnResponseCode;
            }

            // Get the payment processor:
            paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, paymentRecord, localContext, service);


            // Fill in payment data:
            localContext.TracingService.Trace("Put gathered payment information into purchase object.");
            string order_id = Guid.NewGuid().ToString();
            string store_id = (string)paymentProcessor["msnfp_storeid"];
            string api_token = (string)paymentProcessor["msnfp_apikey"];
            string amount = ((Money)paymentRecord["msnfp_amount"]).Value.ToString();
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
                            if (paymentRecord.Contains("msnfp_customerid"))
                            {
                                try
                                {
                                    localContext.TracingService.Trace("Entering address information for AVS validation.");
                                    avsCheck = AssignAVSValidationFieldsFromPaymentMethod(paymentRecord, creditCard, avsCheck, localContext, service);
                                    purchase.SetAvsInfo(avsCheck);
                                }
                                catch
                                {
                                    localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                                    throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)paymentRecord["msnfp_customerid"]).Id);
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("No Donor. Exiting plugin.");
                                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
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
                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
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
                            setStatusCodeOnPayment(paymentRecord, 844060000, localContext, service);

                            // Remove the payment method (if applicable):
                            removePaymentMethod(creditCard, localContext, service);
                        }
                        else
                        {
                            // Set the transaction to failed:
                            setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);

                            // Remove the payment method (if applicable):
                            removePaymentMethod(creditCard, localContext, service);
                        }
                    }
                    else
                    {
                        localContext.TracingService.Trace("Error: Response code is not a number = " + receipt.GetResponseCode());
                        setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                        removePaymentMethod(creditCard, localContext, service);
                        return returnResponseCode;
                    }
                }

                localContext.TracingService.Trace("Creating response record with response: " + receipt.GetMessage());

                // Create response record/associate to gift.
                Entity responseRecord = new Entity("msnfp_response");
                responseRecord["msnfp_identifier"] = "Response for " + (string)paymentRecord["msnfp_name"];
                responseRecord["msnfp_response"] = responseText;
                responseRecord["msnfp_paymentid"] = new EntityReference("msnfp_payment", ((Guid)paymentRecord["msnfp_paymentid"]));

                if (paymentRecord.Contains("msnfp_eventpackageid") && paymentRecord["msnfp_eventpackageid"] != null)
                    responseRecord["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)paymentRecord["msnfp_eventpackageid"]).Id);

                Guid responseGUID = service.Create(responseRecord);

                // Now associate that to the transaction entity:
                if (responseGUID != null)
                {
                    localContext.TracingService.Trace("Response created (" + responseGUID + "). Linking response record to transaction.");
                    paymentRecord["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);

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

                                paymentRecord["msnfp_transactionidentifier"] = receipt.GetReferenceNum();
                                paymentRecord["msnfp_transactionnumber"] = receipt.GetTxnNumber();
                                paymentRecord["msnfp_invoiceidentifier"] = order_id;
                                paymentRecord["msnfp_transactionresult"] = "Approved - " + responsCode;

                                // Set the card type based on the Moneris response code:
                                if (receipt.GetCardType() != null)
                                {
                                    localContext.TracingService.Trace("Card Type Response Code = " + receipt.GetCardType());
                                    switch (receipt.GetCardType())
                                    {
                                        case "M":
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060001);
                                            break;
                                        case "V":
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060000);
                                            break;
                                        case "AX":
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060004);
                                            break;
                                        case "NO":
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060008);
                                            break;
                                        case "D":
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060007);
                                            break;
                                        case "DC": // Diners Club
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060005);
                                            break;
                                        case "C1": // JCB
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060006);
                                            break;
                                        case "JCB": // JCB - Old
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060006);
                                            break;
                                        default:
                                            // Unknown:
                                            paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060010);
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    // We remove the lookup on the gift transaction as well (if applicable):
                    try
                    {
                        creditCard = service.Retrieve("msnfp_paymentmethod", ((EntityReference)paymentRecord["msnfp_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode" }));
                        if (creditCard == null)
                        {
                            localContext.TracingService.Trace("Clear Payment Method lookup on this payment.");
                            paymentRecord["msnfp_paymentmethodid"] = null;
                        }

                    }
                    catch (Exception ex)
                    {
                        localContext.TracingService.Trace("Could not find Payment Method. Clear Payment Method lookup on this payment record.");
                        paymentRecord["msnfp_paymentmethodid"] = null;
                    }
                    service.Update(paymentRecord);
                }

                localContext.TracingService.Trace("Setting return response code: " + receipt.GetResponseCode());
                returnResponseCode = receipt.GetResponseCode();

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Receipt Error: " + e.ToString());
                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                removePaymentMethod(creditCard, localContext, service);
            }

            return returnResponseCode;
        }
        #endregion


        #region Moneris - Recurring Vault Payment API Processing.
        private void processMonerisVaultPayment(Entity paymentRecord, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering processMonerisVaultPayment().");
            Entity creditCard = null;
            Entity paymentProcessor = null;
            localContext.TracingService.Trace("Gathering transaction data from target id.");

            // Get the payment method:
            creditCard = getPaymentMethodForPayment(paymentRecord, localContext, service);

            // Ensure this is a credit card:
            if (creditCard.Contains("msnfp_type"))
            {
                // Credit Card = 844060000:
                if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                    {
                        setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    }
                    return;
                }
            }

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                removePaymentMethod(creditCard, localContext, service);

                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                return;
            }

            // Get the payment processor:
            paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, paymentRecord, localContext, service);

            // If they do not have a vault id, we need to add them to the vault. Otherwise we continue below:
            if (!creditCard.Contains("msnfp_authtoken") || creditCard["msnfp_authtoken"] == null)
            {
                localContext.TracingService.Trace("No data id found for customer. Attempting to process the payment and if successful create a new Moneris Vault profile with this transaction.");

                // Here we charge the card and do the AVS/CVD validation (CVD validation cannot be done when adding a new profile, so we do the transaction first):
                string responseCodeString = processMonerisOneTimePayment(paymentRecord, localContext, service);

                int responsCode;
                if (int.TryParse(responseCodeString, out responsCode))
                {
                    if (responsCode < 50)
                    {
                        localContext.TracingService.Trace("Response was Approved. Now add to vault.");
                        // It completed successfully, add this customer information to the vault (note that this DOES NOT charge the card):
                        addMonerisVaultProfile(paymentRecord, localContext, service);

                    }
                    else
                    {
                        // Otherwise, something went wrong so we exit:
                        localContext.TracingService.Trace("Response code: " + responseCodeString + ". Please check payment details. Exiting plugin.");
                        setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                        return;
                    }
                }
            }
            else if (creditCard.Contains("msnfp_authtoken"))
            {
                localContext.TracingService.Trace("Data id found for customer.");
                localContext.TracingService.Trace("Data id: " + (string)creditCard["msnfp_authtoken"]);

                // Get the customer name:
                string cust_id = ((EntityReference)paymentRecord["msnfp_customerid"]).Id.ToString();

                // Fill in payment data:
                localContext.TracingService.Trace("Put gathered payment information into purchase object.");
                string order_id = Guid.NewGuid().ToString();
                string store_id = (string)paymentProcessor["msnfp_storeid"];
                string api_token = (string)paymentProcessor["msnfp_apikey"];
                string amount = ((Money)paymentRecord["msnfp_amount"]).Value.ToString();
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
                                if (paymentRecord.Contains("msnfp_customerid"))
                                {
                                    try
                                    {
                                        localContext.TracingService.Trace("Entering address information for AVS validation.");
                                        avsCheck = AssignAVSValidationFieldsFromPaymentMethod(paymentRecord, creditCard, avsCheck, localContext, service);
                                        resPurchaseCC.SetAvsInfo(avsCheck);
                                    }
                                    catch
                                    {
                                        localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                        setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                                        throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)paymentRecord["msnfp_customerid"]).Id);
                                    }
                                }
                                else
                                {
                                    localContext.TracingService.Trace("No Donor. Exiting plugin.");
                                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
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
                                setStatusCodeOnPayment(paymentRecord, 844060000, localContext, service);
                            }
                            else
                            {
                                // Set the transaction to failed:
                                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("Error: Response code is not a number = " + receipt.GetResponseCode());
                            setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                            removePaymentMethod(creditCard, localContext, service);
                        }
                    }

                    // Create response record/associate to gift.
                    Entity responseRecord = new Entity("msnfp_response");
                    responseRecord["msnfp_identifier"] = "Response for " + (string)paymentRecord["msnfp_name"];
                    responseRecord["msnfp_response"] = responseText;
                    responseRecord["msnfp_paymentid"] = new EntityReference("msnfp_payment", ((Guid)paymentRecord["msnfp_paymentid"]));

                    if (paymentRecord.Contains("msnfp_eventpackageid") && paymentRecord["msnfp_eventpackageid"] != null)
                        responseRecord["msnfp_eventpackageid"] = new EntityReference("msnfp_eventpackage", ((EntityReference)paymentRecord["msnfp_eventpackageid"]).Id);

                    Guid responseGUID = service.Create(responseRecord);

                    // Now associate that to the transaction entity:
                    if (responseGUID != null)
                    {
                        localContext.TracingService.Trace("Response created (" + responseGUID + "). Linking response record to payment.");
                        paymentRecord["msnfp_responseid"] = new EntityReference("msnfp_response", responseGUID);

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

                                    paymentRecord["msnfp_transactionidentifier"] = receipt.GetReferenceNum();
                                    paymentRecord["msnfp_transactionnumber"] = receipt.GetTxnNumber();
                                    paymentRecord["msnfp_invoiceidentifier"] = order_id;
                                    paymentRecord["msnfp_transactionresult"] = "Approved - " + responsCode;

                                    // Set the card type based on the Moneris response code:
                                    if (receipt.GetCardType() != null)
                                    {
                                        localContext.TracingService.Trace("Card Type Response Code = " + receipt.GetCardType());
                                        switch (receipt.GetCardType())
                                        {
                                            case "M":
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060001);
                                                break;
                                            case "V":
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060000);
                                                break;
                                            case "AX":
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060004);
                                                break;
                                            case "NO":
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060008);
                                                break;
                                            case "D":
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060007);
                                                break;
                                            case "DC": // Diners Club
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060005);
                                                break;
                                            case "C1": // JCB
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060006);
                                                break;
                                            case "JCB": // JCB - Old
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060006);
                                                break;
                                            default:
                                                // Unknown:
                                                paymentRecord["msnfp_ccbrandcodepayment"] = new OptionSetValue(844060010);
                                                break;
                                        }
                                    }
                                }
                                else if (responsCode > 50)
                                {
                                    paymentRecord["msnfp_transactionresult"] = "FAILED";
                                }
                            }
                        }

                        // We remove the lookup on the gift transaction as well (if applicable):
                        try
                        {
                            creditCard = service.Retrieve("msnfp_paymentmethod", ((EntityReference)paymentRecord["msnfp_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode" }));
                            if (creditCard == null)
                            {
                                localContext.TracingService.Trace("Clear Payment Method lookup on this transaction.");
                                paymentRecord["msnfp_paymentmethodid"] = null;
                            }

                        }
                        catch (Exception ex)
                        {
                            localContext.TracingService.Trace("Could not find Payment Method. Clear Payment Method lookup on this transaction record.");
                            localContext.TracingService.Trace(ex.ToString());
                            paymentRecord["msnfp_paymentmethodid"] = null;
                        }
                        service.Update(paymentRecord);
                    }
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace(e.ToString());
                }
            }
        }
        #endregion


        #region  Get Payment Method For Transaction

        private Entity getPaymentMethodForPayment(Entity paymentRecord, LocalPluginContext localContext, IOrganizationService service)
        {
            Entity paymentMethodToReturn;
            // Get the payment method:
            if (paymentRecord.Contains("msnfp_paymentmethodid"))
            {
                paymentMethodToReturn = service.Retrieve("msnfp_paymentmethod", ((EntityReference)paymentRecord["msnfp_paymentmethodid"]).Id, new ColumnSet(new string[] { "msnfp_paymentmethodid", "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_paymentprocessorid", "msnfp_type", "msnfp_isreusable", "msnfp_ccbrandcode", "msnfp_authtoken", "msnfp_telephone1", "msnfp_billing_line1", "msnfp_billing_postalcode", "msnfp_emailaddress1", "msnfp_stripecustomerid", "msnfp_bankactnumber", "msnfp_bankactrtnumber" }));
            }
            else
            {
                localContext.TracingService.Trace("No payment method (msnfp_paymentmethod) on this payment. Exiting plugin.");

                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                throw new ArgumentNullException("msnfp_paymentmethod");
            }

            return paymentMethodToReturn;
        }

        #endregion

        #region  Set Status Code On Payment
        private void setStatusCodeOnPayment(Entity paymentRecord, int statuscode, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("---------Attempting to change payment status.---------");
            if (paymentRecord == null)
            {
                localContext.TracingService.Trace("Payment does not exist.");
                return;
            }

            try
            {
                localContext.TracingService.Trace("Set statuscode to: " + statuscode + " for payment id: " + paymentRecord.Id);
                paymentRecord["statuscode"] = new OptionSetValue(statuscode);
                service.Update(paymentRecord);
                localContext.TracingService.Trace("Updated payment status successfully.");
            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("setStatusCodeOnPayment() Error: " + e.ToString());
            }
        }
        #endregion

        #region Get Payment Processor For Payment Method
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
                setStatusCodeOnPayment(giftTransaction, 844060003, localContext, service);
                throw new ArgumentNullException("msnfp_paymentprocessorid");
            }
            return paymentProcessorToReturn;
        }
        #endregion

        #region  Remove Payment Method
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
        #endregion


        #region Mask Stripe Credit Card
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
        #endregion

        #region  Assign AVS Validation Fields From Payment Method

        private AvsInfo AssignAVSValidationFieldsFromPaymentMethod(Entity paymentRecord, Entity paymentMethod, AvsInfo avsCheck, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering AssignAVSValidationFieldsFromPaymentMethod().");
            try
            {
                // If the customer is missing any mandatory fields, immediately fail:
                if (!paymentMethod.Contains("msnfp_billing_line1") || !paymentMethod.Contains("msnfp_billing_postalcode"))
                {
                    localContext.TracingService.Trace("Donor (" + ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName + ") with id = " + ((EntityReference)paymentRecord["msnfp_customerid"]).Id + " is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    throw new Exception("Donor (" + ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName + ") with id = " + ((EntityReference)paymentRecord["msnfp_customerid"]).Id + " is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
                }

                // Getting the street number in this instance is not 100% reliable, as there could be no/bad data. In this case we should let the user know the data is incorrect.
                string[] address1Split = ((string)paymentMethod["msnfp_billing_line1"]).Split(' ');
                if (address1Split.Length <= 1)
                {
                    // Throw an error, as the field is not setup correctly:
                    localContext.TracingService.Trace("Could not split address for AVS Validation. Please ensure the Street 1 billing address on the payment method is in the form '123 Example Street'. Exiting plugin.");
                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
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
                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                throw new Exception("AssignAVSValidationFieldsFromPaymentMethod() Error: " + e.ToString());
            }

            return avsCheck;
        }

        #endregion

        #region Moneris - First Time Vault Payment API Processing. This adds the profile so we can use the datakey in the future with processMonerisVaultTransaction.
        /// <summary>
        /// Add the customer/donor on the given transaction to the Moneris Vault with AVS validation (optional). This ONLY adds the customer with their credit card info (associated to the transaction) and does NOT charge the card.
        /// </summary>
        /// <param name="giftTransaction">The transaction entity with the associated customer information.</param>
        /// <param name="localContext">Used for trace logs.</param>
        /// <param name="service">Used for updating the payment information and retrieving records.</param>
        private void addMonerisVaultProfile(Entity paymentRecord, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering addMonerisVaultProfile().");
            Entity creditCard = null;
            Entity paymentProcessor = null;
            localContext.TracingService.Trace("Gathering transaction data from target id.");

            // Get the payment method:
            creditCard = getPaymentMethodForPayment(paymentRecord, localContext, service);

            // Ensure this is a credit card:
            if (creditCard.Contains("msnfp_type"))
            {
                // Credit Card = 844060000:
                if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060000)
                {
                    localContext.TracingService.Trace("Not a credit card (844060000). Payment method msnfp_type = " + ((OptionSetValue)creditCard["msnfp_type"]).Value.ToString());
                    if (((OptionSetValue)creditCard["msnfp_type"]).Value != 844060001)
                    {
                        setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    }
                    return;
                }
            }

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                removePaymentMethod(creditCard, localContext, service);

                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                return;
            }

            // Get the payment processor:
            paymentProcessor = getPaymentProcessorForPaymentMethod(creditCard, paymentRecord, localContext, service);


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
            string cust_id = ((EntityReference)paymentRecord["msnfp_customerid"]).Id.ToString();

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
                            if (paymentRecord.Contains("msnfp_customerid"))
                            {
                                try
                                {
                                    localContext.TracingService.Trace("Entering address information for AVS validation.");
                                    avsCheck = AssignAVSValidationFieldsFromPaymentMethod(paymentRecord, creditCard, avsCheck, localContext, service);
                                    resaddcc.SetAvsInfo(avsCheck);
                                }
                                catch
                                {
                                    localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                                    throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)paymentRecord["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)paymentRecord["msnfp_customerid"]).Id);
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("No Donor. Exiting plugin.");
                                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
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
                        localContext.TracingService.Trace("Masked Card Number");
                    }

                    service.Update(creditCard);
                    localContext.TracingService.Trace("Added token to payment method: " + creditCard["msnfp_authtoken"]);
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace("Error, could not assign data id to auth token. Data key: " + receipt.GetDataKey());
                    localContext.TracingService.Trace("Error: " + e.ToString());
                    setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                    throw new ArgumentNullException("msnfp_authtoken");
                }

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error processing response from payment gateway. Exiting plugin.");
                localContext.TracingService.Trace("Error: " + e.ToString());
                setStatusCodeOnPayment(paymentRecord, 844060003, localContext, service);
                throw new Exception("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.");
            }
        }
        #endregion

        #region Updating Event Package Totals

        private void UpdateEventPackageTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventPackageTotals---------");

            if (queriedEntityRecord.Contains("msnfp_eventpackageid"))
            {
                decimal valAmount = 0;

                Entity eventPackage = service.Retrieve("msnfp_eventpackage", ((EntityReference)queriedEntityRecord["msnfp_eventpackageid"]).Id, new ColumnSet(new string[] { "msnfp_eventpackageid", "msnfp_amount", "msnfp_amount_paid", "msnfp_amount_balance" }));


                if (((OptionSetValue)queriedEntityRecord["statuscode"]).Value == 844060004)  // refunded
                {
                    eventPackage["statuscode"] = new OptionSetValue(844060004);
                }

                List<Entity> paymentList = (from a in orgSvcContext.CreateQuery("msnfp_payment")
                                            where ((EntityReference)a["msnfp_eventpackageid"]).Id == eventPackage.Id
                                            && (((OptionSetValue)a["statuscode"]).Value == 844060000 || ((OptionSetValue)a["statuscode"]).Value == 844060004) // completed or refunded
                                            select a).ToList();

                foreach (Entity item in paymentList)
                {
                    valAmount += item.Contains("msnfp_amount_balance") ? ((Money)item["msnfp_amount_balance"]).Value : decimal.Zero;
                }

                decimal totalEventPackageAmount = eventPackage.Contains("msnfp_amount") ? ((Money)eventPackage["msnfp_amount"]).Value : decimal.Zero;
                eventPackage["msnfp_amount_balance"] = new Money(totalEventPackageAmount - valAmount);
                eventPackage["msnfp_amount_paid"] = new Money(valAmount);

                if (totalEventPackageAmount == valAmount)
                    eventPackage["statuscode"] = new OptionSetValue(844060000);

                service.Update(eventPackage);
            }
            localContext.TracingService.Trace("---------Exiting UpdateEventPackageTotals---------");
        }
        #endregion

        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Payment"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_paymentid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_Payment jsonDataObj = new MSNFP_Payment();

                jsonDataObj.paymentid = (Guid)queriedEntityRecord["msnfp_paymentid"];

                if (queriedEntityRecord.Contains("msnfp_eventpackageid") && queriedEntityRecord["msnfp_eventpackageid"] != null)
                {
                    jsonDataObj.eventpackageid = ((EntityReference)queriedEntityRecord["msnfp_eventpackageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventpackageid.");
                }
                else
                {
                    jsonDataObj.eventpackageid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventpackageid.");
                }

                if (queriedEntityRecord.Contains("msnfp_customerid") && queriedEntityRecord["msnfp_customerid"] != null)
                {
                    jsonDataObj.customerid = ((EntityReference)queriedEntityRecord["msnfp_customerid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_customerid.");
                }
                else
                {
                    jsonDataObj.customerid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_customerid.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount") && queriedEntityRecord["msnfp_amount"] != null)
                {
                    jsonDataObj.amount = ((Money)queriedEntityRecord["msnfp_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount");
                }
                else
                {
                    jsonDataObj.amount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_refunded") && queriedEntityRecord["msnfp_amount_refunded"] != null)
                {
                    jsonDataObj.AmountRefunded = ((Money)queriedEntityRecord["msnfp_amount_refunded"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_refunded");
                }
                else
                {
                    jsonDataObj.AmountRefunded = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_refunded.");
                }

                if (queriedEntityRecord.Contains("msnfp_name") && queriedEntityRecord["msnfp_name"] != null)
                {
                    jsonDataObj.name = (string)queriedEntityRecord["msnfp_name"];
                    localContext.TracingService.Trace("Got msnfp_name.");
                }
                else
                {
                    jsonDataObj.name = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_name.");
                }

                if (queriedEntityRecord.Contains("msnfp_transactionfraudcode") && queriedEntityRecord["msnfp_transactionfraudcode"] != null)
                {
                    jsonDataObj.transactionfraudcode = (string)queriedEntityRecord["msnfp_transactionfraudcode"];
                    localContext.TracingService.Trace("Got msnfp_transactionfraudcode.");
                }
                else
                {
                    jsonDataObj.transactionfraudcode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionfraudcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_transactionidentifier") && queriedEntityRecord["msnfp_transactionidentifier"] != null)
                {
                    jsonDataObj.transactionidentifier = (string)queriedEntityRecord["msnfp_transactionidentifier"];
                    localContext.TracingService.Trace("Got msnfp_transactionidentifier.");
                }
                else
                {
                    jsonDataObj.transactionidentifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionidentifier.");
                }

                if (queriedEntityRecord.Contains("msnfp_transactionresult") && queriedEntityRecord["msnfp_transactionresult"] != null)
                {
                    jsonDataObj.transactionresult = (string)queriedEntityRecord["msnfp_transactionresult"];
                    localContext.TracingService.Trace("Got msnfp_transactionresult.");
                }
                else
                {
                    jsonDataObj.transactionresult = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionresult.");
                }

                if (queriedEntityRecord.Contains("msnfp_paymenttype") && queriedEntityRecord["msnfp_paymenttype"] != null)
                {
                    jsonDataObj.paymenttype = ((OptionSetValue)queriedEntityRecord["msnfp_paymenttype"]).Value;
                    localContext.TracingService.Trace("Got msnfp_paymenttype.");
                }
                else
                {
                    jsonDataObj.paymenttype = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymenttype.");
                }

                if (queriedEntityRecord.Contains("msnfp_paymentprocessorid") && queriedEntityRecord["msnfp_paymentprocessorid"] != null)
                {
                    jsonDataObj.paymentprocessorid = ((EntityReference)queriedEntityRecord["msnfp_paymentprocessorid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentprocessorid.");
                }
                else
                {
                    jsonDataObj.paymentprocessorid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentprocessorid.");
                }

                if (queriedEntityRecord.Contains("msnfp_paymentmethodid") && queriedEntityRecord["msnfp_paymentmethodid"] != null)
                {
                    jsonDataObj.paymentmethodid = ((EntityReference)queriedEntityRecord["msnfp_paymentmethodid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentmethodid.");
                }
                else
                {
                    jsonDataObj.paymentmethodid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentmethodid.");
                }

                if (queriedEntityRecord.Contains("msnfp_ccbrandcodepayment") && queriedEntityRecord["msnfp_ccbrandcodepayment"] != null)
                {
                    jsonDataObj.ccbrandcodepayment = ((OptionSetValue)queriedEntityRecord["msnfp_ccbrandcodepayment"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ccbrandcodepayment.");
                }
                else
                {
                    jsonDataObj.ccbrandcodepayment = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ccbrandcodepayment.");
                }

                if (queriedEntityRecord.Contains("msnfp_invoiceidentifier") && queriedEntityRecord["msnfp_invoiceidentifier"] != null)
                {
                    jsonDataObj.invoiceidentifier = (string)queriedEntityRecord["msnfp_invoiceidentifier"];
                    localContext.TracingService.Trace("Got msnfp_invoiceidentifier.");
                }
                else
                {
                    jsonDataObj.invoiceidentifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_invoiceidentifier.");
                }


                //if (queriedEntityRecord.Contains("msnfp_transactionnumber") && queriedEntityRecord["msnfp_transactionnumber"] != null)
                //{
                //    jsonDataObj.transactionnumber = (string)queriedEntityRecord["msnfp_transactionnumber"];
                //    localContext.TracingService.Trace("Got msnfp_transactionnumber.");
                //}
                //else
                //{
                //    jsonDataObj.transactionnumber = string.Empty;
                //    localContext.TracingService.Trace("Did NOT find msnfp_transactionnumber.");
                //}

                if (queriedEntityRecord.Contains("msnfp_responseid") && queriedEntityRecord["msnfp_responseid"] != null)
                {
                    jsonDataObj.responseid = ((EntityReference)queriedEntityRecord["msnfp_responseid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_responseid.");
                }
                else
                {
                    jsonDataObj.responseid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_responseid.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_balance") && queriedEntityRecord["msnfp_amount_balance"] != null)
                {
                    jsonDataObj.AmountBalance = ((Money)queriedEntityRecord["msnfp_amount_balance"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_balance");
                }
                else
                {
                    jsonDataObj.AmountBalance = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_balance.");
                }

                if (queriedEntityRecord.Contains("msnfp_configurationid") && queriedEntityRecord["msnfp_configurationid"] != null)
                {
                    jsonDataObj.configurationid = ((EntityReference)queriedEntityRecord["msnfp_configurationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_configurationid.");
                }
                else
                {
                    jsonDataObj.configurationid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_configurationid.");
                }

                if (queriedEntityRecord.Contains("msnfp_daterefunded") && queriedEntityRecord["msnfp_daterefunded"] != null)
                {
                    jsonDataObj.daterefunded = (DateTime)queriedEntityRecord["msnfp_daterefunded"];
                    localContext.TracingService.Trace("Got msnfp_daterefunded");
                }
                else
                {
                    jsonDataObj.daterefunded = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_daterefunded");
                }

                if (queriedEntityRecord.Contains("msnfp_chequenumber") && queriedEntityRecord["msnfp_chequenumber"] != null)
                {
                    jsonDataObj.chequenumber = (string)queriedEntityRecord["msnfp_chequenumber"];
                    localContext.TracingService.Trace("Got msnfp_chequenumber.");
                }
                else
                {
                    jsonDataObj.chequenumber = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_chequenumber.");
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
                    jsonDataObj.statuscode = ((OptionSetValue)queriedEntityRecord["statuscode"]).Value;
                    localContext.TracingService.Trace("Got statuscode.");
                }
                else
                {
                    jsonDataObj.statuscode = null;
                    localContext.TracingService.Trace("Did NOT find statuscode.");
                }

                if (messageName == "Create")
                {
                    jsonDataObj.createdon = DateTime.UtcNow;
                }
                else if (queriedEntityRecord.Contains("createdon") && queriedEntityRecord["createdon"] != null)
                {
                    jsonDataObj.createdon = (DateTime)queriedEntityRecord["createdon"];
                }
                else
                {
                    jsonDataObj.createdon = null;
                }

                jsonDataObj.syncdate = DateTime.UtcNow;

                if (messageName == "Delete")
                {
                    jsonDataObj.deleted = true;
                    jsonDataObj.deleteddate = DateTime.UtcNow;
                }
                else
                {
                    jsonDataObj.deleted = false;
                    jsonDataObj.deleteddate = null;
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Payment));
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
                localContext.TracingService.Trace("No API URL or Enable Portal Pages. Exiting plugin.");
            }
        }
        #endregion
    }
}

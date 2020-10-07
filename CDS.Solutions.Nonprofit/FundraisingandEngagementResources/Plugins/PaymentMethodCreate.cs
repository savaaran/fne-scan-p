/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Plugins.PaymentProcesses;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using System.Globalization;
using Plugins.AzureModels;
using System.Linq;
using Moneris;
using FundraisingandEngagement.StripeWebPayment.Model;
using System.Text.RegularExpressions;
using FundraisingandEngagement.StripeWebPayment.Service;
using FundraisingandEngagement.StripeIntegration.Helpers;
using Utilities = Plugins.PaymentProcesses.Utilities;
using System.Xml;

namespace Plugins
{
    public class PaymentMethodCreate : PluginBase
    {
        public PaymentMethodCreate(string unsecure, string secure)
            : base(typeof(PaymentMethodCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered PaymentMethodCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Entity queriedEntityRecord = null;
            // Note target is used in Azure sync.
            Entity targetIncomingRecord;
            string messageName = context.MessageName;

            Entity configurationRecord;

            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            // Get the Configuration Record (Either from the User or from the Default Configuration Record)
            configurationRecord = Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering PostPaymentProcessorCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_paymentmethod", targetIncomingRecord.Id, GetColumnSet());
                    }

                    if (targetIncomingRecord != null)
                    {
                        if (messageName == "Create")
                        {
                            // Sync this to Azure. Note we use the target here as we want all the columns:
                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);

                            localContext.TracingService.Trace("Check for payment processor and auth token.");

                            // If there is no auth token, this is a new card not registered to the gateway:
                            if (targetIncomingRecord.GetAttributeValue<EntityReference>("msnfp_paymentprocessorid") != null && targetIncomingRecord.GetAttributeValue<bool>("msnfp_isreusable") == true && targetIncomingRecord.GetAttributeValue<OptionSetValue>("msnfp_type") != null && targetIncomingRecord.GetAttributeValue<string>("msnfp_authtoken") == null)
                            {
                                localContext.TracingService.Trace("No auth token found. Payment processor found. Registering new customer card to gateway.");

                                // Get the gateway:
                                Entity paymentProcessor = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)targetIncomingRecord["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_paymentgatewaytype" }));

                                // Based on the gateway, we register the vault profile if it is a credit card:
                                if (((OptionSetValue)targetIncomingRecord["msnfp_type"]).Value == 844060000)
                                {
                                    // Moneris:
                                    if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                    {
                                        RegisterPaymentMethodWithMonerisVault(targetIncomingRecord, localContext, service);
                                    }
                                    // Stripe:
                                    else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                    {
                                        RegisterPaymentMethodWithStripeVault(targetIncomingRecord, localContext, service);
                                    }
                                    // iATS:
                                    else if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                    {
                                        RegisterPaymentMethodWithiATSVault(targetIncomingRecord, localContext, service);
                                    }
                                    else
                                    {
                                        localContext.TracingService.Trace("((OptionSetValue)paymentProcessor[msnfp_paymentgatewaytype]).Value == " + ((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value.ToString());
                                    }
                                }
                                else
                                {
                                    localContext.TracingService.Trace("Card is not reusable or is not a credit card - ignore.");
                                }

                            }
                            else if (targetIncomingRecord.GetAttributeValue<EntityReference>("msnfp_paymentprocessorid") != null)
                            {
                                localContext.TracingService.Trace("Auth token found. Avoiding registration of profile process for the gateway.");
                            }

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
                    queriedEntityRecord = service.Retrieve("msnfp_paymentmethod", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting PaymentMethodCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_paymentmethodid", "msnfp_name", "msnfp_identifier", "msnfp_bankactnumber", "msnfp_bankactrtnumber", "msnfp_bankname", "msnfp_banktypecode", "msnfp_ccbrandcode", "msnfp_expirydate", "msnfp_ccexpmmyy", "msnfp_expirydate", "msnfp_billing_city", "msnfp_cclast4", "msnfp_ccexpmmyy", "msnfp_emailaddress1", "msnfp_firstname", "msnfp_lastname", "msnfp_nameonfile", "msnfp_paymentprocessorid", "msnfp_customerid", "msnfp_stripecustomerid", "msnfp_telephone1", "msnfp_billing_line1", "msnfp_authtoken", "msnfp_billing_line2", "msnfp_isreusable", "msnfp_billing_line3", "msnfp_billing_postalcode", "msnfp_billing_state", "msnfp_billing_country", "msnfp_abafinancialinstitutionname", "statecode", "statuscode", "msnfp_type", "msnfp_nameonbankaccount", "createdon");
        }

        #region Register Payment Method with Gateway

        /// <summary>
        /// Used for registering a new payment method of type credit card to a payment gateway.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        private void RegisterPaymentMethodWithMonerisVault(Entity paymentMethod, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering RegisterPaymentMethodWithMonerisVault().");
            Entity paymentProcessor = null;

            // Ensure the essential fields are completed:
            if (!paymentMethod.Contains("msnfp_cclast4") || !paymentMethod.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                throw new ArgumentNullException("msnfp_cclast4 or msnfp_ccexpmmyy is null");
            }

            // Get the payment processor information:
            paymentProcessor = getPaymentProcessorForPaymentMethod(paymentMethod, localContext, service);

            // Fill in payment data:
            localContext.TracingService.Trace("Put gathered payment information into vault profile object.");
            string store_id = (string)paymentProcessor["msnfp_storeid"];
            string api_token = (string)paymentProcessor["msnfp_apikey"];
            string pan = ((string)paymentMethod["msnfp_cclast4"]).Trim();
            string expdate = (string)paymentMethod["msnfp_ccexpmmyy"]; //"2001"; //YYMM format from payment method. Note this is the OPPOSITE of most cards.
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
            string cust_id = ((EntityReference)paymentMethod["msnfp_customerid"]).Id.ToString();

            // Get the phone/email from the payment method:
            if (paymentMethod.Contains("msnfp_telephone1"))
            {
                phone = (string)paymentMethod["msnfp_telephone1"];
            }
            if (paymentMethod.Contains("msnfp_emailaddress1"))
            {
                email = (string)paymentMethod["msnfp_emailaddress1"];
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
            if (paymentMethod.Contains("msnfp_ccbrandcode"))
            {
                // Note: AVS is only supported by Visa(844060000 credit,844060002 debit), MasterCard(844060001 credit,844060003 debit), Discover(844060008) and American Express(844060004).
                if ((((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value == 844060000) || (((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value == 844060002) || (((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value == 844060001) || (((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value == 844060003) || (((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value == 844060008) || (((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value == 844060004))
                {
                    if (paymentProcessor.Contains("msnfp_avsvalidation"))
                    {
                        if ((bool)paymentProcessor["msnfp_avsvalidation"])
                        {
                            localContext.TracingService.Trace("AVS Validation = True");
                            if (paymentMethod.Contains("msnfp_customerid"))
                            {
                                try
                                {
                                    localContext.TracingService.Trace("Entering address information for AVS validation.");
                                    avsCheck = AssignAVSValidationFieldsFromPaymentMethod(paymentMethod, avsCheck, localContext, service);
                                    resaddcc.SetAvsInfo(avsCheck);
                                }
                                catch
                                {
                                    localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                    throw new Exception("Unable to set AVSValidation fields. Please ensure the address fields are valid for the customer (" + ((EntityReference)paymentMethod["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)paymentMethod["msnfp_customerid"]).Id);
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("No Donor. Exiting plugin.");
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
                    localContext.TracingService.Trace("Payment Card type: " + ((OptionSetValue)paymentMethod["msnfp_ccbrandcode"]).Value.ToString());
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
                    paymentMethod["msnfp_authtoken"] = receipt.GetDataKey();

                    if (receipt.GetDataKey().Length > 0)
                    {
                        paymentMethod["msnfp_cclast4"] = receipt.GetResDataMaskedPan();
                        localContext.TracingService.Trace("Masked Card Number");
                    }

                    service.Update(paymentMethod);
                    localContext.TracingService.Trace("Added token to payment method: " + paymentMethod["msnfp_authtoken"]);
                }
                catch (Exception e)
                {
                    localContext.TracingService.Trace("Error, could not assign data id to auth token. Data key: " + receipt.GetDataKey());
                    localContext.TracingService.Trace("Error: " + e.ToString());
                    throw new ArgumentNullException("msnfp_authtoken");
                }

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error processing response from payment gateway. Exiting plugin.");
                localContext.TracingService.Trace("Error: " + e.ToString());
                throw new Exception("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.");
            }
    
        }

        /// <summary>
        /// Used for registering a new payment method of type credit card to a payment gateway.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        private void RegisterPaymentMethodWithStripeVault(Entity paymentMethod, LocalPluginContext localContext, IOrganizationService service)
        {
            string orderResponse = string.Empty;
            string stripeCardBrand = "";
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;
            Entity customer = null;
            Entity paymentProcessor = null;
            string order_id = Guid.NewGuid().ToString();

            localContext.TracingService.Trace("Entering RegisterPaymentMethodWithStripeVault().");

            // Get the customer information:
            if (paymentMethod.Contains("msnfp_customerid"))
            {
                customerType = ((EntityReference)paymentMethod["msnfp_customerid"]).LogicalName;
                customerId = ((EntityReference)paymentMethod["msnfp_customerid"]).Id;
                if (customerType == "account")
                    customer = service.Retrieve("account", customerId, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "telephone3", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));
                else
                    customer = service.Retrieve("contact", customerId, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "mobilephone", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid"));

            }
            else
            {
                localContext.TracingService.Trace("msnfp_customerid is null. Exiting plugin.");
                throw new ArgumentNullException("msnfp_customerid");
            }

            localContext.TracingService.Trace("Obtained customer information.");
                        
            try
            {
                StripeCustomer stripeCustomer = null;
                string cardId = null;

                BaseStipeRepository baseStipeRepository = new BaseStipeRepository();

                localContext.TracingService.Trace("Getting payment processor.");
                // Get the payment processor:
                paymentProcessor = getPaymentProcessorForPaymentMethod(paymentMethod, localContext, service);

                string secretKey = paymentProcessor["msnfp_stripeservicekey"].ToString();

                StripeConfiguration.SetApiKey(secretKey);
                                
                localContext.TracingService.Trace("Setting up Stripe objects");

                string custName = customer.LogicalName == "account" ? customer["name"].ToString() : (customer["firstname"].ToString() + customer["lastname"].ToString());
                string custEmail = customer.Contains("emailaddress1") ? customer["emailaddress1"].ToString() : string.Empty;

                localContext.TracingService.Trace("Extracted customer info");
                stripeCustomer = new CustomerService().GetStripeCustomer(custName, custEmail, secretKey);
                localContext.TracingService.Trace("Obtained stripeCustomer object");

                var myToken = new StripeTokenCreateOptions();
                string expMMYY = paymentMethod.Contains("msnfp_ccexpmmyy") ? paymentMethod["msnfp_ccexpmmyy"].ToString() : string.Empty;

                myToken.Card = new StripeCreditCardOptions()
                {
                    Number = paymentMethod["msnfp_cclast4"].ToString(),
                    ExpirationYear = expMMYY.Substring(expMMYY.Length - 2),
                    ExpirationMonth = expMMYY.Substring(0, expMMYY.Length - 2)
                };

                var tokenService = new StripeTokenService();
                localContext.TracingService.Trace("Creating token");
                StripeToken stripeTokenFinal = tokenService.Create(myToken);

                StripeCard stripeCardObj = new StripeCard();
                stripeCardObj.SourceToken = stripeTokenFinal.Id;
                string url = string.Format("https://api.stripe.com/v1/customers/{0}/sources", (object)stripeCustomer.Id);

                localContext.TracingService.Trace("Creating Stripe profile.");

                StripeCard stripeCard = baseStipeRepository.Create<StripeCard>(stripeCardObj, url, secretKey);
                if (string.IsNullOrEmpty(stripeCard.Id))
                    throw new Exception("Unable to add card to customer");
                cardId = stripeCard.Id;
                stripeCardBrand = stripeCard.Brand;

                localContext.TracingService.Trace("Returned Card Id- " + cardId);
                localContext.TracingService.Trace("Returned Stripe Customer Id- " + customerId);

                // Update the payment method:
                localContext.TracingService.Trace("Updating Payment Method Entity.");
                MaskPaymentMethod(localContext, paymentMethod, cardId, stripeCardBrand, stripeCustomer.Id);
                localContext.TracingService.Trace("Payment Method Entity Updated.");

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error : " + e.Message);
                throw new Exception("Error : " + e.Message);
            }

        }


        /// <summary>
        /// Used for registering a new payment method of type credit card to a payment gateway.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        private void RegisterPaymentMethodWithiATSVault(Entity paymentMethod, LocalPluginContext localContext, IOrganizationService service)
        {
            string orderResponse = string.Empty;
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;
            Entity customer = null;
            Entity paymentProcessor = null;
            string order_id = Guid.NewGuid().ToString();
            string agentCode = string.Empty;
            string agentPassword = string.Empty;
            string cardId = null;

            // Get the customer information:
            if (paymentMethod.Contains("msnfp_customerid"))
            {
                customerType = ((EntityReference)paymentMethod["msnfp_customerid"]).LogicalName;
                customerId = ((EntityReference)paymentMethod["msnfp_customerid"]).Id;
                if (customerType == "account")
                    customer = service.Retrieve("account", customerId, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "telephone3", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid", "transactioncurrencyid"));
                else
                    customer = service.Retrieve("contact", customerId, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "telephone2", "mobilephone", "emailaddress1", "msnfp_lasttransactionid", "msnfp_lasttransactiondate", "msnfp_vip", "ownerid", "transactioncurrencyid"));

            }
            else
            {
                localContext.TracingService.Trace("msnfp_customerid is null. Exiting plugin.");
                throw new ArgumentNullException("msnfp_customerid");
            }

            localContext.TracingService.Trace("Obtained customer information.");

            try
            {
                // Get the payment processor:
                paymentProcessor = getPaymentProcessorForPaymentMethod(paymentMethod, localContext, service);
                localContext.TracingService.Trace("Payment processor retrieved.");

                if (paymentProcessor != null)
                {
                    agentCode = paymentProcessor.GetAttributeValue<string>("msnfp_iatsagentcode");
                    agentPassword = paymentProcessor.GetAttributeValue<string>("msnfp_iatspassword");
                }
                else
                {
                    localContext.TracingService.Trace("paymentProcessor object is null. Exiting plugin.");
                    throw new ArgumentNullException("paymentProcessor");
                }
                                                        
                localContext.TracingService.Trace("Create new customer for iATS payment.");

                string expMMYY = paymentMethod.Contains("msnfp_ccexpmmyy") ? paymentMethod.GetAttributeValue<string>("msnfp_ccexpmmyy") : string.Empty;
                string yr = expMMYY.Substring(expMMYY.Length - 2);
                string mth = expMMYY.Substring(0, expMMYY.Length - 2);
                expMMYY = mth + "/" + yr;

                string cardNum = paymentMethod.Contains("msnfp_cclast4") ? paymentMethod.GetAttributeValue<string>("msnfp_cclast4") : string.Empty;

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

                localContext.TracingService.Trace("Creating customer.");
                XmlDocument xmlDocCustCode = iATSProcess.CreateCreditCardCustomerCode(objCreate);
                localContext.TracingService.Trace("Customer created.");
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

                localContext.TracingService.Trace("Mask and update the credit card.");
                MaskPaymentMethod(localContext, paymentMethod, cardId, null, null);                                
            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error : " + e.Message);
                throw new Exception("Error : " + e.Message);
            }

        }

        #endregion

        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "PaymentMethod"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);


            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_paymentmethodid"].ToString());

                MSNFP_PaymentMethod jsonDataObj = new MSNFP_PaymentMethod();

                jsonDataObj.PaymentMethodId = (Guid)queriedEntityRecord["msnfp_paymentmethodid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.Name = queriedEntityRecord.Contains("msnfp_name") ? (string)queriedEntityRecord["msnfp_name"] : string.Empty;
                jsonDataObj.Identifier = queriedEntityRecord.Contains("msnfp_identifier") ? (string)queriedEntityRecord["msnfp_identifier"] : string.Empty;
                localContext.TracingService.Trace("Title: " + jsonDataObj.Name);

                if (queriedEntityRecord.Contains("msnfp_bankactnumber") && queriedEntityRecord["msnfp_bankactnumber"] != null)
                {
                    jsonDataObj.BankActNumber = (string)queriedEntityRecord["msnfp_bankactnumber"];
                    localContext.TracingService.Trace("Got msnfp_bankactnumber.");
                }
                else
                {
                    jsonDataObj.BankActNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bankactnumber.");
                }


                if (queriedEntityRecord.Contains("msnfp_bankactrtnumber") && queriedEntityRecord["msnfp_bankactrtnumber"] != null)
                {
                    jsonDataObj.BankActRtNumber = (string)queriedEntityRecord["msnfp_bankactrtnumber"];
                    localContext.TracingService.Trace("Got msnfp_bankactrtnumber.");
                }
                else
                {
                    jsonDataObj.BankActRtNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bankactrtnumber.");
                }


                if (queriedEntityRecord.Contains("msnfp_bankname") && queriedEntityRecord["msnfp_bankname"] != null)
                {
                    jsonDataObj.BankName = (string)queriedEntityRecord["msnfp_bankname"];
                    localContext.TracingService.Trace("Got msnfp_bankname.");
                }
                else
                {
                    jsonDataObj.BankName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bankname.");
                }


                if (queriedEntityRecord.Contains("msnfp_banktypecode") && queriedEntityRecord["msnfp_banktypecode"] != null)
                {
                    jsonDataObj.BankTypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_banktypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_banktypecode.");
                }
                else
                {
                    jsonDataObj.BankTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_banktypecode.");
                }


                if (queriedEntityRecord.Contains("msnfp_ccbrandcode") && queriedEntityRecord["msnfp_ccbrandcode"] != null)
                {
                    jsonDataObj.CcBrandCode = ((OptionSetValue)queriedEntityRecord["msnfp_ccbrandcode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ccbrandcode.");
                }
                else
                {
                    jsonDataObj.CcBrandCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ccbrandcode.");
                }


                // This is the datetime version of the MMYY field (msnfp_ccexpmmyy). This field (msnfp_expirydate) is used for reporting purposes:
                if (queriedEntityRecord.Contains("msnfp_expirydate") && queriedEntityRecord["msnfp_expirydate"] != null)
                {
                    jsonDataObj.CcExpDate = (DateTime)queriedEntityRecord["msnfp_expirydate"];
                    localContext.TracingService.Trace("Got msnfp_expirydate.");
                }
                else
                {
                    // If it doesn't have it, try and get it from the MMYY field:
                    if (queriedEntityRecord.Contains("msnfp_ccexpmmyy") && queriedEntityRecord["msnfp_ccexpmmyy"] != null)
                    {
                        string expiryDateMMYY = (string)queriedEntityRecord["msnfp_ccexpmmyy"];

                        // Now get a new datetime object from the above:
                        try
                        {
                            jsonDataObj.CcExpDate = DateTime.ParseExact(expiryDateMMYY, "MMyy", CultureInfo.InvariantCulture).AddMonths(1).AddDays(-1);
                            localContext.TracingService.Trace("Got msnfp_expirydate from msnfp_ccexpmmyy (" + expiryDateMMYY + ")");
                            localContext.TracingService.Trace("CcExpDate: " + jsonDataObj.CcExpDate.ToString());

                            // Now save this on the payment method:
                            queriedEntityRecord["msnfp_expirydate"] = jsonDataObj.CcExpDate;
                            localContext.TracingService.Trace("Updating payment method field msnfp_expirydate from null to above CcExpDate.");
                            service.Update(queriedEntityRecord);
                            localContext.TracingService.Trace("Updated record. Continuing with JSON request.");

                        }
                        catch (Exception e)
                        {
                            jsonDataObj.CcExpDate = null;
                            localContext.TracingService.Trace("Did NOT find msnfp_expirydate. Could not convert from MMYY date: " + expiryDateMMYY);
                            localContext.TracingService.Trace("Conversion Error: " + e.ToString());
                        }
                    }
                    else
                    {
                        jsonDataObj.CcExpDate = null;
                        localContext.TracingService.Trace("Did NOT find msnfp_expirydate.");
                    }
                }


                if (queriedEntityRecord.Contains("msnfp_billing_city") && queriedEntityRecord["msnfp_billing_city"] != null)
                {
                    jsonDataObj.BillingCity = (string)queriedEntityRecord["msnfp_billing_city"];
                    localContext.TracingService.Trace("Got msnfp_billing_city.");
                }
                else
                {
                    jsonDataObj.BillingCity = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_city.");
                }


                if (queriedEntityRecord.Contains("msnfp_cclast4") && queriedEntityRecord["msnfp_cclast4"] != null)
                {
                    if (!((string)queriedEntityRecord["msnfp_cclast4"]).Contains("*"))
                    {
                        string censoredString1 = ((string)queriedEntityRecord["msnfp_cclast4"]).Substring(0, 4);
                        localContext.TracingService.Trace("censoredString1 = " + censoredString1);
                        string censoredString2 = ((string)queriedEntityRecord["msnfp_cclast4"]).Substring(((string)queriedEntityRecord["msnfp_cclast4"]).Length - 4); ;

                        localContext.TracingService.Trace("censoredString2 = " + censoredString2);
                        localContext.TracingService.Trace("new msnfp_cclast4 = " + censoredString1 + "***" + censoredString2);

                        jsonDataObj.CcLast4 = censoredString1 + "***" + censoredString2;
                    }
                    else
                    {
                        jsonDataObj.CcLast4 = (string)queriedEntityRecord["msnfp_cclast4"];
                    }

                    localContext.TracingService.Trace("Got msnfp_cclast4.");
                }
                else
                {
                    jsonDataObj.CcLast4 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_cclast4.");
                }


                // Note that this is the string version of MMYY. This field is used in actual processing of payments:
                if (queriedEntityRecord.Contains("msnfp_ccexpmmyy") && queriedEntityRecord["msnfp_ccexpmmyy"] != null)
                {
                    jsonDataObj.CcExpMmYy = (string)queriedEntityRecord["msnfp_ccexpmmyy"];
                    localContext.TracingService.Trace("Got msnfp_ccexpmmyy.");
                }
                else
                {
                    jsonDataObj.CcExpMmYy = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ccexpmmyy.");
                }


                if (queriedEntityRecord.Contains("msnfp_emailaddress1") && queriedEntityRecord["msnfp_emailaddress1"] != null)
                {
                    jsonDataObj.Emailaddress1 = (string)queriedEntityRecord["msnfp_emailaddress1"];
                    localContext.TracingService.Trace("Got msnfp_emailaddress1.");
                }
                else
                {
                    jsonDataObj.Emailaddress1 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_emailaddress1.");
                }


                if (queriedEntityRecord.Contains("msnfp_firstname") && queriedEntityRecord["msnfp_firstname"] != null)
                {
                    jsonDataObj.FirstName = (string)queriedEntityRecord["msnfp_firstname"];
                    localContext.TracingService.Trace("Got msnfp_firstname.");
                }
                else
                {
                    jsonDataObj.FirstName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_firstname.");
                }


                if (queriedEntityRecord.Contains("msnfp_lastname") && queriedEntityRecord["msnfp_lastname"] != null)
                {
                    jsonDataObj.LastName = (string)queriedEntityRecord["msnfp_lastname"];
                    localContext.TracingService.Trace("Got msnfp_lastname.");
                }
                else
                {
                    jsonDataObj.LastName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastname.");
                }


                if (queriedEntityRecord.Contains("msnfp_nameonfile") && queriedEntityRecord["msnfp_nameonfile"] != null)
                {
                    jsonDataObj.NameOnFile = (string)queriedEntityRecord["msnfp_nameonfile"];
                    localContext.TracingService.Trace("Got msnfp_nameonfile.");
                }
                else
                {
                    jsonDataObj.NameOnFile = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_nameonfile.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentprocessorid") && queriedEntityRecord["msnfp_paymentprocessorid"] != null)
                {
                    jsonDataObj.PaymentProcessorId = ((EntityReference)queriedEntityRecord["msnfp_paymentprocessorid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentprocessorid.");
                }
                else
                {
                    // Get the default payment gateway:
                    Entity user = service.Retrieve("systemuser", context.InitiatingUserId, new ColumnSet("msnfp_configurationid"));
                    if (user.Contains("msnfp_configurationid") && user["msnfp_configurationid"] != null)
                    {
                        ColumnSet paymentCols = new ColumnSet("msnfp_configurationid", "msnfp_paymentprocessorid");
                        Entity configRecord = service.Retrieve("msnfp_configuration", ((EntityReference)user["msnfp_configurationid"]).Id, paymentCols);

                        if (configRecord.Contains("msnfp_paymentprocessorid") && configRecord["msnfp_paymentprocessorid"] != null)
                        {
                            jsonDataObj.PaymentProcessorId = ((EntityReference)configRecord["msnfp_paymentprocessorid"]).Id;
                            localContext.TracingService.Trace("Got msnfp_paymentprocessorid from configuration file.");
                        }
                        else
                        {
                            jsonDataObj.PaymentProcessorId = null;
                            localContext.TracingService.Trace("Did NOT find msnfp_paymentprocessorid.");
                        }
                    }
                    else
                    {
                        jsonDataObj.PaymentProcessorId = null;
                        localContext.TracingService.Trace("Did NOT find msnfp_paymentprocessorid.");
                    }
                }


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

                if (queriedEntityRecord.Contains("msnfp_stripecustomerid") && queriedEntityRecord["msnfp_stripecustomerid"] != null)
                {
                    jsonDataObj.StripeCustomerId = (string)queriedEntityRecord["msnfp_stripecustomerid"];
                    localContext.TracingService.Trace("Got msnfp_stripecustomerid.");
                }
                else
                {
                    jsonDataObj.StripeCustomerId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_stripecustomerid.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone1") && queriedEntityRecord["msnfp_telephone1"] != null)
                {
                    jsonDataObj.Telephone1 = (string)queriedEntityRecord["msnfp_telephone1"];
                    localContext.TracingService.Trace("Got msnfp_telephone1.");
                }
                else
                {
                    jsonDataObj.Telephone1 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone1.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_line1") && queriedEntityRecord["msnfp_billing_line1"] != null)
                {
                    jsonDataObj.BillingLine1 = (string)queriedEntityRecord["msnfp_billing_line1"];
                    localContext.TracingService.Trace("Got msnfp_billing_line1.");
                }
                else
                {
                    jsonDataObj.BillingLine1 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line1.");
                }


                if (queriedEntityRecord.Contains("msnfp_authtoken") && queriedEntityRecord["msnfp_authtoken"] != null)
                {
                    jsonDataObj.AuthToken = (string)queriedEntityRecord["msnfp_authtoken"];
                    localContext.TracingService.Trace("Got msnfp_authtoken.");
                }
                else
                {
                    jsonDataObj.AuthToken = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_authtoken.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_line2") && queriedEntityRecord["msnfp_billing_line2"] != null)
                {
                    jsonDataObj.BillingLine2 = (string)queriedEntityRecord["msnfp_billing_line2"];
                    localContext.TracingService.Trace("Got msnfp_billing_line2.");
                }
                else
                {
                    jsonDataObj.BillingLine2 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line2.");
                }


                if (queriedEntityRecord.Contains("msnfp_isreusable") && queriedEntityRecord["msnfp_isreusable"] != null)
                {
                    jsonDataObj.IsReusable = (bool)queriedEntityRecord["msnfp_isreusable"];
                    localContext.TracingService.Trace("Got msnfp_isreusable.");
                }
                else
                {
                    jsonDataObj.IsReusable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_isreusable.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_line3") && queriedEntityRecord["msnfp_billing_line3"] != null)
                {
                    jsonDataObj.BillingLine3 = (string)queriedEntityRecord["msnfp_billing_line3"];
                    localContext.TracingService.Trace("Got msnfp_billing_line3.");
                }
                else
                {
                    jsonDataObj.BillingLine3 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line3.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_postalcode") && queriedEntityRecord["msnfp_billing_postalcode"] != null)
                {
                    jsonDataObj.BillingPostalCode = (string)queriedEntityRecord["msnfp_billing_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_billing_postalcode.");
                }
                else
                {
                    jsonDataObj.BillingPostalCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_postalcode.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_state") && queriedEntityRecord["msnfp_billing_state"] != null)
                {
                    jsonDataObj.BillingState = (string)queriedEntityRecord["msnfp_billing_state"];
                    localContext.TracingService.Trace("Got msnfp_billing_state.");
                }
                else
                {
                    jsonDataObj.BillingState = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_state.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_country") && queriedEntityRecord["msnfp_billing_country"] != null)
                {
                    jsonDataObj.BillingCountry = (string)queriedEntityRecord["msnfp_billing_country"];
                    localContext.TracingService.Trace("Got msnfp_billing_country.");
                }
                else
                {
                    jsonDataObj.BillingCountry = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_country.");
                }

                if (queriedEntityRecord.Contains("msnfp_abafinancialinstitutionname") && queriedEntityRecord["msnfp_abafinancialinstitutionname"] != null)
                {
                    jsonDataObj.AbaFinancialInstitutionName = (string)queriedEntityRecord["msnfp_abafinancialinstitutionname"];
                    localContext.TracingService.Trace("Got msnfp_abafinancialinstitutionname.");
                }
                else
                {
                    jsonDataObj.AbaFinancialInstitutionName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_abafinancialinstitutionname.");
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

                if (queriedEntityRecord.Contains("msnfp_type") && queriedEntityRecord["msnfp_type"] != null)
                {
                    jsonDataObj.Type = ((OptionSetValue)queriedEntityRecord["msnfp_type"]).Value;
                    localContext.TracingService.Trace("Got msnfp_type.");
                }
                else
                {
                    jsonDataObj.Type = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_type.");
                }

                if (queriedEntityRecord.Contains("msnfp_nameonbankaccount") && queriedEntityRecord["msnfp_nameonbankaccount"] != null)
                {
                    jsonDataObj.NameAsItAppearsOnTheAccount = (string)queriedEntityRecord["msnfp_nameonbankaccount"];
                    localContext.TracingService.Trace("Got msnfp_nameonbankaccount.");
                }
                else
                {
                    jsonDataObj.NameAsItAppearsOnTheAccount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_nameonbankaccount.");
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

                jsonDataObj.EventPackage = new HashSet<MSNFP_EventPackage>();
                jsonDataObj.PaymentSchedule = new HashSet<MSNFP_PaymentSchedule>();
                jsonDataObj.Transaction = new HashSet<MSNFP_Transaction>();

                localContext.TracingService.Trace("JSON object created");

                if (messageName == "Create")
                {
                    apiUrl += entityName + "/CreatePaymentMethod";
                }
                else if (messageName == "Update" || messageName == "Delete")
                {
                    apiUrl += entityName + "/UpdatePaymentMethod";
                }

                MemoryStream ms = new MemoryStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_PaymentMethod));
                localContext.TracingService.Trace("Attempt to create JSON via serialization.");
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


        #region Helper Functions


        /// <summary>
        /// Get and return the payment processor entity for the given payment method record.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private Entity getPaymentProcessorForPaymentMethod(Entity paymentMethod, LocalPluginContext localContext, IOrganizationService service)
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
                throw new ArgumentNullException("msnfp_paymentprocessorid");
            }
            return paymentProcessorToReturn;
        }

        /// <summary>
        /// Assigns the address values from the payment method to the AVS Check (Moneris) and returns the assigned AvsInfo record.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="avsCheck"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private AvsInfo AssignAVSValidationFieldsFromPaymentMethod(Entity paymentMethod, AvsInfo avsCheck, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering AssignAVSValidationFieldsFromPaymentMethod().");
            try
            {
                // If the customer is missing any mandatory fields, immediately fail:
                if (!paymentMethod.Contains("msnfp_billing_line1") || !paymentMethod.Contains("msnfp_billing_postalcode"))
                {
                    throw new Exception("Donor (" + ((EntityReference)paymentMethod["msnfp_customerid"]).LogicalName + ") with id = " + ((EntityReference)paymentMethod["msnfp_customerid"]).Id + " is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
                }

                // Getting the street number in this instance is not 100% reliable, as there could be no/bad data. In this case we should let the user know the data is incorrect.
                string[] address1Split = ((string)paymentMethod["msnfp_billing_line1"]).Split(' ');
                if (address1Split.Length <= 1)
                {
                    // Throw an error, as the field is not setup correctly:
                    localContext.TracingService.Trace("Could not split address for AVS Validation. Please ensure the Street 1 billing address on the payment method is in the form '123 Example Street'. Exiting plugin.");
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
                throw new Exception("AssignAVSValidationFieldsFromPaymentMethod() Error: " + e.ToString());
            }

            return avsCheck;
        }

        private void MaskPaymentMethod(LocalPluginContext localContext, Entity primarypaymentMethod, string cardId, string cardBrand, string customerId)
        {
            localContext.TracingService.Trace("Inside the method MaskPaymentMethod. ");
            string updatedCCNumber = Regex.Replace(primarypaymentMethod["msnfp_cclast4"].ToString(), "[0-9](?=[0-9]{4})", "X");
            primarypaymentMethod["msnfp_cclast4"] = updatedCCNumber;

            // Set the card type based on the Stripe response code:
            if (cardBrand != null)
            {
                switch (cardBrand)
                {
                    case "MasterCard":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060001);
                        break;
                    case "Visa":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060000);
                        break;
                    case "American Express":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060004);
                        break;
                    case "Discover":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060008);
                        break;
                    case "Diners Club":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060005);
                        break;
                    case "UnionPay":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060009);
                        break;
                    case "JCB":
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060006);
                        break;
                    default:
                        // Unknown:
                        primarypaymentMethod["msnfp_ccbrandcode"] = new OptionSetValue(844060010);
                        break;
                }
            }

            localContext.TracingService.Trace("CC Number : " + updatedCCNumber);

            primarypaymentMethod["msnfp_authtoken"] = cardId;
            primarypaymentMethod["msnfp_stripecustomerid"] = customerId;

            localContext.OrganizationService.Update(primarypaymentMethod);

            localContext.TracingService.Trace("Credit card record updated...MaskPaymentMethod");
        }

        #endregion


    }
}

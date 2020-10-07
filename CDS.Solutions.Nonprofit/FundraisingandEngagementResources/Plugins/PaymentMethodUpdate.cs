/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Plugins.PaymentProcesses;
using Moneris;
using FundraisingandEngagement.StripeWebPayment.Model;
using FundraisingandEngagement.StripeWebPayment.Service;
using FundraisingandEngagement.StripeIntegration.Helpers;

namespace Plugins
{
    public class PaymentMethodUpdate : PluginBase
    {
        private const string PostImageAlias = "msnfp_paymentmethod";

        public PaymentMethodUpdate(string unsecure, string secure)
            : base(typeof(PaymentMethodUpdate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered PaymentMethodUpdate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;

            Entity paymentMethodRecord;
            Entity paymentProcessor;
            Entity targetTransaction;

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                localContext.TracingService.Trace("---------Entering PaymentMethodUpdate.cs Main Function---------");

                targetTransaction = (Entity)context.InputParameters["Target"];

                Guid currentUserID = context.InitiatingUserId;
                Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

                paymentMethodRecord = service.Retrieve("msnfp_paymentmethod", targetTransaction.Id, GetColumnSet());

                if (paymentMethodRecord == null)
                {
                    throw new ArgumentNullException("msnfp_paymentmethodid");
                }
                else if (user == null)
                {
                    throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
                }

                // Ensure this is a re-usable credit card type with an address, a payment gateway/processor, a credit card number and an existing auth token with a customer attached. Otherwise we do not want to process it:
                if (paymentMethodRecord.Contains("msnfp_isreusable") && paymentMethodRecord.Contains("msnfp_type") && paymentMethodRecord.Contains("msnfp_billing_line1") && paymentMethodRecord.Contains("msnfp_paymentprocessorid") && paymentMethodRecord.Contains("msnfp_customerid") && paymentMethodRecord.Contains("msnfp_authtoken") && paymentMethodRecord.Contains("msnfp_cclast4"))
                {
                    if ((bool)paymentMethodRecord["msnfp_isreusable"] == false || ((OptionSetValue)paymentMethodRecord["msnfp_type"]).Value != 844060000 || (string)paymentMethodRecord["msnfp_billing_line1"] == null)
                    {
                        localContext.TracingService.Trace("Payment Method is not reusable, has no street 1 or is not a credit card. Exiting plugin.");
                        return;
                    }
                    else
                    {
                        // Get the payment processor:
                        paymentProcessor = getPaymentProcessorForPaymentMethod(paymentMethodRecord, localContext, service);
                        // If we have a payment processor:
                        if (paymentProcessor != null)
                        {
                            localContext.TracingService.Trace("Payment Processor retrieved.");

                            if (paymentProcessor.Contains("msnfp_paymentgatewaytype"))
                            {
                                // Moneris:
                                if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060000)
                                {
                                    localContext.TracingService.Trace("Gateway Type = Moneris");
                                    // See if a profile exists already:
                                    if (context.Depth == 1 && paymentMethodRecord["msnfp_authtoken"] != null)
                                    {
                                        // If so, we can now update the profile:
                                        updateMonerisVaultProfile(paymentMethodRecord, paymentProcessor, localContext, service);
                                    }
                                }

                                // Stripe:
                                if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060001)
                                {
                                    localContext.TracingService.Trace("Gateway Type = Stripe");
                                    // See if a profile exists already:
                                    if (context.Depth == 1 && paymentMethodRecord["msnfp_authtoken"] != null)
                                    {
                                        UpdateStripeCreditCard(paymentMethodRecord, paymentProcessor, localContext, service);
                                    }
                                }

                                // iATS:
                                if (((OptionSetValue)paymentProcessor["msnfp_paymentgatewaytype"]).Value == 844060002)
                                {
                                    localContext.TracingService.Trace("Gateway Type = iATS");
                                    // See if a profile exists already:
                                    if (context.Depth == 1 && paymentMethodRecord["msnfp_authtoken"] != null)
                                    {
                                        UpdateIatsCustomerCreditCard(paymentMethodRecord, paymentProcessor, localContext, service);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    localContext.TracingService.Trace("Payment Method is not reusable, has no street 1, is not a credit card or has no payment processor. Exiting plugin.");
                    return;
                }

                localContext.TracingService.Trace("---------Exiting PaymentMethodUpdate.cs---------");
            }
        }

        private static ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_paymentmethodid", "msnfp_customerid", "msnfp_isreusable", "msnfp_type", "msnfp_billing_line1", "msnfp_emailaddress1", "msnfp_telephone1", "msnfp_billing_postalcode", "msnfp_cclast4", "msnfp_authtoken", "msnfp_firstname", "msnfp_lastname", "msnfp_paymentprocessorid", "msnfp_ccexpmmyy", "msnfp_ccbrandcode", "msnfp_nameonfile", "msnfp_stripecustomerid");
        }

        #region Moneris - First Time Vault Payment API Processing. This adds the profile so we can use the datakey in the future with processMonerisVaultTransaction.
        /// <summary>
        /// Add the customer/donor on the given transaction to the Moneris Vault with AVS validation (optional). This ONLY adds the customer with their credit card info (associated to the transaction) and does NOT charge the card.
        /// </summary>
        /// <param name="creditCard">The transaction entity with the associated customer information.</param>
        /// <param name="localContext">Used for trace logs.</param>
        /// <param name="service">Used for updating the payment information and retrieving records.</param>
        private void updateMonerisVaultProfile(Entity creditCard, Entity paymentProcessor, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering updateMonerisVaultProfile().");

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                return;
            }

            // Fill in payment data:
            localContext.TracingService.Trace("Put gathered payment information into vault profile object.");
            string store_id = (string)paymentProcessor["msnfp_storeid"];
            string api_token = (string)paymentProcessor["msnfp_apikey"];
            bool test_mode = paymentProcessor.Contains("msnfp_testmode") ? (bool)paymentProcessor["msnfp_testmode"] : false;
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

            string phone = "";
            string email = "";
            string note = "Modified in Dynamics 365 on " + DateTime.UtcNow + "(UTC)";
            // Get the customer name:
            string cust_id = ((EntityReference)creditCard["msnfp_customerid"]).Id.ToString();

            // Get the phone/email from the payment method:
            if (creditCard.Contains("msnfp_telephone1"))
            {
                phone = (string)creditCard["msnfp_telephone1"];
            }
            if (creditCard.Contains("msnfp_emailaddress1"))
            {
                email = (string)creditCard["msnfp_emailaddress1"];
            }

            string data_key = (string)creditCard["msnfp_authtoken"];

            ResUpdateCC resUpdateCC = new ResUpdateCC();
            resUpdateCC.SetDataKey(data_key);
            resUpdateCC.SetCustId(cust_id);

            // Has this already been processed:
            if (pan != null && pan.Length > 5)
            {
                try
                {
                    // This avoids the issue where other information is updated and the card number is the same:
                    if (IsDigitsOnly(pan))
                    {
                        resUpdateCC.SetPan(pan);
                    }
                }
                catch
                {
                }
            }

            resUpdateCC.SetExpDate(expdate);
            resUpdateCC.SetPhone(phone);
            resUpdateCC.SetEmail(email);
            resUpdateCC.SetNote(note);
            resUpdateCC.SetCryptType(crypt);

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
                            try
                            {
                                localContext.TracingService.Trace("Entering address information for AVS validation.");
                                avsCheck = AssignAVSValidationFieldsFromPaymentMethod(creditCard, avsCheck, localContext, service);
                                resUpdateCC.SetAvsInfo(avsCheck);
                            }
                            catch
                            {
                                localContext.TracingService.Trace("Error with AVSValidation. Exiting plugin.");
                                throw new Exception("Unable to set AVSValidation fields in setStatusCodeOnTransaction(). Please ensure the address fields are valid for the customer (" + ((EntityReference)creditCard["msnfp_customerid"]).LogicalName + ") with id: " + ((EntityReference)creditCard["msnfp_customerid"]).Id);
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
            mpgReq.SetTestMode(test_mode);
            mpgReq.SetTransaction(resUpdateCC);
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

                try
                {
                    // Has this already been processed:
                    if (pan != null)
                    {
                        // This avoids the issue where other information is updated and the card number is the same:
                        if (IsDigitsOnly(pan))
                        {
                            creditCard["msnfp_authtoken"] = receipt.GetDataKey();

                            if (receipt.GetDataKey().Length > 0)
                            {
                                creditCard["msnfp_cclast4"] = receipt.GetResDataMaskedPan();
                                localContext.TracingService.Trace("Masked Card Number.");
                            }

                            service.Update(creditCard);
                            localContext.TracingService.Trace("Set token on payment method to: " + creditCard["msnfp_authtoken"]);
                        }
                        else
                        {
                            localContext.TracingService.Trace("Credit Card already Masked.");
                        }
                    }
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
        #endregion

        #region IatsCreditCard Update
        private void UpdateIatsCustomerCreditCard(Entity creditCard, Entity paymentProcessor, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering UpdateIatsCustomerCreditCard().");

            // Ensure the essential fields are completed:
            if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
            {
                localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                return;
            }

            string agentCode = paymentProcessor.GetAttributeValue<string>("msnfp_iatsagentcode");
            string agentPassword = paymentProcessor.GetAttributeValue<string>("msnfp_iatspassword");
            string cardNum = creditCard.GetAttributeValue<string>("msnfp_cclast4");
            string expDate = creditCard.GetAttributeValue<string>("msnfp_ccexpmmyy");
            string custNameOnCard = creditCard.GetAttributeValue<string>("msnfp_nameonfile");

            string firstTwo = expDate.Substring(0, 2);
            string lastTwo = expDate.Substring(2, 2);
            expDate = firstTwo + "/" + lastTwo;
            localContext.TracingService.Trace("New Expiry format (MMYY):" + expDate);

            string note = "Modified in Dynamics 365 on " + DateTime.UtcNow + "(UTC)";
            string cust_id = creditCard.GetAttributeValue<string>("msnfp_authtoken");


            try
            {
                UpdateIatsCard(cardNum, agentCode, agentPassword, cust_id, expDate, custNameOnCard, localContext);

                string updatedCCNumber = Regex.Replace(cardNum, "[0-9](?=[0-9]{4})", "X");
                Entity entCreditCard = new Entity(creditCard.LogicalName, creditCard.Id);
                entCreditCard["msnfp_cclast4"] = updatedCCNumber;
                localContext.TracingService.Trace("Masked Card Number.");
                service.Update(entCreditCard);
                localContext.TracingService.Trace("Updated credit card record");

            }
            catch (Exception e)
            {
                localContext.TracingService.Trace("Error processing response from iATS payment gateway. Exiting plugin.");
                localContext.TracingService.Trace("Error: " + e.ToString());
                throw new Exception("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.");
            }

        }

        private void UpdateIatsCard(string cardNum, string agentCode, string password, string customerCode, string expDate, string custNameOnCard, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("Entering UpdateIatsCard().");
            try
            {
                GetCustomerCodeDetail objGet = new GetCustomerCodeDetail();
                objGet.agentCode = agentCode;
                objGet.password = password;
                objGet.customerCode = customerCode;

                XmlDocument xmlGetDoc = iATSProcess.GetCustomerCodeDetail(objGet);

                //Checking expiry date in response as sometimes even if result is success it doesn't return anything
                if (xmlGetDoc.InnerText.Contains("Success") && xmlGetDoc.GetElementsByTagName("EXP").Count > 0)
                {
                    localContext.TracingService.Trace("Successfully retrieved customer from iATS account.");
                    string custName = xmlGetDoc.GetElementsByTagName("FLN")[0].InnerText;

                    UpdateCreditCardCustomerCode objUpdate = new UpdateCreditCardCustomerCode();
                    objUpdate.agentCode = agentCode;
                    objUpdate.password = password;
                    objUpdate.customerCode = customerCode;
                    objUpdate.creditCardNum = cardNum;
                    objUpdate.updateCreditCardNum = true;
                    objUpdate.creditCardExpiry = expDate;
                    objUpdate.beginDate = DateTime.Today;
                    objUpdate.endDate = DateTime.Today.AddDays(1);
                    objUpdate.recurring = false;
                    objUpdate.address = xmlGetDoc.GetElementsByTagName("ADD")[0].InnerText;
                    objUpdate.city = xmlGetDoc.GetElementsByTagName("CTY")[0].InnerText;
                    objUpdate.state = xmlGetDoc.GetElementsByTagName("ST")[0].InnerText;
                    objUpdate.companyName = xmlGetDoc.GetElementsByTagName("CO")[0].InnerText;
                    objUpdate.country = xmlGetDoc.GetElementsByTagName("CNT")[0].InnerText;
                    objUpdate.creditCardCustomerName = custNameOnCard;
                    objUpdate.email = xmlGetDoc.GetElementsByTagName("EM")[0].InnerText;
                    objUpdate.fax = xmlGetDoc.GetElementsByTagName("FX")[0].InnerText;
                    objUpdate.firstName = custName.Split(' ')[0];
                    objUpdate.lastName = string.Join(" ", custName.Split(' ').Skip(1));
                    objUpdate.mop = xmlGetDoc.GetElementsByTagName("MB")[0].InnerText;
                    objUpdate.phone = xmlGetDoc.GetElementsByTagName("PH")[0].InnerText;
                    objUpdate.zipCode = xmlGetDoc.GetElementsByTagName("ZC")[0].InnerText;
                    objUpdate.comment = "Modified in Dynamics 365 on " + DateTime.UtcNow + "(UTC)";

                    XmlDocument xmlUpdate = iATSProcess.UpdateCreditCardCustomerCode(objUpdate);

                    if (xmlUpdate.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText.Contains("OK"))
                    {
                        string authResult = xmlUpdate.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;
                        localContext.TracingService.Trace("Card updated successfully - " + authResult);
                    }
                    else
                    {
                        string error = xmlUpdate.GetElementsByTagName("AUTHORIZATIONRESULT")[0].InnerText;
                        localContext.TracingService.Trace("Error Details- " + error);
                        throw new Exception(error);
                    }

                }
                else
                {
                    localContext.TracingService.Trace("Error Details- " + xmlGetDoc.InnerText);
                    throw new Exception("Error getting response from payment gateway");
                }

            }
            catch (Exception ex)
            {
                localContext.TracingService.Trace("Error processing response from iATS payment gateway. Exiting plugin.");
                localContext.TracingService.Trace("Error: " + ex.ToString());
                throw new InvalidPluginExecutionException("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.", ex);
            }

        }
        #endregion

        #region StripeCreditCard Update
        private void UpdateStripeCreditCard(Entity creditCard, Entity paymentProcessor, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering UpdateStripeCreditCard().");

            try
            {
                // Ensure the essential fields are completed:
                if (!creditCard.Contains("msnfp_cclast4") || !creditCard.Contains("msnfp_ccexpmmyy"))
                {
                    localContext.TracingService.Trace("Not a completed credit card. Missing msnfp_cclast4 or msnfp_ccexpmmyy.");
                    return;
                }

                string stripeCustomerId = creditCard.GetAttributeValue<string>("msnfp_stripecustomerid");
                string secretKey = paymentProcessor.GetAttributeValue<string>("msnfp_stripeservicekey");

                if (!string.IsNullOrEmpty(stripeCustomerId) && !string.IsNullOrEmpty(secretKey))
                {
                    BaseStipeRepository baseStipeRepository = new BaseStipeRepository();
                    StripeCustomer stripeCustomer = new StripeCustomer();
                    stripeCustomer.Id = stripeCustomerId;
                    StripeConfiguration.SetApiKey(secretKey);
                    var myToken = new StripeTokenCreateOptions();

                    string cardNum = creditCard.GetAttributeValue<string>("msnfp_cclast4");
                    string expMMYY = creditCard.GetAttributeValue<string>("msnfp_ccexpmmyy");
                    string custNameOnCard = creditCard.GetAttributeValue<string>("msnfp_nameonfile");
                                        
                    myToken.Card = new StripeCreditCardOptions()
                    {
                        Number = cardNum,
                        ExpirationYear = expMMYY.Substring(expMMYY.Length - 2),
                        ExpirationMonth = expMMYY.Substring(0, expMMYY.Length - 2)
                    };
                    
                    localContext.TracingService.Trace("Create token for new card.");
                    var tokenService = new StripeTokenService();
                    StripeToken stripeTokenFinal = tokenService.Create(myToken);

                    StripeCard stripeCardObj = new StripeCard();
                    stripeCardObj.SourceToken = stripeTokenFinal.Id;
                    string url = string.Format("https://api.stripe.com/v1/customers/{0}/sources", (object)stripeCustomer.Id);
                    StripeCard stripeCard = baseStipeRepository.Create<StripeCard>(stripeCardObj, url, secretKey);
                    if (string.IsNullOrEmpty(stripeCard.Id))
                        throw new Exception("UpdateStripeCreditCard - Unable to add card to customer. Returned stripeCard.Id is null or empty.");
                    string cardId = stripeCard.Id;
                    string stripeCardBrand = stripeCard.Brand;
                    localContext.TracingService.Trace("Credit card updated successfully.");

                    MaskStripeCreditCard(localContext, creditCard, cardId, stripeCardBrand, stripeCustomerId);
                }
                else
                {
                    localContext.TracingService.Trace("Error processing response from Stripe payment gateway. Exiting plugin.");
                    localContext.TracingService.Trace("Customer Key or SecretKey not present.");
                    throw new Exception("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.");
                }
            }

            catch (Exception ex)
            {
                localContext.TracingService.Trace("Error processing response from Stripe payment gateway. Exiting plugin.");
                localContext.TracingService.Trace("Error: " + ex.ToString());
                throw new Exception("Error processing response from payment gateway. Please check donor information to make sure it is correctly inputted.");
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

            localContext.TracingService.Trace("Credit card record updated...MaskStripeCreditCard");
        }


        #endregion


        #region Helper Functions
        private bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;

                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get and return the payment processor entity for the given payment method record.
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <param name="giftTransaction"></param>
        /// <param name="localContext"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private Entity getPaymentProcessorForPaymentMethod(Entity paymentMethod, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("Entering getPaymentProcessorForPaymentMethod().");
            Entity paymentProcessorToReturn;
            // Get the payment processor:
            if (paymentMethod.Contains("msnfp_paymentprocessorid"))
            {
                paymentProcessorToReturn = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)paymentMethod["msnfp_paymentprocessorid"]).Id, new ColumnSet(new string[] { "msnfp_apikey", "msnfp_name", "msnfp_storeid", "msnfp_avsvalidation", "msnfp_cvdvalidation", "msnfp_testmode", "msnfp_paymentgatewaytype", "msnfp_iatsagentcode", "msnfp_iatspassword", "msnfp_stripeservicekey" }));
            }
            else
            {
                localContext.TracingService.Trace("No payment processor is assigned to this payment method. Exiting plugin.");
                throw new ArgumentNullException("msnfp_paymentprocessorid");
            }
            return paymentProcessorToReturn;
        }

        /// <summary>
        /// Assigns the values from the customer (account or contact) to the AVS Check (Moneris) and returns the assigned record.
        /// </summary>
        /// <param name="giftTransaction"></param>
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
                    localContext.TracingService.Trace("Customer is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
                    throw new Exception("Customer is missing either msnfp_billing_line1 or msnfp_billing_postalcode fields on their payment method. Exiting plugin.");
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
        #endregion

    }
}

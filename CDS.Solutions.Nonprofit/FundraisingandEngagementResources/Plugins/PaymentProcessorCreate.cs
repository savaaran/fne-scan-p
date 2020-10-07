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
    public class PaymentProcessorCreate : PluginBase
    {
        public PaymentProcessorCreate(string unsecure, string secure)
            : base(typeof(PaymentProcessorCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered PaymentProcessorCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

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

            configurationRecord = Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering PaymentProcessorCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_paymentprocessor", targetIncomingRecord.Id, GetColumnSet());
                    }

                    if (targetIncomingRecord != null)
                    {
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
                    queriedEntityRecord = service.Retrieve("msnfp_paymentprocessor", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting PaymentProcessorCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_paymentprocessorid","msnfp_name",  "msnfp_apikey","msnfp_name","msnfp_identifier","msnfp_paymentgatewaytype","msnfp_storeid","msnfp_testmode","statecode","statuscode","msnfp_bankrunfileformat","msnfp_scotiabankcustomernumber","msnfp_originatorshortname","msnfp_originatorlongname","msnfp_bmooriginatorid","msnfp_abaremittername","msnfp_abausername","msnfp_abausernumber","createdon");
        }

        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "PaymentProcessor"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_paymentprocessorid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_PaymentProcessor jsonDataObj = new MSNFP_PaymentProcessor();

                jsonDataObj.PaymentProcessorId = (Guid)queriedEntityRecord["msnfp_paymentprocessorid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.Name = queriedEntityRecord.Contains("msnfp_name") ? (string)queriedEntityRecord["msnfp_name"] : string.Empty;
                localContext.TracingService.Trace("Title: " + jsonDataObj.Name);

                if (queriedEntityRecord.Contains("msnfp_apikey") && queriedEntityRecord["msnfp_apikey"] != null)
                {
                    jsonDataObj.MonerisApiKey = (string)queriedEntityRecord["msnfp_apikey"];
                    localContext.TracingService.Trace("Got msnfp_apikey.");
                }
                else
                {
                    jsonDataObj.MonerisApiKey = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_apikey.");
                }


                if (queriedEntityRecord.Contains("msnfp_name") && queriedEntityRecord["msnfp_name"] != null)
                {
                    jsonDataObj.Name = (string)queriedEntityRecord["msnfp_name"];
                    localContext.TracingService.Trace("Got msnfp_name.");
                }
                else
                {
                    jsonDataObj.Name = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_name.");
                }


                if (queriedEntityRecord.Contains("msnfp_identifier") && queriedEntityRecord["msnfp_identifier"] != null)
                {
                    jsonDataObj.Identifier = (string)queriedEntityRecord["msnfp_identifier"];
                    localContext.TracingService.Trace("Got msnfp_identifier.");
                }
                else
                {
                    jsonDataObj.Identifier = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_identifier.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentgatewaytype") && queriedEntityRecord["msnfp_paymentgatewaytype"] != null)
                {
                    jsonDataObj.PaymentGatewayType = ((OptionSetValue)queriedEntityRecord["msnfp_paymentgatewaytype"]).Value;
                    localContext.TracingService.Trace("Got msnfp_paymentgatewaytype.");
                }
                else
                {
                    jsonDataObj.PaymentGatewayType = 844060000;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentgatewaytype.");
                }


                if (queriedEntityRecord.Contains("msnfp_storeid") && queriedEntityRecord["msnfp_storeid"] != null)
                {
                    jsonDataObj.MonerisStoreId = (string)queriedEntityRecord["msnfp_storeid"];
                    localContext.TracingService.Trace("Got msnfp_storeid.");
                }
                else
                {
                    jsonDataObj.MonerisStoreId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_storeid.");
                }

                if (queriedEntityRecord.Contains("msnfp_testmode") && queriedEntityRecord["msnfp_testmode"] != null)
                {
                    jsonDataObj.MonerisTestMode = (bool)queriedEntityRecord["msnfp_testmode"];
                    localContext.TracingService.Trace("Got msnfp_testmode.");
                }
                else
                {
                    jsonDataObj.MonerisTestMode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_testmode.");
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

                if (queriedEntityRecord.Contains("msnfp_bankrunfileformat") && queriedEntityRecord["msnfp_bankrunfileformat"] != null)
                {
                    jsonDataObj.BankRunFileFormat = ((OptionSetValue)queriedEntityRecord["msnfp_bankrunfileformat"]).Value;
                    localContext.TracingService.Trace("Got msnfp_bankrunfileformat.");
                }
                else
                {
                    jsonDataObj.BankRunFileFormat = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bankrunfileformat.");
                }

                if (queriedEntityRecord.Contains("msnfp_scotiabankcustomernumber") && queriedEntityRecord["msnfp_scotiabankcustomernumber"] != null)
                {
                    jsonDataObj.ScotiabankCustomerNumber = (string)queriedEntityRecord["msnfp_scotiabankcustomernumber"];
                    localContext.TracingService.Trace("Got msnfp_scotiabankcustomernumber.");
                }
                else
                {
                    jsonDataObj.ScotiabankCustomerNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_scotiabankcustomernumber.");
                }

                if (queriedEntityRecord.Contains("msnfp_originatorshortname") && queriedEntityRecord["msnfp_originatorshortname"] != null)
                {
                    jsonDataObj.OriginatorShortName = (string)queriedEntityRecord["msnfp_originatorshortname"];
                    localContext.TracingService.Trace("Got msnfp_originatorshortname.");
                }
                else
                {
                    jsonDataObj.OriginatorShortName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_originatorshortname.");
                }

                if (queriedEntityRecord.Contains("msnfp_originatorlongname") && queriedEntityRecord["msnfp_originatorlongname"] != null)
                {
                    jsonDataObj.OriginatorLongName = (string)queriedEntityRecord["msnfp_originatorlongname"];
                    localContext.TracingService.Trace("Got msnfp_originatorlongname.");
                }
                else
                {
                    jsonDataObj.OriginatorLongName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_originatorlongname.");
                }

                if (queriedEntityRecord.Contains("msnfp_bmooriginatorid") && queriedEntityRecord["msnfp_bmooriginatorid"] != null)
                {
                    jsonDataObj.BmoOriginatorId = (string)queriedEntityRecord["msnfp_bmooriginatorid"];
                    localContext.TracingService.Trace("Got msnfp_bmooriginatorid.");
                }
                else
                {
                    jsonDataObj.BmoOriginatorId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bmooriginatorid.");
                }

                if (queriedEntityRecord.Contains("msnfp_abaremittername") && queriedEntityRecord["msnfp_abaremittername"] != null)
                {
                    jsonDataObj.AbaRemitterName = (string)queriedEntityRecord["msnfp_abaremittername"];
                    localContext.TracingService.Trace("Got msnfp_abaremittername.");
                }
                else
                {
                    jsonDataObj.AbaRemitterName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bmooriginatorid.");
                }

                if (queriedEntityRecord.Contains("msnfp_abausername") && queriedEntityRecord["msnfp_abausername"] != null)
                {
                    jsonDataObj.AbaUserName = (string)queriedEntityRecord["msnfp_abausername"];
                    localContext.TracingService.Trace("Got msnfp_abausername.");
                }
                else
                {
                    jsonDataObj.AbaUserName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_abausername.");
                }

                if (queriedEntityRecord.Contains("msnfp_abausernumber") && queriedEntityRecord["msnfp_abausernumber"] != null)
                {
                    jsonDataObj.AbaUserNumber = (string)queriedEntityRecord["msnfp_abausernumber"];
                    localContext.TracingService.Trace("Got msnfp_abausernumber.");
                }
                else
                {
                    jsonDataObj.AbaUserNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_abausernumber.");
                }

                    if (queriedEntityRecord.Contains("msnfp_iatsagentcode") && queriedEntityRecord["msnfp_iatsagentcode"] != null)
                    {
                        jsonDataObj.IatsAgentCode = (string)queriedEntityRecord["msnfp_iatsagentcode"];
                        localContext.TracingService.Trace("Got msnfp_iatsagentcode.");
                    }
                    else
                    {
                        jsonDataObj.IatsAgentCode = null;
                        localContext.TracingService.Trace("Did NOT find msnfp_iatsagentcode.");
                    }

                    if (queriedEntityRecord.Contains("msnfp_iatspassword") && queriedEntityRecord["msnfp_iatspassword"] != null)
                    {
                        jsonDataObj.IatsPassword = (string)queriedEntityRecord["msnfp_iatspassword"];
                        localContext.TracingService.Trace("Got msnfp_iatspassword.");
                    }
                    else
                    {
                        jsonDataObj.IatsPassword = null;
                        localContext.TracingService.Trace("Did NOT find msnfp_iatspassword.");
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

                jsonDataObj.Updated = null;
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

                jsonDataObj.Configuration = new HashSet<MSNFP_Configuration>();
                jsonDataObj.PaymentMethod = new HashSet<MSNFP_PaymentMethod>();

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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_PaymentProcessor));
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

                // Here we display an error if we do not get a 200 OK from the API:
                Utilities util = new Utilities();
                util.CheckAPIReturnJSONForErrors(fileContent, configurationRecord.GetAttributeValue<OptionSetValue>("msnfp_showapierrorresponses"), localContext.TracingService);
            }
            else
            {
                localContext.TracingService.Trace("No API URL or Enable Portal Pages. Exiting workflow.");
            }
        }
        #endregion

    }
}
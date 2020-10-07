/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Plugins.PaymentProcesses;
using Microsoft.Xrm.Sdk.Query;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using Plugins.AzureModels;

namespace Plugins
{
    public class TransactionCurrencyCreate : PluginBase
    {
        public TransactionCurrencyCreate(string unsecure, string secure)
            : base(typeof(TransactionCurrencyCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered TransactionCurrencyCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

            Entity queriedEntityRecord = null;
            // Note target is used in Azure sync.
            Entity targetIncomingRecord;
            string messageName = context.MessageName;

            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            Entity configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                var _target = context.InputParameters["Target"];

                var target = _target is EntityReference target_er ? new Entity(target_er.LogicalName, target_er.Id) : _target;

                if (target is Entity)
                {
                    localContext.TracingService.Trace("---------Entering TransactionCurrencyCreate.cs Main Function---------");
                    
                    targetIncomingRecord = (Entity)target;

                    if (messageName == "Update" || messageName == "msnfp_ActionSyncCurrency")
                    {
                        queriedEntityRecord = service.Retrieve("transactioncurrency", targetIncomingRecord.Id, GetColumnSet());
                    }

                    if (targetIncomingRecord != null)
                    {
                        // Sync this to Azure. Note we use the target here as we want all the columns:
                        if (messageName == "Create")
                        {
                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);
                        }
                        else if (messageName == "Update" || messageName == "msnfp_ActionSyncCurrency")
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
                    queriedEntityRecord = service.Retrieve("transactioncurrency", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting TransactionCurrencyCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("transactioncurrencyid", "currencyname", "currencysymbol", "isocurrencycode", "statuscode", "statecode", "exchangerate", "organizationid", "createdon");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "TransactionCurrency"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["transactioncurrencyid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                TransactionCurrency jsonDataObj = new TransactionCurrency();

                jsonDataObj.TransactionCurrencyId = (Guid)queriedEntityRecord["transactioncurrencyid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.CurrencyName = queriedEntityRecord.Contains("currencyname") ? (string)queriedEntityRecord["currencyname"] : string.Empty;
                localContext.TracingService.Trace("Title: " + jsonDataObj.CurrencyName);

                if (queriedEntityRecord.Contains("currencysymbol") && queriedEntityRecord["currencysymbol"] != null)
                {
                    jsonDataObj.CurrencySymbol = (string)queriedEntityRecord["currencysymbol"];
                    localContext.TracingService.Trace("Got currencysymbol.");
                }
                else
                {
                    jsonDataObj.CurrencySymbol = null;
                    localContext.TracingService.Trace("Did NOT find currencysymbol.");
                }


                if (queriedEntityRecord.Contains("isocurrencycode") && queriedEntityRecord["isocurrencycode"] != null)
                {
                    jsonDataObj.IsoCurrencyCode = (string)queriedEntityRecord["isocurrencycode"];
                    localContext.TracingService.Trace("Got currenisocurrencycodecysymbol.");
                }
                else
                {
                    jsonDataObj.IsoCurrencyCode = null;
                    localContext.TracingService.Trace("Did NOT find isocurrencycode.");
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


                if (queriedEntityRecord.Contains("exchangerate") && queriedEntityRecord["exchangerate"] != null)
                {
                    jsonDataObj.ExchangeRate = (decimal)queriedEntityRecord["exchangerate"];
                    localContext.TracingService.Trace("Got exchangerate.");
                }
                else
                {
                    jsonDataObj.ExchangeRate = null;
                    localContext.TracingService.Trace("Did NOT find exchangerate.");
                }


                if (queriedEntityRecord.Contains("organizationid") && queriedEntityRecord["organizationid"] != null)
                {
                    localContext.TracingService.Trace("Querying for base currency = " + ((EntityReference)queriedEntityRecord["organizationid"]).Id.ToString());
                    // Get the base currency from the organization:
                    ColumnSet orgcols = new ColumnSet("basecurrencyid");
                    var currencyresult = service.Retrieve("organization", ((EntityReference)queriedEntityRecord["organizationid"]).Id, orgcols);
                    Guid currencyId = ((EntityReference)currencyresult["basecurrencyid"]).Id;
                    localContext.TracingService.Trace("CurrencyId = " + currencyId.ToString());
                    if (currencyId == (Guid)queriedEntityRecord["transactioncurrencyid"])
                    {
                        jsonDataObj.IsBase = true;
                    }
                    else
                    {
                        jsonDataObj.IsBase = false;
                    }

                    localContext.TracingService.Trace("Got base currency = " + currencyId);
                    localContext.TracingService.Trace("jsonDataObj.IsBase = " + jsonDataObj.IsBase);
                }
                else
                {
                    jsonDataObj.IsBase = null;
                    localContext.TracingService.Trace("Did NOT find isbase.");
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
                else if (messageName == "Update" || messageName == "Delete" || messageName == "msnfp_ActionSyncCurrency")
                {
                    apiUrl += entityName + "/Update" + entityName;
                }

                MemoryStream ms = new MemoryStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(TransactionCurrency));
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
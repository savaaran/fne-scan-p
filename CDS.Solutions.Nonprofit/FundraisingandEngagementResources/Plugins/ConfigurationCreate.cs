/*************************************************************************
* © Microsoft. All rights reserved.
*/

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using Plugins.AzureModels;
using Plugins.PaymentProcesses;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class ConfigurationCreate : PluginBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCreate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public ConfigurationCreate(string unsecure, string secure)
            : base(typeof(ConfigurationCreate))
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

            // TODO: Implement your custom plug-in business logic.
            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Utilities Utilities = new Utilities();
            localContext.TracingService.Trace("Configuration Create.");


            string messageName = context.MessageName;
            Entity primaryConfig = null;
            string apiUrl = string.Empty;



            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity || context.InputParameters["Target"] is EntityReference)
                {
                    
                    localContext.TracingService.Trace("Triggered By: " + messageName);

                    if (messageName == "Create")
                    {
                        primaryConfig = (Entity)context.InputParameters["Target"];
                    }
                    else if (messageName == "Update" || messageName == "Delete")
                    {
                        // primaryConfig is used in azure synching, it should contain all columns
                        primaryConfig = service.Retrieve("msnfp_configuration", context.PrimaryEntityId, GetColumnSet());
                        localContext.TracingService.Trace("Obtained values");
                    }

                    if (primaryConfig != null && (messageName == "Create" || messageName == "Update" || messageName == "Delete"))
                    {
                        localContext.TracingService.Trace("Configuration is Primary entity. Plugin Called On: " + messageName);

                        MSNFP_Configuration configObj = new MSNFP_Configuration();
                        configObj.ConfigurationId = (Guid)primaryConfig["msnfp_configurationid"];
                        configObj.Identifier = primaryConfig.Contains("msnfp_identifier") ? (string)primaryConfig["msnfp_identifier"] : string.Empty;
                        //configObj.AzureWebApp = primaryConfig.Contains("msnfp_azure_webapp") ? (string)primaryConfig["msnfp_azure_webapp"] : string.Empty;

                        localContext.TracingService.Trace("Obtaining JSON Fields to send to API.");

                        configObj.AzureWebApiUrl = primaryConfig.Contains("msnfp_azure_webapiurl") ? (string)primaryConfig["msnfp_azure_webapiurl"] : string.Empty;
                        configObj.AddressAuth1 = primaryConfig.Contains("msnfp_addressauth1") ? (string)primaryConfig["msnfp_addressauth1"] : string.Empty;
                        configObj.AddressAuth2 = primaryConfig.Contains("msnfp_addressauth2") ? (string)primaryConfig["msnfp_addressauth2"] : string.Empty;
                        // Default could be anything, but 1 is the minimum value:
                        configObj.BankRunPregeneratedBy = primaryConfig.Contains("msnfp_bankrun_pregeneratedby") ? (int)primaryConfig["msnfp_bankrun_pregeneratedby"] : 1;
                        configObj.CharityTitle = primaryConfig.Contains("msnfp_charitytitle") ? (string)primaryConfig["msnfp_charitytitle"] : string.Empty;
                        configObj.ScheMaxRetries = primaryConfig.Contains("msnfp_sche_maxretries") ? (int)primaryConfig["msnfp_sche_maxretries"] : 0;

                        if (primaryConfig.Contains("msnfp_sche_recurrencestart"))
                        {
                            configObj.ScheRecurrenceStart = ((OptionSetValue)primaryConfig["msnfp_sche_recurrencestart"]).Value;
                        }
                        else
                        {
                            configObj.ScheRecurrenceStart = null;
                        }

                        configObj.ScheRetryinterval = primaryConfig.Contains("msnfp_sche_retryinterval") ? (int)primaryConfig["msnfp_sche_retryinterval"] : 1;

                        localContext.TracingService.Trace("Get Entity References.");
                        if (primaryConfig.Contains("msnfp_teamownerid") && primaryConfig["msnfp_teamownerid"] != null)
                        {
                            configObj.TeamOwnerId = ((EntityReference)primaryConfig["msnfp_teamownerid"]).Id;
                        }
                        else
                        {
                            configObj.TeamOwnerId = null;
                        }

                        if (primaryConfig.Contains("msnfp_paymentprocessorid") && primaryConfig["msnfp_paymentprocessorid"] != null)
                        {
                            configObj.PaymentProcessorId = ((EntityReference)primaryConfig["msnfp_paymentprocessorid"]).Id;
                        }
                        else
                        {
                            configObj.PaymentProcessorId = null;
                        }

                        if (primaryConfig.Contains("msnfp_defaultconfiguration") && primaryConfig["msnfp_defaultconfiguration"] != null)
                        {
                            configObj.DefaultConfiguration = (bool)primaryConfig["msnfp_defaultconfiguration"];
                            localContext.TracingService.Trace("Got Default Configuration.");
                        }
                        else
                        {
                            configObj.DefaultConfiguration = null;
                            localContext.TracingService.Trace("Did NOT find Default Configuration.");
                        }


                        if (primaryConfig.Contains("statecode") && primaryConfig["statecode"] != null)
                        {
                            configObj.StateCode = ((OptionSetValue)primaryConfig["statecode"]).Value;
                            localContext.TracingService.Trace("Got statecode.");
                        }
                        else
                        {
                            configObj.StateCode = null;
                            localContext.TracingService.Trace("Did NOT find statecode.");
                        }

                        if (primaryConfig.Contains("statuscode") && primaryConfig["statuscode"] != null)
                        {
                            configObj.StatusCode = ((OptionSetValue)primaryConfig["statuscode"]).Value;
                            localContext.TracingService.Trace("Got statuscode.");
                        }
                        else
                        {
                            configObj.StatusCode = null;
                            localContext.TracingService.Trace("Did NOT find statuscode.");
                        }
                        localContext.TracingService.Trace("Set remaining JSON fields to null as they are not used here.");


                        if (messageName == "Create")
                        {
                            configObj.CreatedOn = DateTime.UtcNow;
                        }
                        else if (primaryConfig.Contains("createdon") && primaryConfig["createdon"] != null)
                        {
                            configObj.CreatedOn = (DateTime)primaryConfig["createdon"];
                        }
                        else
                        {
                            configObj.CreatedOn = null;
                        }

                        configObj.SyncDate = DateTime.UtcNow;

                        if (messageName == "Delete")
                        {
                            configObj.Deleted = true;
                            configObj.DeletedDate = DateTime.UtcNow;
                        }
                        else
                        {
                            configObj.Deleted = false;
                            configObj.DeletedDate = null;
                        }

                        // Virtual Enties are null:
                        configObj.PaymentProcessor = null;
                        configObj.Event = new HashSet<MSNFP_Event>();
                        configObj.EventPackage = new HashSet<MSNFP_EventPackage>();
                        configObj.PaymentSchedule = new HashSet<MSNFP_PaymentSchedule>();
                        configObj.ReceiptStack = new HashSet<MSNFP_ReceiptStack>();
                        configObj.Transaction = new HashSet<MSNFP_Transaction>();

                        localContext.TracingService.Trace("Gathered fields.");

                        apiUrl = configObj.AzureWebApiUrl;

                        // Removed deprecated field reference - Is Enable Pages
                        localContext.TracingService.Trace("API URL: " + apiUrl);
                        if (apiUrl.Length > 0)
                        {
                            localContext.TracingService.Trace("Syncing data to Azure.");
                            if (messageName == "Update" || messageName == "Delete")
                            {
                                apiUrl += "Configuration/UpdateConfiguration";
                            }
                            else if (messageName == "Create")
                            {
                                apiUrl += "Configuration/CreateConfiguration";
                            }

                            MemoryStream ms = new MemoryStream();
                            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Configuration));
                            ser.WriteObject(ms, configObj);
                            byte[] json = ms.ToArray();
                            ms.Close();

                            var json1 = Encoding.UTF8.GetString(json, 0, json.Length);

                            localContext.TracingService.Trace("---------Preparing JSON---------");
                            localContext.TracingService.Trace("Converted to json API URL : " + apiUrl);
                            localContext.TracingService.Trace("JSON: " + json1);
                            localContext.TracingService.Trace("---------End of Preparing JSON---------");
                            localContext.TracingService.Trace("Sending data to Azure.");

                            WebAPIClient client = new WebAPIClient();
                            client.Headers[HttpRequestHeader.ContentType] = "application/json";

                            if (primaryConfig.Contains("msnfp_apipadlocktoken"))
                            {
                                if ((string)primaryConfig["msnfp_apipadlocktoken"] != null)
                                {
                                    client.Headers["Padlock"] = (string)primaryConfig["msnfp_apipadlocktoken"];
                                    client.Encoding = UTF8Encoding.UTF8;

                                    string fileContent = client.UploadString(apiUrl, json1);

                                    localContext.TracingService.Trace("Got response.");
                                    localContext.TracingService.Trace("Response: " + fileContent);

                                    // Here we display an error if we do not get a 200 OK from the API:
                                    Utilities util = new Utilities();
                                    util.CheckAPIReturnJSONForErrors(fileContent, primaryConfig.GetAttributeValue<OptionSetValue>("msnfp_showapierrorresponses"), localContext.TracingService);

                                    var currencies = service.RetrieveMultiple(
                                        new QueryExpression { EntityName = "transactioncurrency", 
                                            ColumnSet = new ColumnSet("transactioncurrencyid" )});

                                    foreach (var currency in currencies.Entities)
                                    {
                                        var req = new OrganizationRequest("msnfp_ActionSyncCurrency");
                                        req["Target"] = currency.ToEntityReference();        
                                        service.Execute(req);
                                        localContext.TracingService.Trace($"currency {currency.Id} updated.");
                                    }
                                }
                            }
                            else
                            {
                                localContext.TracingService.Trace("No padlock found, did not send data to Azure.");
                            }
                        }
                        else
                        {
                            localContext.TracingService.Trace("API URL is null or Portal Syncing is turned off. Exiting Plugin.");
                        }
                    }
                    //}
                }

                /* Delete if the message is delete. This is the primary delete entrypoint:
                if (messageName == "Delete")
                {
                    localContext.TracingService.Trace("Attempting to delete record: " + ((EntityReference)context.InputParameters["Target"]).Id);
                    DeleteThisRecordFromAzure(((EntityReference)context.InputParameters["Target"]).Id, localContext, service, context);

                }*/
            }
        }

        private static ColumnSet GetColumnSet()
        {
            return new ColumnSet("statuscode","statecode","msnfp_defaultconfiguration", "msnfp_configurationid", "msnfp_identifier", "msnfp_azure_webapiurl", "msnfp_bankrun_pregeneratedby", "msnfp_charitytitle", "msnfp_sche_maxretries", "msnfp_sche_retryinterval", "msnfp_teamownerid", "msnfp_sche_recurrencestart", "msnfp_paymentprocessorid", "createdon", "msnfp_apipadlocktoken");
        }
    }
}
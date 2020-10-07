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
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class ReceiptStackCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public ReceiptStackCreate(string unsecure, string secure)
            : base(typeof(ReceiptStackCreate))
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
            localContext.TracingService.Trace("---------Triggered ReceiptStackCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            //OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

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

            configurationRecord = Utilities.GetConfigurationRecordByMessageName(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering ReceiptStackCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_receiptstack", targetIncomingRecord.Id, GetColumnSet());
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
                    queriedEntityRecord = service.Retrieve("msnfp_receiptstack", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting ReceiptStackCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_receiptstackid","msnfp_configurationid","msnfp_currentrange","msnfp_numberrange","msnfp_prefix","msnfp_receiptyear","msnfp_startingrange","statecode","statuscode","createdon");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "ReceiptStack"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_receiptstackid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_ReceiptStack jsonDataObj = new MSNFP_ReceiptStack();

                jsonDataObj.ReceiptStackId = (Guid)queriedEntityRecord["msnfp_receiptstackid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.Identifier = queriedEntityRecord.Contains("msnfp_identifier") ? (string)queriedEntityRecord["msnfp_identifier"] : string.Empty;
                localContext.TracingService.Trace("Title: " + jsonDataObj.Identifier);

                if (queriedEntityRecord.Contains("msnfp_configurationid") && queriedEntityRecord["msnfp_configurationid"] != null)
                {
                    jsonDataObj.ConfigurationId = ((EntityReference)queriedEntityRecord["msnfp_configurationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_configurationid.");
                }
                else
                {
                    jsonDataObj.ConfigurationId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_configurationid.");
                }


                if (queriedEntityRecord.Contains("msnfp_currentrange") && queriedEntityRecord["msnfp_currentrange"] != null)
                {
                    jsonDataObj.CurrentRange = (double)queriedEntityRecord["msnfp_currentrange"];
                    localContext.TracingService.Trace("Got msnfp_currentrange.");
                }
                else
                {
                    jsonDataObj.CurrentRange = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_currentrange.");
                }


                if (queriedEntityRecord.Contains("msnfp_numberrange") && queriedEntityRecord["msnfp_numberrange"] != null)
                {
                    jsonDataObj.NumberRange = ((OptionSetValue)queriedEntityRecord["msnfp_numberrange"]).Value;
                    localContext.TracingService.Trace("Got msnfp_numberrange.");
                }
                else
                {
                    jsonDataObj.NumberRange = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_numberrange.");
                }


                if (queriedEntityRecord.Contains("msnfp_prefix") && queriedEntityRecord["msnfp_prefix"] != null)
                {
                    jsonDataObj.Prefix = (string)queriedEntityRecord["msnfp_prefix"];
                    localContext.TracingService.Trace("Got msnfp_prefix.");
                }
                else
                {
                    jsonDataObj.Prefix = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_prefix.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptyear") && queriedEntityRecord["msnfp_receiptyear"] != null)
                {
                    jsonDataObj.ReceiptYear = ((OptionSetValue)queriedEntityRecord["msnfp_receiptyear"]).Value;
                    localContext.TracingService.Trace("Got msnfp_receiptyear.");
                }
                else
                {
                    jsonDataObj.ReceiptYear = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptyear.");
                }


                if (queriedEntityRecord.Contains("msnfp_startingrange") && queriedEntityRecord["msnfp_startingrange"] != null)
                {
                    jsonDataObj.StartingRange = (double)queriedEntityRecord["msnfp_startingrange"];
                    localContext.TracingService.Trace("Got msnfp_startingrange.");
                }
                else
                {
                    jsonDataObj.StartingRange = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_startingrange.");
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

                // TODO: Missing values, to be added later once they are setup in NFP Dynamics:
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

                jsonDataObj.Configuration = null;
                jsonDataObj.Receipt = new HashSet<MSNFP_Receipt>();
                jsonDataObj.ReceiptLog = new HashSet<MSNFP_ReceiptLog>();

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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_ReceiptStack));
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

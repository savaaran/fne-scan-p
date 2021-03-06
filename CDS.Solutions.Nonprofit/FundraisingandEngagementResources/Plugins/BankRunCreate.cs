﻿/*************************************************************************
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
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class BankRunCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public BankRunCreate(string unsecure, string secure)
            : base(typeof(BankRunCreate))
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
            localContext.TracingService.Trace("---------Triggered BankRunCreate.cs---------");

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

            // Get the Configuration Record (Either from the User or from the Default Configuration Record
            configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);


            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering BankRunCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_bankrun", targetIncomingRecord.Id, GetColumnSet());
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
                    queriedEntityRecord = service.Retrieve("msnfp_bankrun", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting BankRunCreate.cs---------");
            }
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "BankRun"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);

            if (apiUrl != string.Empty)
            {
                

                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_bankrunid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_BankRun jsonDataObj = new MSNFP_BankRun();

                jsonDataObj.BankRunId = (Guid)queriedEntityRecord["msnfp_bankrunid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.Identifier = queriedEntityRecord.Contains("msnfp_identifier") ? (string)queriedEntityRecord["msnfp_identifier"] : string.Empty;
                localContext.TracingService.Trace("Identifier: " + jsonDataObj.Identifier);

                if (queriedEntityRecord.Contains("msnfp_startdate") && queriedEntityRecord["msnfp_startdate"] != null)
                {
                    jsonDataObj.StartDate = (DateTime)queriedEntityRecord["msnfp_startdate"];
                    localContext.TracingService.Trace("Got msnfp_startdate.");
                }
                else
                {
                    jsonDataObj.StartDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_startdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_enddate") && queriedEntityRecord["msnfp_enddate"] != null)
                {
                    jsonDataObj.EndDate = (DateTime)queriedEntityRecord["msnfp_enddate"];
                    localContext.TracingService.Trace("Got msnfp_enddate.");
                }
                else
                {
                    jsonDataObj.EndDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_enddate.");
                }


                if (queriedEntityRecord.Contains("msnfp_datetobeprocessed") && queriedEntityRecord["msnfp_datetobeprocessed"] != null)
                {
                    jsonDataObj.DateToBeProcessed = (DateTime)queriedEntityRecord["msnfp_datetobeprocessed"];
                    localContext.TracingService.Trace("Got msnfp_datetobeprocessed.");
                }
                else
                {
                    jsonDataObj.DateToBeProcessed = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_datetobeprocessed.");
                }


                if (queriedEntityRecord.Contains("msnfp_bankrunstatus") && queriedEntityRecord["msnfp_bankrunstatus"] != null)
                {
                    jsonDataObj.BankRunStatus = ((OptionSetValue)queriedEntityRecord["msnfp_bankrunstatus"]).Value;
                    localContext.TracingService.Trace("Got msnfp_bankrunstatus.");
                }
                else
                {
                    jsonDataObj.BankRunStatus = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bankrunstatus.");
                }


                if (queriedEntityRecord.Contains("msnfp_accounttocreditid") && queriedEntityRecord["msnfp_accounttocreditid"] != null)
                {
                    jsonDataObj.AccountToCreditId = ((EntityReference)queriedEntityRecord["msnfp_accounttocreditid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_accounttocreditid");
                }
                else
                {
                    jsonDataObj.AccountToCreditId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_accounttocreditid");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentprocessorid") && queriedEntityRecord["msnfp_paymentprocessorid"] != null)
                {
                    jsonDataObj.PaymentProcessorId = ((EntityReference)queriedEntityRecord["msnfp_paymentprocessorid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentprocessorid");
                }
                else
                {
                    jsonDataObj.PaymentProcessorId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentprocessorid");
                }

                if (queriedEntityRecord.Contains("msnfp_filecreationnumber") && queriedEntityRecord["msnfp_filecreationnumber"] != null)
                {
                    jsonDataObj.FileCreationNumber = ((int)queriedEntityRecord["msnfp_filecreationnumber"]);
                    localContext.TracingService.Trace("Got msnfp_filecreationnumber");
                }
                else
                {
                    jsonDataObj.FileCreationNumber = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_filecreationnumber");
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

                jsonDataObj.StatusCode = ((OptionSetValue)queriedEntityRecord["statuscode"]).Value;
                jsonDataObj.StateCode = ((OptionSetValue)queriedEntityRecord["statecode"]).Value;


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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_BankRun));
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

        private static ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_bankrunid", "msnfp_identifier", "msnfp_startdate", "msnfp_enddate", "msnfp_datetobeprocessed", "msnfp_bankrunstatus", "msnfp_paymentprocessorid", "msnfp_accounttocreditid", "statuscode", "statecode", "createdon", "msnfp_filecreationnumber");
        }
    }
}

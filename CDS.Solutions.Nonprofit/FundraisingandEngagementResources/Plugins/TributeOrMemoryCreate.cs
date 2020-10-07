/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Net;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using Plugins.AzureModels;
using Plugins.PaymentProcesses;

namespace Plugins
{
    public class TributeOrMemoryCreate : PluginBase
    {
        private const string PostImageAlias = "msnfp_tributeormemory";

        /// <summary>
        /// Initializes a new instance of the <see cref="TributeOrMemoryCreate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public TributeOrMemoryCreate(string unsecure, string secure)
            : base(typeof(TributeOrMemoryCreate))
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
            localContext.TracingService.Trace("---------Triggered TributeOrMemoryCreate.cs---------");

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

            // Get the Configuration Record (Either from the User or from the Default Configuration Record
            configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering TributeOrMemoryCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve(PostImageAlias, targetIncomingRecord.Id, GetColumnSet());
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
                    queriedEntityRecord = service.Retrieve(PostImageAlias, ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting TributeOrMemoryCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_tributeormemoryid", "msnfp_identifier", "msnfp_name", "msnfp_tributeormemorytypecode", "statecode", "statuscode", "createdon");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "TributeOrMemory"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_tributeormemoryid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_TributeOrMemory jsonDataObj = new MSNFP_TributeOrMemory();

                jsonDataObj.TributeOrMemoryId = (Guid)queriedEntityRecord["msnfp_tributeormemoryid"];

                // Now we get all the fields for this entity and save them to a JSON object.
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


                if (queriedEntityRecord.Contains("msnfp_tributeormemorytypecode") && queriedEntityRecord["msnfp_tributeormemorytypecode"] != null)
                {
                    jsonDataObj.TributeOrMemoryTypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_tributeormemorytypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_tributeormemorytypecode.");
                }
                else
                {
                    jsonDataObj.TributeOrMemoryTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributeormemorytypecode.");
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
                    localContext.TracingService.Trace("Setting Deleted Date to:" + jsonDataObj.DeletedDate);
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_TributeOrMemory));
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
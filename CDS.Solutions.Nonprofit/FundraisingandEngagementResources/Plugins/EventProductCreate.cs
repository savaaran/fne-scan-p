/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using System.Collections.Generic;
using Plugins.PaymentProcesses;
using Plugins.AzureModels;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class EventProductCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public EventProductCreate(string unsecure, string secure)
            : base(typeof(EventProductCreate))
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
            localContext.TracingService.Trace("---------Triggered EventProductCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Entity queriedEntityRecord = null;
            Entity targetIncomingRecord; // Note target is used in Azure sync.
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
                    localContext.TracingService.Trace("---------Entering EventProductCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_eventproduct", targetIncomingRecord.Id, GetColumnSet());
                    }

                    if (targetIncomingRecord != null)
                    {
                        // Sync this to Azure. Note we use the target here as we want all the columns:
                        if (messageName == "Create")
                        {
                            // Update Products Available
                            if (targetIncomingRecord.Contains("msnfp_quantity") && targetIncomingRecord["msnfp_quantity"] != null)
                            {
                                targetIncomingRecord["msnfp_sum_available"] = (int)targetIncomingRecord["msnfp_quantity"];

                                // updating Event Product record
                                service.Update(targetIncomingRecord);
                            }

                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);
                        }
                        else if (messageName == "Update")
                        {
                            // Updating Event Totals
                            UpdateEventTotals(queriedEntityRecord, orgSvcContext, service, localContext);

                            AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
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
                    queriedEntityRecord = service.Retrieve("msnfp_eventproduct", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting EventProductCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_eventproductid", "msnfp_amount", "msnfp_description", "msnfp_amount_receipted", "msnfp_amount_nonreceiptable", "msnfp_eventid", "msnfp_maxproducts", "msnfp_sum_available", "msnfp_quantity", "msnfp_restrictperregistration", "msnfp_amount_tax", "msnfp_val_sold", "msnfp_identifier", "msnfp_sum_sold", "transactioncurrencyid", "statecode", "statuscode", "createdon");           
        }


        #region Updating Event Totals
        private void UpdateEventTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventTotals---------");

            int countProducts = 0;
            decimal sumProducts = 0;
            Entity parentEvent = service.Retrieve("msnfp_event", ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id, new ColumnSet(new string[] { "msnfp_eventid", "msnfp_sum_products", "msnfp_count_products" }));

            List<Entity> eventProductList = (from a in orgSvcContext.CreateQuery("msnfp_eventproduct")
                                             where ((EntityReference)a["msnfp_eventid"]).Id == ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id
                                             && ((OptionSetValue)a["statecode"]).Value == 0 && ((OptionSetValue)a["statuscode"]).Value != 844060000 //cancelled
                                             select a).ToList();

            if (eventProductList.Count > 0)
            {
                foreach (Entity item in eventProductList)
                {
                    if (item.Contains("msnfp_sum_sold"))
                        countProducts += (int)item["msnfp_sum_sold"];

                    if (item.Contains("msnfp_val_sold"))
                        sumProducts += ((Money)item["msnfp_val_sold"]).Value;
                }
            }

            parentEvent["msnfp_count_products"] = countProducts;
            parentEvent["msnfp_sum_products"] = new Money(sumProducts);

            decimal totalRevenue =
                Utilities.CalculateEventTotalRevenue(parentEvent, service, orgSvcContext, localContext.TracingService);
            parentEvent["msnfp_sum_total"] = new Money(totalRevenue);
            
            // updating event record
            service.Update(parentEvent);
            localContext.TracingService.Trace("Event Record Updated");
        }
        #endregion


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "EventProduct"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_eventproductid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_EventProduct jsonDataObj = new MSNFP_EventProduct();

                jsonDataObj.EventProductId = (Guid)queriedEntityRecord["msnfp_eventproductid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                if (queriedEntityRecord.Contains("msnfp_amount") && queriedEntityRecord["msnfp_amount"] != null)
                {
                    jsonDataObj.Amount = ((Money)queriedEntityRecord["msnfp_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount");
                }
                else
                {
                    jsonDataObj.Amount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount.");
                }

                if (queriedEntityRecord.Contains("msnfp_description") && queriedEntityRecord["msnfp_description"] != null)
                {
                    jsonDataObj.Description = (string)queriedEntityRecord["msnfp_description"];
                    localContext.TracingService.Trace("Got msnfp_description.");
                }
                else
                {
                    jsonDataObj.Description = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_description.");
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

                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonReceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.AmountNonReceiptable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_nonreceiptable.");
                }

                if (queriedEntityRecord.Contains("msnfp_eventid") && queriedEntityRecord["msnfp_eventid"] != null)
                {
                    jsonDataObj.EventId = ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventid.");
                }
                else
                {
                    jsonDataObj.EventId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventid.");
                }

                if (queriedEntityRecord.Contains("msnfp_maxproducts") && queriedEntityRecord["msnfp_maxproducts"] != null)
                {
                    jsonDataObj.MaxProducts = (int)queriedEntityRecord["msnfp_maxproducts"];
                    localContext.TracingService.Trace("Got msnfp_maxproducts.");
                }
                else
                {
                    jsonDataObj.MaxProducts = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_maxproducts.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_available") && queriedEntityRecord["msnfp_sum_available"] != null)
                {
                    jsonDataObj.ValAvailable = (int)queriedEntityRecord["msnfp_sum_available"];
                    localContext.TracingService.Trace("Got msnfp_sum_available.");
                }
                else
                {
                    jsonDataObj.ValAvailable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_val_available.");
                }

                if (queriedEntityRecord.Contains("msnfp_quantity") && queriedEntityRecord["msnfp_quantity"] != null)
                {
                    jsonDataObj.Quantity = (int)queriedEntityRecord["msnfp_quantity"];
                    localContext.TracingService.Trace("Got msnfp_quantity.");
                }
                else
                {
                    jsonDataObj.Quantity = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_quantity.");
                }

                if (queriedEntityRecord.Contains("msnfp_restrictperregistration") && queriedEntityRecord["msnfp_restrictperregistration"] != null)
                {
                    jsonDataObj.RestrictPerRegistration = (bool)queriedEntityRecord["msnfp_restrictperregistration"];
                    localContext.TracingService.Trace("Got msnfp_restrictperregistration.");
                }
                else
                {
                    jsonDataObj.RestrictPerRegistration = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_restrictperregistration.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_sold") && queriedEntityRecord["msnfp_sum_sold"] != null)
                {
                    jsonDataObj.ValSold = (int)queriedEntityRecord["msnfp_sum_sold"];
                    localContext.TracingService.Trace("Got msnfp_sum_sold.");
                }
                else
                {
                    jsonDataObj.ValSold = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_sold.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_tax") && queriedEntityRecord["msnfp_amount_tax"] != null)
                {
                    jsonDataObj.AmountTax = ((Money)queriedEntityRecord["msnfp_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_tax.");
                }
                else
                {
                    jsonDataObj.AmountTax = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_tax.");
                }

                if (queriedEntityRecord.Contains("msnfp_val_sold") && queriedEntityRecord["msnfp_val_sold"] != null)
                {
                    jsonDataObj.SumSold = ((Money)queriedEntityRecord["msnfp_val_sold"]).Value;
                    localContext.TracingService.Trace("Got msnfp_val_sold.");
                }
                else
                {
                    jsonDataObj.SumSold = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_val_sold.");
                }

                if (queriedEntityRecord.Contains("msnfp_identifier") && queriedEntityRecord["msnfp_identifier"] != null)
                {
                    jsonDataObj.Identifier = (string)queriedEntityRecord["msnfp_identifier"];
                    localContext.TracingService.Trace("Got msnfp_identifier.");
                }
                else
                {
                    jsonDataObj.Identifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_identifier.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_sold") && queriedEntityRecord["msnfp_sum_sold"] != null)
                {
                    jsonDataObj.ValSold = ((int)queriedEntityRecord["msnfp_sum_sold"]);
                    localContext.TracingService.Trace("Got msnfp_sum_sold.");
                }
                else
                {
                    jsonDataObj.ValSold = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_sold.");
                }


                if (queriedEntityRecord.Contains("transactioncurrencyid") && queriedEntityRecord["transactioncurrencyid"] != null)
                {
                    jsonDataObj.TransactionCurrencyId = ((EntityReference)queriedEntityRecord["transactioncurrencyid"]).Id;
                    localContext.TracingService.Trace("Got transactioncurrencyid.");
                }
                else
                {
                    jsonDataObj.TransactionCurrencyId = null;
                    localContext.TracingService.Trace("Did NOT find transactioncurrencyid.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_EventProduct));
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
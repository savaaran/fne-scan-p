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
    public class EventSponsorshipPageCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public EventSponsorshipPageCreate(string unsecure, string secure)
            : base(typeof(EventSponsorshipPageCreate))
        {
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
            localContext.TracingService.Trace("---------Triggered EventSponsorshipPageCreate.cs---------");

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
                    localContext.TracingService.Trace("---------Entering EventSponsorshipPageCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_eventsponsorship", targetIncomingRecord.Id, GetColumnSet());
                    }

                    if (targetIncomingRecord != null)
                    {
                        if (messageName == "Create")
                        {
                            // Update Sponsorships Available
                            if (targetIncomingRecord.Contains("msnfp_quantity") && targetIncomingRecord["msnfp_quantity"] != null)
                            {
                                //targetIncomingRecord["msnfp_sum_available"] = (int)targetIncomingRecord["msnfp_quantity"];

                                // updating Event Sponsorship 
                                Entity recordToUpdate = new Entity(targetIncomingRecord.LogicalName, targetIncomingRecord.Id);
                                recordToUpdate["msnfp_sum_available"] = (int)targetIncomingRecord["msnfp_quantity"];
                                service.Update(recordToUpdate);
                            }

                            // Sync this to Azure. Note we use the target here as we want all the columns:
                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);
                        }
                        else if (messageName == "Update")
                        {
                            // Updating Sponsorships Available
                            if (targetIncomingRecord.Contains("msnfp_quantity") && targetIncomingRecord["msnfp_quantity"] != null)
                                UpdateEventSponsorshipsAvailable(queriedEntityRecord, orgSvcContext, service, localContext);

                            // Updating Event Totals
                            UpdateEventTotals(queriedEntityRecord, orgSvcContext, service, localContext);

                            // Sync this to Azure. Note we use the target here as we want all the columns:
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
                    queriedEntityRecord = service.Retrieve("msnfp_eventsponsorship", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting EventSponsorshipPageCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_eventsponsorshipid", "msnfp_advantage", "msnfp_amount", "msnfp_date", "msnfp_description", "msnfp_eventid", "msnfp_order", "msnfp_quantity", "msnfp_fromamount", "msnfp_sum_available", "msnfp_val_sold", "msnfp_identifier", "msnfp_sum_sold", "transactioncurrencyid", "msnfp_amount_nonreceiptable" ,"msnfp_amount_receipted", "statecode", "statuscode", "createdon");
        }


        #region Updating Event Sponsorships Available
        private void UpdateEventSponsorshipsAvailable(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            Entity eventSponsorship = service.Retrieve("msnfp_eventsponsorship", queriedEntityRecord.Id, new ColumnSet(new string[] { "msnfp_eventsponsorshipid", "msnfp_sum_available" }));
            Entity eventSponsorshipToUpdate = new Entity(eventSponsorship.LogicalName, eventSponsorship.Id);

            List<Entity> sponsorshipList = (from a in orgSvcContext.CreateQuery("msnfp_sponsorship")
                                            where ((EntityReference)a["msnfp_eventsponsorshipid"]).Id == eventSponsorship.Id
                                            && ((OptionSetValue)a["statuscode"]).Value != 844060001 //cancelled
                                            select a).ToList();

            if (queriedEntityRecord.Contains("msnfp_quantity") && queriedEntityRecord["msnfp_quantity"] != null)
                //eventSponsorship["msnfp_sum_available"] = (int)queriedEntityRecord["msnfp_quantity"] - sponsorshipList.Count(); // sponsorships available
                eventSponsorshipToUpdate["msnfp_sum_available"] =
                    (int)queriedEntityRecord["msnfp_quantity"] - sponsorshipList.Count(); // sponsorships available

            // updating Event Sponsorship record
            service.Update(eventSponsorshipToUpdate);

        }
        #endregion


        #region Updating Event Totals
        private void UpdateEventTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventTotals---------");

            int countSponsorships = 0;
            decimal sumSponsorships = 0;
            Entity parentEvent = service.Retrieve("msnfp_event", ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id, new ColumnSet(new string[] { "msnfp_eventid", "msnfp_sum_sponsorships", "msnfp_count_sponsorships" }));

            List<Entity> eventSponsorshipList = (from a in orgSvcContext.CreateQuery("msnfp_eventsponsorship")
                                                 where ((EntityReference)a["msnfp_eventid"]).Id == ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id
                                                 && ((OptionSetValue)a["statecode"]).Value == 0 && ((OptionSetValue)a["statuscode"]).Value != 844060000 //cancelled
                                                 select a).ToList();

            if (eventSponsorshipList.Count > 0)
            {
                foreach (Entity item in eventSponsorshipList)
                {
                    if (item.Contains("msnfp_sum_sold"))
                        countSponsorships += (int)item["msnfp_sum_sold"];

                    if (item.Contains("msnfp_val_sold"))
                        sumSponsorships += ((Money)item["msnfp_val_sold"]).Value;
                }
            }

            parentEvent["msnfp_count_sponsorships"] = countSponsorships;
            parentEvent["msnfp_sum_sponsorships"] = new Money(sumSponsorships);

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
            string entityName = "EventSponsorship"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_eventsponsorshipid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_EventSponsorship jsonDataObj = new MSNFP_EventSponsorship();

                jsonDataObj.EventSponsorshipId = (Guid)queriedEntityRecord["msnfp_eventsponsorshipid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                if (queriedEntityRecord.Contains("msnfp_amount") && queriedEntityRecord["msnfp_amount"] != null)
                {
                    jsonDataObj.Amount = ((Money)queriedEntityRecord["msnfp_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount.");
                }
                else
                {
                    jsonDataObj.Amount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount.");
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

                if (queriedEntityRecord.Contains("msnfp_date") && queriedEntityRecord["msnfp_date"] != null)
                {
                    jsonDataObj.Date = (DateTime)queriedEntityRecord["msnfp_date"];
                    localContext.TracingService.Trace("Got msnfp_date.");
                }
                else
                {
                    jsonDataObj.Date = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_date.");
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

                if (queriedEntityRecord.Contains("msnfp_order") && queriedEntityRecord["msnfp_order"] != null)
                {
                    jsonDataObj.Order = (int)queriedEntityRecord["msnfp_order"];
                    localContext.TracingService.Trace("Got msnfp_order.");
                }
                else
                {
                    jsonDataObj.Order = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_order.");
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

                if (queriedEntityRecord.Contains("msnfp_fromamount") && queriedEntityRecord["msnfp_fromamount"] != null)
                {
                    jsonDataObj.FromAmount = ((Money)queriedEntityRecord["msnfp_fromamount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_fromamount.");
                }
                else
                {
                    jsonDataObj.FromAmount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_fromamount.");
                }

                if (queriedEntityRecord.Contains("msnfp_val_sold") && queriedEntityRecord["msnfp_val_sold"] != null)
                {
                    jsonDataObj.ValSold = ((Money)queriedEntityRecord["msnfp_val_sold"]).Value;
                    localContext.TracingService.Trace("Got msnfp_val_sold.");
                }
                else
                {
                    jsonDataObj.ValSold = null;
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
                    jsonDataObj.SumSold = (int)queriedEntityRecord["msnfp_sum_sold"];
                    localContext.TracingService.Trace("Got msnfp_sum_sold.");
                }
                else
                {
                    jsonDataObj.SumSold = null;
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
                    localContext.TracingService.Trace("Did NOT find msnfp_TransactionCurrencyId.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_EventSponsorship));
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

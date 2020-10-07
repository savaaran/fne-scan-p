
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
using Plugins.PaymentProcesses;
using Plugins.AzureModels;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class EventCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public EventCreate(string unsecure, string secure)
            : base(typeof(EventCreate))
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
            localContext.TracingService.Trace("---------Triggered EventCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

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

            configurationRecord = Utilities.GetConfigurationRecordByMessageName(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering EventCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        // need all columns for azure sync
                        queriedEntityRecord = service.Retrieve("msnfp_event", targetIncomingRecord.Id, GetColumnSet());
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

                            //if (messageName.Equals("Update") && targetIncomingRecord.Contains("statecode") && targetIncomingRecord.Contains("statuscode") && ((OptionSetValue)targetIncomingRecord["statecode"]).Value.Equals(1))
                            //{
                            //    localContext.TracingService.Trace("Statecode= " + ((OptionSetValue)targetIncomingRecord["statecode"]).Value);
                            //    DeleteEventOrdersOnListDeactivation(service, targetIncomingRecord.ToEntityReference(), localContext);
                            //}
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
                    queriedEntityRecord = service.Retrieve("msnfp_event", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting EventCreate.cs---------");
            }
        }

        private void DeleteEventOrdersOnListDeactivation(IOrganizationService service, EntityReference entRefEvent, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("Inside DeleteEventOrdersOnListDeactivation Method.");
            QueryExpression orderQuery = new QueryExpression("msnfp_eventorder");
            orderQuery.Criteria.AddCondition("msnfp_fromeventid", ConditionOperator.Equal, entRefEvent.Id);
            orderQuery.ColumnSet.AddColumns("msnfp_toeventlistid", "msnfp_fromeventlistid");

            EntityCollection entColOrders = service.RetrieveMultiple(orderQuery);

            if (entColOrders.Entities.Count > 0)
            {
                localContext.TracingService.Trace("Delete Event Order count- " + entColOrders.Entities.Count);
                foreach (Entity ent in entColOrders.Entities)
                {
                    service.Delete(ent.LogicalName, ent.Id);
                    //Reorder event order records if any record got deleted
                    ReOrderEventOrder(service, (EntityReference)ent["msnfp_toeventlistid"], localContext);
                }

            }
        }

        private void ReOrderEventOrder(IOrganizationService service, EntityReference entRefToEventList, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("ReOrder Event Order");
            QueryExpression orderQuery = new QueryExpression("msnfp_eventorder");
            orderQuery.ColumnSet.AddColumns("msnfp_order");
            orderQuery.Criteria.AddCondition("msnfp_toeventlistid", ConditionOperator.Equal, entRefToEventList.Id);
            orderQuery.AddOrder("msnfp_order", OrderType.Ascending);
            EntityCollection entColOrders = service.RetrieveMultiple(orderQuery);

            int count = 1;
            if (entColOrders.Entities.Count > 0)
            {
                foreach (Entity ent in entColOrders.Entities)
                {
                    if (ent.Contains("msnfp_order") && (int)ent["msnfp_order"] != count)
                    {
                        ent["msnfp_order"] = count;
                        service.Update(ent);
                    }
                    count++;
                }
            }
        }

        private static ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_eventid", "msnfp_goal", "msnfp_capacity", "msnfp_description", "msnfp_name", "msnfp_amount", "msnfp_eventtypecode", "msnfp_identifier", "msnfp_map_line1",
                "msnfp_map_line2", "msnfp_map_line3", "msnfp_map_city", "msnfp_stateorprovince", "msnfp_map_postalcode", "msnfp_map_country", "msnfp_proposedend", "msnfp_proposedstart",
                "msnfp_campaignid", "msnfp_appealid", "msnfp_packageid", "msnfp_designationid", "msnfp_configurationid", "msnfp_venueid",
                "transactioncurrencyid", "statecode", "statuscode", "createdon");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Event"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_eventid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_Event jsonDataObj = new MSNFP_Event
                {
                    EventId = (Guid)queriedEntityRecord["msnfp_eventid"]
                };



                localContext.TracingService.Trace("Got myHttpWebResponse: -1");

                // Now we get all the fields for this entity and save them to a JSON object.

                if (queriedEntityRecord.Contains("msnfp_goal") && queriedEntityRecord["msnfp_goal"] != null)
                {
                    jsonDataObj.Goal = ((Money)queriedEntityRecord["msnfp_goal"]).Value;
                    localContext.TracingService.Trace("Got msnfp_goal");
                }
                else
                {
                    jsonDataObj.Goal = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_goal.");
                }

                if (queriedEntityRecord.Contains("msnfp_capacity") && queriedEntityRecord["msnfp_capacity"] != null)
                {
                    jsonDataObj.Capacity = (int)queriedEntityRecord["msnfp_capacity"];
                    localContext.TracingService.Trace("Got msnfp_capacity");
                }
                else
                {
                    jsonDataObj.Capacity = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_capacity.");
                }


                //if (queriedEntityRecord.Contains("msnfp_coordinator") && queriedEntityRecord["msnfp_coordinator"] != null)
                //{
                //    jsonDataObj.Coordinator = (string)queriedEntityRecord["msnfp_coordinator"];
                //    localContext.TracingService.Trace("Got msnfp_coordinator");
                //}
                //else
                //{
                //    jsonDataObj.Coordinator = string.Empty;
                //    localContext.TracingService.Trace("Did NOT find msnfp_coordinator.");
                //}

                //if (queriedEntityRecord.Contains("msnfp_timeanddate") && queriedEntityRecord["msnfp_timeanddate"] != null)
                //{
                //    jsonDataObj.TimeAndDate = (string)queriedEntityRecord["msnfp_timeanddate"];
                //    localContext.TracingService.Trace("Got msnfp_timeanddate");
                //}
                //else
                //{
                //    jsonDataObj.TimeAndDate = string.Empty;
                //    localContext.TracingService.Trace("Did NOT find msnfp_timeanddate.");
                //}

                if (queriedEntityRecord.Contains("msnfp_description") && queriedEntityRecord["msnfp_description"] != null)
                {
                    jsonDataObj.Description = (string)queriedEntityRecord["msnfp_description"];
                    localContext.TracingService.Trace("Got msnfp_description");
                }
                else
                {
                    jsonDataObj.Description = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_description.");
                }
                               
                if (queriedEntityRecord.Contains("msnfp_name") && queriedEntityRecord["msnfp_name"] != null)
                {
                    jsonDataObj.Name = (string)queriedEntityRecord["msnfp_name"];
                    localContext.TracingService.Trace("Got msnfp_name");
                }
                else
                {
                    jsonDataObj.Name = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_name.");
                }

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

                if (queriedEntityRecord.Contains("msnfp_eventtypecode") && queriedEntityRecord["msnfp_eventtypecode"] != null)
                {
                    jsonDataObj.EventTypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_eventtypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_eventtypecode");
                }
                else
                {
                    jsonDataObj.EventTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventtypecode.");
                }

                if (queriedEntityRecord.Contains("msnfp_identifier") && queriedEntityRecord["msnfp_identifier"] != null)
                {
                    jsonDataObj.Identifier = (string)queriedEntityRecord["msnfp_identifier"];
                    localContext.TracingService.Trace("Got msnfp_identifier");
                }
                else
                {
                    jsonDataObj.Identifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_identifier.");
                }

                if (queriedEntityRecord.Contains("msnfp_map_line1") && queriedEntityRecord["msnfp_map_line1"] != null)
                {
                    jsonDataObj.MapLine1 = (string)queriedEntityRecord["msnfp_map_line1"];
                    localContext.TracingService.Trace("Got msnfp_map_line1");
                }
                else
                {
                    jsonDataObj.MapLine1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_map_line1.");
                }

                if (queriedEntityRecord.Contains("msnfp_map_line2") && queriedEntityRecord["msnfp_map_line2"] != null)
                {
                    jsonDataObj.MapLine2 = (string)queriedEntityRecord["msnfp_map_line2"];
                    localContext.TracingService.Trace("Got msnfp_map_line2");
                }
                else
                {
                    jsonDataObj.MapLine2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_map_line2.");
                }

                if (queriedEntityRecord.Contains("msnfp_map_line3") && queriedEntityRecord["msnfp_map_line3"] != null)
                {
                    jsonDataObj.MapLine3 = (string)queriedEntityRecord["msnfp_map_line3"];
                    localContext.TracingService.Trace("Got msnfp_map_line3");
                }
                else
                {
                    jsonDataObj.MapLine3 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_map_line3.");
                }

                if (queriedEntityRecord.Contains("msnfp_map_city") && queriedEntityRecord["msnfp_map_city"] != null)
                {
                    jsonDataObj.MapCity = (string)queriedEntityRecord["msnfp_map_city"];
                    localContext.TracingService.Trace("Got msnfp_map_city");
                }
                else
                {
                    jsonDataObj.MapCity = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_map_city.");
                }

                if (queriedEntityRecord.Contains("msnfp_stateorprovince") && queriedEntityRecord["msnfp_stateorprovince"] != null)
                {
                    jsonDataObj.MapStateOrProvince = (string)queriedEntityRecord["msnfp_stateorprovince"];
                    localContext.TracingService.Trace("Got msnfp_stateorprovince");
                }
                else
                {
                    jsonDataObj.MapStateOrProvince = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_stateorprovince.");
                }

                if (queriedEntityRecord.Contains("msnfp_map_postalcode") && queriedEntityRecord["msnfp_map_postalcode"] != null)
                {
                    jsonDataObj.MapPostalCode = (string)queriedEntityRecord["msnfp_map_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_map_postalcode");
                }
                else
                {
                    jsonDataObj.MapPostalCode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_map_postalcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_map_country") && queriedEntityRecord["msnfp_map_country"] != null)
                {
                    jsonDataObj.MapCountry = (string)queriedEntityRecord["msnfp_map_country"];
                    localContext.TracingService.Trace("Got msnfp_map_country");
                }
                else
                {
                    jsonDataObj.MapCountry = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_map_country.");
                }

                if (queriedEntityRecord.Contains("msnfp_proposedend") && queriedEntityRecord["msnfp_proposedend"] != null)
                {
                    jsonDataObj.ProposedEnd = (DateTime)queriedEntityRecord["msnfp_proposedend"];
                    localContext.TracingService.Trace("Got msnfp_proposedend");
                }
                else
                {
                    jsonDataObj.ProposedEnd = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_proposedend.");
                }

                if (queriedEntityRecord.Contains("msnfp_proposedstart") && queriedEntityRecord["msnfp_proposedstart"] != null)
                {
                    jsonDataObj.ProposedStart = (DateTime)queriedEntityRecord["msnfp_proposedstart"];
                    localContext.TracingService.Trace("Got msnfp_proposedstart");
                }
                else
                {
                    jsonDataObj.ProposedStart = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_proposedstart.");
                }

                if (queriedEntityRecord.Contains("msnfp_campaignid") && queriedEntityRecord["msnfp_campaignid"] != null)
                {
                    jsonDataObj.CampaignId = ((EntityReference)queriedEntityRecord["msnfp_campaignid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_campaignid");
                }
                else
                {
                    jsonDataObj.CampaignId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_campaignid.");
                }

                if (queriedEntityRecord.Contains("msnfp_appealid") && queriedEntityRecord["msnfp_appealid"] != null)
                {
                    jsonDataObj.AppealId = ((EntityReference)queriedEntityRecord["msnfp_appealid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_appealid");
                }
                else
                {
                    jsonDataObj.AppealId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_appealid.");
                }

                if (queriedEntityRecord.Contains("msnfp_packageid") && queriedEntityRecord["msnfp_packageid"] != null)
                {
                    jsonDataObj.PackageId = ((EntityReference)queriedEntityRecord["msnfp_packageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_packageid");
                }
                else
                {
                    jsonDataObj.PackageId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_packageid.");
                }

                if (queriedEntityRecord.Contains("msnfp_designationid") && queriedEntityRecord["msnfp_designationid"] != null)
                {
                    jsonDataObj.DesignationId = ((EntityReference)queriedEntityRecord["msnfp_designationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_designationid");
                }
                else
                {
                    jsonDataObj.DesignationId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_designationid.");
                }

                if (queriedEntityRecord.Contains("msnfp_configurationid") && queriedEntityRecord["msnfp_configurationid"] != null)
                {
                    jsonDataObj.ConfigurationId = ((EntityReference)queriedEntityRecord["msnfp_configurationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_configurationid");
                }
                else
                {
                    jsonDataObj.ConfigurationId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_configurationid.");
                }

                if (queriedEntityRecord.Contains("msnfp_venueid") && queriedEntityRecord["msnfp_venueid"] != null)
                {
                    jsonDataObj.VenueId = ((EntityReference)queriedEntityRecord["msnfp_venueid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_venueid");
                }
                else
                {
                    jsonDataObj.VenueId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_venueid.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Event));
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

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
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using System.Collections.Generic;
using Plugins.AzureModels;
using Plugins.PaymentProcesses;

namespace Plugins
{
    public class SponsorshipCreate : PluginBase
    {
        private const string PostImageAlias = "msnfp_sponsorship";

        /// <summary>
        /// Initializes a new instance of the <see cref="SponsorshipCreate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public SponsorshipCreate(string unsecure, string secure)
            : base(typeof(SponsorshipCreate))
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
            localContext.TracingService.Trace("---------Triggered SponsorshipCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

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
                    localContext.TracingService.Trace("---------Entering SponsorshipCreate.cs Main Function---------");

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
                            UpdateEventPackageSponsorshipTotals(targetIncomingRecord, orgSvcContext, service, localContext);
                            UpdateEventSponsorshipTotals(targetIncomingRecord, orgSvcContext, service, localContext);
                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);
                        }
                        else if (messageName == "Update")
                        {
                            UpdateEventPackageSponsorshipTotals(queriedEntityRecord, orgSvcContext, service, localContext);
                            UpdateEventSponsorshipTotals(queriedEntityRecord, orgSvcContext, service, localContext);
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

                localContext.TracingService.Trace("---------Exiting SponsorshipCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_sponsorshipid", "msnfp_customerid", "msnfp_eventid", "msnfp_eventpackageid", "msnfp_amount_receipted", "msnfp_amount_nonreceiptable", "msnfp_amount_tax", "msnfp_amount", "msnfp_date", "msnfp_description", "msnfp_name", "msnfp_identifier", "statecode", "statuscode", "createdon", "msnfp_eventpackageid", "msnfp_eventsponsorshipid");
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Sponsorship"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_sponsorshipid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_Sponsorship jsonDataObj = new MSNFP_Sponsorship();

                jsonDataObj.SponsorshipId = (Guid)queriedEntityRecord["msnfp_sponsorshipid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                if (queriedEntityRecord.Contains("msnfp_customerid") && queriedEntityRecord["msnfp_customerid"] != null)
                {
                    jsonDataObj.CustomerId = ((EntityReference)queriedEntityRecord["msnfp_customerid"]).Id;

                    // Set the CustomerIdType. 1 = Account, 2 = Contact:
                    if (((EntityReference)queriedEntityRecord["msnfp_customerid"]).LogicalName.ToLower() == "contact")
                    {
                        jsonDataObj.CustomerIdType = 2;
                    }
                    else if (((EntityReference)queriedEntityRecord["msnfp_customerid"]).LogicalName.ToLower() == "account")
                    {
                        jsonDataObj.CustomerIdType = 1;
                    }

                    localContext.TracingService.Trace("Got msnfp_customerid.");
                }
                else
                {
                    jsonDataObj.CustomerId = null;
                    jsonDataObj.CustomerIdType = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_customerid.");
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

                if (queriedEntityRecord.Contains("msnfp_eventpackageid") && queriedEntityRecord["msnfp_eventpackageid"] != null)
                {
                    jsonDataObj.EventPackageId = ((EntityReference)queriedEntityRecord["msnfp_eventpackageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventpackageid.");
                }
                else
                {
                    jsonDataObj.EventPackageId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventpackageid.");
                }

                if (queriedEntityRecord.Contains("msnfp_eventsponsorshipid") && queriedEntityRecord["msnfp_eventsponsorshipid"] != null)
                {
                    jsonDataObj.EventSponsorshipId = ((EntityReference)queriedEntityRecord["msnfp_eventsponsorshipid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventsponsorshipid.");
                }
                else
                {
                    jsonDataObj.EventSponsorshipId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventsponsorshipid.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_receipted") && queriedEntityRecord["msnfp_amount_receipted"] != null)
                {
                    jsonDataObj.AmountReceipted = ((Money)queriedEntityRecord["msnfp_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_receipted.");
                }
                else
                {
                    jsonDataObj.AmountReceipted = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_receipted.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonreceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.AmountNonreceiptable = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_nonreceiptable.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonreceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.AmountNonreceiptable = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_nonreceiptable.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_tax") && queriedEntityRecord["msnfp_amount_tax"] != null)
                {
                    jsonDataObj.AmountTax = ((Money)queriedEntityRecord["msnfp_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_tax.");
                }
                else
                {
                    jsonDataObj.AmountTax = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_tax.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount") && queriedEntityRecord["msnfp_amount"] != null)
                {
                    jsonDataObj.Amount = ((Money)queriedEntityRecord["msnfp_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount.");
                }
                else
                {
                    jsonDataObj.Amount = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount.");
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
                    jsonDataObj.Description = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_description.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Sponsorship));
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




        #region Updating Event Package Totals

        private void UpdateEventPackageSponsorshipTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventPackageSponsorshipTotals---------");

            if (queriedEntityRecord.Contains("msnfp_eventpackageid"))
            {
                decimal valAmount = 0;
                Entity eventPackage = service.Retrieve("msnfp_eventpackage", ((EntityReference)queriedEntityRecord["msnfp_eventpackageid"]).Id, new ColumnSet(new string[] { "msnfp_eventpackageid", "msnfp_amount" }));

                List<Entity> sponsorshipList = (from a in orgSvcContext.CreateQuery("msnfp_sponsorship")
                                                where ((EntityReference)a["msnfp_eventpackageid"]).Id == eventPackage.Id
                                                && ((OptionSetValue)a["statuscode"]).Value != 844060001 //cancelled
                                                select a).ToList();


                localContext.TracingService.Trace("eventPackage.Id: " + eventPackage.Id.ToString());


                localContext.TracingService.Trace("sponsorshipList.Count(): " + sponsorshipList.Count());

                foreach (Entity item in sponsorshipList)
                {
                    if (item.Contains("msnfp_amount") && item["msnfp_amount"] != null)
                        valAmount += ((Money)item["msnfp_amount"]).Value;
                }

                eventPackage["msnfp_sum_sponsorships"] = sponsorshipList.Count(); // # sponsorships
                eventPackage["msnfp_val_sponsorships"] = new Money(valAmount); // $ sponsorships

                //updating event package record
                service.Update(eventPackage);
            }
        }
        #endregion


        #region Updating Event Sponsorship Totals

        private void UpdateEventSponsorshipTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventSponsorshipTotals---------");

            if (queriedEntityRecord.Contains("msnfp_eventsponsorshipid"))
            {
                decimal valAmount = 0;
                Entity eventSponsorship = service.Retrieve("msnfp_eventsponsorship", ((EntityReference)queriedEntityRecord["msnfp_eventsponsorshipid"]).Id, new ColumnSet(new string[] { "msnfp_eventsponsorshipid", "msnfp_amount", "msnfp_quantity" }));

                List<Entity> sponsorshipList = (from a in orgSvcContext.CreateQuery("msnfp_sponsorship")
                                                where ((EntityReference)a["msnfp_eventsponsorshipid"]).Id == eventSponsorship.Id
                                                && ((OptionSetValue)a["statuscode"]).Value != 844060001 //cancelled
                                                select a).ToList();

                foreach (Entity item in sponsorshipList)
                {
                    if (item.Contains("msnfp_amount") && item["msnfp_amount"] != null)
                        valAmount += ((Money)item["msnfp_amount"]).Value;
                }

                eventSponsorship["msnfp_sum_sold"] = sponsorshipList.Count(); // sponsorships sold
                eventSponsorship["msnfp_val_sold"] = new Money(valAmount); //$ sponsorships sold

                if (eventSponsorship.Contains("msnfp_quantity") && eventSponsorship["msnfp_quantity"] != null)
                    eventSponsorship["msnfp_sum_available"] = (int)eventSponsorship["msnfp_quantity"] - sponsorshipList.Count(); // sponsorships available

                // updating event sponsorship record
                service.Update(eventSponsorship);
            }
        }
        #endregion
    }
}

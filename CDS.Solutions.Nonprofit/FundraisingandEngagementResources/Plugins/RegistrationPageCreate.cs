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
using Plugins.AzureModels;
using Plugins.PaymentProcesses;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class RegistrationPageCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public RegistrationPageCreate(string unsecure, string secure)
            : base(typeof(RegistrationPageCreate))
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
            localContext.TracingService.Trace("---------Triggered RegistrationPageCreate.cs---------");

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

            // Get the Configuration Record (Either from the User or from the Default Configuration Record
            configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering RegistrationPageCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_registration", targetIncomingRecord.Id, GetColumnSet());
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
                        localContext.TracingService.Trace("Target record not found. Exiting plugin.");
                    }
                }

                // Delete if the message is delete. This is the primary delete entrypoint:
                if (messageName == "Delete")
                {
                    queriedEntityRecord = service.Retrieve("msnfp_registration", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting RegistrationPageCreate.cs---------");
            }
        }

        private ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_registrationid","msnfp_firstname","msnfp_lastname","msnfp_email","msnfp_telephone","msnfp_address_line1","msnfp_address_line2","msnfp_address_city","msnfp_address_province","msnfp_address_postalcode","msnfp_address_country","msnfp_tableid","msnfp_team","msnfp_customerid","msnfp_date","msnfp_eventid","msnfp_eventpackageid","msnfp_ticketid","msnfp_groupnotes","msnfp_eventticketid","msnfp_identifier","msnfp_emailaddress1","msnfp_telephone1","msnfp_billing_city","msnfp_billing_country","msnfp_billing_line1","msnfp_billing_line2","msnfp_billing_line3","msnfp_billing_postalcode","msnfp_billing_stateorprovince","statecode","statuscode","createdon");
        }

        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Registration"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_registrationid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_Registration jsonDataObj = new MSNFP_Registration();

                jsonDataObj.RegistrationId = (Guid)queriedEntityRecord["msnfp_registrationid"];


                if (queriedEntityRecord.Contains("msnfp_firstname") && queriedEntityRecord["msnfp_firstname"] != null)
                {
                    jsonDataObj.FirstName = (string)queriedEntityRecord["msnfp_firstname"];
                    localContext.TracingService.Trace("Got msnfp_firstname.");
                }
                else
                {
                    jsonDataObj.FirstName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_firstname.");
                }

                if (queriedEntityRecord.Contains("msnfp_lastname") && queriedEntityRecord["msnfp_lastname"] != null)
                {
                    jsonDataObj.LastName = (string)queriedEntityRecord["msnfp_lastname"];
                    localContext.TracingService.Trace("Got msnfp_lastname.");
                }
                else
                {
                    jsonDataObj.LastName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastname.");
                }

                if (queriedEntityRecord.Contains("msnfp_email") && queriedEntityRecord["msnfp_email"] != null)
                {
                    jsonDataObj.Email = (string)queriedEntityRecord["msnfp_email"];
                    localContext.TracingService.Trace("Got msnfp_email.");
                }
                else
                {
                    jsonDataObj.Email = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_email.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone") && queriedEntityRecord["msnfp_telephone"] != null)
                {
                    jsonDataObj.Telephone = (string)queriedEntityRecord["msnfp_telephone"];
                    localContext.TracingService.Trace("Got msnfp_telephone.");
                }
                else
                {
                    jsonDataObj.Telephone = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone.");
                }

                if (queriedEntityRecord.Contains("msnfp_address_line1") && queriedEntityRecord["msnfp_address_line1"] != null)
                {
                    jsonDataObj.Address_Line1 = (string)queriedEntityRecord["msnfp_address_line1"];
                    localContext.TracingService.Trace("Got msnfp_address_line1.");
                }
                else
                {
                    jsonDataObj.Address_Line1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_address_line1.");
                }

                if (queriedEntityRecord.Contains("msnfp_address_line2") && queriedEntityRecord["msnfp_address_line2"] != null)
                {
                    jsonDataObj.Address_Line2 = (string)queriedEntityRecord["msnfp_address_line2"];
                    localContext.TracingService.Trace("Got msnfp_address_line2.");
                }
                else
                {
                    jsonDataObj.Address_Line2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_address_line2.");
                }

                if (queriedEntityRecord.Contains("msnfp_address_city") && queriedEntityRecord["msnfp_address_city"] != null)
                {
                    jsonDataObj.Address_City = (string)queriedEntityRecord["msnfp_address_city"];
                    localContext.TracingService.Trace("Got msnfp_address_city.");
                }
                else
                {
                    jsonDataObj.Address_City = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_address_city.");
                }

                if (queriedEntityRecord.Contains("msnfp_address_province") && queriedEntityRecord["msnfp_address_province"] != null)
                {
                    jsonDataObj.Address_Province = (string)queriedEntityRecord["msnfp_address_province"];
                    localContext.TracingService.Trace("Got msnfp_address_province.");
                }
                else
                {
                    jsonDataObj.Address_Province = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_address_province.");
                }

                if (queriedEntityRecord.Contains("msnfp_address_postalcode") && queriedEntityRecord["msnfp_address_postalcode"] != null)
                {
                    jsonDataObj.Address_PostalCode = (string)queriedEntityRecord["msnfp_address_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_address_postalcode.");
                }
                else
                {
                    jsonDataObj.Address_PostalCode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_address_postalcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_address_country") && queriedEntityRecord["msnfp_address_country"] != null)
                {
                    jsonDataObj.Address_Country = (string)queriedEntityRecord["msnfp_address_country"];
                    localContext.TracingService.Trace("Got msnfp_address_country.");
                }
                else
                {
                    jsonDataObj.Address_Country = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_address_country.");
                }

                if (queriedEntityRecord.Contains("msnfp_tableid") && queriedEntityRecord["msnfp_tableid"] != null)
                {
                    jsonDataObj.TableId = ((EntityReference)queriedEntityRecord["msnfp_tableid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_tableid");
                }
                else
                {
                    jsonDataObj.TableId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tableid.");
                }

                if (queriedEntityRecord.Contains("msnfp_team") && queriedEntityRecord["msnfp_team"] != null)
                {
                    jsonDataObj.Team = (string)queriedEntityRecord["msnfp_team"];
                    localContext.TracingService.Trace("Got msnfp_team.");
                }
                else
                {
                    jsonDataObj.Team = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_team.");
                }

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

                    localContext.TracingService.Trace("Got msnfp_customerid");
                }
                else
                {
                    jsonDataObj.CustomerId = null;
                    jsonDataObj.CustomerIdType = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_customerid.");
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

                if (queriedEntityRecord.Contains("msnfp_eventid") && queriedEntityRecord["msnfp_eventid"] != null)
                {
                    jsonDataObj.EventId = ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventid .");
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

                if (queriedEntityRecord.Contains("msnfp_ticketid") && queriedEntityRecord["msnfp_ticketid"] != null)
                {
                    jsonDataObj.TicketId = ((EntityReference)queriedEntityRecord["msnfp_ticketid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_ticketid.");
                }
                else
                {
                    jsonDataObj.TicketId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ticketid.");
                }

                if (queriedEntityRecord.Contains("msnfp_groupnotes") && queriedEntityRecord["msnfp_groupnotes"] != null)
                {
                    jsonDataObj.GroupNotes = (string)queriedEntityRecord["msnfp_groupnotes"];
                    localContext.TracingService.Trace("Got msnfp_groupnotes.");
                }
                else
                {
                    jsonDataObj.GroupNotes = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_groupnotes.");
                }

                if (queriedEntityRecord.Contains("msnfp_eventticketid") && queriedEntityRecord["msnfp_eventticketid"] != null)
                {
                    jsonDataObj.EventTicketId = ((EntityReference)queriedEntityRecord["msnfp_eventticketid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventticketid.");
                }
                else
                {
                    jsonDataObj.EventTicketId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventticketid.");
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

                if (queriedEntityRecord.Contains("msnfp_emailaddress1") && queriedEntityRecord["msnfp_emailaddress1"] != null)
                {
                    jsonDataObj.msnfp_Emailaddress1 = (string)queriedEntityRecord["msnfp_emailaddress1"];
                    localContext.TracingService.Trace("Got msnfp_emailaddress1.");
                }
                else
                {
                    jsonDataObj.msnfp_Emailaddress1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_emailaddress1.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone1") && queriedEntityRecord["msnfp_telephone1"] != null)
                {
                    jsonDataObj.msnfp_Telephone1 = (string)queriedEntityRecord["msnfp_telephone1"];
                    localContext.TracingService.Trace("Got msnfp_telephone1.");
                }
                else
                {
                    jsonDataObj.msnfp_Telephone1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone1.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_city") && queriedEntityRecord["msnfp_billing_city"] != null)
                {
                    jsonDataObj.msnfp_Billing_City = (string)queriedEntityRecord["msnfp_billing_city"];
                    localContext.TracingService.Trace("Got msnfp_billing_city.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_City = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_city.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_country") && queriedEntityRecord["msnfp_billing_country"] != null)
                {
                    jsonDataObj.msnfp_Billing_Country = (string)queriedEntityRecord["msnfp_billing_country"];
                    localContext.TracingService.Trace("Got msnfp_billing_country.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_Country = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_country.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_line1") && queriedEntityRecord["msnfp_billing_line1"] != null)
                {
                    jsonDataObj.msnfp_Billing_Line1 = (string)queriedEntityRecord["msnfp_billing_line1"];
                    localContext.TracingService.Trace("Got msnfp_billing_line1.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_Line1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line1.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_line2") && queriedEntityRecord["msnfp_billing_line2"] != null)
                {
                    jsonDataObj.msnfp_Billing_Line2 = (string)queriedEntityRecord["msnfp_billing_line2"];
                    localContext.TracingService.Trace("Got msnfp_billing_line2.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_Line2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line2.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_line3") && queriedEntityRecord["msnfp_billing_line3"] != null)
                {
                    jsonDataObj.msnfp_Billing_Line3 = (string)queriedEntityRecord["msnfp_billing_line3"];
                    localContext.TracingService.Trace("Got msnfp_billing_line3.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_Line3 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line3.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_postalcode") && queriedEntityRecord["msnfp_billing_postalcode"] != null)
                {
                    jsonDataObj.msnfp_Billing_Postalcode = (string)queriedEntityRecord["msnfp_billing_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_billing_postalcode.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_Postalcode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_postalcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_stateorprovince") && queriedEntityRecord["msnfp_billing_stateorprovince"] != null)
                {
                    jsonDataObj.msnfp_Billing_StateorProvince = (string)queriedEntityRecord["msnfp_billing_stateorprovince"];
                    localContext.TracingService.Trace("Got msnfp_billing_stateorprovince.");
                }
                else
                {
                    jsonDataObj.msnfp_Billing_StateorProvince = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_stateorprovince.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_Registration));
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


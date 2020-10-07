/*************************************************************************
* © Microsoft. All rights reserved.
*/

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Plugins.AzureModels;
using Plugins.PaymentProcesses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Plugins
{
    public class ContactCreate : PluginBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContactCreate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public ContactCreate(string unsecure, string secure)
            : base(typeof(ContactCreate))
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
            // ********************************* PLUGIN's BEGINNING *********************************************
            if (localContext == null)
                throw new ArgumentNullException("localContext");

            localContext.TracingService.Trace("---------Triggered ContactCreate.cs---------");

            // ********************************* PLUGIN'S PREPARATION *******************************************
            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;



            // ********************************* PLUGIN'S VALIDATION ********************************************
            if (context.Depth > 1 && !CheckExecutionPipeLine(context))
            {
                localContext.TracingService.Trace("Context.depth > 1 => Exiting Plugin. context.Depth: " + context.Depth);
                localContext.TracingService.Trace($"Parent context: {context.ParentContext.PrimaryEntityName}");
                return;
            }

            string messageName = context.MessageName;
            if (messageName != "Create" && messageName != "Update" && messageName != "Delete")
                throw new InvalidPluginExecutionException("The plugin was triggered NOT in CREATE NOR UPDATE NOR DELETE mode. Exiting plugin.");

            // Get the Configuration Record (Either from the User or from the Default Configuration Record
            Entity configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            Entity targetRecord = null;
            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    targetRecord = (Entity)context.InputParameters["Target"];
                    if (targetRecord == null)
                        throw new InvalidPluginExecutionException("'Target' is null. Exiting plugin.");
                    else
                        localContext.TracingService.Trace("Target entity name: " + targetRecord.LogicalName);
                    if (targetRecord.LogicalName.ToLower() != "contact")
                        throw new InvalidPluginExecutionException("The target entity is NOT CONTACT. Exiting plugin.");
                }
                // If we don't do this, we won't be able to delete an entity as the taget is NOT an entity when deleting, it is an entityreference:
                else if (messageName == "Delete")
                {
                    Entity deletedEntityRecord = service.Retrieve("contact", ((EntityReference)context.InputParameters["Target"]).Id, GetColumns());
                    if (configurationRecord.Contains("msnfp_apipadlocktoken") && configurationRecord["msnfp_apipadlocktoken"] != null)
                    {
                        AddOrUpdateThisRecordWithAzure(deletedEntityRecord, configurationRecord, localContext, service, context);
                    }
                }
                else
                    throw new InvalidPluginExecutionException("The Target is NOT an Entity. Exiting plugin.");
            }
            else
                throw new ArgumentNullException("Target");


            // ********************************* PLUGIN'S IMPLEMENTATION *****************************************
            if (messageName != "Delete")
            {
                Entity contactEntity = service.Retrieve(targetRecord.LogicalName, targetRecord.Id, GetColumns());

                // If the household id is set and the company id is set on create and they are the same value, remove the company id:
                if (messageName == "Create")
                {
                    if (contactEntity.GetAttributeValue<EntityReference>("msnfp_householdid") != null && contactEntity.GetAttributeValue<EntityReference>("parentcustomerid") != null)
                    {
                        localContext.TracingService.Trace("Contact has a msnfp_householdid and parentcustomerid");
                        localContext.TracingService.Trace("msnfp_householdid = " + ((EntityReference)contactEntity["msnfp_householdid"]).Id);
                        localContext.TracingService.Trace("parentcustomerid = " + ((EntityReference)contactEntity["parentcustomerid"]).Id);
                        if (((EntityReference)contactEntity["msnfp_householdid"]).Id.Equals(((EntityReference)contactEntity["parentcustomerid"]).Id))
                        {
                            localContext.TracingService.Trace("They are the same, so remove company.");
                            contactEntity["parentcustomerid"] = null;
                            UpdateEntity(service, contactEntity);
                        }
                    }
                }

                SetUpHousehold(service, configurationRecord.GetAttributeValue<string>("msnfp_householdsequence"), ref contactEntity, targetRecord, context, localContext.TracingService);
                // All logics should happen before this step

                // Synchronize the record to Azure
                if (configurationRecord.Contains("msnfp_apipadlocktoken") && configurationRecord["msnfp_apipadlocktoken"] != null)
                    AddOrUpdateThisRecordWithAzure(contactEntity, configurationRecord, localContext, service, context);
                else
                    localContext.TracingService.Trace("No Padlock Token found to synchronize the record to Azure. AddOrUpdateThisRecordWithAzure() failed.");
            }


            // ********************************* PLUGIN's END *********************************************
            localContext.TracingService.Trace("---------Exitting ContactCreate.cs---------");
        }

        // For process initiated due to donation import contact creation
        // check parent's parent context since this is PostConactUpdate
        private bool CheckExecutionPipeLine(IPluginExecutionContext context)
        {
            // Depth will be 2
            // when donation import creates or updates contact (household related)
            // when account merge triggers contact update
            return (context.ParentContext != null
                    &&
                    (
                            (context.ParentContext.PrimaryEntityName == "account" || context.ParentContext.PrimaryEntityName == "contact")
                            ||
                            (context.ParentContext != null && context.ParentContext.ParentContext != null && context.ParentContext.ParentContext.PrimaryEntityName == "msnfp_donationimport")
                    )
                    );
        }

        private bool CheckExecutionPipelineForDonationImport(IPluginExecutionContext context)
        {
            // Depth will be 2
            // when donation import creates or updates contact (household related)
            // when account merge triggers contact update
            return (context.ParentContext != null
                    && context.ParentContext != null && context.ParentContext.ParentContext != null && context.ParentContext.ParentContext.PrimaryEntityName == "msnfp_donationimport"
                );

        }
        private Entity GetDonationImportFromExecutionPipeLine(IPluginExecutionContext context, IOrganizationService service)
        {
            Entity donationImport = null;
            if (CheckExecutionPipelineForDonationImport(context))
            {
                donationImport = context.ParentContext.ParentContext.SharedVariables.Where(s => s.Key == "DonationImport").Count() > 0 ?
                                (Entity)context.ParentContext.ParentContext.SharedVariables.FirstOrDefault(s => s.Key == "DonationImport").Value : null;

                if (donationImport == null)
                {
                    donationImport = service.Retrieve("msnfp_donationimport", context.ParentContext.ParentContext.PrimaryEntityId, new ColumnSet("msnfp_createhousehold", "msnfp_donationimportid"));
                }
            }

            return donationImport;
        }

        private ColumnSet GetColumns()
        {
            ColumnSet colList = new ColumnSet(new String[]
            {
                "contactid", "address1_addressid", "address1_addresstypecode", "address1_city", "address1_country",
                "address1_county", "address1_latitude", "address1_line1", "address1_line2", "address1_line3",
                "address1_longitude", "address1_name", "address1_postalcode", "address1_postofficebox",
                "address1_stateorprovince", "address2_addressid", "address2_addresstypecode", "address2_city",
                "address2_country", "address2_county", "address2_latitude", "address2_line1", "address2_line2",
                "address2_line3", "address2_longitude", "address2_name", "address2_postalcode",
                "address2_postofficebox", "address2_stateorprovince", "address3_addressid", "address3_addresstypecode",
                "address3_city", "address3_country", "address3_county", "address3_latitude", "address3_line1",
                "address3_line2", "address3_line3", "address3_longitude", "address3_name", "address3_postalcode",
                "address3_postofficebox", "address3_stateorprovince", "msnfp_birthday", "donotbulkemail",
                "donotbulkpostalmail", "donotemail", "donotfax", "donotphone", "donotpostalmail", "emailaddress1",
                "emailaddress2", "emailaddress3", "firstname", "fullname", "gendercode", "jobtitle", "lastname",
                "masterid", "owningbusinessunit", "msnfp_age", "msnfp_anonymity", "msnfp_count_lifetimetransactions",
                "msnfp_givinglevelid", "msnfp_lasteventpackagedate", "msnfp_lasteventpackageid",
                "msnfp_lasttransactiondate", "msnfp_lasttransactionid", "msnfp_preferredlanguagecode",
                "msnfp_primarymembershipid", "msnfp_receiptpreferencecode", "msnfp_sum_lifetimetransactions",
                "msnfp_telephone1typecode", "msnfp_telephone2typecode", "msnfp_telephone3typecode",
                "msnfp_upcomingbirthday", "msnfp_vip", "merged", "middlename", "mobilephone", "parentcustomerid",
                "salutation", "suffix", "telephone1", "telephone2", "telephone3", "transactioncurrencyid", "statecode",
                "statuscode", "createdon", "msnfp_householdrelationship", "msnfp_householdid", "msnfp_deceased",
                "msnfp_year0_giving", "msnfp_year1_giving", "msnfp_year2_giving","msnfp_year3_giving", "msnfp_year4_giving", "msnfp_lifetimegivingsum"
            });
            return colList;
        }


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Contact"; // Used for API calls

            //string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            string apiUrl = configurationRecord.GetAttributeValue<string>("msnfp_azure_webapiurl");

            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {



                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["contactid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                Contact jsonDataObj = new Contact();

                jsonDataObj.ContactId = (Guid)queriedEntityRecord["contactid"];

                // Now we get all the fields for this entity and save them to a JSON object.

                if (queriedEntityRecord.Contains("address1_addressid") && queriedEntityRecord["address1_addressid"] != null)
                {
                    jsonDataObj.Address1_AddressId = (Guid)queriedEntityRecord["address1_addressid"];
                    localContext.TracingService.Trace("Got address1_addressid.");
                }
                else
                {
                    jsonDataObj.Address1_AddressId = null;
                    localContext.TracingService.Trace("Did NOT find address1_addressid.");
                }

                if (queriedEntityRecord.Contains("address1_addresstypecode") && queriedEntityRecord["address1_addresstypecode"] != null)
                {
                    jsonDataObj.Address1_AddressTypeCode = ((OptionSetValue)queriedEntityRecord["address1_addresstypecode"]).Value;
                    localContext.TracingService.Trace("Got address1_addresstypecode.");
                }
                else
                {
                    jsonDataObj.Address1_AddressTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find address1_addresstypecode.");
                }

                if (queriedEntityRecord.Contains("address1_city") && queriedEntityRecord["address1_city"] != null)
                {
                    jsonDataObj.Address1_City = (string)queriedEntityRecord["address1_city"];
                    localContext.TracingService.Trace("Got address1_city.");
                }
                else
                {
                    jsonDataObj.Address1_City = null;
                    localContext.TracingService.Trace("Did NOT find address1_city.");
                }

                if (queriedEntityRecord.Contains("address1_country") && queriedEntityRecord["address1_country"] != null)
                {
                    jsonDataObj.Address1_Country = (string)queriedEntityRecord["address1_country"];
                    localContext.TracingService.Trace("Got address1_country.");
                }
                else
                {
                    jsonDataObj.Address1_Country = null;
                    localContext.TracingService.Trace("Did NOT find address1_country.");
                }

                if (queriedEntityRecord.Contains("address1_county") && queriedEntityRecord["address1_county"] != null)
                {
                    jsonDataObj.Address1_County = (string)queriedEntityRecord["address1_county"];
                    localContext.TracingService.Trace("Got address1_county.");
                }
                else
                {
                    jsonDataObj.Address1_County = null;
                    localContext.TracingService.Trace("Did NOT find address1_county.");
                }

                if (queriedEntityRecord.Contains("address1_latitude") && queriedEntityRecord["address1_latitude"] != null)
                {
                    jsonDataObj.Address1_Latitude = (float)queriedEntityRecord["address1_latitude"];
                    localContext.TracingService.Trace("Got address1_latitude.");
                }
                else
                {
                    jsonDataObj.Address1_Latitude = null;
                    localContext.TracingService.Trace("Did NOT find address1_latitude.");
                }

                if (queriedEntityRecord.Contains("address1_line1") && queriedEntityRecord["address1_line1"] != null)
                {
                    jsonDataObj.Address1_Line1 = (string)queriedEntityRecord["address1_line1"];
                    localContext.TracingService.Trace("Got address1_line1.");
                }
                else
                {
                    jsonDataObj.Address1_Line1 = null;
                    localContext.TracingService.Trace("Did NOT find address1_line1.");
                }

                if (queriedEntityRecord.Contains("address1_line2") && queriedEntityRecord["address1_line2"] != null)
                {
                    jsonDataObj.Address1_Line2 = (string)queriedEntityRecord["address1_line2"];
                    localContext.TracingService.Trace("Got address1_line2.");
                }
                else
                {
                    jsonDataObj.Address1_Line2 = null;
                    localContext.TracingService.Trace("Did NOT find address1_line2.");
                }

                if (queriedEntityRecord.Contains("address1_line3") && queriedEntityRecord["address1_line3"] != null)
                {
                    jsonDataObj.Address1_Line3 = (string)queriedEntityRecord["address1_line3"];
                    localContext.TracingService.Trace("Got address1_line3.");
                }
                else
                {
                    jsonDataObj.Address1_Line3 = null;
                    localContext.TracingService.Trace("Did NOT find address1_line3.");
                }

                if (queriedEntityRecord.Contains("address1_longitude") && queriedEntityRecord["address1_longitude"] != null)
                {
                    jsonDataObj.Address1_Longitude = (float)queriedEntityRecord["address1_longitude"];
                    localContext.TracingService.Trace("Got address1_longitude.");
                }
                else
                {
                    jsonDataObj.Address1_Longitude = null;
                    localContext.TracingService.Trace("Did NOT find address1_longitude.");
                }

                if (queriedEntityRecord.Contains("address1_name") && queriedEntityRecord["address1_name"] != null)
                {
                    jsonDataObj.Address1_Name = (string)queriedEntityRecord["address1_name"];
                    localContext.TracingService.Trace("Got address1_name.");
                }
                else
                {
                    jsonDataObj.Address1_Name = null;
                    localContext.TracingService.Trace("Did NOT find address1_name.");
                }

                if (queriedEntityRecord.Contains("address1_postalcode") && queriedEntityRecord["address1_postalcode"] != null)
                {
                    jsonDataObj.Address1_PostalCode = (string)queriedEntityRecord["address1_postalcode"];
                    localContext.TracingService.Trace("Got address1_postalcode.");
                }
                else
                {
                    jsonDataObj.Address1_PostalCode = null;
                    localContext.TracingService.Trace("Did NOT find address1_postalcode.");
                }

                if (queriedEntityRecord.Contains("address1_postofficebox") && queriedEntityRecord["address1_postofficebox"] != null)
                {
                    jsonDataObj.Address1_PostOfficeBox = (string)queriedEntityRecord["address1_postofficebox"];
                    localContext.TracingService.Trace("Got address1_postofficebox.");
                }
                else
                {
                    jsonDataObj.Address1_PostOfficeBox = null;
                    localContext.TracingService.Trace("Did NOT find address1_postofficebox.");
                }

                if (queriedEntityRecord.Contains("address1_stateorprovince") && queriedEntityRecord["address1_stateorprovince"] != null)
                {
                    jsonDataObj.Address1_StateOrProvince = (string)queriedEntityRecord["address1_stateorprovince"];
                    localContext.TracingService.Trace("Got address1_stateorprovince.");
                }
                else
                {
                    jsonDataObj.Address1_StateOrProvince = null;
                    localContext.TracingService.Trace("Did NOT find address1_stateorprovince.");
                }




                if (queriedEntityRecord.Contains("address2_addressid") && queriedEntityRecord["address2_addressid"] != null)
                {
                    jsonDataObj.Address2_AddressId = (Guid)queriedEntityRecord["address2_addressid"];
                    localContext.TracingService.Trace("Got address2_addressid.");
                }
                else
                {
                    jsonDataObj.Address2_AddressId = null;
                    localContext.TracingService.Trace("Did NOT find address1_addressid.");
                }

                if (queriedEntityRecord.Contains("address2_addresstypecode") && queriedEntityRecord["address2_addresstypecode"] != null)
                {
                    jsonDataObj.Address2_AddressTypeCode = ((OptionSetValue)queriedEntityRecord["address2_addresstypecode"]).Value;
                    localContext.TracingService.Trace("Got address2_addresstypecode.");
                }
                else
                {
                    jsonDataObj.Address2_AddressTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find address2_addresstypecode.");
                }

                if (queriedEntityRecord.Contains("address2_city") && queriedEntityRecord["address2_city"] != null)
                {
                    jsonDataObj.Address2_City = (string)queriedEntityRecord["address2_city"];
                    localContext.TracingService.Trace("Got address2_city.");
                }
                else
                {
                    jsonDataObj.Address2_City = null;
                    localContext.TracingService.Trace("Did NOT find address2_city.");
                }

                if (queriedEntityRecord.Contains("address2_country") && queriedEntityRecord["address2_country"] != null)
                {
                    jsonDataObj.Address2_Country = (string)queriedEntityRecord["address2_country"];
                    localContext.TracingService.Trace("Got address2_country.");
                }
                else
                {
                    jsonDataObj.Address2_Country = null;
                    localContext.TracingService.Trace("Did NOT find address2_country.");
                }

                if (queriedEntityRecord.Contains("address2_county") && queriedEntityRecord["address2_county"] != null)
                {
                    jsonDataObj.Address2_County = (string)queriedEntityRecord["address2_county"];
                    localContext.TracingService.Trace("Got address2_county.");
                }
                else
                {
                    jsonDataObj.Address2_County = null;
                    localContext.TracingService.Trace("Did NOT find address2_county.");
                }

                if (queriedEntityRecord.Contains("address2_latitude") && queriedEntityRecord["address2_latitude"] != null)
                {
                    jsonDataObj.Address2_Latitude = (float)queriedEntityRecord["address2_latitude"];
                    localContext.TracingService.Trace("Got address2_latitude.");
                }
                else
                {
                    jsonDataObj.Address2_Latitude = null;
                    localContext.TracingService.Trace("Did NOT find address2_latitude.");
                }

                if (queriedEntityRecord.Contains("address2_line1") && queriedEntityRecord["address2_line1"] != null)
                {
                    jsonDataObj.Address2_Line1 = (string)queriedEntityRecord["address2_line1"];
                    localContext.TracingService.Trace("Got address2_line1.");
                }
                else
                {
                    jsonDataObj.Address2_Line1 = null;
                    localContext.TracingService.Trace("Did NOT find address2_line1.");
                }

                if (queriedEntityRecord.Contains("address2_line2") && queriedEntityRecord["address2_line2"] != null)
                {
                    jsonDataObj.Address2_Line2 = (string)queriedEntityRecord["address2_line2"];
                    localContext.TracingService.Trace("Got address2_line2.");
                }
                else
                {
                    jsonDataObj.Address2_Line2 = null;
                    localContext.TracingService.Trace("Did NOT find address2_line2.");
                }

                if (queriedEntityRecord.Contains("address2_line3") && queriedEntityRecord["address2_line3"] != null)
                {
                    jsonDataObj.Address2_Line3 = (string)queriedEntityRecord["address2_line3"];
                    localContext.TracingService.Trace("Got address2_line3.");
                }
                else
                {
                    jsonDataObj.Address2_Line3 = null;
                    localContext.TracingService.Trace("Did NOT find address2_line3.");
                }

                if (queriedEntityRecord.Contains("address2_longitude") && queriedEntityRecord["address2_longitude"] != null)
                {
                    jsonDataObj.Address2_Longitude = (float)queriedEntityRecord["address2_longitude"];
                    localContext.TracingService.Trace("Got address2_longitude.");
                }
                else
                {
                    jsonDataObj.Address2_Longitude = null;
                    localContext.TracingService.Trace("Did NOT find address2_longitude.");
                }

                if (queriedEntityRecord.Contains("address2_name") && queriedEntityRecord["address2_name"] != null)
                {
                    jsonDataObj.Address2_Name = (string)queriedEntityRecord["address2_name"];
                    localContext.TracingService.Trace("Got address2_name.");
                }
                else
                {
                    jsonDataObj.Address2_Name = null;
                    localContext.TracingService.Trace("Did NOT find address2_name.");
                }

                if (queriedEntityRecord.Contains("address2_postalcode") && queriedEntityRecord["address2_postalcode"] != null)
                {
                    jsonDataObj.Address2_PostalCode = (string)queriedEntityRecord["address2_postalcode"];
                    localContext.TracingService.Trace("Got address2_postalcode.");
                }
                else
                {
                    jsonDataObj.Address2_PostalCode = null;
                    localContext.TracingService.Trace("Did NOT find address2_postalcode.");
                }

                if (queriedEntityRecord.Contains("address2_postofficebox") && queriedEntityRecord["address2_postofficebox"] != null)
                {
                    jsonDataObj.Address2_PostOfficeBox = (string)queriedEntityRecord["address2_postofficebox"];
                    localContext.TracingService.Trace("Got address2_postofficebox.");
                }
                else
                {
                    jsonDataObj.Address2_PostOfficeBox = null;
                    localContext.TracingService.Trace("Did NOT find address2_postofficebox.");
                }

                if (queriedEntityRecord.Contains("address2_stateorprovince") && queriedEntityRecord["address2_stateorprovince"] != null)
                {
                    jsonDataObj.Address2_StateOrProvince = (string)queriedEntityRecord["address2_stateorprovince"];
                    localContext.TracingService.Trace("Got address2_stateorprovince.");
                }
                else
                {
                    jsonDataObj.Address2_StateOrProvince = null;
                    localContext.TracingService.Trace("Did NOT find address2_stateorprovince.");
                }



                if (queriedEntityRecord.Contains("address3_addressid") && queriedEntityRecord["address3_addressid"] != null)
                {
                    jsonDataObj.Address3_AddressId = (Guid)queriedEntityRecord["address3_addressid"];
                    localContext.TracingService.Trace("Got address3_addressid.");
                }
                else
                {
                    jsonDataObj.Address3_AddressId = null;
                    localContext.TracingService.Trace("Did NOT find address3_addressid.");
                }

                if (queriedEntityRecord.Contains("address3_addresstypecode") && queriedEntityRecord["address3_addresstypecode"] != null)
                {
                    jsonDataObj.Address3_AddressTypeCode = ((OptionSetValue)queriedEntityRecord["address3_addresstypecode"]).Value;
                    localContext.TracingService.Trace("Got address3_addresstypecode.");
                }
                else
                {
                    jsonDataObj.Address3_AddressTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find address3_addresstypecode.");
                }

                if (queriedEntityRecord.Contains("address3_city") && queriedEntityRecord["address3_city"] != null)
                {
                    jsonDataObj.Address3_City = (string)queriedEntityRecord["address3_city"];
                    localContext.TracingService.Trace("Got address3_city.");
                }
                else
                {
                    jsonDataObj.Address3_City = null;
                    localContext.TracingService.Trace("Did NOT find address3_city.");
                }

                if (queriedEntityRecord.Contains("address3_country") && queriedEntityRecord["address3_country"] != null)
                {
                    jsonDataObj.Address3_Country = (string)queriedEntityRecord["address3_country"];
                    localContext.TracingService.Trace("Got address3_country.");
                }
                else
                {
                    jsonDataObj.Address3_Country = null;
                    localContext.TracingService.Trace("Did NOT find address3_country.");
                }

                if (queriedEntityRecord.Contains("address3_county") && queriedEntityRecord["address3_county"] != null)
                {
                    jsonDataObj.Address3_County = (string)queriedEntityRecord["address3_county"];
                    localContext.TracingService.Trace("Got address3_county.");
                }
                else
                {
                    jsonDataObj.Address3_County = null;
                    localContext.TracingService.Trace("Did NOT find address3_county.");
                }

                if (queriedEntityRecord.Contains("address3_latitude") && queriedEntityRecord["address3_latitude"] != null)
                {
                    jsonDataObj.Address3_Latitude = (float)queriedEntityRecord["address3_latitude"];
                    localContext.TracingService.Trace("Got address3_latitude.");
                }
                else
                {
                    jsonDataObj.Address3_Latitude = null;
                    localContext.TracingService.Trace("Did NOT find address3_latitude.");
                }

                if (queriedEntityRecord.Contains("address3_line1") && queriedEntityRecord["address3_line1"] != null)
                {
                    jsonDataObj.Address3_Line1 = (string)queriedEntityRecord["address3_line1"];
                    localContext.TracingService.Trace("Got address3_line1.");
                }
                else
                {
                    jsonDataObj.Address3_Line1 = null;
                    localContext.TracingService.Trace("Did NOT find address3_line1.");
                }

                if (queriedEntityRecord.Contains("address3_line2") && queriedEntityRecord["address3_line2"] != null)
                {
                    jsonDataObj.Address3_Line2 = (string)queriedEntityRecord["address3_line2"];
                    localContext.TracingService.Trace("Got address3_line2.");
                }
                else
                {
                    jsonDataObj.Address3_Line2 = null;
                    localContext.TracingService.Trace("Did NOT find address3_line2.");
                }

                if (queriedEntityRecord.Contains("address3_line3") && queriedEntityRecord["address3_line3"] != null)
                {
                    jsonDataObj.Address3_Line3 = (string)queriedEntityRecord["address3_line3"];
                    localContext.TracingService.Trace("Got address3_line3.");
                }
                else
                {
                    jsonDataObj.Address3_Line3 = null;
                    localContext.TracingService.Trace("Did NOT find address3_line3.");
                }

                if (queriedEntityRecord.Contains("address3_longitude") && queriedEntityRecord["address3_longitude"] != null)
                {
                    jsonDataObj.Address3_Longitude = (float)queriedEntityRecord["address3_longitude"];
                    localContext.TracingService.Trace("Got address3_longitude.");
                }
                else
                {
                    jsonDataObj.Address3_Longitude = null;
                    localContext.TracingService.Trace("Did NOT find address3_longitude.");
                }

                if (queriedEntityRecord.Contains("address3_name") && queriedEntityRecord["address3_name"] != null)
                {
                    jsonDataObj.Address3_Name = (string)queriedEntityRecord["address3_name"];
                    localContext.TracingService.Trace("Got address3_name.");
                }
                else
                {
                    jsonDataObj.Address3_Name = null;
                    localContext.TracingService.Trace("Did NOT find address3_name.");
                }

                if (queriedEntityRecord.Contains("address3_postalcode") && queriedEntityRecord["address3_postalcode"] != null)
                {
                    jsonDataObj.Address3_PostalCode = (string)queriedEntityRecord["address3_postalcode"];
                    localContext.TracingService.Trace("Got address3_postalcode.");
                }
                else
                {
                    jsonDataObj.Address3_PostalCode = null;
                    localContext.TracingService.Trace("Did NOT find address3_postalcode.");
                }

                if (queriedEntityRecord.Contains("address3_postofficebox") && queriedEntityRecord["address3_postofficebox"] != null)
                {
                    jsonDataObj.Address3_PostOfficeBox = (string)queriedEntityRecord["address3_postofficebox"];
                    localContext.TracingService.Trace("Got address3_postofficebox.");
                }
                else
                {
                    jsonDataObj.Address3_PostOfficeBox = null;
                    localContext.TracingService.Trace("Did NOT find address3_postofficebox.");
                }

                if (queriedEntityRecord.Contains("address3_stateorprovince") && queriedEntityRecord["address3_stateorprovince"] != null)
                {
                    jsonDataObj.Address3_StateOrProvince = (string)queriedEntityRecord["address3_stateorprovince"];
                    localContext.TracingService.Trace("Got address3_stateorprovince.");
                }
                else
                {
                    jsonDataObj.Address3_StateOrProvince = null;
                    localContext.TracingService.Trace("Did NOT find address3_stateorprovince.");
                }


                if (queriedEntityRecord.Contains("msnfp_birthday") && queriedEntityRecord["msnfp_birthday"] != null)
                {
                    jsonDataObj.BirthDate = (DateTime)queriedEntityRecord["msnfp_birthday"];
                    localContext.TracingService.Trace("Got msnfp_birthday.");
                }
                else
                {
                    jsonDataObj.BirthDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_birthday.");
                }

                if (queriedEntityRecord.Contains("donotbulkemail") && queriedEntityRecord["donotbulkemail"] != null)
                {
                    jsonDataObj.DoNotBulkEMail = (bool)queriedEntityRecord["donotbulkemail"];
                    localContext.TracingService.Trace("Got donotbulkemail.");
                }
                else
                {
                    jsonDataObj.DoNotBulkEMail = null;
                    localContext.TracingService.Trace("Did NOT find donotbulkemail.");
                }

                if (queriedEntityRecord.Contains("donotbulkpostalmail") && queriedEntityRecord["donotbulkpostalmail"] != null)
                {
                    jsonDataObj.DoNotBulkPostalMail = (bool)queriedEntityRecord["donotbulkpostalmail"];
                    localContext.TracingService.Trace("Got donotbulkpostalmail.");
                }
                else
                {
                    jsonDataObj.DoNotBulkPostalMail = null;
                    localContext.TracingService.Trace("Did NOT find donotbulkpostalmail.");
                }

                if (queriedEntityRecord.Contains("donotemail") && queriedEntityRecord["donotemail"] != null)
                {
                    jsonDataObj.DoNotEmail = (bool)queriedEntityRecord["donotemail"];
                    localContext.TracingService.Trace("Got donotemail.");
                }
                else
                {
                    jsonDataObj.DoNotEmail = null;
                    localContext.TracingService.Trace("Did NOT find donotemail.");
                }

                if (queriedEntityRecord.Contains("donotfax") && queriedEntityRecord["donotfax"] != null)
                {
                    jsonDataObj.DoNotFax = (bool)queriedEntityRecord["donotfax"];
                    localContext.TracingService.Trace("Got donotfax.");
                }
                else
                {
                    jsonDataObj.DoNotFax = null;
                    localContext.TracingService.Trace("Did NOT find donotfax.");
                }

                if (queriedEntityRecord.Contains("donotphone") && queriedEntityRecord["donotphone"] != null)
                {
                    jsonDataObj.DoNotPhone = (bool)queriedEntityRecord["donotphone"];
                    localContext.TracingService.Trace("Got donotphone.");
                }
                else
                {
                    jsonDataObj.DoNotPhone = null;
                    localContext.TracingService.Trace("Did NOT find donotphone.");
                }

                if (queriedEntityRecord.Contains("donotpostalmail") && queriedEntityRecord["donotpostalmail"] != null)
                {
                    jsonDataObj.DoNotPostalMail = (bool)queriedEntityRecord["donotpostalmail"];
                    localContext.TracingService.Trace("Got donotpostalmail.");
                }
                else
                {
                    jsonDataObj.DoNotPostalMail = null;
                    localContext.TracingService.Trace("Did NOT find donotpostalmail.");
                }

                if (queriedEntityRecord.Contains("emailaddress1") && queriedEntityRecord["emailaddress1"] != null)
                {
                    jsonDataObj.EmailAddress1 = (string)queriedEntityRecord["emailaddress1"];
                    localContext.TracingService.Trace("Got emailaddress1.");
                }
                else
                {
                    jsonDataObj.EmailAddress1 = null;
                    localContext.TracingService.Trace("Did NOT find emailaddress1.");
                }

                if (queriedEntityRecord.Contains("emailaddress2") && queriedEntityRecord["emailaddress2"] != null)
                {
                    jsonDataObj.EmailAddress2 = (string)queriedEntityRecord["emailaddress2"];
                    localContext.TracingService.Trace("Got emailaddress2.");
                }
                else
                {
                    jsonDataObj.EmailAddress2 = null;
                    localContext.TracingService.Trace("Did NOT find emailaddress2.");
                }

                if (queriedEntityRecord.Contains("emailaddress3") && queriedEntityRecord["emailaddress3"] != null)
                {
                    jsonDataObj.EmailAddress3 = (string)queriedEntityRecord["emailaddress3"];
                    localContext.TracingService.Trace("Got emailaddress3.");
                }
                else
                {
                    jsonDataObj.EmailAddress3 = null;
                    localContext.TracingService.Trace("Did NOT find emailaddress3.");
                }

                if (queriedEntityRecord.Contains("firstname") && queriedEntityRecord["firstname"] != null)
                {
                    jsonDataObj.FirstName = (string)queriedEntityRecord["firstname"];
                    localContext.TracingService.Trace("Got firstname.");
                }
                else
                {
                    jsonDataObj.FirstName = null;
                    localContext.TracingService.Trace("Did NOT find firstname.");
                }

                if (queriedEntityRecord.Contains("fullname") && queriedEntityRecord["fullname"] != null)
                {
                    jsonDataObj.FullName = (string)queriedEntityRecord["fullname"];
                    localContext.TracingService.Trace("Got fullname.");
                }
                else
                {
                    jsonDataObj.FullName = null;
                    localContext.TracingService.Trace("Did NOT find fullname.");
                }

                if (queriedEntityRecord.Contains("gendercode") && queriedEntityRecord["gendercode"] != null)
                {
                    jsonDataObj.GenderCode = ((OptionSetValue)queriedEntityRecord["gendercode"]).Value;
                    localContext.TracingService.Trace("Got gendercode.");
                }
                else
                {
                    jsonDataObj.GenderCode = null;
                    localContext.TracingService.Trace("Did NOT find gendercode.");
                }

                if (queriedEntityRecord.Contains("jobtitle") && queriedEntityRecord["jobtitle"] != null)
                {
                    jsonDataObj.JobTitle = (string)queriedEntityRecord["jobtitle"];
                    localContext.TracingService.Trace("Got jobtitle.");
                }
                else
                {
                    jsonDataObj.JobTitle = null;
                    localContext.TracingService.Trace("Did NOT find jobtitle.");
                }

                if (queriedEntityRecord.Contains("lastname") && queriedEntityRecord["lastname"] != null)
                {
                    jsonDataObj.LastName = (string)queriedEntityRecord["lastname"];
                    localContext.TracingService.Trace("Got lastname.");
                }
                else
                {
                    jsonDataObj.LastName = null;
                    localContext.TracingService.Trace("Did NOT find lastname.");
                }

                if (queriedEntityRecord.Contains("masterid") && queriedEntityRecord["masterid"] != null)
                {
                    jsonDataObj.MasterId = ((EntityReference)queriedEntityRecord["masterid"]).Id;
                    localContext.TracingService.Trace("Got masterid.");
                }
                else
                {
                    jsonDataObj.MasterId = null;
                    localContext.TracingService.Trace("Did NOT find masterid.");
                }

                if (queriedEntityRecord.Contains("owningbusinessunit") && queriedEntityRecord["owningbusinessunit"] != null)
                {
                    jsonDataObj.OwningBusinessUnitId = ((EntityReference)queriedEntityRecord["owningbusinessunit"]).Id;
                    localContext.TracingService.Trace("Got owningbusinessunit.");
                }
                else
                {
                    jsonDataObj.OwningBusinessUnitId = null;
                    localContext.TracingService.Trace("Did NOT find owningbusinessunit.");
                }

                if (queriedEntityRecord.Contains("msnfp_age") && queriedEntityRecord["msnfp_age"] != null)
                {
                    jsonDataObj.msnfp_Age = (int)queriedEntityRecord["msnfp_age"];
                    localContext.TracingService.Trace("Got msnfp_age.");
                }
                else
                {
                    jsonDataObj.msnfp_Age = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_age.");
                }

                if (queriedEntityRecord.Contains("msnfp_anonymity") && queriedEntityRecord["msnfp_anonymity"] != null)
                {
                    jsonDataObj.msnfp_Anonymity = ((OptionSetValue)queriedEntityRecord["msnfp_anonymity"]).Value;
                    localContext.TracingService.Trace("Got msnfp_anonymity.");
                }
                else
                {
                    jsonDataObj.msnfp_Anonymity = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_anonymity.");
                }

                if (queriedEntityRecord.Contains("msnfp_count_lifetimetransactions") && queriedEntityRecord["msnfp_count_lifetimetransactions"] != null)
                {
                    jsonDataObj.msnfp_Count_LifetimeTransactions = (int)queriedEntityRecord["msnfp_count_lifetimetransactions"];
                    localContext.TracingService.Trace("Got msnfp_count_lifetimetransactions.");
                }
                else
                {
                    jsonDataObj.msnfp_Count_LifetimeTransactions = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_count_lifetimetransactions.");
                }

                if (queriedEntityRecord.Contains("msnfp_givinglevelid") && queriedEntityRecord["msnfp_givinglevelid"] != null)
                {
                    jsonDataObj.msnfp_GivingLevelId = ((EntityReference)queriedEntityRecord["msnfp_givinglevelid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_givinglevelid.");
                }
                else
                {
                    jsonDataObj.msnfp_GivingLevelId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_givinglevelid.");
                }

                if (queriedEntityRecord.Contains("msnfp_lasteventpackagedate") && queriedEntityRecord["msnfp_lasteventpackagedate"] != null)
                {
                    jsonDataObj.msnfp_LastEventPackageDate = (DateTime)queriedEntityRecord["msnfp_lasteventpackagedate"];
                    localContext.TracingService.Trace("Got msnfp_lasteventpackagedate.");
                }
                else
                {
                    jsonDataObj.msnfp_LastEventPackageDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lasteventpackagedate.");
                }

                if (queriedEntityRecord.Contains("msnfp_lasteventpackageid") && queriedEntityRecord["msnfp_lasteventpackageid"] != null)
                {
                    jsonDataObj.msnfp_LastEventPackageId = ((EntityReference)queriedEntityRecord["msnfp_lasteventpackageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_lasteventpackageid.");
                }
                else
                {
                    jsonDataObj.msnfp_LastEventPackageId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lasteventpackageid.");
                }

                if (queriedEntityRecord.Contains("msnfp_lasttransactiondate") && queriedEntityRecord["msnfp_lasttransactiondate"] != null)
                {
                    jsonDataObj.msnfp_LastTransactionDate = (DateTime)queriedEntityRecord["msnfp_lasttransactiondate"];
                    localContext.TracingService.Trace("Got msnfp_lasttransactiondate.");
                }
                else
                {
                    jsonDataObj.msnfp_LastTransactionDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lasttransactiondate.");
                }

                if (queriedEntityRecord.Contains("msnfp_lasttransactionid") && queriedEntityRecord["msnfp_lasttransactionid"] != null)
                {
                    jsonDataObj.msnfp_LastTransactionId = ((EntityReference)queriedEntityRecord["msnfp_lasttransactionid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_lasttransactionid.");
                }
                else
                {
                    jsonDataObj.msnfp_LastTransactionId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lasttransactionid.");
                }

                if (queriedEntityRecord.Contains("msnfp_preferredlanguagecode") && queriedEntityRecord["msnfp_preferredlanguagecode"] != null)
                {
                    jsonDataObj.msnfp_PreferredLanguageCode = ((OptionSetValue)queriedEntityRecord["msnfp_preferredlanguagecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_preferredlanguagecode.");
                }
                else
                {
                    jsonDataObj.msnfp_PreferredLanguageCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_preferredlanguagecode.");
                }

                if (queriedEntityRecord.Contains("msnfp_primarymembershipid") && queriedEntityRecord["msnfp_primarymembershipid"] != null)
                {
                    jsonDataObj.msnfp_PrimaryMembershipId = ((EntityReference)queriedEntityRecord["msnfp_primarymembershipid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_primarymembershipid.");
                }
                else
                {
                    jsonDataObj.msnfp_PrimaryMembershipId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_primarymembershipid.");
                }

                if (queriedEntityRecord.Contains("msnfp_receiptpreferencecode") && queriedEntityRecord["msnfp_receiptpreferencecode"] != null)
                {
                    jsonDataObj.msnfp_ReceiptPreferenceCode = ((OptionSetValue)queriedEntityRecord["msnfp_receiptpreferencecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_receiptpreferencecode.");
                }
                else
                {
                    jsonDataObj.msnfp_ReceiptPreferenceCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptpreferencecode.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_lifetimetransactions") && queriedEntityRecord["msnfp_sum_lifetimetransactions"] != null)
                {
                    jsonDataObj.msnfp_Sum_LifetimeTransactions = ((Money)queriedEntityRecord["msnfp_sum_lifetimetransactions"]).Value;
                    localContext.TracingService.Trace("Got msnfp_primarymembershipid.");
                }
                else
                {
                    jsonDataObj.msnfp_Sum_LifetimeTransactions = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_lifetimetransactions.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone1typecode") && queriedEntityRecord["msnfp_telephone1typecode"] != null)
                {
                    jsonDataObj.msnfp_Telephone1TypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_telephone1typecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_telephone1typecode.");
                }
                else
                {
                    jsonDataObj.msnfp_Telephone1TypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone1typecode.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone2typecode") && queriedEntityRecord["msnfp_telephone2typecode"] != null)
                {
                    jsonDataObj.msnfp_Telephone2TypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_telephone2typecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_telephone2typecode.");
                }
                else
                {
                    jsonDataObj.msnfp_Telephone2TypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone2typecode.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone3typecode") && queriedEntityRecord["msnfp_telephone3typecode"] != null)
                {
                    jsonDataObj.msnfp_Telephone3TypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_telephone3typecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_telephone3typecode.");
                }
                else
                {
                    jsonDataObj.msnfp_Telephone3TypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone3typecode.");
                }

                if (queriedEntityRecord.Contains("msnfp_upcomingbirthday") && queriedEntityRecord["msnfp_upcomingbirthday"] != null)
                {
                    jsonDataObj.msnfp_UpcomingBirthday = (DateTime)queriedEntityRecord["msnfp_upcomingbirthday"];
                    localContext.TracingService.Trace("Got msnfp_upcomingbirthday.");
                }
                else
                {
                    jsonDataObj.msnfp_UpcomingBirthday = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_upcomingbirthday.");
                }

                if (queriedEntityRecord.Contains("msnfp_vip") && queriedEntityRecord["msnfp_vip"] != null)
                {
                    jsonDataObj.msnfp_Vip = (bool)queriedEntityRecord["msnfp_vip"];
                    localContext.TracingService.Trace("Got msnfp_vip.");
                }
                else
                {
                    jsonDataObj.msnfp_Vip = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_vip.");
                }

                if (queriedEntityRecord.Contains("merged") && queriedEntityRecord["merged"] != null)
                {
                    jsonDataObj.Merged = (bool)queriedEntityRecord["merged"];
                    localContext.TracingService.Trace("Got merged.");
                }
                else
                {
                    jsonDataObj.Merged = null;
                    localContext.TracingService.Trace("Did NOT find merged.");
                }

                if (queriedEntityRecord.Contains("middlename") && queriedEntityRecord["middlename"] != null)
                {
                    jsonDataObj.MiddleName = (string)queriedEntityRecord["middlename"];
                    localContext.TracingService.Trace("Got middlename.");
                }
                else
                {
                    jsonDataObj.MiddleName = null;
                    localContext.TracingService.Trace("Did NOT find middlename.");
                }

                if (queriedEntityRecord.Contains("mobilephone") && queriedEntityRecord["mobilephone"] != null)
                {
                    jsonDataObj.MobilePhone = (string)queriedEntityRecord["mobilephone"];
                    localContext.TracingService.Trace("Got mobilephone.");
                }
                else
                {
                    jsonDataObj.MobilePhone = null;
                    localContext.TracingService.Trace("Did NOT find mobilephone.");
                }

                if (queriedEntityRecord.Contains("parentcustomerid") && queriedEntityRecord["parentcustomerid"] != null)
                {
                    jsonDataObj.ParentCustomerId = ((EntityReference)queriedEntityRecord["parentcustomerid"]).Id;

                    // Set the ParentCustomerIdType. 1 = Account, 2 = Contact
                    if (((EntityReference)queriedEntityRecord["parentcustomerid"]).LogicalName.ToLower() == "contact")
                    {
                        jsonDataObj.ParentCustomerIdType = 2;
                    }
                    else if (((EntityReference)queriedEntityRecord["parentcustomerid"]).LogicalName.ToLower() == "account")
                    {
                        jsonDataObj.ParentCustomerIdType = 1;
                    }

                    localContext.TracingService.Trace("Got parentcustomerid.");
                }
                else
                {
                    jsonDataObj.ParentCustomerId = null;
                    jsonDataObj.ParentCustomerIdType = null;
                    localContext.TracingService.Trace("Did NOT find parentcustomerid.");
                }

                if (queriedEntityRecord.Contains("salutation") && queriedEntityRecord["salutation"] != null)
                {
                    jsonDataObj.Salutation = (string)queriedEntityRecord["salutation"];
                    localContext.TracingService.Trace("Got salutation.");
                }
                else
                {
                    jsonDataObj.Salutation = null;
                    localContext.TracingService.Trace("Did NOT find salutation.");
                }

                if (queriedEntityRecord.Contains("suffix") && queriedEntityRecord["suffix"] != null)
                {
                    jsonDataObj.Suffix = (string)queriedEntityRecord["suffix"];
                    localContext.TracingService.Trace("Got suffix.");
                }
                else
                {
                    jsonDataObj.Suffix = null;
                    localContext.TracingService.Trace("Did NOT find suffix.");
                }

                if (queriedEntityRecord.Contains("telephone1") && queriedEntityRecord["telephone1"] != null)
                {
                    jsonDataObj.Telephone1 = (string)queriedEntityRecord["telephone1"];
                    localContext.TracingService.Trace("Got telephone1.");
                }
                else
                {
                    jsonDataObj.Telephone1 = null;
                    localContext.TracingService.Trace("Did NOT find telephone1.");
                }

                if (queriedEntityRecord.Contains("telephone2") && queriedEntityRecord["telephone2"] != null)
                {
                    jsonDataObj.Telephone2 = (string)queriedEntityRecord["telephone2"];
                    localContext.TracingService.Trace("Got telephone2.");
                }
                else
                {
                    jsonDataObj.Telephone2 = null;
                    localContext.TracingService.Trace("Did NOT find telephone2.");
                }

                if (queriedEntityRecord.Contains("telephone3") && queriedEntityRecord["telephone3"] != null)
                {
                    jsonDataObj.Telephone3 = (string)queriedEntityRecord["telephone3"];
                    localContext.TracingService.Trace("Got telephone3.");
                }
                else
                {
                    jsonDataObj.Telephone3 = null;
                    localContext.TracingService.Trace("Did NOT find telephone3.");
                }

                if (queriedEntityRecord.Contains("transactioncurrencyid") && queriedEntityRecord["transactioncurrencyid"] != null)
                {
                    jsonDataObj.TransactionCurrencyId = ((EntityReference)queriedEntityRecord["transactioncurrencyid"]).Id;
                    localContext.TracingService.Trace("Got TransactionCurrencyId.");
                }
                else
                {
                    jsonDataObj.TransactionCurrencyId = null;
                    localContext.TracingService.Trace("Did NOT find TransactionCurrencyId.");
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

                if (queriedEntityRecord.Contains("msnfp_householdid") && queriedEntityRecord["msnfp_householdid"] != null)
                {
                    jsonDataObj.msnfp_householdid = ((EntityReference)queriedEntityRecord["msnfp_householdid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_householdid.");
                }
                else
                {
                    jsonDataObj.msnfp_householdid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_householdid.");
                }


                if (queriedEntityRecord.Contains("msnfp_year0_giving") && queriedEntityRecord["msnfp_year0_giving"] != null)
                {
                    jsonDataObj.msnfp_year0_giving = ((Money)queriedEntityRecord["msnfp_year0_giving"]).Value;
                    localContext.TracingService.Trace("Got msnfp_year0_giving.");
                }
                else
                {
                    jsonDataObj.msnfp_year0_giving = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_year0_giving.");
                }


                if (queriedEntityRecord.Contains("msnfp_year1_giving") && queriedEntityRecord["msnfp_year1_giving"] != null)
                {
                    jsonDataObj.msnfp_year1_giving = ((Money)queriedEntityRecord["msnfp_year1_giving"]).Value;
                    localContext.TracingService.Trace("Got msnfp_year1_giving.");
                }
                else
                {
                    jsonDataObj.msnfp_year1_giving = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_year1_giving.");
                }


                if (queriedEntityRecord.Contains("msnfp_year2_giving") && queriedEntityRecord["msnfp_year2_giving"] != null)
                {
                    jsonDataObj.msnfp_year2_giving = ((Money)queriedEntityRecord["msnfp_year2_giving"]).Value;
                    localContext.TracingService.Trace("Got msnfp_year2_giving.");
                }
                else
                {
                    jsonDataObj.msnfp_year2_giving = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_year2_giving.");
                }

                if (queriedEntityRecord.Contains("msnfp_year3_giving") && queriedEntityRecord["msnfp_year3_giving"] != null)
                {
                    jsonDataObj.msnfp_year3_giving = ((Money)queriedEntityRecord["msnfp_year3_giving"]).Value;
                    localContext.TracingService.Trace("Got msnfp_year3_giving.");
                }
                else
                {
                    jsonDataObj.msnfp_year3_giving = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_year3_giving.");
                }

                if (queriedEntityRecord.Contains("msnfp_year4_giving") && queriedEntityRecord["msnfp_year4_giving"] != null)
                {
                    jsonDataObj.msnfp_year4_giving = ((Money)queriedEntityRecord["msnfp_year4_giving"]).Value;
                    localContext.TracingService.Trace("Got msnfp_year4_giving.");
                }
                else
                {
                    jsonDataObj.msnfp_year4_giving = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_year4_giving.");
                }

                if (queriedEntityRecord.Contains("msnfp_lifetimegivingsum") && queriedEntityRecord["msnfp_lifetimegivingsum"] != null)
                {
                    jsonDataObj.msnfp_lifetimegivingsum = ((Money)queriedEntityRecord["msnfp_lifetimegivingsum"]).Value;
                    localContext.TracingService.Trace("Got msnfp_lifetimegivingsum.");
                }
                else
                {
                    jsonDataObj.msnfp_lifetimegivingsum = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lifetimegivingsum.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Contact));
                ser.WriteObject(ms, jsonDataObj);
                byte[] json = ms.ToArray();
                ms.Close();

                var jsonBody = Encoding.UTF8.GetString(json, 0, json.Length);

                WebAPIClient client = new WebAPIClient();
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers["Padlock"] = (string)configurationRecord["msnfp_apipadlocktoken"];
                client.Encoding = UTF8Encoding.UTF8;

                localContext.TracingService.Trace("---------Preparing JSON---------");
                localContext.TracingService.Trace("Converted to json API URL : " + apiUrl);
                localContext.TracingService.Trace("JSON: " + jsonBody);
                localContext.TracingService.Trace("---------End of Preparing JSON---------");
                localContext.TracingService.Trace("Sending data to Azure.");

                string fileContent = client.UploadString(apiUrl, jsonBody);

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


        private void SetUpHousehold(IOrganizationService service, string houseHoldSequence, ref Entity retreivedContact, Entity target, IPluginExecutionContext context, ITracingService tracingService)
        {
            Entity updatedTarget = new Entity(retreivedContact.LogicalName, retreivedContact.Id);
            bool isDeceased = target.GetAttributeValue<bool>("msnfp_deceased");

            // Just household was updated
            // change relationship to member if it has a primary member
            if (target.GetAttributeValue<EntityReference>("msnfp_householdid") != null && target.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") == null)
            {
                EntityCollection newHouseholdPrimaryMembers = GetMembers(service, target.GetAttributeValue<EntityReference>("msnfp_householdid").Id, (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember, target.Id);

                if (newHouseholdPrimaryMembers.Entities.Count > 0)
                {
                    if (retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") != null
                    && retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship").Value == (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember)
                        UpdateEntity(service, new Entity(target.LogicalName, target.Id)
                        {
                            Attributes =
                        {
                            new KeyValuePair<string, object>("msnfp_householdrelationship",new OptionSetValue((int)Utilities.HouseholdRelationshipType.Member))
                        }
                        });
                }
                else if (retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") == null)
                {
                    // set this member as primary member
                    UpdateEntity(service, new Entity(target.LogicalName, target.Id)
                    {
                        Attributes =
                        {
                            new KeyValuePair<string, object>("msnfp_householdrelationship",new OptionSetValue((int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember))
                        }
                    });

                    // Also update the primary contact id to match the above on the household:
                    tracingService.Trace("SetUpHousehold");
                    service.Update(new Entity("account", target.GetAttributeValue<EntityReference>("msnfp_householdid").Id)
                    {
                        Attributes =
                        {
                            new KeyValuePair<string, object>("primarycontactid",new EntityReference("contact",target.Id))
                        }
                    });
                }

                return;
            }

            Entity donationImport = GetDonationImportFromExecutionPipeLine(context, service);

            if (isDeceased)
            {
                if (retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") != null
                    && retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship").Value != (int)Utilities.HouseholdRelationshipType.Deceased)
                    updatedTarget["msnfp_householdrelationship"] = new OptionSetValue((int)Utilities.HouseholdRelationshipType.Deceased);

                if (retreivedContact.GetAttributeValue<EntityReference>("msnfp_householdid") != null)
                    ResetHouseholdPrimaryContact(service, retreivedContact.GetAttributeValue<EntityReference>("msnfp_householdid"), retreivedContact.Id, tracingService);
            }
            else if (retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") != null)
            {
                switch (retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship").Value)
                {
                    case (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember:
                        // Household lookup is null
                        // Contact creation may have also been triggerred from Donation Import
                        if (retreivedContact.GetAttributeValue<EntityReference>("msnfp_householdid") == null && (donationImport == null || (donationImport != null && donationImport.GetAttributeValue<bool>("msnfp_createhousehold"))))
                        {
                            EntityReference household = Utilities.CreateHouseholdFromContact(service, retreivedContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship"), retreivedContact);
                            updatedTarget["msnfp_householdid"] = household;
                            retreivedContact["msnfp_householdid"] = household;

                            // update donation import with household
                            if (donationImport != null)
                            {
                                Entity updatedDonationImport = new Entity(donationImport.LogicalName, donationImport.Id);
                                updatedDonationImport["msnfp_householdid"] = household;
                                UpdateEntity(service, updatedDonationImport);
                            }
                        }
                        else
                        {
                            // Set all other associated contacts that may be primary member to household as members
                            UpdateHouseholdPrimaryMembers(service, retreivedContact.GetAttributeValue<EntityReference>("msnfp_householdid"), (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember, retreivedContact.Id, tracingService);
                        }

                        if (retreivedContact.GetAttributeValue<bool>("msnfp_deceased"))
                            updatedTarget["msnfp_deceased"] = false;

                        break;
                    case (int)Utilities.HouseholdRelationshipType.Deceased:

                        if (retreivedContact.GetAttributeValue<EntityReference>("msnfp_householdid") != null)
                        {
                            ResetHouseholdPrimaryContact(service, retreivedContact.GetAttributeValue<EntityReference>("msnfp_householdid"), retreivedContact.Id, tracingService);

                            if (!retreivedContact.GetAttributeValue<bool>("msnfp_deceased"))
                                updatedTarget["msnfp_deceased"] = true;
                        }
                        break;
                    case (int)Utilities.HouseholdRelationshipType.Member:
                    case (int)Utilities.HouseholdRelationshipType.Minor:
                        if (retreivedContact.GetAttributeValue<bool>("msnfp_deceased"))
                            updatedTarget["msnfp_deceased"] = false;
                        break;
                    default:
                        break;
                }
            }

            UpdateEntity(service, updatedTarget);
        }

        private static void UpdateEntity(IOrganizationService service, Entity updatedTarget)
        {
            // post create - transaction has completed
            // ensures only modified fields are updated
            if (updatedTarget.Attributes.Any(a => a.Key.Contains("msnfp")))
                service.Update(updatedTarget);
        }

        private static void ResetHouseholdPrimaryContact(IOrganizationService service, EntityReference householdReference, Guid contactId, ITracingService tracingService)
        {
            if (householdReference == null)
                return;
            Entity household = service.Retrieve(householdReference.LogicalName, householdReference.Id, new ColumnSet("primarycontactid"));
            // check if primary contact is same as current contact
            // set to null if matches
            if (household.GetAttributeValue<EntityReference>("primarycontactid") != null && household.GetAttributeValue<EntityReference>("primarycontactid").Id == contactId)
            {
                tracingService.Trace("ResetHouseholdPrimaryContact");
                household["primarycontactid"] = null;
                service.Update(household);
            }
        }

        private void UpdateHouseholdPrimaryMembers(IOrganizationService service, EntityReference houseHoldRef, int relationshiptType, Guid currentContactGuid, ITracingService tracingService)
        {
            if (houseHoldRef == null)
                return;
            EntityCollection contacts = GetMembers(service, houseHoldRef.Id, relationshiptType, currentContactGuid);

            // also update the current contact as primary contact on the household record
            if (relationshiptType == (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember)
            {
                contacts.Entities.ToList().ForEach(c =>
              {
                  Entity updatedContact = new Entity("contact");
                  updatedContact["msnfp_householdrelationship"] = new OptionSetValue((int)Utilities.HouseholdRelationshipType.Member);
                  updatedContact.Id = c.Id;

                  service.Update(updatedContact);
              });
                tracingService.Trace("UpdateHouseholdPrimaryMembers");
                service.Update(new Entity(houseHoldRef.LogicalName, houseHoldRef.Id)
                {
                    Attributes =
                    {
                        new KeyValuePair<string, object>("primarycontactid",new EntityReference("contact",currentContactGuid))
                    }
                });
            }
        }

        private static EntityCollection GetMembers(IOrganizationService service, Guid houseHoldId, int relationshiptType, Guid currentContactGuid)
        {
            QueryExpression queryExpression = new QueryExpression("contact");
            // contacts may be created throuh donation import tool as well
            queryExpression.NoLock = false;
            queryExpression.ColumnSet = new ColumnSet("contactid");
            queryExpression.Criteria.AddCondition(new ConditionExpression("msnfp_householdid", ConditionOperator.Equal, houseHoldId));
            queryExpression.Criteria.AddCondition(new ConditionExpression("msnfp_householdrelationship", ConditionOperator.Equal, relationshiptType));
            queryExpression.Criteria.AddCondition(new ConditionExpression("contactid", ConditionOperator.NotEqual, currentContactGuid));


            return service.RetrieveMultiple(queryExpression);
        }
    }
}

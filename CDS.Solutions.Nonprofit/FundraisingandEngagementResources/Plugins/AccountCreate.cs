/*************************************************************************
* © Microsoft. All rights reserved.
*/

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using Plugins.PaymentProcesses;
using Plugins.AzureModels;
using System.Linq;

namespace Plugins
{
    public class AccountCreate : PluginBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccountCreate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public AccountCreate(string unsecure, string secure)
            : base(typeof(AccountCreate))
        {
            // TODO: Implement your custom configuration handling.
        }

        public ITracingService tracer { get; set; }

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
            tracer = localContext.TracingService;
            // ********************************* PLUGIN's BEGINNING *********************************************
            if (localContext == null)
                throw new ArgumentNullException("localContext");

            localContext.TracingService.Trace("---------Triggered AccountCreate.cs---------");


            // ********************************* PLUGIN'S PREPARATION *******************************************
            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);


            // ********************************* PLUGIN'S VALIDATION ********************************************
            if (string.Compare(context.MessageName, "create", StringComparison.CurrentCultureIgnoreCase) != 0 && context.Depth > 1 && !CheckExecutionPipeLine(context))
            {
                localContext.TracingService.Trace("Context.depth > 1 => Exiting Plugin. context.Depth: " + context.Depth);
                if (context.ParentContext != null)
                    localContext.TracingService.Trace($"Parent context {context.ParentContext.PrimaryEntityName}");

                if (context.ParentContext != null && context.ParentContext.ParentContext != null)
                    localContext.TracingService.Trace($"Parent context (Parent) {context.ParentContext.ParentContext.PrimaryEntityName}");

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
                    if (targetRecord.LogicalName.ToLower() != "account")
                        throw new InvalidPluginExecutionException("The target entity is NOT ACCOUNT. Exiting plugin.");
                }
                // If we don't do this, we won't be able to delete an entity as the taget is NOT an entity when deleting, it is an entityreference:
                else if (messageName == "Delete")
                {
                    Entity deletedEntityRecord = service.Retrieve("account", ((EntityReference)context.InputParameters["Target"]).Id, GetAccountFields());
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
                Entity accountEntity = service.Retrieve(targetRecord.LogicalName, targetRecord.Id, GetAccountFields());



                if (accountEntity.GetAttributeValue<OptionSetValue>("msnfp_accounttype") != null
                    && accountEntity.GetAttributeValue<OptionSetValue>("msnfp_accounttype").Value == (int)Utilities.AccountType.Household)
                {
                    if (messageName.ToLower() == "create")
                    {
                        // Generate household sequence if needed
                        string sequence = Utilities.GenerateHouseHoldSequence(service, accountEntity, configurationRecord);
                        if (!string.IsNullOrEmpty(sequence))
                            targetRecord["name"] = sequence;
                    }

                    // check if primary contact is primary member, else update
                    UpdateHouseholdPrimaryMembers(service, accountEntity.Id, accountEntity.GetAttributeValue<EntityReference>("primarycontactid"));

                }
                // All logics should happen before this step

                // Synchronize the record to Azure
                if (configurationRecord.Contains("msnfp_apipadlocktoken") && configurationRecord["msnfp_apipadlocktoken"] != null)
                    AddOrUpdateThisRecordWithAzure(accountEntity, configurationRecord, localContext, service, context);
                else
                    localContext.TracingService.Trace("No Padlock Token found to synchronize the record to Azure. AddOrUpdateThisRecordWithAzure() failed.");
            }

            // ********************************* PLUGIN's END *********************************************
            localContext.TracingService.Trace("---------Exitting AccountCreate.cs---------");
        }

        private bool CheckExecutionPipeLine(IPluginExecutionContext context)
        {
            return (context.ParentContext != null && context.ParentContext.ParentContext != null
                                                  && (context.ParentContext.ParentContext.PrimaryEntityName == "msnfp_donationimport" || context.ParentContext.ParentContext.PrimaryEntityName == "contact"));
        }

        private ColumnSet GetAccountFields()
        {
            ColumnSet accColumn = new ColumnSet(new String[]
            {
                "accountid", "address1_addressid", "address1_addresstypecode", "address1_city", "address1_country",
                "address1_county", "address1_latitude", "address1_line1", "address1_line2", "address1_line3",
                "address1_longitude", "address1_name", "address1_postalcode", "address1_postofficebox",
                "address1_stateorprovince", "address2_addressid", "address2_addresstypecode", "address2_city",
                "address2_country", "address2_county", "address2_latitude", "address2_line1", "address2_line2",
                "address2_line3", "address2_longitude", "address2_name", "address2_postalcode",
                "address2_postofficebox", "address2_stateorprovince", "donotbulkemail", "donotbulkpostalmail",
                "donotemail", "donotfax", "donotphone", "donotpostalmail", "emailaddress1", "emailaddress2",
                "emailaddress3", "masterid", "owningbusinessunit", "msnfp_anonymity",
                "msnfp_count_lifetimetransactions", "msnfp_givinglevelid", "msnfp_lasteventpackagedate",
                "msnfp_lasteventpackageid", "msnfp_lasttransactiondate", "msnfp_lasttransactionid",
                "msnfp_preferredlanguagecode", "msnfp_primarymembershipid", "msnfp_receiptpreferencecode",
                "msnfp_sum_lifetimetransactions", "msnfp_telephone1typecode", "msnfp_telephone2typecode",
                "msnfp_telephone3typecode", "msnfp_vip", "merged", "name", "parentaccountid", "telephone1",
                "telephone2", "telephone3", "websiteurl", "transactioncurrencyid", "statecode", "statuscode",
                "createdon", "msnfp_accounttype", "msnfp_year0_giving", "msnfp_year1_giving", "msnfp_year2_giving",
                "msnfp_year3_giving","msnfp_year4_giving","primarycontactid", "msnfp_lifetimegivingsum"
            });
            return accColumn;
        }

        private void UpdateHouseholdPrimaryMembers(IOrganizationService service, Guid houseHoldId, EntityReference primaryContactRef)
        {
            if (primaryContactRef == null)
                return;

            QueryExpression queryExpression = new QueryExpression("contact");
            // contacts may be created throuh donation import tool as well
            queryExpression.NoLock = true;
            queryExpression.ColumnSet = new ColumnSet("contactid");
            queryExpression.Criteria.AddCondition(new ConditionExpression("msnfp_householdid", ConditionOperator.Equal, houseHoldId));
            queryExpression.Criteria.AddCondition(new ConditionExpression("msnfp_householdrelationship", ConditionOperator.Equal, (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember));

            EntityCollection contacts = service.RetrieveMultiple(queryExpression);

            // also update the current contact as primary contact on the household record
            contacts.Entities.ToList().ForEach(c =>
            {
                Entity updatedContact = new Entity("contact");

                if (c.Id != primaryContactRef.Id)
                {
                    updatedContact["msnfp_householdrelationship"] = new OptionSetValue((int)Utilities.HouseholdRelationshipType.Member);
                    updatedContact.Id = c.Id;
                    service.Update(updatedContact);
                }
            });

            Entity primaryContact = service.Retrieve(primaryContactRef.LogicalName, primaryContactRef.Id, new ColumnSet("msnfp_householdrelationship"));

            // this is to prevent a loop effect since updating a contact as primary without household will again trigger account create!
            if (contacts.Entities.Any(a => a.Id != primaryContactRef.Id)
                    || primaryContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") == null
                    || (primaryContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") != null
                        && primaryContact.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship").Value != (int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember))

            {
                Entity updatedContact = new Entity("contact");
                updatedContact["msnfp_householdrelationship"] = new OptionSetValue((int)Utilities.HouseholdRelationshipType.PrimaryHouseholdMember);
                updatedContact.Id = primaryContactRef.Id;
                service.Update(updatedContact);
            }
        }

        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "Account"; // Used for API calls

            string apiUrl = configurationRecord.GetAttributeValue<string>("msnfp_azure_webapiurl");

            localContext.TracingService.Trace("Got API URL: " + apiUrl);


            if (apiUrl != string.Empty)
            {



                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["accountid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                Account jsonDataObj = new Account();

                jsonDataObj.AccountId = (Guid)queriedEntityRecord["accountid"];

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

                if (queriedEntityRecord.Contains("name") && queriedEntityRecord["name"] != null)
                {
                    jsonDataObj.Name = (string)queriedEntityRecord["name"];
                    localContext.TracingService.Trace("Got name.");
                }
                else
                {
                    jsonDataObj.Name = null;
                    localContext.TracingService.Trace("Did NOT find name.");
                }

                if (queriedEntityRecord.Contains("parentaccountid") && queriedEntityRecord["parentaccountid"] != null)
                {
                    jsonDataObj.ParentAccountId = ((EntityReference)queriedEntityRecord["parentaccountid"]).Id;

                    localContext.TracingService.Trace("Got parentaccountid.");
                }
                else
                {
                    jsonDataObj.ParentAccountId = null;
                    localContext.TracingService.Trace("Did NOT find parentaccountid.");
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

                if (queriedEntityRecord.Contains("websiteurl") && queriedEntityRecord["websiteurl"] != null)
                {
                    jsonDataObj.WebSiteURL = (string)queriedEntityRecord["websiteurl"];
                    localContext.TracingService.Trace("Got websiteurl.");
                }
                else
                {
                    jsonDataObj.WebSiteURL = null;
                    localContext.TracingService.Trace("Did NOT find websiteurl.");
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


                if (queriedEntityRecord.Contains("msnfp_accounttype") && queriedEntityRecord["msnfp_accounttype"] != null)
                {
                    jsonDataObj.msnfp_accounttype = ((OptionSetValue)queriedEntityRecord["msnfp_accounttype"]).Value;
                    localContext.TracingService.Trace("Got msnfp_accounttype.");
                }
                else
                {
                    jsonDataObj.msnfp_accounttype = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_accounttype.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Account));
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
    }
}
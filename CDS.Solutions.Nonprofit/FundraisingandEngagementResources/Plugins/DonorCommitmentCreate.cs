/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using Plugins.PaymentProcesses;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using Plugins.AzureModels;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Net;

namespace Plugins
{
    public class DonorCommitmentCreate : PluginBase
    {
        private ITracingService tracingService;

        public DonorCommitmentCreate(string unsecure, string secure)
            : base(typeof(DonorCommitmentCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered DonorCommitmentCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            string customerType = string.Empty;
            Guid customerId = Guid.Empty;
            string messageName = context.MessageName;

            Entity targetRecord;
            EntityReference targetReference;
            Entity donorCommitmentTarget = null;

            if (context.InputParameters.Contains("Target"))
            {
                // Get the Configuration Record (Either from the User or from the Default Configuration Record
                Entity configurationRecord = Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);
                tracingService = localContext.TracingService;

                ColumnSet cols;
                cols = new ColumnSet("msnfp_customerid", "msnfp_totalamount", "msnfp_totalamount_paid", "msnfp_totalamount_balance", "msnfp_parentscheduleid", "msnfp_appealid", "msnfp_packageid", "msnfp_commitment_campaignid", "statecode", "statuscode", "msnfp_bookdate");

                localContext.TracingService.Trace("---------Entering DonorCommitmentCreate.cs Main Function---------");
                localContext.TracingService.Trace("Message Name: " + messageName);
                Utilities util = new Utilities();
                if (context.InputParameters["Target"] is Entity)
                {
                    targetRecord = (Entity)context.InputParameters["Target"];

                    donorCommitmentTarget = service.Retrieve("msnfp_donorcommitment", targetRecord.Id, cols);

                    if (messageName == "Create")
                    {
                        if (targetRecord.Contains("msnfp_name") && targetRecord["msnfp_name"] != null)
                        {
                            localContext.TracingService.Trace("Pledge's title: " + (string)targetRecord["msnfp_name"]);

                            if (targetRecord.Contains("msnfp_commitment_campaignid"))
                            {
                                Entity campaign = service.Retrieve("campaign", ((EntityReference)targetRecord["msnfp_commitment_campaignid"]).Id, new ColumnSet("name"));

                                if (campaign.Contains("name"))
                                {
                                    string campaignName = (string)campaign["name"];

                                    Entity myEntity = service.Retrieve(targetRecord.LogicalName, targetRecord.Id, new ColumnSet("msnfp_name"));

                                    if (myEntity.Contains("msnfp_name") && myEntity["msnfp_name"] != null)
                                    {
                                        myEntity["msnfp_name"] = myEntity["msnfp_name"] + " - " + campaignName;
                                        service.Update(myEntity);
                                    }
                                }
                            }
                        }

                        // See if there is a pledge match for this donor:
                        if (targetRecord.Contains("msnfp_customerid"))
                        {
                            localContext.TracingService.Trace("See if this donor has any pledge matches.");
                            // Here we get all pledge matches for this customer:
                            List<Entity> pledgeMatchingList = (from c in orgSvcContext.CreateQuery("msnfp_pledgematch")
                                                               where ((EntityReference)c["msnfp_customerfromid"]).Id == ((EntityReference)targetRecord["msnfp_customerid"]).Id
                                                               select c).ToList();

                            localContext.TracingService.Trace("Pledge Matches found: " + pledgeMatchingList.Count.ToString());
                            // If there is any, we generate another donor commitment for the "Apply the pledge to" lookup and make the donor of this record the constituent on the pledge:
                            if (pledgeMatchingList != null)
                            {
                                foreach (Entity pledgeMatchRecord in pledgeMatchingList)
                                {
                                    localContext.TracingService.Trace("Pledge Match ID to process next: " + pledgeMatchRecord.Id.ToString());
                                    // Add the pledge match for this pledge match record:
                                    AddPledgeFromPledgeMatchRecord(targetRecord, pledgeMatchRecord, localContext, service, context);
                                }
                            }
                        }

                        if (targetRecord.Contains("msnfp_commitment_defaultdesignationid"))
                            CreateDesignationRecords(localContext, service, targetRecord);

                        //Payment ScheduleAmount update on create
                        localContext.TracingService.Trace("Before checking if the Payment Schedule is present");
                        if (donorCommitmentTarget.Contains("msnfp_parentscheduleid") && donorCommitmentTarget["msnfp_parentscheduleid"] != null)
                        {
                            localContext.TracingService.Trace("Before updating the Payment Schedule");
                            util.UpdatePaymentScheduleBalance(orgSvcContext, service, donorCommitmentTarget, ((EntityReference)donorCommitmentTarget["msnfp_parentscheduleid"]), "create");
                            localContext.TracingService.Trace("After updating the Payment Schedule");
                        }

                        // Send record to Azure 
                        if (!string.IsNullOrEmpty(configurationRecord.GetAttributeValue<string>("msnfp_apipadlocktoken")))
                        {
                            AddOrUpdateThisRecordWithAzure(donorCommitmentTarget, configurationRecord, messageName);
                        }

                        Utilities.UpdateHouseholdOnRecord(service, donorCommitmentTarget, "msnfp_householdid", "msnfp_customerid");
                    }
                    if (messageName == "Update")
                    {
                        localContext.TracingService.Trace("Before checking if the Payment Schedule is present");
                        if ((targetRecord.Contains("statuscode")
                            && targetRecord["statuscode"] != null) && donorCommitmentTarget.GetAttributeValue<EntityReference>("msnfp_parentscheduleid") != null
                                                                    && (localContext.PluginExecutionContext.Depth < 2 || (localContext.PluginExecutionContext.ParentContext != null
                                                                                                                    && localContext.PluginExecutionContext.ParentContext.PrimaryEntityName.ToLower() == "msnfp_donorcommitment")))
                        {
                            localContext.TracingService.Trace("Before updating the Payment Schedule");
                            util.UpdatePaymentScheduleBalance(orgSvcContext, service, donorCommitmentTarget, ((EntityReference)donorCommitmentTarget["msnfp_parentscheduleid"]), "update");
                            localContext.TracingService.Trace("After updating the Payment Schedule");
                        }

                        // Send record to Azure 
                        if (!string.IsNullOrEmpty(configurationRecord.GetAttributeValue<string>("msnfp_apipadlocktoken")))
                        {
                            AddOrUpdateThisRecordWithAzure(donorCommitmentTarget, configurationRecord, messageName);
                        }

                        Utilities.UpdateHouseholdOnRecord(service, targetRecord, "msnfp_householdid", "msnfp_customerid");
                    }
                }

                else if (context.InputParameters["Target"] is EntityReference && string.Compare(messageName, "delete", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    targetReference = (EntityReference) context.InputParameters["Target"];
                    donorCommitmentTarget = service.Retrieve("msnfp_donorcommitment", targetReference.Id, cols);
                }


                if (donorCommitmentTarget != null)
                {
                    AddOrUpdateThisRecordWithAzure(donorCommitmentTarget, configurationRecord, messageName);

                    _ = Common.Utilities.CallYearlyGivingServiceAsync(donorCommitmentTarget.Id,
                        donorCommitmentTarget.LogicalName, configurationRecord.Id, service, localContext.TracingService);
                }


                localContext.TracingService.Trace("---------Exiting DonorCommitmentCreate.cs---------");
            }
        }

        #region Perform pledge matching (if applicable)
        private void AddPledgeFromPledgeMatchRecord(Entity donorCommitmentRecord, Entity pledgeMatchRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            // If there is, we generate another donor commitment for the "Apply the pledge to" lookup and make the donor of this record the constituent on the pledge:
            localContext.TracingService.Trace("---------Entering AddPledgeFromPledgeMatchRecord()---------");

            if (pledgeMatchRecord.Contains("msnfp_appliestocode"))
            {
                // 844060001 == Donation, which we don't want on DonorCommitmentCreate (that is handled in PostTransactionGiftCreate.cs).
                if (((OptionSetValue)pledgeMatchRecord["msnfp_appliestocode"]).Value != 844060001)
                {
                    localContext.TracingService.Trace("msnfp_appliestocode applies to transaction create, value: " + ((OptionSetValue)pledgeMatchRecord["msnfp_appliestocode"]).Value.ToString());

                    // Default is 100%:
                    int percentage = pledgeMatchRecord.Contains("msnfp_percentage") ? (int)pledgeMatchRecord["msnfp_percentage"] : 100;
                    Money amount = (Money)donorCommitmentRecord["msnfp_totalamount"];

                    // Calculate using the percentage:
                    Money mcrmAmount = new Money();
                    if (amount.Value == 0 || percentage == 0)
                        mcrmAmount.Value = 0;
                    else
                        mcrmAmount.Value = (amount.Value * percentage / 100);
                    localContext.TracingService.Trace("Commitment amount: " + mcrmAmount.Value.ToString());

                    try
                    {
                        // Now create the donor commitment record and apply all the field values from the original gift record:
                        var newDonorCommitment = new Entity("msnfp_donorcommitment");

                        if (pledgeMatchRecord.Contains("msnfp_customertoid"))
                        {
                            newDonorCommitment["msnfp_customerid"] = pledgeMatchRecord["msnfp_customertoid"]; //new EntityReference(donorEntity, donorGuid);
                            string customerType = ((EntityReference)pledgeMatchRecord["msnfp_customertoid"]).LogicalName;
                            Guid customerID = ((EntityReference)pledgeMatchRecord["msnfp_customertoid"]).Id;
                            Entity customer = null;
                            if (customerType == "contact")
                            {
                                customer = service.Retrieve(customerType, customerID, new ColumnSet("contactid", "firstname", "lastname", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "emailaddress1"));
                                newDonorCommitment["msnfp_firstname"] = customer.Contains("firstname") ? (string)customer["firstname"] : string.Empty;
                                newDonorCommitment["msnfp_lastname"] = customer.Contains("lastname") ? (string)customer["lastname"] : string.Empty;
                            }
                            else if (customerType == "account")
                            {
                                customer = service.Retrieve(customerType, customerID, new ColumnSet("accountid", "name", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "telephone1", "emailaddress1"));
                            }

                            newDonorCommitment["msnfp_billing_line1"] = customer.Contains("address1_line1") ? (string)customer["address1_line1"] : string.Empty;
                            newDonorCommitment["msnfp_billing_line2"] = customer.Contains("address1_line2") ? (string)customer["address1_line2"] : string.Empty;
                            newDonorCommitment["msnfp_billing_line3"] = customer.Contains("address1_line3") ? (string)customer["address1_line3"] : string.Empty;
                            newDonorCommitment["msnfp_billing_city"] = customer.Contains("address1_city") ? (string)customer["address1_city"] : string.Empty;
                            newDonorCommitment["msnfp_billing_stateorprovince"] = customer.Contains("address1_stateorprovince") ? (string)customer["address1_stateorprovince"] : string.Empty;
                            newDonorCommitment["msnfp_billing_country"] = customer.Contains("address1_country") ? (string)customer["address1_country"] : string.Empty;
                            newDonorCommitment["msnfp_billing_postalcode"] = customer.Contains("address1_postalcode") ? (string)customer["address1_postalcode"] : string.Empty;
                            newDonorCommitment["msnfp_telephone1"] = customer.Contains("telephone1") ? (string)customer["telephone1"] : string.Empty;
                            newDonorCommitment["msnfp_emailaddress1"] = customer.Contains("emailaddress1") ? (string)customer["emailaddress1"] : string.Empty;
                        }

                        //newDonorCommitment["msnfp_name"] = "Pledge - " + Guid.NewGuid().ToString().ToUpper().Substring(0, 6); // Mirrors the web resource technique.
                        newDonorCommitment["msnfp_customerid"] = pledgeMatchRecord["msnfp_customertoid"];
                        newDonorCommitment["msnfp_totalamount"] = mcrmAmount;
                        newDonorCommitment["msnfp_totalamount_paid"] = new Money(0);
                        newDonorCommitment["msnfp_totalamount_balance"] = mcrmAmount;
                        newDonorCommitment["msnfp_bookdate"] = donorCommitmentRecord.Contains("msnfp_bookdate") ? donorCommitmentRecord["msnfp_bookdate"] : DateTime.Today;
                        newDonorCommitment["createdby"] = new EntityReference("systemuser", context.InitiatingUserId);

                        if (donorCommitmentRecord.Contains("msnfp_commitment_campaignid"))
                        {
                            localContext.TracingService.Trace("Campaign ID: " + ((EntityReference)donorCommitmentRecord["msnfp_commitment_campaignid"]).Id.ToString());
                            newDonorCommitment["msnfp_commitment_campaignid"] = new EntityReference("campaign", ((EntityReference)donorCommitmentRecord["msnfp_commitment_campaignid"]).Id);
                        }

                        if (donorCommitmentRecord.Contains("msnfp_appealid"))
                        {
                            localContext.TracingService.Trace("Appeal ID: " + ((EntityReference)donorCommitmentRecord["msnfp_appealid"]).Id.ToString());
                            newDonorCommitment["msnfp_appealid"] = new EntityReference("msnfp_appeal", ((EntityReference)donorCommitmentRecord["msnfp_appealid"]).Id);
                        }

                        if (donorCommitmentRecord.Contains("msnfp_packageid"))
                        {
                            localContext.TracingService.Trace("Package ID: " + ((EntityReference)donorCommitmentRecord["msnfp_packageid"]).Id.ToString());
                            newDonorCommitment["msnfp_packageid"] = new EntityReference("msnfp_package", ((EntityReference)donorCommitmentRecord["msnfp_packageid"]).Id);
                        }

                        if (donorCommitmentRecord.Contains("msnfp_designationid"))
                        {
                            localContext.TracingService.Trace("msnfp_designationid ID: " + ((EntityReference)donorCommitmentRecord["msnfp_designationid"]).Id.ToString());
                            newDonorCommitment["msnfp_designationid"] = new EntityReference("msnfp_designation", ((EntityReference)donorCommitmentRecord["msnfp_designationid"]).Id);
                        }

                        if (donorCommitmentRecord.Contains("msnfp_commitment_defaultdesignationid"))
                        {
                            localContext.TracingService.Trace("msnfp_commitment_defaultdesignationid ID: " + ((EntityReference)donorCommitmentRecord["msnfp_commitment_defaultdesignationid"]).Id.ToString());
                            newDonorCommitment["msnfp_commitment_defaultdesignationid"] = new EntityReference("msnfp_designation", ((EntityReference)donorCommitmentRecord["msnfp_commitment_defaultdesignationid"]).Id);
                        }

                        newDonorCommitment["statuscode"] = new OptionSetValue(1); // Active

                        // Add the constituent (the donor who triggered this action):
                        if (donorCommitmentRecord.Contains("msnfp_customerid"))
                        {
                            if (((EntityReference)donorCommitmentRecord["msnfp_customerid"]).LogicalName == "contact")
                            {
                                localContext.TracingService.Trace("Constituent is a contact: " + ((EntityReference)donorCommitmentRecord["msnfp_customerid"]).Id.ToString());
                                newDonorCommitment["msnfp_constituentid"] = new EntityReference("contact", ((EntityReference)donorCommitmentRecord["msnfp_customerid"]).Id);
                            }
                            else if (((EntityReference)donorCommitmentRecord["msnfp_customerid"]).LogicalName == "account")
                            {
                                localContext.TracingService.Trace("Constituent is an account: " + ((EntityReference)donorCommitmentRecord["msnfp_customerid"]).Id.ToString());
                                newDonorCommitment["msnfp_constituentid"] = new EntityReference("account", ((EntityReference)donorCommitmentRecord["msnfp_customerid"]).Id);
                            }
                        }

                        // Set the configuration record on the donor commitment:
                        if (donorCommitmentRecord.Contains("msnfp_configurationid"))
                        {
                            localContext.TracingService.Trace("Configuration record id: " + ((EntityReference)donorCommitmentRecord["msnfp_configurationid"]).Id.ToString());
                            newDonorCommitment["msnfp_configurationid"] = new EntityReference("msnfp_configuration", ((EntityReference)donorCommitmentRecord["msnfp_configurationid"]).Id);
                        }

                        localContext.TracingService.Trace("Creating new donor commitment.");
                        service.Create(newDonorCommitment);
                        localContext.TracingService.Trace("New donor commitment created.");
                    }
                    catch (Exception ex)
                    {
                        localContext.TracingService.Trace("Error creating pledge match donor commitment: " + ex.Message);
                    }
                }
                else
                {
                    localContext.TracingService.Trace("This pledge match is not for Transactions (844060001), msnfp_appliestocode: " + ((OptionSetValue)pledgeMatchRecord["msnfp_appliestocode"]).Value.ToString());
                }
            }
            else
            {
                localContext.TracingService.Trace("No msnfp_appliestocode value found.");
            }
            localContext.TracingService.Trace("---------Exiting AddPledgeFromPledgeMatchRecord()---------");

        }
        #endregion

        private static void CreateDesignationRecords(LocalPluginContext localContext, IOrganizationService service, Entity commitment)
        {
            localContext.TracingService.Trace("Creating Designated Credit");
            EntityReference primaryDesignationRef = commitment.GetAttributeValue<EntityReference>("msnfp_commitment_defaultdesignationid");
            Entity primaryDesignation = service.Retrieve(primaryDesignationRef.LogicalName, primaryDesignationRef.Id, new ColumnSet("msnfp_name"));
            Money amount = commitment.GetAttributeValue<Money>("msnfp_totalamount");
            DateTime bookDate = commitment.GetAttributeValue<DateTime>("msnfp_bookdate");
            DateTime receivedDate = commitment.GetAttributeValue<DateTime>("msnfp_receiveddate");

            Entity designatedCredit = new Entity("msnfp_designatedcredit");
            designatedCredit["msnfp_donorcommitmentid"] = commitment.ToEntityReference();
            designatedCredit["msnfp_designatiedcredit_designationid"] = primaryDesignationRef;

            if (amount != null && amount.Value > 0)
            {
                designatedCredit["msnfp_name"] = primaryDesignation.GetAttributeValue<string>("msnfp_name") + "-$" + amount.Value;
                designatedCredit["msnfp_amount"] = amount;
            }
            else
                designatedCredit["msnfp_name"] = primaryDesignation.GetAttributeValue<string>("msnfp_name");

            if (bookDate != default)
                designatedCredit["msnfp_bookdate"] = bookDate;

            if (receivedDate != default)
                designatedCredit["msnfp_receiveddate"] = receivedDate;

            service.Create(designatedCredit);
            localContext.TracingService.Trace("Created Designated Credit");

            localContext.TracingService.Trace("Created Designation Plan");
            var designationplan = new Entity("msnfp_designationplan");
            designationplan["msnfp_designationplan_campaignid"] = commitment.GetAttributeValue<EntityReference>("msnfp_commitment_campaignid");
            designationplan["msnfp_customerid"] = commitment["msnfp_customerid"];
            designationplan["msnfp_designationplan_donorcommitmentid"] = commitment.ToEntityReference();
            designationplan["msnfp_designationplan_designationid"] = primaryDesignationRef;

            if (amount.Value > 0)
            {
                designationplan["msnfp_name"] = primaryDesignation.GetAttributeValue<string>("msnfp_name") + "-$" + amount.Value;
                designationplan["msnfp_amountofpledgemax"] = amount;
            }
            else
                designationplan["msnfp_name"] = primaryDesignation.GetAttributeValue<string>("msnfp_name");
            if (bookDate != default)
                designationplan["msnfp_bookdate"] = bookDate;

            designationplan["msnfp_designationplan_paymentscheduleid"] = commitment.GetAttributeValue<EntityReference>("msnfp_parentscheduleid");

            service.Create(designationplan);
            localContext.TracingService.Trace("Created Designation Plan");
        }

        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, string messageName)
        {
            tracingService.Trace("---------Send the Record to Azure---------");

            string entityName = "DonorCommitment"; // Used for API calls

            string apiUrl = configurationRecord.GetAttributeValue<string>("msnfp_azure_webapiurl");

            tracingService.Trace("Got API URL: " + apiUrl);

            if (!string.IsNullOrEmpty(apiUrl))
            {

                tracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord.Id.ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_DonorCommitment jsonDataObj = new MSNFP_DonorCommitment();

                jsonDataObj.DonorCommitmentId = queriedEntityRecord.Id;

                // Now we get all the fields for this entity and save them to a JSON object.

                if (queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_appealid") != null)
                {
                    jsonDataObj.AppealId = queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_appealid").Id;
                    tracingService.Trace("Got msnfp_appealid.");
                }
                else
                {
                    jsonDataObj.AppealId = null;
                    tracingService.Trace("Did NOT find msnfp_appealid.");
                }

                if (queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_packageid") != null)
                {
                    jsonDataObj.PackageId = queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_packageid").Id;
                    tracingService.Trace("Got msnfp_packageid.");
                }
                else
                {
                    jsonDataObj.PackageId = null;
                    tracingService.Trace("Did NOT find msnfp_packageid.");
                }

                if (queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_commitment_campaignid") != null)
                {
                    jsonDataObj.CampaignId = queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_commitment_campaignid").Id;
                    tracingService.Trace("Got msnfp_commitment_campaignid.");
                }
                else
                {
                    jsonDataObj.CampaignId = null;
                    tracingService.Trace("Did NOT find msnfp_commitment_campaignid.");
                }

                if (queriedEntityRecord.GetAttributeValue<Money>("msnfp_totalamount") != null)
                {
                    jsonDataObj.TotalAmount = queriedEntityRecord.GetAttributeValue<Money>("msnfp_totalamount").Value;
                    tracingService.Trace("Got msnfp_totalamount.");
                }
                else
                {
                    jsonDataObj.TotalAmount = null;
                    tracingService.Trace("Did NOT find msnfp_totalamount.");
                }

                if (queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_customerid") != null)
                {
                    jsonDataObj.CustomerId = queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_customerid").Id;
                    if(queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_customerid").LogicalName == "contact")
                    {
                        jsonDataObj.CustomerIdType = 2;
                    }
                    else if (queriedEntityRecord.GetAttributeValue<EntityReference>("msnfp_customerid").LogicalName == "account")
                    {
                        jsonDataObj.CustomerIdType = 1;
                    }
                    tracingService.Trace("Got msnfp_customerid.");
                }
                else
                {
                    jsonDataObj.CustomerId = null;
                    jsonDataObj.CustomerIdType = null;
                    tracingService.Trace("Did NOT find msnfp_customerid.");
                }

                if (queriedEntityRecord.GetAttributeValue<DateTime?>("msnfp_bookdate") != null)
                {
                    jsonDataObj.BookDate = queriedEntityRecord.GetAttributeValue<DateTime>("msnfp_bookdate");
                    tracingService.Trace("Got msnfp_bookdate.");
                }
                else
                {
                    jsonDataObj.BookDate = null;
                    tracingService.Trace("Did NOT find msnfp_bookdate.");
                }

                if (queriedEntityRecord.GetAttributeValue<Money>("msnfp_totalamount_balance") != null)
                {
                    jsonDataObj.TotalAmountBalance = queriedEntityRecord.GetAttributeValue<Money>("msnfp_totalamount_balance").Value;
                    tracingService.Trace("Got msnfp_totalamount_balance.");
                }
                else
                {
                    jsonDataObj.TotalAmountBalance = null;
                    tracingService.Trace("Did NOT find msnfp_totalamount_balance.");
                }


                if (messageName == "Create")
                {
                    jsonDataObj.CreatedOn = DateTime.UtcNow;
                }
                else if (queriedEntityRecord.Contains("createdon") && queriedEntityRecord["createdon"] != null)
                {
                    jsonDataObj.CreatedOn = (DateTime)queriedEntityRecord["createdon"];
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

                if (queriedEntityRecord.Contains("statecode") && queriedEntityRecord["statecode"] != null)
                {
                    jsonDataObj.StateCode = ((OptionSetValue)queriedEntityRecord["statecode"]).Value;
                    tracingService.Trace("Got statecode.");
                }
                else
                {
                    jsonDataObj.StateCode = null;
                    tracingService.Trace("Did NOT find statecode.");
                }

                if (queriedEntityRecord.Contains("statuscode") && queriedEntityRecord["statuscode"] != null)
                {
                    jsonDataObj.StatusCode = ((OptionSetValue)queriedEntityRecord["statuscode"]).Value;
                    tracingService.Trace("Got statuscode.");
                }
                else
                {
                    jsonDataObj.StatusCode = null;
                    tracingService.Trace("Did NOT find statuscode.");
                }

                tracingService.Trace("JSON object created");

                if (messageName == "Create")
                {
                    apiUrl += entityName + "/Create" + entityName;
                }
                else if (messageName == "Update" || messageName == "Delete")
                {
                    apiUrl += entityName + "/Update" + entityName;
                }

                MemoryStream ms = new MemoryStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_DonorCommitment));
                ser.WriteObject(ms, jsonDataObj);
                byte[] json = ms.ToArray();
                ms.Close();

                var jsonBody = Encoding.UTF8.GetString(json, 0, json.Length);

                WebAPIClient client = new WebAPIClient();
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers["Padlock"] = (string)configurationRecord["msnfp_apipadlocktoken"];
                client.Encoding = UTF8Encoding.UTF8;

                tracingService.Trace("---------Preparing JSON---------");
                tracingService.Trace("Converted to json API URL : " + apiUrl);
                tracingService.Trace("JSON: " + jsonBody);
                tracingService.Trace("---------End of Preparing JSON---------");
                tracingService.Trace("Sending data to Azure.");

                string fileContent = client.UploadString(apiUrl, jsonBody);

                tracingService.Trace("Got response.");
                tracingService.Trace("Response: " + fileContent);


                // Here we display an error if we do not get a 200 OK from the API:
                Utilities util = new Utilities();
                util.CheckAPIReturnJSONForErrors(fileContent, configurationRecord.GetAttributeValue<OptionSetValue>("msnfp_showapierrorresponses"), tracingService);
            }
            else
            {
                tracingService.Trace("No API URL or Enable Portal Pages. Exiting workflow.");
            }
        }
        #endregion
    }
}
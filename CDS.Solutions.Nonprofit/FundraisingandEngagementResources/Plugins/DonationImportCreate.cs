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
using Microsoft.Crm.Sdk.Messages;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class DonationImportCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public DonationImportCreate(string unsecure, string secure)
            : base(typeof(DonationImportCreate))
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
            localContext.TracingService.Trace("---------Triggered DonationImportCreate.cs ---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            // Note target is used in Azure sync.
            Entity targetIncomingRecord;
            string messageName = context.MessageName;


            localContext.TracingService.Trace("messageName: " + messageName);


            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            Entity configurationRecord = Utilities.GetConfigurationRecordByMessageName(context, service, localContext.TracingService);

            // Note this is broken out as "context.InputParameters["Target"] is Entity" doesn't work for delete plugin calls:
            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering DonationImportCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    //---------------- Start of Original DonationImportCreate.cs

                    Entity donationImport = (Entity)context.InputParameters["Target"];

                    if (donationImport != null)
                    {
                        if (donationImport.Contains("msnfp_statusupdated") && !donationImport.Contains("msnfp_transactionid"))
                        {
                            localContext.TracingService.Trace("Contains msnfp_statusupdated.");

                            if (messageName == "Update")
                            {
                                //ColumnSet cols;
                                //ColumnSet cols = new ColumnSet("msnfp_statusupdated", "msnfp_donationimportid", "createdon", "statuscode", "msnfp_transactionid", "msnfp_amount_receiptable", "msnfp_amount_membership",
                                //    "msnfp_amount_nonreceiptable", "msnfp_amount_tax", "msnfp_amount", "msnfp_anonymous", "msnfp_appealid", "msnfp_originatingcampaignid", "msnfp_ccbrandcode", "msnfp_chequewiredate",
                                //    "msnfp_chequenumber", "msnfp_configurationid", "msnfp_createdonor", "msnfp_customerid", "msnfp_billing_city", "msnfp_billing_country", "msnfp_emailaddress1", "msnfp_organizationname", "msnfp_firstname",
                                //    "msnfp_lastname", "msnfp_gender", "msnfp_billing_line1", "msnfp_billing_line2", "msnfp_billing_line3", "msnfp_middlename", "msnfp_telephone1", "msnfp_salutation", "msnfp_billing_stateorprovince",
                                //    "msnfp_billing_postalcode", "msnfp_donotemail", "msnfp_donotphone", "msnfp_donotbulkemail", "msnfp_donotpostalmail", "msnfp_donotsendmm", "msnfp_birthdate", "msnfp_createconstituent",
                                //    "msnfp_constituentid", "msnfp_constituent_billing_city", "msnfp_constituent_billing_country", "msnfp_constituent_emailaddress1", "msnfp_constituent_firstname", "msnfp_constituent_lastname",
                                //    "msnfp_constituent_billing_line1", "msnfp_constituent_billing_line2", "msnfp_constituent_billing_line3", "msnfp_constituent_middlename", "msnfp_constituent_telephone1", "msnfp_constituent_salutation",
                                //    "msnfp_constituent_billing_stateorprovince", "msnfp_constituent_billing_postalcode", "msnfp_constituent_donotemail", "msnfp_constituent_donotphone", "msnfp_constituent_donotbulkemail",
                                //    "msnfp_constituent_donotpostalmail", "msnfp_constituent_donotsendmm", "msnfp_bookdate", "msnfp_ga_deliverycode", "msnfp_receiveddate", "msnfp_donationimportid", "msnfp_donortype", "msnfp_eventid",
                                //    "msnfp_transactionidentifier", "msnfp_thirdpartyreceiptidentifier", "msnfp_designationid", "msnfp_ga_applicablecode", "msnfp_paymenttypecode", "msnfp_importresult", "msnfp_packageid", "msnfp_teamownerid",
                                //    "msnfp_dataentrysource", "msnfp_dataentryreference", "msnfp_thirdpartyreceipt", "msnfp_identifier", "msnfp_tributecode", "msnfp_tributeacknowledgement", "msnfp_tributeid", "msnfp_tributemessage", "msnfp_type", "msnfp_receiptpreferencecode",
                                //    "msnfp_householdid", "msnfp_householdrelationship", "msnfp_createhousehold");
                                //donationImport = service.Retrieve("msnfp_donationimport", donationImport.Id, cols);

                                donationImport = context.PostEntityImages["postImage"];
                            }


                            if (((OptionSetValue)donationImport["msnfp_statusupdated"]).Value == (int)Utilities.DonationImportStatusReason.Ready)  // status code: Ready
                            {
                                localContext.TracingService.Trace("Checking Import Status..");

                                // If import status is failed
                                // Set Status Updated to failed
                                // Stop processing
                                if (donationImport.GetAttributeValue<OptionSetValue>("msnfp_importresult") != null
                                    && donationImport.GetAttributeValue<OptionSetValue>("msnfp_importresult").Value == 844060001)
                                {
                                    service.Update(new Entity(donationImport.LogicalName, donationImport.Id)
                                    {
                                        Attributes = {
                                            new KeyValuePair<string, object>("msnfp_statusupdated",new OptionSetValue((int)Utilities.DonationImportStatusReason.Failed))
                                        }
                                    });

                                    return;
                                }

                                localContext.TracingService.Trace("Status Ready - processing donation import.");


                                try
                                {

                                    // creating / searching donor record - start

                                    Entity organization = null;
                                    Guid organizationID = Guid.Empty;
                                    bool donorExistYN = false;
                                    bool constituentExistYN = false;
                                    Entity customer = null;
                                    Entity constituent = null;
                                    string customerType = string.Empty;
                                    Guid customerId = Guid.Empty;
                                    Guid constituentId = Guid.Empty;
                                    ColumnSet contactCols = null;
                                    bool isCreateContact = donationImport.Contains("msnfp_createdonor") ? (bool)donationImport["msnfp_createdonor"] : false;
                                    string owneridType = string.Empty;
                                    Guid ownerId = Guid.Empty;
                                    OptionSetValue anonymous = donationImport.GetAttributeValue<OptionSetValue>("msnfp_anonymous");
                                    bool marketingMaterials = donationImport.Contains("msnfp_donotsendmm") ? (bool)donationImport["msnfp_donotsendmm"] : false;
                                    bool constituentMarketingMaterials = donationImport.Contains("msnfp_constituent_donotsendmm") ? (bool)donationImport["msnfp_constituent_donotsendmm"] : false;

                                    // setting owner default values 
                                    if (donationImport.Contains("msnfp_teamownerid") && donationImport["msnfp_teamownerid"] != null)
                                    {
                                        owneridType = ((EntityReference)donationImport["msnfp_teamownerid"]).LogicalName;
                                        ownerId = ((EntityReference)donationImport["msnfp_teamownerid"]).Id;
                                    }
                                    else
                                    {
                                        owneridType = ((EntityReference)donationImport["ownerid"]).LogicalName;
                                        ownerId = ((EntityReference)donationImport["ownerid"]).Id;
                                    }

                                    // Start household related processing
                                    KeyValuePair<string, object> houseHoldFromProcess = InitiateHouseholdProcess(service, donationImport, customer != null ? customer.ToEntityReference() : null);
                                    if (houseHoldFromProcess.Key != "msnfp_householdid")
                                    {
                                        localContext.Trace($"Added shared variable..");
                                        localContext.PluginExecutionContext.SharedVariables.Add("DonationImport", donationImport);
                                    }
                                    else
                                    {
                                        donationImport["msnfp_householdid"] = (EntityReference)houseHoldFromProcess.Value;
                                    }


                                    if (donationImport.Contains("msnfp_customerid"))
                                    {
                                        localContext.TracingService.Trace("Donation Import contains Customer");

                                        customerType = ((EntityReference)donationImport["msnfp_customerid"]).LogicalName;
                                        customerId = ((EntityReference)donationImport["msnfp_customerid"]).Id;

                                        if (customerType == "account")
                                        {
                                            customer = service.Retrieve(customerType, customerId, new ColumnSet("accountid", "name"));
                                        }
                                        else if (customerType == "contact")
                                        {
                                            contactCols = new ColumnSet(new string[] { "contactid", "salutation", "lastname", "fullname", "middlename", "firstname", "birthdate", "emailaddress1", "telephone1", "mobilephone", "gendercode", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode" });
                                            customer = service.Retrieve(customerType, customerId, contactCols);

                                            if (donationImport.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") != null)
                                                service.Update(new Entity(customer.LogicalName, customer.Id)
                                                {
                                                    Attributes =
                                                    {
                                                        new KeyValuePair<string, object>("msnfp_householdrelationship",donationImport.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship"))
                                                    }
                                                });
                                        }

                                        localContext.TracingService.Trace("Update Donation Import with Donor.");
                                    }
                                    else
                                    {
                                        localContext.TracingService.Trace("Donation Import does not have customer.");

                                        string dOrganizationName = donationImport.Contains("msnfp_organizationname") ? (string)donationImport["msnfp_organizationname"] : string.Empty;
                                        string dImportEmail = donationImport.Contains("msnfp_emailaddress1") ? (string)donationImport["msnfp_emailaddress1"] : string.Empty;
                                        string dImportFirstName = donationImport.Contains("msnfp_firstname") ? (string)donationImport["msnfp_firstname"] : string.Empty;
                                        string dImportlastName = donationImport.Contains("msnfp_lastname") ? (string)donationImport["msnfp_lastname"] : string.Empty;
                                        string dImportPostalcode = donationImport.Contains("msnfp_billing_postalcode") ? (string)donationImport["msnfp_billing_postalcode"] : string.Empty;
                                        string dImportCity = donationImport.Contains("msnfp_billing_city") ? (string)donationImport["msnfp_billing_city"] : string.Empty;
                                        string dImportLine1 = donationImport.Contains("msnfp_billing_line1") ? (string)donationImport["msnfp_billing_line1"] : string.Empty;

                                        // Account validation start
                                        localContext.TracingService.Trace("Validating Organization Name.");

                                        if (!string.IsNullOrEmpty(dOrganizationName))
                                        {
                                            localContext.TracingService.Trace("Organization Name: " + dOrganizationName + ".");

                                            ColumnSet accountCol1List = new ColumnSet("name");
                                            List<Entity> accountCount = new List<Entity>();
                                            QueryExpression accountQuery = new QueryExpression("account");
                                            accountQuery.ColumnSet = accountCol1List;
                                            accountQuery.Criteria = new FilterExpression();
                                            accountQuery.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
                                            accountQuery.Criteria.FilterOperator = LogicalOperator.And;

                                            FilterExpression aFilter = accountQuery.Criteria.AddFilter(LogicalOperator.And);
                                            aFilter.AddCondition("name", ConditionOperator.Equal, dOrganizationName);
                                            accountCount = service.RetrieveMultiple(accountQuery).Entities.ToList();

                                            if (accountCount.Count > 0)
                                            {
                                                localContext.TracingService.Trace("Account found.");
                                                organization = accountCount.FirstOrDefault();

                                                // Setting Donor
                                                donationImport["msnfp_customerid"] = new EntityReference("account", organization.Id);
                                                donorExistYN = true;
                                            }
                                            else
                                            {
                                                if (isCreateContact)
                                                {
                                                    localContext.TracingService.Trace("No account found, creating new record.");

                                                    // Creating new account record - start
                                                    organization = new Entity("account");
                                                    organization["name"] = dOrganizationName;

                                                    // setting marketing values
                                                    organization["donotbulkemail"] = donationImport.Contains("msnfp_donotbulkemail") ? (bool)donationImport["msnfp_donotbulkemail"] : false;
                                                    organization["donotemail"] = donationImport.Contains("msnfp_donotemail") ? (bool)donationImport["msnfp_donotemail"] : false;
                                                    organization["donotphone"] = donationImport.Contains("msnfp_donotphone") ? (bool)donationImport["msnfp_donotphone"] : false;
                                                    organization["donotpostalmail"] = donationImport.Contains("msnfp_donotpostalmail") ? (bool)donationImport["msnfp_donotpostalmail"] : false;

                                                    if (marketingMaterials)
                                                        organization["donotbulkpostalmail"] = false;
                                                    else
                                                        organization["donotbulkpostalmail"] = true;

                                                    // setting address type as primary
                                                    organization["address1_addresstypecode"] = new OptionSetValue(3);

                                                    // mark donation as anonymous
                                                    organization["msnfp_anonymity"] = anonymous;

                                                    // receipt preference
                                                    if (donationImport.Contains("msnfp_receiptpreferencecode"))
                                                        organization["msnfp_receiptpreferencecode"] = (OptionSetValue)donationImport["msnfp_receiptpreferencecode"];

                                                    // assigning owner id
                                                    if (owneridType != string.Empty && ownerId != Guid.Empty)
                                                    {
                                                        localContext.TracingService.Trace("Assigning Owner");
                                                        organization["ownerid"] = new EntityReference(owneridType, ownerId);
                                                    }

                                                    organizationID = service.Create(organization);
                                                    ExecuteWorkflowRequest executeWorkflowRequest = new ExecuteWorkflowRequest();
                                                    executeWorkflowRequest.EntityId = organizationID;
                                                    executeWorkflowRequest.WorkflowId = Guid.Parse("810C634A-2F4C-45B7-BFCC-C4FAAE315970");

                                                    service.Execute(executeWorkflowRequest);
                                                    localContext.TracingService.Trace("Account created and giving level calculated..");

                                                    localContext.TracingService.Trace("Account created and set as Donor.");

                                                    if (organizationID != Guid.Empty)
                                                    {
                                                        donationImport["msnfp_customerid"] = new EntityReference("account", organizationID);
                                                        donorExistYN = true;
                                                    }
                                                    // Creating new account record - end
                                                }
                                            }
                                        }
                                        // Account validation end
                                        localContext.TracingService.Trace("Account validation completed.");


                                        ColumnSet col1List = new ColumnSet("contactid", "firstname", "lastname", "middlename", "firstname", "birthdate", "emailaddress1", "emailaddress2", "emailaddress3", "telephone1", "mobilephone", "gendercode", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "msnfp_householdid", "msnfp_householdrelationship");

                                        List<Entity> ecount = new List<Entity>();
                                        QueryExpression quer = new QueryExpression("contact");
                                        quer.ColumnSet = col1List;
                                        quer.Criteria = new FilterExpression();
                                        quer.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
                                        quer.Criteria.FilterOperator = LogicalOperator.And;

                                        if (!string.IsNullOrEmpty(dImportEmail) && !string.IsNullOrEmpty(dImportFirstName) && !string.IsNullOrEmpty(dImportlastName))
                                        {
                                            FilterExpression childFilter = quer.Criteria.AddFilter(LogicalOperator.Or);
                                            childFilter.AddCondition("emailaddress1", ConditionOperator.Equal, dImportEmail);
                                            childFilter.AddCondition("emailaddress2", ConditionOperator.Equal, dImportEmail);
                                            childFilter.AddCondition("emailaddress3", ConditionOperator.Equal, dImportEmail);

                                            FilterExpression filter = quer.Criteria.AddFilter(LogicalOperator.And);
                                            filter.AddCondition("firstname", ConditionOperator.BeginsWith, dImportFirstName.Substring(0, 1));
                                            filter.AddCondition("lastname", ConditionOperator.BeginsWith, dImportlastName);

                                            ecount = service.RetrieveMultiple(quer).Entities.ToList();
                                        }

                                        if (ecount.Count > 0)
                                        {
                                            localContext.TracingService.Trace("customer by email found.");

                                            customer = ecount.FirstOrDefault();
                                        }
                                        else
                                        {
                                            localContext.TracingService.Trace("No customer found by email.");

                                            ecount = new List<Entity>();
                                            FilterExpression filter2 = new FilterExpression();
                                            if (!string.IsNullOrEmpty(dImportlastName) && !string.IsNullOrEmpty(dImportPostalcode) && !string.IsNullOrEmpty(dImportCity)
                                                && !string.IsNullOrEmpty(dImportFirstName) && !string.IsNullOrEmpty(dImportLine1))
                                            {
                                                filter2.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, dImportlastName));
                                                filter2.Conditions.Add(new ConditionExpression("address1_postalcode", ConditionOperator.Equal, dImportPostalcode));
                                                filter2.Conditions.Add(new ConditionExpression("address1_city", ConditionOperator.Equal, dImportCity));
                                                filter2.Conditions.Add(new ConditionExpression("address1_line1", ConditionOperator.Equal, dImportLine1));
                                                filter2.Conditions.Add(new ConditionExpression("firstname", ConditionOperator.BeginsWith, dImportFirstName.Substring(0, 1)));
                                                filter2.Conditions.Add(new ConditionExpression("statecode", ConditionOperator.Equal, 0));
                                                filter2.FilterOperator = LogicalOperator.And;

                                                quer.Criteria = filter2;

                                                ecount = service.RetrieveMultiple(quer).Entities.ToList();
                                            }

                                            if (ecount.Count > 0)
                                            {
                                                customer = ecount.FirstOrDefault();
                                            }
                                            else
                                            {
                                                ecount = new List<Entity>();
                                                FilterExpression filter3 = new FilterExpression();
                                                if (!string.IsNullOrEmpty(dImportlastName) && !string.IsNullOrEmpty(dImportLine1) && !string.IsNullOrEmpty(dImportPostalcode)
                                                    && !string.IsNullOrEmpty(dImportFirstName))
                                                {
                                                    filter3.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, dImportlastName));
                                                    filter3.Conditions.Add(new ConditionExpression("address1_line1", ConditionOperator.Equal, dImportLine1));
                                                    filter3.Conditions.Add(new ConditionExpression("address1_postalcode", ConditionOperator.Equal, dImportPostalcode));
                                                    filter3.Conditions.Add(new ConditionExpression("firstname", ConditionOperator.BeginsWith, dImportFirstName.Substring(0, 1)));
                                                    filter3.Conditions.Add(new ConditionExpression("statecode", ConditionOperator.Equal, 0));
                                                    filter3.FilterOperator = LogicalOperator.And;

                                                    quer.Criteria = filter3;

                                                    ecount = service.RetrieveMultiple(quer).Entities.ToList();
                                                }

                                                if (ecount.Count > 0)
                                                {
                                                    customer = ecount.FirstOrDefault();
                                                }
                                            }
                                        }




                                        localContext.Trace($"Shared variable :{localContext.PluginExecutionContext.SharedVariables.Count}");

                                        if (customer != null)
                                        {
                                            localContext.TracingService.Trace("found customer based on search criteria and set to Customer System");

                                            if (donorExistYN)
                                            {
                                                donationImport["msnfp_constituentid"] = new EntityReference(customer.LogicalName, customer.Id);
                                                constituentExistYN = true;
                                            }
                                            else
                                            {
                                                donationImport["msnfp_customerid"] = new EntityReference(customer.LogicalName, customer.Id);
                                            }


                                            // Set household information on this customer
                                            if (customer.GetAttributeValue<EntityReference>("msnfp_householdid") != null)
                                            {
                                                Entity updateContactHousehold = new Entity(customer.LogicalName, customer.Id);

                                                if (houseHoldFromProcess.Key == "msnfp_householdid")
                                                    updateContactHousehold["msnfp_householdid"] = (EntityReference)houseHoldFromProcess.Value;
                                                if (donationImport.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") != null)
                                                    updateContactHousehold["msnfp_householdrelationship"] = donationImport.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship");

                                                service.Update(updateContactHousehold);
                                            }
                                        }
                                        else
                                        {
                                            localContext.TracingService.Trace("No record found, going to create Contact");

                                            string firstName = donationImport.Contains("msnfp_firstname") ? (string)donationImport["msnfp_firstname"] : string.Empty;
                                            string lastName = donationImport.Contains("msnfp_lastname") ? (string)donationImport["msnfp_lastname"] : string.Empty;

                                            if (donationImport.Contains("msnfp_createdonor") && isCreateContact && !string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
                                            {
                                                //Create Contact
                                                customer = new Entity("contact");

                                                customer["msnfp_householdrelationship"] = donationImport.Attributes.ContainsKey("msnfp_householdrelationship") ? donationImport.GetAttributeValue<OptionSetValue>("msnfp_householdrelationship") : null;
                                                customer["msnfp_householdid"] = houseHoldFromProcess.Key == "msnfp_householdid" ? (EntityReference)houseHoldFromProcess.Value : null;

                                                customer["salutation"] = donationImport.Contains("msnfp_salutation") ? (string)donationImport["msnfp_salutation"] : string.Empty;
                                                customer["lastname"] = donationImport.Contains("msnfp_lastname") ? (string)donationImport["msnfp_lastname"] : string.Empty;
                                                customer["middlename"] = donationImport.Contains("msnfp_middlename") ? (string)donationImport["msnfp_middlename"] : string.Empty;
                                                customer["firstname"] = donationImport.Contains("msnfp_firstname") ? (string)donationImport["msnfp_firstname"] : string.Empty;

                                                if (donationImport.Contains("msnfp_birthdate"))
                                                    customer["birthdate"] = (DateTime)donationImport["msnfp_birthdate"];

                                                customer["emailaddress1"] = donationImport.Contains("msnfp_emailaddress1") ? (string)donationImport["msnfp_emailaddress1"] : string.Empty;
                                                customer["telephone1"] = donationImport.Contains("msnfp_telephone1") ? (string)donationImport["msnfp_telephone1"] : string.Empty;
                                                customer["mobilephone"] = donationImport.Contains("msnfp_mobilephone") ? (string)donationImport["msnfp_mobilephone"] : string.Empty;
                                                customer["address1_line1"] = donationImport.Contains("msnfp_billing_line1") ? (string)donationImport["msnfp_billing_line1"] : string.Empty;
                                                customer["address1_line2"] = donationImport.Contains("msnfp_billing_line2") ? (string)donationImport["msnfp_billing_line2"] : string.Empty;
                                                customer["address1_line3"] = donationImport.Contains("msnfp_billing_line3") ? (string)donationImport["msnfp_billing_line3"] : string.Empty;
                                                customer["address1_city"] = donationImport.Contains("msnfp_billing_city") ? (string)donationImport["msnfp_billing_city"] : string.Empty;
                                                customer["address1_stateorprovince"] = donationImport.Contains("msnfp_billing_stateorprovince") ? (string)donationImport["msnfp_billing_stateorprovince"] : string.Empty;
                                                customer["address1_country"] = donationImport.Contains("msnfp_billing_country") ? (string)donationImport["msnfp_billing_country"] : string.Empty;
                                                customer["address1_postalcode"] = donationImport.Contains("msnfp_billing_postalcode") ? (string)donationImport["msnfp_billing_postalcode"] : string.Empty;


                                                // setting marketing values
                                                customer["donotbulkemail"] = donationImport.Contains("msnfp_donotbulkemail") ? (bool)donationImport["msnfp_donotbulkemail"] : false;
                                                customer["donotemail"] = donationImport.Contains("msnfp_donotemail") ? (bool)donationImport["msnfp_donotemail"] : false;
                                                customer["donotphone"] = donationImport.Contains("msnfp_donotphone") ? (bool)donationImport["msnfp_donotphone"] : false;
                                                customer["donotpostalmail"] = donationImport.Contains("msnfp_donotpostalmail") ? (bool)donationImport["msnfp_donotpostalmail"] : false;

                                                if (marketingMaterials)
                                                    customer["donotbulkpostalmail"] = false;
                                                else
                                                    customer["donotbulkpostalmail"] = true;


                                                // setting address type as primary
                                                customer["address1_addresstypecode"] = new OptionSetValue((int)Utilities.ContactAddressTypeCode.Primary);

                                                // mark donation as anonymous
                                                customer["msnfp_anonymity"] = anonymous;

                                                // receipt preference
                                                if (donationImport.Contains("msnfp_receiptpreferencecode"))
                                                    customer["msnfp_receiptpreferencecode"] = (OptionSetValue)donationImport["msnfp_receiptpreferencecode"];

                                                // assigning owner id
                                                if (owneridType != string.Empty && ownerId != Guid.Empty)
                                                    customer["ownerid"] = new EntityReference(owneridType, ownerId);

                                                Guid newContactID = service.Create(customer);

                                                // Call workflow
                                                ExecuteWorkflowRequest executeWorkflowRequest = new ExecuteWorkflowRequest();
                                                executeWorkflowRequest.EntityId = newContactID;
                                                executeWorkflowRequest.WorkflowId = Guid.Parse("EAAE076C-DB57-4979-A479-CC17B83CE705");

                                                service.Execute(executeWorkflowRequest);
                                                localContext.TracingService.Trace("Contact created and giving level calculated..");

                                                localContext.TracingService.Trace("Contact created and set to Donor");

                                                if (donorExistYN)
                                                {
                                                    donationImport["msnfp_constituentid"] = new EntityReference("contact", newContactID);
                                                    constituentExistYN = true;
                                                }
                                                else
                                                {
                                                    donationImport["msnfp_customerid"] = new EntityReference("contact", newContactID);
                                                }
                                            }
                                        }

                                        localContext.TracingService.Trace("Donation Import updated");
                                    }

                                    // creating / searching donor record - end


                                    // creating / searching constituent record - start



                                    if (!donationImport.Contains("msnfp_constituentid") && !constituentExistYN)
                                    {
                                        localContext.TracingService.Trace("Donation Import does not have constituent");

                                        string dImportFirstName = donationImport.Contains("msnfp_constituent_firstname") ? (string)donationImport["msnfp_constituent_firstname"] : string.Empty;
                                        string dImportLastName = donationImport.Contains("msnfp_constituent_lastname") ? (string)donationImport["msnfp_constituent_lastname"] : string.Empty;

                                        ColumnSet colList = new ColumnSet("contactid", "lastname", "middlename", "firstname", "birthdate", "emailaddress1", "emailaddress2", "emailaddress3", "telephone1", "mobilephone", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode");

                                        List<Entity> ec = new List<Entity>();
                                        QueryExpression query = new QueryExpression("contact");
                                        query.ColumnSet = colList;
                                        query.Criteria = new FilterExpression();
                                        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

                                        if (!string.IsNullOrEmpty(dImportFirstName) && !string.IsNullOrEmpty(dImportLastName))
                                        {
                                            FilterExpression childFilter = new FilterExpression();
                                            childFilter.AddCondition("firstname", ConditionOperator.Equal, dImportFirstName);
                                            childFilter.AddCondition("lastname", ConditionOperator.Equal, dImportLastName);
                                            childFilter.FilterOperator = LogicalOperator.And;

                                            query.Criteria = childFilter;

                                            ec = service.RetrieveMultiple(query).Entities.ToList();

                                            if (ec.Count > 0)
                                            {
                                                localContext.TracingService.Trace("constituent found by first name and last name.");

                                                constituent = ec.FirstOrDefault();
                                            }

                                            if (constituent != null)
                                            {
                                                localContext.TracingService.Trace("found constituent based on search criteria and set to Constituent");

                                                donationImport["msnfp_constituentid"] = new EntityReference(constituent.LogicalName, constituent.Id);
                                            }
                                            else
                                            {
                                                localContext.TracingService.Trace("No constituent record found, going to create new Contact");

                                                if (donationImport.Contains("msnfp_createconstituent") && (bool)donationImport["msnfp_createconstituent"])
                                                {
                                                    //Create Contact
                                                    constituent = new Entity("contact");
                                                    constituent["salutation"] = donationImport.Contains("msnfp_constituent_salutation") ? (string)donationImport["msnfp_constituent_salutation"] : string.Empty;
                                                    constituent["lastname"] = donationImport.Contains("msnfp_constituent_lastname") ? (string)donationImport["msnfp_constituent_lastname"] : string.Empty;
                                                    constituent["middlename"] = donationImport.Contains("msnfp_constituent_middlename") ? (string)donationImport["msnfp_constituent_middlename"] : string.Empty;
                                                    constituent["firstname"] = donationImport.Contains("msnfp_constituent_firstname") ? (string)donationImport["msnfp_constituent_firstname"] : string.Empty;
                                                    constituent["emailaddress1"] = donationImport.Contains("msnfp_constituent_emailaddress1") ? (string)donationImport["msnfp_constituent_emailaddress1"] : string.Empty;
                                                    constituent["telephone1"] = donationImport.Contains("msnfp_constituent_telephone") ? (string)donationImport["msnfp_constituent_telephone"] : string.Empty;
                                                    constituent["mobilephone"] = donationImport.Contains("msnfp_constituent_mobilephone") ? (string)donationImport["msnfp_constituent_mobilephone"] : string.Empty;
                                                    constituent["address1_line1"] = donationImport.Contains("msnfp_constituent_billing_line1") ? (string)donationImport["msnfp_constituent_billing_line1"] : string.Empty;
                                                    constituent["address1_line2"] = donationImport.Contains("msnfp_constituent_billing_line2") ? (string)donationImport["msnfp_constituent_billing_line2"] : string.Empty;
                                                    constituent["address1_line3"] = donationImport.Contains("msnfp_constituent_billing_line3") ? (string)donationImport["msnfp_constituent_billing_line3"] : string.Empty;
                                                    constituent["address1_city"] = donationImport.Contains("msnfp_constituent_billing_city") ? (string)donationImport["msnfp_constituent_billing_city"] : string.Empty;
                                                    constituent["address1_stateorprovince"] = donationImport.Contains("msnfp_constituent_billing_stateorprovince") ? (string)donationImport["msnfp_constituent_billing_stateorprovince"] : string.Empty;
                                                    constituent["address1_country"] = donationImport.Contains("msnfp_constituent_billing_country") ? (string)donationImport["msnfp_constituent_billing_country"] : string.Empty;
                                                    constituent["address1_postalcode"] = donationImport.Contains("msnfp_constituent_billing_postalcode") ? (string)donationImport["msnfp_constituent_billing_postalcode"] : string.Empty;

                                                    // setting marketing values
                                                    constituent["donotbulkemail"] = donationImport.Contains("msnfp_constituent_donotbulkemail") ? (bool)donationImport["msnfp_constituent_donotbulkemail"] : false;
                                                    constituent["donotemail"] = donationImport.Contains("msnfp_constituent_donotemail") ? (bool)donationImport["msnfp_constituent_donotemail"] : false;
                                                    constituent["donotphone"] = donationImport.Contains("msnfp_constituent_donotphone") ? (bool)donationImport["msnfp_constituent_donotphone"] : false;
                                                    constituent["donotpostalmail"] = donationImport.Contains("msnfp_constituent_donotpostalmail") ? (bool)donationImport["msnfp_constituent_donotpostalmail"] : false;

                                                    if (constituentMarketingMaterials)
                                                        constituent["donotbulkpostalmail"] = false;
                                                    else
                                                        constituent["donotbulkpostalmail"] = true;

                                                    // setting address type as primary
                                                    constituent["address1_addresstypecode"] = new OptionSetValue((int)Utilities.ContactAddressTypeCode.Primary);

                                                    // mark donation as anonymous
                                                    constituent["msnfp_anonymity"] = anonymous;

                                                    // receipt preference
                                                    if (donationImport.Contains("msnfp_receiptpreferencecode"))
                                                        constituent["msnfp_receiptpreferencecode"] = (OptionSetValue)donationImport["msnfp_receiptpreferencecode"];

                                                    // assigning owner id
                                                    if (owneridType != string.Empty && ownerId != Guid.Empty)
                                                        constituent["ownerid"] = new EntityReference(owneridType, ownerId);

                                                    Guid newConstituentID = service.Create(constituent);

                                                    //contactCols = new ColumnSet(new string[] { "contactid", "salutation", "lastname", "middlename", "firstname", "birthdate", "emailaddress1", "telephone1", "mobilephone", "address1_line1", "address1_line2", "address1_line3", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode" });
                                                    //constituent = service.Retrieve("contact", newConstituentID, contactCols);

                                                    localContext.TracingService.Trace("constituent created and set to Constituent");

                                                    donationImport["msnfp_constituentid"] = new EntityReference("contact", newConstituentID);
                                                }
                                            }

                                            localContext.TracingService.Trace("Donation Import updated with Constituent");
                                        }
                                    }

                                    // creating / searching constituent record - end


                                    // creating single transaction record - start

                                    Entity gift = new Entity("msnfp_transaction");

                                    if (donationImport.Contains("msnfp_customerid"))
                                    {
                                        localContext.TracingService.Trace("Donation Import contains customer");

                                        string customerTypeTransaction = ((EntityReference)donationImport["msnfp_customerid"]).LogicalName;
                                        Guid customerIDTransaction = ((EntityReference)donationImport["msnfp_customerid"]).Id;
                                        gift["msnfp_customerid"] = new EntityReference(customerTypeTransaction, customerIDTransaction);
                                    }

                                    if (donationImport.Contains("msnfp_constituentid"))
                                    {
                                        localContext.TracingService.Trace("Donation Import contains constituent");

                                        string constituentTypeTransaction = ((EntityReference)donationImport["msnfp_constituentid"]).LogicalName;
                                        Guid constituentIDTransaction = ((EntityReference)donationImport["msnfp_constituentid"]).Id;
                                        gift["msnfp_relatedconstituentid"] = new EntityReference(constituentTypeTransaction, constituentIDTransaction);
                                    }

                                    if (donationImport.Contains("msnfp_amount_receiptable"))
                                        gift["msnfp_amount_receipted"] = (Money)donationImport["msnfp_amount_receiptable"];

                                    if (donationImport.Contains("msnfp_chequewiredate"))
                                        gift["msnfp_chequewiredate"] = (DateTime)donationImport["msnfp_chequewiredate"];

                                    if (donationImport.Contains("msnfp_bookdate"))
                                        gift["msnfp_bookdate"] = (DateTime)donationImport["msnfp_bookdate"];

                                    if (donationImport.Contains("msnfp_paymenttypecode"))
                                        gift["msnfp_paymenttypecode"] = (OptionSetValue)donationImport["msnfp_paymenttypecode"];

                                    if (donationImport.Contains("msnfp_dataentrysource"))
                                        gift["msnfp_dataentrysource"] = (OptionSetValue)donationImport["msnfp_dataentrysource"];

                                    if (donationImport.Contains("msnfp_amount_membership"))
                                        gift["msnfp_amount_membership"] = (Money)donationImport["msnfp_amount_membership"];

                                    if (donationImport.Contains("msnfp_amount_nonreceiptable"))
                                        gift["msnfp_amount_nonreceiptable"] = (Money)donationImport["msnfp_amount_nonreceiptable"];

                                    if (donationImport.Contains("msnfp_amount_tax"))
                                        gift["msnfp_amount_tax"] = (Money)donationImport["msnfp_amount_tax"];

                                    if (donationImport.Contains("msnfp_ccbrandcode"))
                                        gift["msnfp_ccbrandcode"] = (OptionSetValue)donationImport["msnfp_ccbrandcode"];

                                    if (donationImport.Contains("msnfp_appealid"))
                                    {
                                        string appealType = ((EntityReference)donationImport["msnfp_appealid"]).LogicalName;
                                        Guid appealID = ((EntityReference)donationImport["msnfp_appealid"]).Id;
                                        gift["msnfp_appealid"] = new EntityReference(appealType, appealID);
                                    }

                                    if (donationImport.Contains("msnfp_originatingcampaignid"))
                                    {
                                        localContext.TracingService.Trace("Donation Import contains campaign");

                                        string campaignType = ((EntityReference)donationImport["msnfp_originatingcampaignid"]).LogicalName;
                                        Guid campaignID = ((EntityReference)donationImport["msnfp_originatingcampaignid"]).Id;
                                        gift["msnfp_originatingcampaignid"] = new EntityReference(campaignType, campaignID);
                                    }

                                    if (donationImport.Contains("msnfp_amount"))
                                        gift["msnfp_amount"] = (Money)donationImport["msnfp_amount"];

                                    if (donationImport.Contains("msnfp_packageid"))
                                    {
                                        localContext.TracingService.Trace("Donation Import contains package");

                                        string packageType = ((EntityReference)donationImport["msnfp_packageid"]).LogicalName;
                                        Guid packageID = ((EntityReference)donationImport["msnfp_packageid"]).Id;
                                        gift["msnfp_packageid"] = new EntityReference(packageType, packageID);
                                    }

                                    if (donationImport.Contains("msnfp_designationid"))
                                    {
                                        localContext.TracingService.Trace("Donation Import contains fund");

                                        string designationType = ((EntityReference)donationImport["msnfp_designationid"]).LogicalName;
                                        Guid designationID = ((EntityReference)donationImport["msnfp_designationid"]).Id;
                                        gift["msnfp_designationid"] = new EntityReference(designationType, designationID);
                                    }

                                    gift["msnfp_transactionidentifier"] = donationImport.Contains("msnfp_transactionidentifier") ? (string)donationImport["msnfp_transactionidentifier"] : string.Empty;
                                    gift["msnfp_dataentryreference"] = donationImport.Contains("msnfp_dataentryreference") ? (string)donationImport["msnfp_dataentryreference"] : string.Empty;
                                    gift["msnfp_chequenumber"] = donationImport.Contains("msnfp_chequenumber") ? (string)donationImport["msnfp_chequenumber"] : string.Empty;


                                    if (donationImport.Contains("msnfp_receiveddate"))
                                        gift["msnfp_depositdate"] = (DateTime)donationImport["msnfp_receiveddate"];

                                    gift["msnfp_firstname"] = donationImport.Contains("msnfp_firstname") ? (string)donationImport["msnfp_firstname"] : string.Empty;
                                    gift["msnfp_lastname"] = donationImport.Contains("msnfp_lastname") ? (string)donationImport["msnfp_lastname"] : string.Empty;
                                    gift["msnfp_billing_line1"] = donationImport.Contains("msnfp_billing_line1") ? (string)donationImport["msnfp_billing_line1"] : string.Empty;
                                    gift["msnfp_billing_line2"] = donationImport.Contains("msnfp_billing_line2") ? (string)donationImport["msnfp_billing_line2"] : string.Empty;
                                    gift["msnfp_billing_line3"] = donationImport.Contains("msnfp_billing_line3") ? (string)donationImport["msnfp_billing_line3"] : string.Empty;
                                    gift["msnfp_billing_city"] = donationImport.Contains("msnfp_billing_city") ? (string)donationImport["msnfp_billing_city"] : string.Empty;
                                    gift["msnfp_billing_stateorprovince"] = donationImport.Contains("msnfp_billing_stateorprovince") ? (string)donationImport["msnfp_billing_stateorprovince"] : string.Empty;
                                    gift["msnfp_billing_postalcode"] = donationImport.Contains("msnfp_billing_postalcode") ? (string)donationImport["msnfp_billing_postalcode"] : string.Empty;
                                    gift["msnfp_billing_country"] = donationImport.Contains("msnfp_billing_country") ? (string)donationImport["msnfp_billing_country"] : string.Empty;
                                    gift["msnfp_emailaddress1"] = donationImport.Contains("msnfp_emailaddress1") ? (string)donationImport["msnfp_emailaddress1"] : string.Empty;
                                    gift["msnfp_telephone1"] = donationImport.Contains("msnfp_telephone1") ? (string)donationImport["msnfp_telephone1"] : string.Empty;
                                    gift["msnfp_anonymous"] = anonymous;

                                    // charge on create
                                    gift["msnfp_chargeoncreate"] = false;

                                    // assigning owner id
                                    if (owneridType != string.Empty && ownerId != Guid.Empty)
                                        gift["ownerid"] = new EntityReference(owneridType, ownerId);

                                    // setting status reason to completed
                                    gift["statuscode"] = new OptionSetValue(844060000);

                                    // set configuration
                                    gift["msnfp_configurationid"] = donationImport.GetAttributeValue<EntityReference>("msnfp_configurationid");

                                    Guid giftID = service.Create(gift);

                                    donationImport["msnfp_transactionid"] = new EntityReference("msnfp_transaction", giftID);

                                    // creating single transaction record - end


                                }
                                catch (Exception e)
                                {
                                    // catching exception, setting status code to Failed
                                    localContext.TracingService.Trace("error : " + e.Message);
                                    donationImport["msnfp_statusupdated"] = new OptionSetValue((int)Utilities.DonationImportStatusReason.Failed);
                                    localContext.TracingService.Trace("Status code updated to failed");
                                }

                                // updating donation import record
                                service.Update(donationImport);

                            }

                        }
                    }

                    //---------------- End of Original DonationImportCreate.cs

                }

                localContext.TracingService.Trace("---------Exiting DonationImportCreate.cs---------");
            }
        }

        /// <summary>
        /// Start the household process which looks at Household lookup , Household relationship type and Auto create household
        /// This will only add shared variables to the execution pipeline if a household is not found, since contact creation logic will take care of household setup
        /// If a household is present on donation import, contact when updated or created during this plugin itself will carry household value
        /// If a household was found in search, contact when updated or created during this plugin itself will carry household value
        /// </summary>
        /// <param name="donationImport">Donation import</param>
        private KeyValuePair<string, object> InitiateHouseholdProcess(IOrganizationService service, Entity donationImport, EntityReference contactRef)
        {
            // If donation import has household id, during creation of contact this value will be used
            if (donationImport.GetAttributeValue<EntityReference>("msnfp_householdid") != null)
            {
                return new KeyValuePair<string, object>("msnfp_householdid", donationImport.GetAttributeValue<EntityReference>("msnfp_householdid"));
            }
            else
            {
                // search for household
                EntityReference houseHold = Utilities.SearchHousehold(service, donationImport, contactRef);

                if (houseHold != null)
                    return new KeyValuePair<string, object>("msnfp_householdid", houseHold);
                else
                {
                    return new KeyValuePair<string, object>("DonationImport", (object)donationImport);
                }
            }
        }
    }
}

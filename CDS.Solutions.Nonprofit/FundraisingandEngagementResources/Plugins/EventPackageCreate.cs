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
    public class EventPackageCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public EventPackageCreate(string unsecure, string secure)
            : base(typeof(EventPackageCreate))
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
            localContext.TracingService.Trace("---------Triggered EventPackageCreate.cs---------");

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

            configurationRecord = Utilities.GetConfigurationRecordByMessageName(context, service, localContext.TracingService);

            if (context.InputParameters.Contains("Target"))
            {
                // update the asociated receipt (if there is one) 
                ReceiptUtilities.UpdateReceipt(context, service, localContext.TracingService);

                UpdateReceiptStatusReason(context, service, localContext.TracingService);

                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering EventPackageCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    // get all columns for sync to Azure
                    queriedEntityRecord = service.Retrieve("msnfp_eventpackage", targetIncomingRecord.Id, GetColumnSet());


                    if (targetIncomingRecord != null)
                    {

                        // validating msnfp_amount_balance
                        if (targetIncomingRecord.Contains("msnfp_amount_balance"))
                        {

                            decimal balance = ((Money)targetIncomingRecord["msnfp_amount_balance"]).Value;

                            if (balance == 0)
                            {
                                // Update related records status to paid.
                                updatedRelatedRecordsToPaid(targetIncomingRecord, localContext, service);
                            }
                        }

                        // Sync this to Azure. Note we use the target here as we want all the columns:
                        if (messageName == "Create")
                        {
                            if (configurationRecord != null)
                            {
                                Entity updateEventPackage = new Entity(targetIncomingRecord.LogicalName, targetIncomingRecord.Id);
                                updateEventPackage["msnfp_configurationid"] = configurationRecord.ToEntityReference();
                                service.Update(updateEventPackage);
                            }

                            AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext,
                                service, context);
                        }
                        else if (messageName == "Update")
                        {
                            // Updating Event Totals                            
                            UpdateEventTotals(queriedEntityRecord, orgSvcContext, service, localContext);

                            AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext,
                                service, context);
                        }

                        Utilities.UpdateHouseholdOnRecord(service, targetIncomingRecord, "msnfp_householdid", "msnfp_customerid");
                    }
                    else
                    {
                        localContext.TracingService.Trace("Target record not found. Exiting plugin.");
                    }


                    // Modify Last Event Package and Last Event Package Date fields
                    if (queriedEntityRecord != null && queriedEntityRecord.Contains("msnfp_customerid") && queriedEntityRecord["msnfp_customerid"] != null)
                    {
                        PopulateMostRecentEventPackageDataToDonor(service, orgSvcContext, localContext, queriedEntityRecord, messageName);
                    }
                }

                // Delete if the message is delete. This is the primary delete entrypoint:
                if (messageName == "Delete")
                {
                    // get all columns for sync to Azure
                    queriedEntityRecord = service.Retrieve("msnfp_eventpackage", ((EntityReference)context.InputParameters["Target"]).Id, GetColumnSet());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);

                    /// Modify Last Event Package and Last Event Package Date fields
                    if (queriedEntityRecord != null && queriedEntityRecord.Contains("msnfp_customerid") && queriedEntityRecord["msnfp_customerid"] != null)
                    {
                        PopulateMostRecentEventPackageDataToDonor(service, orgSvcContext, localContext, queriedEntityRecord, messageName);
                    }
                }

                if (queriedEntityRecord != null)
                {
                    _ = Common.Utilities.CallYearlyGivingServiceAsync(queriedEntityRecord.Id,
                        queriedEntityRecord.LogicalName, configurationRecord.Id, service, localContext.TracingService);
                }

                localContext.TracingService.Trace("---------Exiting EventPackageCreate.cs---------");
            }
        }

        private static ColumnSet GetColumnSet()
        {
            return new ColumnSet("msnfp_eventpackageid", "msnfp_amount_receipted", "msnfp_amount_nonreceiptable", "msnfp_amount_tax", "msnfp_amount", "msnfp_ref_amount_receipted", "msnfp_ref_amount_nonreceiptable", "msnfp_ref_amount_tax", "msnfp_ref_amount", "msnfp_firstname", "msnfp_lastname", "msnfp_emailaddress1", "msnfp_telephone1", "msnfp_telephone2", "msnfp_billing_city", "msnfp_billing_country", "msnfp_billing_line1", "msnfp_billing_line2", "msnfp_billing_line3", "msnfp_billing_postalcode", "msnfp_billing_stateorprovince", "msnfp_campaignid", "msnfp_packageid", "msnfp_appealid", "msnfp_eventid", "msnfp_chequenumber", "msnfp_chequewiredate", "msnfp_configurationid", "msnfp_constituentid", "msnfp_customerid", "msnfp_date", "msnfp_daterefunded", "msnfp_dataentrysource", "msnfp_identifier", "msnfp_ccbrandcode", "msnfp_organizationname", "msnfp_paymentmethodid", "msnfp_dataentryreference", "msnfp_invoiceidentifier", "msnfp_transactionfraudcode", "msnfp_transactionidentifier", "msnfp_transactionresult", "msnfp_thirdpartyreceipt", "msnfp_sum_donations", "msnfp_sum_products", "msnfp_sum_sponsorships", "msnfp_sum_tickets", "msnfp_sum_registrations", "msnfp_val_donations", "msnfp_val_products", "msnfp_val_sponsorships", "msnfp_val_tickets", "transactioncurrencyid", "msnfp_amount_balance", "createdon", "statuscode", "statecode");
        }

        // if the event package's status reason is changed, we may need to update the associated receipt
        private void UpdateReceiptStatusReason(IPluginExecutionContext context, IOrganizationService service, ITracingService tracingService)
        {
            if (context.MessageName != "Update") return;

            Entity targetIncomingRecord = (Entity)context.InputParameters["Target"];
            OptionSetValue statusReason = targetIncomingRecord.GetAttributeValue<OptionSetValue>("statuscode");
            if (statusReason != null)
            {
                // only proceed if there is a  receipt
                Entity postImage = context.PostEntityImages["postImage"];
                EntityReference receipt = postImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                if (receipt == null) return;

                Entity receiptToUpdate = new Entity("msnfp_receipt", receipt.Id);
                if (statusReason.Value == 844060000)
                {
                    tracingService.Trace("Event Package Status Reason is Complete. Setting Receipt Status Reason to Issued.");
                    receiptToUpdate["statuscode"] = new OptionSetValue(1); // Issued
                    service.Update(receiptToUpdate);
                }
                else if (statusReason.Value == 844060003)
                {
                    tracingService.Trace("Event Package Status Reason is Failed. Setting Receipt Status Reason to Void(Failed).");
                    receiptToUpdate["statuscode"] = new OptionSetValue(844060002); // Issued
                    service.Update(receiptToUpdate);
                }
            }
        }

        #region  Populate Most Recent Event Package Data To Donor

        private void PopulateMostRecentEventPackageDataToDonor(IOrganizationService organizationService, OrganizationServiceContext orgSvcContext, LocalPluginContext localContext, Entity eventPackageRecord, string messageName)
        {
            localContext.TracingService.Trace("----- Populating The Most Recent Event Registration To the according Donor -----");

            var donor = (EntityReference)eventPackageRecord.Attributes["msnfp_customerid"];

            // Check if donor is a contact or account
            if (donor.LogicalName == "contact" || donor.LogicalName == "account")
            {
                // Get the contact entity
                Entity donorEntity = organizationService.Retrieve(donor.LogicalName, donor.Id, new ColumnSet(new String[] { "msnfp_lasteventpackageid" }));

                if (string.Compare(messageName, "Delete", true) == 0)
                {
                    localContext.TracingService.Trace("Message is Delete. Locating the previous Event Package.");
                    localContext.TracingService.Trace("Current EventPackageId=" + eventPackageRecord.Id);
                    Entity mostRecentEventRegistration = (from c in orgSvcContext.CreateQuery("msnfp_eventpackage")
                                                          where ((EntityReference)c["msnfp_customerid"]).Id == donor.Id
                                                                && (Guid)c["msnfp_eventpackageid"] != eventPackageRecord.Id
                                                          orderby c["createdon"] descending
                                                          select c).FirstOrDefault();

                    if (mostRecentEventRegistration != null)
                    {
                        donorEntity["msnfp_lasteventpackageid"] =
                            new EntityReference(mostRecentEventRegistration.LogicalName, mostRecentEventRegistration.Id);
                        donorEntity["msnfp_lasteventpackagedate"] = (DateTime)mostRecentEventRegistration["createdon"];
                        organizationService.Update(donorEntity);
                    }
                }
                else if (donorEntity.Contains("msnfp_lasteventpackageid") && donorEntity["msnfp_lasteventpackageid"] != null
                ) // If this field has data => get the most recent by Date and populate values to donor
                {
                    Entity mostRecentEventRegistration = (from c in orgSvcContext.CreateQuery("msnfp_eventpackage")
                                                          where ((EntityReference)c["msnfp_customerid"]).Id == donor.Id
                                                          orderby c["createdon"] descending
                                                          select c).First();

                    donorEntity["msnfp_lasteventpackageid"] =
                        new EntityReference(mostRecentEventRegistration.LogicalName, mostRecentEventRegistration.Id);
                    donorEntity["msnfp_lasteventpackagedate"] = (DateTime)mostRecentEventRegistration["createdon"];
                    organizationService.Update(donorEntity);
                }
                else // If this field has no data => no need to check but populate values to donor
                {
                    donorEntity["msnfp_lasteventpackageid"] = new EntityReference(eventPackageRecord.LogicalName, eventPackageRecord.Id);
                    donorEntity["msnfp_lasteventpackagedate"] = (DateTime)eventPackageRecord["createdon"];
                    organizationService.Update(donorEntity);
                }
            }
            localContext.TracingService.Trace("----- Finished Populating The Most Recent Event Registration To the according Donor -----");
        }

        #endregion


        #region Update Related Records to Paid
        private void updatedRelatedRecordsToPaid(Entity primaryEventPackage, LocalPluginContext localContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("---------updatedRelatedRecordsToPaid---------");

            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            //get list of registrations (msnfp_registration)
            List<Entity> registrationList = (from a in orgSvcContext.CreateQuery("msnfp_registration")
                                             where ((EntityReference)a["msnfp_eventpackageid"]).Id == primaryEventPackage.Id
                                                                              && ((OptionSetValue)a["statuscode"]).Value == 1 //Pending Payment
                                             select a).ToList();

            localContext.TracingService.Trace("Got registrationList");
            Entity ticketRegistrations = null;

            foreach (var item in registrationList)
            {
                //update status to paid
                if (item.Contains("msnfp_registrationid"))
                {
                    localContext.TracingService.Trace("Got msnfp_registrationid");
                    ticketRegistrations = service.Retrieve("msnfp_registration", item.Id, new ColumnSet("msnfp_registrationid", "statuscode"));

                    ticketRegistrations["statuscode"] = new OptionSetValue(844060000);//Paid
                    localContext.TracingService.Trace("Record updated to Paid");
                    service.Update(ticketRegistrations);

                    localContext.TracingService.Trace("Record updated for ticketRegistrations");
                }
            }

            //get list of purchased products (msnfp_product)
            List<Entity> productList = (from a in orgSvcContext.CreateQuery("msnfp_product")
                                        where ((EntityReference)a["msnfp_eventpackageid"]).Id == primaryEventPackage.Id
                                         && ((OptionSetValue)a["statuscode"]).Value == 1 //Pending Payment
                                        select a).ToList();

            localContext.TracingService.Trace("Got productList");

            Entity product = null;

            foreach (var item in productList)
            {
                //update status to paid
                if (item.Contains("msnfp_productid"))
                {
                    localContext.TracingService.Trace("Got msnfp_productid");
                    product = service.Retrieve("msnfp_product", item.Id, new ColumnSet("msnfp_productid", "statuscode"));

                    product["statuscode"] = new OptionSetValue(844060000);//Paid
                    localContext.TracingService.Trace("Record updated for Paid");
                    service.Update(product);

                    localContext.TracingService.Trace("Record updated for product");
                }
            }


            //get list of sponsorships (msnfp_sponsorship)
            List<Entity> sponsorshipList = (from a in orgSvcContext.CreateQuery("msnfp_sponsorship")
                                            where ((EntityReference)a["msnfp_eventpackageid"]).Id == primaryEventPackage.Id
                                             && ((OptionSetValue)a["statuscode"]).Value == 844060003 //Pending Payment
                                            select a).ToList();

            localContext.TracingService.Trace("Got sponsorshipList");

            Entity sponsorship = null;

            foreach (var item in sponsorshipList)
            {
                //update status to paid in full
                if (item.Contains("msnfp_sponsorshipid"))
                {
                    localContext.TracingService.Trace("Got msnfp_sponsorshipid");
                    sponsorship = service.Retrieve("msnfp_sponsorship", item.Id, new ColumnSet("msnfp_sponsorshipid", "statuscode"));

                    sponsorship["statuscode"] = new OptionSetValue(844060000); //Paid in Full
                    localContext.TracingService.Trace("Record updated for Paid");
                    service.Update(sponsorship);

                    localContext.TracingService.Trace("Record updated for sponsorship");
                }
            }
        }
        #endregion

        #region Updating Event Totals
        private void UpdateEventTotals(Entity queriedEntityRecord, OrganizationServiceContext orgSvcContext, IOrganizationService service, LocalPluginContext localContext)
        {
            localContext.TracingService.Trace("---------UpdateEventTotals---------");

            decimal sumPackages = 0;
            Entity parentEvent = service.Retrieve("msnfp_event", ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id, new ColumnSet(new string[] { "msnfp_eventid", "msnfp_sum_packages", "msnfp_count_packages" }));

            List<Entity> eventPackageList = (from a in orgSvcContext.CreateQuery("msnfp_eventpackage")
                                             where ((EntityReference)a["msnfp_eventid"]).Id == ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id
                                             && ((OptionSetValue)a["statecode"]).Value == 0 && ((OptionSetValue)a["statuscode"]).Value == 844060000 // completed
                                             select a).ToList();

            if (eventPackageList.Count > 0)
            {
                foreach (Entity item in eventPackageList)
                {
                    if (item.Contains("msnfp_val_sponsorships") && item["msnfp_val_sponsorships"] != null)
                        sumPackages += ((Money)item["msnfp_val_sponsorships"]).Value;

                    if (item.Contains("msnfp_val_tickets") && item["msnfp_val_tickets"] != null)
                        sumPackages += ((Money)item["msnfp_val_tickets"]).Value;

                    if (item.Contains("msnfp_val_products") && item["msnfp_val_products"] != null)
                        sumPackages += ((Money)item["msnfp_val_products"]).Value;

                    if (item.Contains("msnfp_val_donations") && item["msnfp_val_donations"] != null)
                        sumPackages += ((Money)item["msnfp_val_donations"]).Value;
                }
            }

            parentEvent["msnfp_count_packages"] = eventPackageList.Count;
            parentEvent["msnfp_sum_packages"] = new Money(sumPackages);

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
            string entityName = "EventPackage"; // Used for API calls

            string apiUrl = configurationRecord.GetAttributeValue<string>("msnfp_azure_webapiurl");
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_eventpackageid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_EventPackage jsonDataObj = new MSNFP_EventPackage();

                jsonDataObj.EventPackageId = (Guid)queriedEntityRecord["msnfp_eventpackageid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                if (queriedEntityRecord.Contains("msnfp_amount_receipted") && queriedEntityRecord["msnfp_amount_receipted"] != null)
                {
                    jsonDataObj.AmountReceipted = ((Money)queriedEntityRecord["msnfp_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_receipted");
                }
                else
                {
                    jsonDataObj.AmountReceipted = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_receipted.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonReceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable");
                }
                else
                {
                    jsonDataObj.AmountNonReceiptable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_nonreceiptable.");
                }

                if (queriedEntityRecord.Contains("msnfp_amount_tax") && queriedEntityRecord["msnfp_amount_tax"] != null)
                {
                    jsonDataObj.AmountTax = ((Money)queriedEntityRecord["msnfp_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_tax");
                }
                else
                {
                    jsonDataObj.AmountTax = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_tax.");
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

                if (queriedEntityRecord.Contains("msnfp_ref_amount_receipted") && queriedEntityRecord["msnfp_ref_amount_receipted"] != null)
                {
                    jsonDataObj.RefAmountReceipted = ((Money)queriedEntityRecord["msnfp_ref_amount_receipted"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_receipted");
                }
                else
                {
                    jsonDataObj.RefAmountReceipted = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_receipted.");
                }

                if (queriedEntityRecord.Contains("msnfp_ref_amount_nonreceiptable") && queriedEntityRecord["msnfp_ref_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.RefAmountNonreceiptable = ((Money)queriedEntityRecord["msnfp_ref_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_nonreceiptable");
                }
                else
                {
                    jsonDataObj.RefAmountNonreceiptable = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_nonreceiptable.");
                }

                if (queriedEntityRecord.Contains("msnfp_ref_amount_tax") && queriedEntityRecord["msnfp_ref_amount_tax"] != null)
                {
                    jsonDataObj.RefAmountTax = ((Money)queriedEntityRecord["msnfp_ref_amount_tax"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount_tax");
                }
                else
                {
                    jsonDataObj.RefAmountTax = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount_tax.");
                }

                if (queriedEntityRecord.Contains("msnfp_ref_amount") && queriedEntityRecord["msnfp_ref_amount"] != null)
                {
                    jsonDataObj.RefAmount = ((Money)queriedEntityRecord["msnfp_ref_amount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ref_amount");
                }
                else
                {
                    jsonDataObj.RefAmount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ref_amount.");
                }

                if (queriedEntityRecord.Contains("msnfp_firstname") && queriedEntityRecord["msnfp_firstname"] != null)
                {
                    jsonDataObj.FirstName = (string)queriedEntityRecord["msnfp_firstname"];
                    localContext.TracingService.Trace("Got msnfp_firstname");
                }
                else
                {
                    jsonDataObj.FirstName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_firstname.");
                }

                if (queriedEntityRecord.Contains("msnfp_lastname") && queriedEntityRecord["msnfp_lastname"] != null)
                {
                    jsonDataObj.LastName = (string)queriedEntityRecord["msnfp_lastname"];
                    localContext.TracingService.Trace("Got msnfp_lastname");
                }
                else
                {
                    jsonDataObj.LastName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastname.");
                }

                if (queriedEntityRecord.Contains("msnfp_emailaddress1") && queriedEntityRecord["msnfp_emailaddress1"] != null)
                {
                    jsonDataObj.Emailaddress1 = (string)queriedEntityRecord["msnfp_emailaddress1"];
                    localContext.TracingService.Trace("Got msnfp_emailaddress1");
                }
                else
                {
                    jsonDataObj.Emailaddress1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_emailaddress1.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone1") && queriedEntityRecord["msnfp_telephone1"] != null)
                {
                    jsonDataObj.Telephone1 = (string)queriedEntityRecord["msnfp_telephone1"];
                    localContext.TracingService.Trace("Got msnfp_telephone1");
                }
                else
                {
                    jsonDataObj.Telephone1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone1.");
                }

                if (queriedEntityRecord.Contains("msnfp_telephone2") && queriedEntityRecord["msnfp_telephone2"] != null)
                {
                    jsonDataObj.Telephone2 = (string)queriedEntityRecord["msnfp_telephone2"];
                    localContext.TracingService.Trace("Got msnfp_telephone2");
                }
                else
                {
                    jsonDataObj.Telephone2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone2.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_city") && queriedEntityRecord["msnfp_billing_city"] != null)
                {
                    jsonDataObj.BillingCity = (string)queriedEntityRecord["msnfp_billing_city"];
                    localContext.TracingService.Trace("Got msnfp_billing_city");
                }
                else
                {
                    jsonDataObj.BillingCity = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_city.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_country") && queriedEntityRecord["msnfp_billing_country"] != null)
                {
                    jsonDataObj.BillingCountry = (string)queriedEntityRecord["msnfp_billing_country"];
                    localContext.TracingService.Trace("Got msnfp_billing_country");
                }
                else
                {
                    jsonDataObj.BillingCountry = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_country.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_line1") && queriedEntityRecord["msnfp_billing_line1"] != null)
                {
                    jsonDataObj.BillingLine1 = (string)queriedEntityRecord["msnfp_billing_line1"];
                    localContext.TracingService.Trace("Got msnfp_billing_line1");
                }
                else
                {
                    jsonDataObj.BillingLine1 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line1.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_line2") && queriedEntityRecord["msnfp_billing_line2"] != null)
                {
                    jsonDataObj.BillingLine2 = (string)queriedEntityRecord["msnfp_billing_line2"];
                    localContext.TracingService.Trace("Got msnfp_billing_line2");
                }
                else
                {
                    jsonDataObj.BillingLine2 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line2.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_line3") && queriedEntityRecord["msnfp_billing_line3"] != null)
                {
                    jsonDataObj.BillingLine3 = (string)queriedEntityRecord["msnfp_billing_line3"];
                    localContext.TracingService.Trace("Got msnfp_billing_line3");
                }
                else
                {
                    jsonDataObj.BillingLine3 = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line3.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_postalcode") && queriedEntityRecord["msnfp_billing_postalcode"] != null)
                {
                    jsonDataObj.BillingPostalCode = (string)queriedEntityRecord["msnfp_billing_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_billing_postalcode");
                }
                else
                {
                    jsonDataObj.BillingPostalCode = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_postalcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_billing_stateorprovince") && queriedEntityRecord["msnfp_billing_stateorprovince"] != null)
                {
                    jsonDataObj.BillingStateorProvince = (string)queriedEntityRecord["msnfp_billing_stateorprovince"];
                    localContext.TracingService.Trace("Got msnfp_billing_stateorprovince");
                }
                else
                {
                    jsonDataObj.BillingStateorProvince = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_stateorprovince.");
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

                if (queriedEntityRecord.Contains("msnfp_appealid") && queriedEntityRecord["msnfp_appealid"] != null)
                {
                    jsonDataObj.Appealid = ((EntityReference)queriedEntityRecord["msnfp_appealid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_appealid");
                }
                else
                {
                    jsonDataObj.Appealid = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_appealid.");
                }

                if (queriedEntityRecord.Contains("msnfp_eventid") && queriedEntityRecord["msnfp_eventid"] != null)
                {
                    jsonDataObj.EventId = ((EntityReference)queriedEntityRecord["msnfp_eventid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_eventid");
                }
                else
                {
                    jsonDataObj.EventId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_eventid.");
                }

                if (queriedEntityRecord.Contains("msnfp_chequenumber") && queriedEntityRecord["msnfp_chequenumber"] != null)
                {
                    jsonDataObj.ChequeNumber = (string)queriedEntityRecord["msnfp_chequenumber"];
                    localContext.TracingService.Trace("Got msnfp_chequenumber");
                }
                else
                {
                    jsonDataObj.ChequeNumber = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_chequenumber.");
                }

                if (queriedEntityRecord.Contains("msnfp_chequewiredate") && queriedEntityRecord["msnfp_chequewiredate"] != null)
                {
                    jsonDataObj.ChequeWireDate = (DateTime)queriedEntityRecord["msnfp_chequewiredate"];
                    localContext.TracingService.Trace("Got msnfp_chequewiredate");
                }
                else
                {
                    jsonDataObj.ChequeWireDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_chequewiredate.");
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

                if (queriedEntityRecord.Contains("msnfp_constituentid") && queriedEntityRecord["msnfp_constituentid"] != null)
                {
                    jsonDataObj.ConstituentId = ((EntityReference)queriedEntityRecord["msnfp_constituentid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_constituentid");
                }
                else
                {
                    jsonDataObj.ConstituentId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_constituentid.");
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
                    localContext.TracingService.Trace("Got msnfp_date");
                }
                else
                {
                    jsonDataObj.Date = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_date.");
                }

                if (queriedEntityRecord.Contains("msnfp_daterefunded") && queriedEntityRecord["msnfp_daterefunded"] != null)
                {
                    jsonDataObj.DateRefunded = (DateTime)queriedEntityRecord["msnfp_daterefunded"];
                    localContext.TracingService.Trace("Got msnfp_daterefunded");
                }
                else
                {
                    jsonDataObj.DateRefunded = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_daterefunded.");
                }

                if (queriedEntityRecord.Contains("msnfp_dataentrysource") && queriedEntityRecord["msnfp_dataentrysource"] != null)
                {
                    jsonDataObj.DataEntrySource = ((OptionSetValue)queriedEntityRecord["msnfp_dataentrysource"]).Value;
                    localContext.TracingService.Trace("Got msnfp_dataentrysource");
                }
                else
                {
                    jsonDataObj.DataEntrySource = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_dataentrysource.");
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

                if (queriedEntityRecord.Contains("msnfp_ccbrandcode") && queriedEntityRecord["msnfp_ccbrandcode"] != null)
                {
                    jsonDataObj.CcBrandCode = ((OptionSetValue)queriedEntityRecord["msnfp_ccbrandcode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ccbrandcode");
                }
                else
                {
                    jsonDataObj.CcBrandCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ccbrandcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_organizationname") && queriedEntityRecord["msnfp_organizationname"] != null)
                {
                    jsonDataObj.OrganizationName = (string)queriedEntityRecord["msnfp_organizationname"];
                    localContext.TracingService.Trace("Got msnfp_organizationname");
                }
                else
                {
                    jsonDataObj.OrganizationName = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_organizationname.");
                }

                if (queriedEntityRecord.Contains("msnfp_paymentmethodid") && queriedEntityRecord["msnfp_paymentmethodid"] != null)
                {
                    jsonDataObj.PaymentmethodId = ((EntityReference)queriedEntityRecord["msnfp_paymentmethodid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentmethodid");
                }
                else
                {
                    jsonDataObj.PaymentmethodId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentmethodid.");
                }

                if (queriedEntityRecord.Contains("msnfp_dataentryreference") && queriedEntityRecord["msnfp_dataentryreference"] != null)
                {
                    jsonDataObj.DataEntryReference = (string)queriedEntityRecord["msnfp_dataentryreference"];
                    localContext.TracingService.Trace("Got msnfp_dataentryreference");
                }
                else
                {
                    jsonDataObj.DataEntryReference = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_dataentryreference.");
                }

                if (queriedEntityRecord.Contains("msnfp_invoiceidentifier") && queriedEntityRecord["msnfp_invoiceidentifier"] != null)
                {
                    jsonDataObj.InvoiceIdentifier = (string)queriedEntityRecord["msnfp_invoiceidentifier"];
                    localContext.TracingService.Trace("Got msnfp_invoiceidentifier");
                }
                else
                {
                    jsonDataObj.InvoiceIdentifier = string.Empty; ;
                    localContext.TracingService.Trace("Did NOT find msnfp_invoiceidentifier.");
                }

                if (queriedEntityRecord.Contains("msnfp_transactionfraudcode") && queriedEntityRecord["msnfp_transactionfraudcode"] != null)
                {
                    jsonDataObj.TransactionFraudCode = (string)queriedEntityRecord["msnfp_transactionfraudcode"];
                    localContext.TracingService.Trace("Got msnfp_transactionfraudcode");
                }
                else
                {
                    jsonDataObj.TransactionFraudCode = string.Empty; ;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionfraudcode.");
                }

                if (queriedEntityRecord.Contains("msnfp_transactionidentifier") && queriedEntityRecord["msnfp_transactionidentifier"] != null)
                {
                    jsonDataObj.TransactionIdentifier = (string)queriedEntityRecord["msnfp_transactionidentifier"];
                    localContext.TracingService.Trace("Got msnfp_transactionidentifier");
                }
                else
                {
                    jsonDataObj.TransactionIdentifier = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionidentifier.");
                }

                if (queriedEntityRecord.Contains("msnfp_transactionresult") && queriedEntityRecord["msnfp_transactionresult"] != null)
                {
                    jsonDataObj.TransactionResult = (string)queriedEntityRecord["msnfp_transactionresult"];
                    localContext.TracingService.Trace("Got msnfp_transactionresult");
                }
                else
                {
                    jsonDataObj.TransactionResult = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionresult.");
                }

                if (queriedEntityRecord.Contains("msnfp_thirdpartyreceipt") && queriedEntityRecord["msnfp_thirdpartyreceipt"] != null)
                {
                    jsonDataObj.ThirdPartyReceipt = (string)queriedEntityRecord["msnfp_thirdpartyreceipt"];
                    localContext.TracingService.Trace("Got msnfp_thirdpartyreceipt");
                }
                else
                {
                    jsonDataObj.ThirdPartyReceipt = string.Empty;
                    localContext.TracingService.Trace("Did NOT find msnfp_thirdpartyreceipt.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_donations") && queriedEntityRecord["msnfp_sum_donations"] != null)
                {
                    jsonDataObj.SumDonations = (int)queriedEntityRecord["msnfp_sum_donations"];
                    localContext.TracingService.Trace("Got msnfp_sum_donations");
                }
                else
                {
                    jsonDataObj.SumDonations = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_donations.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_products") && queriedEntityRecord["msnfp_sum_products"] != null)
                {
                    jsonDataObj.SumProducts = (int)queriedEntityRecord["msnfp_sum_products"];
                    localContext.TracingService.Trace("Got msnfp_sum_products");
                }
                else
                {
                    jsonDataObj.SumProducts = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_products.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_sponsorships") && queriedEntityRecord["msnfp_sum_sponsorships"] != null)
                {
                    jsonDataObj.SumSponsorships = (int)queriedEntityRecord["msnfp_sum_sponsorships"];
                    localContext.TracingService.Trace("Got msnfp_sum_sponsorships");
                }
                else
                {
                    jsonDataObj.SumSponsorships = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_sponsorships.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_tickets") && queriedEntityRecord["msnfp_sum_tickets"] != null)
                {
                    jsonDataObj.SumTickets = (int)queriedEntityRecord["msnfp_sum_tickets"];
                    localContext.TracingService.Trace("Got msnfp_sum_tickets");
                }
                else
                {
                    jsonDataObj.SumTickets = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_tickets.");
                }

                if (queriedEntityRecord.Contains("msnfp_sum_registrations") && queriedEntityRecord["msnfp_sum_registrations"] != null)
                {
                    jsonDataObj.SumRegistrations = (int)queriedEntityRecord["msnfp_sum_registrations"];
                    localContext.TracingService.Trace("Got msnfp_sum_registrations");
                }
                else
                {
                    jsonDataObj.SumRegistrations = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_sum_registrations.");
                }

                if (queriedEntityRecord.Contains("msnfp_val_donations") && queriedEntityRecord["msnfp_val_donations"] != null)
                {
                    jsonDataObj.ValDonations = ((Money)queriedEntityRecord["msnfp_val_donations"]).Value;
                    localContext.TracingService.Trace("Got msnfp_val_donations");
                }
                else
                {
                    jsonDataObj.ValDonations = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_val_donations.");
                }

                if (queriedEntityRecord.Contains("msnfp_val_products") && queriedEntityRecord["msnfp_val_products"] != null)
                {
                    jsonDataObj.ValProducts = ((Money)queriedEntityRecord["msnfp_val_products"]).Value;
                    localContext.TracingService.Trace("Got msnfp_val_products");
                }
                else
                {
                    jsonDataObj.ValProducts = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_val_products.");
                }

                if (queriedEntityRecord.Contains("msnfp_val_sponsorships") && queriedEntityRecord["msnfp_val_sponsorships"] != null)
                {
                    jsonDataObj.ValSponsorships = ((Money)queriedEntityRecord["msnfp_val_sponsorships"]).Value;
                    localContext.TracingService.Trace("Got msnfp_val_sponsorships");
                }
                else
                {
                    jsonDataObj.ValSponsorships = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_val_sponsorships.");
                }

                if (queriedEntityRecord.Contains("msnfp_val_tickets") && queriedEntityRecord["msnfp_val_tickets"] != null)
                {
                    jsonDataObj.ValTickets = ((Money)queriedEntityRecord["msnfp_val_tickets"]).Value;
                    localContext.TracingService.Trace("Got msnfp_val_tickets");
                }
                else
                {
                    jsonDataObj.ValTickets = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_val_tickets.");
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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_EventPackage));
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
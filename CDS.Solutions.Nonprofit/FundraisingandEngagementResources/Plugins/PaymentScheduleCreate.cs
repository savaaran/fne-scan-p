/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Plugins.PaymentProcesses;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;
using Plugins.AzureModels;

namespace Plugins
{
    public class PaymentScheduleCreate : PluginBase
    {
        public PaymentScheduleCreate(string unsecure, string secure)
            : base(typeof(PaymentScheduleCreate))
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }
            localContext.TracingService.Trace("---------Triggered PaymentScheduleCreate.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;

            Entity queriedEntityRecord = null;
            // Note target is used in Azure sync.
            Entity targetIncomingRecord;
            string messageName = context.MessageName;

            Entity configurationRecord = Utilities.GetConfigurationRecordByMessageName(context, service, localContext.TracingService);

            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering PaymentScheduleCreate.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    if (messageName == "Update")
                    {
                        queriedEntityRecord = service.Retrieve("msnfp_paymentschedule", targetIncomingRecord.Id, GetPaymentScheduleColumns());
                    }

                    if (targetIncomingRecord != null)
                    {
                        try
                        {
                            // Sync this to Azure. Note we use the target here as we want all the columns:
                            if (messageName == "Create")
                            {
                                // Check and see if the donor is missing and if so try and assign one:
                                if (!targetIncomingRecord.Contains("msnfp_customerid"))
                                {
                                    localContext.TracingService.Trace("Validating donor - start.");

                                    // Get the updated entity:
                                    queriedEntityRecord = service.Retrieve("msnfp_paymentschedule", targetIncomingRecord.Id, GetPaymentScheduleColumns());
                                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                                }
                                else
                                {
                                    // There is a donor, so proceed as normal:
                                    AddOrUpdateThisRecordWithAzure(targetIncomingRecord, configurationRecord, localContext, service, context);
                                }

                                //// If this is a donation or campaign page record, the team owner is assigned on that page's configuration instead of the normal owner (this field is not saved in Azure so can be done after the above).
                                //if (targetIncomingRecord.Contains("msnfp_donationpageid") || targetIncomingRecord.Contains("msnfp_campaignpageid"))
                                //{
                                //    AssignTeamOwnerIdForDonationOrCampaignPageRecords(localContext, targetIncomingRecord, service);
                                //}
                            }
                            else if (messageName == "Update")
                            {
                                AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                            }
                        }
                        catch (Exception e)
                        {
                            localContext.TracingService.Trace("An error has occured: " + e.ToString());
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
                    queriedEntityRecord = service.Retrieve("msnfp_paymentschedule", ((EntityReference)context.InputParameters["Target"]).Id, GetPaymentScheduleColumns());
                    // Here we update the values to be deleted:
                    AddOrUpdateThisRecordWithAzure(queriedEntityRecord, configurationRecord, localContext, service, context);
                }

                localContext.TracingService.Trace("---------Exiting PaymentScheduleCreate.cs---------");
            }
        }

        private ColumnSet GetPaymentScheduleColumns()
        {
            ColumnSet cols = new ColumnSet("msnfp_paymentscheduleid", "msnfp_name", "createdon", "msnfp_amount_receipted", "msnfp_amount_membership", "msnfp_amount_nonreceiptable", "msnfp_amount_tax", "msnfp_recurringamount", "msnfp_firstpaymentdate", "msnfp_frequencyinterval", "msnfp_frequency", "msnfp_frequencystartcode", "msnfp_nextpaymentdate", "msnfp_cancelationcode", "msnfp_cancellationnote", "msnfp_cancelledon", "msnfp_endondate", "msnfp_lastpaymentdate", "msnfp_scheduletypecode", "msnfp_anonymity", "msnfp_paymentmethodid", "msnfp_appealid", "msnfp_appraiser", "msnfp_billing_city", "msnfp_billing_country", "msnfp_billing_line1", "msnfp_billing_line2", "msnfp_billing_line3", "msnfp_billing_postalcode", "msnfp_billing_stateorprovince", "msnfp_originatingcampaignid", "msnfp_ccbrandcode", "msnfp_chargeoncreate", "msnfp_configurationid", "msnfp_constituentid", "msnfp_customerid", "msnfp_bookdate", "msnfp_ga_deliverycode", "msnfp_depositdate", "msnfp_emailaddress1", "msnfp_eventid", "msnfp_eventpackageid", "msnfp_firstname", "msnfp_giftbatchid", "msnfp_dataentrysource", "msnfp_paymenttypecode", "msnfp_lastname", "msnfp_membershipcategoryid", "msnfp_membershipinstanceid", "msnfp_mobilephone", "msnfp_organizationname", "msnfp_packageid", "msnfp_taxreceiptid", "msnfp_receiptpreferencecode", "msnfp_telephone1", "msnfp_telephone2", "msnfp_dataentryreference", "msnfp_invoiceidentifier", "msnfp_transactionfraudcode", "msnfp_transactionidentifier", "msnfp_transactionresult", "msnfp_tributecode", "msnfp_tributeacknowledgement", "msnfp_tributeid", "msnfp_tributemessage", "msnfp_paymentprocessorid", "msnfp_transactiondescription", "msnfp_designationid", "transactioncurrencyid", "statecode", "statuscode");

            return cols;
        }

        //private void AssignTeamOwnerIdForDonationOrCampaignPageRecords(LocalPluginContext localContext, Entity paymentScheduleRecord, IOrganizationService service)
        //{
        //    localContext.TracingService.Trace("---------Entering AssignTeamOwnerIdForDonationOrCampaignPageRecords()---------");
        //    // Get the ownerid from the owning business unit for donation page records imported donation page:
        //    if (paymentScheduleRecord.Contains("msnfp_donationpageid") || paymentScheduleRecord.Contains("msnfp_campaignpageid"))
        //    {
        //        // If it is a campaign page donation, get that team owner:
        //        if (paymentScheduleRecord.Contains("msnfp_campaignpageid") && paymentScheduleRecord["msnfp_campaignpageid"] != null)
        //        {
        //            localContext.TracingService.Trace("Updating transaction ownerid based on the campaign page 'Set Owning Team' field.");

        //            // Query for the field:
        //            ColumnSet campaignpagecols;
        //            campaignpagecols = new ColumnSet("msnfp_campaignpageid", "msnfp_teamownerid");

        //            Entity campaignPageRecord = service.Retrieve("msnfp_campaignpage", ((EntityReference)paymentScheduleRecord["msnfp_campaignpageid"]).Id, campaignpagecols);
        //            localContext.TracingService.Trace("Owner id: " + ((EntityReference)campaignPageRecord["msnfp_teamownerid"]).Id.ToString());

        //            // Attempt to set the owner id:
        //            paymentScheduleRecord["ownerid"] = new EntityReference("team", ((EntityReference)campaignPageRecord["msnfp_teamownerid"]).Id);
        //        }
        //        // If it is a donation page donation, get that team owner:
        //        else if (paymentScheduleRecord.Contains("msnfp_donationpageid") && paymentScheduleRecord["msnfp_donationpageid"] != null)
        //        {
        //            localContext.TracingService.Trace("Updating transaction ownerid based on the donation page 'Set Owning Team' field.");

        //            // Query for the field:
        //            ColumnSet donationpagecols;
        //            donationpagecols = new ColumnSet("msnfp_donationpageid", "msnfp_teamownerid");

        //            Entity donationPageRecord = service.Retrieve("msnfp_donationpage", ((EntityReference)paymentScheduleRecord["msnfp_donationpageid"]).Id, donationpagecols);
        //            localContext.TracingService.Trace("Owner id: " + ((EntityReference)donationPageRecord["msnfp_teamownerid"]).Id.ToString());

        //            // Attempt to set the owner id:
        //            paymentScheduleRecord["ownerid"] = new EntityReference("team", ((EntityReference)donationPageRecord["msnfp_teamownerid"]).Id);
        //        }

        //        try
        //        {
        //            // Updating payment schedule record
        //            service.Update(paymentScheduleRecord);
        //        }
        //        catch (Exception ownerupdateError)
        //        {
        //            localContext.TracingService.Trace("Failed to update the ownerid field: " + ownerupdateError.Message.ToString());
        //        }
        //    }
        //    else
        //    {
        //        localContext.TracingService.Trace("Not a newly created donation/campaign page payment schedule. Skipping owning team assignment.");
        //    }
        //    localContext.TracingService.Trace("---------Exiting AssignTeamOwnerIdForDonationOrCampaignPageRecords()---------");
        //}


        #region Send the Record to Azure
        private void AddOrUpdateThisRecordWithAzure(Entity queriedEntityRecord, Entity configurationRecord, LocalPluginContext localContext, IOrganizationService service, IPluginExecutionContext context)
        {
            localContext.TracingService.Trace("---------Send the Record to Azure---------");

            string messageName = context.MessageName;
            string entityName = "PaymentSchedule"; // Used for API calls

            string apiUrl = Utilities.GetAzureWebAPIURL(service, context);
            localContext.TracingService.Trace("Got API URL: " + apiUrl);

            if (apiUrl != string.Empty)
            {
                localContext.TracingService.Trace("Getting Latest Info for Record: " + queriedEntityRecord["msnfp_paymentscheduleid"].ToString());

                // Attempt to assign queriedEntityRecord attributes to jsonDataObj for the JSON post.
                MSNFP_PaymentSchedule jsonDataObj = new MSNFP_PaymentSchedule();

                jsonDataObj.PaymentScheduleId = (Guid)queriedEntityRecord["msnfp_paymentscheduleid"];

                // Now we get all the fields for this entity and save them to a JSON object.
                jsonDataObj.Name = queriedEntityRecord.Contains("msnfp_name") ? (string)queriedEntityRecord["msnfp_name"] : string.Empty;
                localContext.TracingService.Trace("Title: " + jsonDataObj.Name);

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


                if (queriedEntityRecord.Contains("msnfp_amount_membership") && queriedEntityRecord["msnfp_amount_membership"] != null)
                {
                    jsonDataObj.AmountMembership = ((Money)queriedEntityRecord["msnfp_amount_membership"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_membership.");
                }
                else
                {
                    jsonDataObj.AmountMembership = 0;
                    localContext.TracingService.Trace("Did NOT find msnfp_amount_membership.");
                }


                if (queriedEntityRecord.Contains("msnfp_amount_nonreceiptable") && queriedEntityRecord["msnfp_amount_nonreceiptable"] != null)
                {
                    jsonDataObj.AmountNonReceiptable = ((Money)queriedEntityRecord["msnfp_amount_nonreceiptable"]).Value;
                    localContext.TracingService.Trace("Got msnfp_amount_nonreceiptable.");
                }
                else
                {
                    jsonDataObj.AmountNonReceiptable = 0;
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


                if (queriedEntityRecord.Contains("msnfp_recurringamount") && queriedEntityRecord["msnfp_recurringamount"] != null)
                {
                    jsonDataObj.RecurringAmount = ((Money)queriedEntityRecord["msnfp_recurringamount"]).Value;
                    localContext.TracingService.Trace("Got msnfp_recurringamount.");
                }
                else
                {
                    jsonDataObj.RecurringAmount = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_recurringamount.");
                }


                if (queriedEntityRecord.Contains("msnfp_firstpaymentdate") && queriedEntityRecord["msnfp_firstpaymentdate"] != null)
                {
                    jsonDataObj.FirstPaymentDate = (DateTime)queriedEntityRecord["msnfp_firstpaymentdate"];
                    localContext.TracingService.Trace("Got msnfp_firstpaymentdate.");
                }
                else
                {
                    jsonDataObj.FirstPaymentDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_firstpaymentdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_frequencyinterval") && queriedEntityRecord["msnfp_frequencyinterval"] != null)
                {
                    jsonDataObj.FrequencyInterval = (int)queriedEntityRecord["msnfp_frequencyinterval"];
                    localContext.TracingService.Trace("Got msnfp_frequencyinterval.");
                }
                else
                {
                    jsonDataObj.FrequencyInterval = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_frequencyinterval.");
                }


                if (queriedEntityRecord.Contains("msnfp_frequency") && queriedEntityRecord["msnfp_frequency"] != null)
                {
                    jsonDataObj.Frequency = ((OptionSetValue)queriedEntityRecord["msnfp_frequency"]).Value;
                    localContext.TracingService.Trace("Got msnfp_frequency.");
                }
                else
                {
                    jsonDataObj.Frequency = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_frequency.");
                }


                if (queriedEntityRecord.Contains("msnfp_nextpaymentdate") && queriedEntityRecord["msnfp_nextpaymentdate"] != null)
                {
                    jsonDataObj.NextPaymentDate = (DateTime)queriedEntityRecord["msnfp_nextpaymentdate"];
                    localContext.TracingService.Trace("Got msnfp_nextpaymentdate.");
                }
                else
                {
                    jsonDataObj.NextPaymentDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_nextpaymentdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_frequencystartcode") && queriedEntityRecord["msnfp_frequencystartcode"] != null)
                {
                    jsonDataObj.FrequencyStartCode = ((OptionSetValue)queriedEntityRecord["msnfp_frequencystartcode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_frequencystartcode.");
                }
                else
                {
                    jsonDataObj.FrequencyStartCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_frequencystartcode.");
                }


                if (queriedEntityRecord.Contains("msnfp_cancelationcode") && queriedEntityRecord["msnfp_cancelationcode"] != null)
                {
                    jsonDataObj.CancelationCode = ((OptionSetValue)queriedEntityRecord["msnfp_cancelationcode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_cancelationcode.");
                }
                else
                {
                    jsonDataObj.CancelationCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_cancelationcode.");
                }


                if (queriedEntityRecord.Contains("msnfp_cancellationnote") && queriedEntityRecord["msnfp_cancellationnote"] != null)
                {
                    jsonDataObj.CancellationNote = (string)queriedEntityRecord["msnfp_cancellationnote"];
                    localContext.TracingService.Trace("Got msnfp_cancellationnote.");
                }
                else
                {
                    jsonDataObj.CancellationNote = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_cancellationnote.");
                }


                if (queriedEntityRecord.Contains("msnfp_cancelledon") && queriedEntityRecord["msnfp_cancelledon"] != null)
                {
                    jsonDataObj.CancelledOn = (DateTime)queriedEntityRecord["msnfp_cancelledon"];
                    localContext.TracingService.Trace("Got msnfp_cancelledon.");
                }
                else
                {
                    jsonDataObj.CancelledOn = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_cancelledon.");
                }


                if (queriedEntityRecord.Contains("msnfp_endondate") && queriedEntityRecord["msnfp_endondate"] != null)
                {
                    jsonDataObj.EndonDate = (DateTime)queriedEntityRecord["msnfp_endondate"];
                    localContext.TracingService.Trace("Got msnfp_endondate.");
                }
                else
                {
                    jsonDataObj.EndonDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_endondate.");
                }


                if (queriedEntityRecord.Contains("msnfp_lastpaymentdate") && queriedEntityRecord["msnfp_lastpaymentdate"] != null)
                {
                    jsonDataObj.LastPaymentDate = (DateTime)queriedEntityRecord["msnfp_lastpaymentdate"];
                    localContext.TracingService.Trace("Got msnfp_lastpaymentdate.");
                }
                else
                {
                    jsonDataObj.LastPaymentDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastpaymentdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_scheduletypecode") && queriedEntityRecord["msnfp_scheduletypecode"] != null)
                {
                    jsonDataObj.ScheduleTypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_scheduletypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_scheduletypecode.");
                }
                else
                {
                    jsonDataObj.ScheduleTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_scheduletypecode.");
                }


                if (queriedEntityRecord.Contains("msnfp_anonymity") && queriedEntityRecord["msnfp_anonymity"] != null)
                {
                    jsonDataObj.Anonymity = ((OptionSetValue)queriedEntityRecord["msnfp_anonymity"]).Value;
                    localContext.TracingService.Trace("Got msnfp_anonymity.");
                }
                else
                {
                    jsonDataObj.Anonymity = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_anonymity.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentmethodid") && queriedEntityRecord["msnfp_paymentmethodid"] != null)
                {
                    jsonDataObj.PaymentMethodId = ((EntityReference)queriedEntityRecord["msnfp_paymentmethodid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentmethodid.");
                }
                else
                {
                    jsonDataObj.PaymentMethodId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentmethodid.");
                }


                if (queriedEntityRecord.Contains("msnfp_designationid") && queriedEntityRecord["msnfp_designationid"] != null)
                {
                    jsonDataObj.DesignationId = ((EntityReference)queriedEntityRecord["msnfp_designationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_designationid.");
                }
                else
                {
                    jsonDataObj.DesignationId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_designationid.");
                }


                if (queriedEntityRecord.Contains("msnfp_appealid") && queriedEntityRecord["msnfp_appealid"] != null)
                {
                    jsonDataObj.AppealId = ((EntityReference)queriedEntityRecord["msnfp_appealid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_appealid.");
                }
                else
                {
                    jsonDataObj.AppealId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_appealid.");
                }


                if (queriedEntityRecord.Contains("msnfp_appraiser") && queriedEntityRecord["msnfp_appraiser"] != null)
                {
                    jsonDataObj.Appraiser = (string)queriedEntityRecord["msnfp_appraiser"];
                    localContext.TracingService.Trace("Got msnfp_appraiser.");
                }
                else
                {
                    jsonDataObj.Appraiser = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_appraiser.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_city") && queriedEntityRecord["msnfp_billing_city"] != null)
                {
                    jsonDataObj.BillingCity = (string)queriedEntityRecord["msnfp_billing_city"];
                    localContext.TracingService.Trace("Got msnfp_billing_city.");
                }
                else
                {
                    jsonDataObj.BillingCity = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_city.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_country") && queriedEntityRecord["msnfp_billing_country"] != null)
                {
                    jsonDataObj.BillingCountry = (string)queriedEntityRecord["msnfp_billing_country"];
                    localContext.TracingService.Trace("Got msnfp_billing_country.");
                }
                else
                {
                    jsonDataObj.BillingCountry = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_country.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_line1") && queriedEntityRecord["msnfp_billing_line1"] != null)
                {
                    jsonDataObj.BillingLine1 = (string)queriedEntityRecord["msnfp_billing_line1"];
                    localContext.TracingService.Trace("Got msnfp_billing_line1.");
                }
                else
                {
                    jsonDataObj.BillingLine1 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line1.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_line2") && queriedEntityRecord["msnfp_billing_line2"] != null)
                {
                    jsonDataObj.BillingLine2 = (string)queriedEntityRecord["msnfp_billing_line2"];
                    localContext.TracingService.Trace("Got msnfp_billing_line2.");
                }
                else
                {
                    jsonDataObj.BillingLine2 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line2.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_line3") && queriedEntityRecord["msnfp_billing_line3"] != null)
                {
                    jsonDataObj.BillingLine3 = (string)queriedEntityRecord["msnfp_billing_line3"];
                    localContext.TracingService.Trace("Got msnfp_billing_line3.");
                }
                else
                {
                    jsonDataObj.BillingLine3 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_line3.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_postalcode") && queriedEntityRecord["msnfp_billing_postalcode"] != null)
                {
                    jsonDataObj.BillingPostalCode = (string)queriedEntityRecord["msnfp_billing_postalcode"];
                    localContext.TracingService.Trace("Got msnfp_billing_postalcode.");
                }
                else
                {
                    jsonDataObj.BillingPostalCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_postalcode.");
                }


                if (queriedEntityRecord.Contains("msnfp_billing_stateorprovince") && queriedEntityRecord["msnfp_billing_stateorprovince"] != null)
                {
                    jsonDataObj.BillingStateorProvince = (string)queriedEntityRecord["msnfp_billing_stateorprovince"];
                    localContext.TracingService.Trace("Got msnfp_billing_stateorprovince.");
                }
                else
                {
                    jsonDataObj.BillingStateorProvince = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_billing_stateorprovince.");
                }


                if (queriedEntityRecord.Contains("msnfp_originatingcampaignid") && queriedEntityRecord["msnfp_originatingcampaignid"] != null)
                {
                    jsonDataObj.OriginatingCampaignId = ((EntityReference)queriedEntityRecord["msnfp_originatingcampaignid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_originatingcampaignid.");
                }
                else
                {
                    jsonDataObj.OriginatingCampaignId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_originatingcampaignid.");
                }


                //if (queriedEntityRecord.Contains("msnfp_campaignpageid") && queriedEntityRecord["msnfp_campaignpageid"] != null)
                //{
                //    jsonDataObj.CampaignPageId = ((EntityReference)queriedEntityRecord["msnfp_campaignpageid"]).Id;
                //    localContext.TracingService.Trace("Got msnfp_campaignpageid.");
                //}
                //else
                //{
                //    jsonDataObj.CampaignPageId = null;
                //    localContext.TracingService.Trace("Did NOT find msnfp_campaignpageid.");
                //}


                if (queriedEntityRecord.Contains("msnfp_ccbrandcode") && queriedEntityRecord["msnfp_ccbrandcode"] != null)
                {
                    jsonDataObj.CcBrandCode = ((OptionSetValue)queriedEntityRecord["msnfp_ccbrandcode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ccbrandcode.");
                }
                else
                {
                    jsonDataObj.CcBrandCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ccbrandcode.");
                }


                if (queriedEntityRecord.Contains("msnfp_chargeoncreate") && queriedEntityRecord["msnfp_chargeoncreate"] != null)
                {
                    jsonDataObj.ChargeonCreate = (bool)queriedEntityRecord["msnfp_chargeoncreate"];
                    localContext.TracingService.Trace("Got msnfp_chargeoncreate.");
                }
                else
                {
                    jsonDataObj.ChargeonCreate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_chargeoncreate.");
                }


                if (queriedEntityRecord.Contains("msnfp_configurationid") && queriedEntityRecord["msnfp_configurationid"] != null)
                {
                    jsonDataObj.ConfigurationId = ((EntityReference)queriedEntityRecord["msnfp_configurationid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_configurationid.");
                }
                else
                {
                    jsonDataObj.ConfigurationId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_configurationid.");
                }


                if (queriedEntityRecord.Contains("msnfp_constituentid") && queriedEntityRecord["msnfp_constituentid"] != null)
                {
                    jsonDataObj.ConstituentId = ((EntityReference)queriedEntityRecord["msnfp_constituentid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_constituentid.");
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

                    localContext.TracingService.Trace("Got msnfp_customerid.");
                }
                else
                {
                    jsonDataObj.CustomerId = null;
                    jsonDataObj.CustomerIdType = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_customerid.");
                }


                if (queriedEntityRecord.Contains("msnfp_bookdate") && queriedEntityRecord["msnfp_bookdate"] != null)
                {
                    jsonDataObj.BookDate = (DateTime)queriedEntityRecord["msnfp_bookdate"];
                    localContext.TracingService.Trace("Got msnfp_bookdate.");
                }
                else
                {
                    jsonDataObj.BookDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_bookdate.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymentprocessorid") && queriedEntityRecord["msnfp_paymentprocessorid"] != null)
                {
                    jsonDataObj.PaymentProcessorId = ((EntityReference)queriedEntityRecord["msnfp_paymentprocessorid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_paymentprocessorid");
                }
                else
                {
                    jsonDataObj.PaymentProcessorId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymentprocessorid");
                }


                if (queriedEntityRecord.Contains("msnfp_ga_deliverycode") && queriedEntityRecord["msnfp_ga_deliverycode"] != null)
                {
                    jsonDataObj.GaDeliveryCode = ((OptionSetValue)queriedEntityRecord["msnfp_ga_deliverycode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_ga_deliverycode.");
                }
                else
                {
                    jsonDataObj.GaDeliveryCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_ga_deliverycode.");
                }


                if (queriedEntityRecord.Contains("msnfp_depositdate") && queriedEntityRecord["msnfp_depositdate"] != null)
                {
                    jsonDataObj.DepositDate = (DateTime)queriedEntityRecord["msnfp_depositdate"];
                    localContext.TracingService.Trace("Got msnfp_depositdate.");
                }
                else
                {
                    jsonDataObj.DepositDate = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_depositdate.");
                }


                //if (queriedEntityRecord.Contains("msnfp_donationpageid") && queriedEntityRecord["msnfp_donationpageid"] != null)
                //{
                //    jsonDataObj.DonationPageId = ((EntityReference)queriedEntityRecord["msnfp_donationpageid"]).Id;
                //    localContext.TracingService.Trace("Got msnfp_donationpageid.");
                //}
                //else
                //{
                //    jsonDataObj.DonationPageId = null;
                //    localContext.TracingService.Trace("Did NOT find msnfp_donationpageid.");
                //}


                if (queriedEntityRecord.Contains("msnfp_emailaddress1") && queriedEntityRecord["msnfp_emailaddress1"] != null)
                {
                    jsonDataObj.EmailAddress1 = (string)queriedEntityRecord["msnfp_emailaddress1"];
                    localContext.TracingService.Trace("Got msnfp_emailaddress1.");
                }
                else
                {
                    jsonDataObj.EmailAddress1 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_emailaddress1.");
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


                if (queriedEntityRecord.Contains("msnfp_firstname") && queriedEntityRecord["msnfp_firstname"] != null)
                {
                    jsonDataObj.FirstName = (string)queriedEntityRecord["msnfp_firstname"];
                    localContext.TracingService.Trace("Got msnfp_firstname.");
                }
                else
                {
                    jsonDataObj.FirstName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_firstname.");
                }


                if (queriedEntityRecord.Contains("msnfp_giftbatchid") && queriedEntityRecord["msnfp_giftbatchid"] != null)
                {
                    jsonDataObj.GiftBatchId = ((EntityReference)queriedEntityRecord["msnfp_giftbatchid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_giftbatchid.");
                }
                else
                {
                    jsonDataObj.GiftBatchId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_giftbatchid.");
                }


                if (queriedEntityRecord.Contains("msnfp_dataentrysource") && queriedEntityRecord["msnfp_dataentrysource"] != null)
                {
                    jsonDataObj.DataEntrySource = ((OptionSetValue)queriedEntityRecord["msnfp_dataentrysource"]).Value;
                    localContext.TracingService.Trace("Got msnfp_dataentrysource.");
                }
                else
                {
                    jsonDataObj.DataEntrySource = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_dataentrysource.");
                }


                if (queriedEntityRecord.Contains("msnfp_paymenttypecode") && queriedEntityRecord["msnfp_paymenttypecode"] != null)
                {
                    jsonDataObj.PaymentTypeCode = ((OptionSetValue)queriedEntityRecord["msnfp_paymenttypecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_paymenttypecode.");
                }
                else
                {
                    jsonDataObj.PaymentTypeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_paymenttypecode.");
                }


                if (queriedEntityRecord.Contains("msnfp_lastname") && queriedEntityRecord["msnfp_lastname"] != null)
                {
                    jsonDataObj.LastName = (string)queriedEntityRecord["msnfp_lastname"];
                    localContext.TracingService.Trace("Got msnfp_lastname.");
                }
                else
                {
                    jsonDataObj.LastName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_lastname.");
                }


                if (queriedEntityRecord.Contains("msnfp_membershipcategoryid") && queriedEntityRecord["msnfp_membershipcategoryid"] != null)
                {
                    jsonDataObj.MembershipCategoryId = ((EntityReference)queriedEntityRecord["msnfp_membershipcategoryid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_membershipcategoryid.");
                }
                else
                {
                    jsonDataObj.MembershipCategoryId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_membershipcategoryid.");
                }


                if (queriedEntityRecord.Contains("msnfp_membershipinstanceid") && queriedEntityRecord["msnfp_membershipinstanceid"] != null)
                {
                    jsonDataObj.MembershipId = ((EntityReference)queriedEntityRecord["msnfp_membershipinstanceid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_membershipinstanceid.");
                }
                else
                {
                    jsonDataObj.MembershipId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_membershipinstanceid.");
                }


                if (queriedEntityRecord.Contains("msnfp_mobilephone") && queriedEntityRecord["msnfp_mobilephone"] != null)
                {
                    jsonDataObj.MobilePhone = (string)queriedEntityRecord["msnfp_mobilephone"];
                    localContext.TracingService.Trace("Got msnfp_mobilephone.");
                }
                else
                {
                    jsonDataObj.MobilePhone = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_mobilephone.");
                }


                if (queriedEntityRecord.Contains("msnfp_organizationname") && queriedEntityRecord["msnfp_organizationname"] != null)
                {
                    jsonDataObj.OrganizationName = (string)queriedEntityRecord["msnfp_organizationname"];
                    localContext.TracingService.Trace("Got msnfp_organizationname.");
                }
                else
                {
                    jsonDataObj.OrganizationName = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_organizationname.");
                }


                if (queriedEntityRecord.Contains("msnfp_packageid") && queriedEntityRecord["msnfp_packageid"] != null)
                {
                    jsonDataObj.PackageId = ((EntityReference)queriedEntityRecord["msnfp_packageid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_packageid.");
                }
                else
                {
                    jsonDataObj.PackageId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_packageid.");
                }


                if (queriedEntityRecord.Contains("msnfp_taxreceiptid") && queriedEntityRecord["msnfp_taxreceiptid"] != null)
                {
                    jsonDataObj.TaxReceiptId = ((EntityReference)queriedEntityRecord["msnfp_taxreceiptid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_taxreceiptid.");
                }
                else
                {
                    jsonDataObj.TaxReceiptId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_taxreceiptid.");
                }


                if (queriedEntityRecord.Contains("msnfp_receiptpreferencecode") && queriedEntityRecord["msnfp_receiptpreferencecode"] != null)
                {
                    jsonDataObj.ReceiptPreferenceCode = ((OptionSetValue)queriedEntityRecord["msnfp_receiptpreferencecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_receiptpreferencecode.");
                }
                else
                {
                    jsonDataObj.ReceiptPreferenceCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_receiptpreferencecode.");
                }


                if (queriedEntityRecord.Contains("msnfp_telephone1") && queriedEntityRecord["msnfp_telephone1"] != null)
                {
                    jsonDataObj.Telephone1 = (string)queriedEntityRecord["msnfp_telephone1"];
                    localContext.TracingService.Trace("Got msnfp_telephone1.");
                }
                else
                {
                    jsonDataObj.Telephone1 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone1.");
                }


                if (queriedEntityRecord.Contains("msnfp_telephone2") && queriedEntityRecord["msnfp_telephone2"] != null)
                {
                    jsonDataObj.Telephone2 = (string)queriedEntityRecord["msnfp_telephone2"];
                    localContext.TracingService.Trace("Got msnfp_telephone2.");
                }
                else
                {
                    jsonDataObj.Telephone2 = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_telephone2.");
                }


                if (queriedEntityRecord.Contains("msnfp_dataentryreference") && queriedEntityRecord["msnfp_dataentryreference"] != null)
                {
                    jsonDataObj.DataEntryReference = (string)queriedEntityRecord["msnfp_dataentryreference"];
                    localContext.TracingService.Trace("Got msnfp_dataentryreference.");
                }
                else
                {
                    jsonDataObj.DataEntryReference = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_dataentryreference.");
                }


                if (queriedEntityRecord.Contains("msnfp_invoiceidentifier") && queriedEntityRecord["msnfp_invoiceidentifier"] != null)
                {
                    jsonDataObj.InvoiceIdentifier = (string)queriedEntityRecord["msnfp_invoiceidentifier"];
                    localContext.TracingService.Trace("Got msnfp_invoiceidentifier.");
                }
                else
                {
                    jsonDataObj.InvoiceIdentifier = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_invoiceidentifier.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactionfraudcode") && queriedEntityRecord["msnfp_transactionfraudcode"] != null)
                {
                    jsonDataObj.TransactionFraudCode = (string)queriedEntityRecord["msnfp_transactionfraudcode"];
                    localContext.TracingService.Trace("Got msnfp_transactionfraudcode.");
                }
                else
                {
                    jsonDataObj.TransactionFraudCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionfraudcode.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactionidentifier") && queriedEntityRecord["msnfp_transactionidentifier"] != null)
                {
                    jsonDataObj.TransactionIdentifier = (string)queriedEntityRecord["msnfp_transactionidentifier"];
                    localContext.TracingService.Trace("Got msnfp_transactionidentifier.");
                }
                else
                {
                    jsonDataObj.TransactionIdentifier = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionidentifier.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactionresult") && queriedEntityRecord["msnfp_transactionresult"] != null)
                {
                    jsonDataObj.TransactionResult = (string)queriedEntityRecord["msnfp_transactionresult"];
                    localContext.TracingService.Trace("Got msnfp_transactionresult.");
                }
                else
                {
                    jsonDataObj.TransactionResult = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactionresult.");
                }


                if (queriedEntityRecord.Contains("msnfp_tributecode") && queriedEntityRecord["msnfp_tributecode"] != null)
                {
                    jsonDataObj.TributeCode = ((OptionSetValue)queriedEntityRecord["msnfp_tributecode"]).Value;
                    localContext.TracingService.Trace("Got msnfp_tributecode.");
                }
                else
                {
                    jsonDataObj.TributeCode = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributecode.");
                }


                if (queriedEntityRecord.Contains("msnfp_tributeacknowledgement") && queriedEntityRecord["msnfp_tributeacknowledgement"] != null)
                {
                    jsonDataObj.TributeAcknowledgement = (string)queriedEntityRecord["msnfp_tributeacknowledgement"];
                    localContext.TracingService.Trace("Got msnfp_tributeacknowledgement.");
                }
                else
                {
                    jsonDataObj.TributeAcknowledgement = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributeacknowledgement.");
                }


                if (queriedEntityRecord.Contains("msnfp_tributeid") && queriedEntityRecord["msnfp_tributeid"] != null)
                {
                    jsonDataObj.TributeId = ((EntityReference)queriedEntityRecord["msnfp_tributeid"]).Id;
                    localContext.TracingService.Trace("Got msnfp_tributeid.");
                }
                else
                {
                    jsonDataObj.TributeId = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributeid.");
                }


                if (queriedEntityRecord.Contains("msnfp_tributemessage") && queriedEntityRecord["msnfp_tributemessage"] != null)
                {
                    jsonDataObj.TributeMessage = (string)queriedEntityRecord["msnfp_tributemessage"];
                    localContext.TracingService.Trace("Got msnfp_tributemessage.");
                }
                else
                {
                    jsonDataObj.TributeMessage = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_tributemessage.");
                }


                if (queriedEntityRecord.Contains("msnfp_transactiondescription") && queriedEntityRecord["msnfp_transactiondescription"] != null)
                {
                    jsonDataObj.TransactionDescription = (string)queriedEntityRecord["msnfp_transactiondescription"];
                    localContext.TracingService.Trace("Got msnfp_transactiondescription.");
                }
                else
                {
                    jsonDataObj.TransactionDescription = null;
                    localContext.TracingService.Trace("Did NOT find msnfp_transactiondescription.");
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
                    localContext.TracingService.Trace("Got StateCode");
                }
                else
                {
                    jsonDataObj.StateCode = null;
                    localContext.TracingService.Trace("Did NOT find StateCode");
                }

                if (queriedEntityRecord.Contains("statuscode") && queriedEntityRecord["statuscode"] != null)
                {
                    jsonDataObj.StatusCode = ((OptionSetValue)queriedEntityRecord["statuscode"]).Value;
                    localContext.TracingService.Trace("Got StatusCode");
                }
                else
                {
                    jsonDataObj.StatusCode = null;
                    localContext.TracingService.Trace("Did NOT find StatusCode");
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

                jsonDataObj.Receipt = new HashSet<MSNFP_Receipt>();
                jsonDataObj.Response = new HashSet<MSNFP_Response>();
                jsonDataObj.Transaction = new HashSet<MSNFP_Transaction>();

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
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MSNFP_PaymentSchedule));
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
                localContext.TracingService.Trace("No API URL or Enable Portal Pages. Exiting workflow.");
            }
        }
        #endregion

    }
}

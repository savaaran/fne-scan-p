/*************************************************************************
* © Microsoft. All rights reserved.
*/

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Plugins.Common;

namespace Plugins.PaymentProcesses
{
    public class Constants
    {
        public static string CRYPTTYPE = "7";
        public static string COUNTRY_CODE_CA = "CA";
        public static string COUNTRY_CODE_US = "US";
        public static string APILOGINID = "7449wRGTz6k";
        public static string APITRANSACTIONKEY = "72XJ978tvsQ4dLR9";
        public static string AZUREWEBAPIURL = "https://donationportalapi.azurewebsites.net/";

        public static int CONTACT_ADDRESS_TYPE_PRIMARY = 1;
        public static int CONTACT_ADDRESS_TYPE_HOME = 2;
        public static int CONTACT_ADDRESS_TYPE_BUSINESS = 3;
        public static int CONTACT_ADDRESS_TYPE_SEASONAL = 4;
        public static int CONTACT_ADDRESS_TYPE_ALTERNATEHOME = 844060000;
        public static int CONTACT_ADDRESS_TYPE_ALTERNATEBUSINESS = 844060001;

        public static int ACCOUNT_ADDRESS_TYPE_PRIMARY = 1;
        public static int ACCOUNT_ADDRESS_TYPE_BUSINESS = 2;
        public static int ACCOUNT_ADDRESS_TYPE_ALTERNATEBUSINESS = 3;
        public static int ACCOUNT_ADDRESS_TYPE_OTHER = 4;

    }

    public class Utilities
    {
        public enum HouseholdRelationshipType
        {
            PrimaryHouseholdMember = 844060000,
            Member,
            Minor,
            Deceased
        }

        public enum AccountType
        {
            Household = 844060000,
            Organization
        }

        public enum DonationImportStatusReason
        {
            Ready = 844060000,
            Failed,
            Active,
            Inactive
        }

        public enum ContactAddressTypeCode
        {
            Primary = 844060000,
            Home,
            Business,
            Seasonal,
            AlternateHome,
            AlternateBusiness
        }

        public enum GivingLevelCalculation
        {
            CurrentCalendar = 844060000,
            CurrentFiscalYear,
            YearToDate
        }

        private enum ShowAPIErrorMessages
        {
            Yes = 844060000,
            No
        }

        public static string GetAzureWebAPIURL(IOrganizationService service, IPluginExecutionContext context)
        {
            string azureWebApiUrl = string.Empty;

            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            Guid currentUserID = context.InitiatingUserId;
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));
            if (user.Contains("msnfp_configurationid"))
            {
                Entity config = (from c in orgSvcContext.CreateQuery("msnfp_configuration")
                                 where (Guid)c["msnfp_configurationid"] == ((EntityReference)user["msnfp_configurationid"]).Id
                                 orderby c["createdon"] descending
                                 select c).FirstOrDefault();

                if (config != null)
                {
                    azureWebApiUrl = config.Contains("msnfp_azure_webapiurl") ? (string)config["msnfp_azure_webapiurl"] : string.Empty;
                }
            }

            return azureWebApiUrl;
        }

        internal void RecalculateGiftBatch(EntityReference giftBatchRef, IOrganizationService service, ITracingService tracingService)
        {
            // we only proceed if the Gift Batch's status is In Progress
            Entity giftBatch = service.Retrieve(giftBatchRef.LogicalName, giftBatchRef.Id, new ColumnSet("statuscode"));
            tracingService.Trace("Gift Batch Id:" + giftBatchRef.Id + ", statuscode:" + giftBatch.GetAttributeValue<OptionSetValue>("statuscode").Value);

            if (giftBatch.GetAttributeValue<OptionSetValue>("statuscode").Value == 1) // in progress
            {
                int transactionCount = 0;
                decimal transactionTotalAmount = 0;
                decimal transactionTotalNonReceiptableAmount = 0;
                decimal transactionTotalReceiptedAmount = 0;
                decimal transactionTotalMembershipAmount = 0;
                // get the list of all active transactions for the gift batch
                QueryByAttribute transactionsQuery = new QueryByAttribute("msnfp_transaction");
                transactionsQuery.AddAttributeValue("msnfp_giftbatchid", giftBatchRef.Id);
                transactionsQuery.AddAttributeValue("statecode", 0);
                transactionsQuery.ColumnSet = new ColumnSet("msnfp_amount", "msnfp_amount_nonreceiptable", "msnfp_amount_receipted", "msnfp_amount_membership");
                var result = service.RetrieveMultiple(transactionsQuery);
                if (result != null && result.Entities.Count > 0)
                {
                    transactionCount = result.Entities.Count;
                    tracingService.Trace($"Found {transactionCount} Transactions associated with the Gift Batch.");

                    transactionTotalAmount = result.Entities.Where(w => w.GetAttributeValue<Money>("msnfp_amount") != null)
                                                            .Sum(t => t.GetAttributeValue<Money>("msnfp_amount").Value);
                    tracingService.Trace($"Transaction Total Amount {transactionTotalAmount}");

                    transactionTotalNonReceiptableAmount = result.Entities.Where(w => w.GetAttributeValue<Money>("msnfp_amount_nonreceiptable") != null)
                                                                          .Sum(t => t.GetAttributeValue<Money>("msnfp_amount_nonreceiptable").Value);
                    tracingService.Trace($"Transaction Total Non-Receiptable Amount {transactionTotalNonReceiptableAmount}");

                    transactionTotalReceiptedAmount = result.Entities.Where(w => w.GetAttributeValue<Money>("msnfp_amount_receipted") != null)
                                                                     .Sum(t => t.GetAttributeValue<Money>("msnfp_amount_receipted").Value);
                    tracingService.Trace($"Transaction Total Receipted Amount {transactionTotalReceiptedAmount}");

                    transactionTotalMembershipAmount = result.Entities.Where(w => w.GetAttributeValue<Money>("msnfp_amount_membership") != null)
                                                                      .Sum(t => t.GetAttributeValue<Money>("msnfp_amount_membership").Value);
                    tracingService.Trace($"Transaction Total Membership Amount {transactionTotalMembershipAmount}");
                }

                Entity giftBatchToUpdate = new Entity(giftBatchRef.LogicalName, giftBatchRef.Id);
                giftBatchToUpdate["msnfp_tally_gifts"] = transactionCount;
                giftBatchToUpdate["msnfp_tally_amount"] = transactionTotalAmount;
                giftBatchToUpdate["msnfp_tally_amount_nonreceiptable"] = transactionTotalNonReceiptableAmount;
                giftBatchToUpdate["msnfp_tally_amount_receipted"] = transactionTotalReceiptedAmount;
                giftBatchToUpdate["msnfp_tally_amount_membership"] = transactionTotalMembershipAmount;
                tracingService.Trace("Updating Gift Batch.");
                service.Update(giftBatchToUpdate);
                tracingService.Trace("Done.");

            }
        }

        //public static bool GetEnablePortal(IOrganizationService service, IPluginExecutionContext context)
        //{
        //    bool enablePortal = false;
        //    OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
        //    Guid currentUserID = context.InitiatingUserId;
        //    Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));
        //    if (user.Contains("msnfp_configurationid"))
        //    {
        //        Entity config = (from c in orgSvcContext.CreateQuery("msnfp_configuration")
        //                         where (Guid)c["msnfp_configurationid"] == ((EntityReference)user["msnfp_configurationid"]).Id
        //                         orderby c["createdon"] descending
        //                         select c).FirstOrDefault();

        //        if (config != null)
        //        {
        //            enablePortal = true;
        //        }
        //    }

        //    return enablePortal;
        //}

        public static string GetOptionsetText(Entity entity, IOrganizationService service, string optionsetName, int optionsetValue)
        {
            string optionsetSelectedText = string.Empty;
            try
            {
                RetrieveOptionSetRequest retrieveOptionSetRequest = new RetrieveOptionSetRequest
                {
                    Name = optionsetName
                };

                // Execute the request.
                RetrieveOptionSetResponse retrieveOptionSetResponse =
                (RetrieveOptionSetResponse)service.Execute(retrieveOptionSetRequest);

                // Access the retrieved OptionSetMetadata.
                OptionSetMetadata retrievedOptionSetMetadata = (OptionSetMetadata)retrieveOptionSetResponse.OptionSetMetadata;

                // Get the current options list for the retrieved attribute.
                OptionMetadata[] optionList = retrievedOptionSetMetadata.Options.ToArray();
                foreach (OptionMetadata optionMetadata in optionList)
                {
                    if (optionMetadata.Value == optionsetValue)
                    {
                        optionsetSelectedText = optionMetadata.Label.UserLocalizedLabel.Label.ToString();
                        break;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return optionsetSelectedText;
        }

        public static string GetOptionSetValueLabel(string entityName, string fieldName, int optionSetValue, IOrganizationService service)
        {

            var attReq = new RetrieveAttributeRequest();
            attReq.EntityLogicalName = entityName;
            attReq.LogicalName = fieldName;
            attReq.RetrieveAsIfPublished = false;

            var attResponse = (RetrieveAttributeResponse)service.Execute(attReq);
            var attMetadata = (EnumAttributeMetadata)attResponse.AttributeMetadata;

            return attMetadata.OptionSet.Options.Where(x => x.Value == optionSetValue).FirstOrDefault().Label.UserLocalizedLabel.Label;

        }

        public Guid CreateVIPAlertTask(IOrganizationService service, Entity Cust, Entity primaryRecord,
            Entity owner)//, LocalPluginContext localContext)
        {
            Entity task = new Entity("msnfp_vipalert");
            string name = string.Empty;
            if (Cust.LogicalName == "contact")
            {
                name = (string)Cust["firstname"] + " " + (string)Cust["lastname"];
            }
            else if (Cust.LogicalName == "account")
            {
                name = (string)Cust["name"];
            }

            task["subject"] = "VIP Alert for " + name + " on the " + ((DateTime)primaryRecord["createdon"]).ToLocalTime().ToShortDateString();
            task["description"] = "Constituent / Organization " + name + " has been marked as a VIP and therefore triggered this task";

            if (owner.LogicalName == "systemuser")
                task["ownerid"] = new EntityReference("systemuser", owner.Id);
            else
                task["ownerid"] = new EntityReference("team", owner.Id);

            task["regardingobjectid"] = new EntityReference(primaryRecord.LogicalName, primaryRecord.Id);

            return service.Create(task);
        }

        public Guid CreateVIPAlertEmail(IOrganizationService service, Entity Cust, Entity primaryRecord,
            Entity owner, Entity config)//, LocalPluginContext localContext)
        {
            Entity email = new Entity("email");
            string name = string.Empty;
            string emailDescription = string.Empty;
            string url = string.Empty;
            Guid emailGuid = Guid.Empty;

            string orgURL = config.Contains("msnfp_organizationurl") ? (string)config["msnfp_organizationurl"] : string.Empty;
            if (!string.IsNullOrEmpty(orgURL))
            {
                if (Cust.LogicalName == "contact")
                {
                    name = (string)Cust["firstname"] + " " + (string)Cust["lastname"];
                    url = string.Format("<a href='" + orgURL + "/main.aspx?etn=contact&id=%7b{0}%7d&newWindow=true&pagetype=entityrecord' target='_blank' style='cursor: pointer;'>{1}</a>", Cust.Id.ToString(), name);
                }
                else if (Cust.LogicalName == "account")
                {
                    name = (string)Cust["name"];
                    url = string.Format("<a href='" + orgURL + "/main.aspx?etn=account&id=%7b{0}%7d&newWindow=true&pagetype=entityrecord' target='_blank' style='cursor: pointer;'>{1}</a>", Cust.Id.ToString(), name);
                }

                emailDescription = "Constituent / Organization " + name + " has been marked as a VIP and therefore triggered this task<br/><br/>";
                emailDescription += "Link to the Constituent / Organization record: " + url;

                email["activitytypecode"] = new OptionSetValue(4202);
                email["subject"] = "VIP Alert for " + name + " on the " + ((DateTime)primaryRecord["createdon"]).ToLocalTime().ToShortDateString();
                email["description"] = emailDescription;

                EntityCollection toPartyList = new EntityCollection();
                Entity toParty1 = new Entity("activityparty");
                toParty1["partyid"] = new EntityReference("systemuser", owner.Id);
                toPartyList.Entities.Add(toParty1);

                email["to"] = toPartyList;
                email["regardingobjectid"] = new EntityReference(primaryRecord.LogicalName, primaryRecord.Id);

                emailGuid = service.Create(email);

                SendEmailRequest sendEmailreq = new SendEmailRequest
                {
                    EmailId = emailGuid,
                    TrackingToken = "",
                    IssueSend = true
                };
                SendEmailResponse sendEmailresp = (SendEmailResponse)service.Execute(sendEmailreq);
            }
            return emailGuid;
        }


        public void UpdateDonorCommitmentBalance(OrganizationServiceContext orgSvcContext, IOrganizationService service, EntityReference donorCommitment, int type)
        {
            decimal valTotalAmount = 0;
            decimal valSumBalanceAmount = 0;
            decimal valBalanceAmount = 0;

            //get donor commitment record
            Entity donorCommitmentRecord = service.Retrieve("msnfp_donorcommitment", donorCommitment.Id, new ColumnSet(new string[] { "msnfp_donorcommitmentid", "msnfp_totalamount", "msnfp_totalamount_balance", "msnfp_totalamount_paid" }));

            if (donorCommitmentRecord != null)
            {
                if (donorCommitmentRecord.Contains("msnfp_totalamount"))
                    valTotalAmount = ((Money)donorCommitmentRecord["msnfp_totalamount"]).Value;

                List<Entity> transactionList = (from a in orgSvcContext.CreateQuery("msnfp_transaction")
                                                where ((EntityReference)a["msnfp_donorcommitmentid"]).Id == donorCommitmentRecord.Id
                                                && (((OptionSetValue)a["statuscode"]).Value == 844060000 || ((OptionSetValue)a["statuscode"]).Value == 844060004) // completed or refunded
                                                select a).ToList();

                foreach (Entity item in transactionList)
                {
                    if (item.Contains("msnfp_amount_receipted"))
                        valSumBalanceAmount += ((Money)item["msnfp_amount_receipted"]).Value;

                    if (item.Contains("msnfp_amount_membership"))
                        valSumBalanceAmount += ((Money)item["msnfp_amount_membership"]).Value;

                    if (item.Contains("msnfp_amount_nonreceiptable"))
                        valSumBalanceAmount += ((Money)item["msnfp_amount_nonreceiptable"]).Value;

                    if (item.Contains("msnfp_amount_tax"))
                        valSumBalanceAmount += ((Money)item["msnfp_amount_tax"]).Value;
                }

                valBalanceAmount = valTotalAmount - valSumBalanceAmount;
                donorCommitmentRecord["msnfp_totalamount_balance"] = new Money(valBalanceAmount);
                donorCommitmentRecord["msnfp_totalamount_paid"] = new Money(valSumBalanceAmount);

                if (valBalanceAmount <= 0)
                    donorCommitmentRecord["statuscode"] = new OptionSetValue(844060000); // completed
                else
                {
                    if (type == 1) // refund, failed or dissassociated pledge
                        donorCommitmentRecord["statuscode"] = new OptionSetValue(1); // active
                }
                service.Update(donorCommitmentRecord);
            }
        }

        public static void UpdateHouseholdOnRecord(IOrganizationService service, Entity record, string householdAttributeName, string customerAttributeName)
        {
            // If customer is contact
            if (record.GetAttributeValue<EntityReference>(customerAttributeName) != null && string.Equals(record.GetAttributeValue<EntityReference>(customerAttributeName).LogicalName, "contact", StringComparison.OrdinalIgnoreCase))
            {
                Entity donorHousehold = service.Retrieve(record.GetAttributeValue<EntityReference>(customerAttributeName).LogicalName, record.GetAttributeValue<EntityReference>(customerAttributeName).Id, new ColumnSet(householdAttributeName));
                if (donorHousehold.GetAttributeValue<EntityReference>(householdAttributeName) != null)
                {
                    Entity updatedRecord = new Entity(record.LogicalName, record.Id);
                    updatedRecord[householdAttributeName] = donorHousehold.GetAttributeValue<EntityReference>(householdAttributeName);
                    service.Update(updatedRecord);
                }
            }
        }

        public void UpdatePaymentScheduleBalance(OrganizationServiceContext orgSvcContext, IOrganizationService service, Entity donorCommitment, EntityReference paymentSchedule, string message)
        {
            // Without getting all existing donor commitments older totals on the payment schedule will keep getting incremented
            QueryExpression query = new QueryExpression("msnfp_paymentschedule");
            query.NoLock = true;
            query.ColumnSet = new ColumnSet("msnfp_paymentscheduleid", "msnfp_totalamount_balance", "msnfp_totalamount_paid", "msnfp_totalamount");
            query.Criteria.AddCondition(new ConditionExpression("msnfp_paymentscheduleid", ConditionOperator.Equal, paymentSchedule.Id));
            query.LinkEntities.Add(new LinkEntity
            {
                EntityAlias = "donorCommitment",
                JoinOperator = JoinOperator.LeftOuter,
                Columns = new ColumnSet("msnfp_totalamount_paid", "msnfp_totalamount_balance", "msnfp_totalamount"),
                LinkCriteria = new FilterExpression
                {
                    Conditions =
                    {
                        // Donor commitments that are not cancelled
                        new ConditionExpression("statuscode",ConditionOperator.NotEqual,844060001)
                    }
                },
                LinkToEntityName = "msnfp_donorcommitment",
                LinkToAttributeName = "msnfp_parentscheduleid",
                LinkFromAttributeName = "msnfp_paymentscheduleid",
                LinkFromEntityName = "msnfp_paymentschedule"
            });

            EntityCollection paymentScheduleInfo = service.RetrieveMultiple(query);
            var paymentScheduleRelatedData = paymentScheduleInfo.Entities.GroupBy(g => g.Id).Select(s => new
            {
                TotalAmountBalance = s.FirstOrDefault().GetAttributeValue<Money>("msnfp_totalamount_balance"),
                TotalAmountPaid = s.FirstOrDefault().GetAttributeValue<Money>("msnfp_totalamount_paid"),
                TotalAmount = s.FirstOrDefault().GetAttributeValue<Money>("msnfp_totalamount"),

                TotalAmountBalanceDC = s.Where(w => w.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount_balance") != null
                                         && ((Money)w.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount_balance").Value) != null)
                                        .Sum(su => ((Money)su.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount_balance").Value).Value),

                TotalAmountPaidDC = s.Where(w => w.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount_paid") != null
                                         && ((Money)w.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount_paid").Value) != null)
                                        .Sum(su => ((Money)su.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount_paid").Value).Value),

                TotalAmountDC = s.Where(w => w.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount") != null
                 && ((Money)w.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount").Value) != null)
                                        .Sum(su => ((Money)su.GetAttributeValue<AliasedValue>("donorCommitment.msnfp_totalamount").Value).Value)
            }).FirstOrDefault();

            if (paymentScheduleRelatedData != null)
            {
                Entity paymentScheduleRecord = new Entity(paymentSchedule.LogicalName, paymentSchedule.Id);
                paymentScheduleRecord["msnfp_totalamount"] = new Money(paymentScheduleRelatedData.TotalAmountDC);
                paymentScheduleRecord["msnfp_totalamount_balance"] = new Money(paymentScheduleRelatedData.TotalAmountBalanceDC);
                paymentScheduleRecord["msnfp_totalamount_paid"] = new Money(paymentScheduleRelatedData.TotalAmountPaidDC);

                service.Update(paymentScheduleRecord);
            }
        }

        /// <summary>
        /// Returns a flag to determine if any error messages from the API should be presented as Plugin exceptions
        /// </summary>
        /// <param name="showAPIErrorMessageOptionSet">Configuration.ShowAPIErrorResponses</param>
        /// <returns>Boolean</returns>
        private bool ShowAPIErrorResponses(OptionSetValue showAPIErrorMessageOptionSet)
        {
            bool returnValue = false;
            if (showAPIErrorMessageOptionSet == null)
                return returnValue;

            returnValue = showAPIErrorMessageOptionSet.Value == (int)ShowAPIErrorMessages.Yes;

            return returnValue;
        }

        /// <summary>
        /// This will throw an exception if there is a problem with the syncing of an entity to the API. Note that throwing an exception will result in the Dynamics record NOT being created.
        /// </summary>
        public void CheckAPIReturnJSONForErrors(string returnedResult, OptionSetValue showAPIErrorMessage, ITracingService tracingService)
        {

            string errorMessage = string.Empty;

            if (returnedResult.Length > 0)
            {
                //string[] arrReturnedResult = returnedResult.Split(',');

                //// The results are always in the same slots:
                //if (arrReturnedResult.Length > 7)
                //{
                //    if (arrReturnedResult[7].Split(':')[1].Contains("200") && arrReturnedResult[8].Split(':')[1].ToLower().Contains("ok"))
                //    {
                //        // All good.
                //        return;
                //    }
                //    else
                //    {
                //        throw new InvalidPluginExecutionException("An API syncing error has occured. Returned result: " + returnedResult);
                //    }
                //}
                JObject returnedResultObject = JObject.Parse(returnedResult);
                int statusCode = (int)returnedResultObject["statusCode"];
                if (statusCode == 200)
                {
                    // all good
                    return;
                }
                else
                {
                    errorMessage = $"An API syncing error has occured. Returned result: {returnedResult}";
                }

            }
            else
            {
                errorMessage = "An API syncing error has occured. Returned result contains no value. Please ensure the API URL in the configuration record is correct";
            }

            tracingService.Trace(errorMessage);

            if (ShowAPIErrorResponses(showAPIErrorMessage))
            {
                throw new InvalidPluginExecutionException(errorMessage);
            }
        }

        public void CreateAddressChange(IOrganizationService service, Entity prePrimaryRecord, Entity postPrimaryRecord, int Type, ITracingService tracingService)
        {
            tracingService.Trace("Entering Address Change method");
            tracingService.Trace("Record Type:" + postPrimaryRecord.LogicalName);
            tracingService.Trace("Address Change Type:" + Type);

            Entity addressChange = new Entity("msnfp_addresschange");
            int addressTypeFrom = 0;
            int addressTypeTo = 0;

            if (Type == 1)
            {
                addressChange["msnfp_from_line1"] = prePrimaryRecord.Contains("address1_line1") ? (string)prePrimaryRecord["address1_line1"] : string.Empty;
                addressChange["msnfp_from_line2"] = prePrimaryRecord.Contains("address1_line2") ? (string)prePrimaryRecord["address1_line2"] : string.Empty;
                addressChange["msnfp_from_line3"] = prePrimaryRecord.Contains("address1_line3") ? (string)prePrimaryRecord["address1_line3"] : string.Empty;
                addressChange["msnfp_from_city"] = prePrimaryRecord.Contains("address1_city") ? (string)prePrimaryRecord["address1_city"] : string.Empty;
                addressChange["msnfp_from_postalcode"] = prePrimaryRecord.Contains("address1_postalcode") ? (string)prePrimaryRecord["address1_postalcode"] : string.Empty;
                addressChange["msnfp_from_stateorprovince"] = prePrimaryRecord.Contains("address1_stateorprovince") ? (string)prePrimaryRecord["address1_stateorprovince"] : string.Empty;
                addressChange["msnfp_from_country"] = prePrimaryRecord.Contains("address1_country") ? (string)prePrimaryRecord["address1_country"] : string.Empty;

                addressChange["msnfp_to_line1"] = postPrimaryRecord.Contains("address1_line1") ? (string)postPrimaryRecord["address1_line1"] : string.Empty;
                addressChange["msnfp_to_line2"] = postPrimaryRecord.Contains("address1_line2") ? (string)postPrimaryRecord["address1_line2"] : string.Empty;
                addressChange["msnfp_to_line3"] = postPrimaryRecord.Contains("address1_line3") ? (string)postPrimaryRecord["address1_line3"] : string.Empty;
                addressChange["msnfp_to_city"] = postPrimaryRecord.Contains("address1_city") ? (string)postPrimaryRecord["address1_city"] : string.Empty;
                addressChange["msnfp_to_postalcode"] = postPrimaryRecord.Contains("address1_postalcode") ? (string)postPrimaryRecord["address1_postalcode"] : string.Empty;
                addressChange["msnfp_to_stateorprovince"] = postPrimaryRecord.Contains("address1_stateorprovince") ? (string)postPrimaryRecord["address1_stateorprovince"] : string.Empty;
                addressChange["msnfp_to_country"] = postPrimaryRecord.Contains("address1_country") ? (string)postPrimaryRecord["address1_country"] : string.Empty;
            }
            else if (Type == 2)
            {
                addressChange["msnfp_from_line1"] = prePrimaryRecord.Contains("address2_line1") ? (string)prePrimaryRecord["address2_line1"] : string.Empty;
                addressChange["msnfp_from_line2"] = prePrimaryRecord.Contains("address2_line2") ? (string)prePrimaryRecord["address2_line2"] : string.Empty;
                addressChange["msnfp_from_line3"] = prePrimaryRecord.Contains("address2_line3") ? (string)prePrimaryRecord["address2_line3"] : string.Empty;
                addressChange["msnfp_from_city"] = prePrimaryRecord.Contains("address2_city") ? (string)prePrimaryRecord["address2_city"] : string.Empty;
                addressChange["msnfp_from_postalcode"] = prePrimaryRecord.Contains("address2_postalcode") ? (string)prePrimaryRecord["address2_postalcode"] : string.Empty;
                addressChange["msnfp_from_stateorprovince"] = prePrimaryRecord.Contains("address2_stateorprovince") ? (string)prePrimaryRecord["address2_stateorprovince"] : string.Empty;
                addressChange["msnfp_from_country"] = prePrimaryRecord.Contains("address2_country") ? (string)prePrimaryRecord["address2_country"] : string.Empty;

                addressChange["msnfp_to_line1"] = postPrimaryRecord.Contains("address2_line1") ? (string)postPrimaryRecord["address2_line1"] : string.Empty;
                addressChange["msnfp_to_line2"] = postPrimaryRecord.Contains("address2_line2") ? (string)postPrimaryRecord["address2_line2"] : string.Empty;
                addressChange["msnfp_to_line3"] = postPrimaryRecord.Contains("address2_line3") ? (string)postPrimaryRecord["address2_line3"] : string.Empty;
                addressChange["msnfp_to_city"] = postPrimaryRecord.Contains("address2_city") ? (string)postPrimaryRecord["address2_city"] : string.Empty;
                addressChange["msnfp_to_postalcode"] = postPrimaryRecord.Contains("address2_postalcode") ? (string)postPrimaryRecord["address2_postalcode"] : string.Empty;
                addressChange["msnfp_to_stateorprovince"] = postPrimaryRecord.Contains("address2_stateorprovince") ? (string)postPrimaryRecord["address2_stateorprovince"] : string.Empty;
                addressChange["msnfp_to_country"] = postPrimaryRecord.Contains("address2_country") ? (string)postPrimaryRecord["address2_country"] : string.Empty;
            }
            else if (Type == 3)
            {
                addressChange["msnfp_from_line1"] = prePrimaryRecord.Contains("address3_line1") ? (string)prePrimaryRecord["address3_line1"] : string.Empty;
                addressChange["msnfp_from_line2"] = prePrimaryRecord.Contains("address3_line2") ? (string)prePrimaryRecord["address3_line2"] : string.Empty;
                addressChange["msnfp_from_line3"] = prePrimaryRecord.Contains("address3_line3") ? (string)prePrimaryRecord["address3_line3"] : string.Empty;
                addressChange["msnfp_from_city"] = prePrimaryRecord.Contains("address3_city") ? (string)prePrimaryRecord["address3_city"] : string.Empty;
                addressChange["msnfp_from_postalcode"] = prePrimaryRecord.Contains("address3_postalcode") ? (string)prePrimaryRecord["address3_postalcode"] : string.Empty;
                addressChange["msnfp_from_stateorprovince"] = prePrimaryRecord.Contains("address3_stateorprovince") ? (string)prePrimaryRecord["address3_stateorprovince"] : string.Empty;
                addressChange["msnfp_from_country"] = prePrimaryRecord.Contains("address3_country") ? (string)prePrimaryRecord["address3_country"] : string.Empty;

                addressChange["msnfp_to_line1"] = postPrimaryRecord.Contains("address3_line1") ? (string)postPrimaryRecord["address3_line1"] : string.Empty;
                addressChange["msnfp_to_line2"] = postPrimaryRecord.Contains("address3_line2") ? (string)postPrimaryRecord["address3_line2"] : string.Empty;
                addressChange["msnfp_to_line3"] = postPrimaryRecord.Contains("address3_line3") ? (string)postPrimaryRecord["address3_line3"] : string.Empty;
                addressChange["msnfp_to_city"] = postPrimaryRecord.Contains("address3_city") ? (string)postPrimaryRecord["address3_city"] : string.Empty;
                addressChange["msnfp_to_postalcode"] = postPrimaryRecord.Contains("address3_postalcode") ? (string)postPrimaryRecord["address3_postalcode"] : string.Empty;
                addressChange["msnfp_to_stateorprovince"] = postPrimaryRecord.Contains("address3_stateorprovince") ? (string)postPrimaryRecord["address3_stateorprovince"] : string.Empty;
                addressChange["msnfp_to_country"] = postPrimaryRecord.Contains("address3_country") ? (string)postPrimaryRecord["address3_country"] : string.Empty;
            }
            tracingService.Trace("Copied Main Address Fields to Address Change Object.");

            string name = string.Empty;
            if (postPrimaryRecord.LogicalName == "contact")
            {
                if (postPrimaryRecord.Contains("firstname"))
                    name = (string)postPrimaryRecord["firstname"] + " ";

                name += (string)postPrimaryRecord["lastname"];

                // processing address type - start
                if (postPrimaryRecord.Contains("address1_addresstypecode") || postPrimaryRecord.Contains("address2_addresstypecode") || postPrimaryRecord.Contains("address3_addresstypecode"))
                {
                    //if (Type == 1)
                    //    addressTypeFrom = ((OptionSetValue)postPrimaryRecord["address1_addresstypecode"]).Value;
                    //else if (Type == 2)
                    //    addressTypeFrom = ((OptionSetValue)postPrimaryRecord["address2_addresstypecode"]).Value;
                    //else if (Type == 3)
                    //    addressTypeFrom = ((OptionSetValue)postPrimaryRecord["address3_addresstypecode"]).Value;

                    if (postPrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode") != null)
                    {
                        addressTypeFrom = postPrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode").Value;
                    }

                    if (addressTypeFrom == Constants.CONTACT_ADDRESS_TYPE_PRIMARY)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060000);
                    else if (addressTypeFrom == Constants.CONTACT_ADDRESS_TYPE_HOME)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060001);
                    else if (addressTypeFrom == Constants.CONTACT_ADDRESS_TYPE_BUSINESS)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060002);
                    else if (addressTypeFrom == Constants.CONTACT_ADDRESS_TYPE_SEASONAL)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060003);
                    else if (addressTypeFrom == Constants.CONTACT_ADDRESS_TYPE_ALTERNATEHOME)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060004);
                    else if (addressTypeFrom == Constants.CONTACT_ADDRESS_TYPE_ALTERNATEBUSINESS)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060005);

                    tracingService.Trace("Got To Address Type Code for Contact");
                }

                if (prePrimaryRecord.Contains("address1_addresstypecode") || prePrimaryRecord.Contains("address2_addresstypecode") || prePrimaryRecord.Contains("address3_addresstypecode"))
                {
                    //if (Type == 1)
                    //    addressTypeTo = ((OptionSetValue)prePrimaryRecord["address1_addresstypecode"]).Value;
                    //else if (Type == 2)
                    //    addressTypeTo = ((OptionSetValue)prePrimaryRecord["address2_addresstypecode"]).Value;
                    //else if (Type == 3)
                    //    addressTypeTo = ((OptionSetValue)prePrimaryRecord["address3_addresstypecode"]).Value;

                    if (prePrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode") != null)
                    {
                        addressTypeFrom = prePrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode").Value;
                    }


                    if (addressTypeTo == Constants.CONTACT_ADDRESS_TYPE_PRIMARY)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060000);
                    else if (addressTypeTo == Constants.CONTACT_ADDRESS_TYPE_HOME)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060001);
                    else if (addressTypeTo == Constants.CONTACT_ADDRESS_TYPE_BUSINESS)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060002);
                    else if (addressTypeTo == Constants.CONTACT_ADDRESS_TYPE_SEASONAL)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060003);
                    else if (addressTypeTo == Constants.CONTACT_ADDRESS_TYPE_ALTERNATEHOME)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060004);
                    else if (addressTypeTo == Constants.CONTACT_ADDRESS_TYPE_ALTERNATEBUSINESS)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060005);

                    tracingService.Trace("Got From Address Type Code for Contact");
                }
                // processing address type - end
            }
            else if (postPrimaryRecord.LogicalName == "account")
            {
                if (postPrimaryRecord.Contains("name"))
                    name = (string)postPrimaryRecord["name"];

                // processing address type - start
                if (postPrimaryRecord.Contains("address1_addresstypecode") || postPrimaryRecord.Contains("address2_addresstypecode"))
                {
                    //if (Type == 1)
                    //    addressTypeFrom = ((OptionSetValue)postPrimaryRecord["address1_addresstypecode"]).Value;
                    //else if (Type == 2)
                    //    addressTypeFrom = ((OptionSetValue)postPrimaryRecord["address2_addresstypecode"]).Value;

                    if (postPrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode") != null)
                    {
                        addressTypeFrom = postPrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode").Value;
                    }

                    if (addressTypeFrom == Constants.ACCOUNT_ADDRESS_TYPE_PRIMARY)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060000);
                    else if (addressTypeFrom == Constants.ACCOUNT_ADDRESS_TYPE_BUSINESS)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060002);
                    else if (addressTypeFrom == Constants.ACCOUNT_ADDRESS_TYPE_ALTERNATEBUSINESS)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060005);
                    else if (addressTypeFrom == Constants.ACCOUNT_ADDRESS_TYPE_OTHER)
                        addressChange["msnfp_to_addresstypecode"] = new OptionSetValue(844060006);

                    tracingService.Trace("Got To Address Type Code for Account");
                }

                if (prePrimaryRecord.Contains("address1_addresstypecode") || prePrimaryRecord.Contains("address2_addresstypecode"))
                {
                    //if (Type == 1)
                    //    addressTypeTo = ((OptionSetValue)prePrimaryRecord["address1_addresstypecode"]).Value;
                    //else if (Type == 2)
                    //    addressTypeTo = ((OptionSetValue)prePrimaryRecord["address2_addresstypecode"]).Value;

                    if (prePrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode") != null)
                    {
                        addressTypeFrom = prePrimaryRecord.GetAttributeValue<OptionSetValue>("address" + Type + "addresstypecode").Value;
                    }

                    if (addressTypeTo == Constants.ACCOUNT_ADDRESS_TYPE_PRIMARY)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060000);
                    else if (addressTypeTo == Constants.ACCOUNT_ADDRESS_TYPE_BUSINESS)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060002);
                    else if (addressTypeTo == Constants.ACCOUNT_ADDRESS_TYPE_ALTERNATEBUSINESS)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060005);
                    else if (addressTypeTo == Constants.ACCOUNT_ADDRESS_TYPE_OTHER)
                        addressChange["msnfp_from_addresstypecode"] = new OptionSetValue(844060006);

                    tracingService.Trace("Got From Address Type Code for Account");
                }
                // processing address type - end
            }

            addressChange["subject"] = "Address Change for " + name + " on the " + DateTime.Now.ToShortDateString();
            addressChange["regardingobjectid"] = new EntityReference(prePrimaryRecord.LogicalName, prePrimaryRecord.Id);

            service.Create(addressChange);

            tracingService.Trace("Exiting Address Change method");
        }


        public Guid CreateAddressChangeAlertTask(IOrganizationService service, Entity prePrimaryRecord, Entity postPrimaryRecord,
            EntityCollection marketingList, Entity contactOwner)
        {
            Entity addressChangeAlert = new Entity("msnfp_addresschange");

            addressChangeAlert["msnfp_from_line1"] = prePrimaryRecord.Contains("address1_line1") ? (string)prePrimaryRecord["address1_line1"] : string.Empty;
            addressChangeAlert["msnfp_from_line2"] = prePrimaryRecord.Contains("address1_line2") ? (string)prePrimaryRecord["address1_line2"] : string.Empty;
            addressChangeAlert["msnfp_from_line3"] = prePrimaryRecord.Contains("address1_line3") ? (string)prePrimaryRecord["address1_line3"] : string.Empty;
            addressChangeAlert["msnfp_from_city"] = prePrimaryRecord.Contains("address1_city") ? (string)prePrimaryRecord["address1_city"] : string.Empty;
            addressChangeAlert["msnfp_from_postalcode"] = prePrimaryRecord.Contains("address1_postalcode") ? (string)prePrimaryRecord["address1_postalcode"] : string.Empty;
            addressChangeAlert["msnfp_from_stateorprovince"] = prePrimaryRecord.Contains("address1_stateorprovince") ? (string)prePrimaryRecord["address1_stateorprovince"] : string.Empty;
            addressChangeAlert["msnfp_from_country"] = prePrimaryRecord.Contains("address1_country") ? (string)prePrimaryRecord["address1_country"] : string.Empty;

            addressChangeAlert["msnfp_to_line1"] = postPrimaryRecord.Contains("address1_line1") ? (string)postPrimaryRecord["address1_line1"] : string.Empty;
            addressChangeAlert["msnfp_to_line2"] = postPrimaryRecord.Contains("address1_line2") ? (string)postPrimaryRecord["address1_line2"] : string.Empty;
            addressChangeAlert["msnfp_to_line3"] = postPrimaryRecord.Contains("address1_line3") ? (string)postPrimaryRecord["address1_line3"] : string.Empty;
            addressChangeAlert["msnfp_to_city"] = postPrimaryRecord.Contains("address1_city") ? (string)postPrimaryRecord["address1_city"] : string.Empty;
            addressChangeAlert["msnfp_to_postalcode"] = postPrimaryRecord.Contains("address1_postalcode") ? (string)postPrimaryRecord["address1_postalcode"] : string.Empty;
            addressChangeAlert["msnfp_to_stateorprovince"] = postPrimaryRecord.Contains("address1_stateorprovince") ? (string)postPrimaryRecord["address1_stateorprovince"] : string.Empty;
            addressChangeAlert["msnfp_to_country"] = postPrimaryRecord.Contains("address1_country") ? (string)postPrimaryRecord["address1_country"] : string.Empty;

            string name = string.Empty;
            if (postPrimaryRecord.LogicalName == "contact")
            {
                if (postPrimaryRecord.Contains("firstname"))
                    name = (string)postPrimaryRecord["firstname"] + " ";

                name += (string)postPrimaryRecord["lastname"];
            }
            else if (postPrimaryRecord.LogicalName == "account" && postPrimaryRecord.Contains("name"))
            {
                name = (string)postPrimaryRecord["name"];
            }

            addressChangeAlert["subject"] = "Address Change for " + name + " on the " + DateTime.Now.ToShortDateString();

            StringBuilder description = new StringBuilder();

            if (marketingList.Entities.Count > 0)
            {
                description.Append("Related Marketing List : " + Environment.NewLine + Environment.NewLine);
                foreach (Entity item in marketingList.Entities)
                {
                    Entity list = service.Retrieve("list", ((EntityReference)item["listid"]).Id, new ColumnSet(new string[] { "listid", "listname" }));
                    description.Append((string)list["listname"]);
                    description.Append(Environment.NewLine + Environment.NewLine);
                }
            }

            addressChangeAlert["description"] = description.ToString();
            if (contactOwner.LogicalName == "systemuser")
                addressChangeAlert["ownerid"] = new EntityReference("systemuser", contactOwner.Id);
            else
                addressChangeAlert["ownerid"] = new EntityReference("team", contactOwner.Id);

            addressChangeAlert["regardingobjectid"] = new EntityReference(prePrimaryRecord.LogicalName, prePrimaryRecord.Id);

            return service.Create(addressChangeAlert);
        }

        public Guid CreateAddressChangeAlertEmail(IOrganizationService service, Entity prePrimaryRecord, Entity postPrimaryRecord, EntityCollection marketingList, Entity contactOwner, Entity config)
        {
            Entity email = new Entity("email");
            Guid emailGuid = Guid.Empty;

            string name = string.Empty;
            string url = string.Empty;

            string orgURL = config.Contains("msnfp_organizationurl") ? (string)config["msnfp_organizationurl"] : string.Empty;
            if (!string.IsNullOrEmpty(orgURL))
            {
                if (postPrimaryRecord.LogicalName == "contact")
                {
                    name = (string)postPrimaryRecord["firstname"] + " " + (string)postPrimaryRecord["lastname"];
                    url = string.Format("<a href='" + orgURL + "/main.aspx?etn=contact&id=%7b{0}%7d&newWindow=true&pagetype=entityrecord' target='_blank' style='cursor: pointer;'>{1}</a>", postPrimaryRecord.Id.ToString(), name);
                }
                else if (postPrimaryRecord.LogicalName == "account")
                {
                    name = (string)postPrimaryRecord["name"];
                    url = string.Format("<a href='" + orgURL + "/main.aspx?etn=account&id=%7b{0}%7d&newWindow=true&pagetype=entityrecord' target='_blank' style='cursor: pointer;'>{1}</a>", postPrimaryRecord.Id.ToString(), name);
                }

                email["activitytypecode"] = new OptionSetValue(4202);

                email["subject"] = "Address Change for " + name + " on the " + DateTime.Now.ToShortDateString();

                StringBuilder description = new StringBuilder();

                description.Append("<b>Record: </b>" + url);
                description.Append("<br/><br/>");

                description.Append("<b>Address Prior : </b>");
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_line1") ? (string)prePrimaryRecord["address1_line1"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_line2") ? (string)prePrimaryRecord["address1_line2"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_line3") ? (string)prePrimaryRecord["address1_line3"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_city") ? (string)prePrimaryRecord["address1_city"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_stateorprovince") ? (string)prePrimaryRecord["address1_stateorprovince"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_country") ? (string)prePrimaryRecord["address1_country"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(prePrimaryRecord.Contains("address1_postalcode") ? (string)prePrimaryRecord["address1_postalcode"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append("========================================");
                description.Append("<br/><br/>");
                description.Append("<br/><br/>");

                description.Append("<b>Address Now : </b>");
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_line1") ? (string)postPrimaryRecord["address1_line1"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_line2") ? (string)postPrimaryRecord["address1_line2"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_line3") ? (string)postPrimaryRecord["address1_line3"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_city") ? (string)postPrimaryRecord["address1_city"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_stateorprovince") ? (string)postPrimaryRecord["address1_stateorprovince"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_country") ? (string)postPrimaryRecord["address1_country"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append(postPrimaryRecord.Contains("address1_postalcode") ? (string)postPrimaryRecord["address1_postalcode"] : string.Empty);
                description.Append("<br/><br/>");
                description.Append("========================================");
                description.Append("<br/><br/>");
                description.Append("<br/><br/>");

                if (marketingList.Entities.Count > 0)
                {
                    description.Append("Related Marketing List : ");
                    description.Append("<br/><br/>");
                    foreach (Entity item in marketingList.Entities)
                    {
                        Entity list = service.Retrieve("list", ((EntityReference)item["listid"]).Id, new ColumnSet(new string[] { "listid", "listname" }));
                        description.Append((string)list["listname"]);
                        description.Append("<br/><br/>");
                    }
                }

                email["description"] = description.ToString();

                EntityCollection toPartyList = new EntityCollection();

                Entity toParty1 = new Entity("activityparty");
                toParty1["partyid"] = new EntityReference("systemuser", contactOwner.Id);
                toPartyList.Entities.Add(toParty1);

                email["to"] = toPartyList;
                email["regardingobjectid"] = new EntityReference(prePrimaryRecord.LogicalName, prePrimaryRecord.Id);

                emailGuid = service.Create(email);

                SendEmailRequest sendEmailreq = new SendEmailRequest
                {
                    EmailId = emailGuid,
                    TrackingToken = "",
                    IssueSend = true
                };
                SendEmailResponse sendEmailresp = (SendEmailResponse)service.Execute(sendEmailreq);
            }

            return emailGuid;
        }

        public static string retrieveobjectTypeCode(string entitylogicalname, IOrganizationService service)
        {
            Entity entity = new Entity(entitylogicalname);
            RetrieveEntityRequest EntityRequest = new RetrieveEntityRequest();
            EntityRequest.LogicalName = entity.LogicalName;
            EntityRequest.EntityFilters = EntityFilters.All;
            RetrieveEntityResponse responseent = (RetrieveEntityResponse)service.Execute(EntityRequest);
            EntityMetadata ent = (EntityMetadata)responseent.EntityMetadata;
            string ObjectTypeCode = ent.ObjectTypeCode.ToString();
            return ObjectTypeCode;
        }

        private string DecryptString(string Message, string Passphrase)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();
            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;
            byte[] DataToDecrypt = Convert.FromBase64String(Message);
            try
            {
                ICryptoTransform Decryptor = TDESAlgorithm.CreateDecryptor();
                Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
            }
            finally
            {
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }
            return UTF8.GetString(Results);
        }

        /// <summary>
        /// This function is used to retrieve the optionset value using the optionset text label
        /// </summary>
        /// <param name="service"></param>
        /// <param name="entityName"></param>
        /// <param name="attributeName"></param>
        /// <param name="selectedLabel"></param>
        /// <returns></returns>
        public static int GetOptionsSetValueForLabel(IOrganizationService service, string entityName, string attributeName, string selectedLabel)
        {
            RetrieveAttributeRequest retrieveAttributeRequest = new
            RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                RetrieveAsIfPublished = false
            };
            // Execute the request.
            RetrieveAttributeResponse retrieveAttributeResponse = (RetrieveAttributeResponse)service.Execute(retrieveAttributeRequest);
            // Access the retrieved attribute.
            Microsoft.Xrm.Sdk.Metadata.PicklistAttributeMetadata retrievedPicklistAttributeMetadata = (Microsoft.Xrm.Sdk.Metadata.PicklistAttributeMetadata)
            retrieveAttributeResponse.AttributeMetadata;// Get the current options list for the retrieved attribute.
            OptionMetadata[] optionList = retrievedPicklistAttributeMetadata.OptionSet.Options.ToArray();
            int selectedOptionValue = 0;
            foreach (OptionMetadata oMD in optionList)
            {
                if (oMD.Label.LocalizedLabels[0].Label.ToString().ToLower() == selectedLabel.ToLower())
                {
                    selectedOptionValue = oMD.Value.Value;
                    break;
                }
            }
            return selectedOptionValue;
        }

        /// <summary>
        /// This function is used to retrieve a Configuration record either using the reference in the user's record,
        /// or if the user does not have one, by retrieving the "Default" Configuration record.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="service"></param>
        /// <param name="tracingService"></param>
        /// <returns>Entity</returns>
        public static Entity GetConfigurationRecordByUser(IPluginExecutionContext context, IOrganizationService service,
            ITracingService tracingService)
        {
            string configColumnName = "msnfp_configurationid";

            Entity configRecord = null;
            ColumnSet configurationColumns =
                new ColumnSet("msnfp_configurationid", "msnfp_apipadlocktoken", "msnfp_sche_retryinterval", "msnfp_azure_webapiurl", "msnfp_householdsequence", "msnfp_givinglevelcalculation", "msnfp_showapierrorresponses");

            EntityReference configRef;

            // first, try to get the User's Config record
            Entity initiatingUser =
                service.Retrieve("systemuser", context.InitiatingUserId, new ColumnSet(configColumnName));
            if (initiatingUser != null &&
                initiatingUser.GetAttributeValue<EntityReference>(configColumnName) != null)
            {
                tracingService.Trace("Retrieving User's Configuration record.");
                configRef = initiatingUser.GetAttributeValue<EntityReference>(configColumnName);
                tracingService.Trace("Configuration Id:" + configRef.Id);
                configRecord = service.Retrieve(configRef.LogicalName, configRef.Id, configurationColumns);
                tracingService.Trace("Got Configuration From User");
            }
            else
            {
                tracingService.Trace("User does not have a Configuration Record. Retrieving Default Configuration Record");
                QueryByAttribute defaultConfigQuery = new QueryByAttribute("msnfp_configuration");
                defaultConfigQuery.ColumnSet = configurationColumns;
                defaultConfigQuery.AddAttributeValue("msnfp_defaultconfiguration", true);
                defaultConfigQuery.AddOrder("modifiedon", OrderType.Descending);
                defaultConfigQuery.TopCount = 1;
                var results = service.RetrieveMultiple(defaultConfigQuery);
                if (results != null && results.Entities != null & results.Entities.Count == 1)
                {
                    configRecord = results.Entities.First();
                    tracingService.Trace("Default Configuration record Id:" + configRecord.Id);
                }
                else
                {
                    throw new Exception("User does not have a Configuration record and no Configuration record has been set as the Default.");
                }
            }

            return configRecord;
        }

        /// <summary>
        /// This function is used to retrieve a Configuration record either using the reference in the user's record (only on Create),
        /// or in the Target record (on Update and Delete).
        /// Should only be used with entities that support this like Transaction, Payment Schedule, Payment Processor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="service"></param>
        /// <param name="tracingService"></param>
        /// <returns>Entity</returns>
        public static Entity GetConfigurationRecordByMessageName(IPluginExecutionContext context, IOrganizationService service, ITracingService tracingService)
        {
            string messageName = context.MessageName;
            string configColumnName = "msnfp_configurationid";

            Entity configRecord = null;
            ColumnSet configurationColumns = new ColumnSet("msnfp_configurationid", "msnfp_apipadlocktoken", "msnfp_sche_retryinterval", "msnfp_azure_webapiurl", "msnfp_showapierrorresponses");
            EntityReference configRef;

            if (string.Compare(messageName, "Create", true) == 0)
            {
                // use the User's Config record on Create
                configRecord = GetConfigurationRecordByUser(context, service, tracingService);
            }
            else if (string.Compare(messageName, "Update", true) == 0 || string.Compare(messageName, "Delete", true) == 0)
            {
                // use the record's own Config record on Update and Delete
                Guid targetId;
                if (context.InputParameters["Target"] is Entity)
                {
                    Entity tempTarget = (Entity)context.InputParameters["Target"];
                    targetId = tempTarget.Id;
                }
                else //(context.InputParameters["Target"] is EntityReference)
                {
                    EntityReference tempTargetRef = (EntityReference)context.InputParameters["Target"];
                    targetId = tempTargetRef.Id;
                }

                Entity target = service.Retrieve(context.PrimaryEntityName, targetId, new ColumnSet(configColumnName));
                if (target != null && target.GetAttributeValue<EntityReference>(configColumnName) != null)
                {
                    configRef = target.GetAttributeValue<EntityReference>(configColumnName);
                    configRecord = service.Retrieve(configRef.LogicalName, configRef.Id, configurationColumns);
                    tracingService.Trace("Got Configuration From Target");
                }
                else
                {
                    throw new Exception("No configuration record found on this record (" + targetId + "). Please ensure the record has a configuration record attached.");
                }
            }
            else
            {
                throw new Exception("Unexpected Message:" + messageName);
            }

            return configRecord;
        }

        public static EntityReference CreateHouseholdFromContact(IOrganizationService service, OptionSetValue householdType, Entity target)
        {
            EntityReference householdReference = null;
            if (householdType.Value == (int)HouseholdRelationshipType.PrimaryHouseholdMember)
            {
                // generate household sequence and create household
                // also set primary contact to current contact
                Entity household = new Entity("account");
                household["primarycontactid"] = target.ToEntityReference();
                household["msnfp_accounttype"] = new OptionSetValue((int)Utilities.AccountType.Household);
                household["name"] = $"{target.GetAttributeValue<string>("lastname")} Household";
                household.Id = service.Create(household);
                // return household
                householdReference = household.ToEntityReference();
            }
            return householdReference;
        }

        /// <summary>
        /// Search for household
        /// </summary>
        /// <param name="service">service</param>
        /// <param name="donationImport">donation import</param>
        /// <returns></returns>
        public static EntityReference SearchHousehold(IOrganizationService service, Entity donationImport, EntityReference contactRef)
        {
            // search logic
            FilterExpression filterExpression = new FilterExpression();
            filterExpression.AddCondition(new ConditionExpression("msnfp_accounttype", ConditionOperator.Equal, (int)Utilities.AccountType.Household));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_line1"))
                filterExpression.AddCondition(new ConditionExpression("address1_line1", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_line1")));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_line2"))
                filterExpression.AddCondition(new ConditionExpression("address1_line2", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_line2")));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_line3"))
                filterExpression.AddCondition(new ConditionExpression("address1_line3", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_line3")));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_postalcode"))
                filterExpression.AddCondition(new ConditionExpression("address1_postalcode", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_postalcode")));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_stateorprovince"))
                filterExpression.AddCondition(new ConditionExpression("address1_stateorprovince", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_stateorprovince")));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_country"))
                filterExpression.AddCondition(new ConditionExpression("address1_country", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_country")));

            if (donationImport.Attributes.ContainsKey("msnfp_billing_city"))
                filterExpression.AddCondition(new ConditionExpression("address1_city", ConditionOperator.Equal, donationImport.GetAttributeValue<string>("msnfp_billing_city")));

            Entity houseHold = null;

            // this ensures that a random account will not be picked with no matches
            if (filterExpression.Conditions.Count > 1)
            {
                QueryExpression queryExpression = new QueryExpression();
                queryExpression.EntityName = "account";
                queryExpression.Criteria.Filters.Add(filterExpression);

                EntityCollection entityCollection = service.RetrieveMultiple(queryExpression);
                houseHold = entityCollection.Entities.FirstOrDefault();
            }

            return houseHold != null ? houseHold.ToEntityReference() : null;
        }

        /// <summary>
        /// TO BE IMPLEMENTED
        /// </summary>
        /// <param name="service">service</param>
        /// <param name="accountEntity">account</param>
        /// <param name="configuration">configuration</param>
        /// <returns>string</returns>
        public static string GenerateHouseHoldSequence(IOrganizationService service, Entity accountEntity, Entity configuration)
        {
            string sequence = string.Empty;
            //if (accountEntity.Attributes.ContainsKey("msnfp_accounttype")
            //    && accountEntity.GetAttributeValue<OptionSetValue>("msnfp_accounttype").Value == (int)AccountType.Household
            //    && configuration.Attributes.ContainsKey("msnfp_householdsequence"))
            //{

            //}

            return sequence;
        }

        // The event total revenue is the sum of the Donations, Tickets, Sponsorships and Products sold for the Event
        public static decimal CalculateEventTotalRevenue(Entity eventToUpdate, IOrganizationService service, OrganizationServiceContext orgSvcContext, ITracingService tracingService)
        {
            tracingService.Trace("Calculating Total Revenue for Event Id " + eventToUpdate.Id);
            decimal totalRevenue = 0;

            // make sure we have all the fields we need for the calculation
            Entity evnt = service.Retrieve(eventToUpdate.LogicalName, eventToUpdate.Id,
                new ColumnSet("msnfp_sum_donations", "msnfp_sum_products", "msnfp_sum_sponsorships", "msnfp_sum_tickets"));
            tracingService.Trace("Got totals for event.");


            IQueryable<decimal> donations = orgSvcContext.CreateQuery("msnfp_transaction").Where(x =>
                    x.GetAttributeValue<EntityReference>("msnfp_eventid").Id == eventToUpdate.Id &&
                    x.GetAttributeValue<OptionSetValue>("statuscode").Value == 844060000 &&
                    x.GetAttributeValue<Money>("msnfp_amount") != null)
                .Select(x => x.GetAttributeValue<Money>("msnfp_amount").Value);
            decimal totalDonations = 0;
            foreach (var curDonation in donations)
            {
                totalDonations += curDonation;
            }
            tracingService.Trace("totalDonations:" + totalDonations);

            IQueryable<decimal> products = orgSvcContext.CreateQuery("msnfp_eventproduct").Where(x =>
                    x.GetAttributeValue<EntityReference>("msnfp_eventid").Id == eventToUpdate.Id &&
                    x.GetAttributeValue<OptionSetValue>("statecode").Value == 0 &&
                    x.GetAttributeValue<OptionSetValue>("statuscode").Value != 844060000 &&
                    x.GetAttributeValue<Money>("msnfp_val_sold") != null)
                .Select(x => x.GetAttributeValue<Money>("msnfp_val_sold").Value);
            decimal totalProducts = 0;
            foreach (var curProduct in products)
            {
                totalProducts += curProduct;
            }
            tracingService.Trace("totalProducts:" + totalProducts);

            IQueryable<decimal> sponsorships = orgSvcContext.CreateQuery("msnfp_eventsponsorship").Where(x =>
                    x.GetAttributeValue<EntityReference>("msnfp_eventid").Id == eventToUpdate.Id &&
                    x.GetAttributeValue<OptionSetValue>("statecode").Value == 0 &&
                    x.GetAttributeValue<OptionSetValue>("statuscode").Value != 844060000 &&
                    x.GetAttributeValue<Money>("msnfp_val_sold") != null)
                .Select(x => x.GetAttributeValue<Money>("msnfp_val_sold").Value);
            decimal totalSponsorships = 0;
            foreach (var curSponsorship in sponsorships)
            {
                totalSponsorships += curSponsorship;
            }
            tracingService.Trace("totalSponsorships:" + totalSponsorships);

            IQueryable<decimal> tickets = orgSvcContext.CreateQuery("msnfp_eventticket").Where(x =>
                    x.GetAttributeValue<EntityReference>("msnfp_eventid").Id == eventToUpdate.Id &&
                    x.GetAttributeValue<OptionSetValue>("statecode").Value == 0 &&
                    x.GetAttributeValue<OptionSetValue>("statuscode").Value != 844060000 &&
                    x.GetAttributeValue<Money>("msnfp_val_sold") != null)
                .Select(x => x.GetAttributeValue<Money>("msnfp_val_sold").Value);
            decimal totalTickets = 0;
            foreach (var curTicket in tickets)
            {
                totalTickets += curTicket;
            }
            tracingService.Trace("totalTickets:" + totalTickets);

            totalRevenue = totalDonations + totalProducts + totalSponsorships + totalTickets;
            tracingService.Trace("totalRevenue:" + totalRevenue);
            return totalRevenue;
        }
    }
}

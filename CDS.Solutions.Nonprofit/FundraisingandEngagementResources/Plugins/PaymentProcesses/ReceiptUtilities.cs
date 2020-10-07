using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Plugins.PaymentProcesses
{
    public class ReceiptUtilities
    {

        #region update receipt fields

        public static void UpdateReceipt(IPluginExecutionContext context, IOrganizationService service,
            ITracingService tracingService)
        {
            tracingService.Trace("Updating associated receipt (if necessary).");
            tracingService.Trace("Preimages:" + context.PreEntityImages.Count);
            tracingService.Trace("Postimages:" + context.PostEntityImages.Count);

            Entity updatedReceipt = null;
            EntityReference receiptReference = null;
            string messageName = context.MessageName;

            Entity targetIncomingRecord = null;
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                tracingService.Trace("Target Incoming Record is an Entity (needed for Create and some Updates).");
                targetIncomingRecord = (Entity)context.InputParameters["Target"];
            }

            Entity preImage = null;
            if (context.PreEntityImages.Contains("preImage"))
            {
                preImage = context.PreEntityImages["preImage"];
                tracingService.Trace("Found preImage");
                if (preImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid")!=null)
                {
                    tracingService.Trace("preImage contains tax receipt id:" + preImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid").Id);
                }
            }
            Entity postImage = null;
            if (context.PostEntityImages.Contains("postImage"))
            {
                postImage = context.PostEntityImages["postImage"];
                tracingService.Trace("Found postImage");
                if (postImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid") != null)
                {
                    tracingService.Trace("postImage contains tax receipt id:" + postImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid").Id);
                }
            }

            if (messageName == "Create" && targetIncomingRecord.GetAttributeValue<EntityReference>("msnfp_taxreceiptid") != null)
            {
                tracingService.Trace("Updating Receipt on on Create");
                receiptReference = targetIncomingRecord.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                // include in sums
                updatedReceipt = UpdateReceiptFields(receiptReference, service, tracingService, targetIncomingRecord, true);
            }
            else if (messageName == "Update")
            {
                tracingService.Trace("Checking on record Update...");
                // adding a receipt
                if (targetIncomingRecord.GetAttributeValue<EntityReference>("msnfp_taxreceiptid") != null)
                {
                    tracingService.Trace("Updating Receipt after receipt was added to record");
                    receiptReference = targetIncomingRecord.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                    // include in sums
                    updatedReceipt = UpdateReceiptFields(receiptReference, service, tracingService, targetIncomingRecord, true);
                }
                // activating the record
                else if (targetIncomingRecord.GetAttributeValue<OptionSetValue>("statecode") != null &&
                         targetIncomingRecord.GetAttributeValue<OptionSetValue>("statecode").Value == 0)
                {
                    tracingService.Trace("Updating Receipt after record was activated");
                    receiptReference = postImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                    // include in sums
                    updatedReceipt = UpdateReceiptFields(receiptReference, service, tracingService, postImage, true);
                }
                // deactivating the record
                else if (targetIncomingRecord.GetAttributeValue<OptionSetValue>("statecode") != null &&
                         targetIncomingRecord.GetAttributeValue<OptionSetValue>("statecode").Value == 1)
                {
                    tracingService.Trace("Updating Receipt after record was inactivated");
                    receiptReference = postImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                    // exclude from sums
                    updatedReceipt = UpdateReceiptFields(receiptReference, service, tracingService, postImage, false);
                }
                else if (preImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid") != null &&
                         postImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid") == null)
                {
                    tracingService.Trace("Updating Receipt after receipt was removed from record");
                    receiptReference = preImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                    // exclude from sums
                    updatedReceipt = UpdateReceiptFields(receiptReference, service, tracingService, preImage, false);
                }
                else
                {
                    tracingService.Trace("No changes needed on this update.");
                }
            }
            // deleting the record
            else if (messageName == "Delete")
            {
                tracingService.Trace("Updating Receipt after record was deleted");
                preImage = context.PreEntityImages["preImage"];
                if (preImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid") != null)
                {
                    receiptReference = preImage.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");
                    // exclude from sums
                    updatedReceipt = UpdateReceiptFields(receiptReference, service, tracingService, preImage, false);
                }

            }

            //if (receiptReference == null) return;
        }

        // to be called on Create and Update when a receipt is assigned to the record
        static Entity UpdateReceiptFields(EntityReference incomingReceiptRef, IOrganizationService service,
            ITracingService tracingService, Entity relatedRecord, bool includeRelatedRecordInCalculations)
        {
            if (incomingReceiptRef == null) return null;

            Entity incomingReceipt = service.Retrieve(incomingReceiptRef.LogicalName, incomingReceiptRef.Id,
                new ColumnSet("msnfp_amount_receipted", "msnfp_amount_nonreceiptable", "msnfp_amount", "msnfp_eventcount",
                    "msnfp_transactioncount"));
            tracingService.Trace("Updating receipt:" + incomingReceipt.Id);

            Entity recordToExclude = null;
            if (includeRelatedRecordInCalculations == false)
            {
                recordToExclude = relatedRecord;
                tracingService.Trace("Record to Exclude from Calculations:");
                tracingService.Trace(recordToExclude.Id + ", " + recordToExclude.LogicalName);
            }

            //tracingService.Trace("Checking money fields for null or 0 values");
            Entity receiptToUpdate = new Entity(incomingReceipt.LogicalName, incomingReceipt.Id);
            bool receiptUpdated = false;
            List<Entity> relatedTransactions = GetRelatedTransactions(incomingReceipt.Id, service, tracingService, recordToExclude);
            List<Entity> relatedEventPackages = GetRelatedEventPackages(incomingReceipt.Id, service, tracingService, recordToExclude);

            tracingService.Trace("updating msnfp_amount_receipted");
            var amountReceipted = new Money(
                GetSumOfMoneyFields(relatedTransactions, "msnfp_amount_receipted", tracingService) +
                GetSumOfMoneyFields(relatedEventPackages, "msnfp_amount_receipted", tracingService));
            tracingService.Trace("New Value:" + amountReceipted.Value);
            receiptToUpdate["msnfp_amount_receipted"] = amountReceipted;
            receiptUpdated = true;

            tracingService.Trace("updating msnfp_amount_nonreceiptable");
            var amountNonReceiptable = new Money(
                GetSumOfMoneyFields(relatedTransactions, "msnfp_amount_membership", tracingService) +
                GetSumOfMoneyFields(relatedTransactions, "msnfp_amount_nonreceiptable", tracingService) +
                GetSumOfMoneyFields(relatedEventPackages, "msnfp_amount_nonreceiptable", tracingService));
            tracingService.Trace(("New Value:" + amountNonReceiptable.Value));
            receiptToUpdate["msnfp_amount_nonreceiptable"] = amountNonReceiptable;
            receiptUpdated = true;

            tracingService.Trace("updating msnfp_amount");
            var amount = new Money(GetSumOfMoneyFields(relatedTransactions, "msnfp_amount", tracingService) +
                                   GetSumOfMoneyFields(relatedEventPackages, "msnfp_amount", tracingService));
            tracingService.Trace(("New Value:" + amount.Value));
            receiptToUpdate["msnfp_amount"] = amount;
            receiptUpdated = true;

            tracingService.Trace("updating msnfp_eventcount");
            tracingService.Trace(("New Value:" + relatedEventPackages.Count));
            receiptToUpdate["msnfp_eventcount"] = relatedEventPackages.Count;
            receiptUpdated = true;

            if (receiptUpdated == true)
            {
                service.Update(receiptToUpdate);
                return receiptToUpdate;
            }

            return null;
        }

        // to be called on Deletions and Updates when the Receipt is removed from the record.
        static Entity UpdateMoneyFieldsOnRemoval(EntityReference incomingReceiptRef, IOrganizationService service,
            ITracingService tracingService)
        {
            throw new NotImplementedException();
        }

        static Entity CopyUpdatedMoneyFieldsToTargetRecord(Entity receiptWithUpdatedMoneyFields, Entity targetRecord,
            ITracingService tracingService)
        {
            tracingService.Trace("Copying updated money fields to target record");
            List<string> moneyFieldsToUpdate = new List<string>()
                {"msnfp_amount_receipted", "msnfp_amount_nonreceiptable", "msnfp_amount"};

            foreach (string curField in moneyFieldsToUpdate)
            {
                if (MoneyFieldHasNonZeroValue(receiptWithUpdatedMoneyFields, curField))
                {
                    tracingService.Trace("Copying " + curField + " to incomingTargetRecord");
                    tracingService.Trace("Value:" + receiptWithUpdatedMoneyFields.GetAttributeValue<Money>(curField).Value);
                    targetRecord[curField] =
                        receiptWithUpdatedMoneyFields.GetAttributeValue<Money>(curField);
                }
            }

            if (receiptWithUpdatedMoneyFields.Contains("msnfp_eventcount") &&
                receiptWithUpdatedMoneyFields.GetAttributeValue<int>("msnfp_eventcount") != 0)
            {
                tracingService.Trace("Copying msnfp_eventcount to incomingTargetRecord");
                targetRecord["msnfp_eventcount"] =
                    receiptWithUpdatedMoneyFields.GetAttributeValue<int>("msnfp_eventcount");
            }

            return targetRecord;
        }

        // returns all Transactions associated to this Receipt
        static List<Entity> GetRelatedTransactions(Guid receiptId, IOrganizationService service, ITracingService tracingService, Entity recordToExclude = null)
        {
            tracingService.Trace("Looking for related Transactions");
            List<Entity> relatedRecords = new List<Entity>();

            QueryExpression query = new QueryExpression("msnfp_transaction");
            query.ColumnSet = new ColumnSet("msnfp_amount_receipted", "msnfp_amount_membership", "msnfp_amount_nonreceiptable", "msnfp_amount");
            query.Criteria.AddCondition("msnfp_taxreceiptid", ConditionOperator.Equal, receiptId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            if (recordToExclude != null && recordToExclude.LogicalName == "msnfp_transaction")
            {
                query.Criteria.AddCondition("msnfp_transactionid", ConditionOperator.NotEqual, recordToExclude.Id);
            }
            var result = service.RetrieveMultiple(query);

            if (result != null)
            {
                relatedRecords = result.Entities.ToList();
                tracingService.Trace("Found " + relatedRecords.Count + " related Transactions");
            }

            return relatedRecords;
        }

        // returns all Event Packages associated to this Receipt
        static List<Entity> GetRelatedEventPackages(Guid receiptId, IOrganizationService service, ITracingService tracingService, Entity recordToExclude = null)
        {
            tracingService.Trace("Looking for related Event Packages");
            List<Entity> relatedRecords = new List<Entity>();

            QueryExpression query = new QueryExpression("msnfp_eventpackage");
            query.ColumnSet = new ColumnSet("msnfp_amount_receipted", "msnfp_amount_nonreceiptable", "msnfp_amount");
            query.Criteria.AddCondition("msnfp_taxreceiptid", ConditionOperator.Equal, receiptId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            if (recordToExclude != null && recordToExclude.LogicalName == "msnfp_eventpackage")
            {
                query.Criteria.AddCondition("msnfp_eventpackageid", ConditionOperator.NotEqual, recordToExclude.Id);
            }
            var result = service.RetrieveMultiple(query);


            if (result != null)
            {
                relatedRecords = result.Entities.ToList();
                tracingService.Trace("Found " + relatedRecords.Count + " related Event Packages");
            }

            return relatedRecords;
        }

        // returns true if moneyField is not null and has a non-zero value
        static bool MoneyFieldHasNonZeroValue(Entity entity, string moneyFieldName)
        {
            bool hasNonZeroValue = false;

            if (entity.Contains(moneyFieldName))
            {
                Money moneyField = entity.GetAttributeValue<Money>(moneyFieldName);
                if (moneyField != null && moneyField.Value != 0)
                    hasNonZeroValue = true;
            }
            else
            {
                hasNonZeroValue = false;
            }

            return hasNonZeroValue;
        }

        // just to make some bits of code a bit more readable
        static bool MoneyFieldIsNullOrZero(Entity entity, string moneyFieldName)
        {
            return !MoneyFieldHasNonZeroValue(entity, moneyFieldName);
        }

        static decimal GetSumOfMoneyFields(List<Entity> records, string moneyFieldToSum, ITracingService tracingService)
        {
            string entityName = records.Count > 0 ? records.First().LogicalName : "";
            decimal moneyFieldSum =
                records.Sum(t => t.GetAttributeValue<Money>(moneyFieldToSum) != null ? t.GetAttributeValue<Money>(moneyFieldToSum).Value : 0);
            tracingService.Trace("Sum of " + moneyFieldToSum + " field across all matching " + entityName + ":" + moneyFieldSum);
            return moneyFieldSum;
        }

        #endregion
    }
}

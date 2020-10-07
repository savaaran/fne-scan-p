using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using Plugins.PaymentProcesses;

namespace Workflows
{

    public class CreateReceiptForPaymentSchedule : CodeActivity
    {
        [ReferenceTarget("msnfp_receipt")]
        [Output("Receipt")]
        public OutArgument<EntityReference> Receipt { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            // Create the tracing service
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            // Create the context
            IWorkflowContext _context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(null);

            try
            {
                tracingService.Trace("Entering CreateReceiptForPaymentSchedule");

                OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

                Guid paymentScheduleId = _context.PrimaryEntityId;
                Entity paymentSchedule = service.Retrieve(_context.PrimaryEntityName, _context.PrimaryEntityId,
                    new ColumnSet("msnfp_configurationid", "msnfp_receiptpreferencecode",
                        "msnfp_taxreceiptid", "msnfp_lastpaymentdate", "transactioncurrencyid", "msnfp_customerid",
                        "ownerid"));
                EntityReference configRef = paymentSchedule.GetAttributeValue<EntityReference>("msnfp_configurationid");

                //if (paymentSchedule.Contains("msnfp_receiptpreferencecode") && paymentSchedule["msnfp_receiptpreferencecode"] != null)
                //{
                //tracingService.Trace("Transaction contains receipt preference");

                //int receiptpreference = ((OptionSetValue)paymentSchedule["msnfp_receiptpreferencecode"]).Value;

                //if (receiptpreference != 844060000) //do not receipt
                //{
                //    tracingService.Trace("Transaction receipt preference is NOT 'DO NOT RECEIPT', value: " + receiptpreference.ToString());

                EntityReference previousReceipt = GetPreviousReceipt(paymentSchedule, tracingService);

                int currentYear = DateTime.Now.Year;
                List<Entity> matchingTransactions = GetMatchingTransactions(paymentScheduleId, currentYear, tracingService, service);
                if (matchingTransactions.Count <= 0)
                {
                    tracingService.Trace("No Matching Transactions Found.");
                    if (previousReceipt != null)
                    {
                        tracingService.Trace("Returning Previous Receipt");
                        Receipt.Set(executionContext, previousReceipt);
                    }
                    tracingService.Trace("Exiting");
                    return;
                }

                DateTime? lastDonationDate = GetLastPaymentDate(paymentSchedule, tracingService);

                int matchingTransactionCount = GetMatchingTransactionCount(matchingTransactions, tracingService);
                decimal totalAmount = GetMatchingTransactionsMoneySum(matchingTransactions, "msnfp_amount", tracingService);
                decimal totalAmountReceipted = GetMatchingTransactionsMoneySum(matchingTransactions, "msnfp_amount_receipted", tracingService);
                decimal totalAmountMembership = GetMatchingTransactionsMoneySum(matchingTransactions, "msnfp_amount_membership", tracingService);
                decimal totalAmountNonReceiptable = GetMatchingTransactionsMoneySum(matchingTransactions, "msnfp_amount_nonreceiptable", tracingService);

                Entity receiptStack = GetReceiptStack(configRef.Id, currentYear, tracingService, service);

                //string receiptIdentifier = GetReceiptIdentifier(receiptStack, tracingService, service);

                // create a new Receipt record
                Guid receiptId = CreateNewReceiptRecord(receiptStack, totalAmountReceipted, totalAmountMembership,
                    totalAmountNonReceiptable, matchingTransactionCount, totalAmount, paymentSchedule, lastDonationDate,
                    tracingService, service);

                // update the Payment Schedule with the new Receipt
                AssociateReceiptToPaymentSchedule(receiptId, paymentSchedule, tracingService, service);
                // now update all the matching Transactions with the new Receipt
                AssociateReceiptToMatchingTransactions(receiptId, matchingTransactions, tracingService, service);

                tracingService.Trace("Done.");
                Receipt.Set(executionContext, new EntityReference("msnfp_receipt", receiptId));
                //}
                //}

            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault> e)
            {
                tracingService.Trace("Workflow Exception: {0}", e.ToString());
                throw;
            }

        }

        private static Guid CreateNewReceiptRecord(Entity receiptStack, decimal totalAmountReceipted,
            decimal totalAmountMembership, decimal totalAmountNonReceiptable, int matchingTransactionCount,
            decimal totalAmount,
            Entity paymentSchedule, DateTime? lastDonationDate,
            ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Generating new Receipt record");

            Entity receipt = new Entity("msnfp_receipt");
            receipt["msnfp_receiptstackid"] = new EntityReference("msnfp_receiptstack", receiptStack.Id);
            receipt["msnfp_amount_receipted"] = new Money(totalAmountReceipted);
            receipt["msnfp_amount_nonreceiptable"] = new Money(totalAmountMembership + totalAmountNonReceiptable);
            receipt["msnfp_receiptgeneration"] = new OptionSetValue(844060000); //System Generated
            receipt["msnfp_receiptissuedate"] = DateTime.Now;
            receipt["msnfp_transactioncount"] = matchingTransactionCount;
            receipt["msnfp_amount"] = new Money(totalAmount);
            receipt["transactioncurrencyid"] = paymentSchedule.GetAttributeValue<EntityReference>("transactioncurrencyid");
            if (paymentSchedule.GetAttributeValue<EntityReference>("msnfp_customerid") != null)
            {
                receipt["msnfp_customerid"] = paymentSchedule.GetAttributeValue<EntityReference>("msnfp_customerid");
            }

            receipt["msnfp_lastdonationdate"] = lastDonationDate;
            receipt["ownerid"] = paymentSchedule.GetAttributeValue<EntityReference>("ownerid");
            receipt["statuscode"] = new OptionSetValue(1); // Issued

            receipt["msnfp_generatedorprinted"] = Convert.ToDouble(1);

            tracingService.Trace("Saving new Receipt.");
            Guid receiptId = service.Create(receipt);
            tracingService.Trace("Receipt Created. Id:" + receiptId);

            return receiptId;
        }

        private static EntityReference GetPreviousReceipt(Entity paymentSchedule, ITracingService tracingService)
        {
            tracingService.Trace("Looking for Previous Receipt (if any)");
            var previousReceipt = paymentSchedule.GetAttributeValue<EntityReference>("msnfp_taxreceiptid");

            if (previousReceipt != null)
                tracingService.Trace("Previous Receipt Found. Id:" + previousReceipt.Id);
            else
                tracingService.Trace("No Previous Receipt Found.");

            return previousReceipt;
        }

        // Get all of the Transactions that were created in the current year by this Scheduled Payment 
        // and which do not have receipts already associated to them.
        private static List<Entity> GetMatchingTransactions(Guid paymentScheduleId, int currentYear, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Getting list of matching Transactions");
            List<Entity> matchingTransactions = new List<Entity>();

            QueryByAttribute matchingTransactionQuery = new QueryByAttribute("msnfp_transaction");
            matchingTransactionQuery.ColumnSet = new ColumnSet("msnfp_bookdate", "msnfp_amount", "msnfp_amount_receipted", "msnfp_amount_membership", "msnfp_amount_nonreceiptable");
            matchingTransactionQuery.AddAttributeValue("msnfp_transaction_paymentscheduleid", paymentScheduleId);
            matchingTransactionQuery.AddAttributeValue("statuscode", 844060000); // Status Reason: Complete
            matchingTransactionQuery.AddAttributeValue("msnfp_taxreceiptid", null); // No Receipt 
            var results = service.RetrieveMultiple(matchingTransactionQuery);

            if (results != null && results.Entities != null)
            {
                tracingService.Trace("Found " + results.Entities.Count + " Transactions. (Possible matches)");
                foreach (Entity curTransaction in results.Entities)
                {
                    // only add the Transactions to the list, if they are from the current year
                    tracingService.Trace("Transaction Id:" + curTransaction.Id + ", Book Date:" + curTransaction.GetAttributeValue<DateTime>("msnfp_bookdate"));
                    DateTime transactionDate = curTransaction.GetAttributeValue<DateTime>("msnfp_bookdate");
                    if (transactionDate != null && transactionDate.Year == currentYear)
                    {
                        tracingService.Trace("Adding Transaction Id:" + curTransaction.Id + " to the list of Matching Transactions");
                        matchingTransactions.Add(curTransaction);
                    }
                }
            }
            tracingService.Trace("Found " + matchingTransactions.Count + " Matching Transactions.");
            return matchingTransactions;
        }

        private static int GetMatchingTransactionCount(List<Entity> matchingTransactions, ITracingService tracingService)
        {
            return matchingTransactions.Count;
        }

        private DateTime? GetLastPaymentDate(Entity paymentSchedule, ITracingService tracingService)
        {
            DateTime? lastPaymentDate = null;
            if (paymentSchedule.GetAttributeValue<DateTime>("msnfp_lastpaymentdate") != DateTime.MinValue)
                lastPaymentDate = paymentSchedule.GetAttributeValue<DateTime>("msnfp_lastpaymentdate");

            return lastPaymentDate;
        }

        // sum up the specified (currency) field from the list of matching transactions
        private static decimal GetMatchingTransactionsMoneySum(List<Entity> matchingTransactions, string moneyFieldToSum, ITracingService tracingService)
        {
            decimal transactionSum =
                matchingTransactions.Sum(t => t.GetAttributeValue<Money>(moneyFieldToSum) != null ? t.GetAttributeValue<Money>(moneyFieldToSum).Value : 0);
            tracingService.Trace("Sum of " + moneyFieldToSum + " field across all matching Transactions:" + transactionSum);
            return transactionSum;
        }


        // the receipt stack must have a Configuration reccord matching the PAyment Schedule AND the year must match the current year
        private static Entity GetReceiptStack(Guid configurationId, int currentYear, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Getting Receipt Stack for year:" + currentYear.ToString());
            Entity receiptStack = null;
            int receiptYearVal =
                Utilities.GetOptionsSetValueForLabel(service, "msnfp_receiptstack", "msnfp_receiptyear", currentYear.ToString());
            tracingService.Trace("receiptYearVal:" + receiptYearVal);

            QueryByAttribute receiptStackQuery = new QueryByAttribute("msnfp_receiptstack");
            receiptStackQuery.ColumnSet = new ColumnSet("msnfp_configurationid", "msnfp_receiptyear", "statecode");
            receiptStackQuery.AddAttributeValue("msnfp_configurationid", configurationId);
            receiptStackQuery.AddAttributeValue("msnfp_receiptyear", receiptYearVal);
            receiptStackQuery.AddAttributeValue("statecode", 0);
            var result = service.RetrieveMultiple(receiptStackQuery);
            if (result != null && result.Entities != null)
            {
                receiptStack = result.Entities[0];
            }

            if (receiptStack != null)
                tracingService.Trace("Found Receipt Stack Id:" + receiptStack.Id);
            else
                tracingService.Trace("No Receipt Stack found.");


            return receiptStack;
        }

        private string GetReceiptIdentifier(Entity receiptStack, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Entering SetReceiptIdentifier().");
            string newReceiptNumber = string.Empty;
            string prefix = string.Empty;
            double currentRange = 0;
            int numberRange = 0;

            if (receiptStack != null)
            {
                tracingService.Trace("Obtaining prefix, current range and number range from Receipt Stack.");
                prefix = receiptStack.GetAttributeValue<string>("msnfp_prefix");
                currentRange = receiptStack.GetAttributeValue<double>("msnfp_currentrange");
                numberRange = receiptStack.GetAttributeValue<OptionSetValue>("msnfp_numberrange") != null ? receiptStack.GetAttributeValue<OptionSetValue>("msnfp_numberrange").Value : 0;

                if (numberRange == 844060006)//six digit
                {
                    tracingService.Trace("Number range : 6 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(6, '0');
                }
                else if (numberRange == 844060008)//eight digit
                {
                    tracingService.Trace("Number range : 8 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(8, '0');
                }
                else if (numberRange == 844060010)//ten digit
                {
                    tracingService.Trace("Number range : 10 digit");
                    newReceiptNumber = prefix + (currentRange + 1).ToString().PadLeft(10, '0');
                }
                else
                {
                    tracingService.Trace("Receipt number range unknown. msnfp_numberrange: " + numberRange.ToString());
                }

                // Update the receipt stacks current number by 1.
                tracingService.Trace("Now update the receipt stacks current number by 1.");
                Entity receiptStackToUpdate = new Entity(receiptStack.LogicalName, receiptStack.Id);
                receiptStackToUpdate["msnfp_currentrange"] = currentRange + 1;
                service.Update(receiptStackToUpdate);
                tracingService.Trace("Updated Receipt Stack current range to: " + (currentRange + 1).ToString());
            }
            else
            {
                tracingService.Trace("No receipt stack found.");
            }

            tracingService.Trace("Exiting SetReceiptIdentifier().");
            return newReceiptNumber;
        }

        private static void AssociateReceiptToMatchingTransactions(Guid receiptId, List<Entity> matchingTransactions, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Associating new Receipt to " + matchingTransactions.Count + " Matching Transactions");
            foreach (Entity curMatchingTransaction in matchingTransactions)
            {
                tracingService.Trace("Updating Transaction Id:" + curMatchingTransaction.Id);
                Entity transactionToUpdate = new Entity(curMatchingTransaction.LogicalName, curMatchingTransaction.Id);
                transactionToUpdate["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", receiptId);
                service.Update(transactionToUpdate);
            }
            tracingService.Trace("Done Associating new Receipt to Matching Transactions");
        }

        private static void AssociateReceiptToPaymentSchedule(Guid receiptId, Entity paymentSchedule, ITracingService tracingService, IOrganizationService service)
        {
            tracingService.Trace("Associating new Receipt to Payment Schedule");
            Entity paymentScheduleToUpdate = new Entity(paymentSchedule.LogicalName, paymentSchedule.Id);
            paymentScheduleToUpdate["msnfp_taxreceiptid"] = new EntityReference("msnfp_receipt", receiptId);
            service.Update(paymentScheduleToUpdate);
            tracingService.Trace("Done Associating new Receipt to Payment Schedule");
        }
    }
}

/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class OpportunityStageCreate : PluginBase
    {
        /// <summary>
        /// Used to sync entity records with Azure (if applicable).
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public OpportunityStageCreate(string unsecure, string secure)
            : base(typeof(OpportunityStageCreate))
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
            localContext.TracingService.Trace("---------Triggered CreateUpdateOpportunityStage.cs---------");

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Entity queriedEntityRecord = null;
            // Note target is used in Azure sync.
            Entity targetIncomingRecord;
            string messageName = context.MessageName;

            /*
              
             * Note that this plugin is different than many others, as it runs on the update of an "Opportunity" but creates/updates an "Opportunity Stage".
              
             */

            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity)
                {
                    localContext.TracingService.Trace("---------Entering CreateUpdateOpportunityStage.cs Main Function---------");

                    targetIncomingRecord = (Entity)context.InputParameters["Target"];

                    Guid currentUserID = context.InitiatingUserId;
                    Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

                    if (user == null)
                    {
                        throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
                    }

                    // Note here we are triggering on "Opportunity" entity, not the "Opportunity Stage" entity, as we want to create a stage record (or update the existing) for an opportunity.
                    ColumnSet cols;
                    cols = new ColumnSet("opportunityid", "stepname", "customerid", "statuscode", "statecode", "processid");

                    queriedEntityRecord = service.Retrieve("opportunity", targetIncomingRecord.Id, cols);

                    if (queriedEntityRecord == null)
                    {
                        localContext.TracingService.Trace("The variable queriedEntityRecord is null. Cannot create opportunity stage without an associated opportunity. Exiting Plugin.");
                        throw new ArgumentNullException("queriedEntityRecord");
                    }

                    // We create a new stage if the opportunities current stage is NOT what the last opportunity stage record says it should be (the stage with an open end date with the same lookup to opportunity).

                    // If this is null, we create right away: GetLatestOpportunityStageForThisOpportunity
                    if (GetLatestOpportunityStageForThisOpportunity(targetIncomingRecord.Id, localContext, orgSvcContext) == null)
                    {
                        localContext.TracingService.Trace("No opportunity stages found, creating first stage.");
                        CreateBrandNewOpportunityStageForOpportunity(queriedEntityRecord, localContext, orgSvcContext, service);
                    }
                    else
                    {
                        // Just because one exists doesn't mean it is closed/changed, so we test for that here:
                        bool differentStageDetected = false;
                        differentStageDetected = CompareThisStageToLastOpportunityStage(queriedEntityRecord, localContext, orgSvcContext);

                        // Then if they are NOT the same stage, we need to close out the last record and create the newest:
                        if (differentStageDetected)
                        {
                            localContext.TracingService.Trace("Different stage detected, closing out last stage and creating new one.");
                            CreateNextOpportunityStageForOpportunity(queriedEntityRecord, localContext, orgSvcContext, service);
                        }
                        else
                        {
                            // If they are the same, get the newest tallies and update the opportunity stage record:              
                            localContext.TracingService.Trace("Same stage detected, updating stage information.");
                            UpdateExistingOpportunityStage(queriedEntityRecord, localContext, orgSvcContext);
                        }
                    }

                }

                localContext.TracingService.Trace("---------Exiting CreateUpdateOpportunityStage.cs---------");
            }
        }

        private Entity GetLatestOpportunityStageForThisOpportunity(Guid opportunityId, LocalPluginContext localContext, OrganizationServiceContext orgSvcContext)
        {
            localContext.TracingService.Trace("---------Entering GetLatestOpportunityStageForThisOpportunity()---------");
            List<Entity> opportunityStages = (from s in orgSvcContext.CreateQuery("msnfp_opportunitystage")
                                              where ((EntityReference)s["msnfp_opportunityid"]).Id == opportunityId
                                              && s.GetAttributeValue<DateTime>("msnfp_finishedon") == null
                                              select s).ToList();

            localContext.TracingService.Trace("opportunityStages.Count = " + opportunityStages.Count);
            if (opportunityStages.Count > 0)
            {
                localContext.TracingService.Trace("Order list by created on");
                opportunityStages = opportunityStages.OrderByDescending(s => s["createdon"]).ToList();

                localContext.TracingService.Trace("Opportunity Stage Id = " + opportunityStages[0].Id.ToString());
                localContext.TracingService.Trace("Opportunity Stage Stage Name = " + opportunityStages[0]["msnfp_stagename"].ToString());
                localContext.TracingService.Trace("---------Exiting GetLatestOpportunityStageForThisOpportunity()---------");
                return opportunityStages[0];
            }
            else
            {
                localContext.TracingService.Trace("No opportunity stage found. Exiting function.");
                localContext.TracingService.Trace("---------Exiting GetLatestOpportunityStageForThisOpportunity()---------");
                return null;
            }

        }


        private bool CompareThisStageToLastOpportunityStage(Entity opportunityRecord, LocalPluginContext localContext, OrganizationServiceContext orgSvcContext)
        {
            localContext.TracingService.Trace("---------Entering CompareThisStageToLastOpportunityStage()---------");
            bool differentStageDetected = false;

            List<Entity> opportunityStages = (from s in orgSvcContext.CreateQuery("msnfp_opportunitystage")
                                              where ((EntityReference)s["msnfp_opportunityid"]).Id == opportunityRecord.Id
                                              && s.GetAttributeValue<DateTime>("msnfp_finishedon") == null
                                              select s).ToList();

            localContext.TracingService.Trace("opportunityStages.Count = " + opportunityStages.Count);

            differentStageDetected = false;

            if (opportunityStages.Count > 0)
            {
                string[] pipleLineStepNameArray = ((string)opportunityRecord["stepname"]).Split('-');
                string pipeLineStepNameText = "";

                if (pipleLineStepNameArray.Length <= 1)
                {
                    pipeLineStepNameText = (string)opportunityRecord["stepname"];
                }
                else
                {
                    pipeLineStepNameText = pipleLineStepNameArray[1];
                }

                localContext.TracingService.Trace("opportunityRecord[stepname] = " + pipeLineStepNameText);
                localContext.TracingService.Trace("opportunityStages[0][msnfp_stagename] = " + opportunityStages[0]["msnfp_stagename"].ToString());
                localContext.TracingService.Trace("Equal = " + (pipeLineStepNameText == (string)opportunityStages[0]["msnfp_stagename"]));

                // Compare the stages:
                if (pipeLineStepNameText == (string)opportunityStages[0]["msnfp_stagename"])
                {
                    differentStageDetected = false;
                }
                else
                {
                    differentStageDetected = true;
                }
            }
            else
            {
                // Since there is none, we say they are different:
                localContext.TracingService.Trace("No opportunity stages exist for this opportunity.");
                differentStageDetected = true;
            }

            localContext.TracingService.Trace("Different Stage Detected = " + differentStageDetected);
            localContext.TracingService.Trace("---------Exiting CompareThisStageToLastOpportunityStage()---------");
            return differentStageDetected;
        }


        #region Create/Update Opportunity Stage records
        /* The following are the fields and how they are calculated.
         *  1.	Stage Name: The current stage name of the related opportunity business process flow
            2.	Started: The initial creation date of this stage, if the initial plugin fires on create the current date/time of the opportunity creation date can be used instead
            3.	Finished: A null value is assumed when the stage has not finished. The expectation is that WHERE Finished = NULL suggests it’s the current active stage
            4.	Days in Stage: This will only be populated when a stage changes, therefore closing out the old stage by entering or calculating the Finished date and the Days in Stage. 
            5.	Appointments: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            6.	Emails: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            7.	Letters: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            8.	Phone Calls: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            9.	Tasks: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            10.	Total Activities: A COUNT of items 5 to 9
            11.	Process (Hidden): Although not visible on the HTML page, the business process related to the stage is stored on the related entity itself
         */

        private Guid CreateBrandNewOpportunityStageForOpportunity(Entity opportunityRecord, LocalPluginContext localContext, OrganizationServiceContext orgSvcContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("---------Entering CreateBrandNewOpportunityStageForOpportunity()---------");
            if (opportunityRecord == null)
            {
                localContext.TracingService.Trace("The variable opportunityRecord is null. Cannot create opportunity stage without an associated opportunity. Exiting Plugin.");
                throw new ArgumentNullException("opportunityRecord");
            }
            Entity newOpportunityStage = new Entity("msnfp_opportunitystage");
            string[] pipleLineStepNameArray = (opportunityRecord.Contains("stepname")) ? ((string)opportunityRecord["stepname"]).Split('-') : new string[0];
            string pipeLineStepNameText = "";
            if (pipleLineStepNameArray.Length <= 1 && opportunityRecord.Contains("stepname"))
            {
                pipeLineStepNameText = (string)opportunityRecord["stepname"];
            }
            else if (pipleLineStepNameArray.Length == 2)
            {
                pipeLineStepNameText = pipleLineStepNameArray[1];
            }
            else
            {
                pipeLineStepNameText = "Qualify";
            }
            localContext.TracingService.Trace("Pipeline Step Name: " + pipeLineStepNameText); // Get the optionsetvalue label information.

            //Stage Name: The current stage name of the related opportunity business process flow. This is the "stepname" on Opportunity.
            newOpportunityStage["msnfp_stagename"] = pipeLineStepNameText;
            newOpportunityStage["msnfp_identifier"] = pipeLineStepNameText + " - " + DateTime.Now.ToShortDateString();
            //Started: The initial creation date of this stage, if the initial plugin fires on create the current date/time of the opportunity creation date can be used instead
            newOpportunityStage["msnfp_startedon"] = DateTime.UtcNow;
            //Appointments: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_appointments"] = 0;
            //Emails: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_emails"] = 0;
            //Letters: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_letters"] = 0;
            //Phone Calls: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_phonecalls"] = 0;
            //Tasks: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_tasks"] = 0;
            //Total Activities: A COUNT of items 5 to 9
            newOpportunityStage["msnfp_totalactivities"] = 0;

            newOpportunityStage["msnfp_opportunityid"] = new EntityReference("opportunity", opportunityRecord.Id);

            localContext.TracingService.Trace("Creating Opportunity Stage.");
            Guid responseGUID = service.Create(newOpportunityStage);
            localContext.TracingService.Trace("Opportunity Stage created with Id: " + responseGUID.ToString());

            localContext.TracingService.Trace("---------Exiting CreateBrandNewOpportunityStageForOpportunity()---------");
            return responseGUID;
        }

        // This happens when a new stage name is entered:
        private Guid CreateNextOpportunityStageForOpportunity(Entity opportunityRecord, LocalPluginContext localContext, OrganizationServiceContext orgSvcContext, IOrganizationService service)
        {
            localContext.TracingService.Trace("---------Entering CreateNextOpportunityStageForOpportunity()---------");
            if (opportunityRecord == null)
            {
                localContext.TracingService.Trace("The variable opportunityRecord is null. Cannot create opportunity stage without an associated opportunity. Exiting Plugin.");
                throw new ArgumentNullException("opportunityRecord");
            }
            Entity newOpportunityStage = new Entity("msnfp_opportunitystage");

            // Get the last record, as we need to update it:
            Entity lastOpportunityStage = GetLatestOpportunityStageForThisOpportunity(opportunityRecord.Id, localContext, orgSvcContext);

            if (lastOpportunityStage == null)
            {
                localContext.TracingService.Trace("Error: There is no last opportunity stage but one was attempted to be retrieved. Exiting plugin.");
                throw new ArgumentNullException("lastOpportunityStage");
            }

            localContext.TracingService.Trace("Updating opportunity stage name: " + (string)lastOpportunityStage["msnfp_stagename"]);

            string[] pipleLineStepNameArray = ((string)opportunityRecord["stepname"]).Split('-');
            string pipeLineStepNameText = "";

            if (pipleLineStepNameArray.Length <= 1)
            {
                pipeLineStepNameText = (string)opportunityRecord["stepname"];
            }
            else
            {
                pipeLineStepNameText = pipleLineStepNameArray[1];
            }

            localContext.TracingService.Trace("Pipeline Step Name: " + pipeLineStepNameText); // Get the optionsetvalue label information.

            // This may need to be an error or trigger the other function.
            if (pipeLineStepNameText == (string)lastOpportunityStage["msnfp_stagename"])
            {
                localContext.TracingService.Trace("Stage names are both: " + pipeLineStepNameText);
            }

            localContext.TracingService.Trace("Getting Appointments");

            DateTime started = lastOpportunityStage.Attributes.ContainsKey("msnfp_startedon") ? lastOpportunityStage.GetAttributeValue<DateTime>("msnfp_startedon") : DateTime.Now;

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> appointmentIds = (from s in orgSvcContext.CreateQuery("appointment")
                                         where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                         && (DateTime)s["createdon"] >= started
                                         select s.Id).ToList();

            localContext.TracingService.Trace("Got Appointments: " + appointmentIds.Count);

            localContext.TracingService.Trace("Getting Emails");

            List<Guid> emailIds = (from s in orgSvcContext.CreateQuery("email")
                                   where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                   && (DateTime)s["createdon"] >= started
                                   select s.Id).ToList();

            localContext.TracingService.Trace("Got Emails: " + emailIds.Count);

            localContext.TracingService.Trace("Getting Letters");

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> letterIds = (from s in orgSvcContext.CreateQuery("letter")
                                    where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                    && (DateTime)s["createdon"] >= started
                                    select s.Id).ToList();

            localContext.TracingService.Trace("Got Letters: " + letterIds.Count);

            localContext.TracingService.Trace("Getting Phonecalls");

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> phonecallIds = (from s in orgSvcContext.CreateQuery("phonecall")
                                       where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                       && (DateTime)s["createdon"] >= started
                                       select s.Id).ToList();

            localContext.TracingService.Trace("Got Phonecalls: " + phonecallIds.Count);

            localContext.TracingService.Trace("Getting Tasks");

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> taskIds = (from s in orgSvcContext.CreateQuery("task")
                                  where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                  && (DateTime)s["createdon"] >= started
                                  select s.Id).ToList();

            localContext.TracingService.Trace("Got Tasks: " + taskIds.Count);

            lastOpportunityStage["msnfp_finishedon"] = DateTime.UtcNow;

            if (lastOpportunityStage.Attributes.ContainsKey("msnfp_startedon"))
            {
                localContext.TracingService.Trace("Days in stage: " + Math.Round((DateTime.Now - (DateTime)lastOpportunityStage["msnfp_startedon"]).TotalDays, MidpointRounding.AwayFromZero));

                lastOpportunityStage["msnfp_daysinstage"] = (Int32)(Math.Round((DateTime.Now - (DateTime)lastOpportunityStage["msnfp_startedon"]).TotalDays, MidpointRounding.AwayFromZero));
            }

            //Appointments: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            lastOpportunityStage["msnfp_appointments"] = appointmentIds.Count;
            //Emails: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            lastOpportunityStage["msnfp_emails"] = emailIds.Count;
            //Letters: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            lastOpportunityStage["msnfp_letters"] = letterIds.Count;
            //Phone Calls: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            lastOpportunityStage["msnfp_phonecalls"] = phonecallIds.Count;
            //Tasks: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            lastOpportunityStage["msnfp_tasks"] = taskIds.Count;
            //Total Activities: A COUNT of items 5 to 9
            lastOpportunityStage["msnfp_totalactivities"] = appointmentIds.Count + emailIds.Count + letterIds.Count + phonecallIds.Count + taskIds.Count;

            localContext.TracingService.Trace("Saving changes to previous stage: " + (string)lastOpportunityStage["msnfp_stagename"]);
            orgSvcContext.UpdateObject(lastOpportunityStage);
            orgSvcContext.SaveChanges();
            localContext.TracingService.Trace("Update complete. Previous Stage End Date: " + DateTime.Now.ToString());

            localContext.TracingService.Trace("Creating New Opportunity Stage with name: " + pipeLineStepNameText);

            //Stage Name: The current stage name of the related opportunity business process flow. This is the "stepname" on Opportunity.
            newOpportunityStage["msnfp_stagename"] = pipeLineStepNameText;
            newOpportunityStage["msnfp_identifier"] = pipeLineStepNameText + " - " + DateTime.Now.ToShortDateString();
            newOpportunityStage["msnfp_startedon"] = DateTime.Now;

            //Appointments: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_appointments"] = 0;
            //Emails: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_emails"] = 0;
            //Letters: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_letters"] = 0;
            //Phone Calls: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_phonecalls"] = 0;
            //Tasks: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            newOpportunityStage["msnfp_tasks"] = 0;
            //Total Activities: A COUNT of items 5 to 9
            newOpportunityStage["msnfp_totalactivities"] = 0;

            newOpportunityStage["msnfp_opportunityid"] = new EntityReference("opportunity", opportunityRecord.Id);

            Guid responseGUID = service.Create(newOpportunityStage);
            localContext.TracingService.Trace("Opportunity Stage created with Id: " + responseGUID.ToString());

            localContext.TracingService.Trace("---------Exiting CreateNextOpportunityStageForOpportunity()---------");
            return responseGUID;
        }

        //This assumes that the stage names are the same.
        private void UpdateExistingOpportunityStage(Entity opportunityRecord, LocalPluginContext localContext, OrganizationServiceContext orgSvcContext)
        {
            localContext.TracingService.Trace("---------Entering UpdateExistingOpportunityStage()---------");
            if (opportunityRecord == null)
            {
                localContext.TracingService.Trace("The variable opportunityRecord is null. Cannot update opportunity stage without an associated opportunity. Exiting Plugin.");
                throw new ArgumentNullException("opportunityRecord");
            }

            // Get the latest opportunity stage for this opportunity:
            Entity opportunityStage = GetLatestOpportunityStageForThisOpportunity(opportunityRecord.Id, localContext, orgSvcContext);

            if (opportunityStage == null)
            {
                localContext.TracingService.Trace("The variable opportunityStage is null. Cannot update the opportunity stage as it is not found. Exiting Plugin.");
                throw new ArgumentNullException("opportunityStage");
            }

            DateTime started = opportunityStage.Attributes.ContainsKey("msnfp_startedon") ? opportunityStage.GetAttributeValue<DateTime>("msnfp_startedon") : DateTime.Now;


            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> appointmentIds = (from s in orgSvcContext.CreateQuery("appointment")
                                         where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                         && (DateTime)s["createdon"] >= started
                                         select s.Id).ToList();

            localContext.TracingService.Trace("Got Appointments: " + appointmentIds.Count);

            localContext.TracingService.Trace("Getting Emails");

            List<Guid> emailIds = (from s in orgSvcContext.CreateQuery("email")
                                   where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                   && (DateTime)s["createdon"] >= started
                                   select s.Id).ToList();

            localContext.TracingService.Trace("Got Emails: " + emailIds.Count);

            localContext.TracingService.Trace("Getting Letters");

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> letterIds = (from s in orgSvcContext.CreateQuery("letter")
                                    where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                    && (DateTime)s["createdon"] >= started
                                    select s.Id).ToList();

            localContext.TracingService.Trace("Got Letters: " + letterIds.Count);

            localContext.TracingService.Trace("Getting Phonecalls");

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> phonecallIds = (from s in orgSvcContext.CreateQuery("phonecall")
                                       where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                       && (DateTime)s["createdon"] >= started
                                       select s.Id).ToList();

            localContext.TracingService.Trace("Got Phonecalls: " + phonecallIds.Count);

            localContext.TracingService.Trace("Getting Tasks");

            // We want the records that are scheduled to start >= the start of the last stage and less than the finish of that stage (now):
            List<Guid> taskIds = (from s in orgSvcContext.CreateQuery("task")
                                  where ((EntityReference)s["regardingobjectid"]).Id == opportunityRecord.Id
                                  && (DateTime)s["createdon"] >= started
                                  select s.Id).ToList();

            localContext.TracingService.Trace("Got Tasks: " + taskIds.Count);

            if (opportunityStage.Attributes.ContainsKey("msnfp_startedon"))
            {
                localContext.TracingService.Trace("Days in stage: " + Math.Round((DateTime.Now - (DateTime)opportunityStage["msnfp_startedon"]).TotalDays, MidpointRounding.AwayFromZero));
                opportunityStage["msnfp_daysinstage"] = (Int32)Math.Round((DateTime.Now - (DateTime)opportunityStage["msnfp_startedon"]).TotalDays, MidpointRounding.AwayFromZero);
            }

            //Appointments: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            opportunityStage["msnfp_appointments"] = appointmentIds.Count;
            //Emails: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            opportunityStage["msnfp_emails"] = emailIds.Count;
            //Letters: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            opportunityStage["msnfp_letters"] = letterIds.Count;
            //Phone Calls: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            opportunityStage["msnfp_phonecalls"] = phonecallIds.Count;
            //Tasks: A COUNT of all appointments associated to the opportunity where the stage is the current stage (WHERE Finished = NULL)
            opportunityStage["msnfp_tasks"] = taskIds.Count;
            //Total Activities: A COUNT of items 5 to 9
            opportunityStage["msnfp_totalactivities"] = appointmentIds.Count + emailIds.Count + letterIds.Count + phonecallIds.Count + taskIds.Count;

            localContext.TracingService.Trace("Opportunity statecode: " + ((OptionSetValue)opportunityRecord["statecode"]).Value.ToString());
            // Here we close out the finished date if the opportunity is no longer active:
            if (((OptionSetValue)opportunityRecord["statecode"]).Value != 0)
            {
                localContext.TracingService.Trace("Setting finish date to now as the opportunity status is inactive.");
                opportunityStage["msnfp_finishedon"] = DateTime.UtcNow;
            }

            localContext.TracingService.Trace("Updating Opportunity Stage.");
            orgSvcContext.UpdateObject(opportunityStage);
            orgSvcContext.SaveChanges();
            localContext.TracingService.Trace("Updated Opportunity Stage.");
            localContext.TracingService.Trace("---------Exiting UpdateExistingOpportunityStage()---------");
        }
        #endregion



    }
}

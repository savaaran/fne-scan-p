using System;
using System.Activities.Presentation;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Plugins.Common
{
    public static class Utilities
    {
        // Generate a random string with a given size  
        public static string RandomString(int size)
        {
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }
            return builder.ToString();
        }

        // returns the content field of a web resource in a byte array
        public static byte[] GetWebResourceContent(string displayName, IOrganizationService service, ITracingService tracingService)
        {
            byte[] imgBytes = null;
            QueryByAttribute query = new QueryByAttribute("webresource");
            query.AddAttributeValue("displayname", displayName);
            query.ColumnSet = new ColumnSet("content");
            var result = service.RetrieveMultiple(query);

            if (result != null && result.Entities != null)
            {
                string imgContents = result.Entities[0].GetAttributeValue<string>("content");
                if (!string.IsNullOrEmpty(imgContents))
                {
                    imgBytes = System.Convert.FromBase64String(imgContents);
                }
            }
            return imgBytes;
        }


        #region Labels

        public static List<Guid> GetAllLabelsForCheckin(EntityReference checkinSecurityRef, IOrganizationService service,
            ITracingService tracingService)
        {
            tracingService.Trace("Creating All Labels for Checking Security:" + checkinSecurityRef.Id);
            List<Guid> labelIds = new List<Guid>();
            Guid securityLabelId = GetSecurityLabel(checkinSecurityRef, service, tracingService);
            labelIds.Add(securityLabelId);

            //get the list of all individual checkins and generate their labels
            QueryByAttribute individualCheckinQuery = new QueryByAttribute("msnfp_individualcheckin");
            individualCheckinQuery.AddAttributeValue("msnfp_checkinsecurity", checkinSecurityRef.Id);
            individualCheckinQuery.AddAttributeValue("statecode", 0);
            individualCheckinQuery.AddAttributeValue("msnfp_checkedouton", null);
            
            var result = service.RetrieveMultiple(individualCheckinQuery);
            if (result != null && result.Entities != null)
            {
                tracingService.Trace("found " + result.Entities.Count + " individual checkins for this security checkin record.");
                foreach (Entity curIndividualCheckin in result.Entities)
                {
                    EntityReference curIndividualCheckinRef = curIndividualCheckin.ToEntityReference();
                    tracingService.Trace("Creating Name Label for " + curIndividualCheckinRef.Id);
                    labelIds.Add(GetNameLabel(curIndividualCheckinRef, service, tracingService));
                    tracingService.Trace("Creating Bag Label for " + curIndividualCheckinRef.Id);
                    labelIds.Add(GetBagLabel(curIndividualCheckinRef, service, tracingService));
                }
            }

            return labelIds;
        }

        public static void DeactivateOldLabels(EntityReference checkinSecurityRef, EntityReference individualCheckinRef,
            int labelType, IOrganizationService service,
            ITracingService tracingService)
        {
            tracingService.Trace("Deactivating old labels...");
            tracingService.Trace("Type:" + labelType);
            tracingService.Trace("checkinSecurityRef:" + (checkinSecurityRef != null ? checkinSecurityRef.Id.ToString() : ""));
            tracingService.Trace("individualCheckinRef:" + (individualCheckinRef != null ? individualCheckinRef.Id.ToString() : ""));

            QueryByAttribute labelQuery = new QueryByAttribute("msnfp_label");
            labelQuery.AddAttributeValue("msnfp_labeltype", labelType);
            labelQuery.AddAttributeValue("statecode", 0);

            if (checkinSecurityRef != null)
                labelQuery.AddAttributeValue("msnfp_securitychecking", checkinSecurityRef.Id);
            if (individualCheckinRef != null)
                labelQuery.AddAttributeValue("msnfp_individualcheckin", individualCheckinRef.Id);

            var results = service.RetrieveMultiple(labelQuery);
            if (results != null && results.Entities != null)
            {
                tracingService.Trace("Found " + results.Entities.Count + " labels to deactivate.");
                foreach (Entity curLabel in results.Entities)
                {
                    Entity labelToDisable = new Entity("msnfp_label", curLabel.Id);
                    labelToDisable["statecode"] = new OptionSetValue(1);
                    labelToDisable["statuscode"] = new OptionSetValue(2);
                    service.Update(labelToDisable);
                }
            }
            tracingService.Trace("Deactivated old labels...");

        }

        public static Guid GetSecurityLabel(EntityReference checkinSecurityRef, IOrganizationService service, ITracingService tracingService)
        {
            tracingService.Trace("Creating Security Label");
            Entity checkinSecurity =
                service.Retrieve(checkinSecurityRef.LogicalName, checkinSecurityRef.Id, new ColumnSet("msnfp_checkedinby", "msnfp_checkedinby","msnfp_checkedinon"));
            tracingService.Trace("checkinSecurity record id:" + checkinSecurity.Id);

            // first get rid of any existing labels for this checking
            DeactivateOldLabels(checkinSecurityRef, null, 844060000, service, tracingService);
            
            // we need to know how many people have been checked
            QueryByAttribute individualCheckinQuery = new QueryByAttribute("msnfp_individualcheckin");
            individualCheckinQuery.AddAttributeValue("msnfp_checkinsecurity", checkinSecurityRef.Id);
            individualCheckinQuery.AddAttributeValue("statecode", 0);
            var results = service.RetrieveMultiple(individualCheckinQuery);
            int checkinCount = (results != null && results.Entities != null) ? results.Entities.Count : 0;
            tracingService.Trace("Found " + checkinCount + " individual checkins.");

            // need some info about the person performing the checking
            EntityReference checkedInByRef = checkinSecurity.GetAttributeValue<EntityReference>("msnfp_checkedinby");
            Entity checkedInBy = service.Retrieve(checkedInByRef.LogicalName, checkedInByRef.Id,
                new ColumnSet("fullname", "telephone1"));
            tracingService.Trace("Got Checked in by Id:" + checkedInBy.Id);

            byte[] entityImageBytes = Utilities.GetWebResourceContent("msnfp_SampleBarCode", service, tracingService);
            tracingService.Trace("Got Entity Image Bytes. Size:" + entityImageBytes.Length);

            Entity label = new Entity("msnfp_label");
            label["msnfp_securitychecking"] = checkinSecurityRef;
            label["msnfp_labeltype"] = new OptionSetValue(844060000);
            label["msnfp_securitycode"] = checkinSecurity.GetAttributeValue<string>("msnfp_securitycode"); // RandomString(4);
            label["msnfp_name"] = checkinSecurity.GetAttributeValue<string>("msnfp_securitycode");
            label["msnfp_checkedinby"] = checkedInBy.GetAttributeValue<string>("fullname");
            label["msnfp_phone"] = checkedInBy.GetAttributeValue<string>("telephone1");
            label["msnfp_checkedincount"] = checkinCount.ToString();
            label["msnfp_checkedindate"] = checkinSecurity.GetAttributeValue<DateTime>("msnfp_checkedinon")
                .ToString("f", CultureInfo.CurrentCulture);
            label["entityimage"] = entityImageBytes;
            Guid labelId = service.Create(label);
            tracingService.Trace("Created Label Id:" + labelId);
            return labelId;
        }


        public static Guid GetNameLabel(EntityReference individualCheckinRef, IOrganizationService service,
            ITracingService tracingService)
        {
            tracingService.Trace("Creating Name Label");
            Entity individualCheckin = service.Retrieve(individualCheckinRef.LogicalName, individualCheckinRef.Id,
                new ColumnSet("msnfp_checkinsecurity","msnfp_contact","msnfp_checkedinas","msnfp_checkedinby","msnfp_emergencyphone", "msnfp_checkedinon"));
            tracingService.Trace("individualCheckin id:" + individualCheckin.Id);

            EntityReference checkinSecurityRef =
                individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkinsecurity") ??
                individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkinsecurity");

            // first get rid of any existing labels for this checking
            DeactivateOldLabels(checkinSecurityRef, individualCheckinRef, 844060001, service, tracingService);


            // we need a bit of info about the person who was checked in
            Entity checkedInContact = null;
            EntityReference checkedInContactRef =
                individualCheckin.GetAttributeValue<EntityReference>("msnfp_contact") != null
                    ? individualCheckin.GetAttributeValue<EntityReference>("msnfp_contact")
                    : null;
            if (checkedInContactRef != null)
            {
                checkedInContact = service.Retrieve(checkedInContactRef.LogicalName, checkedInContactRef.Id,
                    new ColumnSet("fullname", "msnfp_medicalnotes", "msnfp_emergencycontact", "msnfp_emergencyphone"));
                tracingService.Trace("Got checked in contact id:" + checkedInContact.Id);
            }

            Entity label = new Entity("msnfp_label");
            label["msnfp_individualcheckin"] = individualCheckinRef;
            label["msnfp_securitychecking"] = checkinSecurityRef;
            label["msnfp_labeltype"] = new OptionSetValue(844060001);
            label["msnfp_checkedincontactname"] = checkedInContact != null ? checkedInContact.GetAttributeValue<string>("fullname") : "";

            string checkedInAs = GetOptionSetValueLabel(individualCheckinRef.LogicalName, "msnfp_checkedinas",
                individualCheckin.GetAttributeValue<OptionSetValue>("msnfp_checkedinas").Value, service);
            label["msnfp_checkedinas"] = checkedInAs;
            string checkedInBy = individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkedinby") != null
                ? individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkedinby").Name
                : "";
            label["msnfp_checkedinby"] = checkedInBy;
            label["msnfp_phone"] = individualCheckin.GetAttributeValue<string>("msnfp_emergencyphone");
            label["msnfp_checkedindate"] = individualCheckin.GetAttributeValue<DateTime>("msnfp_checkedinon")
                .ToString("f", CultureInfo.CurrentCulture);
            label["msnfp_checkedinevents"] = GetCheckedInEventsForLabel(individualCheckin, service, tracingService);
            label["msnfp_medicalnotes"] = checkedInContact != null ? checkedInContact.GetAttributeValue<string>("msnfp_medicalnotes") : "";
            Guid labelId = service.Create(label);
            tracingService.Trace("Created Label Id:" + labelId);
            return labelId;
        }

        public static Guid GetBagLabel(EntityReference individualCheckinRef, IOrganizationService service,
            ITracingService tracingService)
        {
            tracingService.Trace("Creating Bag Label");
            Entity individualCheckin = service.Retrieve(individualCheckinRef.LogicalName, individualCheckinRef.Id,
                new ColumnSet("msnfp_checkinsecurity","msnfp_contact","msnfp_checkedinas","msnfp_checkedinby","msnfp_emergencyphone","msnfp_checkedinon"));
            tracingService.Trace("individualCheckin id:" + individualCheckin.Id);

            EntityReference checkinSecurityRef =
                individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkinsecurity") ??
                individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkinsecurity");

            // first get rid of any existing labels for this checking
            DeactivateOldLabels(checkinSecurityRef, individualCheckinRef, 844060002, service, tracingService);

            // we need a bit of info about the person who was checked in
            Entity checkedInContact = null;
            EntityReference checkedInContactRef =
                individualCheckin.GetAttributeValue<EntityReference>("msnfp_contact") != null
                    ? individualCheckin.GetAttributeValue<EntityReference>("msnfp_contact")
                    : null;
            if (checkedInContactRef != null)
            {
                checkedInContact = service.Retrieve(checkedInContactRef.LogicalName, checkedInContactRef.Id,
                    new ColumnSet("fullname", "msnfp_medicalnotes", "msnfp_emergencycontact", "msnfp_emergencyphone"));
                tracingService.Trace("Got checked in contact id:" + checkedInContact.Id);
            }

            Entity label = new Entity("msnfp_label");
            label["msnfp_individualcheckin"] = individualCheckinRef;
            label["msnfp_securitychecking"] = checkinSecurityRef;
            label["msnfp_labeltype"] = new OptionSetValue(844060002);
            label["msnfp_checkedincontactname"] =
                checkedInContact != null ? checkedInContact.GetAttributeValue<string>("fullname") : "";
            string checkedInAs = GetOptionSetValueLabel(individualCheckinRef.LogicalName, "msnfp_checkedinas",
                individualCheckin.GetAttributeValue<OptionSetValue>("msnfp_checkedinas").Value, service);
            label["msnfp_checkedinas"] = checkedInAs;

            string checkedInBy = individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkedinby") != null
                ? individualCheckin.GetAttributeValue<EntityReference>("msnfp_checkedinby").Name
                : "";
            label["msnfp_checkedinby"] = checkedInBy;
            label["msnfp_phone"] = individualCheckin.GetAttributeValue<string>("msnfp_emergencyphone");
            label["msnfp_checkedindate"] = individualCheckin.GetAttributeValue<DateTime>("msnfp_checkedinon")
                .ToString("f", CultureInfo.CurrentCulture);
            Guid labelId = service.Create(label);
            tracingService.Trace("Created Label Id:" + labelId);
            return labelId;
        }


        private static string GetCheckedInEventsForLabel(Entity individualCheckin, IOrganizationService service, ITracingService tracingService)
        {
            StringBuilder eventList = new StringBuilder();
            
            string fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
              <entity name='msnfp_checkinevent'>
                <attribute name='msnfp_checkineventid' />
                <attribute name='msnfp_name' />
                <attribute name='createdon' />
                <order attribute='msnfp_name' descending='false' />
                <link-entity name='msnfp_msnfp_individualcheckin_msnfp_checkineven' from='msnfp_checkineventid' to='msnfp_checkineventid' visible='false' intersect='true'>
                  <link-entity name='msnfp_individualcheckin' from='msnfp_individualcheckinid' to='msnfp_individualcheckinid' alias='ab'>
                    <filter type='and'>
                      <condition attribute='msnfp_individualcheckinid' operator='eq' uiname='' uitype='msnfp_individualcheckin' value='{individualCheckin.Id}' />
                    </filter>
                  </link-entity>
                </link-entity>
              </entity>
            </fetch>";

            var result = service.RetrieveMultiple(new FetchExpression(fetch));
            if (result != null && result.Entities != null)
            {
                foreach (Entity curEvent in result.Entities)
                {
                    eventList.AppendLine(curEvent.GetAttributeValue<string>("msnfp_name") + "(" + curEvent.GetAttributeValue<DateTime>("msnfp_datestart").ToString("h:mmtt") + ")");
                }
            }

            return eventList.ToString();
        }
        #endregion


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


        public static async Task CallYearlyGivingServiceAsync(Guid entityId, string entityName, Guid configurationId, IOrganizationService service, ITracingService tracingService)
        {
            tracingService.Trace("Entering CallYearlyGivingServiceAsync");
            // get the url and credentials for the service
            Entity configuration = service.Retrieve("msnfp_configuration", configurationId,
                new ColumnSet("msnfp_yearlygivingsecuritykey", "msnfp_bankrunfilewebjoburl"));
            if (configuration == null)
            {
                tracingService.Trace("No Configuration record found with Id " + configurationId);
                return;
            }

            if (string.IsNullOrEmpty(configuration.GetAttributeValue<string>("msnfp_yearlygivingsecuritykey")) ||
                string.IsNullOrEmpty(configuration.GetAttributeValue<string>("msnfp_bankrunfilewebjoburl")))
            {
                tracingService.Trace("Missing url or security key. Cannot access Background Services App.");
                return;
            }

           
            string url = configuration.GetAttributeValue<string>("msnfp_bankrunfilewebjoburl") + "/api/yearlyGiving/" + entityName + "/" + entityId + "?code=" + configuration.GetAttributeValue<string>("msnfp_yearlygivingsecuritykey");
            tracingService.Trace("url:" + url);
            HttpClient client = new HttpClient();
            var response = await client.GetAsync(url);
            string result = "Response Status Code:" + response.StatusCode + ", Reason:" + response.ReasonPhrase + ", Content:" + response.Content;
            tracingService.Trace("Result:" + result);
        }

        public static string base64EncodeString(string origString)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(origString);
            string encodedString = System.Convert.ToBase64String(bytes);
            return encodedString;
        }
    }
}

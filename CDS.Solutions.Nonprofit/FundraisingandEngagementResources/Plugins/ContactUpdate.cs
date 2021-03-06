﻿/*************************************************************************
* © Microsoft. All rights reserved.
*/

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Client;
using Plugins.PaymentProcesses;

namespace Plugins
{
    /// <summary>
    /// PluginEntryPoint plug-in.
    /// This is a generic entry point for a plug-in class. Use the Plug-in Registration tool found in the CRM SDK to register this class, import the assembly into CRM, and then create step associations.
    /// A given plug-in can have any number of steps associated with it. 
    /// </summary>    
    public class ContactUpdate : PluginBase
    {
        private readonly string preImageAlias = "contact";

        private readonly string postImageAlias = "contact";

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactUpdate"/> class.
        /// </summary>
        /// <param name="unsecure">Contains public (unsecured) configuration information.</param>
        /// <param name="secure">Contains non-public (secured) configuration information. 
        /// When using Microsoft Dynamics CRM for Outlook with Offline Access, 
        /// the secure string is not passed to a plug-in that executes while the client is offline.</param>
        public ContactUpdate(string unsecure, string secure)
            : base(typeof(ContactUpdate))
        {
            //base.RegisteredEvents.Add(new Tuple<int, string, string, Action<LocalPluginContext>>(20, "Update", "account", new Action<LocalPluginContext>(ExecuteCrmPlugin)));
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

            IPluginExecutionContext context = localContext.PluginExecutionContext;
            var service = localContext.OrganizationService;
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);

            Entity preImageEntity = (context.PreEntityImages != null && context.PreEntityImages.Contains(this.preImageAlias)) ? context.PreEntityImages[this.preImageAlias] : null;
            Entity postImageEntity = (context.PostEntityImages != null && context.PostEntityImages.Contains(this.postImageAlias)) ? context.PostEntityImages[this.postImageAlias] : null;

            Guid currentUserID = context.InitiatingUserId;

            localContext.TracingService.Trace("got Contact record as Primary entity.");

            Utilities util = new Utilities();
            string addressLine1From, addressLine1To, addressPostalCodeFrom, addressPostalCodeTo;
            localContext.TracingService.Trace("Address Changed.");

            //get user record
            Entity user = service.Retrieve("systemuser", currentUserID, new ColumnSet("msnfp_configurationid"));

            if (user == null)
            {
                throw new Exception("No user id found. Please ensure the user is valid. Exiting plugin.");
            }

            Entity configurationRecord =
                Utilities.GetConfigurationRecordByUser(context, service, localContext.TracingService);

            // processing address change - start
            if (preImageEntity.Contains("address1_line1") && preImageEntity.Contains("address1_postalcode"))
            {
                addressLine1To = preImageEntity.Contains("address1_line1") ? (string)preImageEntity["address1_line1"] : string.Empty;
                addressLine1From = postImageEntity.Contains("address1_line1") ? (string)postImageEntity["address1_line1"] : string.Empty;

                addressPostalCodeTo = preImageEntity.Contains("address1_postalcode") ? (string)preImageEntity["address1_postalcode"] : string.Empty;
                addressPostalCodeFrom = postImageEntity.Contains("address1_postalcode") ? (string)postImageEntity["address1_postalcode"] : string.Empty;

                if ((addressLine1To != addressLine1From) && (addressPostalCodeTo != addressPostalCodeFrom))
                    util.CreateAddressChange(service, preImageEntity, postImageEntity, 1, localContext.TracingService);
            }

            if (preImageEntity.Contains("address2_line1") && preImageEntity.Contains("address2_postalcode"))
            {
                addressLine1To = preImageEntity.Contains("address2_line1") ? (string)preImageEntity["address2_line1"] : string.Empty;
                addressLine1From = postImageEntity.Contains("address2_line1") ? (string)postImageEntity["address2_line1"] : string.Empty;

                addressPostalCodeTo = preImageEntity.Contains("address2_postalcode") ? (string)preImageEntity["address2_postalcode"] : string.Empty;
                addressPostalCodeFrom = postImageEntity.Contains("address2_postalcode") ? (string)postImageEntity["address2_postalcode"] : string.Empty;

                if ((addressLine1To != addressLine1From) && (addressPostalCodeTo != addressPostalCodeFrom))
                    util.CreateAddressChange(service, preImageEntity, postImageEntity, 2, localContext.TracingService);
            }

            if (preImageEntity.Contains("address3_line1") && preImageEntity.Contains("address3_postalcode"))
            {
                addressLine1To = preImageEntity.Contains("address3_line1") ? (string)preImageEntity["address3_line1"] : string.Empty;
                addressLine1From = postImageEntity.Contains("address3_line1") ? (string)postImageEntity["address3_line1"] : string.Empty;

                addressPostalCodeTo = preImageEntity.Contains("address3_postalcode") ? (string)preImageEntity["address3_postalcode"] : string.Empty;
                addressPostalCodeFrom = postImageEntity.Contains("address3_postalcode") ? (string)postImageEntity["address3_postalcode"] : string.Empty;

                if ((addressLine1To != addressLine1From) && (addressPostalCodeTo != addressPostalCodeFrom))
                    util.CreateAddressChange(service, preImageEntity, postImageEntity, 3, localContext.TracingService);
            }
            // processing address change - end
        }
    }
}


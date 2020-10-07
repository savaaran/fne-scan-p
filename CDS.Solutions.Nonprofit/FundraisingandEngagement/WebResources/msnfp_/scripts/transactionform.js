/*************************************************************************
* © Microsoft. All rights reserved.
*/

function autoPopulateAppealDependingOnCampaign() {

    if (Xrm.Page.getAttribute("msnfp_originatingcampaignid").getValue()) {
        var campaignId = parent.Xrm.Page.getAttribute("msnfp_originatingcampaignid").getValue()[0].id;

        var query = "msnfp_appeals?";
        query += "$select=msnfp_appealid,msnfp_identifier,createdon&$orderby=createdon desc&";
        query += "$filter=_msnfp_campaignid_value eq " + XrmUtility.CleanGuid(campaignId);

        // Get all Appeals under the current Campaign
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query);

        if (!isNullOrUndefined(result) && result.length == 1) { // If there is only 1 Appeal record associated => auto populate it into the form
            var id = result[0].msnfp_appealid;
            var name = result[0].msnfp_identifier;
            var type = "msnfp_appeal";

            parent.Xrm.Page.getAttribute("msnfp_appealid").setValue([{ id: XrmUtility.CleanGuid(id), name: name, entityType: type }]);

            autoPopulatePackageDependingOnAppeal();
        } else { // If there is none or more than 1 Appeal records => clear out the Appeal field
            parent.Xrm.Page.getAttribute("msnfp_appealid").setValue();
        }
    }
}

function autoPopulatePackageDependingOnAppeal() {

    if (Xrm.Page.getAttribute("msnfp_appealid").getValue()) {
        var appealId = parent.Xrm.Page.getAttribute("msnfp_appealid").getValue()[0].id;

        var query = "msnfp_packages?";
        query += "$select=msnfp_packageid,msnfp_identifier,createdon&$orderby=createdon desc&";
        query += "$filter=_msnfp_appealid_value eq " + XrmUtility.CleanGuid(appealId);

        // Get all Packages the current Appeal
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query);

        if (!isNullOrUndefined(result) && result.length == 1) { // If there is only 1 Package record associated => auto populate it into the form
            var id = result[0].msnfp_packageid;
            var name = result[0].msnfp_identifier;
            var type = "msnfp_package";

            parent.Xrm.Page.getAttribute("msnfp_packageid").setValue([{ id: XrmUtility.CleanGuid(id), name: name, entityType: type }]);
        } else { // If there is none or more than 1 Package records => clear out the Package field
            parent.Xrm.Page.getAttribute("msnfp_packageid").setValue();
        }
    }
}

function autoPopulateDesignationDependingOnCampaign() {

    if (Xrm.Page.getAttribute("msnfp_originatingcampaignid").getValue()) {
        var campaignId = parent.Xrm.Page.getAttribute("msnfp_originatingcampaignid").getValue()[0].id;

        var select = "campaigns(" + XrmUtility.CleanGuid(campaignId) + ")?";
        var expand = "$expand=msnfp_Campaign_DefaultDesignation($select=msnfp_designationid,msnfp_name)";

        // Get the associated Designation record of the current Campaign
        var result = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);

        if (!isNullOrUndefined(result) && result.msnfp_Campaign_DefaultDesignation) { // If there is a record associated => auto populate it into the form
            var id = result.msnfp_Campaign_DefaultDesignation["msnfp_designationid"];
            var name = result.msnfp_Campaign_DefaultDesignation["msnfp_name"];
            var type = "msnfp_designation";

            parent.Xrm.Page.getAttribute("msnfp_designationid").setValue([{ id: XrmUtility.CleanGuid(id), name: name, entityType: type }]);
        } else { // If there is no record => clear out the Designation field
            parent.Xrm.Page.getAttribute("msnfp_designationid").setValue();
        }
    }
}

function onFormLoad(executionContext) {
    let formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    if (formType == 1) {
        formContext.getAttribute("msnfp_solicitorid").setValue(null);
        var customerid = formContext.getAttribute("msnfp_customerid").getValue();
        if (customerid) {
            var entityType = customerid[0].entityType;
            if (entityType === 'contact')
                formContext.getAttribute("msnfp_relatedconstituentid").setValue(null);
        }
    }
}

function onChangeDonor(executionContext) {
    var formContext = executionContext.getFormContext();
    let donor = formContext.getAttribute("msnfp_customerid");

    if (donor !== null && donor !== undefined) {
        formContext.ui.clearFormNotification("123")

        if (donor.getValue() != null && donor.getValue()[0].entityType === "account")
            Xrm.WebApi.retrieveRecord("account", donor.getValue()[0].id.replace('{', '').replace('}', ''), "?$select=msnfp_accounttype")
                .then(function (result) {
                    if (result.hasOwnProperty("msnfp_accounttype") && result.msnfp_accounttype === 844060000) {
                        formContext.ui.setFormNotification("Donor cannot be a household account", "ERROR", "123");
                    }
                }, function (error) {
                    console.error(error);
                });
    }
}

function onSave(executionContext) {
    let formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    var saveMode = executionContext.getEventArgs().getSaveMode();

    formContext.ui.clearFormNotification("121");

    // Apply to create
    // save or save and close button
    if (formType === 1 && (saveMode === 1 || saveMode === 2)) {
        formContext.ui.setFormNotification("Clicking Save or Save and Close will not process a transaction. Selecting Process is the only way to complete a Transaction", "ERROR", "121");
        executionContext.getEventArgs().preventDefault();
    }
}

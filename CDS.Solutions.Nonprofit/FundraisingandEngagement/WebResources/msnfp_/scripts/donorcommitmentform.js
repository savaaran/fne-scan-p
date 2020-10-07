function onFormLoad(executionContext) {
    let formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    if (formType == 1) {
        formContext.getAttribute("msnfp_solicitorid").setValue(null);
        var customerid = formContext.getAttribute("msnfp_customerid").getValue();
        if (customerid) {
            var entityType = customerid[0].entityType;
            if (entityType === 'contact')
                formContext.getAttribute("msnfp_constituentid").setValue(null);
        }
    }
}

function onCampaignChange(executionContext) {
    let formContext = executionContext.getFormContext();
    let msnfp_commitment_campaign = formContext.getAttribute("msnfp_commitment_campaignid").getValue();
    if (msnfp_commitment_campaign) {
        Xrm.WebApi.online.retrieveRecord("campaign", msnfp_commitment_campaign[0].id, "?$expand=msnfp_Campaign_DefaultDesignation($select=msnfp_designationid,msnfp_name)").then(
            (result) => {
                let msnfp_Campaign_DefaultDesignation = result["msnfp_Campaign_DefaultDesignation"];
                if (!msnfp_Campaign_DefaultDesignation) return;
                formContext.getAttribute("msnfp_commitment_defaultdesignationid").setValue([{
                    id: msnfp_Campaign_DefaultDesignation["msnfp_designationid"],
                    name: msnfp_Campaign_DefaultDesignation["msnfp_name"],
                    entityType: "msnfp_designation"
                }]);
            });
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
        formContext.ui.setFormNotification("Clicking Save or Save and Close will not process a transaction. Selecting Process is the only way to complete a Donor Commitment", "ERROR", "121");
        executionContext.getEventArgs().preventDefault();
    }
}
/*************************************************************************
* © Microsoft. All rights reserved.
*/

function preventAutoSave(econtext) {
    //var eventArgs = econtext.getEventArgs();
    //if (eventArgs.getSaveMode() == 70) {
    //    eventArgs.preventDefault();
    //}
}

function onHouserholdChange(executionContext) {
    //let formContext = executionContext.getFormContext();
    //let fieldid = "msnfp_householdid";
    //formContext.ui.clearFormNotification(fieldid);
    //var householdid = formContext.getAttribute(fieldid).getValue();
    //if (householdid) {
    //    Xrm.WebApi.online.retrieveRecord("account", householdid[0].id, "?$select=msnfp_accounttype").then(
    //        (re) => {
    //            if (re.msnfp_accounttype != 844060000) {
    //                formContext.ui.setFormNotification("Only Account Records of Type Household Are Allowed in the Household Lookup.", "WARNING", fieldid);
    //                var eventArgs = executionContext.getEventArgs();
    //                if (eventArgs && eventArgs.getSaveMode() == 1)
    //                    eventArgs.preventDefault();
    //            }
    //        }
    //    );
    //}
}

function onCompanyChange(executionContext) {
    //let formContext = executionContext.getFormContext();
    //let fieldid = "parentcustomerid";
    //formContext.ui.clearFormNotification(fieldid);
    //var householdid = formContext.getAttribute(fieldid).getValue();
    //if (householdid) {
    //    Xrm.WebApi.online.retrieveRecord("account", householdid[0].id, "?$select=msnfp_accounttype").then(
    //        (re) => {
    //            if (re.msnfp_accounttype != 844060001) {
    //                formContext.ui.setFormNotification("Only Account Records of Type Organization Are Allowed in the Company Lookup.", "WARNING", fieldid);
    //                var eventArgs = executionContext.getEventArgs();
    //                if (eventArgs && eventArgs.getSaveMode() == 1)
    //                    eventArgs.preventDefault();
    //            }
    //        }
    //    );
    //}
}

function onFormLoad(executionContext) {
    onHouserholdChange(executionContext);
    onCompanyChange(executionContext);
}

function onFormSave(executionContext) {
    onHouserholdChange(executionContext);
    onCompanyChange(executionContext);
}

function setParentAccountByType(executionContext) {
    let formContext = executionContext.getFormContext();
    let pageContext = Xrm.Utility.getPageContext();

    // only proceed if this is a new record
    if (formContext.ui.getFormType() === 1) {
        var createFromEntity = pageContext.input.createFromEntity;
        if (createFromEntity !== null && createFromEntity.entityType == 'account') {
            // we need to determine what type of account this is (household or company)
            Xrm.WebApi.retrieveRecord(createFromEntity.entityType, createFromEntity.id, "?$select=accountid,name,msnfp_accounttype").then(
                function(result) {
                    if (result != null) {
                        var fieldToUpdate = null;
                        var fieldToClear = null;
                        if (result.msnfp_accounttype == 844060000) {
                            // household
                            fieldToUpdate = "msnfp_householdid";
                            fieldToClear = "parentcustomerid";
                        } else if (result.msnfp_accounttype === 844060001) {
                            // company
                            fieldToUpdate = "parentcustomerid";
                            fieldToClear = "msnfp_householdid";
                        }
                        if (fieldToUpdate != null)
                            formContext.getAttribute(fieldToUpdate).setValue([{ id: result.accountid, name: result.name, entityType: "account" }]);
                        if (fieldToClear != null)
                            formContext.getAttribute(fieldToClear).setValue(null);
                    }
                },
                function(error) {
                    console.debug(error.message);
                });
        }
    }
}
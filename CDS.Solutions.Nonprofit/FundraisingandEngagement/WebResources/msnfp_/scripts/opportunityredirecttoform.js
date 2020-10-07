/*************************************************************************
* © Microsoft. All rights reserved.
*/

function showForm() {
    // If the form is not the create form:
    if (parent.Xrm.Page.ui.getFormType() != 1) {
        console.log("Show the correct form for this record.");

        // variable to store the name of the form
        var formId = parent.Xrm.Page.getAttribute("msnfp_primaryformid").getValue();

        console.log("Saved form id: " + formId);
        console.log("Current form id: " + parent.Xrm.Page.ui.formSelector.getCurrentItem().getId());

        // Check if the current form is form that shoul be displayed based on the Primary Form Id value:
        if (parent.Xrm.Page.ui.formSelector.getCurrentItem().getId() != formId) {
            var items = parent.Xrm.Page.ui.formSelector.items.get();
            console.log("Not the same, therefore we switch.");

            for (var i in items) {
                var item = items[i];
                var itemId = item.getId();

                console.log(itemId + "==" + formId);
                if (itemId == formId) {
                    //navigate to the form:
                    console.log("Navigate to form: " + itemId);
                    item.navigate();
                }
            }
        }
        else if (parent.Xrm.Page.ui.formSelector.getCurrentItem().getId() == formId) {
            console.log("Correct form is displaying based on msnfp_primaryformid value.");
        }
    }
}

function assignPrimaryFormId() {
    // If the form is the create form, auto-fill in the form id:
    if (parent.Xrm.Page.ui.getFormType() == 1) {
        // variable to store the name of the form
        var formId = parent.Xrm.Page.getAttribute("msnfp_primaryformid").getValue();

        // Only assign it if there is no current value:
        if (formId == null) {
            console.log("Assigning the primary form id field: " + parent.Xrm.Page.ui.formSelector.getCurrentItem().getId());
            parent.Xrm.Page.getAttribute("msnfp_primaryformid").setValue(parent.Xrm.Page.ui.formSelector.getCurrentItem().getId());
        }
    }
}

function onChangeDonor(executionContext) {
    var formContext = executionContext.getFormContext();
    let donor = formContext.getAttribute("customerid");

    if (donor !== null && donor !== undefined) {
        formContext.ui.clearFormNotification("123")

        if (donor.getValue() != null && donor.getValue()[0].entityType === "account")
            Xrm.WebApi.retrieveRecord("account", donor.getValue()[0].id.replace('{', '').replace('}', ''), "?$select=msnfp_accounttype")
                .then(function (result) {
                    if (result.hasOwnProperty("msnfp_accounttype") && result.msnfp_accounttype === 844060000) {
                        formContext.ui.setFormNotification("Donor cannot be a household account", "ERROR", "123");
                        donor.setValue(null);
                    }
                }, function (error) {
                    console.error(error);
                });
    }
}
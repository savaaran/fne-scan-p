/*************************************************************************
* © Microsoft. All rights reserved.
*/

function showForm() {
    // If the form is not the create form:
    //if (parent.Xrm.Page.ui.getFormType() != 1) {
    //    console.log("Show the correct form for this record.");

    //    // variable to store the name of the form
    //    var formId = parent.Xrm.Page.getAttribute("msnfp_primaryform").getValue();

    //    console.log("Saved form id: " + formId);
    //    console.log("Current form id: " + parent.Xrm.Page.ui.formSelector.getCurrentItem().getId());
    //    try {

    //        let msnfp_accounttype = parent.Xrm.Page.data.entity.attributes.get("msnfp_accounttype");
    //        // Check if the current form is form that shoul be displayed based on the Primary Form Id value:
    //        if (parent.Xrm.Page.ui.formSelector.getCurrentItem().getId() != formId && msnfp_accounttype.getValue() == 844060000) {
    //            var items = parent.Xrm.Page.ui.formSelector.items.get();
    //            console.log("Not the same, therefore we switch.");

    //            for (var i in items) {
    //                var item = items[i];
    //                var itemId = item.getId();

    //                console.log(itemId + "==" + formId);
    //                if (itemId == formId) {
    //                    //navigate to the form:
    //                    console.log("Navigate to form: " + itemId);
    //                    item.navigate();
    //                }
    //            }
    //        }
    //        else if (parent.Xrm.Page.ui.formSelector.getCurrentItem().getId() == formId) {
    //            console.log("Correct form is displaying based on msnfp_primaryform value or form type is not household.");
    //        }
    //    }
    //    catch {
    //        console.log("An error has occured in showForm()");
    //    }

    //}
    console.log("showForm called at " + new Date());

    // ribbon js will be loaded on the form)
    // check if it has the form handler
    if (typeof showFormForConfigured === "function")
        showFormForConfigured();
}

function assignPrimaryFormId() {
    // If the form is the create form, auto-fill in the form id:
    if (parent.Xrm.Page.ui.getFormType() == 1) {
        // variable to store the name of the form
        var formId = parent.Xrm.Page.getAttribute("msnfp_primaryform").getValue();

        // Only assign it if there is no current value:
        if (formId == null) {
            console.log("Assigning the primary form id field: " + parent.Xrm.Page.ui.formSelector.getCurrentItem().getId());
            parent.Xrm.Page.getAttribute("msnfp_primaryform").setValue(parent.Xrm.Page.ui.formSelector.getCurrentItem().getId());
        }
    }
}
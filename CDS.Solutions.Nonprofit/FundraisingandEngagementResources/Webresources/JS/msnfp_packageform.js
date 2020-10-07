/*************************************************************************
* © Microsoft. All rights reserved.
*/
function onFormLoad() {
    var param = parent.Xrm.Page.context.getQueryStringParameters();

    var eventId = param["param_msnfp_eventid"];
    var eventName = param["param_msnfp_eventidname"];
    var eventType = param["param_msnfp_eventidtype"];

    //Populate the Regarding if there is one
    if (eventId != undefined) {
        parent.Xrm.Page.getAttribute("msnfp_eventid").setValue([{ id: eventId, name: eventName, entityType: eventType }]);
    }

    var customerId = param["param_msnfp_customerid"];
    var customerName = param["param_msnfp_customeridname"];
    var customerType = param["param_msnfp_customeridtype"];

    //Populate the Regarding if there is one
    if (customerId != undefined) {
        parent.Xrm.Page.getAttribute("msnfp_customerid").setValue([{ id: customerId, name: customerName, entityType: customerType }]);
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
                        donor.setValue(null);
                    }
                }, function (error) {
                    console.error(error);
                });
    }
}
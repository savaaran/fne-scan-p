/*************************************************************************
* © Microsoft. All rights reserved.
*/

function onChangeHousehold(executionContext) {
    var formContext = executionContext.getFormContext();
    var donor = formContext.getAttribute("msnfp_customerid");
    var houseHold = formContext.getAttribute("msnfp_householdid");
    var houseHoldRelationship = formContext.getAttribute("msnfp_householdrelationship");

    if (donor !== null
        && houseHold !== null
        && houseHold.getValue() !== null
        && houseHoldRelationship !== null
        && houseHoldRelationship.getValue() === null
        && donor.getValue() !== null
        && donor.getValue()[0].entityType === "contact") {
        Xrm.WebApi.retrieveRecord(donor.getValue()[0].entityType, donor.getValue()[0].id.replace("{", "").replace("}", ""), "?$select=_msnfp_householdid_value,msnfp_householdrelationship")
            .then(function (result) {

                if (result !== null && result !== undefined
                    && result.hasOwnProperty("_msnfp_householdid_value")
                    && result._msnfp_householdid_value.toLowerCase() === houseHold.getValue()[0].id.replace("{", "").replace("}", "").toLowerCase()) {
                    houseHoldRelationship.setValue(result.msnfp_householdrelationship);
                }

            }, function (error) {
                console.error(error);
            });
    }
}
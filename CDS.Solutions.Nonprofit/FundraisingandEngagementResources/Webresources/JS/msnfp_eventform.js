/*************************************************************************
* © Microsoft. All rights reserved.
*/

//On Batch Gift

function autoPopulateDesignationDependingOnCampaign(executionContext) {
    let formContext = executionContext.getFormContext();
    var campaignId = formContext.getAttribute("msnfp_campaignid");
    let defaultDesignationId = formContext.getAttribute("msnfp_designationid");

    // Campaign is selected
    // Designation is empty
    if (campaignId !== null && campaignId.getValue() !== null && defaultDesignationId !== null && defaultDesignationId.getValue() === null) {

        Xrm.WebApi.retrieveRecord(campaignId.getValue()[0].entityType, campaignId.getValue()[0].id, "?$select=campaignid&$expand=msnfp_Campaign_DefaultDesignation($select=msnfp_designationid,msnfp_name)").then(function (result) {
            if (result.hasOwnProperty("msnfp_Campaign_DefaultDesignation") && result.msnfp_Campaign_DefaultDesignation.msnfp_designationid !== null) {
                defaultDesignationId.setValue([{ id: result.msnfp_Campaign_DefaultDesignation.msnfp_designationid.replace("{", "").replace("}", ""), name: result.msnfp_Campaign_DefaultDesignation.msnfp_name, entityType: "msnfp_designation" }]);
            }
        }, function (error) {
            console.error("Error in autoPopulateDesignationDependingOnCampaign :" + error);
        });
    }
}
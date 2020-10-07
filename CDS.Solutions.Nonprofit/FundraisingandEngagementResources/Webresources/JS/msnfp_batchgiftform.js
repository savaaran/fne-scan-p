/*************************************************************************
* © Microsoft. All rights reserved.
*/

//On Batch Gift
function showMessageNotification() {

    // clearing warning messages
    parent.Xrm.Page.ui.clearFormNotification("AmountGift");

    //msnfp_tally_amount and msnfp_amount
    var currentamttotal = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount");
    var paymentamttotal = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount");

    if (!isNullOrUndefined(currentamttotal) && !isNullOrUndefined(paymentamttotal)) {
        var currentAmtTotalValue = currentamttotal.getValue();
        var paymentAmtTotalValue = paymentamttotal.getValue();

        if (!isNullOrUndefined(currentAmtTotalValue) && !isNullOrUndefined(paymentAmtTotalValue) && currentAmtTotalValue > 0 && paymentAmtTotalValue > 0) {
            if (currentAmtTotalValue > paymentAmtTotalValue) {
                parent.Xrm.Page.ui.setFormNotification("The total gift amount exceeds the expected amount", "WARNING", "AmountTotal");
            }

            if (currentAmtTotalValue <= paymentAmtTotalValue) {
                parent.Xrm.Page.ui.clearFormNotification("AmountTotal");
            }
        }
        else {
            parent.Xrm.Page.ui.clearFormNotification("AmountTotal");
        }
    }

    //msnfp_tally_gifts and msnfp_paymentnoofgifts
    var currentNoOfGift = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_gifts");
    var paymentNoOfGift = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymentnoofgifts");

    if (!isNullOrUndefined(currentNoOfGift) && !isNullOrUndefined(paymentNoOfGift)) {
        var currentNoOfGiftValue = currentNoOfGift.getValue();
        var paymentNoOfGiftValue = paymentNoOfGift.getValue();

        if (!isNullOrUndefined(currentNoOfGiftValue) && !isNullOrUndefined(paymentNoOfGiftValue) && currentNoOfGiftValue > 0 && paymentNoOfGiftValue > 0) {
            if (currentNoOfGiftValue > paymentNoOfGiftValue) {
                parent.Xrm.Page.ui.setFormNotification("The total number of gifts exceeds what is expected", "WARNING", "GiftTotal");
            }

            if (currentNoOfGiftValue <= paymentNoOfGiftValue) {
                parent.Xrm.Page.ui.clearFormNotification("GiftTotal");
            }
        }
        else {
            parent.Xrm.Page.ui.clearFormNotification("GiftTotal");
        }
    }

    //msnfp_tally_amount_membership and msnfp_amount_membership
    var currentAmtDues = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_membership");
    var paymentAmtDues = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount_membership");

    if (!isNullOrUndefined(currentAmtDues) && !isNullOrUndefined(paymentAmtDues)) {
        var currentAmtDuesValue = currentAmtDues.getValue();
        var paymentAmtDuesValue = paymentAmtDues.getValue();

        if (!isNullOrUndefined(currentAmtDuesValue) && !isNullOrUndefined(paymentAmtDuesValue) && currentAmtDuesValue > 0 && paymentAmtDuesValue > 0) {
            if (currentAmtDuesValue > paymentAmtDuesValue) {
                parent.Xrm.Page.ui.setFormNotification("The current total of membership dues exceeds what is expected", "WARNING", "AmountDues");
            }

            if (currentAmtDuesValue <= paymentAmtDuesValue) {
                parent.Xrm.Page.ui.clearFormNotification("AmountDues");
            }
        }
        else {
            parent.Xrm.Page.ui.clearFormNotification("AmountDues");
        }
    }

    //msnfp_tally_amount_tax and msnfp_amount_tax
    var currentAmtNonReceiptable = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_tax");
    var paymentAmtNonReceiptable = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount_nonreceiptable");

    if (!isNullOrUndefined(currentAmtNonReceiptable) && !isNullOrUndefined(paymentAmtNonReceiptable)) {
        var currentAmtNonReceiptableValue = currentAmtNonReceiptable.getValue();
        var paymentAmtNonReceiptableValue = paymentAmtNonReceiptable.getValue();

        if (!isNullOrUndefined(currentAmtNonReceiptableValue) && !isNullOrUndefined(paymentAmtNonReceiptableValue) && currentAmtNonReceiptableValue > 0 && paymentAmtNonReceiptableValue > 0) {
            if (currentAmtNonReceiptableValue > paymentAmtNonReceiptableValue) {
                parent.Xrm.Page.ui.setFormNotification("The current total taxes exceeds what is expected", "WARNING", "AmountDues");
            }

            if (currentAmtNonReceiptableValue <= paymentAmtNonReceiptableValue) {
                parent.Xrm.Page.ui.clearFormNotification("AmountDues");
            }
        }
        else {
            parent.Xrm.Page.ui.clearFormNotification("AmountDues");
        }
    }

    //msnfp_tally_amount_tax and msnfp_amount_tax
    var currentAmtTaxes = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_tax");
    var paymentAmtTaxes = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount_tax");

    if (!isNullOrUndefined(currentAmtTaxes) && !isNullOrUndefined(paymentAmtTaxes)) {
        var currentAmtTaxesValue = currentAmtTaxes.getValue();
        var paymentAmtTaxesValue = paymentAmtTaxes.getValue();

        if (!isNullOrUndefined(currentAmtTaxesValue) && !isNullOrUndefined(paymentAmtTaxesValue) && currentAmtTaxesValue > 0 && paymentAmtTaxesValue > 0) {
            if (currentAmtTaxesValue > paymentAmtTaxesValue) {
                parent.Xrm.Page.ui.setFormNotification("The current total taxes exceeds what is expected", "WARNING", "AmountDues");
            }

            if (currentAmtTaxesValue <= paymentAmtTaxesValue) {
                parent.Xrm.Page.ui.clearFormNotification("AmountDues");
            }
        }
        else {
            parent.Xrm.Page.ui.clearFormNotification("AmountDues");
        }
    }
}

function isNullOrUndefined(value) {
    return (typeof (value) === "undefined" || value === null || value === "");
}

function onLoadValidateCounts(executionContext) {
    var formContext = executionContext.getFormContext();
    var statusReason = formContext.getAttribute("statuscode");

    // recalculate no of transaction and amounts
    if (formContext.ui.getFormType() === 2 && statusReason != null && statusReason.getValue() === 1) {

        var entityId = formContext.data.entity.getId().replace("{", '').replace("}", '');

        var fetchXML = "<fetch aggregate='true' >"
            + "<entity name='msnfp_transaction' >"
            + "<attribute name='msnfp_amount_membership' alias='msnfp_amount_membership' aggregate='sum' />"
            + "<attribute name='msnfp_amount_receipted' alias='msnfp_amount_receipted' aggregate='sum' />"
            + "<attribute name='msnfp_amount_nonreceiptable' alias='msnfp_amount_nonreceiptable' aggregate='sum' />"
            + "<attribute name='msnfp_amount_tax' alias='msnfp_amount_tax' aggregate='sum' />"
            + "<attribute name='msnfp_giftbatchid' alias='msnfp_giftbatchid' groupby='true' />"
            + "<attribute name='msnfp_transactionid' alias='count' aggregate='count' />"
            + "<filter>"
            + "<condition attribute='msnfp_giftbatchid' operator='eq' value='" + entityId + "' />"
            + "</filter>"
            + "</entity>"
            + "</fetch>";

        Xrm.WebApi.retrieveMultipleRecords("msnfp_transaction", "?fetchXml=" + fetchXML).then(function (result) {
            if (result.entities.length > 0) {
                var aggregate = result.entities[0];

                var totalGiftMembershipAmount = aggregate.msnfp_amount_membership === undefined ? 0 : aggregate.msnfp_amount_membership;
                var totalReceiptableAmount = aggregate.msnfp_amount_receipted === undefined ? 0 : aggregate.msnfp_amount_receipted;
                var totalNonReceiptableAmount = aggregate.msnfp_amount_nonreceiptable === undefined ? 0 : aggregate.msnfp_amount_nonreceiptable;
                var totalTaxAmount = aggregate.msnfp_amount_tax === undefined ? 0 : aggregate.msnfp_amount_tax;

                var currentNoOfGifts = aggregate.count;
                var currentAmountTotal = parseFloat(totalReceiptableAmount + totalGiftMembershipAmount + totalNonReceiptableAmount + totalTaxAmount);
                var currentAmtDuesTotal = parseFloat(totalGiftMembershipAmount);
                var currentAmtReceiptableTotal = parseFloat(totalReceiptableAmount);
                var currentAmtNonReceiptableTotal = parseFloat(totalNonReceiptableAmount);

                // The total amount membership included:
                formContext.getAttribute("msnfp_tally_amount").setValue(currentAmountTotal);

                // Receiptable amount:
                formContext.getAttribute("msnfp_tally_amount_receipted").setValue(currentAmtReceiptableTotal);

                // Non receiptable amount:
                formContext.getAttribute("msnfp_tally_amount_nonreceiptable").setValue(currentAmtNonReceiptableTotal);

                // Membership Dues:
                formContext.getAttribute("msnfp_tally_amount_membership").setValue(currentAmtDuesTotal);

                // Taxes Dues:
                formContext.getAttribute("msnfp_tally_amount_tax").setValue(totalTaxAmount);

                // Number of gifts:
                formContext.getAttribute("msnfp_tally_gifts").setValue(currentNoOfGifts);

                // async save
                formContext.data.save();
            }
        }, function (error) {
            console.error("An error occurred in onLoadValidateCounts: " + error);
        });
    }
}

function validateGiftType() {

    ////var formType = parent.Xrm.Page.ui.getFormType();

    //alert("validateGiftType");

    ////alert(formType);
    ////if (formType == 1) {
    //var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    //alert(currentuserID);

    //    var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_batch_eft";
    //    var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
    //    var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

    //    //console.log("_msnfp_configurationid_value: " + user._msnfp_configurationid_value);
    //    if (!isNullOrUndefined(configresult)) {
    //        configRecord = configresult[0];

    //        alert(configRecord.msnfp_batch_eft);

    //        if (!isNullOrUndefined(configRecord.msnfp_batch_eft) || configRecord.msnfp_batch_eft == false) {
    //            alert("false");
    //        }
    //        else
    //            alert("true")
    //   // }
    //}
}

function setConfigurationandOwingTeam() {
    var configRecord = null;
    var formType = parent.Xrm.Page.ui.getFormType();
    if (formType == 1) {
        var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
        var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
        userSelect += "&$filter=systemuserid eq " + currentuserID;
        var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
        user = user[0];

        if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
            var select = "msnfp_configurations(" + user._msnfp_configurationid_value + ")?";
            var expand = "$expand=msnfp_TeamOwnerId($select=teamid,name)";

            var configresult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);

            if (!isNullOrUndefined(configresult)) {
                parent.Xrm.Page.getAttribute("msnfp_configurationid").setValue([{ id: configresult.msnfp_configurationid, name: configresult.msnfp_identifier, entityType: "msnfp_configuration" }]);

                if (!isNullOrUndefined(configresult._msnfp_teamownerid_value)) {
                    var teamID = configresult.msnfp_TeamOwnerId.teamid;
                    var teamName = configresult.msnfp_TeamOwnerId.name;
                    parent.Xrm.Page.getAttribute("msnfp_teamownerid").setValue([{ id: teamID, name: teamName, entityType: "team" }]);
                }
            }
        }
    }
}

// function called on load and onChange of BatchCode
function setDefaultTabFields(executionContext) {
    var formContext = executionContext.getFormContext();
    var field = "msnfp_amount";
    var batchTypeCode = formContext.getAttribute("msnfp_batchcode");

    if (batchTypeCode !== null && batchTypeCode !== undefined) {
        var defaultTab = formContext.ui.tabs.get("{48c97ab4-3f9a-4fc9-a7e8-30808891ed62}");

        if (defaultTab !== null && defaultTab !== undefined) {
            var section = defaultTab.sections.get("{48c97ab4-3f9a-4fc9-a7e8-30808891ed62}_section_2");

            if (section !== null && section !== undefined) {
                section.controls.forEach(function (attribute, index) {
                    if (attribute.getName() === field) {
                        if (batchTypeCode.getValue() === 844060001) {
                            attribute.setVisible(true);
                            attribute.getAttribute().setRequiredLevel("required");
                        }
                        else {
                            attribute.setVisible(false);
                            attribute.getAttribute().setRequiredLevel("none");
                        }
                    }
                });
            }
        }
    }
}

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

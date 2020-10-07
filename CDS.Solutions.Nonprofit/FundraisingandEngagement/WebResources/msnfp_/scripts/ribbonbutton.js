/*************************************************************************
* © Microsoft. All rights reserved.
*/
// msnfp_ribbonbutton.js


//On Pledge entity Ribbon
function showWriteOff() {

    var writeOffAmount = parent.Xrm.Page.getAttribute("msnfp_totalamount_writeoff").getValue();
    var amountTotal = parent.Xrm.Page.getAttribute("msnfp_totalamount").getValue();
    var amountBalance = parent.Xrm.Page.data.entity.attributes.get("msnfp_totalamount_balance").getValue();
    var currencyId = parent.Xrm.Page.data.entity.attributes.get("transactioncurrencyid") !== null ? parent.Xrm.Page.data.entity.attributes.get("transactioncurrencyid").getValue() : null;
    var campaign = parent.Xrm.Page.data.entity.attributes.get("msnfp_commitment_campaignid").getValue();
    var campaignID = "";

    if (!isNullOrUndefined(campaign)) {
        campaignID = parent.Xrm.Page.data.entity.attributes.get("msnfp_commitment_campaignid").getValue()[0].id;
    }

    if (currencyId !== null) {
        currencyId = currencyId[0].id
    }

    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    var params = encodeURIComponent("writeOffAmount=" + writeOffAmount + "&amountTotal=" + amountTotal + "&amountBalance=" + amountBalance + "&campaignID=" + campaignID + "&currentEntityGUID=" + currentEntityGUID + "&currencyId=" + currencyId);

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/writeoffpopup.html", windowOptions, params);
}

function HideWriteoffButtonBasedOnCondition() {
    var writeoff = parent.Xrm.Page.data.entity.attributes.get("msnfp_totalamount_writeoff");
    var amountBalance = parent.Xrm.Page.data.entity.attributes.get("msnfp_totalamount_balance");
    var amountTotal = parent.Xrm.Page.data.entity.attributes.get("msnfp_totalamount");

    var isVisible = false;
    if ((writeoff.getValue() < amountTotal.getValue()) && (amountBalance.getValue() > 0)) {
        return isVisible = true;
    }

    return isVisible;
}

function showUpdateCreditCard() {
    var currentEntityName = parent.Xrm.Page.data.entity.getEntityName();
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var creditCardID;
    var params;

    if (currentEntityName === "msnfp_transaction") {
        creditCardID = parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentmethodid").getValue()[0].id;
        params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "&creditCardID=" + creditCardID);
    }
    else if (currentEntityName === "msnfp_paymentschedule") {
        creditCardID = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymentmethodid").getValue()[0].id;
        params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "&creditCardID=" + creditCardID);
    }
    else {
        params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "");
    }

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/updatecreditcard.html", windowOptions, params);
}


function HideCreditCardButtonBasedOnCondition() {
    var donationType = parent.Xrm.Page.data.entity.attributes.get("msnfp_type");
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var creditCard = parent.Xrm.Page.data.entity.attributes.get("msnfp_creditcardid").getValue();

    var isVisible = false;

    if (donationType.getValue() === 84406010 && status.getValue() === 1 && creditCard !== null) {
        return isVisible = true;
    }
    return isVisible;
}


// Perform a gift sync on demand from the configuration page:
function TriggerGiftSyncOnDemand() {
    console.log("TriggerGiftSyncOnDemand()");
    let giftSyncURL = Xrm.Page.data.entity.attributes.get("msnfp_giftsyncwebjoburl").getValue();

    // We get the values we need to actually trigger the web job:
    if (!isNullOrUndefined(giftSyncURL)) {

        try {
            $.ajax({
                type: "GET",
                url: giftSyncURL,
                dataType: 'json',
                success: function () {
                }
            });
        }
        catch (e) {
            console.log(e);
        }

        let alertStrings = { title: "Triggering Gift Sync", confirmButtonLabel: "Okay", text: "Note that it will take a few minutes to sync these gifts into Dynamics." };
        let alertOptions = { height: 120, width: 260 };
        Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
            function success(result) {
                console.log("Alert dialog closed");
            },
            function (error) {
                console.log(error.message);
            }
        );

    }
    else {
        let errorString = { title: "Cannot Process Request", confirmButtonLabel: "Okay", text: "Please ensure the field 'Gift Sync Function App URL' is set in this configuration record and try again." };
        let errorOptions = { height: 150, width: 360 };
        Xrm.Navigation.openAlertDialog(errorString, errorOptions).then(
            function success(result) {
                console.log("Alert dialog closed");
            },
            function (error) {
                console.log(error.message);
            }
        );
    }
}

// Associates all of the payment schedules to a bank run through creation of the bank run schedule linking entity:
function GetLatestBankRunRelatedPaymentSchedulesAndTransactions() {

    console.log("GetLatestBankRunRelatedPaymentSchedulesAndTransactions()");
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var configRecord = null;

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_bankrunsecuritykey,msnfp_bankrunfilewebjoburl";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            console.log("configRecord.msnfp_bankrunfilewebjoburl = " + configRecord.msnfp_bankrunfilewebjoburl);

            // We get the values we need to actually trigger the web job:
            if (!isNullOrUndefined(configRecord.msnfp_bankrunfilewebjoburl) && !isNullOrUndefined(configRecord.msnfp_bankrunsecuritykey)) {
                if (isNullOrUndefined(currentEntityGUID)) {
                    alert("Could not get the current entity GUID. Please ensure this record is saved and try again.");
                    return;
                }
                else {
                    // Note that the space is not a mistake, that is required instead of & for webjob argument passing.
                    //var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "?arguments=BRGuid=" + currentEntityGUID + " SelectedProcess=List";
                    //var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "?bankRun --action List --id " + currentEntityGUID;
                    var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "/api/bankRun/List/" + currentEntityGUID + "?code=" + configRecord.msnfp_bankrunsecuritykey;
                    console.debug(webJobURL);

                    try {
                        $.ajax
                            ({
                                type: "GET",
                                url: webJobURL,
                                success: function () {
                                }
                            });
                    }
                    catch (e) {
                        console.log(e);
                    }

                    let alertStrings = { title: "Associating Payment Schedules", confirmButtonLabel: "Okay", text: "Note that it will take some time to populate these links and sync them back into Dynamics." };
                    let alertOptions = { height: 120, width: 260 };
                    Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                        function success(result) {
                            console.log("Alert dialog closed");
                        },
                        function (error) {
                            console.log(error.message);
                        }
                    );
                }
            }
            else {
                let errorString = { title: "Cannot Process Request", confirmButtonLabel: "Okay", text: "Please ensure the fields 'Bank Run Security Key' and 'Background Services Web Job URL' are set in the configuration record and try again." };
                let errorOptions = { height: 120, width: 260 };
                Xrm.Navigation.openAlertDialog(errorString, errorOptions).then(
                    function success(result) {
                        console.log("Alert dialog closed");
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );
            }
        }
    }
}

// Hides all buttons that relate to creating a list of bank run schedules or generating bank run files:
function HideBankRunProcessingButtons() {
    console.log("HideBankRunProcessingButtons()");
    var bankRunStatus = parent.Xrm.Page.data.entity.attributes.get("msnfp_bankrunstatus");

    // If it is Open, Report Available or gift list retrieved status, we allow to do it again. Otherwise hide it:
    if (bankRunStatus.getValue() !== 844060000 && bankRunStatus.getValue() !== 844060003 && bankRunStatus.getValue() !== 844060004) {
        return false;
    }
    else {
        return true;
    }
}

function ConfirmBankRunPaymentScheduleList() {
    console.log("ConfirmBankRunPaymentScheduleList()");
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var configRecord = null;

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_bankrunsecuritykey,msnfp_bankrunfilewebjoburl";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            console.log("configRecord.msnfp_bankrunfilewebjoburl = " + configRecord.msnfp_bankrunfilewebjoburl);

            // We get the values we need to actually trigger the web job:
            if (!isNullOrUndefined(configRecord.msnfp_bankrunfilewebjoburl) && !isNullOrUndefined(configRecord.msnfp_bankrunsecuritykey)) {
                if (isNullOrUndefined(currentEntityGUID)) {
                    alert("Could not get the current entity GUID. Please ensure this record is saved and try again.");
                    return;
                }
                else {
                    var confirmStrings = { text: "Create transaction records for all associated payment schedules and update their next payment dates?", title: "Confirm Payment Schedule List", confirmButtonLabel: "Create and Update" };
                    var confirmOptions = { height: 200, width: 450 };
                    Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
                        function (success) {
                            if (success.confirmed) {
                                // Note that the space is not a mistake, that is required instead of & for webjob argument passing.
                                //var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "?arguments=BRGuid=" + currentEntityGUID + " SelectedProcess=GenerateTransactions";
                                //var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "?bankrun --action GenerateTransactions --id " + currentEntityGUID;
                                var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "/api/bankRun/GenerateTransactions/" + currentEntityGUID + "?code=" + configRecord.msnfp_bankrunsecuritykey;
                                console.debug(webJobURL);

                                try {
                                    $.ajax
                                        ({
                                            type: "GET",
                                            url: webJobURL,
                                            success: function () {
                                            }
                                        });
                                }
                                catch (e) {
                                    console.log(e);
                                }

                                var status = parent.Xrm.Page.data.entity.attributes.get("msnfp_bankrunstatus");
                                status.setValue(844060002); // List Confirmed
                                parent.Xrm.Page.data.entity.save();
                                console.log("Dialog closed using OK button.");

                                let alertStrings2 = { title: "List Confirmed", confirmButtonLabel: "Okay", text: "This bank run is now scheduled to be processed. This will take some time to sync the records into Dynamics." };
                                let alertOptions2 = { height: 120, width: 260 };
                                Xrm.Navigation.openAlertDialog(alertStrings2, alertOptions2).then(
                                    function success(result2) {
                                        console.log("Alert dialog closed");
                                    },
                                    function (error2) {
                                        console.log(error2.message);
                                    }
                                );
                            }
                            else {
                                console.log("Dialog closed using Cancel button or X.");
                            }
                        });
                }
            }
            else {
                let errorString = { title: "Cannot Process Request", confirmButtonLabel: "Okay", text: "Please ensure the fields 'Bank Run Security Key' and 'Background Services Web Job URL' are set in the configuration record and try again." };
                let errorOptions = { height: 120, width: 260 };
                Xrm.Navigation.openAlertDialog(errorString, errorOptions).then(
                    function success(result) {
                        console.log("Alert dialog closed");
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );
            }
        }
    }
}

function HideConfirmBankRunPaymentScheduleListButton() {
    console.log("HideConfirmBankRunPaymentScheduleListButton()");
    var bankRunStatus = parent.Xrm.Page.data.entity.attributes.get("msnfp_bankrunstatus");

    // If it is gift list retrieved or Report Available status, we show it:
    if (bankRunStatus.getValue() === 844060003 || bankRunStatus.getValue() === 844060004) {
        return true;
    }
    else {
        return false;
    }
}

function GenerateBankRunFile() {

    console.log("GenerateBankRunFile()");
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var configRecord = null;

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_bankrunsecuritykey,msnfp_bankrunfilewebjoburl";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            console.log("configRecord.msnfp_bankrunfilewebjoburl = " + configRecord.msnfp_bankrunfilewebjoburl);

            // We get the values we need to actually trigger the web job:
            if (!isNullOrUndefined(configRecord.msnfp_bankrunfilewebjoburl) && !isNullOrUndefined(configRecord.msnfp_bankrunsecuritykey)) {
                if (isNullOrUndefined(currentEntityGUID)) {
                    alert("Could not get the current entity GUID. Please ensure this record is saved and try again.");
                    return;
                }
                else {
                    // Note that the space is not a mistake, that is required instead of & for webjob argument passing.
                    //var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "?arguments=BRGuid=" + currentEntityGUID + " SelectedProcess=File";
                    //var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "?bankrun --action File --id " + currentEntityGUID;
                    var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "/api/bankRun/File/" + currentEntityGUID + "?code=" + configRecord.msnfp_bankrunsecuritykey;
                    console.debug(webJobURL);

                    console.debug(webJobURL);

                    try {
                        $.ajax
                            ({
                                type: "GET",
                                url: webJobURL,
                                success: function () {
                                }
                            });
                    }
                    catch (e) {
                        console.log(e);
                    }

                    let alertStrings = { title: "Generating Bank Run File", confirmButtonLabel: "Okay", text: "Note that it may take some time to generate the file. The file will be added to this record's Notes." };
                    let alertOptions = { height: 120, width: 260 };
                    Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                        function success(result) {
                            console.log("Alert dialog closed");
                        },
                        function (error) {
                            console.log(error.message);
                        }
                    );
                }
            }
            else {
                let errorString = { title: "Cannot Process Request", confirmButtonLabel: "Okay", text: "Please ensure the fields 'Bank Run Security Key' and 'Background Services Web Job URL' are set in the configuration record and try again." };
                let errorOptions = { height: 120, width: 260 };
                Xrm.Navigation.openAlertDialog(errorString, errorOptions).then(
                    function success(result) {
                        console.log("Alert dialog closed");
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );
            }
        }
    }

}

function HideGenerateBankRunFileButton() {
    console.log("HideGenerateBankRunFileButton()");
    var bankRunStatus = parent.Xrm.Page.data.entity.attributes.get("msnfp_bankrunstatus");

    // If it is List Confirmed status or Report Available status, we show it:
    if (bankRunStatus.getValue() === 844060003 || bankRunStatus.getValue() === 844060004) {
        return true;
    }
    else {
        return false;
    }
}




function showUpdateBankAccount() {
    var currentEntityName = parent.Xrm.Page.data.entity.getEntityName();
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var bankAccountID;
    var params;

    if (currentEntityName === "msnfp_paymentschedule" || currentEntityName === "msnfp_transaction") {
        bankAccountID = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymentmethodid").getValue()[0].id;
        params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "&bankAccountID=" + bankAccountID);
    }
    else {
        params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "");
    }

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/updatebankaccount.html", windowOptions, params);
}

function HideBankAccountButtonBasedOnCondition() {
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var bankAccount = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymentmethodid").getValue();
    var giftType = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymenttypecode");

    var configRecord = null;
    var isVisible = false;

    var entityName = parent.Xrm.Page.data.entity.getEntityName();

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            // NFP CHANGE: if (!isNullOrUndefined(configRecord.msnfp_paymentgateway) && configRecord.msnfp_paymentgateway != 844060000

            if (entityName == "msnfp_paymentschedule" && status.getValue() === 1 && bankAccount !== null && (giftType.getValue() === 844060003 || giftType.getValue() === 844060014)) { // Bank or EFT
                return isVisible = true;
            }
        }
    }
    return isVisible;
}

// TODO: This does not work correctly due to NFP changes, should see if contact/account has a bank account not if config has a payment gateway.
function HideBankAccountonAccountandContact() {
    var configRecord = null;
    var isVisible = false;
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];


    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        return isVisible = true;
        /*
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_paymentgateway";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            if (!isNullOrUndefined(configRecord.msnfp_paymentgateway) && configRecord.msnfp_paymentgateway != 844060000) {
                //return isVisible = true;
            }
        }*/
    }
    return isVisible;
}

function HideAddNewCreditCard() {
    var isVisible = false;
    var type = parent.Xrm.Page.data.entity.attributes.get("msnfp_type");
    var giftType = parent.Xrm.Page.data.entity.attributes.get("msnfp_gifttype");// 703,650, 002
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");

    if ((type.getValue() === 84406010 && giftType.getValue() === 844060002)//Recurring and CreditCard
        || (status.getValue() === 844060003 && giftType.getValue() === 844060002))//Failed and CreditCard
    {
        isVisible = true;
    }

    return isVisible;
}

function HideAddNewBankAccount() {
    var isVisible = false;
    //var type = parent.Xrm.Page.data.entity.attributes.get("msnfp_type");
    var giftType = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymenttypecode");
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var entityName = parent.Xrm.Page.data.entity.getEntityName();

    if ((entityName == "msnfp_paymentschedule" && (giftType.getValue() === 844060003 || giftType.getValue() === 844060014))//Recurring and BankAccount/EFT
        || (status.getValue() === 844060003 && (giftType.getValue() === 844060003 || giftType.getValue() === 844060014)))//Failed and BankAccount/EFT
    {
        isVisible = true;
    }

    return isVisible;
}

function HideCreditCardGroup() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var giftType = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymenttypecode");
    var isVisible = false;

    if (formType == 2 && giftType.getValue() === 844060002) { // Credit / Debit Card
        return isVisible = true;
    }

    return isVisible;
}

function HideBankAccountGroup() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var giftType = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymenttypecode");
    var entityName = parent.Xrm.Page.data.entity.getEntityName();

    var isVisible = false;

    console.log("entityName: " + entityName);

    if (entityName == "msnfp_transaction" || entityName == "msnfp_paymentschedule") {
        if (formType == 2 && (giftType.getValue() === 844060003 || giftType.getValue() === 844060014)) { // EFT or Bank (Ach)
            return isVisible = true;
        }
    }

    return isVisible;
}

function showAssignCreditCard() {
    var currentEntityName = parent.Xrm.Page.data.entity.getEntityName();
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var customerID = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue();
    if (customerID != null) {
        customerID = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue()[0].id;
        var params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "&customerGUID=" + customerID + "");
    }
    else
        var params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "&customerGUID=" + null + "");


    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/assigncreditcardtogift.html", windowOptions, params);
}

function showAssignBankAccount() {
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var customerID = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue();
    if (customerID != null) {
        customerID = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue()[0].id;
        var params = encodeURIComponent("entityGUID=" + currentEntityGUID + "&customerGUID=" + customerID + "&entityName=" + entityName + "");
    }
    else
        var params = encodeURIComponent("entityGUID=" + currentEntityGUID + "&customerGUID=" + null + "&entityName=" + entityName + "");

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/assignbankaccounttogift.html", windowOptions, params);
}

function OpenRefundPaymentPopup() {
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    var params = encodeURIComponent("entityGUID=" + currentEntityGUID);

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/refundpayment.html", windowOptions, params);
}

function hideRefundPaymentButton() {
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();

    if (formType != 1) // 1 means this is the Create Form => dont show the button
        isVisible = true;

    return isVisible;
}


function hideRefundGiftButton() {
    //var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();

    if (formType != 1) // 1 means this is the Create Form => dont show the button
        isVisible = true;

    return isVisible;
}

function OpenRefundPopup() {
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    var params = encodeURIComponent("entityGUID=" + currentEntityGUID);

    var windowOptions = { height: 800, width: 700 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/refundgift.html", windowOptions, params);
}

function OpenRefundPopupFromView(selectedItems) {
    if (selectedItems.length > 0) {
        var currentEntityGUID = selectedItems[0].Id.replace('{', '').replace('}', '').toUpperCase();

        var params = encodeURIComponent("entityGUID=" + currentEntityGUID);

        var windowOptions = { height: 800, width: 700 };
        Xrm.Navigation.openWebResource("msnfp_/webpages/refundgift.html", windowOptions, params);
    }
}

function HideReturnFailButton() {

    var formType = parent.Xrm.Page.ui.getFormType();
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var isVisible = false;
    if (formType == 2 && status.getValue() === 844060000)//completed
    {
        return isVisible = true;
    }

    return isVisible;
}

function OpenReturnFailPopup(selectedItems) {
    var params;
    if (!isNullOrUndefined(selectedItems) && selectedItems.length > 0) {
        var IDs = [];

        for (var i = 0; i < selectedItems.length; i++) {
            var recordGuid = selectedItems[i].replace('{', '').replace('}', '').toUpperCase();

            var dQuery = "msnfp_transactions?";
            dQuery += "$select=msnfp_transactionid,statuscode";
            dQuery += "&$filter=statuscode eq 844060000 and msnfp_transactionid eq " + recordGuid;
            var dResult = ExecuteQuery(GetWebAPIUrl() + dQuery);

            if (!isNullOrUndefined(dResult))
                IDs.push(recordGuid);
        }
        params = encodeURIComponent("entityGUID=" + IDs + "&isList=1");
    }
    else {
        var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

        params = encodeURIComponent("entityGUID=" + currentEntityGUID + "&isList=0");
    }

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("/msnfp_/webpages/returnfailgift.html", windowOptions, params);
}

function HideReceiptGroup() {
    //var typeControl = parent.Xrm.Page.getControl("msnfp_scheduletypecode");
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var isReceiptingRole = CheckUserInRole('FundraisingandEngagement: Receipting', currentUserID);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);

    if (isReceiptingRole || isSystemAdminRole) {
        if (!isNullOrUndefined(entityName)) {

            if (formType === 2 && entityName == "msnfp_transaction") { // Donation or Recurring
                return isVisible = true;
            }

            if (formType === 2 && entityName == "msnfp_eventpackage") { // Event Package
                return isVisible = true;
            }

            if (entityName == "msnfp_paymentschedule") {
                var typeValue = parent.Xrm.Page.data.entity.attributes.get('msnfp_scheduletypecode').getValue();
                if (formType === 2 && typeValue == 844060003) {
                    return isVisible = true;
                }
            }

        }
    }
    return isVisible;
}

function hideReceiptUpdateButtons() {
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    // NFP CHANGE: Type is no longer used. This was only to check and make sure it was not on a recurring donation. var typeControl = parent.Xrm.Page.getControl("msnfp_type");

    var isReceiptingRole = CheckUserInRole('FundraisingandEngagement: Receipting', currentUserID);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);

    if (isReceiptingRole || isSystemAdminRole) {
        if (formType == 2) {
            var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");
            if (!isNullOrUndefined(receiptControl)) { //NFP CHANGE:&& !isNullOrUndefined(typeControl)) {
                var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
                //NFP CHANGE: var typeValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_type").getValue();
                if (!isNullOrUndefined(receiptValue)) { //NFP CHANGE:&& typeValue != 84406010) {
                    var receiptID = receiptValue[0].id;

                    if (!isNullOrUndefined(receiptID)) {
                        return isVisible = true;
                    }
                }
            }
        }
    }
    return isVisible;
}

function hideReceiptVoidPaymentFailed() {
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");
    var status = parent.Xrm.Page.getControl("statuscode");
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var isReceiptingRole = CheckUserInRole('FundraisingandEngagement: Receipting', currentUserID);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);

    if (isReceiptingRole || isSystemAdminRole) {
        if (!isNullOrUndefined(receiptControl) && !isNullOrUndefined(status)) {
            var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
            var statusValue = parent.Xrm.Page.data.entity.attributes.get("statuscode").getValue();

            if (!isNullOrUndefined(receiptValue) && !isNullOrUndefined(statusValue)) {
                var receiptID = receiptValue[0].id;

                if (formType == 2 && !isNullOrUndefined(receiptID) && statusValue == 844060003)//Failed
                {
                    return isVisible = true;
                }
            }
        }
    }
    return isVisible;
}

function updateRelatedReceiptToVoidPF() {
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");

    if (!isNullOrUndefined(receiptControl)) {
        var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
        if (!isNullOrUndefined(receiptValue)) {
            var receiptID = receiptValue[0].id.replace('{', '').replace('}', '').toUpperCase();

            if (!isNullOrUndefined(receiptID)) {
                var rQuery = "msnfp_receipts?";
                rQuery += "$select=msnfp_receiptid,statuscode";
                rQuery += "&$filter=msnfp_receiptid eq " + receiptID;
                var rResult = ExecuteQuery(GetWebAPIUrl() + rQuery);

                if (!isNullOrUndefined(rResult)) {
                    var rec = {};
                    rec["statuscode"] = 844060002;
                    //rec["msnfp_isupdated"] = true;
                    var qry = GetWebAPIUrl() + "msnfp_receipts(" + receiptID + ")";
                    UpdateRecord(qry, rec);
                    parent.Xrm.Page.data.refresh(true);
                }
            }
        }
    }
}

function updateRelatedReceiptToVoidReissued() {
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");
    if (!isNullOrUndefined(receiptControl)) {
        var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
        if (!isNullOrUndefined(receiptValue)) {
            var receiptID = receiptValue[0].id.replace('{', '').replace('}', '').toUpperCase();

            if (!isNullOrUndefined(receiptID)) {
                var rQuery = "msnfp_receipts?";
                rQuery += "$select=msnfp_receiptid,statuscode";
                rQuery += "&$filter=msnfp_receiptid eq " + receiptID;
                var rResult = ExecuteQuery(GetWebAPIUrl() + rQuery);

                if (!isNullOrUndefined(rResult)) {
                    // Get Donor information from the Transaction
                    console.debug("getting donor info")
                    var donorAttrib = parent.Xrm.Page.getAttribute("msnfp_customerid");
                    var donor = null;
                    if (donorAttrib != null) {
                        if (donorAttrib.getValue() != null) {
                            console.debug("donor val");
                            donor = donorAttrib.getValue()[0];
                            console.debug(donor);
                        }

                    }

                    var rec = {};
                    rec["statuscode"] = 844060001;
                    if (donor != null) {
                        console.debug(donor.id);
                        var donorid = donor.id.replace("{", "").replace("}", "");
                        console.debug(donorid);
                        if (donor.entityType === "contact")
                            rec["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + donorid + ")";
                        else if (donor.entityType === "account")
                            rec["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + donorid + ")";
                    }
                    console.debug(rec);
                    //rec["msnfp_isupdated"] = true;
                    var qry = GetWebAPIUrl() + "msnfp_receipts(" + receiptID + ")";
                    UpdateRecord(qry, rec);
                    parent.Xrm.Page.data.refresh(true);
                }
            }
        }
    }
}

function updateRelatedReceiptToVoid() {
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");

    if (!isNullOrUndefined(receiptControl)) {
        var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
        if (!isNullOrUndefined(receiptValue)) {
            var receiptID = receiptValue[0].id.replace('{', '').replace('}', '').toUpperCase();

            if (!isNullOrUndefined(receiptID)) {
                var rQuery = "msnfp_receipts?";
                rQuery += "$select=msnfp_receiptid,statuscode";
                rQuery += "&$filter=msnfp_receiptid eq " + receiptID;
                var rResult = ExecuteQuery(GetWebAPIUrl() + rQuery);

                if (!isNullOrUndefined(rResult)) {
                    var rec = {};
                    rec["statuscode"] = 844060000;
                    //rec["msnfp_isupdated"] = true;
                    var qry = GetWebAPIUrl() + "msnfp_receipts(" + receiptID + ")";
                    UpdateRecord(qry, rec);
                    parent.Xrm.Page.data.refresh(true);
                }
            }
        }
    }
}

function showGenerateReceiptButton() {
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var receiptPreferenceControl = parent.Xrm.Page.getControl("msnfp_receiptpreferencecode");
    // Do not use for recurring: var typeControl = parent.Xrm.Page.getControl("msnfp_type");
    var entityName = parent.Xrm.Page.data.entity.getEntityName();

    var isReceiptingRole = CheckUserInRole('FundraisingandEngagement: Receipting', currentUserID);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);

    if (isReceiptingRole || isSystemAdminRole) {
        if (!isNullOrUndefined(receiptPreferenceControl) && !isNullOrUndefined(receiptControl) && !isNullOrUndefined(entityName)) {
            var receiptPreferenceValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_receiptpreferencecode").getValue();
            var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
            //var typeValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_type").getValue();

            if (formType == 2 && isNullOrUndefined(receiptValue) && isNullOrUndefined(receiptPreferenceValue) && entityName != "msnfp_paymentschedule") {
                return isVisible = true;
            }
            else if (formType == 2 && isNullOrUndefined(receiptValue) && !isNullOrUndefined(receiptPreferenceValue) && receiptPreferenceValue != 844060000 && entityName != "msnfp_paymentschedule") //not recurring and DO NOT RECEIPT)
            {
                return isVisible = true;
            }
        }
    }
    return isVisible;
}

function showGenerateReceiptButtonOnEventPackge() {
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();
    //console.debug("formType:" + formType);
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var isReceiptingRole = CheckUserInRole('FundraisingandEngagement: Receipting', currentUserID);
    //console.debug("isReceiptingRole:" + isReceiptingRole);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);
    //console.debug("isSystemAdminRole:" + isSystemAdminRole);

    if (isReceiptingRole || isSystemAdminRole) {
        if (!isNullOrUndefined(receiptControl)) {
            var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
            //var typeValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_type").getValue();
            console.debug("receiptValue:" + receiptValue);

            if (formType == 2 && isNullOrUndefined(receiptValue)) {
                return isVisible = true;
            }
        }
    }
    return isVisible;
}

function GenerateReceiptOnGift() {
    var giftID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var selectGift = "msnfp_transactions(" + giftID + ")?$select=msnfp_transactionid,statuscode,msnfp_name,msnfp_bookdate,msnfp_amount_receipted,msnfp_amount_membership,msnfp_amount_nonreceiptable,msnfp_amount,_transactioncurrencyid_value,_msnfp_customerid_value,_msnfp_taxreceiptid_value,msnfp_thirdpartyreceipt,msnfp_typecode";
    var expandGift = "&$expand=msnfp_CustomerId_contact($select=contactid,fullname),msnfp_CustomerId_account($select=accountid,name)";

    var giftResult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + selectGift + expandGift);

    if (!isNullOrUndefined(giftResult)) {
        if ((giftResult.statuscode === 844060000 || giftResult.statuscode === 844060004) && giftResult.msnfp_typecode === 844060000) {
            var dateYear = new Date(giftResult.msnfp_bookdate).getFullYear();

            var qry = "GlobalOptionSetDefinitions(Name='msnfp_receiptyear')";
            var receiptYear = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + qry);
            var receipt = {};

            if (!isNullOrUndefined(receiptYear)) {
                var receiptYearValue = null;
                $(receiptYear.Options).each(function () {
                    if (this.Label.LocalizedLabels[0].Label == dateYear)
                        receiptYearValue = this.Value;
                });

                var configID = parent.Xrm.Page.data.entity.attributes.get("msnfp_configurationid").getValue()[0].id.replace('{', '').replace('}', '').toUpperCase();

                var selectQuery = "msnfp_receiptstacks?$select=msnfp_receiptstackid,msnfp_receiptyear,_msnfp_configurationid_value";
                var filterConfig = "&$filter=msnfp_receiptyear eq " + receiptYearValue + " and _msnfp_configurationid_value eq " + configID;
                var stackResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectQuery + filterConfig);

                if (!isNullOrUndefined(stackResult) && stackResult.length > 0) {
                    Xrm.Utility.showProgressIndicator("Generating Receipt...");
                    var stackRecord = stackResult[0];

                    receipt["msnfp_ReceiptStackId@odata.bind"] = "/msnfp_receiptstacks(" + stackRecord.msnfp_receiptstackid + ")";

                    if (!isNullOrUndefined(giftResult.msnfp_thirdpartyreceipt)) {
                        receipt["msnfp_identifier"] = giftResult.msnfp_thirdpartyreceipt;
                        receipt["msnfp_receiptgeneration"] = 844060002;//Third Party
                    }
                    else {
                        receipt["msnfp_identifier"] = giftResult.msnfp_name + " receipt for " + dateYear;
                        receipt["msnfp_receiptgeneration"] = 844060000;//System Generated
                    }

                    var amount = !isNullOrUndefined(giftResult.msnfp_amount_receipted) ? parseFloat(giftResult.msnfp_amount_receipted) : 0;
                    var amountMembership = !isNullOrUndefined(giftResult.msnfp_amount_membership) ? parseFloat(giftResult.msnfp_amount_membership) : 0;
                    var amountNonreceiptable = !isNullOrUndefined(giftResult.msnfp_amount_nonreceiptable) ? parseFloat(giftResult.msnfp_amount_nonreceiptable) : 0;
                    var totalHeader = !isNullOrUndefined(giftResult.msnfp_amount) ? parseFloat(giftResult.msnfp_amount) : 0;

                    receipt["msnfp_amount_receipted"] = amount;
                    receipt["msnfp_amount_nonreceiptable"] = amountMembership + amountNonreceiptable;
                    receipt["msnfp_generatedorprinted"] = 1;
                    receipt["msnfp_receiptissuedate"] = DSTOffsetYYYYMMDD(new Date());
                    receipt["msnfp_transactioncount"] = 1;
                    receipt["msnfp_amount"] = totalHeader;

                    if (!isNullOrUndefined(giftResult._transactioncurrencyid_value))
                        receipt["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + giftResult._transactioncurrencyid_value + ")";

                    if (!isNullOrUndefined(giftResult.msnfp_CustomerId_contact))
                        receipt["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + giftResult._msnfp_customerid_value + ")";
                    else if (!isNullOrUndefined(giftResult.msnfp_CustomerId_account))
                        receipt["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + giftResult._msnfp_customerid_value + ")";

                    receipt["statuscode"] = 1;

                    var oldReceiptID;
                    // If a receipt existed previously, we can link it here:
                    if (parent.Xrm.Page.getAttribute("msnfp_taxreceiptid").getValue() != null) {
                        oldReceiptID = XrmUtility.CleanGuid(parent.Xrm.Page.getAttribute("msnfp_taxreceiptid").getValue()[0].id);
                        receipt["msnfp_ReplacesReceiptId@odata.bind"] = "/msnfp_receipts(" + oldReceiptID + ")";
                    }

                    var query = XrmServiceUtility.GetWebAPIUrl() + "msnfp_receipts";
                    var receiptID = XrmServiceUtility.CreateRecord(query, receipt);

                    if (!isNullOrUndefined(receiptID)) {
                        var gift = {};
                        gift["msnfp_TaxReceiptId@odata.bind"] = "/msnfp_receipts(" + receiptID + ")";
                        //gift["msnfp_isupdated"] = false;
                        //gift["msnfp_isrefunded"] = false;

                        var qry1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + giftResult.msnfp_transactionid + ")";
                        XrmServiceUtility.UpdateRecord(qry1, gift);
                    }
                    else {
                        Xrm.Utility.closeProgressIndicator();
                        console.log("Receipt not created.");
                    }

                    // Now update the old receipt to be Void (Reissued):
                    if (!isNullOrUndefined(oldReceiptID)) {
                        var rec = {};
                        rec["statuscode"] = 844060001; // Void (Reissued)

                        var qry1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_receipts(" + oldReceiptID + ")";
                        XrmServiceUtility.UpdateRecord(qry1, rec);
                    }

                    Xrm.Utility.closeProgressIndicator();
                    parent.Xrm.Page.data.refresh(true);
                }
                else {
                    Xrm.Utility.closeProgressIndicator();
                    console.log("Could not find receipt stack for receiptYearValue: " + receiptYearValue + " and for config id: " + configID);
                    Xrm.Utility.alertDialog("Could not find receipt stack for given receipt year. Please ensure there is a receipt stack for the year of the transaction.");
                }
            }
            else {
                Xrm.Utility.closeProgressIndicator();
                console.log("Cold not find optionset msnfp_receiptyear: " + receiptYear);
            }
        }
    }
}

function showCreateRelatedReceiptButton() {
    var isVisible = false;
    var formType = parent.Xrm.Page.ui.getFormType();
    var receiptControl = parent.Xrm.Page.getControl("msnfp_taxreceiptid");
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var receiptPreferenceControl = parent.Xrm.Page.getControl("msnfp_receiptpreferencecode");
    var typeControl = parent.Xrm.Page.getControl("msnfp_scheduletypecode");
    var entityName = parent.Xrm.Page.data.entity.getEntityName();

    var isReceiptingRole = CheckUserInRole('FundraisingandEngagement: Receipting', currentUserID);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);

    if (isReceiptingRole || isSystemAdminRole) {
        if (!isNullOrUndefined(receiptPreferenceControl) && !isNullOrUndefined(receiptControl) && !isNullOrUndefined(typeControl) && !isNullOrUndefined(entityName)) {
            var receiptPreferenceValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_receiptpreferencecode").getValue();
            var receiptValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid").getValue();
            var typeValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_scheduletypecode").getValue();

            console.log("Test1 " + typeValue);

            if (formType == 2 && entityName == "msnfp_paymentschedule" && typeValue == 844060003 && //isNullOrUndefined(receiptValue) && typeValue == 84406010 && 
                receiptPreferenceValue != 844060000)//recurring and not equal to DO NOT RECEIPT
            {
                return isVisible = true;
            }
        }
    }
    return isVisible;
}


function CreateRelatedReceiptForEventPackage() {
    console.debug("CreateRelatedReceiptForPaymentSchedule");
    var xrm = XrmUtility.get_Xrm();
    var entityId = parent.Xrm.Page.data.entity.getId().replace(/\{|\}/gi, '');

    try {
        var organizationUrl = parent.Xrm.Page.context.getClientUrl();
        var data = {};
        var query = "msnfp_eventpackages(" + entityId + ")/" + "Microsoft.Dynamics.CRM.msnfp_ActionGenerateReceiptforEventPackage";
        console.debug(organizationUrl + "/api/data/v8.0/" + query);
        var req = new XMLHttpRequest();
        req.open("POST", organizationUrl + "/api/data/v8.0/" + query, true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-Version", "4.0");
        req.onreadystatechange = function () {
            if (this.readyState == 4) {
                console.debug(this);
                req.onreadystatechange = null;
                if (this.status == 204 || this.status == 200) {
                    parent.Xrm.Page.data.refresh(true);
                }
            }
        };
        req.send(window.JSON.stringify(data));
    }
    catch (error) {
        console.log("Receipt Template Error: " + error);
        //errorHandler(error);
    }
}

function CreateRelatedReceiptForPaymentSchedule() {
    console.debug("CreateRelatedReceiptForPaymentSchedule");
    var xrm = XrmUtility.get_Xrm();
    var entityId = parent.Xrm.Page.data.entity.getId().replace(/\{|\}/gi, '');

    //if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_receiptpreferencecode"))) {
    //    var receiptPref = parent.Xrm.Page.data.entity.attributes.get("msnfp_receiptpreferencecode").getValue();
    //    if (isNullOrUndefined(receiptPref)) {
    //        // They need to set a receipt option:
    //        var alertStrings = { confirmButtonLabel: "Okay", text: "Please select a receipt preference for this payment schedule." };
    //        var alertOptions = { height: 120, width: 260 };
    //        Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
    //            function success(result) {
    //                console.log("Alert dialog closed");
    //            },
    //            function (error) {
    //                console.log(error.message);
    //            }
    //        );
    //    }
    //}

    try {
        var organizationUrl = parent.Xrm.Page.context.getClientUrl();
        var data = {};
        //var query = "msnfp_paymentschedules(" + entityId + ")/" + "Microsoft.Dynamics.CRM.msnfp_ActionGiftReceipteaca044965f7e911a813000d3a0c8ff0";
        var query = "msnfp_paymentschedules(" + entityId + ")/" + "Microsoft.Dynamics.CRM.msnfp_ActionGenerateReceiptforPaymentSchedule";
        console.debug(organizationUrl + "/api/data/v8.0/" + query);
        var req = new XMLHttpRequest();
        req.open("POST", organizationUrl + "/api/data/v8.0/" + query, true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-Version", "4.0");
        req.onreadystatechange = function () {
            if (this.readyState == 4) {
                console.debug(this);
                req.onreadystatechange = null;
                if (this.status == 204 || this.status == 200) {
                    parent.Xrm.Page.data.refresh(true);
                }
            }
        };
        req.send(window.JSON.stringify(data));
    }
    catch (error) {
        console.log("Receipt Template Error: " + error);
        //errorHandler(error);
    }
}

function CreateRelatedReceipt() {

    var giftID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var selectGift = "msnfp_paymentschedules(" + giftID + ")?$select=msnfp_paymentscheduleid,msnfp_name,statuscode,msnfp_bookdate,msnfp_amount_receipted,msnfp_amount_membership,msnfp_amount_nonreceiptable,msnfp_recurringamount,_transactioncurrencyid_value,_msnfp_customerid_value,_msnfp_taxreceiptid_value";
    var expandGift = "&$expand=msnfp_CustomerId_contact($select=contactid,fullname),msnfp_CustomerId_account($select=accountid,name)";

    var giftResult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + selectGift + expandGift);
    if (!isNullOrUndefined(giftResult)) {
        var dateYear = new Date(giftResult.msnfp_bookdate).getFullYear();

        var selectChild = "msnfp_transactions?$select=msnfp_transactionid,msnfp_amount_receipted,msnfp_amount_membership,msnfp_amount_nonreceiptable,_msnfp_transaction_paymentscheduleid_value,_msnfp_taxreceiptid_value,msnfp_bookdate,statuscode";
        var filterChild = "&$filter=_msnfp_transaction_paymentscheduleid_value eq " + giftID + " and (statuscode eq 844060000 or statuscode eq 844060004)";//completed or refunded
        // NFP CHANGE: for above previously filtered on 'and _msnfp_taxreceiptid_value eq null'

        var childGiftResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectChild + filterChild);

        var childList = [];

        $(childGiftResult).each(function () {
            if (this.msnfp_bookdate.split('-')[0] == dateYear)
                childList.push(this);
        });

        if (!isNullOrUndefined(childList) && childList.length > 0) {
            var qry = "GlobalOptionSetDefinitions(Name='msnfp_receiptyear')";
            var receiptYear = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + qry);

            if (!isNullOrUndefined(receiptYear)) {
                var receiptYearValue = null;
                $(receiptYear.Options).each(function () {
                    if (this.Label.LocalizedLabels[0].Label == dateYear)
                        receiptYearValue = this.Value;
                });

                var configID = parent.Xrm.Page.data.entity.attributes.get("msnfp_configurationid").getValue()[0].id.replace('{', '').replace('}', '').toUpperCase();

                var selectQuery = "msnfp_receiptstacks?$select=msnfp_receiptstackid,msnfp_receiptyear,_msnfp_configurationid_value";
                var filterConfig = "&$filter=msnfp_receiptyear eq " + receiptYearValue + " and _msnfp_configurationid_value eq " + configID;
                var stackResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectQuery + filterConfig);

                if (!isNullOrUndefined(stackResult) && stackResult.length > 0) {
                    Xrm.Utility.showProgressIndicator("Generating Receipts...");
                    var stackRecord = stackResult[0];
                    var receipt = {};
                    receipt["msnfp_ReceiptStackId@odata.bind"] = "/msnfp_receiptstacks(" + stackRecord.msnfp_receiptstackid + ")";

                    // NFP CHANGE: var amount = !isNullOrUndefined(giftResult.msnfp_amount_receipted) ? parseFloat(giftResult.msnfp_amount_receipted) : 0;
                    // NFP CHANGE: var amountMembership = !isNullOrUndefined(giftResult.msnfp_amount_membership) ? parseFloat(giftResult.msnfp_amount_membership) : 0;
                    // NFP CHANGE: var amountNonreceiptable = !isNullOrUndefined(giftResult.msnfp_amount_nonreceiptable) ? parseFloat(giftResult.msnfp_amount_nonreceiptable) : 0;
                    // NFP CHANGE: var totalHeader = !isNullOrUndefined(giftResult.msnfp_recurringamount) ? parseFloat(giftResult.msnfp_recurringamount) : 0;

                    var amount = 0;
                    var amountMembership = 0;
                    var amountNonreceiptable = 0;

                    // NFP CHANGE:
                    // Add up all completed/refunded children and add them to the receipt total:
                    $(childList).each(function () {
                        amount = amount + this.msnfp_amount_receipted;
                        amountMembership = amountMembership + this.msnfp_amount_membership;
                        amountNonreceiptable = amountNonreceiptable + this.msnfp_amount_nonreceiptable;
                    });

                    receipt["msnfp_amount_receipted"] = amount;
                    receipt["msnfp_amount_nonreceiptable"] = amountMembership + amountNonreceiptable;
                    receipt["msnfp_generatedorprinted"] = 1;
                    receipt["msnfp_receiptgeneration"] = 844060000;//System Generated
                    receipt["msnfp_receiptissuedate"] = DSTOffsetYYYYMMDD(new Date());

                    receipt["msnfp_identifier"] = giftResult.msnfp_name + " receipt for " + dateYear;

                    receipt["msnfp_transactioncount"] = childList.length; //1;
                    receipt["msnfp_amount"] = amount + amountMembership; //totalHeader;

                    if (!isNullOrUndefined(giftResult._transactioncurrencyid_value))
                        receipt["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + giftResult._transactioncurrencyid_value + ")";

                    if (!isNullOrUndefined(giftResult.msnfp_CustomerId_contact))
                        receipt["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + giftResult._msnfp_customerid_value + ")";
                    else if (!isNullOrUndefined(giftResult.msnfp_customerid_account))
                        receipt["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + giftResult._msnfp_customerid_value + ")";

                    receipt["statuscode"] = 1;
                    receipt["msnfp_PaymentScheduleId@odata.bind"] = "/msnfp_paymentschedules(" + giftID + ")";

                    var oldReceiptID;
                    // If a receipt existed previously, we can link it here:
                    if (parent.Xrm.Page.getAttribute("msnfp_taxreceiptid").getValue() != null) {
                        oldReceiptID = XrmUtility.CleanGuid(parent.Xrm.Page.getAttribute("msnfp_taxreceiptid").getValue()[0].id);
                        receipt["msnfp_ReplacesReceiptId@odata.bind"] = "/msnfp_receipts(" + oldReceiptID + ")";
                    }

                    var query = XrmServiceUtility.GetWebAPIUrl() + "msnfp_receipts";
                    var receiptID = XrmServiceUtility.CreateRecord(query, receipt);

                    // Now link the receipt to the payment schedule:
                    if (!isNullOrUndefined(receiptID)) {
                        var gift = {};
                        gift["msnfp_TaxReceiptId@odata.bind"] = "/msnfp_receipts(" + receiptID + ")";

                        var qry1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_paymentschedules(" + giftID + ")";
                        XrmServiceUtility.UpdateRecord(qry1, gift);
                    }
                    else {
                        Xrm.Utility.closeProgressIndicator();
                        console.log("Receipt not created.");
                    }

                    // Now link the children to the receipt:
                    if (!isNullOrUndefined(receiptID) && !isNullOrUndefined(childList)) {
                        $(childList).each(function () {
                            var gift = {};
                            gift["msnfp_TaxReceiptId@odata.bind"] = "/msnfp_receipts(" + receiptID + ")";

                            var qry1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + this.msnfp_transactionid + ")";
                            XrmServiceUtility.UpdateRecord(qry1, gift);
                        });
                    }
                    else {
                        Xrm.Utility.closeProgressIndicator();
                        console.log("Child Receipts not created.");
                    }

                    // Now update the old receipt to be Void (Reissued):
                    if (!isNullOrUndefined(oldReceiptID)) {
                        var rec = {};
                        rec["statuscode"] = 844060001; // Void (Reissued)

                        var qry1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_receipts(" + oldReceiptID + ")";
                        XrmServiceUtility.UpdateRecord(qry1, rec);
                    }

                    Xrm.Utility.closeProgressIndicator();
                    parent.Xrm.Page.data.refresh(true);
                }
                else {
                    Xrm.Utility.closeProgressIndicator();
                }
            }
        }
    }
}



//on Contact and Account entity Ribbon
function showUpdatePrimaryAddress() {
    Xrm.Utility.confirmDialog("Would you like to update the Address? Selecting OK will push the primary address of this record to any related open recurring Donations or Pledges", updatePrimaryAddress, cancelUpdate);
}

function updatePrimaryAddress() {
    var currentRecordId = parent.Xrm.Page.data.entity.getId();

    var str = "";
    str += "msnfp_donationpledges?";
    str += "$select=msnfp_donationpledgeid,msnfp_billing_city,msnfp_billing_country,msnfp_billing_line1,msnfp_billing_line2,msnfp_billing_line3,msnfp_billing_postalcode,msnfp_billing_stateorprovince,_msnfp_customerid_value&";
    str += "$filter=_msnfp_customerid_value eq " + XrmUtility.CleanGuid(currentRecordId) + "";
    str += " and (msnfp_type eq 84406001 or msnfp_type eq 84406010 or msnfp_type eq 84406003) and statuscode eq 1";

    var queryResults = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + str);

    if (!isNullOrUndefined(queryResults)) {
        var relatedDPID = "";
        for (var i = 0; i < queryResults.length; i++) {
            relatedDPID = queryResults[i].msnfp_donationpledgeid;

            var relatedDP = {};

            relatedDP["msnfp_billing_line1"] = parent.Xrm.Page.getAttribute("address1_line1").getValue();
            relatedDP["msnfp_billing_line2"] = parent.Xrm.Page.getAttribute("address1_line2").getValue();
            relatedDP["msnfp_billing_line3"] = parent.Xrm.Page.getAttribute("address1_line3").getValue();
            relatedDP["msnfp_billing_city"] = parent.Xrm.Page.getAttribute("address1_city").getValue();
            relatedDP["msnfp_billing_stateorprovince"] = parent.Xrm.Page.getAttribute("address1_stateorprovince").getValue();
            relatedDP["msnfp_billing_country"] = parent.Xrm.Page.getAttribute("address1_country").getValue();
            relatedDP["msnfp_billing_postalcode"] = parent.Xrm.Page.getAttribute("address1_postalcode").getValue();

            var str1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpledges(" + relatedDPID + ")";

            var updatedRecordID = XrmServiceUtility.UpdateRecord(str1, relatedDP);
        }
    }
}

function cancelUpdate() {
    //Alert.hide();
}

function showUpdateSecondaryAddress() {
    Xrm.Utility.confirmDialog("Would you like to update the Address? Selecting OK will push the secondary address of this record to any related open recurring Donations or Pledges", updateSecondaryAddress, cancelUpdate);
}

function updateSecondaryAddress() {
    var currentRecordId = parent.Xrm.Page.data.entity.getId();

    var str = "";
    str += "msnfp_donationpledges?";
    str += "$select=msnfp_donationpledgeid,msnfp_billing_city,msnfp_billing_country,msnfp_billing_line1,msnfp_billing_line2,msnfp_billing_line3,msnfp_billing_postalcode,msnfp_billing_stateorprovince,_msnfp_customerid_value&";
    str += "$filter=_msnfp_customerid_value eq " + XrmUtility.CleanGuid(currentRecordId) + "";
    str += " and (msnfp_type eq 84406001 or msnfp_type eq 84406010 or msnfp_type eq 84406003) and statuscode eq 1";

    var queryResults = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + str);

    if (queryResults != null && queryResults != undefined && queryResults.length > 0) {
        var relatedDPID = "";
        for (var i = 0; i < queryResults.length; i++) {
            relatedDPID = queryResults[i].msnfp_donationpledgeid;

            var relatedDP = {};

            relatedDP["msnfp_billing_line1"] = parent.Xrm.Page.getAttribute("address2_line1").getValue();
            relatedDP["msnfp_billing_line2"] = parent.Xrm.Page.getAttribute("address2_line2").getValue();
            relatedDP["msnfp_billing_line3"] = parent.Xrm.Page.getAttribute("address2_line3").getValue();
            relatedDP["msnfp_billing_city"] = parent.Xrm.Page.getAttribute("address2_city").getValue();
            relatedDP["msnfp_billing_stateorprovince"] = parent.Xrm.Page.getAttribute("address2_stateorprovince").getValue();
            relatedDP["msnfp_billing_country"] = parent.Xrm.Page.getAttribute("address2_country").getValue();
            relatedDP["msnfp_billing_postalcode"] = parent.Xrm.Page.getAttribute("address2_postalcode").getValue();

            var str1 = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpledges(" + relatedDPID + ")";

            var updatedRecordID = XrmServiceUtility.UpdateRecord(str1, relatedDP);
        }
    }
}

function showNewGift() {

    var options = { openInNewWindow: false };
    var parameters = {};
    parameters["param_msnfp_customerid"] = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    if (entityName == "account") {
        parameters["param_msnfp_customeridname"] = parent.Xrm.Page.data.entity.attributes.get("name").getValue();
        parameters["param_msnfp_customeridtype"] = "account";
    }
    else {
        var firstName = parent.Xrm.Page.data.entity.attributes.get("firstname").getValue();
        var lastName = parent.Xrm.Page.data.entity.attributes.get("lastname").getValue();
        parameters["param_msnfp_customeridname"] = firstName + " " + lastName;
        parameters["param_msnfp_customeridtype"] = "contact";
    }
    Xrm.Utility.openEntityForm("msnfp_transaction", null, parameters, options);
}

function showNewMembership() {
    var options = { openInNewWindow: true };
    var parameters = {};
    parameters["param_msnfp_customerid"] = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    if (entityName === "account") {
        parameters["param_msnfp_customeridname"] = parent.Xrm.Page.data.entity.attributes.get("name").getValue();
        parameters["param_msnfp_customeridtype"] = "account";
    }
    else {
        var firstName = parent.Xrm.Page.data.entity.attributes.get("firstname").getValue();
        var lastName = parent.Xrm.Page.data.entity.attributes.get("lastname").getValue();
        parameters["param_msnfp_customeridname"] = firstName + " " + lastName;
        parameters["param_msnfp_customeridtype"] = "contact";
    }
    Xrm.Utility.openEntityForm("msnfp_membership", null, parameters, options);
}

//show the membership ribbon button when the msnfp_configuration.msnfp_showmembership = TRUE (Account and Contact)
function ShowMembershipAccountORContact() {
    var configRecord = null;
    var isVisible = false;

    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];

    /*
    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value"
    userSelect += "&$filter=systemuserid eq " + currentuserID;
    var user = ExecuteQuery(GetWebAPIUrl() + userSelect);
    user = user[0];
    */

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_tran_membership";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        console.log("_msnfp_configurationid_value: " + user._msnfp_configurationid_value);
        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            if (!isNullOrUndefined(configRecord.msnfp_tran_membership) && configRecord.msnfp_tran_membership) {
                return isVisible = true;
            }
        }
    }
    return isVisible;
}

function showNewPledgeMatch() {

    console.log("showNewPledgeMatch()");

    var options = { openInNewWindow: true };
    var parameters = {};
    parameters["param_msnfp_customerfromid"] = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    if (entityName === "account") {
        parameters["param_msnfp_customerfromidname"] = parent.Xrm.Page.data.entity.attributes.get("name").getValue();;
        parameters["param_msnfp_customerfromidtype"] = "account";
    }
    else {
        var firstName = parent.Xrm.Page.data.entity.attributes.get("firstname").getValue();
        var lastName = parent.Xrm.Page.data.entity.attributes.get("lastname").getValue();
        parameters["param_msnfp_customerfromidname"] = firstName + " " + lastName;
        parameters["param_msnfp_customerfromidtype"] = "contact";
    }
    Xrm.Utility.openEntityForm("msnfp_pledgematch", null, parameters, options);
}

function showNewSoftCreditMatch() {
    console.log("showNewSoftCreditMatch()");
    var options = { openInNewWindow: true };
    var parameters = {};
    parameters["param_msnfp_customerfromid"] = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    if (entityName == "account") {
        parameters["param_msnfp_customerfromidname"] = parent.Xrm.Page.data.entity.attributes.get("name").getValue();;
        parameters["param_msnfp_customerfromidtype"] = "account";
    }
    else {
        var firstName = parent.Xrm.Page.data.entity.attributes.get("firstname").getValue();
        var lastName = parent.Xrm.Page.data.entity.attributes.get("lastname").getValue();
        parameters["param_msnfp_customerfromidname"] = firstName + " " + lastName;
        parameters["param_msnfp_customerfromidtype"] = "contact";
    }
    Xrm.Utility.openEntityForm("msnfp_softcredit", null, parameters, options);
}

function showNewPackage() {
    var options = { openInNewWindow: false };
    var parameters = {};
    parameters["param_msnfp_customerid"] = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();
    var entityName = parent.Xrm.Page.data.entity.getEntityName();
    if (entityName === "account") {
        parameters["param_msnfp_customeridname"] = parent.Xrm.Page.data.entity.attributes.get("name").getValue();;
        parameters["param_msnfp_customeridtype"] = "account";
    }
    else {
        var firstName = parent.Xrm.Page.data.entity.attributes.get("firstname").getValue();
        var lastName = parent.Xrm.Page.data.entity.attributes.get("lastname").getValue();
        parameters["param_msnfp_customeridname"] = firstName + " " + lastName;
        parameters["param_msnfp_customeridtype"] = "contact";
    }
    Xrm.Utility.openEntityForm("msnfp_eventpackage", null, parameters, options);
}

function showAddNewCreditCard() {
    var currentEntityName = parent.Xrm.Page.data.entity.getEntityName();
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "");

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/addnewcreditcard.html", windowOptions, params);
}

function showAddNewBankAccount() {
    var currentEntityName = parent.Xrm.Page.data.entity.getEntityName();
    var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    var params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "");

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/addnewbankaccount.html", windowOptions, params);
}



//On Fund entity Ribbon
function CloneRecord() {
    var cloneData = {};
    var originRecordType = parent.Xrm.Page.data.entity.getEntityName();
    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var oDataUri = parent.Xrm.Page.context.getClientUrl() + "/api/data/v8.2/" +
        originRecordType + "s?$select=*&$filter=msnfp_fundid eq " + originRecordGuid;

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data) {
                cloneData = setFieldValue(data, originRecordType, originRecordGuid);

                replacer = function (key, value) {
                    if (key == "modifiedon" || key == originRecordType + "id" ||
                        key == "createdon" || key == "statecode" ||
                        key == "statuscode") {
                        return undefined;
                    }
                    else return value;
                }
                //Create new Activity
                var oDataUri = parent.Xrm.Page.context.getClientUrl() + "/api/data/v8.2/" + originRecordType + "s";

                jQuery.support.cors = true;
                jQuery.ajax({
                    type: "POST",
                    contentType: "application/json; charset=utf-8",
                    datatype: "json",
                    url: oDataUri,
                    async: false, //Synchronous operation 
                    data: JSON.stringify(cloneData, replacer),
                    beforeSend: function (XMLHttpRequest) {
                        //Specifying this header ensures that the results will be returned as JSON.           
                        XMLHttpRequest.setRequestHeader("Accept", "application/json");
                    },
                    success: function (data, textStatus, XmlHttpRequest) {
                        var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
                        recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
                        OpenRecords(recordID, "msnfp_fund");
                    },
                    error: function (XmlHttpRequest, textStatus, errorThrown) {
                        alert("Error :  has occured during creation of the activity "
                            + originRecordType.text + ": " +
                            XmlHttpRequest.responseText);
                    }
                });
            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + originRecordType.text + ": " +
                XmlHttpRequest.responseText);
        }
    });
}

function setFieldValue(data, originRecordType, originRecordGuid) {
    var cloneData = {};
    cloneData["msnfp_originatingfundid@odata.bind"] = "/" + originRecordType + "s(" + originRecordGuid + ")";

    if (!isNullOrUndefined(data.value[0].msnfp_title))
        cloneData["msnfp_title"] = data.value[0].msnfp_title;

    if (!isNullOrUndefined(data.value[0].msnfp_validfrom))
        cloneData["msnfp_validfrom"] = data.value[0].msnfp_validfrom;

    if (!isNullOrUndefined(data.value[0].msnfp_validto))
        cloneData["msnfp_validto"] = data.value[0].msnfp_validto;

    if (!isNullOrUndefined(data.value[0].msnfp_fundtype))
        cloneData["msnfp_fundtype"] = data.value[0].msnfp_fundtype;

    if (!isNullOrUndefined(data.value[0].msnfp_fundname))
        cloneData["msnfp_fundname"] = data.value[0].msnfp_fundname;

    if (!isNullOrUndefined(data.value[0].msnfp_fundidentifier))
        cloneData["msnfp_fundidentifier"] = data.value[0].msnfp_fundidentifier;

    if (!isNullOrUndefined(data.value[0].msnfp_ytdtotalpledgeamout))
        cloneData["msnfp_ytdtotalpledgeamout"] = data.value[0].msnfp_ytdtotalpledgeamout;

    if (!isNullOrUndefined(data.value[0].msnfp_ytdtotaldonationamount))
        cloneData["msnfp_ytdtotaldonationamount"] = data.value[0].msnfp_ytdtotaldonationamount;

    if (!isNullOrUndefined(data.value[0].msnfp_ytdtotalamount))
        cloneData["msnfp_ytdtotalamount"] = data.value[0].msnfp_ytdtotalamount;

    return cloneData;
}

function SetSolicitTypeAndDateContact(SelectedRecordGUID) {

    if (typeof ($) === 'undefined') {
        $ = parent.$;
        jQuery = parent.jQuery;
    }

    Xrm.Utility.showProgressIndicator("Setting Solicitation Statuses");
    //the parameter will receive all GUIDS in comma separated
    for (var i = 0; i < SelectedRecordGUID.length; i++) {

        var currentRecord = {};
        currentRecord["msnfp_solicitcode"] = 844060000;
        var dt = new Date();
        currentRecord["msnfp_solicitdate"] = dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();

        var qry = GetWebAPIUrl() + "contacts(" + SelectedRecordGUID[i] + ")";
        UpdateRecord(qry, currentRecord);
    }
    Xrm.Utility.closeProgressIndicator();
}

function SetSolicitTypeAndDateAccount(SelectedRecordGUID) {

    if (typeof ($) === 'undefined') {
        $ = parent.$;
        jQuery = parent.jQuery;
    }

    Xrm.Utility.showProgressIndicator("Setting Solicitation Statuses");
    //the parameter will receive all GUIDS in comma separated
    for (var i = 0; i < SelectedRecordGUID.length; i++) {

        var currentRecord = {};
        currentRecord["msnfp_solicitcode"] = 844060000;
        var dt = new Date();
        currentRecord["msnfp_solicitdate"] = dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();

        var qry = GetWebAPIUrl() + "accounts(" + SelectedRecordGUID[i] + ")";
        UpdateRecord(qry, currentRecord);
    }
    Xrm.Utility.closeProgressIndicator();
}


function isNullOrUndefined(value) {
    return (typeof (value) === "undefined" || value === null);
}

function OpenRecords(Record_Id, Logical_Name_Of_Entity) {
    var reletivePath = "/main.aspx?etn=" + Logical_Name_Of_Entity;
    reletivePath = reletivePath + "&pagetype=entityrecord&id=";

    var serverUrl = parent.Xrm.Page.context.getClientUrl();

    if (serverUrl != null && serverUrl != "" && Record_Id.replace("{", "").replace("}", "") != null) {
        serverUrl = serverUrl + reletivePath;
        serverUrl = serverUrl + Record_Id.replace("{", "").replace("}", "");
        window.open(serverUrl, "_top");
    }
}

function CloneListRecord(selectedItems) {
    if (selectedItems.length > 0) {
        for (var i = 0; i < selectedItems.length; i++) {
            var cloneData = {};
            var originRecordType = selectedItems[i].TypeName;
            var originRecordGuid = selectedItems[i].Id.replace('{', '').replace('}', '').toUpperCase();

            var oDataUri = parent.Xrm.Page.context.getClientUrl() + "/api/data/v8.2/" +
                originRecordType + "s?$select=*&$filter=msnfp_fundid eq " + originRecordGuid;

            jQuery.support.cors = true;
            jQuery.ajax({
                type: "GET",
                contentType: "application/json; charset=utf-8",
                datatype: "json",
                url: oDataUri,
                async: false, //Synchronous operation 
                beforeSend: function (XMLHttpRequest) {
                    //Specifying this header ensures that the results will be returned as JSON.           
                    XMLHttpRequest.setRequestHeader("Accept", "application/json");
                },
                success: function (data, textStatus, XmlHttpRequest) {
                    if (data) {
                        cloneData = setFieldValue(data, originRecordType, originRecordGuid);

                        replacer = function (key, value) {
                            if (key == "modifiedon" || key == originRecordType + "id" ||
                                key == "createdon" || key == "statecode" ||
                                key == "statuscode") {
                                return undefined;
                            }
                            else return value;
                        }
                        //Create new Activity
                        var oDataUri = parent.Xrm.Page.context.getClientUrl() + "/api/data/v8.2/" + originRecordType + "s";

                        jQuery.support.cors = true;
                        jQuery.ajax({
                            type: "POST",
                            contentType: "application/json; charset=utf-8",
                            datatype: "json",
                            url: oDataUri,
                            async: false, //Synchronous operation 
                            data: JSON.stringify(cloneData, replacer),
                            beforeSend: function (XMLHttpRequest) {
                                //Specifying this header ensures that the results will be returned as JSON.           
                                XMLHttpRequest.setRequestHeader("Accept", "application/json");
                            },
                            success: function (data, textStatus, XmlHttpRequest) {
                                if (data && data.d) {
                                    //OpenRecords(data.d.msnfp_fundId, "msnfp_fund");
                                }
                            },
                            error: function (XmlHttpRequest, textStatus, errorThrown) {
                                alert("Error :  has occured during creation of the activity "
                                    + originRecordType.text + ": " +
                                    XmlHttpRequest.responseText);
                            }
                        });
                    }
                    else {
                        //No data returned
                    }
                },
                error: function (XmlHttpRequest, textStatus, errorThrown) {
                    alert("Error :  has occured during retrieving of the activity "
                        + originRecordType.text + ": " +
                        XmlHttpRequest.responseText);
                }
            });
        }

        window.location.reload(true);
    }
}

//On Event entity Ribbon
function CloneEventRecord() {
    var originRecordType = parent.Xrm.Page.data.entity.getEntityName();
    var originRecordGuid = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    if (originRecordGuid != null) {

        var copyEventID = CreateEvent(originRecordType, originRecordGuid);

        if (!isNullOrUndefined(copyEventID)) {

            // Completed all actions, show this to the user so they do not click again:
            var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
            var alertOptions = { height: 120, width: 260 };
            Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                function success(result) {
                    console.log("Alert dialog closed");
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
    }
}

function HideCloneEventButton() {
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2) {
        return isVisible = true;
    }
    return isVisible;
}

function CreateEvent(originRecordType, originRecordGuid) {

    if (typeof ($) === 'undefined') {
        $ = parent.$;
        jQuery = parent.jQuery;
    }

    var s = "EntityDefinitions?$select=EntitySetName,LogicalName&$filter=LogicalName eq '" + originRecordType + "'";
    var pluralName = (ExecuteQuery(GetWebAPIUrl() + s))[0].EntitySetName;

    var cQuery = pluralName + "?";
    cQuery += "$select=msnfp_eventid,msnfp_name,statuscode,statecode,msnfp_budget,msnfp_amount,msnfp_goal,msnfp_proposedend,overriddencreatedon,";
    cQuery += "msnfp_proposedstart,exchangerate,msnfp_description,";
    cQuery += "msnfp_eventtypecode,msnfp_map_city,msnfp_map_postalcode,msnfp_map_line1,msnfp_map_line2,";
    cQuery += "msnfp_stateorprovince,msnfp_map_line3,msnfp_identifier,msnfp_map_country,";
    cQuery += "utcconversiontimezonecode,msnfp_capacity,importsequencenumber,";
    cQuery += "timezoneruleversionnumber,_msnfp_venueid_value,_msnfp_configurationid_value,_owningbusinessunit_value,_owningteam_value,";
    cQuery += "_modifiedonbehalfby_value,_owninguser_value,_ownerid_value,_transactioncurrencyid_value,_msnfp_appealid_value,_msnfp_campaignid_value,_msnfp_designationid_value,_msnfp_packageid_value&";
    cQuery += "$filter=msnfp_eventid eq " + originRecordGuid;

    var event = ExecuteQuery(GetWebAPIUrl() + cQuery);
    var newEventGuid = null;

    if (event != null && event != undefined) {
        var CloneEvent = {};

        CloneEvent["msnfp_name"] = event[0].msnfp_name + " - Clone";
        CloneEvent["statuscode"] = event[0].statuscode;
        CloneEvent["statecode"] = event[0].statecode;
        //CloneEvent["msnfp_costamount"] = event[0].msnfp_costamount;
        CloneEvent["msnfp_budget"] = event[0].msnfp_budget;
        CloneEvent["msnfp_amount"] = event[0].msnfp_amount;
        CloneEvent["msnfp_goal"] = event[0].msnfp_goal;
        CloneEvent["msnfp_proposedend"] = event[0].msnfp_proposedend;
        CloneEvent["overriddencreatedon"] = event[0].overriddencreatedon;
        CloneEvent["msnfp_proposedstart"] = event[0].msnfp_proposedstart;
        //CloneEvent["msnfp_costpercentage"] = event[0].msnfp_costpercentage;
        CloneEvent["exchangerate"] = event[0].exchangerate;
        CloneEvent["msnfp_sponsorship"] = event[0].msnfp_sponsorship;
        //CloneEvent["msnfp_invoicemessage"] = event[0].msnfp_invoicemessage;
        //CloneEvent["msnfp_thankyou"] = event[0].msnfp_thankyou;
        CloneEvent["msnfp_eventtypecode"] = event[0].msnfp_eventtypecode;
        //CloneEvent["msnfp_labellanguagecode"] = event[0].msnfp_labellanguagecode;
        CloneEvent["msnfp_map_city"] = event[0].msnfp_map_city;
        CloneEvent["msnfp_map_postalcode"] = event[0].msnfp_map_postalcode;
        CloneEvent["msnfp_map_line1"] = event[0].msnfp_map_line1;
        CloneEvent["msnfp_map_line2"] = event[0].msnfp_map_line2;
        //CloneEvent["msnfp_homepageurl"] = event[0].msnfp_homepageurl;
        //CloneEvent["msnfp_externalurl"] = event[0].msnfp_externalurl;
        //CloneEvent["msnfp_largeimage"] = event[0].msnfp_largeimage;
        CloneEvent["msnfp_stateorprovince"] = event[0].msnfp_stateorprovince;
        CloneEvent["msnfp_map_line3"] = event[0].msnfp_map_line3;
        //CloneEvent["msnfp_smallimage"] = event[0].msnfp_smallimage;
        CloneEvent["msnfp_identifier"] = event[0].msnfp_identifier;
        CloneEvent["msnfp_map_country"] = event[0].msnfp_map_country;
        //CloneEvent["msnfp_showapple"] = event[0].msnfp_showapple;
        //CloneEvent["msnfp_showgiftaid"] = event[0].msnfp_showgiftaid;
        //CloneEvent["msnfp_forceredirect"] = event[0].msnfp_forceredirect;
        //CloneEvent["msnfp_showcovercosts"] = event[0].msnfp_showcovercosts;
        //CloneEvent["msnfp_setsignup"] = event[0].msnfp_setsignup;
        //CloneEvent["msnfp_showinvoice"] = event[0].msnfp_showinvoice;
        //CloneEvent["msnfp_setcovercosts"] = event[0].msnfp_setcovercosts;
        //CloneEvent["msnfp_freeevent"] = event[0].msnfp_freeevent;
        //CloneEvent["msnfp_setacceptnotice"] = event[0].msnfp_setacceptnotice;
        //CloneEvent["msnfp_showcompany"] = event[0].msnfp_showcompany;
        //CloneEvent["msnfp_showmap"] = event[0].msnfp_showmap;
        //CloneEvent["msnfp_showpaypal"] = event[0].msnfp_showpaypal;
        //CloneEvent["msnfp_selectcurrency"] = event[0].msnfp_selectcurrency;
        //CloneEvent["msnfp_showgoogle"] = event[0].msnfp_showgoogle;
        //CloneEvent["msnfp_showcreditcard"] = event[0].msnfp_showcreditcard;
        CloneEvent["utcconversiontimezonecode"] = event[0].utcconversiontimezonecode;
        CloneEvent["msnfp_capacity"] = event[0].msnfp_capacity;
        //CloneEvent["msnfp_forceredirecttiming"] = event[0].msnfp_forceredirecttiming;
        CloneEvent["importsequencenumber"] = event[0].importsequencenumber;
        CloneEvent["timezoneruleversionnumber"] = event[0].timezoneruleversionnumber;

        // binding lookup fields
        if (event[0]._msnfp_venueid_value != null)
            CloneEvent["msnfp_VenueId@odata.bind"] = "/msnfp_appeals(" + event[0]._msnfp_venueid_value + ")";

        if (event[0]._msnfp_configurationid_value != null)
            CloneEvent["msnfp_ConfigurationId@odata.bind"] = "/msnfp_configurations(" + event[0]._msnfp_configurationid_value + ")";

        //if (event[0]._msnfp_termsofreferenceid_value != null)
        //    CloneEvent["msnfp_TermsOfReferenceId@odata.bind"] = "/msnfp_termsofreferences(" + event[0]._msnfp_termsofreferenceid_value + ")";

        if (event[0]._ownerid_value != null)
            CloneEvent["ownerid@odata.bind"] = "/systemusers(" + event[0]._ownerid_value + ")";

        if (event[0]._transactioncurrencyid_value != null)
            CloneEvent["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + event[0]._transactioncurrencyid_value + ")";

        if (event[0]._msnfp_appealid_value != null)
            CloneEvent["msnfp_AppealId@odata.bind"] = "/msnfp_appeals(" + event[0]._msnfp_appealid_value + ")";

        if (event[0]._msnfp_campaignid_value != null)
            CloneEvent["msnfp_CampaignId@odata.bind"] = "/campaigns(" + event[0]._msnfp_campaignid_value + ")";

        if (event[0]._msnfp_packageid_value != null)
            CloneEvent["msnfp_PackageId@odata.bind"] = "/msnfp_packages(" + event[0]._msnfp_packageid_value + ")";

        if (event[0]._msnfp_designationid_value != null)
            CloneEvent["msnfp_DesignationId@odata.bind"] = "/msnfp_designations(" + event[0]._msnfp_designationid_value + ")";

        var qry = GetWebAPIUrl() + pluralName;

        //creating event record
        newEventGuid = CreateRecord(qry, CloneEvent);

        //cloning event disclaimers
        cloneEventDisclaimers(originRecordGuid, newEventGuid);

        //cloning event tickets
        cloneEventTickets(originRecordGuid, newEventGuid);

        //cloning event donations
        cloneEventDonations(originRecordGuid, newEventGuid);

        //cloning event products
        cloneEventProducts(originRecordGuid, newEventGuid);

        //cloning event sponsorship
        cloneEventSponsorship(originRecordGuid, newEventGuid);

        //cloning event preferences
        cloneEventPreferences(originRecordGuid, newEventGuid);
    }

    return newEventGuid;
}


function cloneEventDisclaimers(originEventGuid, newEventGuid) {

    var str = "";
    str += "msnfp_eventdisclaimers?";
    str += "$select=msnfp_eventdisclaimerid,importsequencenumber, msnfp_description,msnfp_identifier,msnfp_name,";
    str += "_ownerid_value,statecode,statuscode,timezoneruleversionnumber,utcconversiontimezonecode&";
    str += "$filter=statecode eq 0 and _msnfp_eventid_value eq " + originEventGuid + "";

    var queryResults = ExecuteQuery(GetWebAPIUrl() + str);

    if (!isNullOrUndefined(queryResults)) {

        for (var i = 0; i < queryResults.length; i++) {
            var newED = {};
            newED["msnfp_EventId@odata.bind"] = "/msnfp_events(" + newEventGuid + ")";
            newED["msnfp_name"] = "COPY: " + queryResults[i].msnfp_name;

            // binding NOT lookup fields
            newED["msnfp_description"] = queryResults[i].msnfp_description;
            newED["msnfp_identifier"] = queryResults[i].msnfp_identifier;
            newED["statecode"] = queryResults[i].statecode;
            newED["statuscode"] = queryResults[i].statuscode;
            newED["timezoneruleversionnumber"] = queryResults[i].timezoneruleversionnumber;
            newED["utcconversiontimezonecode"] = queryResults[i].utcconversiontimezonecode;

            // binding lookup fields
            if (queryResults[i]._ownerid_value != null)
                newED["ownerid@odata.bind"] = "/systemusers(" + queryResults[i]._ownerid_value + ")";

            var qry = GetWebAPIUrl() + "msnfp_eventdisclaimers";

            CreateRecord(qry, newED);
        }
    }
}

function cloneEventTickets(originEventGuid, newEventGuid) {

    var str = "";
    str += "msnfp_eventtickets?";
    str += "$select=msnfp_eventticketid,msnfp_name,exchangerate,msnfp_amount,msnfp_amount_nonreceiptable,msnfp_amount_receipted,msnfp_amount_tax,";
    str += "statecode,statuscode,_msnfp_customerid_value,msnfp_date,msnfp_description,msnfp_groupnotes,msnfp_identifier,";
    str += "msnfp_maxspots,msnfp_registrationsperticket,msnfp_val_sold,msnfp_tickets,msnfp_quantity,_ownerid_value,timezoneruleversionnumber,_transactioncurrencyid_value,utcconversiontimezonecode&";
    str += "$filter=statecode eq 0 and _msnfp_eventid_value eq " + originEventGuid + "";
    var expand = "&$expand=msnfp_CustomerId_contact($select=contactid,fullname),msnfp_CustomerId_account($select=accountid,name)";
    var queryResults = ExecuteQuery(GetWebAPIUrl() + str + expand);

    if (!isNullOrUndefined(queryResults)) {

        for (var i = 0; i < queryResults.length; i++) {
            var newET = {};
            newET["msnfp_EventId@odata.bind"] = "/msnfp_events(" + newEventGuid + ")";
            newET["msnfp_name"] = "COPY: " + queryResults[i].msnfp_name;

            // binding NOT lookup fields
            newET["statecode"] = queryResults[i].statecode;
            newET["statuscode"] = queryResults[i].statuscode;
            newET["exchangerate"] = queryResults[i].exchangerate;
            newET["msnfp_amount"] = queryResults[i].msnfp_amount;
            newET["msnfp_amount_nonreceiptable"] = queryResults[i].msnfp_amount_nonreceiptable;
            newET["msnfp_amount_receipted"] = queryResults[i].msnfp_amount_receipted;
            newET["msnfp_amount_tax"] = queryResults[i].msnfp_amount_tax;
            newET["msnfp_date"] = queryResults[i].msnfp_date;
            newET["msnfp_description"] = queryResults[i].msnfp_description;
            newET["msnfp_groupnotes"] = queryResults[i].msnfp_groupnotes;
            newET["msnfp_identifier"] = queryResults[i].msnfp_identifier;
            newET["msnfp_maxspots"] = queryResults[i].msnfp_maxspots;
            newET["msnfp_registrationsperticket"] = queryResults[i].msnfp_registrationsperticket;
            //newET["msnfp_val_sold"] = queryResults[i].msnfp_val_sold;
            newET["msnfp_tickets"] = queryResults[i].msnfp_tickets;
            newET["msnfp_quantity"] = queryResults[i].msnfp_quantity;
            newET["timezoneruleversionnumber"] = queryResults[i].timezoneruleversionnumber;
            newET["utcconversiontimezonecode"] = queryResults[i].utcconversiontimezonecode;

            // binding lookup fields


            if (!isNullOrUndefined(queryResults[i].msnfp_CustomerId_contact))
                newET["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + giftResult._msnfp_customerid_value + ")";
            else if (!isNullOrUndefined(queryResults[i].msnfp_CustomerId_account))
                newET["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + giftResult._msnfp_customerid_value + ")";

            if (queryResults[i]._ownerid_value != null)
                newET["ownerid@odata.bind"] = "/systemusers(" + queryResults[i]._ownerid_value + ")";

            if (queryResults[i]._transactioncurrencyid_value !== null)
                newET["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + queryResults[i]._transactioncurrencyid_value + ")";

            var qry = GetWebAPIUrl() + "msnfp_eventtickets";

            CreateRecord(qry, newET);
        }
    }
}

function cloneEventDonations(originEventGuid, newEventGuid) {

    var str = "";
    str += "msnfp_eventdonations?";
    str += "$select=msnfp_eventdonationid,msnfp_name,exchangerate,msnfp_amount,msnfp_description,msnfp_identifier,_ownerid_value,statecode,statuscode,timezoneruleversionnumber,";
    str += "_transactioncurrencyid_value,utcconversiontimezonecode&";
    str += "$filter=statecode eq 0 and _msnfp_eventid_value eq " + originEventGuid + "";
    var queryResults = ExecuteQuery(GetWebAPIUrl() + str);

    if (!isNullOrUndefined(queryResults)) {

        for (var i = 0; i < queryResults.length; i++) {
            var newED = {};
            newED["msnfp_EventId@odata.bind"] = "/msnfp_events(" + newEventGuid + ")";
            newED["msnfp_name"] = "COPY: " + queryResults[i].msnfp_name;

            // binding NOT lookup fields
            newED["statecode"] = queryResults[i].statecode;
            newED["statuscode"] = queryResults[i].statuscode;
            newED["exchangerate"] = queryResults[i].exchangerate;
            newED["msnfp_amount"] = queryResults[i].msnfp_amount;
            newED["msnfp_description"] = queryResults[i].msnfp_description;
            newED["msnfp_identifier"] = queryResults[i].msnfp_identifier;
            newED["timezoneruleversionnumber"] = queryResults[i].timezoneruleversionnumber;
            newED["utcconversiontimezonecode"] = queryResults[i].utcconversiontimezonecode;


            // binding lookup fields
            if (queryResults[i]._ownerid_value != null)
                newED["ownerid@odata.bind"] = "/systemusers(" + queryResults[i]._ownerid_value + ")";

            if (queryResults[i]._transactioncurrencyid_value !== null)
                newED["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + queryResults[i]._transactioncurrencyid_value + ")";

            var qry = GetWebAPIUrl() + "msnfp_eventdonations";

            CreateRecord(qry, newED);
        }
    }
}

function cloneEventProducts(originEventGuid, newEventGuid) {

    var str = "";
    str += "msnfp_eventproducts?";
    str += "$select=msnfp_eventproductid,statecode,statuscode,msnfp_amount,msnfp_amount_nonreceiptable,msnfp_amount_receipted,msnfp_amount_tax,msnfp_description,msnfp_identifier,_ownerid_value,";
    str += "msnfp_maxproducts,msnfp_name,msnfp_quantity,msnfp_restrictperregistration,msnfp_sum_sold,msnfp_val_sold,timezoneruleversionnumber,_transactioncurrencyid_value,utcconversiontimezonecode&";
    str += "$filter=statecode eq 0 and _msnfp_eventid_value eq " + originEventGuid + "";
    var queryResults = ExecuteQuery(GetWebAPIUrl() + str);

    if (!isNullOrUndefined(queryResults)) {

        for (var i = 0; i < queryResults.length; i++) {
            var newEP = {};
            newEP["msnfp_EventId@odata.bind"] = "/msnfp_events(" + newEventGuid + ")";
            newEP["msnfp_name"] = "COPY: " + queryResults[i].msnfp_name;

            // binding NOT lookup fields
            newEP["statecode"] = queryResults[i].statecode;
            newEP["statuscode"] = queryResults[i].statuscode;
            newEP["exchangerate"] = queryResults[i].exchangerate;
            newEP["msnfp_amount"] = queryResults[i].msnfp_amount;
            newEP["msnfp_amount_nonreceiptable"] = queryResults[i].msnfp_amount_nonreceiptable;
            newEP["msnfp_amount_receipted"] = queryResults[i].msnfp_amount_receipted;
            newEP["msnfp_amount_tax"] = queryResults[i].msnfp_amount_tax;
            newEP["msnfp_identifier"] = queryResults[i].msnfp_identifier;
            newEP["msnfp_maxproducts"] = queryResults[i].msnfp_maxproducts;
            newEP["msnfp_quantity"] = queryResults[i].msnfp_quantity;
            newEP["msnfp_restrictperregistration"] = queryResults[i].msnfp_restrictperregistration;
            //newEP["msnfp_sum_sold"] = queryResults[i].msnfp_sum_sold;
            //newEP["msnfp_val_sold"] = queryResults[i].msnfp_val_sold;
            newEP["timezoneruleversionnumber"] = queryResults[i].timezoneruleversionnumber;
            newEP["utcconversiontimezonecode"] = queryResults[i].utcconversiontimezonecode;

            // binding lookup fields
            if (queryResults[i]._ownerid_value != null)
                newEP["ownerid@odata.bind"] = "/systemusers(" + queryResults[i]._ownerid_value + ")";

            if (queryResults[i]._transactioncurrencyid_value !== null)
                newEP["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + queryResults[i]._transactioncurrencyid_value + ")";

            var qry = GetWebAPIUrl() + "msnfp_eventproducts";

            CreateRecord(qry, newEP);
        }
    }
}


function cloneEventSponsorship(originEventGuid, newEventGuid) {

    var str = "";
    str += "msnfp_eventsponsorships?";
    str += "$select=msnfp_eventsponsorshipid,statecode,statuscode,exchangerate,msnfp_amount,msnfp_date,msnfp_description,msnfp_fromamount,msnfp_identifier,msnfp_name,";
    str += "msnfp_order,msnfp_quantity,_ownerid_value,timezoneruleversionnumber,_transactioncurrencyid_value,utcconversiontimezonecode&";
    str += "$filter=statecode eq 0 and _msnfp_eventid_value eq " + originEventGuid + "";
    var queryResults = ExecuteQuery(GetWebAPIUrl() + str);

    if (!isNullOrUndefined(queryResults)) {

        for (var i = 0; i < queryResults.length; i++) {
            var newES = {};
            newES["msnfp_EventId@odata.bind"] = "/msnfp_events(" + newEventGuid + ")";
            newES["msnfp_name"] = "COPY: " + queryResults[i].msnfp_name;

            // binding NOT lookup fields
            newES["statecode"] = queryResults[i].statecode;
            newES["statuscode"] = queryResults[i].statuscode;
            newES["exchangerate"] = queryResults[i].exchangerate;
            newES["msnfp_amount"] = queryResults[i].msnfp_amount;
            newES["msnfp_date"] = queryResults[i].msnfp_date;
            newES["msnfp_description"] = queryResults[i].msnfp_description;
            newES["msnfp_fromamount"] = queryResults[i].msnfp_fromamount;
            newES["msnfp_identifier"] = queryResults[i].msnfp_identifier;
            newES["msnfp_order"] = queryResults[i].msnfp_order;
            newES["msnfp_quantity"] = queryResults[i].msnfp_quantity;
            newES["timezoneruleversionnumber"] = queryResults[i].timezoneruleversionnumber;
            newES["utcconversiontimezonecode"] = queryResults[i].utcconversiontimezonecode;

            // binding lookup fields
            if (queryResults[i]._ownerid_value != null)
                newES["ownerid@odata.bind"] = "/systemusers(" + queryResults[i]._ownerid_value + ")";

            if (queryResults[i]._transactioncurrencyid_value !== null)
                newES["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + queryResults[i]._transactioncurrencyid_value + ")";

            var qry = GetWebAPIUrl() + "msnfp_eventsponsorships";

            CreateRecord(qry, newES);
        }
    }
}

function cloneEventPreferences(originEventGuid, newEventGuid) {

    var str = "";
    str += "msnfp_eventpreferences?";
    str += "$select=msnfp_eventpreferenceid,statecode,statuscode,msnfp_freetext,_msnfp_preferencecategoryid_value,_msnfp_preferenceid_value,";
    str += "_ownerid_value,timezoneruleversionnumber,utcconversiontimezonecode&";
    str += "$filter=statecode eq 0 and _msnfp_eventid_value eq " + originEventGuid + "";
    var queryResults = ExecuteQuery(GetWebAPIUrl() + str);

    if (!isNullOrUndefined(queryResults)) {

        for (var i = 0; i < queryResults.length; i++) {
            var newEP = {};
            newEP["msnfp_EventId@odata.bind"] = "/msnfp_events(" + newEventGuid + ")";

            // binding NOT lookup fields
            newEP["statecode"] = queryResults[i].statecode;
            newEP["statuscode"] = queryResults[i].statuscode;

            if (queryResults[i].msnfp_freetext != null)
                newEP["msnfp_freetext"] = queryResults[i].msnfp_freetext;

            if (queryResults[i].timezoneruleversionnumber != null)
                newEP["timezoneruleversionnumber"] = queryResults[i].timezoneruleversionnumber;

            if (queryResults[i].utcconversiontimezonecode != null)
                newEP["utcconversiontimezonecode"] = queryResults[i].utcconversiontimezonecode;

            // binding lookup fields
            if (queryResults[i]._msnfp_preferencecategoryid_value != null)
                newEP["msnfp_PreferenceCategoryId@odata.bind"] = "/msnfp_preferencecategories(" + queryResults[i]._msnfp_preferencecategoryid_value + ")";

            if (queryResults[i]._msnfp_preferenceid_value != null)
                newEP["msnfp_PreferenceId@odata.bind"] = "/msnfp_preferences(" + queryResults[i]._msnfp_preferenceid_value + ")";

            if (queryResults[i]._ownerid_value != null)
                newEP["ownerid@odata.bind"] = "/systemusers(" + queryResults[i]._ownerid_value + ")";


            var qry = GetWebAPIUrl() + "msnfp_eventpreferences";

            CreateRecord(qry, newEP);
        }
    }
}


function CloneEventListRecords(selectedItems) {
    if (selectedItems.length > 0) {
        for (var i = 0; i < selectedItems.length; i++) {
            var cloneData = {};
            var originRecordType = selectedItems[i].TypeName;
            var originRecordGuid = selectedItems[i].Id.replace('{', '').replace('}', '').toUpperCase();


            if (originRecordGuid != null) {

                var copyEventID = CreateEvent(originRecordType, originRecordGuid);

                if (!isNullOrUndefined(copyEventID)) {

                    // Completed all actions, show this to the user so they do not click again:
                    var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
                    var alertOptions = { height: 120, width: 260 };
                    Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                        function success(result) {
                            console.log("Alert dialog closed");
                        },
                        function (error) {
                            console.log(error.message);
                        }
                    );
                }
            }
        }
        window.location.reload(true);
    }
}

function openEventPackageFromEvent() {
    var options = { openInNewWindow: false };
    var parameters = {};
    parameters["param_msnfp_eventid"] = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    parameters["param_msnfp_eventidname"] = parent.Xrm.Page.data.entity.attributes.get("msnfp_name").getValue();;
    parameters["param_msnfp_eventidtype"] = "msnfp_event";
    Xrm.Utility.openEntityForm("msnfp_eventpackage", null, parameters, options);
}

function HideNewEventPackageButtonOnEvent() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");

    var isVisible = false;

    if (status.getValue() == 1)//Active
    {
        return isVisible = true;
    }
    return isVisible;
}

function openUpdateHouseholdAddressPopup() {
    let name = parent.Xrm.Page.data.entity.getEntityName();
    var householdid;
    if (name === 'contact') {
        var household = parent.Xrm.Page.getAttribute("msnfp_householdid").getValue();
        if (!household) return parent.Xrm.Navigation.openAlertDialog("Household info not found for current contact.");
        householdid = household[0].id;
    }
    else if (name === 'account') {
        householdid = parent.Xrm.Page.data.entity.getId();
    }
    var windowOptions = { height: 650, width: 1250 };
    var data = { from: name, refreshbackgroundpage: true, fromid: parent.Xrm.Page.data.entity.getId().replace(/[{}]/g, ""), householdid: householdid.replace(/[{}]/g, "") };
    parent.Xrm.Navigation.openWebResource("msnfp_/webpages/updatehouseholdaddress.html", windowOptions, JSON.stringify(data));
}

function HideUpdateHouseholdAddressButtonOnAccount() {
    var status = parent.Xrm.Page.data.entity.attributes.get("msnfp_accounttype");
    var isVisible = false;

    if (status) {
        if (status.getValue() == 844060000)//Household
        {
            return isVisible = true;
        }
    }
    return isVisible;
}

function HideUpdateHouseholdAddressButtonOnContact() {
    var householdId = parent.Xrm.Page.data.entity.attributes.get("msnfp_householdid");
    var isVisible = false;

    if (householdId) {
        if (!isNullOrUndefined(householdId.getValue()))//Household
        {
            return isVisible = true;
        }
    }
    return isVisible;
}

//On Campaign entity Ribbon
function CloneCampaignRecord() {
    var originRecordType = parent.Xrm.Page.data.entity.getEntityName();
    var originRecordGuid = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    if (originRecordGuid != null)
        var copyCampaignID = CreateCampaign(originRecordType, originRecordGuid);
}

function CloneCampaignListRecords(selectedItems) {
    if (selectedItems.length > 0) {
        for (var i = 0; i < selectedItems.length; i++) {
            var cloneData = {};
            var originRecordType = selectedItems[i].TypeName;
            var originRecordGuid = selectedItems[i].Id.replace('{', '').replace('}', '').toUpperCase();;

            CreateCampaign(originRecordType, originRecordGuid);
        }
        window.location.reload(true);
    }
}

function CreateCampaign(originRecordType, originRecordGuid) {
    var s = "EntityDefinitions?$select=EntitySetName,LogicalName&$filter=LogicalName eq '" + originRecordType + "'";
    var pluralName = (ExecuteQuery(GetWebAPIUrl() + s))[0].EntitySetName;

    var cQuery = pluralName + "?";
    cQuery += "$select=campaignid,name&";
    cQuery += "$filter=campaignid eq " + originRecordGuid;

    var campaign = ExecuteQuery(GetWebAPIUrl() + cQuery);

    if (campaign != null) {
        var CloneCampaign = {};

        CloneCampaign["name"] = "COPY: " + campaign[0].name;
        CloneCampaign["msnfp_originalcampaigneventid@odata.bind"] = "/" + pluralName + "(" + originRecordGuid + ")";

        var qry = GetWebAPIUrl() + pluralName;

        CreateRecord(qry, CloneCampaign);
    }
}

function GetWebAPIUrl() {
    return parent.Xrm.Page.context.getClientUrl() + "/api/data/v8.2/";
}

function ExecuteQuery(oDataQuery) {
    var results;
    $.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataQuery,
        async: false,
        beforeSend: function (xhr) {
            xhr.setRequestHeader("Accept", "application/json");
            xhr.setRequestHeader("Prefer", "odata.include-annotations=*");
            xhr.setRequestHeader("OData-MaxVersion", "4.0");
            xhr.setRequestHeader("OData-Version", "4.0");
        },
        success: function (data, textStatus, xhr) {
            if (data.value != null && data.value.length) {
                results = data.value;
            }
        },
        error: function (xhr, textStatus, errorThrown) {
            if (xhr && xhr.responseText) {
                console.error('OData Query Failed: \r\n' + oDataQuery + '\r\n' + xhr.responseText);
            }
            throw new Error(errorThrown);
        }
    });
    return results;
}

function ExecuteQuery2(oDataQuery) {
    var results;
    $.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataQuery,
        async: false,
        beforeSend: function (xhr) {
            xhr.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, xhr) {
            if (data.value != null && data.value.length) {
                results = data.value;
            }
        },
        error: function (xhr, textStatus, errorThrown) {
            alert(xhr.responseJSON["error"].message);
        }
    });
    return results;
}

function CreateRecord(oDataQuery, entity) {
    var jsonEntityDetail = JSON.stringify(entity);

    var recordID;
    $.ajax({
        type: "POST",
        async: false,
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataQuery,
        data: jsonEntityDetail,
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
            XMLHttpRequest.setRequestHeader("Content-Type", "application/json; charset=utf-8");
            XMLHttpRequest.setRequestHeader("Prefer", "odata.include-annotations=*");
            XMLHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
            XMLHttpRequest.setRequestHeader("OData-Version", "4.0");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
            recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
        },
        error: function (XMLHttpRequest, textStatus, errorThrown) {
            alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
        }
    });

    return recordID;
}

function UpdateRecord(oDataQuery, entity) {
    var jsonEntityDetail = JSON.stringify(entity);

    var recordID;
    $.ajax({
        type: "PATCH",
        async: false,
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataQuery,
        data: jsonEntityDetail,
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
            XMLHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
            XMLHttpRequest.setRequestHeader("OData-Version", "4.0");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
            recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
        },
        error: function (XMLHttpRequest, textStatus, errorThrown) {
            console.debug(XMLHttpRequest);
            console.debug(textStatus);
            console.debug(errorThrown);
            alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
        }
    });

    return recordID;
}

function CloseCampaign() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    status.setValue(844060001);//Completed
    parent.Xrm.Page.data.entity.save();
}

function HideCloseButton() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");

    var isVisible = false;

    if (status.getValue() == 844060000)//Active
    {
        return isVisible = true;
    }
    return isVisible;
}

function ApproveCampaign() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    status.setValue(844060000);//Active
    parent.Xrm.Page.data.entity.save();
}

function HideApproveButton() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var isVisible = false;
    if (status.getValue() == 0)//Draft
    {
        return isVisible = true;
    }
    return isVisible;
}

function PostToWebSiteCampaign() {
    var postToWeb = parent.Xrm.Page.data.entity.attributes.get("msnfp_posttowebsite");
    postToWeb.setValue(true);
    parent.Xrm.Page.data.entity.save();
}

function HidePostToWebSiteButton() {
    var postToWeb = parent.Xrm.Page.data.entity.attributes.get("msnfp_posttowebsite").getValue();
    var isVisible = false;
    if (!postToWeb) {
        return isVisible = true;
    }
    return isVisible;
}

function RemoveFromWebSiteCampaign() {
    var RemoveFromWeb = parent.Xrm.Page.data.entity.attributes.get("msnfp_posttowebsite");
    RemoveFromWeb.setValue(false);
    parent.Xrm.Page.data.entity.save();
}

function HideRemoveFromWebSiteButton() {
    var RemoveFromWeb = parent.Xrm.Page.data.entity.attributes.get("msnfp_posttowebsite").getValue();
    var isVisible = false;
    if (RemoveFromWeb) {
        return isVisible = true;
    }
    return isVisible;
}

function CancelCampaign() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    status.setValue(844060002);//Cancelled
    parent.Xrm.Page.data.entity.save();
}

function HideCancelButton() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var isVisible = false;
    if (status.getValue() == 0 || status.getValue() == 844060000)//Draft or Active
    {
        return isVisible = true;
    }
    return isVisible;
}

function PopNewBroadCast() {
    var options = { openInNewWindow: true };
    Xrm.Utility.openEntityForm("msnfp_eventbroadcast", null, null, options);
}

function ShowVolunteerSummary() {
    var campaignGuid = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    var rdlName = "VolunteerSummary.rdl";
    var reportGuid = parent.Xrm.Page.data.entity.attributes.get("msnfp_volunteersummaryreportid").getValue();
    var url = parent.Xrm.Page.context.getClientUrl()
        + "/crmreports/viewer/viewer.aspx?action=filter&helpID="
        + rdlName + "&id={" + reportGuid + "}&p:campaignID=" + campaignGuid;

    window.open(url, null, 600, 400, true, false, null);
}

function ShowEventPromotionSheet() {
    var campaignGuid = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    var rdlName = "EventPromotionSheet.rdl";
    var reportGuid = parent.Xrm.Page.data.entity.attributes.get("msnfp_eventpromotionsheetreportid").getValue();
    var url = parent.Xrm.Page.context.getClientUrl()
        + "/crmreports/viewer/viewer.aspx?action=filter&helpID="
        + rdlName + "&id={" + reportGuid + "}&p:campaignID=" + campaignGuid;

    window.open(url, null, 600, 400, true, false, null);
}

function showNewEventPackage() {
    var options = { openInNewWindow: false };
    var parameters = {};
    parameters["param_msnfp_campaigneventid"] = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
    parameters["param_msnfp_campaigneventidname"] = parent.Xrm.Page.data.entity.attributes.get("name").getValue();;
    parameters["param_msnfp_campaigneventidtype"] = "campaign";
    Xrm.Utility.openEntityForm("msnfp_eventpackage", null, parameters, options);
}

function HideNewEventPackageButton() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");

    var isVisible = false;

    if (status.getValue() == 844060000)//Active
    {
        return isVisible = true;
    }
    return isVisible;
}

//On Campaign/Event Package entity ribbon
//NOTE: Campaign does not have an anonymous or anonymity field.
//NOTE 2: This function is not actually in a Command on the Ribbon
// Changes of "msnfp_anonymous" to "msnfp_anonymity" should be tested if/when this gets added to the ribbon
function ClonePackageRecord() {
    var cloneData = {};
    var originRecordType = parent.Xrm.Page.data.entity.getEntityName();
    var originRecordGuid = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    var customer = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue();
    var customerType = "";
    if (customer != null) {
        customerType = customer[0].typename;
    }

    var s = "EntityDefinitions?$select=EntitySetName,LogicalName&$filter=LogicalName eq '" + originRecordType + "'";
    var pluralName = (ExecuteQuery(GetWebAPIUrl() + s))[0].EntitySetName;

    var select = "msnfp_eventpackageid,msnfp_name,msnfp_amount,msnfp_sum_amountpaid,msnfp_anonymity,msnfp_sum_balance,_msnfp_bankaccountid_value,";
    select += "msnfp_billing_city,msnfp_billing_country,msnfp_billing_line1,msnfp_billing_line2,msnfp_billing_line3,msnfp_billing_postalcode,msnfp_billing_stateorprovince,";
    select += "_msnfp_eventid_value,msnfp_chequenumber,_msnfp_constituentid_value,_msnfp_creditcardid_value,_msnfp_customerid_value,msnfp_date,msnfp_daterefunded,";
    select += "msnfp_discountamount,msnfp_emailaddress1,msnfp_firstname,msnfp_giftsource,msnfp_gifttype,msnfp_identifier,msnfp_lastname,";
    select += "msnfp_organizationname,msnfp_sponsorships,msnfp_sum_donations,msnfp_sum_products,msnfp_sum_sponsorships,msnfp_sum_ticketregistrations,";
    select += "msnfp_telephone1,msnfp_val_refunded,msnfp_transactionfraudcode,msnfp_val_payments,msnfp_initialpaid,msnfp_transactionidentifier,msnfp_transactionresult,msnfp_transactionstatus,";
    select += "msnfp_val_donations,msnfp_val_products,msnfp_val_ticketregistrations";

    var oDataUri = GetWebAPIUrl() + pluralName +
        "?$select=" + select + "&$filter=msnfp_eventpackageid eq " + originRecordGuid + "";

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data != null && data != undefined) {
                cloneData["msnfp_name"] = "COPY:" + data.value[0].msnfp_name;
                cloneData["msnfp_amount"] = data.value[0].msnfp_amount;
                cloneData["msnfp_sum_amountpaid"] = data.value[0].msnfp_sum_amountpaid;
                cloneData["msnfp_anonymity"] = data.value[0].msnfp_anonymity;
                cloneData["msnfp_sum_balance"] = data.value[0].msnfp_sum_balance;
                cloneData["msnfp_billing_city"] = data.value[0].msnfp_billing_city;
                cloneData["msnfp_billing_country"] = data.value[0].msnfp_billing_country;
                cloneData["msnfp_billing_line1"] = data.value[0].msnfp_billing_line1;
                cloneData["msnfp_billing_line2"] = data.value[0].msnfp_billing_line2;
                cloneData["msnfp_billing_line3"] = data.value[0].msnfp_billing_line3;
                cloneData["msnfp_billing_postalcode"] = data.value[0].msnfp_billing_postalcode;
                cloneData["msnfp_billing_stateorprovince"] = data.value[0].msnfp_billing_stateorprovince;
                cloneData["msnfp_chequenumber"] = data.value[0].msnfp_chequenumber;
                cloneData["msnfp_date"] = data.value[0].msnfp_date;
                cloneData["msnfp_daterefunded"] = data.value[0].msnfp_daterefunded;
                cloneData["msnfp_discountamount"] = data.value[0].msnfp_discountamount;
                cloneData["msnfp_emailaddress1"] = data.value[0].msnfp_emailaddress1;
                cloneData["msnfp_firstname"] = data.value[0].msnfp_firstname;
                cloneData["msnfp_giftsource"] = data.value[0].msnfp_giftsource;
                cloneData["msnfp_gifttype"] = data.value[0].msnfp_gifttype;
                cloneData["msnfp_identifier"] = data.value[0].msnfp_identifier;
                cloneData["msnfp_lastname"] = data.value[0].msnfp_lastname;
                cloneData["msnfp_organizationname"] = data.value[0].msnfp_organizationname;
                cloneData["msnfp_sponsorships"] = data.value[0].msnfp_sponsorships;
                cloneData["msnfp_sum_donations"] = data.value[0].msnfp_sum_donations;
                cloneData["msnfp_sum_products"] = data.value[0].msnfp_sum_products;
                //cloneData["msnfp_sum_registrations"] = data.value[0].msnfp_sum_registrations;
                cloneData["msnfp_sum_sponsorships"] = data.value[0].msnfp_sum_sponsorships;
                cloneData["msnfp_sum_ticketregistrations"] = data.value[0].msnfp_sum_ticketregistrations;
                cloneData["msnfp_telephone1"] = data.value[0].msnfp_telephone1;
                cloneData["msnfp_val_refunded"] = data.value[0].msnfp_val_refunded;
                cloneData["msnfp_val_payments"] = data.value[0].msnfp_val_payments;
                cloneData["msnfp_initialpaid"] = data.value[0].msnfp_initialpaid;
                cloneData["msnfp_transactionfraudcode"] = data.value[0].msnfp_transactionfraudcode;
                cloneData["msnfp_transactionidentifier"] = data.value[0].msnfp_transactionidentifier;
                cloneData["msnfp_transactionresult"] = data.value[0].msnfp_transactionresult;
                cloneData["msnfp_transactionstatus"] = data.value[0].msnfp_transactionstatus;
                cloneData["msnfp_val_donations"] = data.value[0].msnfp_val_donations;
                cloneData["msnfp_val_products"] = data.value[0].msnfp_val_products;
                cloneData["msnfp_val_ticketregistrations"] = data.value[0].msnfp_val_ticketregistrations;
                cloneData["statuscode"] = 1;

                if (data.value[0]._msnfp_bankaccountid_value != null)
                    cloneData["msnfp_bankaccountid@odata.bind"] = "/msnfp_bankaccounts(" + data.value[0]._msnfp_bankaccountid_value + ")";
                if (data.value[0]._msnfp_eventid_value != null)
                    cloneData["msnfp_eventid@odata.bind"] = "/msnfp_events(" + data.value[0]._msnfp_eventid_value + ")";
                if (data.value[0]._msnfp_constituentid_value != null)
                    cloneData["msnfp_constituentid@odata.bind"] = "/contacts(" + data.value[0]._msnfp_constituentid_value + ")";
                if (data.value[0]._msnfp_creditcardid_value != null)
                    cloneData["msnfp_creditcardid@odata.bind"] = "/msnfp_creditcards(" + data.value[0]._msnfp_creditcardid_value + ")";
                if (customerType != "" && customerType == "account" && data.value[0]._msnfp_customerid_value != null)
                    cloneData["msnfp_customerid_account@odata.bind"] = "/accounts(" + data.value[0]._msnfp_customerid_value + ")";
                else if (customerType != "" && customerType == "contact" && data.value[0]._msnfp_customerid_value != null)
                    cloneData["msnfp_customerid_contact@odata.bind"] = "/contacts(" + data.value[0]._msnfp_customerid_value + ")";

                //Create new Record
                var oDataUri = GetWebAPIUrl() + pluralName;

                var clonePackageID = CreateRecord(oDataUri, cloneData);

                OpenRecords(clonePackageID, originRecordType);

            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + originRecordType.text + ": " +
                XmlHttpRequest.responseText);
        }
    });
}

//On Event Broadcast Template entity ribbon
function ApproveBroadcastTemplate() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    status.setValue(844060000);//Active
    parent.Xrm.Page.data.entity.save();
}

function HideApproveBroadcastButton() {
    var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    var isVisible = false;
    if (status.getValue() == 1)//Draft
    {
        return isVisible = true;
    }
    return isVisible;
}

//On Gift Batch entity ribbon
function closeBatch() {
    Xrm.Utility.confirmDialog("This will close the batch and no other gifts can then be created, would you like to continue?", ContinueCloseBatchGift, cancelUpdate);
}

function ContinueCloseBatchGift() {
    var batchID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();
    var batch = {};
    batch["statecode"] = 1;
    batch["statuscode"] = 2;
    var qry = GetWebAPIUrl() + "msnfp_giftbatchs(" + batchID + ")";
    UpdateRecord(qry, batch);

    Xrm.Utility.openEntityForm("msnfp_giftbatch", batchID, null, true);
}

//On Donation Page entity ribbon
function PostToWebSiteDonationPage() {
    var visible = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible");
    visible.setValue(true);
    parent.Xrm.Page.data.entity.attributes.get("msnfp_madevisible").setValue(new Date());

    parent.Xrm.Page.data.entity.save();
}

function HidePostToWebSiteButtonDonationPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && !visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function RemoveFromWebSiteDonationPage() {
    var visible = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible");
    visible.setValue(false);
    var removed = parent.Xrm.Page.data.entity.attributes.get("msnfp_removed");
    removed.setValue(true);
    parent.Xrm.Page.data.entity.attributes.get("msnfp_removedon").setValue(new Date());

    parent.Xrm.Page.data.entity.save();
}

function HideRemoveFromWebSiteButtonDonationPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function SynchronizeNowDonationPage() {
    parent.Xrm.Page.data.entity.attributes.get("msnfp_lastpublished").setValue(new Date());

    parent.Xrm.Page.data.entity.save();
}

function HideSynchronizeNowButtonDonationPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function ViewPageDonationPage() {
    var externalURL = parent.Xrm.Page.data.entity.attributes.get("msnfp_externalurl").getValue();
    // DEPRECATED:
    //var height = parent.Xrm.Page.data.entity.attributes.get("msnfp_maxheight").getValue();
    //var width = parent.Xrm.Page.data.entity.attributes.get("msnfp_maxwidth").getValue();

    if (!isNullOrUndefined(externalURL)) {
        //if (!isNullOrUndefined(height) && !isNullOrUndefined(width)) {
        //    window.open(externalURL, "", "width=" + width + ",height=" + height + "", '_blank');
        //}
        //else {
        window.open(externalURL, "width=400,height=600", '_blank');
        //}
    }
}

function HideViewPageButtonDonationPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

var AddToListDonationPage = function (formContext) {

    var currentEntityGUID = formContext.data.entity.getId().replace('{', '').replace('}', '');

    var params = "recordId=" + currentEntityGUID;

    //var windowOptions = { height: 600, width: 600, directories: 0, titlebar: 0, toolbar: 0, location: 0, status: 0, menubar: 0, scrollbars: 0, resizable: 0 };
    //Xrm.Navigation.openWebResource("msnfp_PageOrderDonationList.html", windowOptions, params);

    var pageInput = {
        pageType: "webresource",
        webresourceName: "msnfp_donationlist.html",
        data: params
    };
    var navigationOptions = {
        target: 2, // 2 is for opening the page as a dialog. 
        width: 800, // default is px. can be specified in % as well. 
        height: 700, // default is px. can be specified in % as well. 
        position: 1 // Specify 1 to open the dialog in center; 2 to open the dialog on the side. Default is 1 (center). 
    };
    Xrm.Navigation.navigateTo(pageInput, navigationOptions).then(
        function success() {

        },
        function error(e) {
            // Handle errors 
        }
    );
};


//** ---------------------------Donation List Related Script -----------------------------------------------------**//

/* Show Hide Methods can be deleted as these functionalities are handled in ribbon customization level */

var PostToWebSiteDonationList = function (formContext) {

    var visible = formContext.getAttribute("msnfp_visible");
    visible.setValue(true);
    formContext.getAttribute("msnfp_madevisible").setValue(new Date());

    var removed = formContext.getAttribute("msnfp_removed");
    removed.setValue(false);
    formContext.getAttribute("msnfp_removedon").setValue(null);

    formContext.data.entity.save();
};

var RemoveFromWebSiteDonationList = function (formContext) {

    var visible = formContext.getAttribute("msnfp_visible");
    visible.setValue(false);
    formContext.getAttribute("msnfp_madevisible").setValue(null);

    var removed = formContext.getAttribute("msnfp_removed");
    removed.setValue(true);
    formContext.getAttribute("msnfp_removedon").setValue(new Date());

    formContext.data.entity.save();
};

var GenerateURLDonatioList = function (formContext) {

    var giftAzureURL = "";

    var userSettings = Xrm.Utility.getGlobalContext().userSettings;
    var currentuserID = userSettings.userId.replace('{', '').replace('}', '').toUpperCase();
    var currentRecordId = formContext.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    Xrm.WebApi.retrieveRecord("systemuser", currentuserID, "?$select=fullname&$expand=msnfp_ConfigurationId($select=msnfp_azure_webapp)")
        .then(function success(result) {
            giftAzureURL = result.msnfp_ConfigurationId.msnfp_azure_webapp;
            formContext.getAttribute("msnfp_externalurl").setValue(giftAzureURL + "DonationList/GetDonationPageList/" + currentRecordId);
            formContext.data.entity.save();
        },
            function error(e) {
                console.log(e.message);
            }
        );
};

var SynchronizeNowDonationListPage = function (formContext) {
    formContext.getAttribute("msnfp_lastpublished").setValue(new Date());

    formContext.data.entity.save();
};

var ViewPageDonationListPage = function (formContext) {
    var url = formContext.getAttribute("msnfp_externalurl").getValue();
    if (url !== null) {
        window.open(url);
    }
};

var donationPageContext;
var OrderListDonationListPage = function (formContext) {
    donationPageContext = formContext;
    var userSettings = Xrm.Utility.getGlobalContext().userSettings;
    var currentuserID = userSettings.userId.replace('{', '').replace('}', '').toUpperCase();
    var currentEntityGUID = formContext.data.entity.getId().replace('{', '').replace('}', '');

    var params = "recordId=" + currentEntityGUID + "&userId=" + currentuserID;

    //var windowOptions = { height: 600, width: 600, directories: 0, titlebar: 0, toolbar: 0, location: 0, status: 0, menubar: 0, scrollbars: 0, resizable: 0 };
    //Xrm.Navigation.openWebResource("msnfp_PageOrderDonationList.html", windowOptions, params);

    var pageInput = {
        pageType: "webresource",
        webresourceName: "msnfp_PageOrderList.Html",
        data: params
    };
    var navigationOptions = {
        target: 2, // 2 is for opening the page as a dialog. 
        width: 800, // default is px. can be specified in % as well. 
        height: 700, // default is px. can be specified in % as well. 
        position: 1 // Specify 1 to open the dialog in center; 2 to open the dialog on the side. Default is 1 (center). 
    };
    Xrm.Navigation.navigateTo(pageInput, navigationOptions).then(
        function success() {

            let webResourceControl = donationPageContext.getControl("WebResource_PageOrdersDonationList").getObject();
            if (webResourceControl !== null) {
                let src = webResourceControl.src;
                webResourceControl.src = "about:blank";
                webResourceControl.src = src;
            }
        },
        function error(e) {
            // Handle errors 
        }
    );

};


var CloneDonationListPage = function (formContext) {

    Xrm.Utility.showProgressIndicator("Processing");

    var originRecordGuid = formContext.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    Xrm.WebApi.retrieveRecord("msnfp_donationlist", originRecordGuid, "?$select=msnfp_maxheight,msnfp_maxwidth,msnfp_message,msnfp_smallimage,_msnfp_parentlistid_value,_msnfp_configurationid_value,msnfp_title")
        .then(function success(result) {

            var data =
            {
                "msnfp_maxheight": result.msnfp_maxheight,
                "msnfp_maxwidth": result.msnfp_maxwidth,
                "msnfp_message": result.msnfp_message,
                "msnfp_smallimage": result.msnfp_smallimage,
                "msnfp_cloned": true,
                "msnfp_title": result.msnfp_title + " - Clone"

            };
            if (result._msnfp_parentlistid_value !== null)
                data["msnfp_ParentListId@odata.bind"] = "/msnfp_donationlists(" + result._msnfp_parentlistid_value + ")";
            if (result._msnfp_configurationid_value !== null)
                data["msnfp_ConfigurationId@odata.bind"] = "/msnfp_configurations(" + result._msnfp_configurationid_value + ")";
            // create record
            Xrm.WebApi.createRecord("msnfp_donationlist", data).then(
                function success(result) {
                    Xrm.Utility.closeProgressIndicator();

                    var newRecordId = result.id;

                    Xrm.WebApi.retrieveMultipleRecords("msnfp_pageorder", "?$select=msnfp_order,_msnfp_fromdonationlistid_value,_msnfp_fromdonationpageid_value,_msnfp_todonationlistid_value,msnfp_orderdate&$filter=_msnfp_todonationlistid_value eq " + originRecordGuid).then(
                        function success(results) {

                            if (results.entities.length > 0) {
                                CreatePageOrder(newRecordId, results.entities);
                            }
                            else {
                                Xrm.Utility.closeProgressIndicator();
                                var entityFormOptions = {};
                                entityFormOptions["entityName"] = "msnfp_donationlist";
                                entityFormOptions["entityId"] = newRecordId;

                                // Open the form.
                                Xrm.Navigation.openForm(entityFormOptions).then(
                                    function (success) {
                                        Xrm.Utility.closeProgressIndicator();
                                        // Completed all actions, show this to the user so they do not click again:
                                        var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
                                        var alertOptions = { height: 120, width: 260 };
                                        Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                                            function success(result) {
                                                console.log("Alert dialog closed");
                                            },
                                            function (error) {
                                                console.log(error.message);
                                            }
                                        );
                                    },
                                    function (error) {
                                        console.log(error);
                                        Xrm.Utility.closeProgressIndicator();
                                    });
                            }
                        },
                        function (error) {
                            console.log(error.message);
                            // handle error conditions
                        }
                    );

                },
                function (error) {
                    console.log(error);
                    Xrm.Utility.closeProgressIndicator();
                }
            );

        },
            function error(e) {
                console.log(e);
                Xrm.Utility.closeProgressIndicator();
            }
        );
};



var CreatePageOrder = function (newRecordId, result) {

    for (var count = 0; count < result.length; count++) {
        var data =
        {
            "msnfp_order": result[count].msnfp_order,
            "msnfp_orderdate": result[count].msnfp_orderdate
        };

        if (result[count]._msnfp_fromdonationlistid_value !== null)
            data["msnfp_FromDonationListId@odata.bind"] = "/msnfp_donationlists(" + result[count]._msnfp_fromdonationlistid_value + ")";
        if (result[count]._msnfp_fromdonationpageid_value !== null)
            data["msnfp_FromDonationPageId@odata.bind"] = "/msnfp_donationpages(" + result[count]._msnfp_fromdonationpageid_value + ")";
        if (result[count]._msnfp_todonationlistid_value !== null)
            data["msnfp_ToDonationListId@odata.bind"] = "/msnfp_donationlists(" + newRecordId + ")";

        Xrm.WebApi.createRecord("msnfp_pageorder", data).then(
            function success(result) {
                console.log("Page order created");
            },
            function (error) {
                console.log(error);
            }
        );
    }

    Xrm.Utility.closeProgressIndicator();
    var entityFormOptions = {};
    entityFormOptions["entityName"] = "msnfp_donationlist";
    entityFormOptions["entityId"] = newRecordId;

    // Open the form.
    Xrm.Navigation.openForm(entityFormOptions).then(
        function (success) {
            Xrm.Utility.closeProgressIndicator();
            // Completed all actions, show this to the user so they do not click again:
            var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
            var alertOptions = { height: 120, width: 260 };
            Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                function success(result) {
                    console.log("Alert dialog closed");
                },
                function (error) {
                    console.log(error.message);
                }
            );
        },
        function (error) {
            console.log(error);
            Xrm.Utility.closeProgressIndicator();
        });

};

/** -----------------------------End: Donation List Related Script ---------------------------------------------------------------------------**/


//** ---------------------------Start: Event List Related Script -----------------------------------------------------**//

/* Show Hide Methods can be deleted as these functionalities are handled in ribbon customization level */

var PostToWebSiteEventList = function (formContext) {

    var visible = formContext.getAttribute("msnfp_visible");
    visible.setValue(true);
    formContext.getAttribute("msnfp_madevisible").setValue(new Date());

    var removed = formContext.getAttribute("msnfp_removed");
    removed.setValue(false);
    formContext.getAttribute("msnfp_removedon").setValue(null);

    formContext.data.entity.save();
};

var RemoveFromWebSiteEventList = function (formContext) {

    var visible = formContext.getAttribute("msnfp_visible");
    visible.setValue(false);
    formContext.getAttribute("msnfp_madevisible").setValue(null);

    var removed = formContext.getAttribute("msnfp_removed");
    removed.setValue(true);
    formContext.getAttribute("msnfp_removedon").setValue(new Date());

    formContext.data.entity.save();
};

var GenerateURLEventList = function (formContext) {

    var giftAzureURL = "";

    var userSettings = Xrm.Utility.getGlobalContext().userSettings;
    var currentuserID = userSettings.userId.replace('{', '').replace('}', '').toUpperCase();
    var currentRecordId = formContext.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    Xrm.WebApi.retrieveRecord("systemuser", currentuserID, "?$select=fullname&$expand=msnfp_ConfigurationId($select=msnfp_azure_webapp)")
        .then(function success(result) {
            giftAzureURL = result.msnfp_ConfigurationId.msnfp_azure_webapp;
            formContext.getAttribute("msnfp_externalurl").setValue(giftAzureURL + "EventList/GetEventList/" + currentRecordId);
            formContext.data.entity.save();
        },
            function error(e) {
                console.log(e.message);
            }
        );
};

var SynchronizeNowEventList = function (formContext) {
    formContext.getAttribute("msnfp_lastpublished").setValue(new Date());

    formContext.data.entity.save();
};

var ViewPageEventList = function (formContext) {
    var url = formContext.getAttribute("msnfp_externalurl").getValue();
    if (url !== null) {
        window.open(url);
    }
};

var eventContext;
var OrderListEventList = function (formContext) {
    eventContext = formContext;
    var userSettings = Xrm.Utility.getGlobalContext().userSettings;
    var currentuserID = userSettings.userId.replace('{', '').replace('}', '').toUpperCase();
    var currentEntityGUID = formContext.data.entity.getId().replace('{', '').replace('}', '');

    var params = "recordId=" + currentEntityGUID + "&userId=" + currentuserID;

    var pageInput = {
        pageType: "webresource",
        webresourceName: "msnfp_EventOrderList.Html",
        data: params
    };
    var navigationOptions = {
        target: 2, // 2 is for opening the page as a dialog. 
        width: 800, // default is px. can be specified in % as well. 
        height: 700, // default is px. can be specified in % as well. 
        position: 1 // Specify 1 to open the dialog in center; 2 to open the dialog on the side. Default is 1 (center). 
    };
    Xrm.Navigation.navigateTo(pageInput, navigationOptions).then(
        function success() {
            let webResourceControl = eventContext.getControl("WebResource_PageOrderEventList").getObject();
            if (webResourceControl !== null) {
                let src = webResourceControl.src;
                webResourceControl.src = "about:blank";
                webResourceControl.src = src;
            }
        },
        function error(e) {
            // Handle errors 
        }
    );

};


var CloneEventList = function (formContext) {

    Xrm.Utility.showProgressIndicator("Processing");

    var originRecordGuid = formContext.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    Xrm.WebApi.retrieveRecord("msnfp_eventlist", originRecordGuid, "?$select=msnfp_maxheight,msnfp_maxwidth,msnfp_message,msnfp_smallimage,_msnfp_parentlistid_value,_msnfp_termsofreferenceid_value,msnfp_identifier,_msnfp_configurationid_value")
        .then(function success(result) {

            var data =
            {
                "msnfp_maxheight": result.msnfp_maxheight,
                "msnfp_maxwidth": result.msnfp_maxwidth,
                "msnfp_message": result.msnfp_message,
                "msnfp_smallimage": result.msnfp_smallimage,
                "msnfp_cloned": true,
                "msnfp_identifier": result.msnfp_identifier + " - Clone"
            };
            if (result._msnfp_parentlistid_value !== null)
                data["msnfp_ParentListId@odata.bind"] = "/msnfp_eventlists(" + result._msnfp_parentlistid_value + ")";
            if (result._msnfp_termsofreferenceid_value !== null)
                data["msnfp_TermsofReferenceId@odata.bind"] = "/msnfp_termsofreferences(" + result._msnfp_termsofreferenceid_value + ")";
            if (result._msnfp_configurationid_value !== null)
                data["msnfp_ConfigurationId@odata.bind"] = "/msnfp_configurations(" + result._msnfp_configurationid_value + ")";
            // create record
            Xrm.WebApi.createRecord("msnfp_eventlist", data).then(
                function success(result) {
                    var newRecordId = result.id;

                    Xrm.WebApi.retrieveMultipleRecords("msnfp_eventorder", "?$select=msnfp_order,_msnfp_fromeventlistid_value,_msnfp_fromeventid_value,_msnfp_toeventlistid_value,msnfp_orderdate&$filter=_msnfp_toeventlistid_value eq " + originRecordGuid).then(
                        function success(results) {

                            if (results.entities.length > 0) {
                                CreateOrder(newRecordId, results.entities);
                            }
                            else {
                                Xrm.Utility.closeProgressIndicator();
                                var entityFormOptions = {};
                                entityFormOptions["entityName"] = "msnfp_eventlist";
                                entityFormOptions["entityId"] = newRecordId;

                                // Open the form.
                                Xrm.Navigation.openForm(entityFormOptions).then(
                                    function (success) {
                                        Xrm.Utility.closeProgressIndicator();
                                        // Completed all actions, show this to the user so they do not click again:
                                        var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
                                        var alertOptions = { height: 120, width: 260 };
                                        Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                                            function success(result) {
                                                console.log("Alert dialog closed");
                                            },
                                            function (error) {
                                                console.log(error.message);
                                            }
                                        );
                                    },
                                    function (error) {
                                        console.log(error);
                                        Xrm.Utility.closeProgressIndicator();
                                    });
                            }
                        },
                        function (error) {
                            console.log(error.message);
                            // handle error conditions
                        }
                    );

                },
                function (error) {
                    console.log(error);
                    Xrm.Utility.closeProgressIndicator();
                }
            );

        },
            function error(e) {
                console.log(e);
                Xrm.Utility.closeProgressIndicator();
            }
        );
};

var CreateOrder = function (newRecordId, result) {

    for (var count = 0; count < result.length; count++) {
        var data =
        {
            "msnfp_order": result[count].msnfp_order,
            "msnfp_orderdate": result[count].msnfp_orderdate
        };

        if (result[count]._msnfp_fromeventlistid_value !== null)
            data["msnfp_FromEventListId@odata.bind"] = "/msnfp_eventlists(" + result[count]._msnfp_fromeventlistid_value + ")";
        if (result[count]._msnfp_fromeventid_value !== null)
            data["msnfp_FromEventId@odata.bind"] = "/msnfp_events(" + result[count]._msnfp_fromeventid_value + ")";
        if (result[count]._msnfp_toeventlistid_value !== null)
            data["msnfp_ToEventListId@odata.bind"] = "/msnfp_eventlists(" + newRecordId + ")";

        Xrm.WebApi.createRecord("msnfp_eventorder", data).then(
            function success(result) {
                console.log("Event order created");
            },
            function (error) {
                console.log(error);
            }
        );

    }

    Xrm.Utility.closeProgressIndicator();
    var entityFormOptions = {};
    entityFormOptions["entityName"] = "msnfp_eventlist";
    entityFormOptions["entityId"] = newRecordId;

    // Open the form.
    Xrm.Navigation.openForm(entityFormOptions).then(
        function (success) {
            Xrm.Utility.closeProgressIndicator();
            // Completed all actions, show this to the user so they do not click again:
            var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
            var alertOptions = { height: 120, width: 260 };
            Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                function success(result) {
                    console.log("Alert dialog closed");
                },
                function (error) {
                    console.log(error.message);
                }
            );
        },
        function (error) {
            console.log(error);
            Xrm.Utility.closeProgressIndicator();
        });

};


/** -----------------------------End: Event List Related Script ---------------------------------------------------------------------------**/

//On Bulk Receipt entity

function HideButtonsOnBulkReceipt() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    if (formType == 2) {
        return isVisible = true;
    }
    return isVisible;
}

function markGiftAsComplete() {
    var giftIdentifiers = parent.Xrm.Page.data.entity.attributes.get("msnfp_giftidentifiers").getValue();
    var currentEntityGUID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '');

    if (giftIdentifiers != null) {
        var splitIDs = currentEntityGUID.split(";");

        for (var i = 0; i < splitIDs.length; i++) {
            var gift = {};
            var dt = new Date();
            gift["msnfp_printed"] = dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();

            var qry = GetWebAPIUrl() + "msnfp_donationpledges(" + splitIDs[i] + ")";
            UpdateRecord(qry, gift);
        }

        var bulkReceipt = {};
        var dt = new Date();
        bulkReceipt["msnfp_completedon"] = dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();
        bulkReceipt["msnfp_reversedon"] = null;
        bulkReceipt["statecode"] = 1;
        bulkReceipt["statuscode"] = 2;
        var qry = GetWebAPIUrl() + "msnfp_bulkreceipts(" + currentEntityGUID + ")";
        UpdateRecord(qry, bulkReceipt);
    }
    //Xrm.Page.data.refresh(true);
    Xrm.Utility.openEntityForm("msnfp_bulkreceipt", currentEntityGUID, null, true);
}

function ReversedCompletion() {
    var currentEntityGUID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '');
    var giftIdentifiers = parent.Xrm.Page.data.entity.attributes.get("msnfp_giftidentifiers").getValue();

    if (giftIdentifiers != null) {
        var splitIDs = currentEntityGUID.split(";");

        for (var i = 0; i < splitIDs.length; i++) {
            var gift = {};
            gift["msnfp_printed"] = null;

            var qry = GetWebAPIUrl() + "msnfp_donationpledges(" + splitIDs[i] + ")";
            UpdateRecord(qry, gift);
        }
    }

    var bulkReceipt = {};
    var dt = new Date();
    bulkReceipt["msnfp_reversedon"] = dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();
    bulkReceipt["msnfp_completedon"] = null;
    bulkReceipt["statecode"] = 0;
    bulkReceipt["statuscode"] = 1;
    var qry = GetWebAPIUrl() + "msnfp_bulkreceipts(" + currentEntityGUID + ")";
    UpdateRecord(qry, bulkReceipt);

    //Xrm.Page.data.refresh(true);
    Xrm.Utility.openEntityForm("msnfp_bulkreceipt", currentEntityGUID, null, true);
}

function HideReversedCompletion() {
    var completedValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_completedon").getValue();

    var isVisible = false;
    if (!isNullOrUndefined(completedValue)) {
        return isVisible = true;
    }
    return isVisible;
}

//On Membership Entity form
function openMembershipGroupSelection() {
    var currentEntityGUID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '');
    var params = "entityGUID=" + currentEntityGUID + "";

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/membershipgrouplist.html", windowOptions, params);
}

function HideButtonsOnMembership() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    if (formType == 2) {
        return isVisible = true;
    }
    return isVisible;
}

function cloneMembership() {
    var cloneData = {};

    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var select = "msnfp_membershipid,msnfp_name,msnfp_basecost,msnfp_goodwilldate,msnfp_membershipduration,msnfp_renewaldate";

    var oDataUri = GetWebAPIUrl() +
        "msnfp_memberships?$select=" + select + "&$filter=msnfp_membershipid eq " + originRecordGuid + "";

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data != null && data != undefined) {
                cloneData["msnfp_name"] = data.value[0].msnfp_name;
                cloneData["msnfp_basecost"] = data.value[0].msnfp_basecost;
                cloneData["msnfp_goodwilldate"] = data.value[0].msnfp_goodwilldate;
                cloneData["msnfp_membershipduration"] = data.value[0].msnfp_membershipduration;
                cloneData["msnfp_renewaldate"] = data.value[0].msnfp_renewaldate;

                //Create new Record
                var oDataUri = GetWebAPIUrl() + "msnfp_memberships";

                var cloneMembershipID = CreateRecord(oDataUri, cloneData);

                var goQuery = "msnfp_grouporders?";
                goQuery += "$select=msnfp_grouporderid,msnfp_title,_msnfp_frommembershipid_value,_msnfp_tomembershipgroupid_value,msnfp_order,msnfp_orderdate";
                goQuery += "&$filter=_msnfp_frommembershipid_value eq " + originRecordGuid;
                var goResult = ExecuteQuery(GetWebAPIUrl() + goQuery);

                if (!isNullOrUndefined(goResult) && goResult.length > 0) {
                    $(goResult).each(function () {
                        var groupOrderId = this.msnfp_grouporderid;

                        var obj = $.grep(goResult, function (e) {
                            return e.msnfp_grouporderid.toLowerCase() == groupOrderId.toLowerCase();
                        });

                        if (obj != null && obj.length > 0) {
                            var cloneGroupOrder = {};
                            if (!isNullOrUndefined(obj[0]._msnfp_frommembershipid_value))
                                cloneGroupOrder["msnfp_frommembershipid@odata.bind"] = "/msnfp_memberships(" + obj[0]._msnfp_frommembershipid_value + ")";
                            if (!isNullOrUndefined(obj[0]._msnfp_tomembershipgroupid_value))
                                cloneGroupOrder["msnfp_tomembershipgroupid@odata.bind"] = "/msnfp_membershipgroups(" + obj[0]._msnfp_tomembershipgroupid_value + ")";
                            cloneGroupOrder["msnfp_order"] = obj[0].msnfp_order;
                            cloneGroupOrder["msnfp_orderdate"] = obj[0].msnfp_orderdate;

                            var cloneUri = GetWebAPIUrl() + "msnfp_grouporders";
                            CreateRecord(cloneUri, cloneGroupOrder);
                        }
                    });
                }
            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + XmlHttpRequest.responseText);
        }
    });
}

//On Membership Group Entity form
function groupOrderOnMembershipGroup() {
    var currentEntityGUID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '');
    var params = encodeURIComponent("entityGUID=" + currentEntityGUID + "");

    var windowOptions = { height: 500, width: 600 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/grouporderlist.html", windowOptions, params);
}

function cloneMembershipGroup() {
    var cloneData = {};

    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var select = "msnfp_membershipgroupid,msnfp_identifier,msnfp_groupname";

    var oDataUri = GetWebAPIUrl() +
        "msnfp_membershipgroups?$select=" + select + "&$filter=msnfp_membershipgroupid eq " + originRecordGuid + "";

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data != null && data != undefined) {
                //cloneData["msnfp_isclone"] = true;
                var numberOfGroupOrders = 0;

                // Make sure we have room for the words, otherwise use the same name:
                if (data.value[0].msnfp_identifier.length < 91) {
                    cloneData["msnfp_identifier"] = "Clone of " + data.value[0].msnfp_identifier;
                    cloneData["msnfp_groupname"] = "Clone of " + data.value[0].msnfp_groupname;
                }
                else {
                    cloneData["msnfp_identifier"] = data.value[0].msnfp_identifier;
                    cloneData["msnfp_groupname"] = data.value[0].msnfp_groupname;
                }

                console.log("Got membership group, now create record.");

                //Create new Record
                var oDataUri = GetWebAPIUrl() + "msnfp_membershipgroups";

                var cloneMembershipID = CreateRecord(oDataUri, cloneData);

                console.log("Created Record.");

                var goQuery = "msnfp_membershiporders?";
                goQuery += "$select=msnfp_membershiporderid,msnfp_identifier,_msnfp_frommembershipid_value,_msnfp_tomembershipgroupid_value,msnfp_order,msnfp_orderdate";
                goQuery += "&$filter=_msnfp_tomembershipgroupid_value eq " + originRecordGuid;
                var goResult = ExecuteQuery(GetWebAPIUrl() + goQuery);

                if (!isNullOrUndefined(goResult) && goResult.length > 0) {
                    $(goResult).each(function () {
                        var groupOrderId = this.msnfp_membershiporderid;

                        var obj = $.grep(goResult, function (e) {
                            return e.msnfp_membershiporderid.toLowerCase() == groupOrderId.toLowerCase();
                        });

                        if (obj != null && obj.length > 0) {
                            var cloneGroupOrder = {};
                            if (!isNullOrUndefined(obj[0]._msnfp_frommembershipid_value))
                                cloneGroupOrder["msnfp_FromMembershipId@odata.bind"] = "/msnfp_membershipcategories(" + obj[0]._msnfp_frommembershipid_value + ")";
                            if (!isNullOrUndefined(obj[0]._msnfp_tomembershipgroupid_value))
                                cloneGroupOrder["msnfp_ToMembershipGroupId@odata.bind"] = "/msnfp_membershipgroups(" + cloneMembershipID + ")";
                            cloneGroupOrder["msnfp_order"] = obj[0].msnfp_order;
                            cloneGroupOrder["msnfp_identifier"] = "Clone of " + obj[0].msnfp_identifier;
                            cloneGroupOrder["msnfp_orderdate"] = obj[0].msnfp_orderdate;

                            var cloneUri = GetWebAPIUrl() + "msnfp_membershiporders";
                            CreateRecord(cloneUri, cloneGroupOrder);
                            numberOfGroupOrders++;
                        }
                    });

                    // Show a confirmation result after cloning the group:
                    if (cloneMembershipID != null) {
                        var confirmStrings = { text: "Successfully Cloned this Membership Group as well as " + numberOfGroupOrders + " related Membership Orders.", title: "Clone Completed Successfully", subtitle: "", "cancelButtonLabel": "Close", confirmButtonLabel: "Okay" };
                        var confirmOptions = { height: 200, width: 500 };
                        Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
                            function (success) {
                                if (success.confirmed)
                                    console.log("Dialog closed using OK button.");
                                else
                                    console.log("Dialog closed using Cancel button or X.");
                            });
                    }
                }
            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + XmlHttpRequest.responseText);
        }
    });
}

function HideButtonsOnMembershipGroup() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    if (formType == 2) {
        return isVisible = true;
    }
    return isVisible;
}

//Gift Aid Declaration Entity form
function ShowAidDeclarationButtonOnCondition() {
    var configRecord = null;
    var isVisible = false;

    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];


    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_tran_enablegiftaid";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            if (configRecord.msnfp_tran_enablegiftaid) {
                return isVisible = true;
            }
        }
    }
    return isVisible;
}

function OpenGiftAidDeclaration() {
    var customerID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var rQuery = "msnfp_giftaiddeclarations?";
    rQuery += "$select=msnfp_giftaiddeclarationid,_msnfp_customerid_value,createdon&$orderby=createdon desc";
    rQuery += "&$filter=_msnfp_customerid_value eq " + customerID;
    var rResult = ExecuteQuery(GetWebAPIUrl() + rQuery);

    if (!isNullOrUndefined(rResult)) {
        var rec = {};
        var dt = new Date();
        rec["msnfp_updated"] = new Date(dt.getUTCFullYear(), dt.getUTCMonth(), dt.getUTCDate());
        var qry = GetWebAPIUrl() + "msnfp_giftaiddeclarations(" + rResult[0].msnfp_giftaiddeclarationid + ")";
        UpdateRecord(qry, rec);
        alert("Declaration already available for the Customer.");
    }
    else {
        var jobtitle = parent.Xrm.Page.getAttribute("jobtitle").getValue();
        var firstname = parent.Xrm.Page.getAttribute("firstname").getValue();
        var surname = parent.Xrm.Page.getAttribute("lastname").getValue();

        var address1_line1 = parent.Xrm.Page.getAttribute("address1_line1").getValue();
        var address1_line2 = parent.Xrm.Page.getAttribute("address1_line2").getValue();
        var address1_line3 = parent.Xrm.Page.getAttribute("address1_line3").getValue();
        var address1_city = parent.Xrm.Page.getAttribute("address1_city").getValue();
        var address1_stateorprovince = parent.Xrm.Page.getAttribute("address1_stateorprovince").getValue();
        var address1_postalcode = parent.Xrm.Page.getAttribute("address1_postalcode").getValue();
        var address1_country = parent.Xrm.Page.getAttribute("address1_country").getValue();
        var organizationname = "";

        try {
            var customerid = parent.Xrm.Page.data.entity.attributes.get("parentcustomerid");
            if (!isNullOrUndefined(customerid)) {
                organizationname = parent.Xrm.Page.data.entity.attributes.get("parentcustomerid").getValue()[0].name;
            }
        }
        catch (e) {
            console.log(e);
        }

        var currentEntityName = parent.Xrm.Page.data.entity.getEntityName();
        var currentEntityGUID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());
        var params = encodeURIComponent("entityName=" + currentEntityName + "&entityGUID=" + currentEntityGUID + "" + "&jobtitle=" + jobtitle + "&firstname=" + firstname + "&lastname=" + surname + "&address1_line1=" + address1_line1 + "&address1_line2=" + address1_line2 + "&address1_line3=" + address1_line3 + "&address1_city=" + address1_city + "&address1_stateorprovince=" + address1_stateorprovince + "&address1_postalcode=" + address1_postalcode + "&address1_country=" + address1_country + "&organizationname=" + organizationname);

        var windowOptions = { height: 690, width: 870 };
        Xrm.Navigation.openWebResource("msnfp_/webpages/giftaiddeclaration.html", windowOptions, params);
    }
}

function ViewGiftAidDeclaration() {
    var htmlAttribute = parent.Xrm.Page.getAttribute("msnfp_giftaiddeclarationhtml");
    if (htmlAttribute !== null)
        sessionStorage.setItem("html", htmlAttribute.getValue());

    var windowOptions = { height: 690, width: 870 };
    Xrm.Navigation.openWebResource("msnfp_/webpages/viewgiftaiddeclaration.html", windowOptions, null);
}

//When gift Declaration Declaration Delivere Verbal;
function ShowGenerateDeclarationButtonOnCondition() {
    var declarationDelivered = parent.Xrm.Page.data.entity.attributes.get("msnfp_declarationdelivered");

    var isVisible = false;

    if (declarationDelivered.getValue() === 84406001) {
        return isVisible = true;
    }
    return isVisible;
}

//When gift Declaration Declaration Delivere Verbal Or Online;
function ShowGenerateDeclarationButtonOnBothCondition() {
    var declarationDelivered = parent.Xrm.Page.data.entity.attributes.get("msnfp_declarationdelivered");

    var isVisible = false;

    if (declarationDelivered.getValue() === 84406001 || declarationDelivered.getValue() === 84406000) {
        return isVisible = true;
    }
    return isVisible;
}

function GenerateDeclaration() {

    var workflowName = null;

    workflowName = "WF - Gift Aid Declaration - Email Receipt";

    var workflowId = getWorkflowId(workflowName);

    var currentID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    if (workflowId != undefined && workflowId != null) {
        ExecuteWorkflow(currentID, workflowId);
    }
}

function getWorkflowId(workflowName) {
    //var workflowName = "WF - Gift - Email Receipt";
    select = "workflows?$select=workflowid&$filter=statecode eq 1 and _parentworkflowid_value eq null and name eq \'" + workflowName + "\'";
    var workflowRecord = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);

    if (workflowRecord != undefined && workflowRecord != null) {
        return workflowRecord[0].workflowid;
    }
    return undefined;
}

function ExecuteWorkflow(currentID, workflowId) {
    var functionName = "executeWorkflow >>";
    var query = "";
    try {
        //Define the query to execute the action
        query = "workflows(" + workflowId.replace("}", "").replace("{", "") + ")/Microsoft.Dynamics.CRM.ExecuteWorkflow";
        var data = { "EntityId": currentID };

        var req = new XMLHttpRequest();
        req.open("POST", XrmServiceUtility.GetWebAPIUrl() + query, true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-Version", "4.0");

        req.onreadystatechange = function () {
            if (this.readyState == 4) {
                req.onreadystatechange = null;
                if (this.status == 200) {
                    //success callback this returns null since no return value available.
                    var result = JSON.parse(this.response);
                    Xrm.Utility.alertDialog("Mail sent successfully");
                }
                else {
                    //error callback
                    var error = this.response;
                }
            }
        };
        req.send(JSON.stringify(data));
    }
    catch (e) {
        // throwError(functionName, e);
    }
}

function PrintDeclaration() {
    DownloadReceiptTemplete("Microsoft.Dynamics.CRM.msnfp_ActionGiftAidDeclarationReceipt");
}

function DownloadReceiptTemplete(requestName) {

    var entityId = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    //var entityId = parent.Xrm.Page.data.entity.getId().replace(/\{|\}/gi, '');

    try {
        var entityName = "msnfp_giftaiddeclaration";

        var entity = {};

        var organizationUrl = parent.Xrm.Page.context.getClientUrl();
        var data = {};
        var query = "msnfp_giftaiddeclarations(" + entityId + ")/" + requestName;
        var req = new XMLHttpRequest();
        req.open("POST", organizationUrl + "/api/data/v8.0/" + query, true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-Version", "4.0");
        req.onreadystatechange = function () {
            if (this.readyState == 4) {
                req.onreadystatechange = null;
                if (this.status == 204) {
                    select = "annotations?$select=documentbody,createdon,subject,filename,notetext,_objectid_value,annotationid&$orderby=createdon desc&$top=1&$filter=(_objectid_value eq " + entityId + ")";
                    var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);
                    if (result != null && result.length > 0) {
                        download(base64ToArrayBuffer(result[0].documentbody), result[0].filename, "application/pdf");
                        XrmServiceUtility.DeleteRecord(result[0].annotationid, 'annotations');
                    }
                }
            }
        };
        req.send(window.JSON.stringify(data));
    }
    catch (error) {
        //errorHandler(error);
    }
}

function base64ToArrayBuffer(base64) {
    var binaryString = window.atob(base64);
    var len = binaryString.length;
    var bytes = new Uint8Array(len);
    for (var i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes.buffer;
}

//Gift Aid Returns Entity form
function HideGiftAidRetunrnsButtons() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    if (formType == 2) {
        return isVisible = true;
    }
    return isVisible;
}

function RecalculateSubmission() {

    var currentGiftAidReturID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var transactioncurrencyId = parent.Xrm.Page.data.entity.attributes.get("transactioncurrencyid").getValue()[0].id;
    var businessunitId = parent.Xrm.Page.data.entity.attributes.get("msnfp_businessunit").getValue()[0].id;
    var yearEndDate = parent.Xrm.Page.data.entity.attributes.get("msnfp_yearenddate").getValue();

    var yearendDt = yearEndDate;
    var finalDate = "";

    if (!isNullOrUndefined(yearEndDate)) {
        yearendDt = yearendDt.setFullYear(yearEndDate.getFullYear() - 4);
        finalDate = new Date(yearendDt);
        finalDate = finalDate.getFullYear() + '-' + (finalDate.getMonth() + 1) + '-' + finalDate.getDate();
    }


    if (!isNullOrUndefined(transactioncurrencyId) && !isNullOrUndefined(businessunitId) & !isNullOrUndefined(yearEndDate)) {
        var giftList = [];
        var giftClimAmount = [];
        //Get gift List


        giftList = executeQuerywithFetchXMLGift(transactioncurrencyId, businessunitId, finalDate, currentGiftAidReturID);



        // Set Cliam Amount value in Gift Aid Declaration Entity
        giftClimAmount = executeQuerywithFetchXMLSetClimAmount(transactioncurrencyId, businessunitId, finalDate, currentGiftAidReturID);

    }

}

//Get List of Gifts
function executeQuerywithFetchXMLGift(transactioncurrencyId, businessunitId, finalDate, currentGiftAidReturID) {
    var giftList = [];
    var apiQuery = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpledges";

    var fetchXML = '<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">' +
        '<entity name="msnfp_donationpledge">' +
        '<attribute name="msnfp_donationpledgeid" />' +
        '<order attribute="msnfp_title" descending="true" />' +
        '<filter type="and">' +
        '<condition attribute="transactioncurrencyid" operator="eq" value="' + transactioncurrencyId + '" />' +
        '<condition attribute="owningbusinessunit" operator="eq" uitype="owningbusinessunit" value="' + businessunitId + '" />' +
        '<condition attribute="statuscode" operator="eq" value="844060000" />' +
        '<condition attribute="msnfp_type" operator="eq" value="84406000" />' +
        '<condition attribute="msnfp_giftaidreturn" operator="null" />' +
        '<condition attribute="msnfp_date" operator="on-or-after" value="' + finalDate + '" />' +
        ' </filter>' +
        '<link-entity name="contact" from="contactid" to="msnfp_customerid" link-type="inner" alias="ag">' +
        '<link-entity name="msnfp_giftaiddeclaration" from="msnfp_customerid" to="contactid" link-type="inner" alias="ah">' +
        '<filter type="and">' +
        '<condition attribute="statuscode" operator="eq" value="1" />' +
        '</filter>' +
        '</link-entity>' +
        '</link-entity>' +
        '</entity>' +
        '</fetch>';

    var encodedFetchXML = encodeURIComponent(fetchXML);

    var results;
    var claimAmount;
    var req = new XMLHttpRequest();
    req.open("GET", apiQuery + "?fetchXml=" + encodedFetchXML, true);
    req.setRequestHeader("OData-MaxVersion", "4.0");
    req.setRequestHeader("OData-Version", "4.0");
    req.setRequestHeader("Accept", "application/json");
    req.setRequestHeader("Prefer", "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"");
    req.onreadystatechange = function () {
        if (this.readyState === 4) {
            req.onreadystatechange = null;
            if (this.status === 200) {
                results = JSON.parse(this.responseText);
                if (!isNullOrUndefined(results) && results.value.length > 0) {
                    giftList = results.value;

                    if (!isNullOrUndefined(giftList) && giftList.length > 0) {
                        $(giftList).each(function () {
                            var entity = {};
                            entity["msnfp_GiftAidReturn@odata.bind"] = "/msnfp_giftaidreturns(" + currentGiftAidReturID + ")";
                            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpledges(" + this.msnfp_donationpledgeid + ")";
                            var updatedID = XrmServiceUtility.UpdateRecord(qry, entity);
                        });
                    }
                }
            }
        }
    };
    req.send();

    //return results;
}

//Get List of Clim Amount
function executeQuerywithFetchXMLSetClimAmount(transactioncurrencyId, businessunitId, finalDate, currentGiftAidReturID) {
    var giftClimAmount = [];
    var apiQuery = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpledges";

    var fetchXML = '<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">' +
        '<entity name="msnfp_donationpledge">' +
        '<attribute name="msnfp_donationpledgeid" />' +
        '<attribute name="msnfp_giftaidclaimamount" />' +
        '<order attribute="msnfp_title" descending="true" />' +
        '<filter type="and">' +
        '<condition attribute="transactioncurrencyid" operator="eq" value="' + transactioncurrencyId + '" />' +
        '<condition attribute="owningbusinessunit" operator="eq" uitype="owningbusinessunit" value="' + businessunitId + '" />' +
        '<condition attribute="statuscode" operator="eq" value="844060000" />' +
        '<condition attribute="msnfp_type" operator="eq" value="84406000" />' +
        '<condition attribute="msnfp_giftaidreturn" operator="eq" value="' + currentGiftAidReturID + '" />' +
        '<condition attribute="msnfp_date" operator="on-or-after" value="' + finalDate + '" />' +
        ' </filter>' +
        '<link-entity name="contact" from="contactid" to="msnfp_customerid" link-type="inner" alias="ag">' +
        '<link-entity name="msnfp_giftaiddeclaration" from="msnfp_customerid" to="contactid" link-type="inner" alias="ah">' +
        '<filter type="and">' +
        '<condition attribute="statuscode" operator="eq" value="1" />' +
        '</filter>' +
        '</link-entity>' +
        '</link-entity>' +
        '</entity>' +
        '</fetch>';


    var encodedFetchXML = encodeURIComponent(fetchXML);

    var results;
    var claimAmount = 0;
    var req = new XMLHttpRequest();
    req.open("GET", apiQuery + "?fetchXml=" + encodedFetchXML, true);
    req.setRequestHeader("OData-MaxVersion", "4.0");
    req.setRequestHeader("OData-Version", "4.0");
    req.setRequestHeader("Accept", "application/json");
    req.setRequestHeader("Prefer", "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"");
    req.onreadystatechange = function () {
        if (this.readyState === 4) {
            req.onreadystatechange = null;
            if (this.status === 200) {
                results = JSON.parse(this.responseText);
                if (!isNullOrUndefined(results) && results.value.length > 0) {
                    giftClimAmount = results.value;

                    if (!isNullOrUndefined(giftClimAmount) && giftClimAmount.length > 0) {
                        $(giftClimAmount).each(function () {
                            claimAmount += parseFloat(this.msnfp_giftaidclaimamount);
                        });
                        parent.Xrm.Page.data.entity.attributes.get("msnfp_claimamount").setValue(claimAmount);
                        parent.Xrm.Page.data.entity.save();
                    }
                }
            }
        }
    };
    req.send();
    // return results;
}


//On Event Page entity ribbon

function PostToWebSiteEventPage() {
    var visible = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible");
    visible.setValue(true);
    parent.Xrm.Page.data.entity.attributes.get("msnfp_madevisible").setValue(new Date());

    parent.Xrm.Page.data.entity.save();
}

function HidePostToWebSiteButtonEventPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && !visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function RemoveFromWebSiteEventPage() {
    var visible = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible");
    visible.setValue(false);
    var removed = parent.Xrm.Page.data.entity.attributes.get("msnfp_removed");
    removed.setValue(true);
    parent.Xrm.Page.data.entity.attributes.get("msnfp_removedon").setValue(new Date());

    parent.Xrm.Page.data.entity.save();
}

function HideRemoveFromWebSiteButtonEventPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function SynchronizeNowEventPage() {
    parent.Xrm.Page.data.entity.attributes.get("msnfp_lastpublished").setValue(new Date());
    parent.Xrm.Page.data.entity.save();
}

function HideSynchronizeNowButtonEventPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function ViewPageEventPage() {
    var externalURL = parent.Xrm.Page.data.entity.attributes.get("msnfp_externalurl").getValue();
    var height = parent.Xrm.Page.data.entity.attributes.get("msnfp_maxheight").getValue();
    var width = parent.Xrm.Page.data.entity.attributes.get("msnfp_maxwidth").getValue();

    if (!isNullOrUndefined(externalURL)) {
        if (!isNullOrUndefined(height) && !isNullOrUndefined(width)) {
            window.open(externalURL, "", "width=" + width + ",height=" + height + "", '_blank');
        }
        else {
            window.open(externalURL, "width=400,height=600", '_blank');
        }
    }
}

function HideViewPageButtonEventPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

var AddToListEventPage = function (formContext) {

    var currentEntityGUID = formContext.data.entity.getId().replace('{', '').replace('}', '');

    var params = "recordId=" + currentEntityGUID;

    var pageInput = {
        pageType: "webresource",
        webresourceName: "msnfp_EventList.html",
        data: params
    };
    var navigationOptions = {
        target: 2, // 2 is for opening the page as a dialog. 
        width: 800, // default is px. can be specified in % as well. 
        height: 700, // default is px. can be specified in % as well. 
        position: 1 // Specify 1 to open the dialog in center; 2 to open the dialog on the side. Default is 1 (center). 
    };
    Xrm.Navigation.navigateTo(pageInput, navigationOptions).then(
        function success() {

        },
        function error(e) {
            // Handle errors 
        }
    );
};

function CloneEventPage() {

    var cloneData = {};
    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var select = "msnfp_eventpageid,_msnfp_appealid_value,_msnfp_campaignid_value,_msnfp_eventid_value,_msnfp_fundid_value,_msnfp_packageid_value,";
    select += "msnfp_carouselimagefive,msnfp_carouselimagefour,msnfp_carouselimageone,msnfp_carouselimagesix,msnfp_carouselimagethree,";
    select += "msnfp_carouselimagetwo,_msnfp_configurationid_value,msnfp_costamount,msnfp_costpercentage,msnfp_externalurl,msnfp_largeimage,";
    select += "msnfp_lastpublished,msnfp_madevisible,msnfp_message,msnfp_paymentnotice,msnfp_removed,msnfp_removedon,msnfp_selectcurrency,";
    select += "msnfp_showapple,msnfp_showcompany,msnfp_showcovercosts,msnfp_showgoogle,msnfp_showvisa,msnfp_thankyou,msnfp_visible,";
    select += "_msnfp_teamownerid_value,_msnfp_termsofreferenceid_value,msnfp_labellanguage,msnfp_showinvoice,msnfp_invoicemessage,";
    select += "msnfp_homepageurl,msnfp_forceredirecttiming,msnfp_forceredirect,statuscode,statecode";

    var oDataUri = GetWebAPIUrl() +
        "msnfp_eventpages?$select=" + select + "&$filter=msnfp_eventpageid eq " + originRecordGuid + "";

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data != null && data != undefined) {
                cloneData["msnfp_carouselimagefive"] = data.value[0].msnfp_carouselimagefive;
                cloneData["msnfp_carouselimagefour"] = data.value[0].msnfp_carouselimagefour;
                cloneData["msnfp_carouselimageone"] = data.value[0].msnfp_carouselimageone;
                cloneData["msnfp_carouselimagesix"] = data.value[0].msnfp_carouselimagesix;
                cloneData["msnfp_carouselimagethree"] = data.value[0].msnfp_carouselimagethree;
                cloneData["msnfp_carouselimagetwo"] = data.value[0].msnfp_carouselimagetwo;
                cloneData["msnfp_costamount"] = data.value[0].msnfp_costamount;
                cloneData["msnfp_costpercentage"] = data.value[0].msnfp_costpercentage;
                cloneData["msnfp_eventpagename"] = data.value[0].msnfp_eventpagename;
                cloneData["msnfp_giftaidaceptence"] = data.value[0].msnfp_giftaidaceptence;
                cloneData["msnfp_giftaiddeclaration"] = data.value[0].msnfp_giftaiddeclaration;
                cloneData["msnfp_giftaiddetails"] = data.value[0].msnfp_giftaiddetails;
                cloneData["msnfp_largeimage"] = data.value[0].msnfp_largeimage;
                cloneData["msnfp_maxheight"] = data.value[0].msnfp_maxheight;
                cloneData["msnfp_maxwidth"] = data.value[0].msnfp_maxwidth;
                cloneData["msnfp_message"] = data.value[0].msnfp_message;
                cloneData["msnfp_paymentnotice"] = data.value[0].msnfp_paymentnotice;
                cloneData["msnfp_selectcurrency"] = data.value[0].msnfp_selectcurrency;
                cloneData["msnfp_showamex"] = data.value[0].msnfp_showamex;
                cloneData["msnfp_showapple"] = data.value[0].msnfp_showapple;
                cloneData["msnfp_showbank"] = data.value[0].msnfp_showbank;
                cloneData["msnfp_showcompany"] = data.value[0].msnfp_showcompany;
                cloneData["msnfp_showcovercosts"] = data.value[0].msnfp_showcovercosts;
                cloneData["msnfp_showdiners"] = data.value[0].msnfp_showdiners;
                cloneData["msnfp_showgoogle"] = data.value[0].msnfp_showgoogle;
                cloneData["msnfp_showjcb"] = data.value[0].msnfp_showjcb;
                cloneData["msnfp_showmastercard"] = data.value[0].msnfp_showmastercard;
                cloneData["msnfp_showmastercarddebit"] = data.value[0].msnfp_showmastercarddebit;
                cloneData["msnfp_showmessage"] = data.value[0].msnfp_showmessage;
                cloneData["msnfp_showpaymentnotice"] = data.value[0].msnfp_showpaymentnotice;
                cloneData["msnfp_showsignup"] = data.value[0].msnfp_showsignup;
                // cloneData["msnfp_showtaxrebate"] = data.value[0].msnfp_showtaxrebate;
                cloneData["msnfp_showthankyou"] = data.value[0].msnfp_showthankyou;
                cloneData["msnfp_showvisa"] = data.value[0].msnfp_showvisa;
                cloneData["msnfp_showvisadebit"] = data.value[0].msnfp_showvisadebit;
                cloneData["msnfp_signup"] = data.value[0].msnfp_signup;
                cloneData["msnfp_smallimage"] = data.value[0].msnfp_smallimage;
                cloneData["msnfp_thankyou"] = data.value[0].msnfp_thankyou;
                cloneData["msnfp_visible"] = data.value[0].msnfp_visible;
                cloneData["statuscode"] = data.value[0].statuscode;
                cloneData["statecode"] = data.value[0].statecode;
                cloneData["msnfp_labellanguage"] = data.value[0].msnfp_labellanguage;
                cloneData["msnfp_showinvoice"] = data.value[0].msnfp_showinvoice;
                cloneData["msnfp_invoicemessage"] = data.value[0].msnfp_invoicemessage;
                cloneData["msnfp_forceredirect"] = data.value[0].msnfp_forceredirect;
                cloneData["msnfp_forceredirecttiming"] = data.value[0].msnfp_forceredirecttiming;
                cloneData["msnfp_homepageurl"] = data.value[0].msnfp_homepageurl;

                if (data.value[0]._msnfp_appealid_value != null)
                    cloneData["msnfp_appealid@odata.bind"] = "/msnfp_appeals(" + data.value[0]._msnfp_appealid_value + ")";
                if (data.value[0]._msnfp_eventid_value != null)
                    cloneData["msnfp_eventid@odata.bind"] = "/msnfp_events(" + data.value[0]._msnfp_eventid_value + ")";
                if (data.value[0]._msnfp_campaignid_value != null)
                    cloneData["msnfp_campaignid@odata.bind"] = "/campaigns(" + data.value[0]._msnfp_campaignid_value + ")";
                if (data.value[0]._msnfp_fundid_value != null)
                    cloneData["msnfp_fundid@odata.bind"] = "/msnfp_funds(" + data.value[0]._msnfp_fundid_value + ")";
                if (data.value[0]._msnfp_packageid_value != null)
                    cloneData["msnfp_packageid@odata.bind"] = "/msnfp_packages(" + data.value[0]._msnfp_packageid_value + ")";
                if (data.value[0]._msnfp_configurationid_value != null)
                    cloneData["msnfp_configurationid@odata.bind"] = "/msnfp_configurations(" + data.value[0]._msnfp_configurationid_value + ")";

                if (data.value[0]._msnfp_teamownerid_value != null)
                    cloneData["msnfp_teamownerid@odata.bind"] = "/teams(" + data.value[0]._msnfp_teamownerid_value + ")";
                if (data.value[0]._msnfp_termsofreferenceid_value != null)
                    cloneData["msnfp_TermsofReferenceid@odata.bind"] = "/msnfp_termsofreferences(" + data.value[0]._msnfp_termsofreferenceid_value + ")";

                ///RTP/RTPUAT fields only
                if (data.value[0]._rtp_accountcodeid_value != null)
                    cloneData["rtp_accountcodeid@odata.bind"] = "/rtp_accountcodeses(" + data.value[0]._rtp_accountcodeid_value + ")";
                if (data.value[0]._rtp_departmentcodeid_value != null)
                    cloneData["rtp_departmentcodeid@odata.bind"] = "/rtp_departmentcodes(" + data.value[0]._rtp_departmentcodeid_value + ")";
                if (data.value[0]._msnfp_fundraisingstream_value != null)
                    cloneData["msnfp_fundraisingstream@odata.bind"] = "/msnfp_fundraisingstreams(" + data.value[0]._msnfp_fundraisingstream_value + ")";

                if (!isNullOrUndefined(data.value[0].msnfp_fundingtype)) {
                    cloneData["msnfp_fundingtype"] = data.value[0].msnfp_fundingtype;
                }
                ///end of RTP/RTPUAT fields only

                //Create new Record
                var oDataUri = GetWebAPIUrl() + "msnfp_eventpages";

                var cloneEventPageID = CreateRecord(oDataUri, cloneData);
            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + XmlHttpRequest.responseText);
        }
    });
}


function HideGenerateiFrameButtonEventPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

function GenerateURLEventPage() {
    var giftAzureURL = "";
    var configRecord = null;

    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value"
    userSelect += "&$filter=systemuserid eq " + currentuserID;
    var user = ExecuteQuery(GetWebAPIUrl() + userSelect);
    user = user[0];

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_Azure_WebApp";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            var currentRecordID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

            configRecord = configresult[0];

            if (!isNullOrUndefined(configRecord.msnfp_Azure_WebApp)) {
                giftAzureURL = configRecord.msnfp_Azure_WebApp + "EventPage/ViewEventDetail/";
            }

            parent.Xrm.Page.data.entity.attributes.get("msnfp_externalurl").setValue(giftAzureURL + currentRecordID);

            parent.Xrm.Page.data.entity.save();
        }
    }
}

function HideGenerateURLButtonEventPage() {
    var visibleValue = parent.Xrm.Page.data.entity.attributes.get("msnfp_visible").getValue();
    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType == 2 && visibleValue) {
        return isVisible = true;
    }
    return isVisible;
}

//Helper Methods
function getFormattedDate(date) {
    return date.getFullYear()
        + "-"
        + ("0" + (date.getMonth() + 1)).slice(-2)
        + "-"
        + ("0" + date.getDate()).slice(-2);
}


//On Receipt Entity
function SetVoidPaymentFailed() {
    //var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    //status.setValue(844060002);//Void (Payment Failed)
    //debugger;
    //var isUpdated = parent.Xrm.Page.data.entity.attributes.get("msnfp_isupdated");
    //isUpdated.setValue(true);
    //Xrm.Page.data.entity.save();

    var currentRecordID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    if (!isNullOrUndefined(currentRecordID)) {
        var rQuery = "msnfp_receipts?";
        rQuery += "$select=msnfp_receiptid,statuscode,msnfp_isupdated";
        rQuery += "&$filter=msnfp_receiptid eq " + currentRecordID;
        var rResult = ExecuteQuery(GetWebAPIUrl() + rQuery);

        if (!isNullOrUndefined(rResult)) {
            var rec = {};
            rec["statuscode"] = 844060002;
            rec["msnfp_isupdated"] = true;
            var qry = GetWebAPIUrl() + "msnfp_receipts(" + currentRecordID + ")";
            UpdateRecord(qry, rec);
            parent.Xrm.Page.data.refresh(true);
        }
    }
}

function SetVoidReissued() {
    //var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    //status.setValue(844060001);//Void (Reissued)
    //debugger;
    //var isUpdated = parent.Xrm.Page.data.entity.attributes.get("msnfp_isupdated");
    //isUpdated.setValue(true);
    //Xrm.Page.data.entity.save();
    var currentRecordID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    if (!isNullOrUndefined(currentRecordID)) {
        var rQuery = "msnfp_receipts?";
        rQuery += "$select=msnfp_receiptid,statuscode,msnfp_isupdated";
        rQuery += "&$filter=msnfp_receiptid eq " + currentRecordID;
        var rResult = ExecuteQuery(GetWebAPIUrl() + rQuery);

        if (!isNullOrUndefined(rResult)) {
            var rec = {};
            rec["statuscode"] = 844060001;
            rec["msnfp_isupdated"] = true;
            var qry = GetWebAPIUrl() + "msnfp_receipts(" + currentRecordID + ")";
            UpdateRecord(qry, rec);
            parent.Xrm.Page.data.refresh(true);
        }
    }
}

//On Campaign Page entity Ribbon

function HideCloneCampaignPageButton() {

    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType === 2) {
        return isVisible = true;
    }
    return isVisible;
}

function CloneCampaignPage() {

    var cloneData = {};
    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var select = "msnfp_campaignpageid,msnfp_identifier,msnfp_actionheader,_msnfp_appealid_value,msnfp_campaignend,_msnfp_campaignid_value,msnfp_campaignstart,_msnfp_configurationid_value,msnfp_covercostamount,msnfp_covercostpercentage,";
    select += "msnfp_displaytitle,_msnfp_eventpageid_value,msnfp_forceredirecttiming,msnfp_forceredirecturl,msnfp_giftaidacceptence,msnfp_givingfrequencycode,msnfp_goal_showamount,msnfp_goal_showcountdown,msnfp_goal_showpercent,msnfp_goal_showsupporters,";
    select += "msnfp_goal_amount,msnfp_headermessage,msnfp_labellanguagecode,msnfp_largeimage,msnfp_message,_msnfp_packageid_value,msnfp_recurrencestartcode,msnfp_setacceptnotice,msnfp_setcovercosts,msnfp_setsignup,";
    select += "msnfp_showapple,msnfp_showcompany,msnfp_showcovercosts,msnfp_showcreditcard,msnfp_showgiftaid,msnfp_showgoogle,msnfp_showinhonor,msnfp_showmatchdonation,msnfp_signup,msnfp_smallimage,_msnfp_teamownerid_value,_msnfp_termsofreferenceid_value,msnfp_thankyou,_ownerid_value,statecode,statuscode";

    var oDataUri = GetWebAPIUrl() +
        "msnfp_campaignpages?$select=" + select + "&$filter=msnfp_campaignpageid eq " + originRecordGuid + "";

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data !== null && data !== undefined) {
                // We set msnfp_wascloned to true. If we don't do this, the plugin will create additional campaign page actions on create of the campaign page (8 in total).
                cloneData["msnfp_wascloned"] = true;

                if (!isNullOrUndefined(data.value[0].msnfp_actionheader)) {
                    cloneData["msnfp_actionheader"] = data.value[0].msnfp_actionheader;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_campaignend)) {
                    cloneData["msnfp_campaignend"] = data.value[0].msnfp_campaignend;
                }
                // Deprecated:
                //if (!isNullOrUndefined(data.value[0].msnfp_campaignpagename)) {
                //    cloneData["msnfp_campaignpagename"] = data.value[0].msnfp_campaignpagename;
                //}
                if (!isNullOrUndefined(data.value[0].msnfp_campaignstart)) {
                    cloneData["msnfp_campaignstart"] = data.value[0].msnfp_campaignstart;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_covercostamount)) {
                    cloneData["msnfp_covercostamount"] = data.value[0].msnfp_covercostamount;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_covercostpercentage)) {
                    cloneData["msnfp_covercostpercentage"] = data.value[0].msnfp_covercostpercentage;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_displaytitle)) {
                    cloneData["msnfp_displaytitle"] = data.value[0].msnfp_displaytitle;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_identifier)) {
                    // Make sure we have enough space for the words ' - Clone':
                    if (data.value[0].msnfp_identifier.length >= 142) {
                        cloneData["msnfp_identifier"] = data.value[0].msnfp_identifier;
                    }
                    else {
                        cloneData["msnfp_identifier"] = data.value[0].msnfp_identifier + " - Clone";
                    }
                }
                if (!isNullOrUndefined(data.value[0].msnfp_maxwidth)) {
                    cloneData["msnfp_maxwidth"] = data.value[0].msnfp_maxwidth;
                }
                /* Deprecated:
                 * if (!isNullOrUndefined(data.value[0].msnfp_forceredirect)) {
                    cloneData["msnfp_forceredirect"] = data.value[0].msnfp_forceredirect;
                }*/
                if (!isNullOrUndefined(data.value[0].msnfp_forceredirecttiming)) {
                    cloneData["msnfp_forceredirecttiming"] = data.value[0].msnfp_forceredirecttiming;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_forceredirecturl)) {
                    cloneData["msnfp_forceredirecturl"] = data.value[0].msnfp_forceredirecturl;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_giftaidacceptence)) {
                    cloneData["msnfp_giftaidacceptence"] = data.value[0].msnfp_giftaidacceptence;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_givingfrequencycode)) {
                    cloneData["msnfp_givingfrequencycode"] = data.value[0].msnfp_givingfrequencycode;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_goal_showamount)) {
                    cloneData["msnfp_goal_showamount"] = data.value[0].msnfp_goal_showamount;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_goal_showcountdown)) {
                    cloneData["msnfp_goal_showcountdown"] = data.value[0].msnfp_goal_showcountdown;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_goal_showpercent)) {
                    cloneData["msnfp_goal_showpercent"] = data.value[0].msnfp_goal_showpercent;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_goal_showsupporters)) {
                    cloneData["msnfp_goal_showsupporters"] = data.value[0].msnfp_goal_showsupporters;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_goal_amount)) {
                    cloneData["msnfp_goal_amount"] = data.value[0].msnfp_goal_amount;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_headermessage)) {
                    cloneData["msnfp_headermessage"] = data.value[0].msnfp_headermessage;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_labellanguagecode)) {
                    cloneData["msnfp_labellanguagecode"] = data.value[0].msnfp_labellanguagecode;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_largeimage)) {
                    cloneData["msnfp_largeimage"] = data.value[0].msnfp_largeimage;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_message)) {
                    cloneData["msnfp_message"] = data.value[0].msnfp_message;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_recurrencestartcode)) {
                    cloneData["msnfp_recurrencestartcode"] = data.value[0].msnfp_recurrencestartcode;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_setacceptnotice)) {
                    cloneData["msnfp_setacceptnotice"] = data.value[0].msnfp_setacceptnotice;
                }

                if (!isNullOrUndefined(data.value[0].msnfp_setcovercosts)) {
                    cloneData["msnfp_setcovercosts"] = data.value[0].msnfp_setcovercosts;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_setsignup)) {
                    cloneData["msnfp_setsignup"] = data.value[0].msnfp_setsignup;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_showapple)) {
                    cloneData["msnfp_showapple"] = data.value[0].msnfp_showapple;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_showcompany)) {
                    cloneData["msnfp_showcompany"] = data.value[0].msnfp_showcompany;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_showcovercosts)) {
                    cloneData["msnfp_showcovercosts"] = data.value[0].msnfp_showcovercosts;
                }

                if (!isNullOrUndefined(data.value[0].msnfp_showcreditcard)) {
                    cloneData["msnfp_showcreditcard"] = data.value[0].msnfp_showcreditcard;
                }
                cloneData["statuscode"] = data.value[0].statuscode;
                cloneData["statecode"] = data.value[0].statecode;
                if (!isNullOrUndefined(data.value[0].msnfp_showgiftaid)) {
                    cloneData["msnfp_showgiftaid"] = data.value[0].msnfp_showgiftaid;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_showgoogle)) {
                    cloneData["msnfp_showgoogle"] = data.value[0].msnfp_showgoogle;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_showinhonor)) {
                    cloneData["msnfp_showinhonor"] = data.value[0].msnfp_showinhonor;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_showmatchdonation)) {
                    cloneData["msnfp_showmatchdonation"] = data.value[0].msnfp_showmatchdonation;
                }
                /* Deprecated:
                 * if (!isNullOrUndefined(data.value[0].msnfp_showsignup)) {
                    cloneData["msnfp_showsignup"] = data.value[0].msnfp_showsignup;
                }*/
                if (!isNullOrUndefined(data.value[0].msnfp_signup)) {
                    cloneData["msnfp_signup"] = data.value[0].msnfp_signup;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_smallimage)) {
                    cloneData["msnfp_smallimage"] = data.value[0].msnfp_smallimage;
                }
                if (!isNullOrUndefined(data.value[0].msnfp_thankyou)) {
                    cloneData["msnfp_thankyou"] = data.value[0].msnfp_thankyou;
                }

                if (data.value[0]._msnfp_appealid_value !== null)
                    cloneData["msnfp_AppealId@odata.bind"] = "/msnfp_appeals(" + data.value[0]._msnfp_appealid_value + ")";
                if (data.value[0]._msnfp_campaignid_value !== null)
                    cloneData["msnfp_CampaignId@odata.bind"] = "/campaigns(" + data.value[0]._msnfp_campaignid_value + ")";
                if (data.value[0]._msnfp_eventpageid_value !== null)
                    cloneData["msnfp_EventPageId@odata.bind"] = "/msnfp_events(" + data.value[0]._msnfp_eventpageid_value + ")";

                /* Deprecated:
                 *if (data.value[0]._msnfp_fundid_value !== null)
                    cloneData["msnfp_fundid@odata.bind"] = "/msnfp_funds(" + data.value[0]._msnfp_fundid_value + ")";
                */

                if (data.value[0]._msnfp_packageid_value !== null)
                    cloneData["msnfp_PackageId@odata.bind"] = "/msnfp_packages(" + data.value[0]._msnfp_packageid_value + ")";
                if (data.value[0]._msnfp_configurationid_value !== null)
                    cloneData["msnfp_ConfigurationId@odata.bind"] = "/msnfp_configurations(" + data.value[0]._msnfp_configurationid_value + ")";
                if (data.value[0]._transactioncurrencyid_value !== null)
                    cloneData["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + data.value[0]._transactioncurrencyid_value + ")";

                if (data.value[0]._msnfp_teamownerid_value != null)
                    cloneData["msnfp_TeamOwnerId@odata.bind"] = "/teams(" + data.value[0]._msnfp_teamownerid_value + ")";

                if (data.value[0]._ownerid_value !== null)
                    cloneData["ownerid@odata.bind"] = "/systemusers(" + data.value[0]._ownerid_value + ")";


                if (data.value[0]._msnfp_termsofreferenceid_value !== null)
                    cloneData["msnfp_TermsOfReferenceId@odata.bind"] = "/msnfp_termsofreferences(" + data.value[0]._msnfp_termsofreferenceid_value + ")";

                ////Create new Record
                var oDataUri = GetWebAPIUrl() + "msnfp_campaignpages";
                var cloneCampaignpagePageID = CreateRecord(oDataUri, cloneData);

                //Created Related Image
                if (!isNullOrUndefined(cloneCampaignpagePageID)) {
                    var daQuery = "msnfp_relatedimages?";
                    daQuery += "$select=msnfp_relatedimageid,_msnfp_campaignpageid_value,msnfp_smallimage,msnfp_lastpublished,msnfp_identifier";
                    daQuery += "&$filter=_msnfp_campaignpageid_value eq " + "'" + originRecordGuid + "'";
                    var daResult = ExecuteQuery(GetWebAPIUrl() + daQuery);

                    if (!isNullOrUndefined(daResult) && daResult.length > 0) {
                        $(daResult).each(function () {

                            var cloneRelatedImage = {};

                            if (!isNullOrUndefined(this.msnfp_smallimage)) {
                                cloneRelatedImage["msnfp_smallimage"] = this.msnfp_smallimage; // was msnfp_image
                            }
                            if (!isNullOrUndefined(this.msnfp_lastpublished)) {
                                cloneRelatedImage["msnfp_lastpublished"] = this.msnfp_lastpublished;
                            }
                            if (!isNullOrUndefined(this.msnfp_identifier)) {
                                // Make sure we have enough space for the words ' - Clone':
                                if (this.msnfp_identifier.length >= 142) {
                                    cloneRelatedImage["msnfp_identifier"] = this.msnfp_identifier; // was msnfp_title
                                }
                                else {
                                    cloneRelatedImage["msnfp_identifier"] = this.msnfp_identifier + " - Clone"; // was msnfp_title
                                }
                            }

                            cloneRelatedImage["msnfp_CampaignPageId@odata.bind"] = "/msnfp_campaignpages(" + cloneCampaignpagePageID + ")";

                            var cloneRelatedImageUri = GetWebAPIUrl() + "msnfp_relatedimages";
                            CreateRecord(cloneRelatedImageUri, cloneRelatedImage);
                        });
                    }
                }

                //Created Campaign Actions’ 

                if (!isNullOrUndefined(cloneCampaignpagePageID)) {

                    var caQuery = "msnfp_campaignpageactions?";
                    caQuery += "$select=msnfp_campaignpageactionid,_msnfp_campaignpageid_value,msnfp_identifier,msnfp_displaytitle,msnfp_customamount,msnfp_defaultamount,msnfp_display,msnfp_givingfrequencycode,msnfp_smallimage,msnfp_message,msnfp_minimumamount,msnfp_order,_msnfp_packageid_value";
                    caQuery += "&$filter=_msnfp_campaignpageid_value eq " + "'" + originRecordGuid + "'";
                    var caResult = ExecuteQuery(GetWebAPIUrl() + caQuery);

                    if (!isNullOrUndefined(caResult) && caResult.length > 0) {
                        $(caResult).each(function () {

                            var cloneCampaignAction = {};

                            /* Deprecated:
                             * if (!isNullOrUndefined(this.msnfp_amount)) {
                                cloneCampaignAction["msnfp_amount"] = this.msnfp_amount;
                            }*/

                            if (!isNullOrUndefined(this.msnfp_identifier)) {
                                // Make sure we have enough space for the words ' - Clone':
                                if (this.msnfp_identifier.length >= 142) {
                                    cloneCampaignAction["msnfp_identifier"] = this.msnfp_identifier;
                                }
                                else {
                                    cloneCampaignAction["msnfp_identifier"] = this.msnfp_identifier + " - Clone";
                                }
                            }

                            if (!isNullOrUndefined(this.msnfp_customamount)) {
                                cloneCampaignAction["msnfp_customamount"] = this.msnfp_customamount;
                            }
                            if (!isNullOrUndefined(this.msnfp_defaultamount)) {
                                cloneCampaignAction["msnfp_defaultamount"] = this.msnfp_defaultamount;
                            }
                            if (!isNullOrUndefined(this.msnfp_display)) {
                                cloneCampaignAction["msnfp_display"] = this.msnfp_display;
                            }

                            if (!isNullOrUndefined(this.msnfp_displaytitle)) {
                                cloneCampaignAction["msnfp_displaytitle"] = this.msnfp_displaytitle;
                            }

                            if (!isNullOrUndefined(this.msnfp_givingfrequencycode)) {
                                cloneCampaignAction["msnfp_givingfrequencycode"] = this.msnfp_givingfrequencycode;
                            }

                            if (!isNullOrUndefined(this.msnfp_smallimage)) {
                                cloneCampaignAction["msnfp_smallimage"] = this.msnfp_smallimage;
                            }

                            if (!isNullOrUndefined(this.msnfp_message)) {
                                cloneCampaignAction["msnfp_message"] = this.msnfp_message;
                            }

                            if (!isNullOrUndefined(this.msnfp_minimumamount)) {
                                cloneCampaignAction["msnfp_minimumamount"] = this.msnfp_minimumamount;
                            }

                            if (!isNullOrUndefined(this.msnfp_order)) {
                                cloneCampaignAction["msnfp_order"] = this.msnfp_order;
                            }

                            cloneCampaignAction["msnfp_CampaignPageId@odata.bind"] = "/msnfp_campaignpages(" + cloneCampaignpagePageID + ")";

                            if (!isNullOrUndefined(this._msnfp_packageid_value)) {
                                cloneCampaignAction["msnfp_PackageId@odata.bind"] = "/msnfp_packages(" + this._msnfp_packageid_value + ")";
                            }

                            /* Deprecated:
                             * if (!isNullOrUndefined(this._msnfp_donationpageid_value)) {
                                cloneCampaignAction["msnfp_DonationPageId@odata.bind"] = "/msnfp_donationpages(" + this._msnfp_donationpageid_value + ")";
                            }*/

                            var cloneCampaignActionUri = GetWebAPIUrl() + "msnfp_campaignpageactions";
                            CreateRecord(cloneCampaignActionUri, cloneCampaignAction);
                        });
                    }
                }

                // Completed all actions, show this to the user so they do not click again:
                var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
                var alertOptions = { height: 120, width: 260 };
                Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                    function success(result) {
                        console.log("Alert dialog closed");
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );


            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + XmlHttpRequest.responseText);
        }
    });
}


function ShowCreatePledge() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;

    isMFRole = false;
    isMFMRole = false;
    isMSARole = false;
    isSARole = false;
    isSetasCommitted = false;
    isPledgeScheduleCreated = false;
    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    if (!isNullOrUndefined(currentuserID)) {
        isMFRole = CheckUserInRole("FundraisingandEngagement: Fundraiser", currentuserID);
        isMFMRole = CheckUserInRole("FundraisingandEngagement: Fundraiser Manager", currentuserID);
        isMSARole = CheckUserInRole("FundraisingandEngagement: System Administrator", currentuserID);
        isSARole = CheckUserInRole("System Administrator", currentuserID);
    }

    var potentialCampaign = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialcampaign").getValue();
    var estDate = parent.Xrm.Page.data.entity.attributes.get("msnfp_projectedawarddate").getValue();
    var fund = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialfund").getValue();

    var setasCommitted = parent.Xrm.Page.data.entity.attributes.get("msnfp_setascommitted");
    var pledgeScheduleCreated = parent.Xrm.Page.data.entity.attributes.get("msnfp_pledgeschedulecreated");

    if (!isNullOrUndefined(setasCommitted)) {
        isSetasCommitted = setasCommitted.getValue();
    }
    else {
        isSetasCommitted = false;
    }


    if (!isNullOrUndefined(pledgeScheduleCreated)) {
        isPledgeScheduleCreated = pledgeScheduleCreated.getValue();
    }
    else {
        isPledgeScheduleCreated = false;
    }

    if (formType === 2 && !isPledgeScheduleCreated && isSetasCommitted && !isNullOrUndefined(potentialCampaign) && !isNullOrUndefined(estDate) && !isNullOrUndefined(fund) && (isMFRole || isMFMRole || isMSARole || isSARole)) {
        return isVisible = true;
    }
    return isVisible;
}

function SetOpportunityAsCommitted() {
    try {
        var committed = parent.Xrm.Page.data.entity.attributes.get("msnfp_setcommitted");
        var committedDate = parent.Xrm.Page.data.entity.attributes.get("msnfp_setcommitteddate");


        committed.setValue(true);
        committedDate.setValue(new Date());

        parent.Xrm.Page.data.entity.save();
    }
    catch (ex) {
        console.log("SetOpportunityAsCommitted() ribbon button error: " + ex);
    }
}

function ShowSetCommittedOpportunity() {
    var setasCommitted = parent.Xrm.Page.data.entity.attributes.get("msnfp_setcommitted");
    console.log("ShowSetCommittedOpportunity()");
    isMFRole = false;
    isMFMRole = false;
    isMSARole = false;
    isSARole = false;
    isSetasCommitted = false;
    var formType = parent.Xrm.Page.ui.getFormType();

    if (!isNullOrUndefined(setasCommitted)) {
        isSetasCommitted = setasCommitted.getValue();
    }
    else {
        isSetasCommitted = false;
    }
    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    if (!isNullOrUndefined(currentuserID)) {
        isMFRole = CheckUserInRole("FundraisingandEngagement: Set Committed", currentuserID);
        isMFMRole = CheckUserInRole("FundraisingandEngagement: Fundraiser Manager", currentuserID);
        isMSARole = CheckUserInRole("FundraisingandEngagement: System Administrator", currentuserID);
        isSARole = CheckUserInRole("System Administrator", currentuserID);
    }

    if (formType === 2 && !isSetasCommitted && (isMFRole || isMFMRole || isMSARole || isSARole)) {
        console.log("isSetasCommitted = " + isSetasCommitted);
        return isVisible = true;
    }
    else {
        return false;
    }
}

function ShowCreatePledgeOptionsOnOpportunity() {
    var setasCommitted = parent.Xrm.Page.data.entity.attributes.get("msnfp_setcommitted");
    console.log("ShowCreatePledgeOptionsOnOpportunity()");
    isMFRole = false;
    isMFMRole = false;
    isMSARole = false;
    isSARole = false;
    isSetasCommitted = false;
    var formType = parent.Xrm.Page.ui.getFormType();

    if (!isNullOrUndefined(setasCommitted)) {
        isSetasCommitted = setasCommitted.getValue();
    }
    else {
        isSetasCommitted = false;
    }
    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    if (!isNullOrUndefined(currentuserID)) {
        isMFRole = CheckUserInRole("FundraisingandEngagement: Gift Administrator", currentuserID);
        isMFMRole = CheckUserInRole("FundraisingandEngagement: Generate Pledges", currentuserID);
        isMSARole = CheckUserInRole("FundraisingandEngagement: System Administrator", currentuserID);
        isSARole = CheckUserInRole("System Administrator", currentuserID);
    }

    if (formType === 2 && isSetasCommitted && (isMFRole || isMFMRole || isMSARole || isSARole)) {
        console.log("isSetasCommitted = " + isSetasCommitted);
        return isVisible = true;
    }
    else {
        return false;
    }
}


// Creates a new pledge on the Opportunity form
function CreatePledgeFromOpportunityDesignationPlans() {

    var currentID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();
    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var customer = parent.Xrm.Page.data.entity.attributes.get("customerid").getValue();
    var customerId = parent.Xrm.Page.data.entity.attributes.get("customerid").getValue()[0].id;
    var appeal = parent.Xrm.Page.data.entity.attributes.get("msnfp_appealid").getValue();
    var campaign = parent.Xrm.Page.data.entity.attributes.get("campaignid").getValue();
    var expectedAmount = parent.Xrm.Page.data.entity.attributes.get("msnfp_expectedamount").getValue();
    var bookDate = parent.Xrm.Page.data.entity.attributes.get("estimatedclosedate").getValue();
    var pledgeCreated = parent.Xrm.Page.data.entity.attributes.get("msnfp_pledgecreated");
    var solicator = parent.Xrm.Page.data.entity.attributes.get("msnfp_solicitorid");

    var configuration = null;

    if (bookDate === null || expectedAmount === null || campaign === null) {
        var confirmStrings = { text: "In order to create a donor commitment, please enter the Source Campaign, Est. Close Date and Expected Amount fields.", title: "Process Aborted" };
        var confirmOptions = { height: 200, width: 450 };

        parent.Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
            function (success) {
                if (success.confirmed)
                    console.log("Dialog closed using OK button.");
                else
                    console.log("Dialog closed using Cancel button or X.");
            });

        return;
    }

    if (pledgeCreated) {
        if (pledgeCreated.getValue()) {
            var alertStrings = { confirmButtonLabel: "Okay", text: "Donor Commitment has been created." };
            var alertOptions = { height: 120, width: 260 };
            Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                function success(result) { console.log("Alert dialog closed"); },
                function (error) { console.log(error.message); }
            );

            return;
        }


    }

    var pledge = {};

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentuserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);

    if (!isNullOrUndefined(user)) {
        user = user[0];
    }

    if (user._msnfp_configurationid_value != null) {
        configuration = user._msnfp_configurationid_value;
        console.log("configuration = " + configuration);
        //pledge["msnfp_ConfigurationId@odata.bind"] = "/configurations(" + XrmUtility.CleanGuid(configuration) + ")"; 
        console.log("1");
    }

    // Get the customer info: 
    if (customer != null) {
        if (customer[0].entityType == "account") {
            var selectAC = "accounts?$select=accountid,name,msnfp_anonymity,emailaddress1,telephone1,telephone2,address1_line1,address1_line2,address1_line3,address1_city,address1_country,address1_stateorprovince,address1_postalcode";
            selectAC += "&$filter=accountid eq " + XrmUtility.CleanGuid(customerId);
            var act = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectAC);

            if (act != null) {
                pledge["msnfp_anonymous"] = act[0].msnfp_anonymity;
                pledge["msnfp_billing_city"] = act[0].address1_city;
                pledge["msnfp_billing_country"] = act[0].address1_country;
                pledge["msnfp_billing_line1"] = act[0].address1_line1;
                pledge["msnfp_billing_line2"] = act[0].address1_line2;
                pledge["msnfp_billing_line3"] = act[0].address1_line3;
                pledge["msnfp_billing_postalcode"] = act[0].address1_postalcode;
                pledge["msnfp_billing_stateorprovince"] = act[0].address1_stateorprovince;
                // This does not exist: pledge["msnfp_organizationname"] = act[0].name;
                pledge["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + XrmUtility.CleanGuid(customerId) + ")";
                pledge["msnfp_telephone1"] = act[0].telephone1;
                if (act[0].telephone2 != null) {
                    pledge["msnfp_telephone2"] = act[0].telephone2;
                }
                if (act[0].emailaddress1 != null) {
                    pledge["msnfp_emailaddress1"] = act[0].emailaddress1;
                }
            }
        }
        else if (customer[0].entityType == "contact") {
            var selectC = "contacts?$select=contactid,firstname,lastname,msnfp_anonymity,emailaddress1,telephone1,telephone2,address1_line1,address1_line2,address1_line3,address1_city,address1_country,address1_stateorprovince,address1_postalcode";
            selectC += "&$filter=contactid eq " + XrmUtility.CleanGuid(customerId);
            var cntc = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectC);

            if (cntc != null) {
                pledge["msnfp_anonymous"] = cntc[0].msnfp_anonymity;
                pledge["msnfp_billing_city"] = cntc[0].address1_city;
                pledge["msnfp_billing_country"] = cntc[0].address1_country;
                pledge["msnfp_billing_line1"] = cntc[0].address1_line1;
                pledge["msnfp_billing_line2"] = cntc[0].address1_line2;
                pledge["msnfp_billing_line3"] = cntc[0].address1_line3;
                pledge["msnfp_billing_postalcode"] = cntc[0].address1_postalcode;
                pledge["msnfp_billing_stateorprovince"] = cntc[0].address1_stateorprovince;
                pledge["msnfp_firstname"] = cntc[0].firstname;
                pledge["msnfp_lastname"] = cntc[0].lastname;
                pledge["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + XrmUtility.CleanGuid(customerId) + ")";
                pledge["msnfp_telephone1"] = cntc[0].telephone1;
                if (cntc[0].telephone2 != null) {
                    pledge["msnfp_telephone2"] = cntc[0].telephone2;
                }
                if (cntc[0].emailaddress1 != null) {
                    pledge["msnfp_emailaddress1"] = cntc[0].emailaddress1;
                }
            }
        }
    }

    pledge["msnfp_totalamount"] = expectedAmount;

    if (appeal != null) {
        pledge["msnfp_AppealId@odata.bind"] = "/msnfp_appeals(" + XrmUtility.CleanGuid(appeal[0].id) + ")";
    }
    if (campaign != null) {
        pledge["msnfp_Commitment_CampaignId@odata.bind"] = "/campaigns(" + XrmUtility.CleanGuid(campaign[0].id) + ")";
    }
    if (bookDate != null) {
        pledge["msnfp_bookdate"] = bookDate;
    }

    // Could not find: pledge["msnfp_dataentrysource"] = parent.Xrm.Page.data.entity.attributes.get("msnfp_scheduleamount").getValue();

    if (parent.Xrm.Page.data.entity.attributes.get("msnfp_packageid").getValue() != null) {
        pledge["msnfp_PackageId@odata.bind"] = "/msnfp_packages(" + XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_packageid").getValue()[0].id) + ")";
    }

    pledge["msnfp_opportunityid@odata.bind"] = "/opportunities(" + XrmUtility.CleanGuid(currentID) + ")";

    if (parent.Xrm.Page.data.entity.attributes.get("msnfp_opportunity_defaultdesignationid").getValue() != null) {
        pledge["msnfp_Commitment_DefaultDesignationId@odata.bind"] = "/msnfp_designations(" + XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_opportunity_defaultdesignationid").getValue()[0].id) + ")";
    }

    if (solicator !== null && solicator.getValue()) {
        var solicitorValue = solicator.getValue()[0];

        if (solicitorValue.entityType === "contact") {
            pledge["msnfp_SolicitorId_contact@odata.bind"] = "/contacts(" + XrmUtility.CleanGuid(solicitorValue.id) + ")";
        }
        else if (solicitorValue.entityType === "account") {
            pledge["msnfp_SolicitorId_account@odata.bind"] = "/accounts(" + XrmUtility.CleanGuid(solicitorValue.id) + ")";
        }
    }

    //pledge["statuscode"] = 844060000;
    pledge["msnfp_totalamount_paid"] = 0;
    pledge["msnfp_totalamount_balance"] = expectedAmount;

    var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donorcommitments";
    var recordid = XrmServiceUtility.CreateRecord(qry, pledge);

    if (recordid != null) {
        console.log("Created pledge recordid = " + recordid);

        var currentRecord = {};
        currentRecord["msnfp_pledgecreated"] = true;

        var qry = XrmServiceUtility.GetWebAPIUrl() + "opportunities(" + currentID + ")";
        XrmServiceUtility.UpdateRecord(qry, currentRecord);

        parent.Xrm.Page.getAttribute("msnfp_pledgecreated").setValue(true);

        // Now update all the designation plans linked to this opportunity:
        var designationSelect = "msnfp_designationplans?$select=msnfp_designationplanid,_msnfp_designationplan_opportunityid_value";
        designationSelect += "&$filter=(_msnfp_designationplan_opportunityid_value eq " + currentID + ")";

        var designationPlans = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + designationSelect);
        if (designationPlans != null) {
            for (var i = 0; i < designationPlans.length; i++) {
                // Update the designation plan with the new donor commitment:
                var recordToUpdate = {};
                recordToUpdate["msnfp_DesignationPlan_DonorCommitmentId@odata.bind"] = "/msnfp_donorcommitments(" + recordid + ")";
                var qryToUpdate = XrmServiceUtility.GetWebAPIUrl() + "msnfp_designationplans(" + designationPlans[i].msnfp_designationplanid + ")";
                XrmServiceUtility.UpdateRecord(qryToUpdate, recordToUpdate);
                console.log("Updated DP: " + designationPlans[i].msnfp_designationplanid);
            }
        }

        var confirmStrings = { text: "Pledge record has been created successfully.", title: "Pledge Created Successfully" };
        var confirmOptions = { height: 200, width: 450 };
        parent.Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
            function (success) {
                if (success.confirmed)
                    console.log("Dialog closed using OK button.");
                else
                    console.log("Dialog closed using Cancel button or X.");
            });
    }
    else {
        console.log("An error occured while saving the pledge.");
    }
}


function ShowSetAsDebitSelection() {
    try {
        var debitselection = parent.Xrm.Page.data.entity.attributes.get("msnfp_debitselection");

        if (debitselection.getValue()) {
            return false;
        }
        else {
            return true;
        }

    }
    catch (ex) {
        console.log("ShowSetAsDebitSelection() ribbon button error: " + ex);
    }
}

function SetAsDebitSelection() {
    try {
        var debitselection = parent.Xrm.Page.data.entity.attributes.get("msnfp_debitselection");
        debitselection.setValue(true);
        parent.Xrm.Page.data.entity.save();
    }
    catch (ex) {
        console.log("SetAsDebitSelection() ribbon button error: " + ex);
    }
}


function RemoveAsDebitSelection() {
    try {
        var debitselection = parent.Xrm.Page.data.entity.attributes.get("msnfp_debitselection");
        debitselection.setValue(false);
        parent.Xrm.Page.data.entity.save();
    }
    catch (ex) {
        console.log("SetAsDebitSelection() ribbon button error: " + ex);
    }
}

function ShowRemoveAsDebitSelection() {
    try {
        var debitselection = parent.Xrm.Page.data.entity.attributes.get("msnfp_debitselection");

        if (debitselection.getValue()) {
            return true;
        }
        else {
            return false;
        }

    }
    catch (ex) {
        console.log("ShowSetAsDebitSelection() ribbon button error: " + ex);
    }
}

// TODO:
function CreatePledgeScheduleFromOpportunityDesignationPlans() {

}



//to generate Pledge Allocation for Pledge Schedule
function CreatePledge() {

    var scheduleAmount = parent.Xrm.Page.data.entity.attributes.get("msnfp_scheduleamount").getValue();
    var scheduleStart = parent.Xrm.Page.data.entity.attributes.get("msnfp_schedulestart").getValue();
    var instance = parent.Xrm.Page.data.entity.attributes.get("msnfp_schedulerecurrence").getValue();
    var recurrenceType = parent.Xrm.Page.data.entity.attributes.get("msnfp_schedulerecurrencetype").getValue();


    var currentID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();
    var selectLead = "leads?$select=leadid,msnfp_scheduleamount,msnfp_schedulestart,msnfp_schedulerecurrence,msnfp_schedulerecurrencetype,msnfp_fundingtype,msnfp_pledgeschedulecreated";
    var filterLead = "&$filter=leadid eq " + currentID;
    var resultLead = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectLead + filterLead);


    if (!isNullOrUndefined(resultLead)) {
        if (!resultLead[0].msnfp_pledgeschedulecreated) {
            resultLead = resultLead[0];

            var select = "msnfp_paymentschedules?$select=msnfp_paymentscheduleid,msnfp_title,msnfp_amount,msnfp_date,msnfp_financialinkind,_msnfp_fundid_value,_msnfp_leadid_value,msnfp_paymentdescription,msnfp_reporting,msnfp_reportingdescription,statuscode&$orderby=msnfp_date";
            var filter = "&$filter=(_msnfp_leadid_value eq " + currentID + ") and statuscode eq 1";
            var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);

            if (isNullOrUndefined(result)) {
                Xrm.Utility.alertDialog("Complete all required payment schedules and details before creating a related pledge");
                return false;
            }
            else {
                if (!isNullOrUndefined(scheduleAmount) && !isNullOrUndefined(scheduleStart) && !isNullOrUndefined(instance) && !isNullOrUndefined(recurrenceType)) {
                    var entity = {};

                    entity["msnfp_type"] = 84406003;//Pledge Schedule
                    entity["msnfp_gifttype"] = 844060000;//Cash
                    entity["msnfp_occurrencetype"] = 84406001;//Recurring
                    //entity["msnfp_startondate"] = scheduleStart;

                    var str = getFormattedDate(scheduleStart);
                    //str = str.split('/');
                    //entity["msnfp_date"] = str[2] + '-' + str[0] + '-' + str[1];
                    //entity["msnfp_startondate"] = str[2] + '-' + str[0] + '-' + str[1];

                    entity["msnfp_date"] = str;
                    entity["msnfp_startondate"] = str;

                    entity["msnfp_recurrenceinstance"] = instance;
                    entity["msnfp_recurrencetype"] = recurrenceType;
                    entity["msnfp_amount"] = scheduleAmount;

                    var account = parent.Xrm.Page.data.entity.attributes.get("parentaccountid").getValue();
                    var accountID = "";

                    var customer;
                    if (!isNullOrUndefined(account)) {
                        accountID = parent.Xrm.Page.data.entity.attributes.get("parentaccountid").getValue()[0].id;
                        entity["msnfp_customerid_account@odata.bind"] = "/accounts(" + XrmUtility.CleanGuid(accountID) + ")";

                        var selectAC = "accounts?$select=accountid,name,emailaddress1,telephone1,address1_line1,address1_line2,address1_line3,address1_city,address1_stateorprovince,address1_postalcode";
                        selectAC += "&$filter=accountid eq " + XrmUtility.CleanGuid(accountID);
                        var act = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectAC);

                        entity["msnfp_organizationname"] = act[0].name;
                        customer = act[0];
                    }

                    var contact = parent.Xrm.Page.data.entity.attributes.get("parentcontactid").getValue();
                    var contactID = "";

                    if (!isNullOrUndefined(contact)) {
                        contactID = parent.Xrm.Page.data.entity.attributes.get("parentcontactid").getValue()[0].id;
                        entity["msnfp_customerid_contact@odata.bind"] = "/contacts(" + XrmUtility.CleanGuid(contactID) + ")";

                        var selectC = "contacts?$select=contactid,firstname,lastname,emailaddress1,telephone1,address1_line1,address1_line2,address1_line3,address1_city,address1_stateorprovince,address1_postalcode";
                        selectC += "&$filter=contactid eq " + XrmUtility.CleanGuid(contactID);
                        var cntc = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectC);

                        entity["msnfp_firstname"] = cntc[0].firstname;
                        entity["msnfp_lastname"] = cntc[0].lastname;
                        customer = cntc[0];
                    }

                    entity["msnfp_emailaddress1"] = customer.emailaddress1;
                    entity["msnfp_telephone1"] = customer.telephone1;
                    entity["msnfp_billing_line1"] = customer.address1_line1;
                    entity["msnfp_billing_line2"] = customer.address1_line2;
                    entity["msnfp_billing_line3"] = customer.address1_line3;
                    entity["msnfp_billing_city"] = customer.address1_city;
                    entity["msnfp_billing_stateorprovince"] = customer.address1_stateorprovince;
                    entity["msnfp_billing_postalcode"] = customer.address1_postalcode;
                    entity["statuscode"] = parseInt(1);

                    entity["msnfp_OriginatingProspect@odata.bind"] = "/leads(" + currentID + ")";


                    var potentialCampaign = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialcampaign").getValue();
                    var potentialCampaignID;
                    if (!isNullOrUndefined(potentialCampaign)) {
                        potentialCampaignID = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialcampaign").getValue()[0].id;
                        entity["msnfp_campaigneventid@odata.bind"] = "/campaigns(" + XrmUtility.CleanGuid(potentialCampaignID) + ")";
                    }


                    var potentialFund = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialfund").getValue();
                    var potentialFundID;
                    if (!isNullOrUndefined(potentialFund)) {
                        potentialFundID = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialfund").getValue()[0].id;
                        entity["msnfp_Fund@odata.bind"] = "/msnfp_funds(" + XrmUtility.CleanGuid(potentialFundID) + ")";
                    }

                    var potentialAppeal = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialappealid").getValue();
                    var potentialAppealId;
                    if (!isNullOrUndefined(potentialAppeal)) {
                        potentialAppealId = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialappealid").getValue()[0].id;
                        entity["msnfp_appealid@odata.bind"] = "/msnfp_appeals(" + XrmUtility.CleanGuid(potentialAppealId) + ")";
                    }

                    var potentialPackage = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialpackage").getValue();
                    var potentialPackageID;
                    if (!isNullOrUndefined(potentialPackage)) {
                        potentialPackageID = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialpackage").getValue()[0].id;
                        entity["msnfp_packageid@odata.bind"] = "/msnfp_packages(" + XrmUtility.CleanGuid(potentialPackageID) + ")";
                    }


                    var query = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpledges";

                    var donationPledgeID = XrmServiceUtility.CreateRecord(query, entity);

                    if (!isNullOrUndefined(donationPledgeID)) {
                        $(result).each(function (index) {
                            var entity = {};
                            entity["msnfp_title"] = "New Pledge Allocation " + (index + 1);
                            entity["msnfp_amount"] = this.msnfp_amount;
                            //entity["msnfp_amountpaid"] = 0;
                            entity["msnfp_donationpledgeid@odata.bind"] = "/msnfp_donationpledges(" + donationPledgeID + ")";
                            entity["statuscode"] = 1; //In Progress

                            var date = new Date(this.msnfp_date);

                            date = date.getMonth() + 1 + '/' + date.getDate() + '/' + date.getFullYear();

                            date = new Date(date);
                            entity["msnfp_date"] = date.getFullYear() + '-' + (date.getMonth() + 1) + '-' + date.getDate();
                            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_pledgeallocations";
                            XrmServiceUtility.CreateRecord(qry, entity);
                        });

                        var selectDP = "msnfp_donationpledges?$select=msnfp_donationpledgeid,msnfp_title";
                        selectDP += "&$filter=msnfp_donationpledgeid eq " + donationPledgeID;
                        var donationPledge = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectDP);

                        var relatedGiftID = donationPledge[0].msnfp_donationpledgeid;
                        var relatedGiftName = donationPledge[0].msnfp_title;
                        var relatedGiftType = "msnfp_donationpledge";

                        var leadEntity = {};
                        leadEntity["msnfp_RelatedGift@odata.bind"] = "/msnfp_donationpledges(" + relatedGiftID + ")";
                        leadEntity["msnfp_pledgeschedulecreated"] = true;
                        var qryLead = XrmServiceUtility.GetWebAPIUrl() + "leads(" + currentID + ")";
                        XrmServiceUtility.UpdateRecord(qryLead, leadEntity);

                        var options = { openInNewWindow: true };
                        var parameters = {};
                        Xrm.Utility.openEntityForm("msnfp_donationpledge", donationPledgeID, parameters, options);
                    }
                }
            }
        }
        else {
            alert("A pledge schedule has been or is being created");
        }
    }
}

function CheckUserInRole(roleName, userId) {
    console.log("---------Entered CheckUserInRole-----------");

    console.log("Check for " + roleName)

    var roleFound = false;

    var context = XrmUtility.get_Xrm();

    if (context !== undefined && context !== null) {
        var roles = context.Utility.getGlobalContext().userSettings.roles;
        if (roles !== undefined || roles !== null) {
            $.each(roles._collection, function () {
                console.log("Comparing to " + this.name)

                roleFound = this.name.toLowerCase() === roleName.toLowerCase();

                console.log("Match found " + roleFound);

                if (roleFound) {
                    return false;
                }
            });
        }
    }

    console.log("---------Exiting CheckUserInRole returning " + roleFound);
    return roleFound;
}

function ShowSetCommitted() {

    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;

    isMFRole = false;
    isMFMRole = false;
    isMSARole = false;
    isSARole = false;
    isSetasCommitted = false;


    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var setasCommitted = parent.Xrm.Page.data.entity.attributes.get("msnfp_setascommitted");

    if (!isNullOrUndefined(setasCommitted)) {

        isSetasCommitted = setasCommitted.getValue();
    }
    else {
        isSetasCommitted = false;

    }

    if (!isNullOrUndefined(currentuserID)) {
        isMFRole = CheckUserInRole("FundraisingandEngagement: Fundraiser", currentuserID);
        isMFMRole = CheckUserInRole("FundraisingandEngagement: Fundraiser Manager", currentuserID);
        isMSARole = CheckUserInRole("FundraisingandEngagement: System Administrator", currentuserID);
        isSARole = CheckUserInRole("System Administrator", currentuserID);
    }

    if (formType === 2 && !isSetasCommitted && (isMFRole || isMFMRole || isMSARole || isSARole)) {
        return isVisible = true;
    }
    return isVisible;
}

function SetCommitted() {
    var estimatedAmount = parent.Xrm.Page.data.entity.attributes.get("estimatedamount").getValue();
    var projectedAwardDate = parent.Xrm.Page.data.entity.attributes.get("msnfp_projectedawarddate").getValue();
    var potentialCampaignId = parent.Xrm.Page.data.entity.attributes.get("msnfp_potentialcampaign").getValue();

    if (!isNullOrUndefined(estimatedAmount) && !isNullOrUndefined(projectedAwardDate) && !isNullOrUndefined(potentialCampaignId)) {
        var setasCommitted = parent.Xrm.Page.data.entity.attributes.get("msnfp_setascommitted");
        setasCommitted.setValue(true);
        parent.Xrm.Page.data.entity.save();
        parent.Xrm.Page.ui.clearFormNotification("strCommitted");
    }
    else {
        parent.Xrm.Page.ui.setFormNotification("Please complete all opportunity details, financial details, expected date information and any interests and restrictions if this opportunity is restricted.", "WARNING", "strCommitted");
    }
}


function hideUnNecessaryButton() {
    var isVisible = false;

    return isVisible;
}


function ShowOpportunityAssessmentForm() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    if (formType === 2) {
        return isVisible = true;
    }
    return isVisible;
}

function OpportunityAssessmentForm() {
    DownloadOpportunityAssessmentForm("Microsoft.Dynamics.CRM.msnfp_ActionOpportunityAssessmentForm");
}

function DownloadOpportunityAssessmentForm(requestName) {

    var entityId = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.getId());

    //var entityId = parent.Xrm.Page.data.entity.getId().replace(/\{|\}/gi, '');

    try {
        var entityName = "lead";

        var entity = {};

        var organizationUrl = parent.Xrm.Page.context.getClientUrl();
        var data = {};
        var query = "leads(" + entityId + ")/" + requestName;
        var req = new XMLHttpRequest();
        req.open("POST", organizationUrl + "/api/data/v8.0/" + query, true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-Version", "4.0");
        req.onreadystatechange = function () {
            if (this.readyState === 4) {
                req.onreadystatechange = null;
                if (this.status === 204) {
                    select = "annotations?$select=documentbody,createdon,subject,filename,notetext,_objectid_value,annotationid&$orderby=createdon desc&$top=1&$filter=(_objectid_value eq " + entityId + ")";
                    var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);
                    if (result !== null && result.length > 0) {
                        download(base64ToArrayBuffer(result[0].documentbody), result[0].filename, "application/pdf");
                        XrmServiceUtility.DeleteRecord(result[0].annotationid, 'annotations');
                        parent.Xrm.Page.data.entity.attributes.get("msnfp_isopportunityassessmentformcreated").setValue(1);
                        parent.Xrm.Page.data.entity.save();
                        parent.Xrm.Page.data.refresh(true);
                    }
                }
            }
        };
        req.send(window.JSON.stringify(data));
    }
    catch (error) {
        //errorHandler(error);
    }
}

//BatchGift Entity form
function HideBatchGiftButtons() {
    var formType = parent.Xrm.Page.ui.getFormType();
    var isVisible = false;
    if (formType === 2) {
        return isVisible = true;
    }
    return isVisible;
}

function RecalculateBatchGiftRecrod() {
    // Changes made to this function should also be carried over to gift batch on load onLoadValidateCounts
    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var dpQuery = "msnfp_transactions?$select=msnfp_transactionid,msnfp_amount_receipted,msnfp_amount_nonreceiptable,msnfp_amount_membership,msnfp_amount_tax,_msnfp_giftbatchid_value";
    var filter = "&$filter=_msnfp_giftbatchid_value eq " + originRecordGuid + "";

    var dpResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + dpQuery + filter);

    var currentNoOfGifts = 0;
    var totalGiftMembershipAmount = 0;
    var totalReceiptableAmount = 0;
    var totalNonReceiptableAmount = 0;
    var totalTaxAmount = 0;

    if (!isNullOrUndefined(dpResult)) {
        $(dpResult).each(function () {

            totalGiftMembershipAmount += this.msnfp_amount_membership;
            totalReceiptableAmount += this.msnfp_amount_receipted;
            totalNonReceiptableAmount += this.msnfp_amount_nonreceiptable;
            totalTaxAmount += this.msnfp_amount_tax;

        });

        currentNoOfGifts = dpResult.length;
        var currentAmountTotal = parseFloat(totalReceiptableAmount + totalGiftMembershipAmount + totalNonReceiptableAmount + totalTaxAmount);
        var currentAmtDuesTotal = parseFloat(totalGiftMembershipAmount);
        var currentAmtReceiptableTotal = parseFloat(totalReceiptableAmount);
        var currentAmtNonReceiptableTotal = parseFloat(totalNonReceiptableAmount);

        // The total amount membership included:
        var currentAmount = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount");//("msnfp_currentamttotal");
        currentAmount.setValue(currentAmountTotal);

        // Receiptable amount:
        var currentamtreceiptable = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_receipted");
        currentamtreceiptable.setValue(currentAmtReceiptableTotal);

        // Non receiptable amount:
        var currentamtnonreceiptable = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_nonreceiptable");
        currentamtnonreceiptable.setValue(currentAmtNonReceiptableTotal);

        // Membership Dues:
        var currentamtdues = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_membership");//("msnfp_currentamtdues");
        currentamtdues.setValue(currentAmtDuesTotal);

        // Taxes Dues:
        var currentamtTaxes = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_tax");//("msnfp_currentamtdues");
        currentamtTaxes.setValue(totalTaxAmount);

        // Number of gifts:
        var currentNumberOfGifts = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_gifts");//("msnfp_currentnoofgifts");
        currentNumberOfGifts.setValue(currentNoOfGifts);
        parent.Xrm.Page.data.entity.save();
    }
}

// Sets everything to completed:
function CloseBatchGiftButtons() {
    //var status = parent.Xrm.Page.data.entity.attributes.get("statuscode");
    //status.setValue(1);//Completed
    //Xrm.Page.data.entity.save();

    if (validateGiftBatchTotals()) {

        var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

        if (!isNullOrUndefined(originRecordGuid)) {
            var gb = {};
            gb["statuscode"] = 844060000;
            var qry = GetWebAPIUrl() + "msnfp_giftbatchs(" + originRecordGuid + ")";
            UpdateRecord(qry, gb);

            var dpQuery = "msnfp_transactions?$select=msnfp_transactionid,_msnfp_giftbatchid_value";
            var filter = "&$filter=_msnfp_giftbatchid_value eq " + originRecordGuid + "";

            var dpResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + dpQuery + filter);

            if (!isNullOrUndefined(dpResult)) {
                $(dpResult).each(function () {

                    var dp = {};
                    dp["statuscode"] = 844060000;
                    var dpqry = GetWebAPIUrl() + "msnfp_transactions(" + this.msnfp_transactionid + ")";
                    UpdateRecord(dpqry, dp);
                });

                // NFP CHANGE:
                var parentTransactionId = parent.Xrm.Page.data.entity.attributes.get("msnfp_parenttransactionid").getValue();
                if (!isNullOrUndefined(parentTransactionId)) {
                    var parentTransaction = {};
                    parentTransaction["statuscode"] = 844060000;
                    var parentqry = GetWebAPIUrl() + "msnfp_transactions(" + XrmUtility.CleanGuid(parentTransactionId[0].id) + ")";
                    UpdateRecord(parentqry, parentTransaction);
                }
            }

            parent.Xrm.Page.data.refresh(true);
        }
    }
}


function validateGiftBatchTotals() {

    var valPassed = true;

    // clearing notifications
    parent.Xrm.Page.ui.clearFormNotification("CurrentAmountTotal");
    parent.Xrm.Page.ui.clearFormNotification("AmountTotal");
    parent.Xrm.Page.ui.clearFormNotification("AmountGift");
    parent.Xrm.Page.ui.clearFormNotification("AmountNonreceiptable");
    parent.Xrm.Page.ui.clearFormNotification("AmountDues");
    parent.Xrm.Page.ui.clearFormNotification("GiftTotal");


    //msnfp_tally_amount and msnfp_amount
    var currentamttotal = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount").getValue();
    var paymentamttotal = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount").getValue();

    if (isNullOrUndefined(paymentamttotal) && isNullOrUndefined(currentamttotal)) {
        parent.Xrm.Page.ui.setFormNotification("Gift Batch cannot be closed - current total amount cannot be blank", "WARNING", "CurrentAmountTotal");
        return false;
    }
    else if (paymentamttotal > 0) {
        if (isNullOrUndefined(currentamttotal) || (!isNullOrUndefined(currentamttotal) && (paymentamttotal != currentamttotal))) {
            parent.Xrm.Page.ui.setFormNotification("Gift Batch cannot be closed - the expected total is not equal current total amount", "WARNING", "AmountTotal");
            valPassed = false;
        }
    }

    //msnfp_tally_amount_receipted and msnfp_amount_receipted    
    var currentamt = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_receipted").getValue();
    var expectedamt = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount_receipted").getValue();

    if (expectedamt > 0) {
        if (isNullOrUndefined(currentamt) || (!isNullOrUndefined(currentamt) && (expectedamt != currentamt))) {
            parent.Xrm.Page.ui.setFormNotification("Gift Batch cannot be closed - the expected gift is not equal current gift amount", "WARNING", "AmountGift");
            valPassed = false;
        }
    }


    //msnfp_tally_amount_nonreceiptable and msnfp_amount_nonreceiptable
    var currentnonreceiptableamt = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_nonreceiptable").getValue();
    var expectenonreceiptabledamt = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount_nonreceiptable").getValue();

    if (expectenonreceiptabledamt > 0) {
        if (isNullOrUndefined(currentnonreceiptableamt) || (!isNullOrUndefined(currentnonreceiptableamt) && (expectenonreceiptabledamt != currentnonreceiptableamt))) {
            parent.Xrm.Page.ui.setFormNotification("Gift Batch cannot be closed - the expected nonreceiptable is not equal current nonreceiptable amount", "WARNING", "AmountNonreceiptable");
            valPassed = false;
        }
    }

    //msnfp_tally_amount_membership and msnfp_amount_membership
    var currentAmtDues = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_amount_membership").getValue();
    var paymentAmtDues = parent.Xrm.Page.data.entity.attributes.get("msnfp_amount_membership").getValue();

    if (paymentAmtDues > 0) {
        if (isNullOrUndefined(currentAmtDues) || (!isNullOrUndefined(currentAmtDues) && (paymentAmtDues != currentAmtDues))) {
            parent.Xrm.Page.ui.setFormNotification("Gift Batch cannot be closed - the expected dues is not equal current dues amount", "WARNING", "AmountDues");
            valPassed = false;
        }
    }

    //msnfp_tally_gifts and msnfp_paymentnoofgifts
    var currentNoOfGift = parent.Xrm.Page.data.entity.attributes.get("msnfp_tally_gifts").getValue();
    var paymentNoOfGift = parent.Xrm.Page.data.entity.attributes.get("msnfp_paymentnoofgifts").getValue();

    if (paymentNoOfGift > 0) {
        if (isNullOrUndefined(currentNoOfGift) || (!isNullOrUndefined(currentNoOfGift) && (paymentNoOfGift != currentNoOfGift))) {
            parent.Xrm.Page.ui.setFormNotification("Gift Batch cannot be closed - payment number of gifts is not equal current number of gifts", "WARNING", "GiftTotal");
            valPassed = false;
        }
    }

    return valPassed;
}



//On Donation Page entity Ribbon
function HideCloneDonationPageButton() {

    var formType = parent.Xrm.Page.ui.getFormType();

    var isVisible = false;
    if (formType === 2) {
        return isVisible = true;
    }
    return isVisible;
}


// NOTE: Donation Page is not currently in the solution.
function CloneDonationPage() {

    var cloneData = {};
    var originRecordGuid = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '').toUpperCase();

    var select = "msnfp_donationpageid,msnfp_anonymous,_msnfp_appealid_value,_msnfp_campaignid_value,_msnfp_configurationid_value,msnfp_covercostamount,msnfp_covercostpercentage,msnfp_customamount,msnfp_defaultamount,_msnfp_eventid_value,msnfp_externalurl,";
    select += "msnfp_forceredirecturl,msnfp_forceredirecttiming,msnfp_friendlyurl,msnfp_fullpage,msnfp_givingfrequencycode,msnfp_headermessage,msnfp_identifier,msnfp_labellanguagecode,msnfp_largeimage,msnfp_lastpublished,";
    select += "msnfp_madevisible,msnfp_message,msnfp_minimumamount,_msnfp_packageid_value,msnfp_paymentnotice,_msnfp_paymentprocessorid_value,msnfp_recurrencestartcode,msnfp_removed,msnfp_removedon,msnfp_selectcurrency,";
    select += "msnfp_setacceptnotice,msnfp_setanonymous,msnfp_setcovercosts,msnfp_setsignup,msnfp_showanonymous,msnfp_showapple,msnfp_showcompany,msnfp_showcovercosts,msnfp_showcreditcard,msnfp_showgiftaid,msnfp_showgoogle,";
    select += "msnfp_showinhonor,msnfp_showmatchdonation,msnfp_showmembership,msnfp_smallimage,_msnfp_teamownerid_value,_msnfp_termsofreferenceid_value,msnfp_thankyou,msnfp_visible,msnfp_whydonatemonthly,_ownerid_value,statuscode,statecode,";
    select += "_msnfp_designationid_value,msnfp_enabledesignationselection,msnfp_designationselectionlabel";

    var oDataUri = GetWebAPIUrl() +
        "msnfp_donationpages?$select=" + select + "&$filter=msnfp_donationpageid eq " + originRecordGuid + "";

    console.debug(oDataUri = GetWebAPIUrl() +
        "msnfp_donationpages?$select=" + select + "&$filter=msnfp_donationpageid eq " + originRecordGuid + "");

    jQuery.support.cors = true;
    jQuery.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        url: oDataUri,
        async: false, //Synchronous operation 
        beforeSend: function (XMLHttpRequest) {
            //Specifying this header ensures that the results will be returned as JSON.           
            XMLHttpRequest.setRequestHeader("Accept", "application/json");
        },
        success: function (data, textStatus, XmlHttpRequest) {
            if (data !== null && data !== undefined) {
                cloneData["statuscode"] = data.value[0].statuscode;
                cloneData["statecode"] = data.value[0].statecode;

                // binding NOT lookup fields
                if (!isNullOrUndefined(data.value[0].msnfp_anonymous))
                    cloneData["msnfp_anonymous"] = data.value[0].msnfp_anonymous;

                if (!isNullOrUndefined(data.value[0].msnfp_covercostamount))
                    cloneData["msnfp_covercostamount"] = data.value[0].msnfp_covercostamount;

                if (!isNullOrUndefined(data.value[0].msnfp_covercostpercentage))
                    cloneData["msnfp_covercostpercentage"] = data.value[0].msnfp_covercostpercentage;

                if (!isNullOrUndefined(data.value[0].msnfp_customamount))
                    cloneData["msnfp_customamount"] = data.value[0].msnfp_customamount;

                if (!isNullOrUndefined(data.value[0].msnfp_defaultamount))
                    cloneData["msnfp_defaultamount"] = data.value[0].msnfp_defaultamount;


                if (!isNullOrUndefined(data.value[0].msnfp_forceredirecturl))
                    cloneData["msnfp_forceredirecturl"] = data.value[0].msnfp_forceredirecturl;

                if (!isNullOrUndefined(data.value[0].msnfp_forceredirecttiming))
                    cloneData["msnfp_forceredirecttiming"] = data.value[0].msnfp_forceredirecttiming;

                if (!isNullOrUndefined(data.value[0].msnfp_friendlyurl))
                    cloneData["msnfp_friendlyurl"] = data.value[0].msnfp_friendlyurl;

                if (!isNullOrUndefined(data.value[0].msnfp_fullpage))
                    cloneData["msnfp_fullpage"] = data.value[0].msnfp_fullpage;

                if (!isNullOrUndefined(data.value[0].msnfp_givingfrequencycode))
                    cloneData["msnfp_givingfrequencycode"] = data.value[0].msnfp_givingfrequencycode;

                if (!isNullOrUndefined(data.value[0].msnfp_headermessage))
                    cloneData["msnfp_headermessage"] = data.value[0].msnfp_headermessage;

                if (!isNullOrUndefined(data.value[0].msnfp_identifier)) {
                    // Make sure we have enough space for the words ' - Clone':
                    if (data.value[0].msnfp_identifier.length >= 142)
                        cloneData["msnfp_identifier"] = data.value[0].msnfp_identifier;
                    else
                        cloneData["msnfp_identifier"] = data.value[0].msnfp_identifier + " - Clone";
                }

                if (!isNullOrUndefined(data.value[0].msnfp_labellanguagecode))
                    cloneData["msnfp_labellanguagecode"] = data.value[0].msnfp_labellanguagecode;

                if (!isNullOrUndefined(data.value[0].msnfp_largeimage))
                    cloneData["msnfp_largeimage"] = data.value[0].msnfp_largeimage;

                if (!isNullOrUndefined(data.value[0].msnfp_lastpublished))
                    cloneData["msnfp_lastpublished"] = data.value[0].msnfp_lastpublished;

                if (!isNullOrUndefined(data.value[0].msnfp_madevisible))
                    cloneData["msnfp_madevisible"] = data.value[0].msnfp_madevisible;

                if (!isNullOrUndefined(data.value[0].msnfp_message))
                    cloneData["msnfp_message"] = data.value[0].msnfp_message;

                if (!isNullOrUndefined(data.value[0].msnfp_minimumamount))
                    cloneData["msnfp_minimumamount"] = data.value[0].msnfp_minimumamount;

                if (!isNullOrUndefined(data.value[0].msnfp_paymentnotice))
                    cloneData["msnfp_paymentnotice"] = data.value[0].msnfp_paymentnotice;

                if (!isNullOrUndefined(data.value[0].msnfp_recurrencestartcode))
                    cloneData["msnfp_recurrencestartcode"] = data.value[0].msnfp_recurrencestartcode;

                if (!isNullOrUndefined(data.value[0].msnfp_removed))
                    cloneData["msnfp_removed"] = data.value[0].msnfp_removed;

                if (!isNullOrUndefined(data.value[0].msnfp_removedon))
                    cloneData["msnfp_removedon"] = data.value[0].msnfp_removedon;

                if (!isNullOrUndefined(data.value[0].msnfp_selectcurrency))
                    cloneData["msnfp_selectcurrency"] = data.value[0].msnfp_selectcurrency;

                if (!isNullOrUndefined(data.value[0].msnfp_setacceptnotice))
                    cloneData["msnfp_setacceptnotice"] = data.value[0].msnfp_setacceptnotice;

                if (!isNullOrUndefined(data.value[0].msnfp_setanonymous))
                    cloneData["msnfp_setanonymous"] = data.value[0].msnfp_setanonymous;

                if (!isNullOrUndefined(data.value[0].msnfp_setcovercosts))
                    cloneData["msnfp_setcovercosts"] = data.value[0].msnfp_setcovercosts;

                if (!isNullOrUndefined(data.value[0].msnfp_setsignup))
                    cloneData["msnfp_setsignup"] = data.value[0].msnfp_setsignup;

                if (!isNullOrUndefined(data.value[0].msnfp_showanonymous))
                    cloneData["msnfp_showanonymous"] = data.value[0].msnfp_showanonymous;

                if (!isNullOrUndefined(data.value[0].msnfp_showapple))
                    cloneData["msnfp_showapple"] = data.value[0].msnfp_showapple;

                if (!isNullOrUndefined(data.value[0].msnfp_showcompany))
                    cloneData["msnfp_showcompany"] = data.value[0].msnfp_showcompany;

                if (!isNullOrUndefined(data.value[0].msnfp_showcovercosts))
                    cloneData["msnfp_showcovercosts"] = data.value[0].msnfp_showcovercosts;

                if (!isNullOrUndefined(data.value[0].msnfp_showcreditcard))
                    cloneData["msnfp_showcreditcard"] = data.value[0].msnfp_showcreditcard;

                if (!isNullOrUndefined(data.value[0].msnfp_showgiftaid))
                    cloneData["msnfp_showgiftaid"] = data.value[0].msnfp_showgiftaid;

                if (!isNullOrUndefined(data.value[0].msnfp_showgoogle))
                    cloneData["msnfp_showgoogle"] = data.value[0].msnfp_showgoogle;

                if (!isNullOrUndefined(data.value[0].msnfp_showinhonor))
                    cloneData["msnfp_showinhonor"] = data.value[0].msnfp_showinhonor;

                if (!isNullOrUndefined(data.value[0].msnfp_showmatchdonation))
                    cloneData["msnfp_showmatchdonation"] = data.value[0].msnfp_showmatchdonation;

                if (!isNullOrUndefined(data.value[0].msnfp_showmembership))
                    cloneData["msnfp_showmembership"] = data.value[0].msnfp_showmembership;

                if (!isNullOrUndefined(data.value[0].msnfp_smallimage))
                    cloneData["msnfp_smallimage"] = data.value[0].msnfp_smallimage;

                if (!isNullOrUndefined(data.value[0].msnfp_thankyou))
                    cloneData["msnfp_thankyou"] = data.value[0].msnfp_thankyou;

                if (!isNullOrUndefined(data.value[0].msnfp_visible))
                    cloneData["msnfp_visible"] = data.value[0].msnfp_visible;

                if (!isNullOrUndefined(data.value[0].msnfp_whydonatemonthly))
                    cloneData["msnfp_whydonatemonthly"] = data.value[0].msnfp_whydonatemonthly;

                if (!isNullOrUndefined(data.value[0].msnfp_enabledesignationselection))
                    cloneData["msnfp_enabledesignationselection"] = data.value[0].msnfp_enabledesignationselection;

                if (!isNullOrUndefined(data.value[0].msnfp_designationselectionlabel))
                    cloneData["msnfp_designationselectionlabel"] = data.value[0].msnfp_designationselectionlabel;


                // binding lookup fields
                if (data.value[0]._msnfp_appealid_value !== null)
                    cloneData["msnfp_AppealId@odata.bind"] = "/msnfp_appeals(" + data.value[0]._msnfp_appealid_value + ")";

                if (data.value[0]._msnfp_campaignid_value !== null)
                    cloneData["msnfp_CampaignId@odata.bind"] = "/campaigns(" + data.value[0]._msnfp_campaignid_value + ")";

                if (data.value[0]._msnfp_configurationid_value !== null)
                    cloneData["msnfp_ConfigurationId@odata.bind"] = "/msnfp_configurations(" + data.value[0]._msnfp_configurationid_value + ")";

                if (data.value[0]._msnfp_eventid_value !== null)
                    cloneData["msnfp_EventId@odata.bind"] = "/msnfp_events(" + data.value[0]._msnfp_eventid_value + ")";

                if (data.value[0]._msnfp_packageid_value !== null)
                    cloneData["msnfp_PackageId@odata.bind"] = "/msnfp_packages(" + data.value[0]._msnfp_packageid_value + ")";

                if (data.value[0]._msnfp_paymentprocessorid_value !== null)
                    cloneData["msnfp_PaymentProcessorId@odata.bind"] = "/msnfp_paymentprocessors(" + data.value[0]._msnfp_paymentprocessorid_value + ")";

                if (data.value[0]._msnfp_teamownerid_value != null)
                    cloneData["msnfp_TeamOwnerId@odata.bind"] = "/teams(" + data.value[0]._msnfp_teamownerid_value + ")";

                if (data.value[0]._msnfp_termsofreferenceid_value !== null)
                    cloneData["msnfp_TermsOfReferenceId@odata.bind"] = "/msnfp_termsofreferences(" + data.value[0]._msnfp_termsofreferenceid_value + ")";

                if (data.value[0]._ownerid_value !== null)
                    cloneData["ownerid@odata.bind"] = "/systemusers(" + data.value[0]._ownerid_value + ")";

                if (data.value[0]._transactioncurrencyid_value != null)
                    cloneData["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + data.value[0]._transactioncurrencyid_value + ")";

                if (data.value[0]._msnfp_designationid_value != null)
                    cloneData["msnfp_DesignationId@odata.bind"] = "/msnfp_designations(" + data.value[0]._msnfp_designationid_value + ")";

                console.debug(cloneData);

                //Create new Record
                var oDataUri = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpages";
                var cloneDonationPageID = XrmServiceUtility.CreateRecord(oDataUri, cloneData);



                //Created Donation Page Amount record
                if (!isNullOrUndefined(cloneDonationPageID)) {
                    var daQuery = "msnfp_donationpageamounts?";
                    daQuery += "$select=exchangerate,msnfp_defaultamount,_transactioncurrencyid_value,_msnfp_donationpageid_value,msnfp_identifier,statuscode,statecode";
                    daQuery += "&$filter=_msnfp_donationpageid_value eq " + "'" + originRecordGuid + "'";
                    var daResult = ExecuteQuery(GetWebAPIUrl() + daQuery);

                    if (!isNullOrUndefined(daResult) && daResult.length > 0) {
                        $(daResult).each(function () {
                            var cloneDonationAmountData = {};

                            cloneDonationAmountData["statuscode"] = this.statuscode;
                            cloneDonationAmountData["statecode"] = this.statecode;

                            if (!isNullOrUndefined(this.exchangerate))
                                cloneDonationAmountData["exchangerate"] = this.exchangerate;

                            if (!isNullOrUndefined(this.msnfp_defaultamount))
                                cloneDonationAmountData["msnfp_defaultamount"] = this.msnfp_defaultamount;

                            if (this._transactioncurrencyid_value != null)
                                cloneDonationAmountData["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + this._transactioncurrencyid_value + ")";

                            if (!isNullOrUndefined(this.msnfp_identifier)) {
                                // Make sure we have enough space for the words ' - Clone':
                                if (this.msnfp_identifier.length >= 142) {
                                    cloneDonationAmountData["msnfp_identifier"] = this.msnfp_identifier;
                                }
                                else {
                                    cloneDonationAmountData["msnfp_identifier"] = this.msnfp_identifier + " - Clone";
                                }
                            }

                            cloneDonationAmountData["msnfp_DonationPageId@odata.bind"] = "/msnfp_donationpages(" + cloneDonationPageID + ")";

                            var cloneDonationAmountUri = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donationpageamounts";
                            XrmServiceUtility.CreateRecord(cloneDonationAmountUri, cloneDonationAmountData);
                        });
                    }
                }

                // Completed all actions, show this to the user so they do not click again:
                var alertStrings = { confirmButtonLabel: "Okay", text: "The Cloning Process has been Completed!" };
                var alertOptions = { height: 120, width: 260 };
                Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                    function success(result) {
                        console.log("Alert dialog closed");
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );
            }
            else {
                //No data returned
            }
        },
        error: function (XmlHttpRequest, textStatus, errorThrown) {
            alert("Error :  has occured during retrieving of the activity "
                + XmlHttpRequest.responseText);
        }
    });
}

// Function to handle hiding or showing button according to the passed parameter
function HideButtonAccordingToSecurityRoles(securityRoleName) {
    var currentUserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var hasAccordingRole = CheckUserInRole(securityRoleName, currentUserID);
    var isSystemAdminRole = CheckUserInRole('System Administrator', currentUserID);
    var isVisible = false;

    if (hasAccordingRole || isSystemAdminRole)
        return isVisible = true;
    else
        return isVisible;
}

function DSTOffsetYYYYMMDD(str) {
    let userSettings;

    // Syncronous call:
    jQuery.ajax({
        url: `${parent.Xrm.Page.context.getClientUrl()}/api/data/v9.1/usersettingscollection(${MissionFunctions.GetCurrentUserID()})`,
        success: function (result) {
            userSettings = result;
        },
        async: false
    });

    if (!isNullOrUndefined(userSettings)) {
        let dt = new Date(str);
        let start = new Date(dt.getYear(), userSettings.timezonedaylightmonth - 1, userSettings.timezonedaylightday);
        let end = new Date(dt.getYear(), userSettings.timezonestandardmonth - 1, userSettings.timezonestandardday);
        if (dt >= start && dt < end) dt.setMinutes(-userSettings.timezonedaylightbias);
        return dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();
    }
    else {
        return "";
    }
}

function DSTOffset(str, successCallback) {
    let userSettings;

    // Syncronous call:
    jQuery.ajax({
        url: `${parent.Xrm.Page.context.getClientUrl()}/api/data/v9.1/usersettingscollection(${MissionFunctions.GetCurrentUserID()})`,
        success: function (result) {
            userSettings = result;

            var returnObject = {};
            if (!isNullOrUndefined(userSettings)) {
                let dt = new Date(str);
                let start = new Date(dt.getYear(), userSettings.timezonedaylightmonth - 1, userSettings.timezonedaylightday);
                let end = new Date(dt.getYear(), userSettings.timezonestandardmonth - 1, userSettings.timezonestandardday);
                if (dt >= start && dt < end) dt.setMinutes(-userSettings.timezonedaylightbias);
                returnObject = dt;
            }
            else {
                returnObject = "";
            }

            if (successCallback !== undefined && successCallback !== null) {
                successCallback(returnObject);
            }
        },
        async: true
    });
}

function showSetAsAppraiserButton() {
    var accountid = parent.Xrm.Page.data.entity.getId().replace("{", "").replace("}", "");
    var formType = parent.Xrm.Page.ui.getFormType();

    if (formType != 1) {

        var query = "accounts?$filter=accountid eq " + accountid + "&$select=msnfp_isappraiser";
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query)[0];
        if (result.msnfp_isappraiser === true) return false;
        else return true;
    }
    else {
        return false;
    }
}

function SetAsAppraiser(formContext) {
    var accountid = parent.Xrm.Page.data.entity.getId().replace("{", "").replace("}", "");
    var rec = {};
    rec["msnfp_isappraiser"] = true;
    var qry = GetWebAPIUrl() + "accounts(" + accountid + ")";
    UpdateRecord(qry, rec);
    formContext.ui.refreshRibbon();
}

function showRemoveAsAppraiser() {
    var accountid = parent.Xrm.Page.data.entity.getId().replace("{", "").replace("}", "");
    var formType = parent.Xrm.Page.ui.getFormType();

    if (formType != 1) {
        var query = "accounts?$filter=accountid eq " + accountid + "&$select=msnfp_isappraiser";
        //console.debug(XrmServiceUtility.GetWebAPIUrl() + query);
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query)[0];
        //console.debug(result);
        if (result.msnfp_isappraiser === true) return true;
        else return false;
    }
    else {
        return false;
    }
}

function RemoveAsAppraiser(formContext) {
    var accountid = parent.Xrm.Page.data.entity.getId().replace("{", "").replace("}", "");
    var rec = {};
    rec["msnfp_isappraiser"] = false;
    var qry = GetWebAPIUrl() + "accounts(" + accountid + ")";
    UpdateRecord(qry, rec);
    formContext.ui.refreshRibbon();
}

function getFormName(formContext) {
    var formName = formContext.ui.formSelector.getCurrentItem().getLabel();
    return formName;
}
// formNames should be a comma separated string
function hideRibbonElementOnTheseForms(formNames, formContext) {
    console.debug("formNames:" + formNames);
    if (!isNullOrUndefined(formNames)) {
        //var formContext = primaryControl.getFormContext();
        var formNamesArray = formNames.split(",");
        var currentFormName = getFormName(formContext);
        console.debug(currentFormName);
        if (currentFormName != null) {
            if (formNamesArray.includes(currentFormName)) {
                return false;
            }
        }
    }

    return true;
}

function hideRibbonButtonIfHousehold() {
    try {

        let msnfp_accounttype = parent.Xrm.Page.data.entity.attributes.get("msnfp_accounttype");

        if (!isNullOrUndefined(msnfp_accounttype)) {
            // Don't show if household:
            if (msnfp_accounttype.getValue() == 844060000) {
                console.log("Hide");
                return false;
            }
            else {
                console.log("Show");
                return true;
            }
        }
    }
    catch (e) {
        console.log("Error in hideRibbonButtonIfHousehold()");
    }
}

var forms = {
    supportedEntity: "account",
    HouseHoldFormId: "1738bfe6-7a21-4d68-b90f-179a693ecb9c",
    DefaultFormId: "7a0ea9e9-fcb8-45ca-b24f-b8c2b6980eb5"
}

// Form redirect for household
if (parent.Xrm.Page.data.entity.getEntityName() == forms.supportedEntity) {
    this.showFormForConfigured();
}

function showFormForConfigured() {
    var entityObject = "";

    if (parent.Xrm.Page.data.entity !== undefined && parent.Xrm.Page.data.entity !== null)
        entityObject = parent.Xrm.Page.data.entity;
    else if (Xrm.Page.data.entity !== undefined && Xrm.Page.data.entity !== null)
        entityObject = Xrm.Page.data.entity;


    // If the form is not the create form:

    var formObject = "";
    if (parent.Xrm.Page.ui !== null && parent.Xrm.Page.ui !== undefined)
        formObject = parent.Xrm.Page.ui;
    else if (Xrm.Page.ui !== null && Xrm.Page.ui !== undefined)
        formObject = Xrm.Page.ui;

    if ((formObject === undefined) || (entityObject === undefined) || !(entityObject.getEntityName() == forms.supportedEntity))
        return;

    if (formObject.getFormType() !== 1) {
        console.log("Show the correct form for this record.");

        // variable to store the name of the form
        var formId = parent.Xrm.Page.getAttribute("msnfp_primaryform").getValue();

        if (formId == null)
            formId = forms.HouseHoldFormId;

        console.log("Saved form id: " + formId);
        console.log("Current form id: " + formObject.formSelector.getCurrentItem().getId());
        try {
            //parent.Xrm.Page
            let msnfp_accounttype = parent.Xrm.Page.getAttribute("msnfp_accounttype");
            // Check if the current form is form that shoul be displayed based on the Primary Form Id value:
            if (formObject.formSelector.getCurrentItem().getId() != forms.HouseHoldFormId && msnfp_accounttype !== null && msnfp_accounttype.getValue() == 844060000) {
                console.log("Not the same, therefore we switch.");
                // Otherwise it will be an endless loop
                if (formId !== forms.HouseHoldFormId) {

                    formId = forms.HouseHoldFormId;
                    var primaryForm = parent.Xrm.Page.getAttribute("msnfp_primaryform");

                    if (primaryForm !== null) {
                        primaryForm.setValue(formId);
                        parent.Xrm.Page.data.entity.save();
                    }
                }

                var householdForm = parent.Xrm.Page.ui.formSelector.items.get(formId);
                if (householdForm) {
                    householdForm.navigate();
                }
            } else if (parent.Xrm.Page.ui.formSelector.getCurrentItem().getId() == forms.HouseHoldFormId && msnfp_accounttype.getValue() != 844060000) {
                var defaultMainForm = parent.Xrm.Page.ui.formSelector.items.get(forms.DefaultFormId);
                if (defaultMainForm)
                    defaultMainForm.navigate();
            }
        }
        catch (e) {
            console.log("An error has occured in showForm()");
        }

    }
}


// Event Receipting Functions
function UpdateTicketsFromEventTicket() {
    console.log("UpdateTicketsFromEventTicket()");
    UpdateEntitiesFromEventEntity("msnfp_eventticket");
}

function UpdateProductsFromEventProduct() {
    console.log("UpdateProductsFromEventProduct()");
    UpdateEntitiesFromEventEntity("msnfp_eventproduct");
}

function UpdateSponsorshipsFromEventSponsorship() {
    console.log("UpdateSponsorshipsFromEventSponsorship()");
    UpdateEntitiesFromEventEntity("msnfp_eventsponsorship");
}
// this generically named function is called by UpdateTicketsFromEventTicket, UpdateProductsFromEventProduct, UpdateTicketsFromEventTickets
// this uses config elements originally set up for bank run (which will hopefully be renamed at some point)
function UpdateEntitiesFromEventEntity(entityName) {
    console.debug("entityName:" + entityName);

    var currentEntityGUID = XrmUtility.CleanGuid(Xrm.Page.data.entity.getId());
    var currentUserID = Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
    var configRecord = null;

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentUserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
    user = user[0];

    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        var selectQuery = "msnfp_configurations?$select=msnfp_configurationid,msnfp_eventreceiptingsecuritykey,msnfp_bankrunfilewebjoburl";
        var filterConfig = "&$filter=msnfp_configurationid eq " + user._msnfp_configurationid_value;
        var configresult = ExecuteQuery(GetWebAPIUrl() + selectQuery + filterConfig);

        if (!isNullOrUndefined(configresult)) {
            configRecord = configresult[0];

            console.log("configRecord.msnfp_bankrunfilewebjoburl = " + configRecord.msnfp_bankrunfilewebjoburl);

            // We get the values we need to actually trigger the web job:
            if (!isNullOrUndefined(configRecord.msnfp_bankrunfilewebjoburl) && !isNullOrUndefined(configRecord.msnfp_eventreceiptingsecuritykey)) {
                if (isNullOrUndefined(currentEntityGUID)) {
                    alert("Could not get the current entity GUID. Please ensure this record is saved and try again.");
                    return;
                }
                else {
                    // Note that the space is not a mistake, that is required instead of & for webjob argument passing.
                    var webJobURL = configRecord.msnfp_bankrunfilewebjoburl + "/api/eventReceipting/" + entityName + "/" + currentEntityGUID + "?code=" + configRecord.msnfp_eventreceiptingsecuritykey;
                    console.debug(webJobURL);

                    try {
                        $.ajax
                            ({
                                type: "GET",
                                url: webJobURL,
                                success: function () {
                                    console.debug("success.");
                                },
                                error: function (jqXHR, textStatus, errorThrown) {
                                    console.debug(jqXHR);
                                    console.debug(textStatus);
                                    console.debug(errorThrown);
                                }
                            });
                    }
                    catch (e) {
                        console.debug(e);
                    }

                    let alertStrings = { title: "Updating Records", confirmButtonLabel: "Okay", text: "Note that it may take some time to update all the records." };
                    let alertOptions = { height: 120, width: 260 };
                    Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(
                        function success(result) {
                            console.log("Alert dialog closed");
                        },
                        function (error) {
                            console.log(error.message);
                        }
                    );
                }
            }
            else {
                let errorString = { title: "Cannot Process Request", confirmButtonLabel: "Okay", text: "Please ensure the fields 'Event Receipting Security Key' and 'Background Services Web Job URL' are set in the configuration record and try again." };
                let errorOptions = { height: 120, width: 260 };
                Xrm.Navigation.openAlertDialog(errorString, errorOptions).then(
                    function success(result) {
                        console.log("Alert dialog closed");
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );
            }
        }
    }

}

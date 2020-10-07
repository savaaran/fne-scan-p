/*************************************************************************
* © Microsoft. All rights reserved.
*/
var DonationType =
{
    Donation: 844060000,
    Credit: 844060001,
}

var DonationOccurrence = {
    PostDated: 84406000,
    Recurring: 84406001,
    Instant: 84406002
}

var PaymentType = {
    Cash: 844060000,
    Cheque: 844060001,
    CreditDebit: 844060002,
    Bank: 844060003,
    InKind: 844060004,
    Gift: 844060005,
    Stock: 844060006,
    Property: 844060007,
    Other: 844060008,
    WireTransfer: 844060009,
    DirectDebit: 844060010,
    FundTrasfer: 844060011,
    ExternalCreditDebit: 844060012
}

var RecurrenceStart = {
    CurrentDay: 844060000,
    FirstOftheMonth: 844060001,
    FifteenoOftheMonth: 844060002
}

var MembershipDuration = {
    Lifetime: { value: 844060008, name: "Lifetime" },
    Year10: { value: 844060007, name: "10 Years" },
    Year5: { value: 844060006, name: "5 Years" },
    Year4: { value: 844060005, name: "4 Years" },
    Year3: { value: 844060004, name: "3 Years" },
    Month24: { value: 844060003, name: "24 Month" },
    Month12: { value: 844060002, name: "12 Month" },
    Month6: { value: 844060001, name: "6 Month" },
    Month3: { value: 844060000, name: "3 Month" },
    NotApplicable: { value: 844060009, name: "Not Applicable" }
};

var Anonymity = {
    No: 844060000,
    Yes: 844060001
};

var xrm;
var formType;
var currentID;
var selectedDonor = null;
var userSettings;
var donorId = null;
var donationPledge = [];
var selectedConstitute = null;
var constituteId = null;
var campaignEventID = null;
var cardExists = false;
var bankExists = false;
var totalAmountReceiptedOfTargetYear = 0;

//Refund
var customerId;
var existingPledgeLoaded = false;
var relatedPledgeId;
var noResult = false;

//Recurring Donations
var nextDonationDate;
var relatedDonationId;
var relatedDonationEntity;
var createRelatedEntity;

//Pledge Schedule
var searchTable;
var isActive = false;
var configRecord = null;
var select = "";
var expand = "";
var filter = "";
var isFromPledgeAllocation = false;

var bankid;
var cardid;
var pledgeAllocationResult = null;
var parentGiftID;
var parentTypeWhenTransferGiftAmount;
var parentGiftAmount;
var cancelReasonOptions = [];
var currencySymbol;
var creditCardType;
var isoCurrencyCode;
var isIncludeMembership;
var finalAmount = 0;
var selectedMembershipAmount = 0;

var isAmexCard = false;
var userConfigID = null;
var pledgeScheduleID;

var appraiserResult = [];
var selectedAppraiser;
var selectedAppraiserAccount = false;

$(document).ready(function () {
    console.debug("msnfp_ManualDonation_Transaction.js");

    // First, setup all the datepickers:
    $("#txtNextRecurringDate").datepicker({
        dateFormat: 'mm/dd/yy',
        minDate: 0
    });

    $("#txtRecurringStartDate").datepicker({
        dateFormat: 'mm/dd/yy',
        minDate: 0
    });

    $('#txtRecurringEndDate').datepicker({
        dateFormat: 'mm/dd/yy',
        minDate: 0
    });

    $('#txtChequeDate').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtDepositDate').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtNewChequeDate').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtNewDepositDate').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtNewWireDate').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtNewWireDepositDate').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtNewDepositDateAll').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    $('#txtDateofSale').datepicker({
        dateFormat: 'mm/dd/yy',
    });

    // Modal content starts here. Note that modal is used for the Membership section.
    // Get the Membership Modal:
    var membershipModal = document.getElementById("membershipModal");

    // Get the <span> element that closes the modal:
    var closeButtonSpan = document.getElementsByClassName("closeBtn")[0];

    // When the user clicks on <span> (x), close the modal:
    if (!isNullOrUndefined(closeButtonSpan)) {
        closeButtonSpan.onclick = function () {
            membershipModal.style.display = "none";
        }
    }

    // When the user clicks anywhere outside of the modal, close it:
    window.onclick = function (event) {
        if (event.target == membershipModal) {
            membershipModal.style.display = "none";
        }
    }

    // Modal content ends here.       
    xrm = XrmUtility.get_Xrm();

    formType = parent.Xrm.Page.ui.getFormType();

    // Fill in the date field for new gifts to now:
    if (formType == FormType.Create) {
        parent.Xrm.Page.getAttribute('msnfp_bookdate').setValue(new Date());
    }

    // Current ID of the gift page we are on:
    currentID = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '');
    customerId = getLookupGuid('msnfp_customerid');

    var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();

    var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
    userSelect += "&$filter=systemuserid eq " + currentuserID;
    var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);

    if (!isNullOrUndefined(user)) {
        user = user[0];
    }

    getCurrencySymbol();

    // Initialize money fields as 0.00:
    $('#txtMembershipAmount').val(currencySymbol + '0.00');
    $('#txtMembershipAmountCommitted').val(currencySymbol + '0.00');
    $('#txtAmountNonReceiptable').val(currencySymbol + '0.00');
    $('#txtAmountTax').val(currencySymbol + '0.00');
    $('#txtTotalAmount').val(currencySymbol + '0.00');
    $('#txtAppraiserAmount').val(currencySymbol + '0.00');
    $('#txtAppraiserNonReceiptable').val(currencySymbol + '0.00');


    // If the configuration record is found for the user, show/hide several fields:
    if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
        userConfigID = user._msnfp_configurationid_value;
        loadConfiguration(user._msnfp_configurationid_value);

        // Show the correct payment info as Donation is NOT an option so we must change it immediately:
        paymentTypeChange();
        donationPledgeTypechange();
        showHidePaymentType(configRecord);

        if (isNullOrUndefined(configRecord.msnfp_tran_anonymous) || configRecord.msnfp_tran_anonymous == false) {
            $('#radioVisibleDonation').next('label').remove();
            $('#radioVisibleDonation').remove();
            $('#radioAnonymous').next('label').remove();
            $('#radioAnonymous').remove();
        }

        if (!isNullOrUndefined(configRecord.msnfp_tran_nonreceiptable) &&
            configRecord.msnfp_tran_nonreceiptable == true) {
            $('.divAmountNonReceiptable').css("display", "flex");
            $('.divAmount').css("display", "flex");
        } else {
            $('.divAmountNonReceiptable').css("display", "none");
            $('.divAmount').css("display", "none");
        }

        if (!isNullOrUndefined(configRecord.msnfp_tran_showamounttax) && configRecord.msnfp_tran_showamounttax == true) {
            $('.divAmountTax').css("display", "flex");
        }

        if (!isNullOrUndefined(configRecord.msnfp_tran_membership) && configRecord.msnfp_tran_membership) {
            $('#newMembershipToDonation').css("display", "flex");
            $('#divMembership').css("display", "block");
            $('#divMembershipContainer').css("display", "block");
        }
        else {
            $('#newMembershipToDonation').hide();
            $('#divMembership').hide();
            $('#divMembershipContainer').hide();
        }

        let enforceappraiserlookup = configRecord.msnfp_enforceappraiserlookup;
        $('#ddlAppraiser').closest('div').toggleClass(enforceappraiserlookup ? 'mandatory' : '');
        $('.divAppraiserFirst input, #btnAppraiserNext').prop('disabled', enforceappraiserlookup);

        parent.Xrm.Page.getAttribute("msnfp_transaction_paymentscheduleid").addOnChange(RefreshPaymentSchedule);
        RefreshPaymentSchedule();
        customerChange();
    }

    // Populate fields with existing data in the event of an existing record:
    if (formType === FormType.Update || formType === FormType.Disabled || formType === FormType.ReadOnly) {
        loadExistingTransactionData();

        // Show any relevant API error messages (if applicable):
        showAPIErrorMessage();
    }
    else if (formType == FormType.Create) {

        // If this is a new entity, try and fill in fields from the parent (if applicable):
        var dPldgeID;
        var xrmObject = parent.Xrm.Page.context.getQueryStringParameters();

        // If this is created by a pledge:
        if (xrmObject["parameter_parentdonationid"] != null) {
            relatedPledgeId = xrmObject["parameter_parentdonationid"].toString();
            loadParentDonationDetails(relatedPledgeId);
            dPldgeID = xrmObject["parameter_parentdonationid"].toString();
        }

        // Set the donor from the add gift button on the contact or account page (or from the payment schedule page):
        if (xrmObject["param_msnfp_customerid"] != null && xrmObject["param_msnfp_customeridname"] != null && xrmObject["param_msnfp_customeridtype"] != null) {
            var donorId = xrmObject["param_msnfp_customerid"];
            var donorName = xrmObject["param_msnfp_customeridname"];
            var donorType = xrmObject["param_msnfp_customeridtype"];

            parent.Xrm.Page.getAttribute("msnfp_customerid").setValue([{ id: donorId, name: donorName, entityType: donorType }]);
            // Now trigger the change of fields:
            customerChange();
        }

        // If this is being added via a pledge schedule (when the user clicks the Donation button on a pledge schedule):
        if (xrmObject["param_msnfp_pledgeallocationid"] != null) {
            isFromPledgeAllocation = true;
            var pledgeAllocationID = xrmObject["param_msnfp_pledgeallocationid"];

            var donorId = xrmObject["param_msnfp_customerid"];
            var donorName = xrmObject["param_msnfp_customeridname"];
            var donorType = xrmObject["param_msnfp_customeridtype"];
            var amount = parseFloat(xrmObject["param_msnfp_amount"]);
            var DPtype = parseInt(xrmObject["param_msnfp_type"]);
            var dpAnonymity = parseInt(xrmObject["msnfp_anonymous"]);
            dPldgeID = xrmObject["param_pledge_donationid"].toString();
            pledgeScheduleID = xrmObject["param_pledge_donationid"].toString();

            if (xrmObject["param_pledge_donationid"]) {
                // Get the payment schedule name and set the parent payment schedule id:
                var psselect = "msnfp_paymentschedules?$select=msnfp_paymentscheduleid,msnfp_name";
                var psfilter = "&$filter=msnfp_paymentscheduleid eq " + pledgeScheduleID;
                var psResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + psselect + psfilter);

                if (!isNullOrUndefined(psResult)) {
                    parent.Xrm.Page.getAttribute("msnfp_transaction_paymentscheduleid").setValue([{ id: pledgeScheduleID, name: psResult[0].msnfp_name, entityType: "msnfp_paymentschedule" }]);
                }
            }

            parent.Xrm.Page.getAttribute("msnfp_customerid").setValue([{ id: donorId, name: donorName, entityType: donorType }]);
            parent.Xrm.Page.getAttribute("msnfp_amount").setValue(amount);

            // Here we get the donor commitment that is to be tied to this transaction:
            select = "msnfp_donorcommitments(" + pledgeAllocationID + ")?";
            expand = "$select=msnfp_donorcommitmentid,msnfp_name";
            pledgeAllocationResult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);

            if (pledgeAllocationResult != null) {
                if (pledgeAllocationResult.msnfp_donorcommitmentid != null) {
                    relatedPledgeId = pledgeAllocationResult.msnfp_donorcommitmentid;
                    var relatedPledgeName = pledgeAllocationResult.msnfp_name;
                    var relatedPledgeType = "msnfp_donorcommitment";

                    parent.Xrm.Page.getAttribute("msnfp_donorcommitmentid").setValue([{ id: relatedPledgeId, name: relatedPledgeName, entityType: relatedPledgeType }]);
                }
            }

            $("input[name=donationPledgeType][value='" + DPtype + "']").prop("checked", true);
            $("input[name=donationPledgeType]").each(function () {
                $(this).attr("disabled", "disabled");
            });

            $("input[name=anonymousDonation][value='" + dpAnonymity + "']").prop("checked", true);


            //$('#txtAmount').val(amount);
            $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount).toFixed(2)));
            $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount).toFixed(2)));

            let con = parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedconstituentid");
            if (!con.getValue() && xrmObject["param_msnfp_constituentid"]) {
                con.setValue(xrmObject["param_msnfp_constituentid"]);
            }

            // We have to grab the details of the payment schedule here as if we did it below in the !isNullOrUndefined(dPldgeID) condition it would be for the wrong entity type:
            select = "msnfp_paymentschedules(" + dPldgeID + ")?";
            expand = "$expand=msnfp_OriginatingCampaignId($select=campaignid,name)";
            let pledgeScheduleResult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);
            if (!isNullOrUndefined(pledgeScheduleResult) && !isNullOrUndefined(pledgeScheduleResult.msnfp_OriginatingCampaignId)) {
                campaignEventID = pledgeScheduleResult.msnfp_OriginatingCampaignId.campaignid;
                let campaignEventName = pledgeScheduleResult.msnfp_OriginatingCampaignId.name;

                if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_originatingcampaignid")))
                    parent.Xrm.Page.getAttribute("msnfp_originatingcampaignid").setValue([{ id: campaignEventID, name: campaignEventName, entityType: "campaign" }]);
            }

            var pdselect = "msnfp_paymentschedules(" + dPldgeID + ")?";
            var pdexpand = "$expand=msnfp_DesignationId($select=msnfp_designationid,msnfp_name),msnfp_AppealId($select=msnfp_appealid,msnfp_identifier),msnfp_PackageId($select=msnfp_packageid,msnfp_identifier)";
            pdResult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + pdselect + pdexpand);
            if (!isNullOrUndefined(pdResult) && !isNullOrUndefined(pdResult.msnfp_DesignationId)) {
                let designationID = pdResult.msnfp_DesignationId.msnfp_designationid;
                let designationName = pdResult.msnfp_DesignationId.msnfp_name;

                if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_designationid")))
                    parent.Xrm.Page.getAttribute("msnfp_designationid").setValue([{ id: designationID, name: designationName, entityType: "msnfp_designation" }]);

            }

            // Set the appeal and the package from the donor commitment:
            if (!isNullOrUndefined(pdResult) && !isNullOrUndefined(pdResult.msnfp_AppealId)) {
                let parentAppealID = pdResult.msnfp_AppealId.msnfp_appealid;
                let parentAppealName = pdResult.msnfp_AppealId.msnfp_identifier;

                if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_appealid"))) {
                    parent.Xrm.Page.getAttribute("msnfp_appealid").setValue([{ id: parentAppealID, name: parentAppealName, entityType: "msnfp_appeal" }]);
                }
            }

            if (!isNullOrUndefined(pdResult) && !isNullOrUndefined(pdResult.msnfp_PackageId)) {
                let parentPackageID = pdResult.msnfp_PackageId.msnfp_packageid;
                let parentPackageName = pdResult.msnfp_PackageId.msnfp_identifier;

                if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_packageid"))) {
                    parent.Xrm.Page.getAttribute("msnfp_packageid").setValue([{ id: parentPackageID, name: parentPackageName, entityType: "msnfp_package" }]);
                }
            }
        }

        // If this was opened from a pledge (donor commitment) or pledge schedule:
        if (!isNullOrUndefined(dPldgeID)) {
            // Donor commitment:
            if (xrmObject["param_msnfp_pledgeallocationid"] == null) {
                select = "msnfp_donorcommitments(" + dPldgeID + ")?";
                expand = "$expand=msnfp_Commitment_DefaultDesignationId($select=msnfp_designationid,msnfp_name),msnfp_AppealId($select=msnfp_appealid,msnfp_identifier),msnfp_PackageId($select=msnfp_packageid,msnfp_identifier),msnfp_Commitment_CampaignId($select=campaignid,name)";
                let donorCommitmentResult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);

                // Get the campaign from the donor commitment:
                if (!isNullOrUndefined(donorCommitmentResult) && !isNullOrUndefined(donorCommitmentResult.msnfp_Commitment_CampaignId)) {
                    campaignEventID = donorCommitmentResult.msnfp_Commitment_CampaignId.campaignid;
                    let campaignEventName = donorCommitmentResult.msnfp_Commitment_CampaignId.name;

                    if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_originatingcampaignid"))) {
                        parent.Xrm.Page.getAttribute("msnfp_originatingcampaignid").setValue([{ id: campaignEventID, name: campaignEventName, entityType: "campaign" }]);
                    }
                }

                // Get the designation from the donor commitment:
                if (!isNullOrUndefined(donorCommitmentResult) && !isNullOrUndefined(donorCommitmentResult.msnfp_Commitment_DefaultDesignationId)) {
                    let designationID = donorCommitmentResult.msnfp_Commitment_DefaultDesignationId.msnfp_designationid;
                    let designationName = donorCommitmentResult.msnfp_Commitment_DefaultDesignationId.msnfp_name;

                    if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_designationid"))) {
                        parent.Xrm.Page.getAttribute("msnfp_designationid").setValue([{ id: designationID, name: designationName, entityType: "msnfp_designation" }]);
                    }
                }

                // Set the appeal from the donor commitment:
                if (!isNullOrUndefined(donorCommitmentResult) && !isNullOrUndefined(donorCommitmentResult.msnfp_AppealId)) {
                    let parentAppealID = donorCommitmentResult.msnfp_AppealId.msnfp_appealid;
                    let parentAppealName = donorCommitmentResult.msnfp_AppealId.msnfp_identifier;

                    if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_appealid"))) {
                        parent.Xrm.Page.getAttribute("msnfp_appealid").setValue([{ id: parentAppealID, name: parentAppealName, entityType: "msnfp_appeal" }]);
                    }
                }

                // Set the package from the donor commitment:
                if (!isNullOrUndefined(donorCommitmentResult) && !isNullOrUndefined(donorCommitmentResult.msnfp_PackageId)) {
                    let parentPackageID = donorCommitmentResult.msnfp_PackageId.msnfp_packageid;
                    let parentPackageName = donorCommitmentResult.msnfp_PackageId.msnfp_identifier;

                    if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_packageid"))) {
                        parent.Xrm.Page.getAttribute("msnfp_packageid").setValue([{ id: parentPackageID, name: parentPackageName, entityType: "msnfp_package" }]);
                    }
                }
            }
        }
    }

    showHideControlsAndLabelChange();

    loadAppraiser();

    // Bind the inputs to their respective function calls:
    $('input[name=donationPledgeType]').change(function () {
        donationPledgeTypechange();
    });

    $('input[name=paymentType]').change(function () {
        paymentTypeChange();
    });

    $('input[name=anonymousDonation]').change(function () {
        manageTab(this);
        var selectedVal = $('input[name=anonymousDonation]:checked').val();
        if (selectedVal == Anonymity.Yes) {
            parent.Xrm.Page.getAttribute("msnfp_customerid").setRequiredLevel("none");
            parent.Xrm.Page.getAttribute("msnfp_relatedconstituentid").setRequiredLevel("none");
        }
        else {
            parent.Xrm.Page.getAttribute("msnfp_customerid").setRequiredLevel("required");
        }
    });

    $('input[name=giftSource]').change(function () {
        manageTab(this);
    });

    $('#ddlExistingBank').change(function () {
        // New Bank == -1:
        if ($(this).val() == '-1') {
            $('.donationBankAccount .exists').hide(); //.css( "display", "flex" );
            $('.manual-main .error-msg').hide();
            $('.manual-main .error-msg').html('');
        }
        else if ($(this).val() != "0") {
            $('.manual-main .error-msg').hide();
            $('.manual-main .error-msg').html('');
            $('.donationBankAccount .exists').hide();
        }
        else if ($(this).val() == "0") {
            $('.manual-main .error-msg').hide();
            $('.manual-main .error-msg').html('');
        }

        // Regardless of the above, if it is not new show process:
        if ($(this).val() != '-1') {
            showNextForCreditBank(false);
        }
        else {
            // Since we have a NEW bank, we need the info on the next page:
            showNextForCreditBank(true);
        }
        addRemoveBankValidation();
    });

    $('#ddlExistingCard').change(function () {
        // New Credit Card == -1:
        if ($(this).val() == '-1') {
            $('.donationCreditCard .exists').hide();
            $('.manual-main .error-msg').hide();
            $('.manual-main .error-msg').html('');
        }
        else if ($(this).val() != "0") {
            $('.manual-main .error-msg').hide();
            $('.manual-main .error-msg').html('');

            $('.donationCreditCard .exists').hide();
        }
        else if ($(this).val() == "0") {
            $('.manual-main .error-msg').hide();
            $('.manual-main .error-msg').html('');
        }

        // Regardless of the above, if it is not new show process:
        if ($(this).val() != '-1') {
            showNextForCreditBank(false);
        }
        else {
            // Since we have a NEW card, we need the info on the next page:
            showNextForCreditBank(true);
        }
        addRemoveCardValidation();
    });

    $("input[id=txtInHonorMemoryOf]").change(function () {
        if (!isNullOrUndefined($("#txtInHonorMemoryOf").val())) {
            $("#ddlTributeCode").closest('div').addClass("mandatory");
        } else {
            $("#ddlTributeCode").closest('div').removeClass("mandatory");
        }
    });

    $("#ddlTributeCode").change(function () {
        console.debug($("#ddlTributeCode").val());
        if (!isNullOrUndefined($("#ddlTributeCode").val())) {
            $("#txtInHonorMemoryOf").closest('div').addClass("mandatory").css("display", "flex");
        } else {
            $("#txtInHonorMemoryOf").closest('div').removeClass("mandatory").css("display", "none");
        }
    });

    $('#btnAppraiserNext').click(function () {
        //$("#lblAppraiserError")[0].innerText = "";
        $("#lblAppraiserError").html('');
        if ($('#ddlAppraiser').closest('div').hasClass('mandatory') && $('#ddlAppraiser').val() == 0) {
            //$("#lblAppraiserError")[0].innerText = "Please select Appraiser.";
            $("#lblAppraiserError").append('<p>Please select Appraiser.</p>');
        }
        else {
            $('.divAppraiserFirst').hide();
            $('.divAppraiserSecond').show();
            $('#btnAppraiserNext').hide();
            $('#btnAppraiserProcess').show();
        }
    });

    $('#txtAppraiserName').change(function () {
        var appCity = $('#txtAppraiserCity').val();
        if (!isNullOrUndefined($(this).val()) && !isNullOrUndefined(appCity))
            $('#btnAppraiserNext').prop('disabled', false);
        else
            $('#btnAppraiserNext').prop('disabled', true);
    });

    $('#txtAppraiserCity').change(function () {
        var appName = $('#txtAppraiserName').val();
        if (!isNullOrUndefined($(this).val()) && !isNullOrUndefined(appName))
            $('#btnAppraiserNext').prop('disabled', false);
        else
            $('#btnAppraiserNext').prop('disabled', true);
    });

    $('#txtAppraiserAmount').change(function () {
        setMandatoryAdvDescription();

        var currentVal = $(this).val();
        if (!isNullOrUndefined(currentVal)) {
            $(this).val(currencySymbol + addCommasonLoad(parseFloat(currentVal.replace(currencySymbol, '').replace(/,/g, '')).toFixed(2)));
        }
    });

    //$('#txtEligibleAmount').change(function () {
    //    setMandatoryAdvDescription();

    //    var currentVal = $(this).val();
    //    if (!isNullOrUndefined(currentVal)) {
    //        $(this).val(currencySymbol + addCommasonLoad(parseFloat(currentVal.replace(currencySymbol, '').replace(/,/g, '')).toFixed(2)));
    //    }
    //});

    $('#txtAppraiserNonReceiptable').change(function () {
        setMandatoryAdvDescription();

        var currentVal = $(this).val();
        if (!isNullOrUndefined(currentVal)) {
            $(this).val(currencySymbol + addCommasonLoad(parseFloat(currentVal.replace(currencySymbol, '').replace(/,/g, '')).toFixed(2)));
        }
    });

    $('#btnAppraiserProcess').click(function () {
        $('#btnProcess').click();
    });


    $('#ddlAppraiser').change(function () {
        console.debug("test");
        console.debug(appraiserResult);

        if (!isNullOrUndefined(appraiserResult) && $(this).val() != 0) {
            var ddlAppraiserValue = $(this).val();
            if (selectedAppraiserAccount)
                selectedAppraiser = $.grep(appraiserResult, function (e) { return e.accountid == ddlAppraiserValue; });
            else
                selectedAppraiser = $.grep(appraiserResult, function (e) { return e.contactid == ddlAppraiserValue; });

            if (!isNullOrUndefined(selectedAppraiser) && selectedAppraiser.length > 0) {
                selectedAppraiser = selectedAppraiser[0];

                if (selectedAppraiserAccount)
                    $('#txtAppraiserName').val(selectedAppraiser.name);
                else
                    $('#txtAppraiserName').val(selectedAppraiser.fullname);

                $('#txtAppraiserStreet1').val(selectedAppraiser.address1_line1);
                $('#txtAppraiserStreet2').val(selectedAppraiser.address1_line2);
                $('#txtAppraiserStreet3').val(selectedAppraiser.address1_line3);
                $('#txtAppraiserCity').val(selectedAppraiser.address1_city);
                $('#txtAppraiserState').val(selectedAppraiser.address1_stateorprovince);
                $('#txtAppraiserCountry').val(selectedAppraiser.address1_country);

                if (!isNullOrUndefined($('#txtAppraiserName').val()) && !isNullOrUndefined($('#txtAppraiserCity').val()))
                    $('#btnAppraiserNext').prop('disabled', false);
            }
        }
        else {
            selectedAppraiser = null;
            $('.divAppraiserFirst input').val('');
            $('#btnAppraiserNext').prop('disabled', true);
        }
    });


    $('.upperCase').keyup(function () {
        $(this).val($(this).val().toUpperCase());
    });

    $('#txtCostPerStock').change(function () {
        var currentVal = $(this).val();
        if (!isNullOrUndefined(currentVal)) {
            if (currentVal == 0) {
                $('#lblStockError')[0].innerText = "Cost per Stock should be greater then 0.";
                $(this).addClass('red-border');
            }
            else {
                $(this).val(currencySymbol + addCommasonLoad(parseFloat(currentVal.replace(currencySymbol, '').replace(/,/g, '')).toFixed(2)));
                $(this).removeClass('red-border');
            }
        }
    });

    $('#txtNoOfShares').change(function () {
        var currentVal = $(this).val();
        if (!isNullOrUndefined(currentVal)) {
            if (currentVal == 0) {
                $('#lblStockError')[0].innerText = "# of Shares should be greater then 0.";
                $(this).addClass('red-border');
            }
            else {
                $(this).removeClass('red-border');
            }
        }
    });

    $('#txtGainLossAmount, #txtStockAdvantageAmount, #txtStockAmount, #txtAmountPriorSale').change(function () {
        var currentVal = $(this).val();
        if (!isNullOrUndefined(currentVal)) {
            $(this).val(currencySymbol + addCommasonLoad(parseFloat(currentVal.replace(currencySymbol, '').replace(/,/g, '')).toFixed(2)));
        }
    });

    $('#btnStockProcess').click(function () {
        $('#btnProcess').click();
    });



    // When the final process button is clicked, validate the data and save/update:
    $('#btnProcess').click(function () {
        $('.manual-main .error-msg').hide();
        $('#lblErrorMessageText').html('');

        if (validate()) {
            var paymentType = $('input[name=paymentType]:checked').val();
            var selectedDonationType = parseInt($('input[name=donationPledgeType]:checked').val());

            xrm.Utility.showProgressIndicator("Processing Transaction");

            var attributes = parent.Xrm.Page.data.entity.attributes.get();
            var entity = MissionFunctions.GetEntityLookups(msnfp_entity_lookups);

            var solicitor = parent.Xrm.Page.data.entity.attributes.get("msnfp_solicitorid").getValue();
            if (!isNullOrUndefined(solicitor)) {
                if (solicitor[0].entityType == "account") {
                    entity["msnfp_SolicitorId_account@odata.bind"] = "/accounts(" + XrmUtility.CleanGuid(solicitor[0].id) + ")";
                }
                else {
                    entity["msnfp_SolicitorId_contact@odata.bind"] = "/contacts(" + XrmUtility.CleanGuid(solicitor[0].id) + ")";
                }
            }

            if (parent.Xrm.Page.getAttribute("msnfp_name").getValue() == null) {
                parent.Xrm.Page.getAttribute("msnfp_name").setValue("Donation - " + RandomGuid().substring(0, 6).toUpperCase());
            }

            entity["msnfp_name"] = parent.Xrm.Page.getAttribute("msnfp_name").getValue();

            entity["msnfp_bookdate"] = parent.Xrm.Page.getAttribute("msnfp_bookdate").getValue();
            entity["msnfp_receiveddate"] = DSTOffset($('#txtDepositDate').val());

            // Get the type:
            if (!isNullOrUndefined(selectedDonationType)) {
                entity["msnfp_typecode"] = selectedDonationType;
            }

            if (!isNullOrUndefined($('#txtChequeDate').val())) {
                entity["msnfp_chequewiredate"] = new Date($('#txtChequeDate').val());
            }

            entity["msnfp_chequenumber"] = $('#txtChequeNumber').val();

            if ($('#ddlCardType').val() > 0) {
                entity["msnfp_ccbrandcode"] = parseInt($('#ddlCardType').val());
            }

            if (!isNullOrUndefined($('#txtDepositDate').val())) {
                entity["msnfp_depositdate"] = DSTOffsetYYYYMMDD($('#txtDepositDate').val());
            }

            // Save the configuration to the gift from the user:
            if (!isNullOrUndefined(userConfigID)) {
                entity["msnfp_ConfigurationId@odata.bind"] = "/msnfp_configurations(" + userConfigID + ")";
            }

            // giftAmount - the amount as added by the user into txtTotalAmount
            var giftAmount = parseFloat($('#txtTotalAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));

            //entity["msnfp_amount_receipted"] = giftAmount;

            // Amount - Not Receiptable - as entered by the user into txtAmountNonReceiptable
            var nonReceiptableAmount = 0;
            if (!isNullOrUndefined($('#txtAmountNonReceiptable').val())) {
                nonReceiptableAmount = parseFloat($('#txtAmountNonReceiptable').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
                entity["msnfp_amount_nonreceiptable"] = nonReceiptableAmount;
            }

            // Amount - Tax:
            var amountTax = 0;
            if (!isNullOrUndefined($('#txtAmountTax').val())) {
                amountTax = parseFloat($('#txtAmountTax').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
                entity["msnfp_amount_tax"] = amountTax;
            }

            // Amount - Membership:
            var membershipAmount = !isNullOrUndefined($('#txtMembershipAmount').val()) ? parseFloat($('#txtMembershipAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
            if (membershipAmount <= 0) {
                entity["msnfp_amount_membership"] = 0;
            }
            else {
                entity["msnfp_amount_membership"] = membershipAmount;
            }


            // If the donation type is Credit:
            if (selectedDonationType == DonationType.Credit) {
                membershipAmount = 0;

                // Set the related customer id (if applicable):
                var relatedCustomerID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedcustomerid").getValue()[0].id);
                if (!isNullOrUndefined(relatedCustomerID)) {
                    if (parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedcustomerid").getValue()[0].entityType == "account") {
                        entity["msnfp_RelatedCustomerId_account@odata.bind"] = "/accounts(" + relatedCustomerID + ")";
                    }
                    else {
                        entity["msnfp_RelatedCustomerId_contact@odata.bind"] = "/contacts(" + relatedCustomerID + ")";
                    }
                }
            }


            // Total Header - this is the total amount of money processed - i.e. the amount of money actually given to the organization
            // it is the gift amount + membership amount (where gift amount includes receiptable and non-receiptable amounts)
            if (!isNullOrUndefined(parentGiftID)) {
                entity["msnfp_amount"] = 0;
            }
            else {
                entity["msnfp_amount"] = giftAmount; // + membershipAmount;
            }

            // Receiptable amount - this is the original gift amount minus the non-receiptable amount
            entity["msnfp_amount_receipted"] = giftAmount - nonReceiptableAmount - membershipAmount - amountTax;


            for (var i in attributes) { attributes[i].setSubmitMode("never"); }


            // Get the payment processor from the config file:
            entity["msnfp_PaymentProcessorId@odata.bind"] = "/msnfp_paymentprocessors(" + XrmUtility.CleanGuid(configRecord._msnfp_paymentprocessorid_value) + ")";

            // Get the parent pledge (if applicable):
            var parentPledge = parent.Xrm.Page.data.entity.attributes.get("msnfp_donorcommitmentid").getValue();
            if (!isNullOrUndefined(parentPledge)) {
                entity["msnfp_DonorCommitmentId@odata.bind"] = "/msnfp_donorcommitments(" + XrmUtility.CleanGuid(parentPledge[0].id) + ")";
            }
            else {
                entity["msnfp_DonorCommitmentId"] = null;
            }

            // Assign the contact/account:
            var anonymousDonation = $('input[name=anonymousDonation]:checked').val();
            entity["msnfp_anonymous"] = anonymousDonation;
            if (anonymousDonation == Anonymity.Yes && (selectedDonor == null || selectedDonor.length == 0)) {
                var anonymousContactId = createAnonymousContact();

                if (isNullOrUndefined(anonymousContactId)) {
                    return;
                }

                entity["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + anonymousContactId + ")";
                selectedDonor = {
                    contactid: anonymousContactId,
                    isAccount: false,
                    Name: ($('#txtFirstName').val() + ' ' + $('#txtLastName').val())
                };
            }
            else if (selectedDonor != null) {
                if (selectedDonor.isAccount) {
                    entity["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + selectedDonor.accountid + ")";
                }
                else {
                    entity["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + selectedDonor.contactid + ")";
                }
            }

            entity["msnfp_organizationname"] = $('#txtOrganization').val();

            // Assign the constitute, campaign and fund (if applicable):
            let cons = getConstituent();
            if (cons) entity["msnfp_RelatedConstituentId@odata.bind"] = cons;

            //// Campaign:
            //if (parent.Xrm.Page.data.entity.attributes.get("msnfp_originatingcampaignid").getValue()) {
            //    campaignEventID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_originatingcampaignid").getValue()[0].id);
            //    if (!isNullOrUndefined(campaignEventID)) {
            //        entity["msnfp_OriginatingCampaignId@odata.bind"] = "/campaigns(" + campaignEventID + ")";
            //    }
            //    else {
            //        entity["msnfp_OriginatingCampaignId"] = null;
            //    }
            //}
            //else {
            //    entity["msnfp_OriginatingCampaignId"] = null;
            //}

            //// Appeal:
            //if (parent.Xrm.Page.data.entity.attributes.get("msnfp_appealid").getValue()) {
            //    appealID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_appealid").getValue()[0].id);
            //    if (!isNullOrUndefined(appealID)) {
            //        entity["msnfp_AppealId@odata.bind"] = "/msnfp_appeals(" + appealID + ")";
            //    }
            //    else {
            //        entity["msnfp_AppealId"] = null;
            //    }
            //}
            //else {
            //    entity["msnfp_AppealId"] = null;
            //}

            //// Package:
            //if (parent.Xrm.Page.data.entity.attributes.get("msnfp_packageid").getValue()) {
            //    packageID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_packageid").getValue()[0].id);
            //    if (!isNullOrUndefined(packageID)) {
            //        entity["msnfp_PackageId@odata.bind"] = "/msnfp_packages(" + packageID + ")";
            //    }
            //    else {
            //        entity["msnfp_PackageId"] = null;
            //    }
            //}
            //else {
            //    entity["msnfp_PackageId"] = null;
            //}

            //// Primary Designation:
            //if (parent.Xrm.Page.data.entity.attributes.get("msnfp_designationid").getValue()) {
            //    msnfp_designationID = XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_designationid").getValue()[0].id);
            //    if (!isNullOrUndefined(msnfp_designationID)) {
            //        entity["msnfp_DesignationId@odata.bind"] = "/msnfp_designations(" + msnfp_designationID + ")";
            //    }
            //    else {
            //        entity["msnfp_DesignationId"] = null;
            //    }
            //}
            //else {
            //    entity["msnfp_DesignationId"] = null;
            //}


            // Save the gift description (if applicable):
            if (!isNullOrUndefined($('#txtDescription').val())) {
                entity["msnfp_transactiondescription"] = $('#txtDescription').val();
            }

            // Save the appraiser (if applicable):
            if (!isNullOrUndefined($('#txtApraiser').val())) {
                entity["msnfp_appraiser"] = $('#txtApraiser').val();
            }

            if ($('input[name=giftSource]:checked').val() != undefined) {
                entity["msnfp_dataentrysource"] = $('input[name=giftSource]:checked').val();
            }

            if ($('input[name=receiptPreference]:checked').val() != undefined) {
                entity["msnfp_receiptpreferencecode"] = $('input[name=receiptPreference]:checked').val();
            }

            entity["msnfp_firstname"] = $('#txtFirstName').val();
            entity["msnfp_lastname"] = $('#txtLastName').val();
            entity["msnfp_emailaddress1"] = $('#txtEmail').val();
            entity["msnfp_telephone1"] = $('#txtPhone').val();
            entity["msnfp_telephone2"] = $('#txtAltPhone').val();
            entity["msnfp_mobilephone"] = $('#txtMobilePhone').val();
            entity["msnfp_billing_line1"] = $('#txtAddressLine1').val();
            entity["msnfp_billing_line2"] = $('#txtAddressLine2').val();
            entity["msnfp_billing_line3"] = $('#txtAddressLine3').val();
            entity["msnfp_billing_city"] = $('#txtAddressCity').val();
            entity["msnfp_billing_stateorprovince"] = $('#txtAddressProvince').val();
            entity["msnfp_billing_postalcode"] = $('#txtAddressPostalCode').val();
            entity["msnfp_billing_country"] = $('#txtAddressCountry').val();

            // Save the In Honour/Memory of (if applicable):
            if (!isNullOrUndefined($('#txtInHonorMemoryOf').val())) {
                entity["msnfp_tributename"] = $('#txtInHonorMemoryOf').val();
            }

            console.log("Tribute Code:" + $('#ddlTributeCode').val());
            if ($('#ddlTributeCode').val() != "") {
                entity["msnfp_tributecode"] = parseInt($('#ddlTributeCode').val());
            }


            // Add a membership if applicable:
            if (!isNullOrUndefined(parent.Xrm.Page.getAttribute("msnfp_membershipcategoryid").getValue())) {
                entity["msnfp_MembershipCategoryId@odata.bind"] = "/msnfp_membershipcategories(" + XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_membershipcategoryid").getValue()[0].id) + ")";
            }

            // Completed:
            entity["statuscode"] = parseInt(844060000);

            // Do not charge on create of a Bank transaction EVER. This is done by the Bank Run:
            if (paymentType == PaymentType.Bank) {
                entity["msnfp_chargeoncreate"] = false;
            }

            // When trying to process a child donation manually for credit debit:
            if (paymentType == PaymentType.CreditDebit && !isNullOrUndefined(parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentscheduleid").getValue())) {
                entity["statuscode"] = parseInt(844060000);
            }


            if (paymentType == PaymentType.InKind || paymentType == PaymentType.Gift || paymentType == PaymentType.Property) {
                //entity["msnfp_giftdescription"] = $('#txtDescription').val();
                //entity["msnfp_appraiser"] = $('#txtApraiser').val();
                entity["msnfp_appraiser"] = $('#txtAppraiserName').val();
                entity["msnfp_appraiser_name"] = $('#txtAppraiserName').val();
                entity["msnfp_appraiser_line1"] = $('#txtAppraiserStreet1').val();
                entity["msnfp_appraiser_line2"] = $('#txtAppraiserStreet2').val();
                entity["msnfp_appraiser_line3"] = $('#txtAppraiserStreet3').val();
                entity["msnfp_appraiser_city"] = $('#txtAppraiserCity').val();
                entity["msnfp_appraiser_stateorprovince"] = $('#txtAppraiserState').val();
                entity["msnfp_appraiser_country"] = $('#txtAppraiserCountry').val();
                entity["msnfp_transactiondescription"] = $('#txtAppraiserDescription').val();
                entity["msnfp_description"] = $('#txtAdvDescription').val();

                //var eligibleAmount = !isNullOrUndefined($('#txtEligibleAmount').val()) ? parseFloat($('#txtEligibleAmount').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                var advantage = !isNullOrUndefined($('#txtAppraiserNonReceiptable').val()) ? parseFloat($('#txtAppraiserNonReceiptable').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                var amountOfGift = !isNullOrUndefined($('#txtAppraiserAmount').val()) ? parseFloat($('#txtAppraiserAmount').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;

                //entity["msnfp_amount_receipted"] = eligibleAmount;
                entity["msnfp_amount_nonreceiptable"] = advantage;
                entity["msnfp_amount"] = amountOfGift;

                //if (amountOfGift > eligibleAmount)
                //    entity["msnfp_amount_nonreceiptable"] = amountOfGift - eligibleAmount;
                //else
                //    entity["msnfp_amount_nonreceiptable"] = 0;

                if (amountOfGift > advantage)
                    entity["msnfp_amount_receipted"] = amountOfGift - advantage;
                else
                    entity["msnfp_amount_receipted"] = 0;

                if (!isNullOrUndefined(selectedAppraiser)) {
                    entity["msnfp_appraiser_postalcode"] = selectedAppraiser.address1_postalcode;
                    if (selectedAppraiserAccount)
                        entity["msnfp_appraiser_lookup_account@odata.bind"] = "/accounts(" + selectedAppraiser.accountid + ")";
                    else
                        entity["msnfp_appraiser_lookup_contact@odata.bind"] = "/contacts(" + selectedAppraiser.contactid + ")";
                }
            }

            else if (paymentType == PaymentType.Stock) {
                entity["msnfp_depositdate"] = DSTOffsetYYYYMMDD($('#txtDateofSale').val());
                entity["msnfp_securities_totalprior"] = !isNullOrUndefined($('#txtAmountPriorSale').val()) ? parseFloat($('#txtAmountPriorSale').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                entity["msnfp_securities_gainorloss"] = !isNullOrUndefined($('#txtGainLossAmount').val()) ? parseFloat($('#txtGainLossAmount').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                entity["msnfp_securities_shares"] = !isNullOrUndefined($('#txtNoOfShares').val()) ? parseInt($('#txtNoOfShares').val()) : 0;
                entity["msnfp_securities_stockprice"] = !isNullOrUndefined($('#txtCostPerStock').val()) ? parseFloat($('#txtCostPerStock').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                entity["msnfp_stocksymbol"] = $('#txtStockSymbol').val();
                entity["msnfp_transactiondescription"] = $('#txtStockDescription').val();

                var stockAdvantageAmount = !isNullOrUndefined($('#txtStockAdvantageAmount').val()) ? parseFloat($('#txtStockAdvantageAmount').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                //var eligibleAmount = !isNullOrUndefined($('#txtStockEligibleAmount').val()) ? parseFloat($('#txtStockEligibleAmount').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;
                var amountOfGift = !isNullOrUndefined($('#txtStockAmount').val()) ? parseFloat($('#txtStockAmount').val().replace(currencySymbol, '').replace(/,/g, '')) : 0;

                entity["msnfp_amount_nonreceiptable"] = stockAdvantageAmount;
                entity["msnfp_amount"] = amountOfGift;
                entity["msnfp_amount_receipted"] = amountOfGift - stockAdvantageAmount;
            }


            // Save the parent payment schedule (if applicable):
            var parentPaymentSchedule = parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentscheduleid").getValue();
            if (!isNullOrUndefined(parentPaymentSchedule)) {
                entity["msnfp_Transaction_PaymentScheduleId@odata.bind"] = "/msnfp_paymentschedules(" + XrmUtility.CleanGuid(parentPaymentSchedule[0].id) + ")";
            }

            entity["msnfp_paymenttypecode"] = parseInt(paymentType);

            // Save/Create the credit card if necessary:
            if (paymentType == PaymentType.CreditDebit) {
                cardid = saveCreditCardDetails();
                console.log("CCId- " + cardid);
                // Then link it to this record:
                if (cardid != null) {
                    entity["msnfp_Transaction_PaymentMethodId@odata.bind"] = "/msnfp_paymentmethods(" + cardid + ")";
                }
                else {
                    alert('An error has occured when saving the credit/debit card details.');
                    return;
                }
            }
            // Save/Create the bank account if necessary:
            else if (paymentType == PaymentType.Bank) {
                bankid = saveBankDetails(paymentType);
                if (bankid != null) {
                    entity["msnfp_Transaction_PaymentMethodId@odata.bind"] = "/msnfp_paymentmethods(" + bankid + ")";
                }
                else {
                    alert('An error has occured when saving the bank details.');
                    return;
                }
            }

            if (createRelatedEntity) {
                relatedDonationEntity = entity;
            }

            // Now either create or update this gift record:
            if (isNullOrUndefined(currentID)) {
                var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions";
                XrmServiceUtility.CreateRecordAsync(qry, entity, saveSuccess, errorFailure);
            }
            else {
                var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
                XrmServiceUtility.UpdateRecordAsync(qry, entity, saveSuccess, errorFailure);
            }
        }
        else {
            //alert("A Validation Erorr Occured. Please Correct This Prior to Processing a Gift");
            // $('#lblErrorMessageText').html('');
            $('#lblErrorMessageText').append('<p>A Validation Error Occured. Please Correct This Prior to Processing a Gift</p>');
        }
    });

    $('#btnExclude').click(function () {
        isIncludeMembership = false;

        var giftAmount = finalAmount;
        var devidedAmount = 0;

        if (!isNullOrUndefined(selectedMembershipAmount)) {
            devidedAmount = selectedMembershipAmount / 12;
        }

        $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(finalAmount).toFixed(2)));
        $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(giftAmount + devidedAmount).toFixed(2)));
        $('#txtMembershipAmountCommitted').val(currencySymbol + addCommasonLoad(parseFloat(devidedAmount).toFixed(2)));
    });

    $('#btnInclude').click(function () {
        isIncludeMembership = true;

        var giftAmount = finalAmount;
        var devidedAmount = 0;

        if (!isNullOrUndefined(selectedMembershipAmount)) {
            devidedAmount = selectedMembershipAmount / 12;
        }

        $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(giftAmount - devidedAmount).toFixed(2)));
        $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(giftAmount).toFixed(2)));
        $('#txtMembershipAmountCommitted').val(currencySymbol + addCommasonLoad(parseFloat(devidedAmount).toFixed(2)));
    });

    $('#btnThankyou').click(function () {
        DownloadReceiptTemplete("Microsoft.Dynamics.CRM.msnfp_ActionTransactionThankYou");
    });

    $('#btnPrintReceipt').click(function () {
        DownloadReceiptTemplete("Microsoft.Dynamics.CRM.msnfp_ActionTransactionReceipt");
    });

    // Assign a donation to an Existing Pledge
    $('#btnLinkExistingPledge').click(function () {
        $('.divLinkExistingPledgeNull').find('.error-msg').hide();
        if (existingPledgeLoaded == false) {
            loadRelatedPledge();
            existingPledgeLoaded = true;
        }
        else {
            if (!noResult) {
                $('.existing-pledge').css("display", "block");
                $('.default-subsequent').hide();
            }
            else {
                $('.divLinkExistingPledgeNull').find('.error-msg').css("display", "block");
            }
        }
    });

    $('#btnLinkNoPledge').click(function () {
        console.log("btnLinkNoPledge existingPledgeLoaded: " + existingPledgeLoaded)
        $('.divLinkExistingPledgeNull').find('.error-msg').hide();
        if (existingPledgeLoaded == false) {
            loadRelatedPledge();
            existingPledgeLoaded = true;
        }
        else {
            if (!noResult) {
                $('.existing-pledge').css("display", "block");
                $('.default-subsequent').hide();
            }
            else {
                $('.divLinkExistingPledgeNull').find('.error-msg').css("display", "block");
            }
        }
    });

    $('#btnComplete').click(function () {
        $('.existing-pledge .error-msg').hide();
        var selectedPledge = $('input[name=radioExistingPledge]:checked').val();
        if (selectedPledge == undefined || selectedPledge == '') {
            $('.existing-pledge .error-msg').css("display", "block");
        }
        else {
            updateDonationPledge(selectedPledge, false);
        }
    });

    $('#btnRemoveLink').click(function () {
        var pledgeId = $('.spanExistingPledge').data('id');
        updateDonationPledge(pledgeId, true);
    });

    $('.spanExistingPledge').click(function () {
        var donationId = $(this).data('id');
        if (donationId != null) {
            var parameters = {};
            xrm.Utility.openEntityForm("msnfp_donorcommitment", donationId, parameters, true);
        }
    });


    $('#btnCancelDonation').click(function () {
        $('#lblErrorMessageText').html('');
        $('cancelDonation .error-msg-cancel').hide();
        var isValidate = true;
        $('.cancelDonation div.mandatory input, .cancelDonation div.mandatory textarea, .cancelDonation div.mandatory select').each(function () {
            var $this = $(this);
            if ($this.val() == '') {
                isValidate = false;
                return false;
            }
        });

        if (isValidate) {
            var message = "Selecting OK will Cancel This Recurring Donation";
            xrm.Utility.confirmDialog(message, CancelDonationOK, cancelClick);
        }
        else {
            $('.cancelDonation .error-msg-cancel').css("display", "flex");
            // $('.cancelDonation .error-msg-cancel').html('Please Enter a Cancellation Note');
            $('#lblerrormsg').html('Please Enter a Cancellation Note');
        }
    });

    //Back Button Cancel Donation
    $('#btnCancelDonationBack').click(function () {
        $('#lblErrorMessageText').html('');
        $('#txtCancelDonationNote').val('');
        $('#ddlCancelDonationReason').val(0);

        $('.default-subsequent').css("display", "flex");

        if (donationPledge.statuscode == 844060001) {
            $('.cancelDonation').hide();
        }
        $('.cancelDonation .error-msg-cancel').hide();
    });


    parent.Xrm.Page.getAttribute("msnfp_customerid").addOnChange(customerChange);
    parent.Xrm.Page.getAttribute("msnfp_relatedconstituentid").addOnChange(constituteChange);
    if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_receiptpreferencecode")))
        parent.Xrm.Page.getAttribute("msnfp_receiptpreferencecode").addOnChange(receiptPreferenceChange);

    $('#btnRecurringNextDonation').click(function () {
        $('#divUpdateNextRecurringDate').toggle();
    });

    $('#btnRecurringNextDonationAmount').click(function () {
        $('#divUpdateNextRecurringAmount').toggle();
    });

    // Validate the inputs before showing the final page on credit/bank transactions:
    $('#btnNextEnterCreditBank').click(function () {
        $('.manual-main .error-msg').hide();
        $('#lblerrormsg').html('');
        $('#lblErrorMessageText').html('');
        $("#divErrorMessageContainer").hide();
        var isValidate = true;

        if ($("#txtAmount").val() == '') {
            isValidate = false;
        }

        if (isValidate) {

            addRemoveBankValidation();
            addRemoveCardValidation();

            var selectedVal = $('input[name=paymentType]:checked').val();
            if (selectedVal == PaymentType.CreditDebit) {
                // Show new card fields:
                $('.donationCreditCard').show();
                $('.donationCreditCard .exists').css("display", "block");
                addRemoveCardValidation();
                showNextForCreditBank(false);

                // Make fields mandatory:
                $('#txtCardNumber').closest('div').addClass('mandatory');
                $('#txtCardName').closest('div').addClass('mandatory');
                $('#ddlCardType').closest('div').addClass('mandatory');
                $('#txtExpiry').closest('div').addClass('mandatory');

                console.log("Show process button, hide others");
                $('.donationCreditCard .notexists').hide();
                $('.divDepositDate').hide();
                $('.divAmount').hide();
                $('.divAmountNonReceiptable').hide();
                $('.divMembershipFields').hide();

            }
            else if (selectedVal == PaymentType.Bank) {
                // Show new bank fields:
                $('.donationBankAccount').show();
                $('.donationBankAccount .exists').css("display", "block");
                addRemoveBankValidation();
                showNextForCreditBank(false);

                // Make fields mandatory:
                $('#txtBankName').closest('div').addClass('mandatory');
                $('#txtAccountNumber').closest('div').addClass('mandatory');
                $('#txtRoutingNumber').closest('div').addClass('mandatory');
                $('#txtBankUserName').closest('div').addClass('mandatory');
                $('#ddlAccountType').closest('div').addClass('mandatory');

                console.log("Show process button, hide others");
                $('.donationBankAccount .notexists').hide();
                $('.divDepositDate').hide();
                $('.divAmount').hide();
                $('.divAmountNonReceiptable').hide();
                $('.divMembershipFields').hide();

                $('#ddlExistingBank').closest('div.notExists').removeClass('mandatory');
            }
        }
        else {
            $('.manual-main .error-msg').show();
            //$('.manual-main .error-msg').html('Please complete all mandatory fields.');
            $('#lblErrorMessageText').html('Please complete all mandatory fields.');
            $("#divErrorMessageContainer").show();
        }
    });

    $('#btnUpdateNextRecurringDate').click(function () {
        var donationPledge = {};
        if (!isNullOrUndefined($('#txtNextRecurringDate').val())) {
            var nextDonationDate = new Date($('#txtNextRecurringDate').val());
            donationPledge["msnfp_nextdonation"] = nextDonationDate.getFullYear() + '-' + (nextDonationDate.getMonth() + 1) + '-' + nextDonationDate.getDate();

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        } else {
            $('#lblNextDateError').css("display", "flex");
        }
    });

    $('#btnUpdateNextRecurringAmount').click(function () {
        var updateEntity = {};

        if (!isNullOrUndefined($('#txtNextRecurringAmount').val())) {
            var newAmount = parseFloat($('#txtNextRecurringAmount').val());
            var amountMembership = 0;

            if (!isNullOrUndefined(donationPledge))
                amountMembership = donationPledge.msnfp_amount_membership;

            updateEntity["msnfp_amount"] = newAmount;

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, updateEntity, reloadOnUpdateSuccess, errorFailure);
        } else {
            $('#lblNextRecurringAmountError').css("display", "flex");
        }
    });


    $('#txtTotalAmount').focus(function () {
        $(this).data("prevVal", $(this).val());
    });

    $('#txtTotalAmount').change(function () {
        var amount = !isNullOrUndefined($(this).val()) ? parseFloat($(this).val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;

        var hdnMembershipAmount = 0;
        if (!isNullOrUndefined($('#hdnMembershipAmount').val()))
            hdnMembershipAmount = parseFloat($('#hdnMembershipAmount').val());

        var membershipAmount = 0;
        if (!isNullOrUndefined($('#txtMembershipAmount').val()))
            membershipAmount = parseFloat($('#txtMembershipAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));

        var amountTax = 0;
        if (!isNullOrUndefined($('#txtAmountTax').val()))
            amountTax = parseFloat($('#txtAmountTax').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));

        var amountNonReceiptable = 0;
        if (!isNullOrUndefined($('#txtAmountNonReceiptable').val())) {
            amountNonReceiptable = parseFloat($('#txtAmountNonReceiptable').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
        }

        if (amount < membershipAmount + amountTax && configRecord.msnfp_tran_membership == true) {
            $(this).val($(this).data("prevVal"));
            $(this).addClass('red-border');
            $("#divErrorMessageContainer").show();
            //$("#lblErrorMessageText")[0].innerText = "The " + $('.divTotalAmount').find('label').text() + " cannot be less than the cost of the Membership.";
            $("#lblErrorMessageText").append('<p>The ' + $('.divTotalAmount').find('label').text() + ' cannot be less than the cost of the Membership.</p>');

        }
        else if (amount - membershipAmount - amountTax < amountNonReceiptable) {
            $(this).val($(this).data("prevVal"));
            $(this).addClass('red-border');
            $("#divErrorMessageContainer").show();
            //$("#lblErrorMessageText")[0].innerText = "The " + $('.divAmountNonReceiptable').find('label').text() + " cannot be greater than the donation amount.";
            $("#lblErrorMessageText").append('<p>The ' + $('.divAmountNonReceiptable').find('label').text() + ' cannot be greater than the donation amount.</p>');

        }
        else {
            $(this).removeClass('red-border');
            $("#divErrorMessageContainer").hide();
            $("#lblErrorMessageText").html('');

            if (parseFloat(amount) === 0 && parseFloat(amountNonReceiptable) > 0) {
                $('#txtAmount').val($('#txtAmountNonReceiptable').val());
                amount = amountNonReceiptable;
            }


            //$('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount + membershipAmount - amountNonReceiptable + amountTax).toFixed(2)));
            $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount - amountNonReceiptable - membershipAmount - amountTax).toFixed(2)));
            //$('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount - amountNonReceiptable).toFixed(2)));

            $(this).val(currencySymbol + addCommasonLoad(parseFloat(amount.toString()).toFixed(2)));

            if (!isNullOrUndefined(parentGiftAmount)) {
                if (amount > parentGiftAmount) {
                    //$("#lblErrorMessageText")[0].innerText =
                    //    "Amount should not exceed " + parentGiftAmount + " amount of the parent gift.";
                    $("#lblErrorMessageText").append('<p>Amount should not exceed ' + parentGiftAmount + ' amount of the parent gift.</p>');
                    $(this).addClass('red-border');
                    $("#divErrorMessageContainer").show();
                } else {
                    $("#lblErrorMessageText").html('');
                    $("#divErrorMessageContainer").hide();
                    $(this).removeClass('red-border');
                }
            }

            finalAmount = parseFloat($(this).val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
        }
    });

    $('#txtAmountNonReceiptable').focus(function () {
        $(this).data("prevVal", $(this).val());
    });
    $('#txtAmountNonReceiptable').change(function () {
        var amount = 0;
        var hdnAmountMembership = 0;
        var amountMembership = 0;
        var amountNonReceiptable = 0;
        var amountTax = 0;

        if (!isNullOrUndefined($('#txtTotalAmount').val())) {
            amount = parseFloat($('#txtTotalAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
        }

        if (!isNullOrUndefined($('#hdnMembershipAmount').val()))
            hdnAmountMembership = parseFloat($('#hdnMembershipAmount').val());

        if (!isNullOrUndefined($('#txtMembershipAmount').val()))
            amountMembership = parseFloat($('#txtMembershipAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));

        if (!isNullOrUndefined($('#txtAmountTax').val()))
            amountTax = parseFloat($('#txtAmountTax').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));

        amountNonReceiptable = !isNullOrUndefined($(this).val()) ? parseFloat($(this).val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
        $(this).val(currencySymbol + addCommasonLoad(parseFloat(amountNonReceiptable.toString()).toFixed(2)));

        if (parseFloat(amount) === 0 && parseFloat(amountNonReceiptable) > 0) {
            $('#txtTotalAmount').val($(this).val());
            amount = amountNonReceiptable;
        }

        if (amount - amountMembership - amountTax < amountNonReceiptable) {
            $(this).val($(this).data("prevVal"));
            $(this).addClass('red-border');
            $("#divErrorMessageContainer").show();
            //$("#lblErrorMessageText")[0].innerText = "The " +
            //    $('.divAmountNonReceiptable').find('label').text() +
            //    " cannot be greater than the donation amount.";
            $("#lblErrorMessageText").append('<p>The ' + $('.divAmountNonReceiptable').find('label').text() + ' cannot be greater than the donation amount.</p>');
        } else {
            //$('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount + amountMembership - amountNonReceiptable + amountTax).toFixed(2)));
            $('#txtAmount').val(currencySymbol +
                addCommasonLoad(parseFloat(amount - amountNonReceiptable - amountMembership - amountTax).toFixed(2)));
            //$('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount - amountNonReceiptable).toFixed(2)));

            if (amountNonReceiptable > 0) {
                $('#txtDescription').closest('div').addClass('mandatory');
                $('.description').css("display", "block");
            } else {

                var selectedVal = $('input[name=paymentType]:checked').val();

                if (selectedVal != PaymentType.InKind &&
                    selectedVal != PaymentType.Gift &&
                    selectedVal != PaymentType.Stock &&
                    selectedVal != PaymentType.Property) {
                    $('#txtDescription').closest('div').removeClass('mandatory');
                    $('.description').hide();
                }
            }
        }
    });

    $('#txtAmountTax').change(function () {
        var amount = 0;
        var amountMembership = 0;
        var amountNonReceiptable = 0;
        var amountTax = 0;

        if (!isNullOrUndefined($('#txtAmount').val())) {
            amount = parseFloat($('#txtAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
        }

        if (!isNullOrUndefined($('#hdnMembershipAmount').val()))
            amountMembership = parseFloat($('#hdnMembershipAmount').val());

        if (!isNullOrUndefined($('#txtAmountNonReceiptable').val()))
            amountNonReceiptable = parseFloat($('#txtAmountNonReceiptable').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));

        amountTax = !isNullOrUndefined($(this).val()) ? parseFloat($(this).val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
        $(this).val(currencySymbol + addCommasonLoad(parseFloat(amountTax.toString()).toFixed(2)));

        //$('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(amount - amountNonReceiptable).toFixed(2)));
    });

    $('#ddlRecurringEveryDayType').change(function () {
        if ($(this).val() != 844060002)//Months
        {
            $('#ddlRecurrenceStart').val(RecurrenceStart.CurrentDay);
            $('#ddlRecurrenceStart').attr('disabled', 'disabled');
        }
        else {
            $('#ddlRecurrenceStart').val("");
            $('#ddlRecurrenceStart').attr('disabled', false);
        }
    });


    $('#spanAddMembershipButton').click(function () {
        if (!isNullOrUndefined(selectedDonor)) {
            $("#divErrorMessageContainer").hide();
            $("#lblErrorMessageText").html('');
            loadGroupOrderList();
            membershipModal.style.display = "block";
            document.getElementById("divSelectMembership").style.display = "block";
        }
        else {
            $("#divErrorMessageContainer").show();
            //$("#lblErrorMessageText")[0].innerText = "Please select a Donor before trying to add a membership.";
            $("#lblErrorMessageText").append('<p>Please select a Donor before trying to add a membership.</p>');
        }
    });

    $('#divMembership').click(function () {
        if ($(this).hasClass('newMembership')) {
            if (!isNullOrUndefined(selectedDonor)) {
                loadGroupOrderList();
                membershipModal.style.display = "block";
                document.getElementById("divSelectMembership").style.display = "block";
            }
        }
    });

    // Here we add the selected membership category to this gift:
    $('#btnAddToGift').click(function () {
        var selectedTab = $('.tabview').find('a.tab-active');
        var tblID = selectedTab[0].title.replace('div', 'tbl');
        var dt = $('#' + tblID).DataTable();

        var selectedRow = dt.$(".chkMembership:checked", { "page": "all" });

        var previousTotalAmount = !isNullOrUndefined($('#txtTotalAmount').val()) ? parseFloat($('#txtTotalAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
        var amountNonReceiptable = !isNullOrUndefined($('#txtAmountNonReceiptable').val()) ? parseFloat($('#txtAmountNonReceiptable').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
        var amountReceiptable = !isNullOrUndefined($('#txtAmount').val()) ? parseFloat($('#txtAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
        var newTotalAmount;


        if (selectedRow.length == 0) {
            parent.Xrm.Page.getAttribute("msnfp_membershipcategoryid").setValue(null);
            $('#txtMembershipAmount').val(currencySymbol + '0.00');
            $('#txtMembershipAmountCommitted').val(currencySymbol + '0.00');
            $('#txtAmountTax').val(currencySymbol + '0.00');
            $('#hdnMembershipAmount').val(0);
            $('#hdnMembershipAmountTax').val(0);

            // recalculate the total amount and the amount receipted
            newTotalAmount = amountNonReceiptable + amountReceiptable;
            $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(newTotalAmount).toFixed(2)));

            $('.divRecurringNext #btnExclude').addClass('hide');
            $('.divRecurringNext #btnInclude').addClass('hide');
        }
        else {
            selectedRow.each(function (index, elem) {
                var isChecked = $(elem).prop('checked');
                var membershipID = $(elem).data('id');

                if (isChecked && !isNullOrUndefined(membershipID)) {
                    var query = "msnfp_membershipcategories?";
                    query += "$select=msnfp_membershipcategoryid,msnfp_amount,msnfp_amount_membership,msnfp_amount_tax,msnfp_name&";
                    query += "$filter=msnfp_membershipcategoryid eq " + membershipID;
                    var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query);

                    if (!isNullOrUndefined(result)) {
                        var membershipName = result[0].msnfp_name;

                        // Set the membership category to the selected category:
                        parent.Xrm.Page.getAttribute("msnfp_membershipcategoryid").setValue([{ id: membershipID, name: membershipName, entityType: "msnfp_membershipcategory" }]);

                        var baseCost = 0;
                        if (isNullOrUndefined(result[0].msnfp_amount) || parseFloat(result[0].msnfp_amount) == 0)
                            baseCost = 0;
                        else
                            baseCost = parseFloat(result[0].msnfp_amount);

                        var amountTax = 0;
                        if (isNullOrUndefined(result[0].msnfp_amount_tax) || parseFloat(result[0].msnfp_amount_tax) == 0)
                            amountTax = 0;
                        else
                            amountTax = parseFloat(result[0].msnfp_amount_tax);

                        var amountMembership = !isNullOrUndefined(result[0].msnfp_amount_membership) ? parseFloat(result[0].msnfp_amount_membership) : 0;
                        console.debug("baseCost:" + baseCost);
                        console.debug("amountTax:" + amountTax);
                        console.debug("amountMembership:" + amountMembership);


                        var previousMembershipAmount =
                            (!isNullOrUndefined($('#hdnMembershipAmount').val()) ? parseFloat($('#hdnMembershipAmount').val()) : 0) +
                            (!isNullOrUndefined($('#hdnMembershipAmountTax').val()) ? parseFloat($('#hdnMembershipAmountTax').val()) : 0);
                        //var previousTotalAmount = !isNullOrUndefined($('#txtTotalAmount').val()) ? parseFloat($('#txtTotalAmount').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;
                        //var amountNonReceiptable = !isNullOrUndefined($('#txtAmountNonReceiptable').val()) ? parseFloat($('#txtAmountNonReceiptable').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, "")) : 0;

                        //calculatedAmount = amountTotal - amountNonReceiptable;// + baseCost;
                        $('#txtMembershipAmount').val(currencySymbol + addCommasonLoad(parseFloat(amountMembership).toFixed(2)));
                        $('#txtMembershipAmountCommitted').val(currencySymbol + addCommasonLoad(parseFloat(amountMembership).toFixed(2)));
                        //$('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(baseCost).toFixed(2)));
                        $('#txtAmountTax').val(currencySymbol + addCommasonLoad(parseFloat(amountTax).toFixed(2)));
                        $('#hdnMembershipAmount').val(amountMembership);
                        $('#hdnMembershipAmountTax').val(amountTax);
                        selectedMembershipAmount = baseCost;

                        // recalculate the total amount and the amount receipted
                        newTotalAmount = previousTotalAmount - previousMembershipAmount + amountMembership + amountTax;
                        $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(newTotalAmount).toFixed(2)));
                        var newAmountReceipted = newTotalAmount - amountMembership - amountTax - amountNonReceiptable;
                        $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(newAmountReceipted).toFixed(2)));
                    }
                }
            });

            $('.divRecurringNext #btnExclude').removeClass('hide');
            $('.divRecurringNext #btnInclude').removeClass('hide');
        }

        $('#txtAmount').change();
        membershipModal.style.display = "none";
    });

    $('#btnMembershipCancel').click(function () {
        parent.Xrm.Page.getAttribute("msnfp_membershipcategoryid").setValue(null);
        $('#txtMembershipAmount').val(currencySymbol + '0.00');

        $('#txtMembershipAmountCommitted').val(currencySymbol + '0.00');

        $('#txtAmount').change();
        membershipModal.style.display = "none";
    });



    $('#txtRecurringStartDate').change(function () {
        parent.Xrm.Page.getAttribute('msnfp_bookdate').setValue(new Date($(this).val()));
    });


    $('#btnChequeDate').click(function () {
        if ($('#divUpdateChequeDate').is(':visible')) {
            $('#divUpdateChequeDate').css("display", "none");
        }
        else {
            $('#divUpdateChequeDate').css("display", "flex");
        }
    });

    $('#btnDepositDate').click(function () {
        if ($('#divUpdateDepositDate').is(':visible')) {
            $('#divUpdateDepositDate').css("display", "none");
        }
        else {
            $('#divUpdateDepositDate').css("display", "flex");
        }
    });

    $('#btnUpdateChequeDate').click(function () {
        var donationPledge = {};
        if ($('#txtNewChequeDate').val() != null) {
            donationPledge["msnfp_chequewiredate"] = new Date($('#txtNewChequeDate').val());

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    $('#btnUpdateDepositDate').click(function () {
        var donationPledge = {};
        if ($('#txtNewDepositDate').val() != null) {
            donationPledge["msnfp_depositdate"] = DSTOffsetYYYYMMDD($('#txtNewDepositDate').val());

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    $('#btnChequeNumber').click(function () {
        if ($('#divUpdateChequeNumber').is(':visible')) {
            $('#divUpdateChequeNumber').css("display", "none");
        }
        else {
            $('#divUpdateChequeNumber').css("display", "flex");
        }
    });

    $('#btnUpdateChequeNumber').click(function () {
        var donationPledge = {};

        if ($('#txtNewChequeNumber').val() != null) {
            donationPledge["msnfp_chequenumber"] = $('#txtNewChequeNumber').val();

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    //Wire Trasfer ///
    $('#btnWireDate').click(function () {
        $('#divUpdateWireDate').toggle();
    });

    $('#btnWireDepositDate').click(function () {
        $('#divUpdateWireDepositDate').toggle();
    });

    $('#btnWireNumber').click(function () {
        $('#divUpdateWireNumber').toggle();
    });

    $('#btnDepositDateAll').click(function () {
        $('#divUpdateDepositDateAll').toggle();
    });

    $('#btnUpdateWireDate').click(function () {
        var donationPledge = {};
        if ($('#txtNewWireDate').val() != null) {
            donationPledge["msnfp_chequewiredate"] = new Date($('#txtNewWireDate').val());

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    $('#btnUpdateWireDepositDate').click(function () {
        var donationPledge = {};
        if ($('#txtNewWireDepositDate').val() != null) {
            donationPledge["msnfp_depositdate"] = DSTOffsetYYYYMMDD($('#txtNewWireDepositDate').val());

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    $('#btnUpdateDepositDateAll').click(function () {
        var donationPledge = {};
        if (!isNullOrUndefined($('#txtNewDepositDateAll').val())) {
            donationPledge["msnfp_depositdate"] = DSTOffsetYYYYMMDD($('#txtNewDepositDateAll').val());

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        } else {
            $('#lblDepositDateAllError').css("display", "flex");
        }
    });

    $('#btnUpdateWireNumber').click(function () {
        var donationPledge = {};
        if ($('#txtNewWireNumber').val() != null) {
            donationPledge["msnfp_chequenumber"] = $('#txtNewWireNumber').val();

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    $('#lblCancelDonationReason').click(function () {
        $('#divUpdateCancelReason').toggle();
    });

    $('#btnUpdateCancelReason').click(function () {
        $('.lblUpdateCancelReason').hide();

        var donationPledge = {};

        if (isNullOrUndefined($('#txtUpdateCancelReason').val()) || $('#ddlUpdateCancelDonationReason').val() == "0") {
            $('.lblUpdateCancelReason').css("display", "flex");
            $('.lblUpdateCancelReason').html('Please Enter a Cancellation Note');
            return false;
        }
        else {
            donationPledge["msnfp_cancellationtext"] = $('#txtUpdateCancelReason').val();
            donationPledge["msnfp_cancelationreason"] = parseInt($('#ddlUpdateCancelDonationReason').val());

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
            XrmServiceUtility.UpdateRecordAsync(qry, donationPledge, reloadOnUpdateSuccess, errorFailure);
        }
    });

    parent.Xrm.Page.getAttribute("transactioncurrencyid").addOnChange(getCurrencySymbol);

    if (!isNullOrUndefined(parentGiftID)) {
        parent.Xrm.Page.getAttribute("msnfp_paymenttypecode").setValue(844060011);//Fund Trasfer
        hideOtherPaymentType();
        $("input[name=paymentType][value='" + 844060011 + "']").prop("checked", true);

        // Check the correct radio button:
        if (parentTypeWhenTransferGiftAmount == DonationType.Donation) {
            $("input[name=donationPledgeType][value='" + DonationType.Donation + "']").prop("checked", true);
        }
        else if (parentTypeWhenTransferGiftAmount == DonationType.Credit) {
            $("input[name=donationPledgeType][value='" + DonationType.Credit + "']").prop("checked", true);
        }

        $("input[name=donationPledgeType]").each(function () {
            $(this).attr("disabled", "disabled");
        });

        $("input[name=anonymousDonation]").each(function () {
            $(this).attr("disabled", "disabled");
        });

        $("input[name=giftSource]").each(function () {
            $(this).attr("disabled", "disabled");
        });
    }
    $.get(`${parent.Xrm.Page.context.getClientUrl()}/api/data/v9.1/usersettingscollection(${MissionFunctions.GetCurrentUserID()})`).then(
        (s) => { userSettings = s });
});


function showError(message) {
    var error = document.getElementById("errorMessage");
    error.innerText = message;
    error.style.display = "flex";

    setTimeout(function () {
        error.style.display = "none";
    }, 10000)
}


function receiptPreferenceChange() {
    if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_receiptpreferencecode"))) {
        var receiptPref = parent.Xrm.Page.data.entity.attributes.get("msnfp_receiptpreferencecode").getValue();

        $("input[name=receiptPreference][value='" + receiptPref + "']").prop("checked", true);
    }
}

function showHideControlsAndLabelChange() {

    // In Honour
    if (!isNullOrUndefined(configRecord.msnfp_transaction_inhonour) && configRecord.msnfp_transaction_inhonour)
        $('.divInHonour').css('display', 'flex');
    else
        $('.divInHonour').css('display', 'none');

    if (!isNullOrUndefined(configRecord.msnfp_label_inhonourmemoryof))
        $('.divInHonour').find('label').text(configRecord.msnfp_label_inhonourmemoryof);

    //console.debug("configRecord.msnfp_fmvlabel:" + configRecord.msnfp_fmvlabel);
    //if (!isNullOrUndefined(configRecord.msnfp_fmvlabel))
    //    $('.divAmountNonReceiptable').find('label').text(configRecord.msnfp_fmvlabel);


    // Address
    if (!isNullOrUndefined(configRecord.msnfp_transaction_showaddress) && configRecord.msnfp_transaction_showaddress) {
        $('.divAddressFields').css('display', 'flex');

        if (!isNullOrUndefined(configRecord.msnfp_label_line1))
            $('#txtAddressLine1').prev('label').text(configRecord.msnfp_label_line1);

        if (!isNullOrUndefined(configRecord.msnfp_label_line2))
            $('#txtAddressLine2').prev('label').text(configRecord.msnfp_label_line2);

        if (!isNullOrUndefined(configRecord.msnfp_label_line3))
            $('#txtAddressLine3').prev('label').text(configRecord.msnfp_label_line3);

        if (!isNullOrUndefined(configRecord.msnfp_label_city))
            $('#txtAddressCity').prev('label').text(configRecord.msnfp_label_city);

        if (!isNullOrUndefined(configRecord.msnfp_label_stateorprovince))
            $('#txtAddressProvince').prev('label').text(configRecord.msnfp_label_stateorprovince);

        if (!isNullOrUndefined(configRecord.msnfp_label_postalcode))
            $('#txtAddressPostalCode').prev('label').text(configRecord.msnfp_label_postalcode);

        if (!isNullOrUndefined(configRecord.msnfp_label_country))
            $('#txtAddressCountry').prev('label').text(configRecord.msnfp_label_country);
    }
    else {
        $('.divAddressFields').css('display', 'none');
    }

    // Email
    if (!isNullOrUndefined(configRecord.msnfp_transaction_showemail) && configRecord.msnfp_transaction_showemail)
        $('.divEmail').css('display', 'flex');
    else
        $('.divEmail').css('display', 'none');

    // Phone
    if (!isNullOrUndefined(configRecord.msnfp_transaction_showtelephone) && configRecord.msnfp_transaction_showtelephone) {
        $('.divPhone').css('display', 'flex');
        $('.divAltPhone').css('display', 'flex');
        $('.divMobilePhone').css('display', 'flex');
        if (!isNullOrUndefined(configRecord.msnfp_label_telephone1))
            $('.divPhone').find('label').text(configRecord.msnfp_label_telephone1);
        if (!isNullOrUndefined(configRecord.msnfp_label_telephone2))
            $('.divAltPhone').find('label').text(configRecord.msnfp_label_telephone2);
        if (!isNullOrUndefined(configRecord.msnfp_label_telephone3))
            $('.divMobilePhone').find('label').text(configRecord.msnfp_label_telephone3);
    }
    else {
        $('.divPhone').css('display', 'none');
        $('.divAltPhone').css('display', 'none');
        $('.divMobilePhone').css('display', 'none');
    }

    // Receipt Preference
    if (!isNullOrUndefined(configRecord.msnfp_tran_showreceiptpreference) && configRecord.msnfp_tran_showreceiptpreference) {
        $('#divReceiptPreference').css('display', 'flex');
    }
    else {
        $('#divReceiptPreference').css('display', 'none');
    }

    // Gift Details
    if (!isNullOrUndefined(configRecord.msnfp_label_donation))
        $('#radioDonation').next('label')[0].innerText = configRecord.msnfp_label_donation;

    if (!isNullOrUndefined(configRecord.msnfp_label_softcredit))
        $('#radioCredit').next('label')[0].innerText = configRecord.msnfp_label_softcredit;

    // Description
    if (!isNullOrUndefined(configRecord.msnfp_label_transactiondescription)) {
        $('.description').find('label').text(configRecord.msnfp_label_transactiondescription);
        $('.divDisableDescription').find('label').text(configRecord.msnfp_label_transactiondescription);
    }

    // Amounts
    if (!isNullOrUndefined(configRecord.msnfp_label_amount_receipted)) {
        $('.divAmount').find('label').text(configRecord.msnfp_label_amount_receipted);
        $('.divDisableAmount').find('label').text(configRecord.msnfp_label_amount_receipted);
        $('label[for="txtDisableTotal"]').text(configRecord.msnfp_label_amount_receipted);
        $('label[for="txtEligibleAmount"]').text(configRecord.msnfp_label_amount_receipted);
        $('label[for="txtStockEligibleAmount"]').text(configRecord.msnfp_label_amount_receipted);
    }

    if (!isNullOrUndefined(configRecord.msnfp_label_amount_nonreceiptable)) {
        $('.divAmountNonReceiptable').find('label').text(configRecord.msnfp_label_amount_nonreceiptable);
        $('.divDisableAmountNonreceiptable').find('label').text(configRecord.msnfp_label_amount_nonreceiptable);
        $('label[for="txtDisableAmountNonreceiptable"]').text(configRecord.msnfp_label_amount_nonreceiptable);
        $('label[for="txtAppraiserNonReceiptable"]').text(configRecord.msnfp_label_amount_nonreceiptable);
        $('label[for="txtStockAdvantageAmount"]').text(configRecord.msnfp_label_amount_nonreceiptable);
    }

    if (!isNullOrUndefined(configRecord.msnfp_label_amount)) {
        $('.divDisableTotal').find('label').text(configRecord.msnfp_label_amount);
        $('.divTotalAmount').find('label').text(configRecord.msnfp_label_amount);
        $('label[for="txtDisableAmount"]').text(configRecord.msnfp_label_amount);
        $('label[for="txtAppraiserAmount"]').text(configRecord.msnfp_label_amount);
        $('label[for="txtStockAmount"]').text(configRecord.msnfp_label_amount);
    }

    if (!isNullOrUndefined(configRecord.msnfp_label_amount_tax)) {
        $('.divAmountTax').find('label').text(configRecord.msnfp_label_amount_tax);
    }

    // Memberhip amount with add membership button
    if (!isNullOrUndefined(configRecord.msnfp_label_amount_membership)) {
        $('.divMembershipAmount').find('label')[0].innerHTML = '<span id="spanAddMembershipButton" class="badge badge-success">Add +</span>' + configRecord.msnfp_label_amount_membership;
        $('label[for="txtDisableAmountMembership"]').text(configRecord.msnfp_label_amount_membership);
    }
}

function getCurrencySymbol() {
    var currencyField = parent.Xrm.Page.data.entity.attributes.get("transactioncurrencyid").getValue();
    if (!isNullOrUndefined(currencyField)) {
        var currencyid = parent.Xrm.Page.data.entity.attributes.get("transactioncurrencyid").getValue()[0].id;
        var currencySelect = "transactioncurrencies?$select=transactioncurrencyid,currencysymbol,isocurrencycode"
        currencySelect += "&$filter=transactioncurrencyid eq " + XrmUtility.CleanGuid(currencyid);
        var currencyRec = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + currencySelect);
        if (!isNullOrUndefined(currencyRec))
            currencyRec = currencyRec[0];

        if (!isNullOrUndefined(currencyRec)) {
            currencySymbol = currencyRec.currencysymbol;
            isoCurrencyCode = currencyRec.isocurrencycode;
        }
    }
}

function GenerateEmailReciept() {
    var workflowName = null;
    if (donationPledge.statuscode == 844060004)//refund
        workflowName = "WF - Gift - Email Refund Receipt";
    else
        workflowName = "WF - Gift - Email Receipt";

    var workflowId = getWorkflowId(workflowName);
    if (workflowId != undefined && workflowId != null) {
        ExecuteWorkflow(currentID, workflowId);
    }
}
// Cancel Donation
function CancelDonationOK() {
    var entity = {};

    entity["statuscode"] = 844060001;
    entity["msnfp_cancelationreason"] = parseInt($('#ddlCancelDonationReason').val());
    entity["msnfp_cancellationtext"] = $('#txtCancelDonationNote').val();
    entity["msnfp_cancelledon"] = new Date();

    var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
    XrmServiceUtility.UpdateRecord(qry, entity);
    $('.cancelDonation-success').css("display", "flex");
    parent.Xrm.Page.data.refresh(true);

    var cancelDonationReasonNote = $("#ddlCancelDonationReason option:selected").text() + " " + $('#txtCancelDonationNote').val();

    $('#lblCancelDonationReason').html('');
    // $('#lblCancelDonationReason')[0].innerText = cancelDonationReasonNote;
    $('#lblCancelDonationReason').append('<p>' + cancelDonationReasonNote + '</p>');

    $('.default-subsequent').css("display", "flex");
    $('.cancelDonation').hide();
    $('.recurringDonation-success').hide();
    $('#btnRecurringCancelDonation').hide();
}

function errorFailure(message) {
    xrm.Utility.closeProgressIndicator();
    alert(message);
}


function showHidePaymentType(configRec) {
    if (isNullOrUndefined(configRec.msnfp_tran_cash) || configRec.msnfp_tran_cash == false) {
        $('#radioCash').next('label').remove();
        $('#radioCash').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_cash))
        $('#radioCash').next('label')[0].innerText = configRec.msnfp_label_cash;

    if (isNullOrUndefined(configRec.msnfp_tran_cheque) || configRec.msnfp_tran_cheque == false) {
        $('#radioCheque').next('label').remove();
        $('#radioCheque').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_cheque))
        $('#radioCheque').next('label')[0].innerText = configRec.msnfp_label_cheque;

    if (isNullOrUndefined(configRec.msnfp_tran_creditcard) || configRec.msnfp_tran_creditcard == false) {
        $('#radioCreditDebit').next('label').remove();
        $('#radioCreditDebit').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_creditcard))
        $('#radioCreditDebit').next('label')[0].innerText = configRec.msnfp_label_creditcard;

    if (isNullOrUndefined(configRec.msnfp_tran_bank) || configRec.msnfp_tran_bank == false) {
        $('#radioBankACH').next('label').remove();
        $('#radioBankACH').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_bankach))
        $('#radioBankACH').next('label')[0].innerText = configRec.msnfp_label_bankach;

    if (isNullOrUndefined(configRec.msnfp_tran_inkind) || configRec.msnfp_tran_inkind == false) {
        $('#radioInKind').next('label').remove();
        $('#radioInKind').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_inkind))
        $('#radioInKind').next('label')[0].innerText = configRec.msnfp_label_inkind;

    if (isNullOrUndefined(configRec.msnfp_tran_stock) || configRec.msnfp_tran_stock == false) {
        $('#radioStock').next('label').remove();
        $('#radioStock').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_securities))
        $('#radioStock').next('label')[0].innerText = configRec.msnfp_label_securities;

    if (isNullOrUndefined(configRec.msnfp_tran_property) || configRec.msnfp_tran_property == false) {
        $('#radioProperty').next('label').remove();
        $('#radioProperty').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_property))
        $('#radioProperty').next('label')[0].innerText = configRec.msnfp_label_property;

    if (isNullOrUndefined(configRec.msnfp_transaction_other) || configRec.msnfp_transaction_other == false) {
        $('#radioOther').next('label').remove();
        $('#radioOther').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_other))
        $('#radioOther').next('label')[0].innerText = configRec.msnfp_label_other;

    if (isNullOrUndefined(configRec.msnfp_tran_wireortransfer) || configRec.msnfp_tran_wireortransfer == false) {
        $('#radioWireTransfer').next('label').remove();
        $('#radioWireTransfer').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_wire))
        $('#radioWireTransfer').next('label')[0].innerText = configRec.msnfp_label_wire;

    if (isNullOrUndefined(configRec.msnfp_tran_extcreditcard) || configRec.msnfp_tran_extcreditcard == false) {
        $('#radioExtCreditCard').next('label').remove();
        $('#radioExtCreditCard').remove();
    }
    else if (!isNullOrUndefined(configRec.msnfp_label_extcreditcard))
        $('#radioExtCreditCard').next('label')[0].innerText = configRec.msnfp_label_extcreditcard;

    $('#radioFundTransfer').next('label').hide();
    $('#radioFundTransfer').hide();
}

function hideOtherPaymentType() {
    $('#radioCash').next('label').remove();
    $('#radioCash').remove();
    $('#radioCheque').next('label').remove();
    $('#radioCheque').remove();
    $('#radioCreditDebit').next('label').remove();
    $('#radioCreditDebit').remove();
    $('#radioBankACH').next('label').remove();
    $('#radioBankACH').remove();
    $('#radioInKind').next('label').remove();

    $('#radioInKind').remove();
    $('#radioGift').next('label').remove();
    $('#radioGift').remove();
    $('#radioStock').next('label').remove();
    $('#radioStock').remove();
    $('#radioProperty').next('label').remove();
    $('#radioProperty').remove();
    $('#radioWireTransfer').next('label').remove();
    $('#radioWireTransfer').remove();
    $('#radioFundTransfer').css("display", "flex");
    $('#radioFundTransfer').next('label').css("display", "flex");
}

function showHideMembership(isTrue) {
    var monthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    if (!isNullOrUndefined(selectedDonor)) {
        if (!isNullOrUndefined(selectedDonor._msnfp_primarymembershipid_value)) {
            var mQuery = "msnfp_memberships?";
            mQuery += "$select=msnfp_membershipid,msnfp_name,msnfp_enddate&";
            mQuery += "$filter=msnfp_membershipid eq " + selectedDonor._msnfp_primarymembershipid_value + "";

            var mRecord = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + mQuery);

            if (!isNullOrUndefined(mRecord[0].msnfp_enddate)) {
                var utcYear = new Date(mRecord[0].msnfp_enddate).getUTCFullYear();
                var utcMonth = new Date(mRecord[0].msnfp_enddate).getUTCMonth();
                var utcDate = new Date(mRecord[0].msnfp_enddate).getUTCDate();

                var nextDt = new Date(utcYear, utcMonth, utcDate);
                month = nextDt.getMonth();
                suffix = getSuffix(nextDt.getDate());

                $('#divMembership').find('.spanDate')[0].innerHTML = monthNames[month].toUpperCase() + " " + nextDt.getDate() + ", " + nextDt.getFullYear();
                $("#divMembership").css("background-image", "url('msnfp_/images/SolicitationAdd32')");
                var currentDt = new Date();
                currentDt = new Date(currentDt.getFullYear(), currentDt.getMonth(), currentDt.getDate());

                if (nextDt < currentDt) {
                    if (isTrue) {
                        if (donationPledge.length > 0 && donationPledge.statuscode != 844060000) {
                            $("#divMembership").css("border-bottom-color", "#FD3211");//red
                        }
                        else {
                            $("#divMembership").css("background-image", 'none');
                        }
                    }
                    else {
                        $("#divMembership").css("border-bottom-color", "#FD3211");//red
                        $("#divMembership").css("background-image", 'none');
                        $('#divMembership').removeClass('newMembership');
                        $($("#divMembership").find('.spanDate')[0]).css("color", "black");
                    }
                }
                else if (nextDt <= currentDt.setDate(currentDt.getDate() + 30)) {
                    if (isTrue) {
                        if (donationPledge.length > 0 && donationPledge.statuscode != 844060000) {
                            $("#divMembership").css("border-bottom-color", "#F49F25");//orange
                        }
                    }
                    else {
                        $("#divMembership").css("border-bottom-color", "#F49F25");//orange
                        $($("#divMembership").find('.spanDate')[0]).css("color", "black");
                    }
                }
                else if (nextDt > currentDt) {
                    if (isTrue) {
                        $("#divMembership").css("border-bottom-color", "#243a5e");//darkblue
                        if (isNullOrUndefined($('#divMembership').closest('a')[0])) {
                            $('#divMembership').addClass('newMembership');
                            $("#divMembership").wrap("<a id='newMembershipToDonation' href='javascript:void(0);' style='text-decoration: none;'></a>");
                        }
                    }
                    else {
                        $("#divMembership").css("background-image", 'none');
                        $('#divMembership').removeClass('newMembership');
                        $('#divMembership').closest('a#newMembershipToDonation').contents().unwrap();
                        $('#divSelectMembership').hide();
                    }
                }
            }

            $("#divMembership").find('.spanMembership')[0].innerHTML = mRecord[0].msnfp_name != null ? mRecord[0].msnfp_name : "";
        }
        else {
            $("#divMembership").find('.spanMembership')[0].innerHTML = "No Membership on File";
            $('#divMembership').find('.spanDate')[0].innerHTML = "";
            $($('#divMembership').find('.spanMembership')[0]).css('color', '000000');
            if (!isNullOrUndefined(donationPledge.statuscode) && donationPledge.statuscode != 844060000) {
                $("#divMembership").css("background-image", 'none');
                $('#divMembership').removeClass('newMembership');
            }
            else {
                $("#divMembership").css("border-bottom-color", "#FD3211");//red
                $("#divMembership").css("background-image", "url('msnfp_/images/SolicitationAdd32')");

                $('#divMembership').addClass('newMembership');
                if (isNullOrUndefined($('#divMembership').closest('a')))
                    $('#divMembership').wrap('<a id="newMembershipToDonation" href="javascript:void(0);" style="text-decoration: none;"></a>');
            }
        }
    }
}

function resetTimeZoneOffset(value) {
    var dtValue = new Date(value + ' UTC');

    var _userOffset = dtValue.getTimezoneOffset() * 60 * 1000;
    var _centralOffset = 6 * 60 * 60 * 1000;
    return _date = new Date(dtValue.getTime() - _userOffset + _centralOffset);
}

function reloadOnUpdateSuccess() {
    var parameters = {};
    xrm.Utility.openEntityForm("msnfp_transaction", currentID, parameters, true);
}

function cancelClick() {
}

function manageTab(current) {
    $(this).children('label').each(function () {
        if (this == current) {
            $(this).toggleClass('back-color');
        }
        else {
            $(this).removeClass('back-color');
        }
    });
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

function DownloadReceiptTemplete(requestName) {
    var entityId = parent.Xrm.Page.data.entity.attributes.get("msnfp_taxreceiptid");

    try {
        var organizationUrl = parent.Xrm.Page.context.getClientUrl();
        var data = {};
        var query = "msnfp_transactions(" + currentID + ")/" + requestName;
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
                    select = "annotations?$select=documentbody,createdon,subject,filename,notetext,_objectid_value,annotationid&$orderby=createdon desc&$top=1&$filter=(_objectid_value eq " + currentID + ")";
                    var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);
                    if (result != null && result.length > 0) {
                        download(base64ToArrayBuffer(result[0].documentbody), result[0].filename, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                        XrmServiceUtility.DeleteRecord(result[0].annotationid, 'annotations');
                    }
                }
            }
        };
        req.send(window.JSON.stringify(data));
    }
    catch (error) {
        console.log("Receipt Template Error: " + error);
    }

}


function customerChange() {

    var donor = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue();
    var expandCustomer;
    if (donor != null) {
        donorId = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue()[0].id;
        if (donor[0].entityType == "account") {
            select = "accounts(" + XrmUtility.CleanGuid(donorId) + ")?$select=accountid,name,address1_line1,address1_line2,address1_line3,address1_city,address1_stateorprovince,address1_country,address1_postalcode,telephone1,telephone2,telephone3,emailaddress1,_primarycontactid_value,_msnfp_primarymembershipid_value,msnfp_anonymity,msnfp_accounttype,msnfp_receiptpreferencecode";
            expandCustomer = "&$expand=primarycontactid($select=contactid,fullname,firstname,lastname)";
            selectedDonor = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expandCustomer);

            if (selectedDonor.msnfp_accounttype !== null && selectedDonor.msnfp_accounttype !== undefined && selectedDonor.msnfp_accounttype === 844060000) {
                parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").setValue(null);
                return;
            }

            if (selectedDonor.primarycontactid != null) {
                var primaryContactID = selectedDonor.primarycontactid.contactid;
                var primaryContactName = selectedDonor.primarycontactid.fullname;
                var primaryContactType = "contact";

                let cons = parent.Xrm.Page.getAttribute("msnfp_relatedconstituentid");
                if (!cons.getValue() && !isNullOrUndefined(cons))
                    cons.setValue([{ id: primaryContactID, name: primaryContactName, entityType: primaryContactType }]);
                $('#txtFirstName').val(selectedDonor.primarycontactid.firstname != null ? selectedDonor.primarycontactid.firstname : "");
                $('#txtLastName').val(selectedDonor.primarycontactid.lastname != null ? selectedDonor.primarycontactid.lastname : "");
            }

            selectedDonor.isAccount = true;
            $('#txtFirstName').closest('div').removeClass('mandatory');
            $('#txtLastName').closest('div').removeClass('mandatory');
            $('#txtOrganization').closest('div').addClass('mandatory');
            $('#txtOrganization').val(selectedDonor.name != null ? selectedDonor.name : "");
            $('#txtCardName').val(selectedDonor.name != null ? selectedDonor.name : "");
            $('#txtBankUserName').val(selectedDonor.name != null ? selectedDonor.name : "");
            $('#txtDisableDonorName').val(selectedDonor.name != null ? selectedDonor.name : "");
            $('#txtMobilePhone').val(selectedDonor.telephone3 != null ? selectedDonor.telephone3 : "");
        }
        else if (donor[0].entityType == "contact") {
            select = "contacts(" + XrmUtility.CleanGuid(donorId) + ")?$select=contactid,firstname,lastname,fullname,address1_line1,address1_line2,address1_line3,address1_city,address1_stateorprovince,address1_country,address1_postalcode,telephone1,telephone2,mobilephone,emailaddress1,_parentcustomerid_value,_msnfp_primarymembershipid_value,msnfp_anonymity,msnfp_receiptpreferencecode";
            selectedDonor = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select); //+ expand);

            if (selectedDonor.parentcustomerid_account == null) {
                $('#txtOrganization').val('');
            }

            selectedDonor.isAccount = false;

            $('#txtFirstName').val(selectedDonor.firstname != null ? selectedDonor.firstname : "");
            $('#txtLastName').val(selectedDonor.lastname != null ? selectedDonor.lastname : "");
            $('#txtCardName').val(selectedDonor.fullname != null ? selectedDonor.fullname : "");
            $('#txtBankUserName').val(selectedDonor.fullname != null ? selectedDonor.fullname : "");
            $('#txtDisableDonorName').val(selectedDonor.fullname != null ? selectedDonor.fullname : "");

            $('#txtFirstName').closest('div').addClass('mandatory');
            $('#txtLastName').closest('div').addClass('mandatory');
            $('#txtOrganization').closest('div').removeClass('mandatory');
            $('#txtMobilePhone').val(selectedDonor.mobilephone != null ? selectedDonor.mobilephone : "");
        }

        // Get the total transaction receipted amount according to the book date's year -- work for both contact and account
        var dateField = parent.Xrm.Page.data.entity.attributes.get("msnfp_bookdate").getValue();
        var utcYear = new Date(dateField).getUTCFullYear();

        $('#txtEmail').val(selectedDonor.emailaddress1 != null ? selectedDonor.emailaddress1 : "");
        $('#txtPhone').val(selectedDonor.telephone1 != null ? selectedDonor.telephone1 : "");
        $('#txtAltPhone').val(selectedDonor.telephone2 != null ? selectedDonor.telephone2 : "");
        $('#txtAddressLine1').val(selectedDonor.address1_line1 != null ? selectedDonor.address1_line1 : "");
        $('#txtAddressLine2').val(selectedDonor.address1_line2 != null ? selectedDonor.address1_line2 : "");
        $('#txtAddressLine3').val(selectedDonor.address1_line3 != null ? selectedDonor.address1_line3 : "");
        $('#txtAddressCity').val(selectedDonor.address1_city != null ? selectedDonor.address1_city : "");
        $('#txtAddressProvince').val(selectedDonor.address1_stateorprovince != null ? selectedDonor.address1_stateorprovince : "");
        $('#txtAddressPostalCode').val(selectedDonor.address1_postalcode != null ? selectedDonor.address1_postalcode.replace(/\s/g, '') : "");
        $('#txtAddressCountry').val(selectedDonor.address1_country != null ? selectedDonor.address1_country : "");
        $("input[name=anonymousDonation][value='" + selectedDonor.msnfp_anonymity + "']").prop("checked", true);

        if (!isNullOrUndefined(selectedDonor.msnfp_receiptpreferencecode)) {
            $("input[name=receiptPreference][value='" + selectedDonor.msnfp_receiptpreferencecode + "']").prop("checked", true);

            if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_receiptpreferencecode")))
                parent.Xrm.Page.getAttribute("msnfp_receiptpreferencecode").setValue(selectedDonor.msnfp_receiptpreferencecode);
        }

        loadBankDetail(donorId);
        loadCreditCardDetail(donorId);
        showHideMembership(true);

        var currentSelectedDonationType = parseInt($('input[name=donationPledgeType]:checked').val());
        if (currentSelectedDonationType == DonationType.Credit) {
            autoPopulateRelatedCustomerOnCreate();
        }
    }
    else {
        $('#txtFirstName').val('');
        $('#txtLastName').val('');
        $('#txtOrganization').val('');
        $('#txtEmail').val('');
        $('#txtPhone').val('');
        $('#txtAltPhone').val('');
        $('#txtMobilePhone').val('');
        $('#txtAddressLine1').val('');
        $('#txtAddressLine2').val('');
        $('#txtAddressLine3').val('');
        $('#txtAddressCity').val('');
        $('#txtAddressProvince').val('');
        $('#txtAddressPostalCode').val('');
        $('#txtAddressCountry').val('');
        $('#txtDonorName').val('');
        $('#txtInHonorMemoryOf').val('');
        $('#txtChequeNumber').val('');
        $('#txtDescription').val('');
        $('#txtApraiser').val('');

        $('#txtChequeDate').val('');
        $('#txtChequeNumber').val('');
        $('#txtCardName').val('');
        $('#txtCardNumber').val('');
        $('#txtExpiry').val('');
        $('#txtBankName').val('');
        $('#txtAccountNumber').val('');
        $('#txtRoutingNumber').val('');
        $('#txtBankUserName').val('');
        $("#radioCash").click();
        loadBankDetail(null);
        loadCreditCardDetail(null);
    }
}

function constituteChange() {
    var constitute = parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedconstituentid").getValue();
    if (constitute != null) {
        constituteId = parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedconstituentid").getValue()[0].id;
        select = "contacts?$select=contactid,fullname,firstname,lastname,emailaddress1&";
        filter = "$filter=contactid eq " + XrmUtility.CleanGuid(constituteId) + "";

        var donorResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);
        selectedConstitute = donorResult[0];
        if (!isNullOrUndefined(selectedDonor) && selectedDonor.isAccount) {
            $('#txtEmail').val(selectedConstitute.emailaddress1 != null ? selectedConstitute.emailaddress1 : "");
            $('#txtFirstName').val(selectedConstitute.firstname != null ? selectedConstitute.firstname : "");
            $('#txtLastName').val(selectedConstitute.lastname != null ? selectedConstitute.lastname : "");
        }
    }
}

// Populate the fields with existing data:
function loadExistingTransactionData() {
    selectQuery = "msnfp_transactions(" + currentID + ")?";
    donationPledge = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + selectQuery);

    mapRelatedDonationFields();

    $(donationPledge).each(function () {

        donorId = this.msnfp_customerid;

        // Set the msnfp_typecode radio button:
        if (this.msnfp_typecode == DonationType.Donation) {
            $("input[name=donationPledgeType][value='" + DonationType.Donation + "']").prop("checked", true);
            // Hide the related customer field:
            if (!isNullOrUndefined(xrm.Page.getControl("msnfp_relatedcustomerid"))) {
                parent.Xrm.Page.getControl("msnfp_relatedcustomerid").setVisible(false);
            }
        }
        else if (this.msnfp_typecode == DonationType.Credit) {
            $("input[name=donationPledgeType][value='" + DonationType.Credit + "']").prop("checked", true);
            // Show the related customer field:
            if (!isNullOrUndefined(xrm.Page.getControl("msnfp_relatedcustomerid"))) {
                parent.Xrm.Page.getControl("msnfp_relatedcustomerid").setVisible(true);
            }
        }

        $("input[name=paymentType][value='" + this.msnfp_paymenttypecode + "']").prop("checked", true);
        $("input[name=giftSource][value='" + this.msnfp_dataentrysource + "']").prop("checked", true);

        var anonymity = this.msnfp_anonymous;
        $("input[name=anonymousDonation][value='" + this.msnfp_anonymous + "']").prop("checked", true);



        if (!isNullOrUndefined(this.msnfp_receiptpreferencecode)) {
            $("input[name=receiptPreference][value='" + this.msnfp_receiptpreferencecode + "']").prop("checked", true);
        }

        $('.divUpdateDepositDateMain').css("display", "flex");
        if (!isNullOrUndefined(this.msnfp_depositdate)) {
            let splitDate = this.msnfp_depositdate.split("-");
            $('#btnDepositDateAll').val(splitDate[1] + "/" + splitDate[2] + "/" + splitDate[0]);
        }
        else {
            $('#btnDepositDateAll').val("Not Selected");
        }

        //Specify the Pledge Allocation
        relatedPledgeId = this._msnfp_donorcommitmentid_value;

        if (this.msnfp_bookdate != null && this.msnfp_bookdate != '') {
            var str = this.msnfp_bookdate.split('-');
            var _date = new Date(str[0] + '-' + str[1] + '-' + str[2]);
            var _utcdate = new Date(_date.getUTCFullYear(), _date.getUTCMonth(), _date.getUTCDate(), _date.getUTCHours(), _date.getUTCMinutes(), _date.getUTCSeconds());
            $('#txtDate').val(getFormattedDate(_utcdate));
        }

        $('#txtFirstName').val(this.msnfp_firstname);
        $('#txtLastName').val(this.msnfp_lastname);
        $('#txtOrganization').val(this.msnfp_organizationname);
        if (!isNullOrUndefined(this.msnfp_organizationname)) {
            $('#txtOrganization').closest('div').css("display", "flex");
        }
        else {
            $('#txtOrganization').closest('div').removeClass('mandatory');
            $('#txtOrganization').closest('div').hide();
        }
        $('#txtEmail').val(this.msnfp_emailaddress1);
        $('#emailReceipt').val(this.msnfp_emailaddress1);
        $('#txtPhone').val(this.msnfp_telephone1);
        $('#txtAltPhone').val(this.msnfp_telephone2);
        $('#txtMobilePhone').val(this.msnfp_mobilephone);
        $('#txtAddressLine1').val(this.msnfp_billing_line1);
        $('#txtAddressLine2').val(this.msnfp_billing_line2);
        $('#txtAddressLine3').val(this.msnfp_billing_line3);
        $('#txtAddressCity').val(this.msnfp_billing_city);
        $('#txtAddressProvince').val(this.msnfp_billing_stateorprovince);
        $('#txtAddressPostalCode').val(this.msnfp_billing_postalcode);
        $('#txtAddressCountry').val(this.msnfp_billing_country);
        $('#txtDonorName').val();

        if (!isNullOrUndefined(this.msnfp_tributecode)) {
            $('#txtInHonorMemoryOf').closest('div').css("display", "flex");
            $('#txtInHonorMemoryOf').val(this.msnfp_tributename);
        }
        else {
            $('#txtInHonorMemoryOf').closest('div').removeClass('mandatory');
            $('#txtInHonorMemoryOf').closest('div').hide();
        }

        $('#ddlTributeCode').val(this.msnfp_tributecode);

        //if (!isNullOrUndefined(this.msnfp_amount_receipted)) {
        //    if (this.msnfp_amount_receipted > 0) {
        //        $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_receipted).toFixed(2)));
        //        if (this.msnfp_paymenttypecode == PaymentType.InKind ||
        //            this.msnfp_paymenttypecode == PaymentType.Gift ||
        //            this.msnfp_paymenttypecode == PaymentType.Property) {
        //            $('#txtEligibleAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_receipted).toFixed(2)));
        //        }
        //    } else {
        //        $('#txtAmount').val(currencySymbol + '0.00');
        //        if (this.msnfp_paymenttypecode == PaymentType.InKind ||
        //            this.msnfp_paymenttypecode == PaymentType.Gift ||
        //            this.msnfp_paymenttypecode == PaymentType.Property) {
        //            $('#txtEligibleAmount').val(currencySymbol + '0.00');
        //        }
        //    }
        //}
        //else {
        //    $('#txtAmount').val(currencySymbol + '0.00');
        //    if (this.msnfp_paymenttypecode == PaymentType.InKind ||
        //        this.msnfp_paymenttypecode == PaymentType.Gift ||
        //        this.msnfp_paymenttypecode == PaymentType.Property) {
        //        $('#txtEligibleAmount').val(currencySymbol + '0.00');
        //    }
        //}

        if (!isNullOrUndefined(this.msnfp_amount_nonreceiptable)) {
            if (this.msnfp_amount_nonreceiptable > 0) {
                $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount).toFixed(2)));
                if (this.msnfp_paymenttypecode == PaymentType.InKind ||
                    this.msnfp_paymenttypecode == PaymentType.Gift ||
                    this.msnfp_paymenttypecode == PaymentType.Property) {
                    $('#txtAppraiserNonReceiptable').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_nonreceiptable).toFixed(2)));
                }
            } else {
                $('#txtAmount').val(currencySymbol + '0.00');
                if (this.msnfp_paymenttypecode == PaymentType.InKind ||
                    this.msnfp_paymenttypecode == PaymentType.Gift ||
                    this.msnfp_paymenttypecode == PaymentType.Property) {
                    $('#txtEligibtxtAppraiserNonReceiptableleAmount').val(currencySymbol + '0.00');
                }
            }
        }
        else {
            $('#txtAmount').val(currencySymbol + '0.00');
            if (this.msnfp_paymenttypecode == PaymentType.InKind ||
                this.msnfp_paymenttypecode == PaymentType.Gift ||
                this.msnfp_paymenttypecode == PaymentType.Property) {
                $('#txtAppraiserNonReceiptable').val(currencySymbol + '0.00');
            }
        }


        // Set the credit card drop down:
        if (!isNullOrUndefined(parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentmethodid").getValue())) {
            $('#ddlExistingCard').val(XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentmethodid").getValue()[0].id).toLowerCase());
        }

        // Load the Tax:
        if (!isNullOrUndefined(this.msnfp_amount_tax)) {
            if (this.msnfp_amount_tax > 0) {
                $('#txtAmountTax').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_tax).toFixed(2)));
            } else {
                $('#txtAmountTax').val(currencySymbol + '0.00');
            }
        }
        else {
            $('#txtAmountTax').val(currencySymbol + '0.00');
        }

        // Set the deposit date:
        if (!isNullOrUndefined(this.msnfp_receiveddate)) {
            $('#txtDepositDate').val(getFormattedDate(new Date(this.msnfp_receiveddate)));
        }

        // this.statuscode == 1 == Active
        if ((!isNullOrUndefined(configRecord.msnfp_tran_membership) && configRecord.msnfp_tran_membership == true) &&
            (!isNullOrUndefined(this.msnfp_amount_membership) && parseFloat(this.msnfp_amount_membership) > 0)) {
            $('#txtMembershipAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_membership).toFixed(2)));

            if (this.statuscode !== 1) {
                $('#txtDisableAmountMembership').closest('div').css("display", "flex");
            } else {
                $('#txtDisableAmountMembership').closest('div').hide();
            }

            $('#txtDisableAmountMembership').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_membership).toFixed(2)));

        }
        else {
            $('#txtDisableAmountMembership').closest('div').hide();
            $('#txtMembershipAmount').val(currencySymbol + '0.00');
        }

        if ((!isNullOrUndefined(configRecord.msnfp_tran_nonreceiptable) && configRecord.msnfp_tran_nonreceiptable == true) &&
            (!isNullOrUndefined(this.msnfp_amount_nonreceiptable) && parseFloat(this.msnfp_amount_nonreceiptable) > 0)) {
            $('#txtAmountNonReceiptable').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_nonreceiptable).toFixed(2)));

            if (this.statuscode !== 1) {
                $('#txtDisableAmountNonreceiptable').closest('div').css("display", "flex");
            } else {
                $('#txtDisableAmountNonreceiptable').closest('div').hide();
            }

            $('#txtDisableAmountNonreceiptable').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_nonreceiptable).toFixed(2)));

        }
        else {
            $('#txtDisableAmountNonreceiptable').closest('div').hide();
        }

        if (!isNullOrUndefined(this.msnfp_amount) && parseFloat(this.msnfp_amount) > 0) {
            if (this.statuscode !== 1) {
                $('#txtDisableTotal').closest('div').css("display", "flex");
            } else {
                $('#txtDisableTotal').closest('div').hide();

            }
            $('#txtDisableTotal').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount).toFixed(2)));
        }
        else {
            $('#txtDisableTotal').closest('div').hide();
        }

        if ((this.msnfp_paymenttypecode == 844060004 || this.msnfp_paymenttypecode == 844060005 || this.msnfp_paymenttypecode == 844060007 || this.msnfp_paymenttypecode == 844060006) ||
            ((!isNullOrUndefined(this.msnfp_amount_nonreceiptable) && parseFloat(this.msnfp_amount_nonreceiptable) > 0))) {  // Gift Type is In-Kind, Gift, Property, Stock 

            if (this.statuscode !== 1) {
                $('#txtDisableDescription').closest('div').css("display", "flex");
            } else {
                $('#txtDisableDescription').closest('div').hide();
            }
            $('#txtDisableDescription').val(this.msnfp_transactiondescription);
            $('#txtDescription').val(this.msnfp_transactiondescription);
            $('#txtAppraiserDescription').val(this.msnfp_transactiondescription);
        }
        else {
            $('#txtDisableDescription').closest('div').hide();
        }

        if (!isNullOrUndefined(this.msnfp_amount_membership_committed) && !isNullOrUndefined(donationPledge._msnfp_membershipinstanceid_value))
            $('#txtMembershipAmountCommitted').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount_membership_committed).toFixed(2)));
        else
            $('#txtMembershipAmountCommitted').val(currencySymbol + '0.00');
        if (!isNullOrUndefined(this.msnfp_amount)) {
            $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount).toFixed(2)));
            if (this.msnfp_paymenttypecode == PaymentType.InKind ||
                this.msnfp_paymenttypecode == PaymentType.Gift ||
                this.msnfp_paymenttypecode == PaymentType.Property) {
                $('#txtAppraiserAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_amount).toFixed(2)));
            }
        } else {
            $('#txtTotalAmount').val(currencySymbol + '0.00');
            if (this.msnfp_paymenttypecode == PaymentType.InKind ||
                this.msnfp_paymenttypecode == PaymentType.Gift ||
                this.msnfp_paymenttypecode == PaymentType.Property) {
                $('#txtAppraiserAmount').val(currencySymbol + '0.00');
            }

        }

        $('#txtChequeNumber').val(this.msnfp_chequenumber);

        $('.divUpdateChequeDepositDate').hide();
        $('.divUpdateWireDepositDate').hide();

        // Show the respective divs based on the gift type:
        if (this.msnfp_paymenttypecode == 844060001)//cheque
        {
            $('.divUpdateChequeDepositDate').css("display", "flex");
            $('.divUpdateDepositDateMain').hide();

            if (!isNullOrUndefined(this.msnfp_chequenumber)) {
                $('#btnChequeNumber').val(this.msnfp_chequenumber);
            }
            else {
                $('#btnChequeNumber').val("Not Entered");
            }
        }
        else if (this.msnfp_paymenttypecode == 844060009)//Wire Trasfer
        {
            $('.divUpdateWireDepositDate').css("display", "flex");
            $('.divUpdateDepositDateMain').hide();
            if (!isNullOrUndefined(this.msnfp_chequenumber)) {
                $('#btnWireNumber').val(this.msnfp_chequenumber);
            }
            else {
                $('#btnWireNumber').val("Not Entered");
            }
        }
        else if (this.msnfp_paymenttypecode == 844060002)//credit card
        {
            $('#divCreditCard').css("display", "flex");
            $('#divSourceIdentifier').css("display", "flex");
            $('#divTransactionIdentifier').css("display", "flex");

            if (!isNullOrUndefined(this.msnfp_sourceidentifier)) {
                $('#txtSourceIdentifier').val(this.msnfp_sourceidentifier);
            }
            if (!isNullOrUndefined(this.msnfp_transactionidentifier)) {
                $('#txtTransactionIdentifier').val(this.msnfp_transactionidentifier);
            }
        }
        else if (this.msnfp_paymenttypecode == 844060003)//bank account
        {
            $('#divBankAccount').css("display", "flex");
            $('#divSourceIdentifier').css("display", "flex");
            $('#divTransactionIdentifier').css("display", "flex");
            var bankAccount = parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentmethodid").getValue();
            if (!isNullOrUndefined(bankAccount)) {
                var bankAccountName = parent.Xrm.Page.data.entity.attributes.get("msnfp_transaction_paymentmethodid").getValue()[0].name;
                $('#txtBankAccount').val(bankAccountName);
            }
            if (!isNullOrUndefined(this.msnfp_sourceidentifier)) {
                $('#txtSourceIdentifier').val(this.msnfp_sourceidentifier);
            }
            if (!isNullOrUndefined(this.msnfp_transactionidentifier)) {
                $('#txtTransactionIdentifier').val(this.msnfp_transactionidentifier);
            }
        }
        else if (this.msnfp_paymenttypecode == 844060006) //stock/securities
        {
            $('#txtStockAmount').val(this.msnfp_amount);
            $('#txtStockEligibleAmount').val(this.msnfp_amount_receipted);
            $('#txtStockAdvantageAmount').val(this.msnfp_amount_nonreceiptable);
            $('#txtStockDescription').val(this.msnfp_transactiondescription);
        }

        var cancelReasonMessage;
        $(cancelReasonOptions).each(function () {
            if (this.Value == donationPledge.msnfp_cancelationreason) {
                cancelReasonMessage = this.Label.LocalizedLabels[0].Label;
            }
        });

        if (configRecord.msnfp_tran_receipt && this.msnfp_typecode != DonationType.Credit)
            $('#btnPrintReceipt').css('display', 'flex');
        else
            $('#btnPrintReceipt').css('display', 'none');

        if (configRecord.msnfp_transaction_thankyou)
            $('#btnThankyou').css('display', 'flex');
        else
            $('#btnThankyou').css('display', 'none');


        if (this.statecode == 1)//inactive
        {
            relatedPledgeId = this._msnfp_donorcommitmentid_value;
            disableFields();

            $('.subsequent-main').css("display", "flex");
            $('#donationInfo').hide();
            $('.cancelDonation-success').css("display", "flex");
            $('#lblCancelDonationReason').html('');
            //$('#lblCancelDonationReason')[0].innerText = cancelReasonMessage + " " + donationPledge.msnfp_cancellationtext;
            $('#lblCancelDonationReason').append('<p>' + cancelReasonMessage + ' ' + donationPledge.msnfp_cancellationtext + '</p>')
            $('.recurringDonation-success').hide();
            $('.recurringDonation-last').css("display", "flex");
            $('#btnRecurringCancelDonation').hide();
            $('.divLinkExistingPledgeNull').hide();
            $('.divUpdateChequeDepositDate').hide();
            $('.divUpdateWireDepositDate').hide();
            $('.divLinkExistingPledge').hide();
        }
        else if (this.statuscode == 844060000 || this.statuscode == 844060004 || this.statuscode == 844060001) {
            relatedPledgeId = this._msnfp_donorcommitmentid_value;

            $('.subsequent-main').css("display", "flex");
            $('#donationInfo').hide();

            if (this.statuscode == 844060001) {
                $('.cancelDonation-success').css("display", "flex");
                $('#lblCancelDonationReason').html('');
                //$('#lblCancelDonationReason')[0].innerText = cancelReasonMessage + " " + donationPledge.msnfp_cancellationtext;
                $('#lblCancelDonationReason').append('<p>' + cancelReasonMessage + ' ' + donationPledge.msnfp_cancellationtext + '</p>')
                $('.recurringDonation-success').hide();
                $('.recurringDonation-last').css("display", "flex");
                $('#btnRecurringCancelDonation').hide();
                $('.divLinkExistingPledgeNull').hide();
                $('.divUpdateChequeDepositdate').hide();
                $('.divUpdateWireDepositDate').hide();
                $('.divLinkExistingPledge').hide();

            }
            else {
                bindShowRefundInfo();

                if (relatedPledgeId != null) {
                    bindRelatedPledge();
                    $('.divLinkExistingPledge').css("display", "block");
                    $('.divLinkExistingPledgeNull').hide();
                }
                else {
                    $('.divLinkExistingPledge').hide();
                }

                bindChequeWireFields(donationPledge);
                nextDonationDate = donationPledge.msnfp_nextdonation;
                isActive = true;
            }
            donationPledgeTypechange();
            disableFields();

            $("#divMembership").css("background-image", 'none');
            $('#divMembership').removeClass('newMembership');
            $('#divMembership').closest('a#newMembershipToDonation').contents().unwrap();
        }
        else if (this.statuscode == 844060003 || this.statuscode == 1)//failed or active
        {
            paymentTypeChange();
        }
    });

}

function loadConfiguration(configID) {
    if (isNullOrUndefined(configID)) {
        configID = userConfigID;
    }
    //get Configuration record
    select = "msnfp_configurations?$select=msnfp_configurationid,msnfp_tran_anonymous,_msnfp_paymentprocessorid_value,";
    select += "msnfp_tran_softcredit,msnfp_sche_pledgeschedule,msnfp_comm_pledge,msnfp_tran_donation,";
    select += "msnfp_tran_cash,msnfp_tran_cheque,msnfp_tran_wireortransfer,msnfp_tran_creditcard,msnfp_tran_bank,msnfp_tran_inkind,msnfp_tran_property,msnfp_tran_stock,";
    select += "msnfp_sche_graceperiod,";
    select += "msnfp_transaction_other,msnfp_tran_extcreditcard,msnfp_label_bankach,msnfp_label_cash,msnfp_label_cheque,msnfp_label_creditcard,";
    select += "msnfp_label_donation,msnfp_label_extcreditcard,msnfp_label_inkind,msnfp_label_membership,msnfp_label_other,msnfp_tran_membership,";
    select += "msnfp_label_property,msnfp_label_recurringdonation,msnfp_label_securities,msnfp_label_softcredit,msnfp_label_wire,";
    select += "msnfp_tran_nonreceiptable,msnfp_tran_receipt,msnfp_transaction_thankyou,msnfp_transaction_email,msnfp_label_inhonourmemoryof,msnfp_transaction_inhonour,msnfp_transaction_showaddress,msnfp_transaction_showemail,";
    select += "msnfp_transaction_showtelephone,msnfp_label_telephone1,msnfp_label_telephone2,msnfp_label_telephone3,msnfp_label_line1,msnfp_label_line2,msnfp_label_line3,";
    select += "msnfp_label_city,msnfp_label_stateorprovince,msnfp_label_postalcode,msnfp_label_country,msnfp_tran_showreceiptpreference,msnfp_tran_showamounttax,msnfp_enforceappraiserlookup,";
    select += "msnfp_label_amount_receipted,msnfp_label_amount_nonreceiptable,msnfp_label_amount,msnfp_label_amount_tax,msnfp_label_amount_membership,msnfp_label_transactiondescription";
    select += "&$expand=msnfp_PaymentProcessorId($select=msnfp_testmode,msnfp_avsvalidation,msnfp_cvdvalidation)";
    select += "&$filter=msnfp_configurationid eq " + configID;
    var configresult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);
    configRecord = configresult[0];
}

function bindChequeWireFields(donationPledge) {
    let msnfp_chequewiredate = parent.Xrm.Page.getAttribute("msnfp_chequewiredate").getValue();
    let chequewiredate = msnfp_chequewiredate ? msnfp_chequewiredate.toISOString().substr(0, 10) : "Not Selected";
    let msnfp_depositdate = parent.Xrm.Page.getAttribute("msnfp_depositdate").getValue();
    let depositdate = msnfp_depositdate ? msnfp_depositdate.toISOString().substr(0, 10) : "Not Selected";

    if (donationPledge.msnfp_paymenttypecode == 844060001) { //cheque    
        $('.divUpdateChequeDepositDate').css("display", "flex");
        $('#btnChequeDate').val(chequewiredate);
        $('#btnDepositDate').val(depositdate);
    }
    else if (donationPledge.msnfp_paymenttypecode == 844060009) {
        $('.divUpdateWireDepositDate').css("display", "flex");
        $('#btnChequeDate').val(chequewiredate);
        $('#btnWireDepositDate').val(depositdate);
    }
}


function paymentTypeChange() {
    $("#divErrorMessageContainer").hide();
    var selectedVal = $('input[name=paymentType]:checked').val();
    var amountNonReceiptable = 0;
    $('.donationCheque, .divAppraiserInfo, .donationCreditCard, .donationBankAccount, .divStockInfo').hide();
    $('#txtChequeNumber').closest('div').removeClass('mandatory');

    if (!isNullOrUndefined($('#txtAmountNonReceiptable').val())) {
        amountNonReceiptable = parseFloat($('#txtAmountNonReceiptable').val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
    }

    if (amountNonReceiptable == 0) {
        $('.description').hide();
        $('#txtDescription').closest('div').removeClass('mandatory');
    }

    $('.noMatch').hide();

    addRemoveBankValidation();
    addRemoveCardValidation();
    addRemoveSecuritiesFieldsValidation(false);

    // Reset these from the credit/bank page:
    showNextForCreditBank(false);
    $('.divDepositDate').show();
    $('.divAmount').show();
    $('.divTotalAmount').show();
    $('.divAmountNonReceiptable').show();
    $('.divAmountNonReceiptable').css("display", "flex");
    $('.divMembershipFields').show();

    if (selectedVal == PaymentType.Cash) {
        $('#txtDepositDate').val('');
    }
    else if (selectedVal == PaymentType.Cheque) {
        $('.spnChequeDate').css("display", "flex");
        $('.spnChequeNumber').css("display", "flex");
        $('.spnWireNumber').hide();
        $('.spnWireDate').hide();
        $('.donationCheque').css("display", "block");
        $('#txtChequeNumber').closest('div').addClass('mandatory');
        $('#txtDepositDate').val('');
    }
    else if (selectedVal == PaymentType.WireTransfer) {
        $('.spnChequeDate').hide();
        $('.spnChequeNumber').hide();
        $('.spnWireNumber').show();
        $('.spnWireDate').css("display", "flex");
        $('.donationCheque').css("display", "block");
        $('#txtChequeNumber').closest('div').addClass('mandatory');
        $('#txtDepositDate').val('');
    }
    else if (selectedVal == PaymentType.CreditDebit) {
        $('.donationCreditCard .notexists').show();
        $('.donationCreditCard').css("display", "flex");
        addRemoveCardValidation();
        $('#ddlExistingCard').change();

        var dDate = parent.Xrm.Page.getAttribute('msnfp_bookdate').getValue();

        if (dDate != null && dDate != '') {
            $('#txtDepositDate').val(getFormattedDate(dDate));
        }

    }
    else if (selectedVal == PaymentType.Bank) {
        $('.donationBankAccount').css("display", "block");
        addRemoveBankValidation();
        $('#ddlExistingBank').change();
        $('#txtDepositDate').val('');
    }
    else if (selectedVal == PaymentType.InKind ||
        selectedVal == PaymentType.Gift ||
        selectedVal == PaymentType.Property) {
        $('#txtDescription').closest('div').removeClass('mandatory');
        $('.divAppraiserInfo').css("display", "block");
        $('#txtDepositDate').val('');

        $('.divDepositDate').hide();
        $('.divAmount').hide();
        $('.divTotalAmount').hide();
        $('.divAmountNonReceiptable').hide();
        $('.divMembershipFields').hide();
        $('.divProcess').hide();

    }
    else if (selectedVal == PaymentType.Stock) {
        $('#txtDescription').closest('div').removeClass('mandatory');
        $('.divStockInfo').css("display", "block");
        $('#txtDepositDate').val('');
        addRemoveSecuritiesFieldsValidation(true);

        $('.divDepositDate').hide();
        $('.divAmount').hide();
        $('.divTotalAmount').hide();
        $('.divAmountNonReceiptable').hide();
        $('.divMembershipFields').hide();
        $('.divProcess').hide();
    }

    // Recurring Donation
    addRemoveRecurringDonationFields();

    $('label.errorMSG').each(function () {
        $(this).text('');
    });
}

function donationPledgeTypechange() {
    $('input[name=paymentType]').each(function () {
        $(this).next('label').css("display", "flex");
    });

    var selectedVal = $('input[name=donationPledgeType]:checked').val();

    addRemoveRecurringDonationFields();

    $('#radioCreditDebit').next('label').css('border-radius', '0');
    $('#radioBankACH').next('label').css('border-radius', '0');

    $('#radioExtCreditCard').next('label').hide();
    $('#radioExtCreditCard').hide();
    $('#radioBACSDirectDebit').next('label').hide();
    $('#radioBACSDirectDebit').hide();

    $('.divAmountNonReceiptable').css("display", "flex");

    if (selectedVal == DonationType.Donation) {
        $('#txtMembershipAmount').closest('div').removeClass('mandatory');
        //$('#txtAmount').closest('div').addClass('mandatory');
        paymentTypeChange();
        showHideMembership(true);
        $('.divMembershipFields').css("display", "block");

        if (isNullOrUndefined(configRecord.msnfp_tran_membership) || configRecord.msnfp_tran_membership == false) {
            $('.divMembershipAmount').hide();
            $('.divRecurringNext #btnExclude').addClass('hide');
            $('.divRecurringNext #btnInclude').addClass('hide');
        }
        else {
            $('.divMembershipAmount').css("display", "flex");
            $('.divRecurringNext #btnExclude').removeClass('hide');
            $('.divRecurringNext #btnInclude').removeClass('hide');
        }

        $("#divPaymentTypes").show();
        // Hide the related customer field:
        if (!isNullOrUndefined(xrm.Page.getControl("msnfp_relatedcustomerid"))) {
            parent.Xrm.Page.getControl("msnfp_relatedcustomerid").setVisible(false);
        }

        $('.divMembershipCommittedAmount').hide();
        $('.divTotalAmount').css("display", "flex");
        $('.divDepositDate').css("display", "flex");

        $('#radioExtCreditCard').css("display", "flex");
        $('#radioExtCreditCard').next('label').css("display", "flex");

        $('#radioBACSDirectDebit').css("display", "flex");
        $('#radioBACSDirectDebit').next('label').css("display", "flex");
    }
    else if (selectedVal == DonationType.Credit) {
        $('#txtMembershipAmount').closest('div').removeClass('mandatory');
        //$('#txtAmount').closest('div').addClass('mandatory');
        paymentTypeChange();
        $('.divMembershipFields').css("display", "block");

        // Show the related customer field:
        if (!isNullOrUndefined(xrm.Page.getControl("msnfp_relatedcustomerid"))) {
            parent.Xrm.Page.getControl("msnfp_relatedcustomerid").setVisible(true);
        }
        // If empty, set the default to the donor (this can be changed afterwards by the user):
        // Only do this on create, otherwise we may blow out the donor:
        autoPopulateRelatedCustomerOnCreate();

        $('.divMembershipAmount').hide();
        $('.divRecurringNext #btnExclude').addClass('hide');
        $('.divRecurringNext #btnInclude').addClass('hide');

        $("#radioCash").click(); // Just to avoid credit card/bank options. Credit donation types don't need a type so the actual value is not used.
        $("#divPaymentTypes").hide();
        console.log("paymentType hide");

        $('.divMembershipCommittedAmount').hide();
        $('.divTotalAmount').css("display", "flex");
        $('.divDepositDate').css("display", "flex");

        $('#radioExtCreditCard').css("display", "flex");
        $('#radioExtCreditCard').next('label').css("display", "flex");

        $('#radioBACSDirectDebit').css("display", "flex");
        $('#radioBACSDirectDebit').next('label').css("display", "flex");
    }

    if (!isNullOrUndefined(donationPledge) && donationPledge.msnfp_paymenttypecode == 844060011)//Fund Transfer
    {
        $('#radioFundTransfer').css("display", "flex");
        $('#radioFundTransfer').next('label').css("display", "flex");
    }
    else {
        $('#radioFundTransfer').next('label').hide();
        $('#radioFundTransfer').hide();
    }
}


function autoPopulateRelatedCustomerOnCreate() {
    var donor = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue();
    var currentRelatedCustomer = parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedcustomerid").getValue();
    var donorName = "";

    // Only do this on create, otherwise we may blow out the donor:
    if (formType == FormType.Create) {
        if (!isNullOrUndefined(donor) && isNullOrUndefined(currentRelatedCustomer)) {
            var selectDonor = "";
            var filterDonor = "";
            if (donor[0].entityType == "account") {
                selectDonor = "accounts?";
                filterDonor = "$filter=accountid eq " + donor[0].id;
            }
            else {
                selectDonor = "contacts?";
                filterDonor = "$filter=contactid eq " + donor[0].id;
            }

            var resultDonor = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectDonor + filterDonor);

            if (resultDonor != null && donor[0].entityType == "account") {
                donorName = resultDonor[0].name;
            }
            else if (resultDonor != null && donor[0].entityType == "contact") {
                donorName = resultDonor[0].fullname;
            }

            parent.Xrm.Page.getAttribute("msnfp_relatedcustomerid").setValue([{ id: donor[0].id, name: donorName, entityType: donor[0].entityType }]);
        }
    }
}


function saveSuccess(recordID) {
    $('.payment-failed-msg').hide();
    $('#payment-failed-block').text('');

    if (recordID != null && recordID != '') {
        currentID = recordID;
        // Create the instance of the membership if there was a membership category added on create:
        if (!isNullOrUndefined(parent.Xrm.Page.getAttribute("msnfp_membershipcategoryid").getValue())) {
            createMembershipFromMembershipCategory(XrmUtility.CleanGuid(parent.Xrm.Page.data.entity.attributes.get("msnfp_membershipcategoryid").getValue()[0].id))
        }


        //var selectedDonationPledgeType = parseInt($('input[name=donationPledgeType]:checked').val());
        var paymentType = $('input[name=paymentType]:checked').val();
        if (paymentType == PaymentType.Bank) {

            select = "msnfp_transactions?$select=msnfp_transactionid,msnfp_bookdate,msnfp_transactionresult,statuscode,msnfp_paymenttypecode";
            filter = "&$filter=(msnfp_transactionid eq " + recordID + ")";
            var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);
            if (result != null) {
                result = result[0];
                if (result.statuscode == 844060000 || (result.statuscode == 1 && paymentType == PaymentType.Bank)) {
                    savedDonationSuccessfully();
                }
                else {
                    console.log("ERROR result.statuscode: " + result.statuscode);
                    console.log("ERROR result.msnfp_transactionresult: " + result.msnfp_transactionresult);
                    $('.payment-failed-msg').css("display", "flex");
                    $('#payment-failed-block').text(result.msnfp_transactionresult);
                    $('.donationBankAccount, .donationCreditCard, .divAmount, .divProcess').hide();
                }
            }
            xrm.Utility.closeProgressIndicator();
        }
        else {
            savedDonationSuccessfully();
        }

    }
    else {
        $('.payment-failed-msg').css("display", "flex");
        $('#payment-failed-block').text('An Error Occurred, please check the response logs for details.');
        $('.donationBankAccount, .donationCreditCard, .divAmount, .divProcess').hide();
        xrm.Utility.closeProgressIndicator();
    }
}

function savedDonationSuccessfully() {
    updatePrimaryContacttoAccount();
    xrm.Utility.closeProgressIndicator();

    var donationId = $(this).data('id');

    if (donationId != '') {
        var parameters = {};
        xrm.Utility.openEntityForm("msnfp_transaction", currentID, parameters, true);
    }
}

function createAnonymousContact() {
    var conatct = {};
    conatct["firstname"] = $('#txtFirstName').val();
    conatct["lastname"] = $('#txtLastName').val();
    var qry = XrmServiceUtility.GetWebAPIUrl() + "contacts";
    return XrmServiceUtility.CreateRecord(qry, conatct);
}


function saveBankDetails(paymentType) {
    if ($('.donationBankAccount .notExists').is(':visible')) {
        if ($('#ddlExistingBank').val() != '-1')
            return $('#ddlExistingBank').val();
    }
    var eEntity = {};
    if (selectedDonor.isAccount)
        eEntity["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + selectedDonor.accountid + ")";
    else
        eEntity["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + selectedDonor.contactid + ")";

    var accountNo = $('#txtAccountNumber').val();

    eEntity["msnfp_firstname"] = $('#txtFirstName').val();
    eEntity["msnfp_lastname"] = $('#txtLastName').val();
    eEntity["msnfp_emailaddress1"] = $('#txtEmail').val();
    eEntity["msnfp_telephone1"] = $('#txtPhone').val();
    eEntity["msnfp_billing_line1"] = $('#txtAddressLine1').val();
    eEntity["msnfp_billing_line2"] = $('#txtAddressLine2').val();
    eEntity["msnfp_billing_line3"] = $('#txtAddressLine3').val();
    eEntity["msnfp_billing_city"] = $('#txtAddressCity').val();
    eEntity["msnfp_billing_state"] = $("#txtAddressProvince").val();
    eEntity["msnfp_billing_country"] = $("#txtAddressCountry").val();
    eEntity["msnfp_billing_postalcode"] = $('#txtAddressPostalCode').val();
    eEntity["msnfp_bankname"] = $('#txtBankName').val();
    eEntity["msnfp_bankactnumber"] = accountNo;
    eEntity["msnfp_bankactrtnumber"] = $('#txtRoutingNumber').val();
    eEntity["msnfp_nameonfile"] = $('#txtBankUserName').val();
    eEntity["msnfp_banktypecode"] = $('#ddlAccountType').val();
    eEntity["msnfp_identifier"] = RandomGuid().substring(0, 8);
    eEntity["msnfp_name"] = $('#txtBankName').val() + " - " + accountNo.substr(accountNo.length - 4);
    // Type = Bank Account:
    eEntity["msnfp_type"] = 844060001;

    // Get the payment processor from the config file:
    eEntity["msnfp_PaymentProcessorId@odata.bind"] = "/msnfp_paymentprocessors(" + XrmUtility.CleanGuid(configRecord._msnfp_paymentprocessorid_value) + ")";

    // Gift type to go here - use same option set id. This is used to prevent masking of bank account numbers on EFT gift types.
    //console.log("Payment Type to Save: " + paymentType);

    var query = XrmServiceUtility.GetWebAPIUrl() + "msnfp_paymentmethods";
    return XrmServiceUtility.CreateRecord(query, eEntity);
}


function saveCreditCardDetails() {
    // If it exist already, return that value:
    if ($('.donationCreditCard .notExists').is(':visible')) {
        if ($('#ddlExistingCard').val() != '-1') {
            var select = "msnfp_paymentmethods?$select=msnfp_paymentmethodid,msnfp_ccbrandcode";
            var filter = "&$filter=(msnfp_paymentmethodid eq " + $('#ddlExistingCard').val() + ")";
            var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);
            if (result != null) {
                result = result[0];
                creditCardType = result.msnfp_ccbrandcode;
            }
            return $('#ddlExistingCard').val();
        }
    }

    var eEntity = {};

    if (selectedDonor.isAccount)
        eEntity["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + selectedDonor.accountid + ")";
    else
        eEntity["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + selectedDonor.contactid + ")";

    var cardNo = $('#txtCardNumber').val();
    eEntity["msnfp_firstname"] = $('#txtFirstName').val();
    eEntity["msnfp_lastname"] = $('#txtLastName').val();
    eEntity["msnfp_nameonfile"] = $("#txtCardName").val();
    eEntity["msnfp_emailaddress1"] = $('#txtEmail').val();
    eEntity["msnfp_telephone1"] = $('#txtPhone').val();
    eEntity["msnfp_billing_line1"] = $('#txtAddressLine1').val();
    eEntity["msnfp_billing_line2"] = $('#txtAddressLine2').val();
    eEntity["msnfp_billing_line3"] = $('#txtAddressLine3').val();
    eEntity["msnfp_billing_city"] = $('#txtAddressCity').val();
    eEntity["msnfp_billing_state"] = $("#txtAddressProvince").val();
    eEntity["msnfp_billing_country"] = $("#txtAddressCountry").val();
    eEntity["msnfp_billing_postalcode"] = $('#txtAddressPostalCode').val();
    eEntity["msnfp_cclast4"] = cardNo;
    eEntity["msnfp_ccexpmmyy"] = $('#txtExpiry').val();
    // Type = Credit card:
    eEntity["msnfp_type"] = 844060000;

    try {
        var expiryMMYY = $('#txtExpiry').val();
        var expiryMM = expiryMMYY.substring(0, 2);
        var expiryYY = expiryMMYY.substring(2, 4);

        // Here we need to get the date value of the above:
        d = new Date();
        d.setFullYear(parseInt(expiryYY) + 2000, parseInt(expiryMM), 0);
        eEntity["msnfp_expirydate"] = d.getFullYear() + "-" + (d.getMonth() + 1) + "-" + d.getDate();
    }
    catch (e) {
    }

    // Get the payment processor from the config file:
    eEntity["msnfp_PaymentProcessorId@odata.bind"] = "/msnfp_paymentprocessors(" + XrmUtility.CleanGuid(configRecord._msnfp_paymentprocessorid_value) + ")";

    eEntity["msnfp_ccbrandcode"] = parseInt($('#ddlCardType').val());
    creditCardType = parseInt($('#ddlCardType').val());
    var identifier = RandomGuid().substring(0, 8);
    eEntity["msnfp_identifier"] = identifier;
    eEntity["msnfp_name"] = $('#ddlCardType option:selected').text() + " - " + cardNo.substr(cardNo.length - 4);
    var recordId = "";
    var query = XrmServiceUtility.GetWebAPIUrl() + "msnfp_paymentmethods";

    recordId = XrmServiceUtility.CreateRecord(query, eEntity);
    console.log(recordId);
    return recordId;
}

//Specify the acknowledgement constituent
function updatePrimaryContacttoAccount() {
    if (selectedDonor.isAccount && selectedDonor.primarycontactid == null && selectedConstitute != null) {
        var account = {};
        account["primarycontactid@odata.bind"] = "/contacts(" + selectedConstitute.contactid + ")";
        var qry = XrmServiceUtility.GetWebAPIUrl() + "accounts(" + selectedDonor.accountid + ")";
        XrmServiceUtility.UpdateRecord(qry, account);
    }
}

//Validations
function disableFields() {
    $('.switch-field input, .donationFields input, .donationFields select, ' +
        '.headerFields input, .headerFields select').each(function () {
            $(this).attr("disabled", "disabled");
        });

    var dName = parent.Xrm.Page.data.entity.attributes.get("msnfp_firstname").getValue() + " " + parent.Xrm.Page.data.entity.attributes.get("msnfp_lastname").getValue();
    if (dName != null && parent.Xrm.Page.data.entity.attributes.get("msnfp_firstname").getValue() != null && parent.Xrm.Page.data.entity.attributes.get("msnfp_lastname").getValue() != null) {

        $('#txtDisableDonorName').val(dName);
        $('#txtDisableDonorName').closest('div').css("display", "flex");
    }
    else {
        $('#txtDisableDonorName').closest('div').hide();
    }

    if (parent.Xrm.Page.getAttribute("msnfp_amount_receipted").getValue() != null) {
        $('#txtDisableAmount').val(currencySymbol + addCommasonLoad(parseFloat(parent.Xrm.Page.getAttribute("msnfp_amount_receipted").getValue()).toFixed(2)));
    }
    else {
        $('#txtDisableAmount').val($('#txtAmount').val());
    }
    $('#txtDisableAmount').closest('div').css("display", "flex");
}

function validate() {
    var isValidate = true;

    //$("#lblErrorMessageText")[0].innerText = '';
    $("#lblErrorMessageText").html('');
    $("#divErrorMessageContainer").hide();

    var paymentType = $('input[name=paymentType]:checked').val();
    var campaignField = parent.Xrm.Page.data.entity.attributes.get("msnfp_originatingcampaignid").getValue();
    var dateField = parent.Xrm.Page.data.entity.attributes.get("msnfp_bookdate").getValue();
    var donorField = parent.Xrm.Page.data.entity.attributes.get("msnfp_customerid").getValue();

    var isAnonSelected = $('input[name=anonymousDonation]:checked').val();
    if (isAnonSelected != Anonymity.Yes && isNullOrUndefined(donorField)) {
        //$("#lblErrorMessageText")[0].innerText = "Could not retrieve the donor field.";
        $("#lblErrorMessageText").append('<p>Could not retrieve the donor field.</p>');

        $("#divErrorMessageContainer").show();
        isValidate = false;
    }


    if (isNullOrUndefined(campaignField)) {
        // $("#lblErrorMessageText")[0].innerText = "Could not retrieve the campaign field.";
        $("#lblErrorMessageText").append('<p>Could not retrieve the campaign field.</p>');
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }

    if (isNullOrUndefined(dateField)) {
        //$("#lblErrorMessageText")[0].innerText = "Could not retrieve date field.";
        $("#lblErrorMessageText").append('<p>Could not retrieve the date field.</p>');
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }

    if (isNullOrUndefined($('#txtFirstName').val())) {
        $("#lblErrorMessageText").append('<p>First Name is required.</p>');
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }
    if (isNullOrUndefined($('#txtLastName').val())) {
        $("#lblErrorMessageText").append('<p>Last Name is required.</p>');
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }


    if (!isNullOrUndefined($('#ddlTributeCode').val()) && isNullOrUndefined($('#txtInHonorMemoryOf').val())) {
        //$("#lblErrorMessageText")[0].innerText = "Could not retrieve date field.";
        $("#lblErrorMessageText").append('<p>In Honor/ Memory of is a required field.</p>');
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }


    if (paymentType != PaymentType.Stock.toString()
        && paymentType != PaymentType.InKind.toString()
        && paymentType != PaymentType.Property.toString()
        && paymentType != PaymentType.Gift.toString()
        && paymentType != PaymentType.Other.toString()) {
        if (isNullOrUndefined($('#txtTotalAmount').val()) || $('#txtTotalAmount').val() == "$0.00") {
            $("#lblErrorMessageText").append('<p>Total Amount must be greater than 0.</p>');
            $("#divErrorMessageContainer").show();
            isValidate = false;
        }
    }


    if (paymentType == PaymentType.Bank.toString()) {

        if (bankExists && isNullOrUndefined($('#ddlExistingBank').val())) {
            $("#lblErrorMessageText").append('<p>Existing Bank is required.</p>');
            $("#divErrorMessageContainer").show();
            isValidate = false;
        }

         if (($('#ddlExistingBank').val() == "-1" || !bankExists)
            && (isNullOrUndefined($('#txtBankName').val()) || isNullOrUndefined($('#txtAccountNumber').val())
                || isNullOrUndefined($('#txtRoutingNumber').val()) || isNullOrUndefined($('#txtBankUserName').val())
                || (isNullOrUndefined($('#ddlAccountType').val()) || $('#ddlAccountType').val() == "0"))) {
            $("#lblErrorMessageText").append('<p>Bank Account Details are required.</p>');
            $("#divErrorMessageContainer").show();
            isValidate = false;
        }

    }
    if (paymentType == PaymentType.CreditDebit.toString()) {

        if (cardExists && isNullOrUndefined($('#ddlExistingCard').val())) {
            $("#lblErrorMessageText").append('<p>Existing Card is required.</p>');
            $("#divErrorMessageContainer").show();
            isValidate = false;
        }

        if (($('#ddlExistingCard').val() == "-1" || !cardExists)
            && (isNullOrUndefined($('#txtCardName').val()) || isNullOrUndefined($('#txtCardNumber').val())
            || isNullOrUndefined($('#txtExpiry').val())
            || (isNullOrUndefined($('#ddlCardType').val()) || $('#ddlCardType').val() == "0"))) {
            $("#lblErrorMessageText").append('<p>Card Details are required.</p>');
            $("#divErrorMessageContainer").show();
            isValidate = false;
        }

    }

    if (paymentType == PaymentType.Cheque.toString() || paymentType == PaymentType.WireTransfer.toString()
        && isNullOrUndefined($('#txtChequeNumber').val())) {
        $("#lblErrorMessageText").append('<p>Check/Wire details are required.</p>');
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }

    var giftAmountField = $("input#txtAmount");
    var giftAmount = parseFloat(giftAmountField.val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
    console.debug("giftAmount:" + giftAmount);

    var totalAmountField = $("input#txtTotalAmount");
    var totalAmount = parseFloat(totalAmountField.val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
    console.debug("totalAmount:" + totalAmount);

    var nonReceiptableAmountField = $("input#txtAmountNonReceiptable");
    var nonReceiptableAmount = 0;
    if (nonReceiptableAmountField.val().length > 0) {
        nonReceiptableAmount = parseFloat(nonReceiptableAmountField.val().replace(currencySymbol, '').replace(/[^\d\.\-]/g, ""));
    }
    console.debug("nonReceiptableAmount:" + nonReceiptableAmount);

    if (totalAmount < nonReceiptableAmount) {

        var amountNonReceiptableTxt = "Advantage";
        var totalAmountTxt = "Gift Amount";

        if (!isNullOrUndefined(configRecord.msnfp_label_amount_nonreceiptable)) {
            amountNonReceiptableTxt = configRecord.msnfp_label_amount_nonreceiptable;
        }

        if (!isNullOrUndefined(configRecord.msnfp_label_amount)) {
            totalAmountTxt = configRecord.msnfp_label_amount;
        }

        //$("#lblErrorMessageText")[0].innerText = amountNonReceiptableTxt + " cannot exceed the " + totalAmountTxt;
        $("#lblErrorMessageText").append('<p>' + amountNonReceiptableTxt + ' cannot exceed the ' + totalAmountTxt + '</p>')
        $("#divErrorMessageContainer").show();
        isValidate = false;
    }


    $('.manual-main div.mandatory input, .manual-main div.mandatory select').each(function () {
        var $this = $(this);
        if ($this.val() == '') {
            isValidate = false;
            $this.addClass('red-border');
            console.log("Mandatory field incomplete: " + $(this)[0].id);
        }
        else {
            $this.removeClass('red-border');
        }
    });


    if (paymentType == PaymentType.InKind || paymentType == PaymentType.Gift || paymentType == PaymentType.Property) {
        if (isNullOrUndefined($('#txtAppraiserAmount').val()) ||
            parseFloat($('#txtAppraiserAmount').val().replace(currencySymbol, '')) < 0) {
            // $("#lblAppraiserAmountError")[0].innerText = "Please enter valid amount.";
            $("#lblAppraiserAmountError").append('<p>Please enter valid amount.</p>');

            $('#txtAppraiserAmount').addClass('red-border');
            isValidate = false;
        }

        //if (isNullOrUndefined($('#txtEligibleAmount').val()) ||
        //    parseFloat($('#txtEligibleAmount').val().replace(currencySymbol, '')) < 0) {
        //    $("#lblAppraiserAmountError")[0].innerText = "Please enter valid amount.";
        //    $('#txtEligibleAmount').addClass('red-border');
        //    isValidate = false;
        //}

        if (isNullOrUndefined($('#txtAppraiserNonReceiptable').val()) ||
            parseFloat($('#txtAppraiserNonReceiptable').val().replace(currencySymbol, '')) < 0) {
            //$("#lblAppraiserAmountError")[0].innerText = "Please enter valid amount.";
            $("#lblAppraiserAmountError").append('<p>Please enter valid amount.</p>');
            $('#txtAppraiserNonReceiptable').addClass('red-border');
            isValidate = false;
        }

        if (isNullOrUndefined($("#txtAppraiserDescription").val())) {
            isValidate = false;
            // $("#lblAppraiserError")[0].innerText = "Please enter the Description.";
            $("#lblAppraiserError").append('<p>Please enter the Description.</p>');

            return;
        }
        if ($('#txtAdvDescription').closest('div').hasClass('mandatory') &&
            isNullOrUndefined($('#txtAdvDescription').val())) {
            isValidate = false;
            // $("#lblAppraiserError")[0].innerText = "Please enter the Advantage Description.";
            $("#lblAppraiserError").append('<p>Please enter the Advantage Description.</p>');

            return;
        }

        $('.divAppraiserInfo div.mandatory input').each(function () {
            var $this = $(this);
            if ($this.val() == '') {
                isValidate = false;
                $this.addClass('red-border');
            } else {
                $this.removeClass('red-border');
            }
        });
    }

    if (paymentType == PaymentType.Stock) {

        $('#divStockInfo div.mandatory input, #divStockInfo div.mandatory textarea').each(function () {
            var $this = $(this);
            if ($this.val() == '') {
                isValidate = false;
                $this.addClass('red-border');
            }
            else {
                $this.removeClass('red-border');
            }
        });
    }



    return isValidate;
}

function validateExpiry() {
    //$("#lblErrorMessageText")[0].innerText = '';
    $("#lblErrorMessageText").html('');
    $("#divErrorMessageContainer").hide();

    var currentDate = new Date();
    var isExpValidate = true;
    var cardExp = $('#txtExpiry').val();
    var mm = parseInt(cardExp.substring(0, 2));
    var yy = parseInt(cardExp.substring(cardExp.length - 2));
    var currentMonth = parseInt(currentDate.getMonth() + 1);
    var currentYear = parseInt(currentDate.getFullYear().toString().substr(-2));

    if (mm > 12 || mm <= currentMonth) {
        if (yy == currentYear && mm >= currentMonth) {
            isExpValidate = true;
        }
        else if (yy > currentYear && mm < 12) {
            isExpValidate = true;
        }
        else {
            // $("#lblErrorMessageText")[0].innerText = "Month value must be 1 to 12 and greater than current month.";
            $("#lblErrorMessageText").append('<p>Month value must be 1 to 12 and greater than current month.</p>');
            $("#divErrorMessageContainer").show();
            isExpValidate = false;
        }
    }

    if (yy < currentYear) {
        //$("#lblErrorMessageText")[0].innerText = "Year value must be greater than or equal to current year.";
        $("#lblErrorMessageText").append('<p>Year value must be greater than or equal to current year.</p>');
        $("#divErrorMessageContainer").show();
        isExpValidate = false;
    }
    return isExpValidate;
}

function checkIt(evt) {
    evt = (evt) ? evt : window.event;
    var charCode = (evt.which) ? evt.which : evt.keyCode;
    if (charCode > 31 && (charCode < 48 || charCode > 57)) {
        status = "This field accepts numbers only."
        return false;
    }
    status = "";
    return true;
}

// We only show this for credit/banks:
function showNextForCreditBank(shouldDisplay) {

    console.log("showNextForCreditBank? == " + shouldDisplay);

    if (shouldDisplay) {
        $(".divProcessNextEnterCreditBank").css("display", "flex");
        $(".divProcess").css("display", "none");
    }
    else {
        $(".divProcessNextEnterCreditBank").css("display", "none");
        $(".divProcess").css("display", "flex");
    }


    if (!$('#donationCreditCard exists').is(':visible') && !$('#donationCreditCard notExists').is(':visible')) {
        $('.donationCreditCard').css("margin-bottom", "0px");
    }
    else {
        $('.donationCreditCard').css("margin-bottom", "5px");
    }
}

function addRemoveBankValidation() {
    $('#ddlExistingBank').closest('div').removeClass('mandatory');
    $('#txtBankName').closest('div').removeClass('mandatory');
    $('#txtAccountNumber').closest('div').removeClass('mandatory');
    $('#txtRoutingNumber').closest('div').removeClass('mandatory');
    $('#txtBankUserName').closest('div').removeClass('mandatory');
    $('#ddlAccountType').closest('div').removeClass('mandatory');

    var selectedVal = $('input[name=paymentType]:checked').val();
    if (selectedVal == PaymentType.Bank) {
        $('.noMatch').hide();
        var requiredDetail = false;
        if (bankExists) {
            $('#ddlExistingBank').closest('div').addClass('mandatory');
            if ($('#ddlExistingBank').val() == '-1')
                requiredDetail = true;
        }
        else {
            console.log("No banks exist");
            $('#ddlExistingBank').closest('div.notExists').removeClass('mandatory');
            $('#ddlExistingBank').closest('div.notExists').hide();
            showNextForCreditBank(true);
            requiredDetail = true;
        }

        if (requiredDetail) {
            $('#txtBankName').closest('div').addClass('mandatory');
            $('#txtAccountNumber').closest('div').addClass('mandatory');
            $('#txtRoutingNumber').closest('div').addClass('mandatory');
            $('#txtBankUserName').closest('div').addClass('mandatory');
            $('#ddlAccountType').closest('div').addClass('mandatory');
        }
    }
}

function addRemoveCardValidation() {
    $('#ddlExistingCard').closest('div').removeClass('mandatory');
    $('#txtCardNumber').closest('div').removeClass('mandatory');
    $('#txtCardName').closest('div').removeClass('mandatory');
    $('#ddlCardType').closest('div').removeClass('mandatory');
    $('#txtExpiry').closest('div').removeClass('mandatory');
    $('.noMatch').hide();

    var selectedVal = $('input[name=paymentType]:checked').val();
    if (selectedVal == PaymentType.CreditDebit) {
        var requiredDetail = false;
        if (cardExists) {
            $('#ddlExistingCard').closest('div.notExists').addClass('mandatory');
            if ($('#ddlExistingCard').val() == '-1')
                requiredDetail = true;
        }
        else {
            console.log("No cards exist");
            $('#ddlExistingCard').closest('div.notExists').removeClass('mandatory');
            $('#ddlExistingCard').closest('div.notExists').hide();
            showNextForCreditBank(true);
            requiredDetail = true;
        }

        if (requiredDetail) {
            $('#txtCardNumber').closest('div').addClass('mandatory');
            $('#txtCardName').closest('div').addClass('mandatory');
            $('#ddlCardType').closest('div').addClass('mandatory');
            $('#txtExpiry').closest('div').addClass('mandatory');
        }
    }
}

function loadBankDetail(donorId) {
    if (!isNullOrUndefined(donorId)) {
        select = "msnfp_paymentmethods?$select=msnfp_type,msnfp_bankname,msnfp_name,createdon&$orderby=createdon desc&$filter=(statuscode eq 1 and _msnfp_customerid_value eq " + XrmUtility.CleanGuid(donorId) + " and msnfp_type eq 844060001)"; // Filter out cards by type
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);

        if (result != null && result.length > 0) {
            bankExists = true;
            $('.donationBankAccount .exists').hide();
            $('.donationBankAccount .notExists').css("display", "flex");

            //Add Validation
            addRemoveBankValidation();

            $('#ddlExistingBank').find('option').remove().end().append('<option value="">--Select--</option>');
            $.each(result, function (i, item) {
                try {
                    // If this is a new gift this will fail:
                    var bankAccountName = parent.Xrm.Page.data.entity.attributes.get("msnfp_Transaction_PaymentMethodId").getValue()[0].name;
                    if (bankAccountName == item.msnfp_name) {
                        $('#ddlExistingBank').
                            append('<option value="' + item.msnfp_paymentmethodid + '" selected="selected">' + item.msnfp_title + '</option>');
                    }
                    else {
                        $('#ddlExistingBank').
                            append($('<option>',
                                {
                                    value: item.msnfp_paymentmethodid,
                                    text: item.msnfp_name
                                }));
                    }
                }
                catch (e) {
                    $('#ddlExistingBank').
                        append($('<option>',
                            {
                                value: item.msnfp_paymentmethodid,
                                text: item.msnfp_name
                            }));
                }
            });

            $('#ddlExistingBank').append('<option value="-1">New Bank Account</option>');
        }
        else {
            bankExists = false;
            addRemoveBankValidation();
        }
    }
    else {
        $('#ddlExistingBank').find('option').remove().end().append('<option value="">--Select--</option>');
    }
}


function loadCreditCardDetail(donorId) {
    if (!isNullOrUndefined(donorId)) {

        select = "msnfp_paymentmethods?$select=msnfp_type,msnfp_cclast4,msnfp_name,createdon,_msnfp_paymentprocessorid_value&$orderby=createdon desc&$filter=(statuscode eq 1 and _msnfp_customerid_value eq " + XrmUtility.CleanGuid(donorId) + " and msnfp_type eq 844060000)"; // Filter out banks by type
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select);

        if (result != null && result.length > 0) {
            cardExists = true;
            $('.donationCreditCard .exists').hide();
            $('.donationCreditCard .notExists').css("display", "flex");

            //Add Validation
            addRemoveCardValidation();

            $('#ddlExistingCard').find('option').remove().end().append('<option value="">--Select--</option>');
            $.each(result, function (i, item) {

                // Here we assign the payment processor name to the on hover of a credit card selection (useful in systems with multiple payment gateways).
                let paymentProcessorName = "";
                if (!isNullOrUndefined(item._msnfp_paymentprocessorid_value)) {
                    paymentProcessorName = getNameForPaymentProcessor(item._msnfp_paymentprocessorid_value);
                }
                $('#ddlExistingCard').
                    append('<option title="' + paymentProcessorName + '" value="' + item.msnfp_paymentmethodid + '">' + item.msnfp_name + '</option>');

            });
            $('#ddlExistingCard').append('<option value="-1">New Credit Card</option>');
        }
        else {
            cardExists = false;
            addRemoveCardValidation();
        }
    }
    else {
        $('#ddlExistingCard').find('option').remove().end().append('<option value="">--Select--</option>');
    }
}

function getWorkflowId(workflowName) {
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
        console.log("Query: " + query);
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
                    xrm.Utility.alertDialog("Mail is now queued to be sent");
                }
                else {
                    //error callback
                    var error = JSON.parse(this.response).error;
                    console.log("ExecuteWorkflow error: " + error);
                }
            }
        };
        req.send(JSON.stringify(data));
    }
    catch (e) {
        throwError(functionName, e);
    }
}

//8. Assign a donation to an Existing Pledge
function loadRelatedPledge() {
    select = "msnfp_donorcommitments?$select=msnfp_donorcommitmentid,msnfp_bookdate,msnfp_name,msnfp_totalamount,msnfp_totalamount_balance,statecode";
    filter = "&$filter=statecode eq 0 and _msnfp_customerid_value eq " + customerId + "";
    var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);

    $(result).each(function () {
        var radioButton = "<input aria-label='Select/Unselect a Pledge' type='radio' value='" + this.msnfp_donorcommitmentid + "' name='radioExistingPledge' />";
        var date = new Date(this.msnfp_bookdate).getMonth() + 1 + '/' + new Date(this.msnfp_bookdate).getDate() + '/' + new Date(this.msnfp_bookdate).getFullYear();
        searchTable.row.add([
            radioButton,
            date,
            this.msnfp_name,
            currencySymbol + addCommasonLoad(parseFloat(this.msnfp_totalamount).toFixed(2)),
            formatCurrencyValue(this.msnfp_totalamount_balance)
        ]).draw(false);
    });

    if (result == undefined || result.length == 0) {
        $('.divLinkExistingPledgeNull').find('.error-msg').css("display", "flex");
        noResult = true;
    }
    else {
        $('.existing-pledge').css("display", "block");
        $('.default-subsequent').hide();
    }
}

function updateDonationPledge(pledgeId, isRemoved) {
    var donationPledge = {};

    if (isRemoved) {
        // In order to clear a lookup field, we have to "delete" the reference in the field on the specified entity (setting it to null and updating will not work):
        try {
            var serverURL = parent.Xrm.Page.context.getClientUrl();
            var xhr = new XMLHttpRequest();

            xhr.open("DELETE", serverURL + "/api/data/v8.0/msnfp_transactions(" + XrmUtility.CleanGuid(currentID) + ")/msnfp_DonorCommitmentId/$ref", true);
            xhr.setRequestHeader("Accept", "application/json");
            xhr.setRequestHeader("Content-Type", "application/json; charset=utf-8");
            xhr.setRequestHeader("OData-MaxVersion", "4.0");
            xhr.setRequestHeader("OData-Version", "4.0");
            xhr.onreadystatechange = function () {
                if (this.readyState == 4) {
                    xhr.onreadystatechange = null;
                    if (this.status == 204) {
                        console.log('Field Value Deleted');
                        UpdatePledgeValuesAfterAssociateDissassociate(pledgeId);
                    }
                    else {
                        var error = JSON.parse(this.response).error;

                        console.log(error.message);
                    }
                }
            };
            xhr.send();
        }
        catch (e) {
            console.log("updateDonationPledge " + (e.message || e.description));
        }
    }
    else {
        donationPledge["msnfp_DonorCommitmentId@odata.bind"] = "/msnfp_transactions(" + pledgeId + ")";

        var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
        XrmServiceUtility.UpdateRecord(qry, donationPledge);
        relatedPledgeId = pledgeId;

        UpdatePledgeValuesAfterAssociateDissassociate(relatedPledgeId);
    }

    if (!isRemoved) {
        bindRelatedPledge();
        $('.divLinkExistingPledge').css("display", "block");
        $('.divLinkExistingPledgeNull').hide();
    }
    else {
        $('.divLinkExistingPledge').hide();
        $('.divLinkExistingPledgeNull').css("display", "block");
    }

    $('.existing-pledge').hide();
    $('.default-subsequent').css("display", "block");
}

function UpdatePledgeValuesAfterAssociateDissassociate(relatedPledgeId) {
    // Note that this function is called AFTER the update is done, so the query will contain this transaction as removed/added already in the loop below.
    var select = "msnfp_donorcommitments?$select=msnfp_donorcommitmentid,msnfp_name,msnfp_totalamount,msnfp_totalamount_balance,msnfp_totalamount_paid";
    var filter = "&$filter=msnfp_donorcommitmentid eq " + relatedPledgeId + "";
    var pledgeResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);

    if (!isNullOrUndefined(pledgeResult) && pledgeResult.length > 0) {
        // Get the current values:
        let msnfp_totalamount = pledgeResult[0].msnfp_totalamount;
        let msnfp_totalamount_paid = pledgeResult[0].msnfp_totalamount_paid;
        let moreThanOneLinked = false;

        // Now get all the linked completed transactions:
        select = "msnfp_transactions?$select=msnfp_transactionid,msnfp_amount,_msnfp_donorcommitmentid_value,statuscode";
        filter = "&$filter=(_msnfp_donorcommitmentid_value eq " + pledgeResult[0].msnfp_donorcommitmentid + " and statuscode eq 844060000)";
        var transactionResults = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);

        // We set the msnfp_totalamount_balance = msnfp_totalamount - msnfp_totalamount_paid (the totals of each linked transaction):
        if (!isNullOrUndefined(transactionResults)) {
            for (var i = 0; i < transactionResults.length; i++) {
                if (!moreThanOneLinked) {
                    // If there is more than one transaction linked, set it to 0 intially so we can append the amounts accordingly:
                    msnfp_totalamount_paid = 0;
                    moreThanOneLinked = true;
                }
                msnfp_totalamount_paid += transactionResults[i].msnfp_amount;
            }
        }

        // If this is the only one, reset the values:
        if (!moreThanOneLinked) {
            msnfp_totalamount_paid = 0;
        }

        console.log("msnfp_totalamount = " + msnfp_totalamount);
        console.log("msnfp_totalamount_paid = " + msnfp_totalamount_paid);

        // Update the donor commitment record:
        donorCommitmentRecord = {};
        donorCommitmentRecord["msnfp_totalamount"] = msnfp_totalamount;
        donorCommitmentRecord["msnfp_totalamount_paid"] = msnfp_totalamount_paid;

        var qryDC = XrmServiceUtility.GetWebAPIUrl() + "msnfp_donorcommitments(" + relatedPledgeId + ")";
        XrmServiceUtility.UpdateRecord(qryDC, donorCommitmentRecord);
    }
}

function successUpdateDonorCommitment() { console.log("Updated Donor Commitment"); }

function successDisassociate() { }

function addCommaToEvery3Digits(num) {
    var retval = "";

    if (!isNullOrUndefined(num))
        retval = num.toString().replace(/(\d)(?=(\d\d\d)+(?!\d))/g, "$1,");

    return retval;
}

function formatCurrencyValue(num) {
    var retval = "";

    if (!isNullOrUndefined(num))
        retval = currencySymbol + addCommasonLoad(parseFloat(num).toFixed(2));

    return retval;
}

function bindRelatedPledge() {
    select = "msnfp_donorcommitments?$select=msnfp_donorcommitmentid,msnfp_name,msnfp_totalamount";
    filter = "&$filter=msnfp_donorcommitmentid eq " + relatedPledgeId + "";
    var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + select + filter);

    if (result != null && result.length > 0) {

        if (isNullOrUndefined(currencySymbol)) {
            getCurrencySymbol();
        }

        result = result[0];
        var str = '';
        var name = result.msnfp_name;
        str = 'Pledge: ' + name;
        if (result.msnfp_totalamount != null) {
            //str += ' Amount: ' + currencySymbol + addCommaToEvery3Digits(result.msnfp_totalamount).toString().replace(/(\d)(?=(\d\d\d)+(?!\d))/g, "$1,");
            str += ' Amount: ' + currencySymbol + addCommasonLoad(parseFloat(result.msnfp_totalamount).toFixed(2));
        }

        $('.spanExistingPledge').html(str);
        $('.spanExistingPledge').data('id', result.msnfp_donorcommitmentid);
    }
}

function getLookupGuid(fieldName) {
    var field = parent.Xrm.Page.data.entity.attributes.get(fieldName);
    if (field != null && field.getValue() != null) {
        return parent.Xrm.Page.data.entity.attributes.get(fieldName).getValue()[0].id.replace('{', '').replace('}', '');
    }
    return null;
}

function bindShowRefundInfo() {
    searchTable = $('#searchResult').DataTable({
        "info": false,
        "bPaginate": true,
        "bFilter": false,
        "bLengthChange": false,
        "sPaginationType": "numbers",
        "iDisplayLength": 5,
        "columnDefs": [{
            orderable: false, targets: [0]
        }],
        "order": [[1, "asc"]]
    });

    ShowRefundInfo();
}

function ShowRefundInfo() {
    var statusreason = donationPledge.statuscode;
    if (statusreason == 844060004) {
        if (donationPledge.msnfp_daterefunded != null) {
            $('.refund-donation-sucess').css("display", "block");
            $('#txtRefundSucessOn').val(donationPledge.msnfp_daterefunded.toLocaleString().split("T")[0]);
            $('#txtRefundSuccessAmount').val(currencySymbol + addCommasonLoad(parseFloat(donationPledge.msnfp_ref_amount).toFixed(2)));
        }
    }
}

function getGiftType(giftType) {
    if (giftType == 844060000) { return 'Cash'; }
    if (giftType == 844060001) { return 'Cheque'; }
    if (giftType == 844060002) { return 'Credit / Debit Card'; }
    if (giftType == 844060003) { return 'Bank (Ach)'; }
    if (giftType == 844060004) { return 'In Kind'; }
    if (giftType == 844060005) { return 'Gift'; }
    if (giftType == 844060006) { return 'Stock'; }
    if (giftType == 844060007) { return 'Property'; }
    if (giftType == 844060009) { return 'WireTransfer'; }
    if (giftType == 844060008) { return 'Other'; }
    return '';
}


function getSuffix(n) {
    return n < 11 || n > 13 ? ['st', 'nd', 'rd', 'th'][Math.min((n - 1) % 10, 3)] : 'th'
}


function getNameForPaymentProcessor(paymentProcessorId) {
    var retval = "";

    if (isNullOrUndefined(paymentProcessorId)) {
        return retval;
    }

    var selectPP = "msnfp_paymentprocessors?$select=msnfp_paymentprocessorid,msnfp_name,msnfp_paymentgatewaytype";
    filterPP = "&$filter=(msnfp_paymentprocessorid eq " + XrmUtility.CleanGuid(paymentProcessorId) + ")";
    var resultPP = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectPP + filterPP);

    if (resultPP != null) {
        if (resultPP[0].msnfp_name != null) {
            retval = resultPP[0].msnfp_name;
        }
    }

    return retval;
}

// Convert a Pledge to a Donation
function loadParentDonationDetails(parentDonationId) {
    selectQuery = "msnfp_donorcommitments(" + parentDonationId + ")?";
    expandQuery = "$expand=msnfp_CustomerId_contact($select=contactid,fullname),msnfp_CustomerId_account($select=accountid,name),msnfp_ConstituentId($select=contactid,fullname)";
    var result = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + selectQuery + expandQuery);

    $(result).each(function () {
        var parentId = this.msnfp_donorcommitmentid;
        var parentName = this.msnfp_name;
        var parentType = "msnfp_donorcommitment";

        console.log("parentId: " + parentId);
        console.log("parentName: " + parentName);
        console.log("parentType: " + parentType);

        parent.Xrm.Page.getAttribute("msnfp_donorcommitmentid").setValue([{ id: parentId, name: parentName, entityType: parentType }]);

        $("input[name=donationPledgeType][value='" + DonationType.Donation + "']").prop("checked", true);
        $("input[name=donationPledgeType]").each(function () {
            $(this).attr("disabled", "disabled");
        });

        $("input[name=anonymousDonation][value='" + this.msnfp_anonymous + "']").prop("checked", true);
        $("input[name=giftSource][value='" + this.msnfp_dataentrysource + "']").prop("checked", true);

        if (this.msnfp_bookdate != null && this.msnfp_bookdate != '')
            var str = this.msnfp_bookdate.split('-');
        var _date = new Date(str[0] + '-' + str[1] + '-' + str[2]);
        var _utcdate = new Date(_date.getUTCFullYear(), _date.getUTCMonth(), _date.getUTCDate(), _date.getUTCHours(), _date.getUTCMinutes(), _date.getUTCSeconds());

        $('#txtDate').val(getFormattedDate(_utcdate));
        $('#txtFirstName').val(this.msnfp_firstname);
        $('#txtLastName').val(this.msnfp_lastname);
        $('#txtOrganization').val(this.msnfp_organizationname);
        $('#txtEmail').val(this.msnfp_emailaddress1);
        $('#txtPhone').val(this.msnfp_telephone1);
        $('#txtAltPhone').val(this.msnfp_telephone2);
        $('#txtMobilePhone').val(this.msnfp_mobilephone);
        $('#txtAddressLine1').val(this.msnfp_billing_line1);
        $('#txtAddressLine2').val(this.msnfp_billing_line2);
        $('#txtAddressLine3').val(this.msnfp_billing_line3);
        $('#txtAddressCity').val(this.msnfp_billing_city);
        $('#txtAddressProvince').val(this.msnfp_billing_stateorprovince);
        $('#txtAddressPostalCode').val(this.msnfp_billing_postalcode);

        if (!isNullOrUndefined(parent.Xrm.Page.getControl("msnfp_relatedconstituentid")) && !isNullOrUndefined(this.msnfp_ConstituentId)) {
            selectedConstitute = this.msnfp_ConstituentId;
            parent.Xrm.Page.getAttribute("msnfp_relatedconstituentid").setValue([{ id: this.msnfp_ConstituentId.contactid, name: this.msnfp_ConstituentId.fullname, entityType: "contact" }]);
        }

        $('#txtAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_totalamount).toFixed(2)));
        $('#txtTotalAmount').val(currencySymbol + addCommasonLoad(parseFloat(this.msnfp_totalamount).toFixed(2)));


        var selectRelated = "";
        var filterRelated = "";
        if (this.msnfp_CustomerId_account != null) {
            selectRelated = "accounts?";
            filterRelated = "$filter=accountid eq " + this._msnfp_customerid_value;
        }
        else if (this.msnfp_CustomerId_contact != null) {
            selectRelated = "contacts?";
            filterRelated = "$filter=contactid eq " + this._msnfp_customerid_value;
        }

        if (this.msnfp_CustomerId_account != null || this.msnfp_CustomerId_contact != null) {
            var resultRelatedDonor = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectRelated + filterRelated);

            if (resultRelatedDonor != null && this.msnfp_CustomerId_account != null) {
                var donorID = resultRelatedDonor[0].accountid;
                var donorName = resultRelatedDonor[0].name;
                var donorType = "account";
            }
            else if (resultRelatedDonor != null && this.msnfp_CustomerId_contact != null) {
                var donorID = resultRelatedDonor[0].contactid;
                var donorName = resultRelatedDonor[0].fullname;
                var donorType = "contact";
            }
            parent.Xrm.Page.getAttribute("msnfp_customerid").setValue([{ id: donorID, name: donorName, entityType: donorType }]);
            customerChange();
        }
    });
}

//14.	Recurring Donations
function addRemoveRecurringDonationFields() {
    $('.donatoinRecurring, .divRecurringNext').hide();
    var paymentType = $('input[name=paymentType]:checked').val();

    if ((formType === FormType.Update || formType === FormType.Disabled || formType === FormType.ReadOnly) && paymentType == PaymentType.Bank) {
        $('#btnProcess').hide();
    }
    else {
        $('#btnProcess').css("display", "block");
    }

    $('#txtRecurringStartDate, #txtRecurringInstance, #ddlRecurringEveryDayType').closest('div').removeClass('mandatory');
}

function bindInputEvents() {
    $('.recurringDonationLastList span').click(function () {
        var donationId = $(this).data('id');
        if (donationId != '') {
            var parameters = {};
            xrm.Utility.openEntityForm("msnfp_donationpledge", donationId, parameters, true);
        }
    });
}

function mapRelatedDonationFields() {
    relatedDonationEntity = {};

    relatedDonationEntity["msnfp_amount_membership_refunded"] = donationPledge.msnfp_amount_membership_refunded;

    if (donationPledge.msnfp_anonymous != null)
        relatedDonationEntity["msnfp_anonymous"] = donationPledge.msnfp_anonymous;

    relatedDonationEntity["msnfp_authorizationid"] = donationPledge.msnfp_authorizationid;

    if (donationPledge.msnfp_CustomerId_account != null)
        relatedDonationEntity["msnfp_CustomerId_account@odata.bind"] = "/accounts(" + donationPledge._msnfp_customerid_value + ")";
    else if (donationPledge.msnfp_CustomerId_contact != null)
        relatedDonationEntity["msnfp_CustomerId_contact@odata.bind"] = "/contacts(" + donationPledge._msnfp_customerid_value + ")";

    if (donationPledge._msnfp_relatedconstituentid_value != null)
        relatedDonationEntity["msnfp_RelatedConstituentId@odata.bind"] = "/contacts(" + donationPledge._msnfp_relatedconstituentid_value + ")";

    if (donationPledge._msnfp_originatingcampaignid_value != null)
        relatedDonationEntity["msnfp_originatingcampaignid@odata.bind"] = "/campaigns(" + donationPledge._msnfp_originatingcampaignid_value + ")";

    var date = new Date();
    relatedDonationEntity["msnfp_bookdate"] = date.getFullYear() + '-' + (date.getMonth() + 1) + '-' + date.getDate();

    if (donationPledge.msnfp_paymenttypecode != null)
        relatedDonationEntity["msnfp_paymenttypecode"] = donationPledge.msnfp_paymenttypecode;

    //if (donationPledge.msnfp_anonymous != null)
    //    relatedDonationEntity["msnfp_anonymous"] = donationPledge.msnfp_anonymous;

    if (donationPledge.msnfp_dataentrysource != null)
        relatedDonationEntity["msnfp_dataentrysource"] = donationPledge.msnfp_dataentrysource;

    relatedDonationEntity["msnfp_firstname"] = donationPledge.msnfp_firstname;
    relatedDonationEntity["msnfp_lastname"] = donationPledge.msnfp_lastname;

    if (donationPledge.msnfp_organizationname != null)
        relatedDonationEntity["msnfp_organizationname"] = donationPledge.msnfp_organizationname;

    relatedDonationEntity["msnfp_emailaddress1"] = donationPledge.msnfp_emailaddress1;
    relatedDonationEntity["msnfp_telephone1"] = donationPledge.msnfp_telephone1;
    relatedDonationEntity["msnfp_billing_line1"] = donationPledge.msnfp_billing_line1;
    relatedDonationEntity["msnfp_billing_line2"] = donationPledge.msnfp_billing_line2;
    relatedDonationEntity["msnfp_billing_line3"] = donationPledge.msnfp_billing_line3;
    relatedDonationEntity["msnfp_billing_city"] = donationPledge.msnfp_billing_city;
    relatedDonationEntity["msnfp_billing_stateorprovince"] = donationPledge.msnfp_billing_stateorprovince;
    relatedDonationEntity["msnfp_billing_postalcode"] = donationPledge.msnfp_billing_postalcode;
    relatedDonationEntity["msnfp_invoiceidentifier"] = "MIS" + RandomGuid().substring(0, 6).toUpperCase();

    if (donationPledge._msnfp_donationallocationid_value != null)
        relatedDonationEntity["msnfp_DonationAllocationid@odata.bind"] = "/msnfp_donationallocations(" + donationPledge._msnfp_donationallocationid_value + ")";

    relatedDonationEntity["msnfp_donorname"] = '';
    if (donationPledge.msnfp_donorname != null)
        relatedDonationEntity["msnfp_donorname"] = donationPledge.msnfp_donorname;

    if (donationPledge.msnfp_honorormemory != null)
        relatedDonationEntity["msnfp_honorormemory"] = donationPledge.msnfp_honorormemory;

    relatedDonationEntity["msnfp_appraiser"] = donationPledge.msnfp_appraiser;
    relatedDonationEntity["msnfp_giftdescription"] = donationPledge.msnfp_giftdescription;
    relatedDonationEntity["msnfp_chequenumber"] = donationPledge.msnfp_chequenumber;

    if (donationPledge.msnfp_chequewiredate != null)
        relatedDonationEntity["msnfp_chequewiredate"] = donationPledge.msnfp_chequewiredate;

    if (donationPledge._msnfp_bankaccountid_value != null)
        relatedDonationEntity["msnfp_bankaccountid@odata.bind"] = "/msnfp_bankaccounts(" + donationPledge._msnfp_bankaccountid_value + ")";

    if (donationPledge._msnfp_creditcardid_value != null)
        relatedDonationEntity["msnfp_creditcardid@odata.bind"] = "/msnfp_creditcards(" + donationPledge._msnfp_creditcardid_value + ")";

    relatedDonationEntity["msnfp_amount"] = donationPledge.msnfp_amount;
    relatedDonationEntity["msnfp_amountowing"] = donationPledge.msnfp_amountowing;
    relatedDonationEntity["msnfp_amount_membership"] = donationPledge.msnfp_amount_membership;
    relatedDonationEntity["msnfp_amount_transfer"] = donationPledge.msnfp_amount_transfer;
    relatedDonationEntity["msnfp_amount_membership_committed"] = donationPledge.msnfp_amount_membership_committed;
    relatedDonationEntity["msnfp_amountowing_membership"] = donationPledge.msnfp_amountowing_membership;
    relatedDonationEntity["msnfp_amountpaid_membership"] = donationPledge.msnfp_amountpaid_membership;
    relatedDonationEntity["msnfp_alert"] = donationPledge.msnfp_alert;

    if (donationPledge.msnfp_alert != null)
        relatedDonationEntity["msnfp_alert"] = donationPledge.msnfp_alert;

    relatedDonationEntity["msnfp_transactionresult"] = donationPledge.msnfp_transactionresult;

    if (donationPledge.msnfp_printed != null)
        relatedDonationEntity["msnfp_printed"] = donationPledge.msnfp_printed;

    if (donationPledge.msnfp_recurrencestart != null)
        relatedDonationEntity["msnfp_recurrencestart"] = donationPledge.msnfp_recurrencestart;

    if (donationPledge.msnfp_endondate != null)
        relatedDonationEntity["msnfp_endondate"] = donationPledge.msnfp_endondate;

    relatedDonationEntity["msnfp_sourceidentifier"] = donationPledge.msnfp_sourceidentifier;

    if (!isNullOrUndefined(donationPledge._msnfp_configurationid_value))
        relatedDonationEntity["msnfp_Configurationid@odata.bind"] = "/msnfp_configurations(" + donationPledge._msnfp_configurationid_value + ")";

    relatedDonationEntity["msnfp_signup"] = donationPledge.msnfp_signup;

    if (donationPledge.msnfp_cctype != null)
        relatedDonationEntity["msnfp_cctype"] = donationPledge.msnfp_cctype;

    relatedDonationEntity["msnfp_invoiceidentifier"] = donationPledge.msnfp_invoiceidentifier;

    if (donationPledge.msnfp_paymenttypecode != null)
        relatedDonationEntity["msnfp_paymenttypecode"] = donationPledge.msnfp_paymenttypecode;

    if (donationPledge.msnfp_dataentrysource != null)
        relatedDonationEntity["msnfp_dataentrysource"] = donationPledge.msnfp_dataentrysource;

    if (donationPledge.msnfp_customfund != null)
        relatedDonationEntity["msnfp_customfund"] = donationPledge.msnfp_customfund;

    if (donationPledge.msnfp_startondate != null)
        relatedDonationEntity["msnfp_startondate"] = donationPledge.msnfp_startondate;

    relatedDonationEntity["msnfp_authorizationid"] = donationPledge.msnfp_authorizationid;

    if (donationPledge._msnfp_donationallocationid_value != null)
        relatedDonationEntity["msnfp_DonationAllocationid@odata.bind"] = "/msnfp_donationallocations(" + donationPledge._msnfp_donationallocationid_value + ")";

    relatedDonationEntity["msnfp_transactionreceiptid"] = donationPledge.msnfp_transactionreceiptid;

    if (donationPledge.msnfp_customtemplate != null)
        relatedDonationEntity["msnfp_customtemplate"] = donationPledge.msnfp_customtemplate;

    if (donationPledge.msnfp_nextdonation != null)
        relatedDonationEntity["msnfp_nextdonation"] = donationPledge.msnfp_nextdonation;

    relatedDonationEntity["msnfp_chargeoncreate"] = true;

    if (donationPledge.msnfp_cancelledon != null)
        relatedDonationEntity["msnfp_cancelledon"] = donationPledge.msnfp_cancelledon;

    if (donationPledge.msnfp_cancelationreason != null)
        relatedDonationEntity["msnfp_cancelationreason"] = donationPledge.msnfp_cancelationreason;

    relatedDonationEntity["msnfp_cancellationtext"] = donationPledge.msnfp_cancellationtext;

    if (donationPledge._transactioncurrencyid_value != null)
        relatedDonationEntity["transactioncurrencyid@odata.bind"] = "/transactioncurrencies(" + donationPledge._transactioncurrencyid_value + ")";

    if (donationPledge.msnfp_cancelledon != null)
        relatedDonationEntity["msnfp_anonymous"] = donationPledge.msnfp_anonymous;

    relatedDonationEntity["msnfp_recurrenceinstance"] = donationPledge.msnfp_recurrenceinstance;

    if (donationPledge.msnfp_recurrencetype != null)
        relatedDonationEntity["msnfp_recurrencetype"] = donationPledge.msnfp_recurrencetype;

    if (donationPledge.msnfp_tributename != null)
        relatedDonationEntity["msnfp_tributename"] = donationPledge.msnfp_tributename;
}


function updateLastDonationDate(entity, isUpdate) {
    var nextDonationDate = entity.msnfp_startondate;
    if (formType == FormType.Update) {
        var dtOnly = entity.msnfp_nextdonation.split('-')[2];
        var dt = new Date(entity.msnfp_nextdonation.replace(/-/g, '/'));
        dt.setDate(dtOnly);
        nextDonationDate = dt;
    }
    else
        nextDonationDate = new Date(new Date(nextDonationDate).getUTCFullYear(), new Date(nextDonationDate).getUTCMonth(), new Date(nextDonationDate).getUTCDate());

    var instance = entity.msnfp_recurrenceinstance;
    var dayType = entity.msnfp_recurrencetype;
    var recurrenceStart = entity.msnfp_recurrencestart;
    var gracePeriod = 0;

    if (!isNullOrUndefined(configRecord.msnfp_sche_graceperiod)) {
        gracePeriod = parseInt(configRecord.msnfp_sche_graceperiod);

        if (!isUpdate && nextDonationDate.getDate() >= gracePeriod && recurrenceStart != RecurrenceStart.CurrentDay) {
            nextDonationDate.setMonth(nextDonationDate.getMonth() + 1);
        }
    }

    if (dayType == 844060000)//Daily
        nextDonationDate.setDate(nextDonationDate.getDate() + instance);
    else if (dayType == 844060001)//Weekly
        nextDonationDate.setDate(nextDonationDate.getDate() + (instance * 7));
    else if (dayType == 844060002)//Monthly
    {
        if (recurrenceStart == RecurrenceStart.CurrentDay) {
            var lastDateOfCurrentMonth = getLastDateOfMonth(nextDonationDate.getFullYear(), nextDonationDate.getMonth());
            var lastDateofNextmonth = getLastDateOfMonth(nextDonationDate.getFullYear(), nextDonationDate.getMonth() + 1);

            if (nextDonationDate.getDate() > lastDateofNextmonth.getDate()) {
                if (lastDateOfCurrentMonth.getDate() > lastDateofNextmonth.getDate()) {
                    nextDonationDate = getLastDateOfMonth(nextDonationDate.getFullYear(), nextDonationDate.getMonth() + 1);
                }
                else
                    nextDonationDate.setMonth(nextDonationDate.getMonth() + 1);
            }
            else {
                nextDonationDate.setMonth(nextDonationDate.getMonth() + 1);
            }
        }
        else if (recurrenceStart == RecurrenceStart.FirstOftheMonth) {
            nextDonationDate = getFirstDateOfNextMonth(nextDonationDate.getFullYear(), nextDonationDate.getMonth());
        }
        else if (recurrenceStart == RecurrenceStart.FifteenoOftheMonth) {
            nextDonationDate = getFifteenthDateOfNextMonth(nextDonationDate.getFullYear(), nextDonationDate.getMonth());
        }
    }
    else if (dayType == 844060003)//Annually
        nextDonationDate.setFullYear(nextDonationDate.getFullYear() + instance);

    nextDonationDate = (nextDonationDate.getMonth() + 1) + '/' + nextDonationDate.getDate() + '/' + nextDonationDate.getFullYear();
    nextDonationDate = new Date(nextDonationDate);
    entity["msnfp_nextdonation"] = nextDonationDate.getFullYear() + '-' + (nextDonationDate.getMonth() + 1) + '-' + nextDonationDate.getDate();
    entity["msnfp_lastdonation"] = new Date();

    return entity;
}

var getLastDateOfMonth = function (y, m) {
    return new Date(y, m + 1, 0);
}

var getFirstDateOfNextMonth = function (y, m) {
    return new Date(y, m + 1);
}

var getFifteenthDateOfNextMonth = function (y, m) {
    return new Date(y, m + 1, 15);
}


//add Membership to Donation
var membershipGroupList = [];
var membershipList = [];
var isLoaded = false;

function loadGroupOrderList() {
    if (!isLoaded) {
        membershipList = [];

        var goQuery = "msnfp_membershiporders?";
        goQuery += "$select=_msnfp_frommembershipid_value,_msnfp_tomembershipgroupid_value,msnfp_order&$orderby=msnfp_order asc";
        var goResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + goQuery);

        $(goResult).each(function () {
            if (!isNullOrUndefined(this._msnfp_tomembershipgroupid_value)) {
                var mgQuery = "msnfp_membershipgroups?";
                mgQuery += "$select=msnfp_membershipgroupid,msnfp_identifier,msnfp_groupname";
                mgQuery += "&$filter=msnfp_membershipgroupid eq " + this._msnfp_tomembershipgroupid_value;
                var mgResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + mgQuery);

                if (!isNullOrUndefined(mgResult)) {
                    membershipGroupList.push(mgResult[0]);
                }
            }
        });

        membershipGroupList = membershipGroupList.distinctMembershipGroup();
        membershipGroupList = membershipGroupList.sort(listSorter);

        $(membershipGroupList).each(function (index) {
            if (!isLoaded) {
                var link = $("<a>");
                link.addClass("tab");
                link.attr('id', 'Tab' + this.msnfp_groupname);
                link.attr('title', 'divMGroup' + index);
                link.attr("href", "javascript:void(0);");
                link.text(this.msnfp_groupname);

                $('.tabview').append(link);
            }
        });

        $(membershipGroupList).each(function (index) {
            if (!isLoaded) {
                $('.tabview').append($('.membershipGroup-clone').clone());
                var divAddedMembershipGroup = $('.tabview .membershipGroup-clone');
                $(divAddedMembershipGroup).removeClass('membershipGroup-clone').addClass('divMembershipGroup');
                $(divAddedMembershipGroup).attr('id', 'divMGroup' + index);
                $($(divAddedMembershipGroup).find('table')[0]).attr('id', 'tblMGroup' + index);

                var tblName = 'tblMGroup' + index;
                var tblDT;

                if ($.fn.DataTable.isDataTable('#' + tblName)) {
                    $('#tblMGroup' + index).DataTable().clear();
                    tblDT = $('#tblMGroup' + index).DataTable();
                }
                else {
                    tblDT = $('#tblMGroup' + index).DataTable({
                        "pagingType": "numbers",
                        "paging": true,
                        "searching": false,
                        "lengthChange": false,
                        "info": false,
                        "pageLength": 5,
                        "columnDefs": [
                            { "className": "dt-center", "width": "15%", "targets": 0 },
                            { "width": "55%", "targets": 1 },
                            { "width": "30%", "targets": 2 },
                        ],
                    });
                }

                membershipList = getMembershipCategoriesForThisGroup(this.msnfp_membershipgroupid);

                $(membershipList).each(function () {
                    var rdoBtn = "<input type='checkbox' aria-label='Select/Unselect' data-id='" + this.msnfp_membershipcategoryid + "' name='chkMembership' class='chkMembership'/>";
                    var name = !isNullOrUndefined(this.msnfp_name) ? this.msnfp_name : "";
                    var baseCost = currencySymbol + (!isNullOrUndefined(this.msnfp_amount) ? this.msnfp_amount : 0);

                    tblDT.row.add([
                        rdoBtn,
                        name,
                        baseCost
                    ]).draw(false);
                });
            }
        });
    }

    isLoaded = true;

    // Remove all active:
    $('.tabview').children('a').each(function () {
        $(this).removeClass('tab-active');
    });

    // Add active to the first:
    $('.tabview a:first').addClass('tab-active');

    $('.tabview').children('div.divMembershipGroup').each(function () {
        if ($(this)[0].id == $('.tabview').find('a:first')[0].title) {
            $(this).css("display", "block");
            $($($(this)[0]).find('table')[0]).css('display', 'table')
        }
        else {
            $($($(this)[0]).find('table')[0]).css('display', 'none')
            $(this).hide();
        }
    });

    $('.tabview').children('a').each(function () {
        $(this).click(function (e) {
            manageMembershipTab(this);
        });
    });

    $('.chkMembership').click(function () {

        $('input.chkMembership').not(this).prop('checked', false);
    });
}

function manageMembershipTab(current) {
    $('.tabview').children('a').each(function () {
        if (this === current) {
            $(this).addClass('tab-active');
            $('#' + this.title).css("display", "block");
            $($('#' + this.title).find('table')[0]).css('display', 'table');
        }
        else {
            $(this).removeClass('tab-active');
            $($('#' + this.title).find('table')[0]).css('display', 'none');
            $('#' + this.title).hide();
        }
    });
}

function getMembershipCategoriesForThisGroup(mGroupID) {
    var apiQuery = XrmServiceUtility.GetWebAPIUrl() + "msnfp_membershipcategories";
    var fetchXML = '<fetch distinct="true" mapping="logical" output-format="xml-platform" version="1.0">' +
        '<entity name="msnfp_membershipcategory">' +
        '<attribute name="msnfp_membershipcategoryid"/>' +
        '<attribute name="msnfp_name"/>' +
        '<attribute name="msnfp_amount"/>' +
        '<attribute name="msnfp_amount_tax"/>' +
        '<attribute name="msnfp_amount_membership"/>' +
        '<link-entity name="msnfp_membershiporder" alias="ah" to="msnfp_membershipcategoryid" from="msnfp_frommembershipid">' +
        '<attribute name="msnfp_order"/>' +
        '<filter type="and">' +
        '<condition attribute="msnfp_tomembershipgroupid" value="' + mGroupID + '" uitype="msnfp_membershipgroup" operator="eq"/>' +
        '</filter>' +
        '<order attribute="msnfp_order" descending="false" />' +
        '</link-entity>' +
        '</entity>' +
        '</fetch>';
    var encodedFetchXML = encodeURIComponent(fetchXML);

    var results;
    var req = new XMLHttpRequest();
    req.open("GET", apiQuery + "?fetchXml=" + encodedFetchXML, false);
    req.setRequestHeader("OData-MaxVersion", "4.0");
    req.setRequestHeader("OData-Version", "4.0");
    req.setRequestHeader("Accept", "application/json");
    req.setRequestHeader("Prefer", "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"");
    req.onreadystatechange = function () {
        if (this.readyState === 4) {
            req.onreadystatechange = null;
            if (this.status === 200) {
                results = JSON.parse(this.responseText);
                if (!isNullOrUndefined(results) && results.value.length > 0)
                    results = results.value;
            }
            else {
                console.log("Error: status - " + this.status);
            }
        }
    };
    req.send();

    return results;
}

// Here we create the instance of the membership for this gift:
function createMembershipFromMembershipCategory(membershipCategoryGUID) {
    console.log("Entering: createMembershipFromMembershipCategory");
    if (membershipCategoryGUID != null) {
        var query = "msnfp_membershipcategories?";
        query += "$select=msnfp_membershipcategoryid,msnfp_amount,msnfp_amount_membership,msnfp_amount_tax,msnfp_name,msnfp_membershipduration,msnfp_renewaldate&";
        query += "$filter=msnfp_membershipcategoryid eq " + membershipCategoryGUID;
        var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query);

        if (!isNullOrUndefined(result)) {
            var entity = {};

            entity["msnfp_MembershipCategoryId@odata.bind"] = "/msnfp_membershipcategories(" + membershipCategoryGUID + ")";

            console.log("selectedDonor.accountid - " + selectedDonor.accountid);
            console.log("selectedDonor.contactid - " + selectedDonor.contactid);

            // Set the name and the customer binding:
            if (selectedDonor.isAccount) {
                entity["msnfp_customer_account@odata.bind"] = "/accounts(" + selectedDonor.accountid + ")";
                entity["msnfp_name"] = result[0].msnfp_name;
            }
            else {
                entity["msnfp_customer_contact@odata.bind"] = "/contacts(" + selectedDonor.contactid + ")";
                entity["msnfp_name"] = result[0].msnfp_name;
            }

            // Date from today:
            entity["msnfp_startdate"] = getFormattedDateYYYYMMDD(new Date());

            // The Date To field is set based on the Renewal date of the category + the duration of the category.
            entity["msnfp_enddate"] = getFormattedDateYYYYMMDD(calculateMembershipExpiryDate(result[0].msnfp_membershipduration));

            // Temp set to primary:
            entity["msnfp_primary"] = true;

            var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_memberships";

            // Now update the gift to link the donation in the saveMembershipSuccess function:
            var membershipInstanceID = XrmServiceUtility.CreateRecord(qry, entity);
            if (membershipInstanceID != null) {
                saveMembershipSuccess(membershipInstanceID);
            }
        }
    }
    else {
        console.log("Error(in createMembershipFromMembershipCategory): membershipCategoryGUID not set.");
    }
}

// Now that the membership is saved, update the gift:
function saveMembershipSuccess(recordID) {
    if (recordID != null && recordID != '') {
        // First update the gift:
        var entity = {};
        entity["msnfp_MembershipInstanceId@odata.bind"] = "/msnfp_memberships(" + recordID + ")";
        var qry = XrmServiceUtility.GetWebAPIUrl() + "msnfp_transactions(" + currentID + ")";
        XrmServiceUtility.UpdateRecord(qry, entity);

        // Then update the contact or account:
        if (selectedDonor.isAccount) {
            var customerentity = {};
            customerentity["msnfp_primarymembershipid@odata.bind"] = "/msnfp_memberships(" + recordID + ")";
            var customerqry = XrmServiceUtility.GetWebAPIUrl() + "accounts(" + selectedDonor.accountid + ")";
            XrmServiceUtility.UpdateRecord(customerqry, customerentity);
        }
        else {
            var customerentity = {};
            customerentity["msnfp_primarymembershipid@odata.bind"] = "/msnfp_memberships(" + recordID + ")";
            var customerqry = XrmServiceUtility.GetWebAPIUrl() + "contacts(" + selectedDonor.contactid + ")";
            XrmServiceUtility.UpdateRecord(customerqry, customerentity);
        }

    }
}

function calculateMembershipExpiryDate(duration) {
    var modifiedEndDate = new Date();

    if (MembershipDuration.Month3.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 3);
    }
    else if (MembershipDuration.Month6.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 6);
    }
    else if (MembershipDuration.Month12.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 12);
    }
    else if (MembershipDuration.Month24.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 24);
    }
    else if (MembershipDuration.Year3.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 36);
    }
    else if (MembershipDuration.Year4.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 48);
    }
    else if (MembershipDuration.Year5.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 60);
    }
    else if (MembershipDuration.Year10.value == duration) {
        modifiedEndDate.setMonth(modifiedEndDate.getMonth() + 120);
    }
    else if (MembershipDuration.Lifetime.value == duration) {
        modifiedEndDate.setValue(new Date('2099-12-31'));
    }
    else
        modifiedEndDate = null;

    return modifiedEndDate ? new Date(modifiedEndDate) : null;
}

//Helper Methods
Array.prototype.distinct = function () {
    var map = {}, out = [];
    var l = this.length;

    for (var i = 0; i < l; i++) {
        if (map[this[i].data]) { continue; }

        out.push(this[i]);
        map[this[i].data] = 1;
    }

    return out;
}

Array.prototype.distinctMembershipGroup = function () {
    var map = {}, out = [];
    var l = this.length;

    for (var i = 0; i < l; i++) {
        if (map[this[i].msnfp_identifier]) { continue; }

        out.push(this[i]);
        map[this[i].msnfp_identifier] = 1;
    }

    return out;
}

function getLookupGuid(fieldName) {
    var field = parent.Xrm.Page.data.entity.attributes.get(fieldName);
    if (field != null && field.getValue() != null) {
        return parent.Xrm.Page.data.entity.attributes.get(fieldName).getValue()[0].id.replace('{', '').replace('}', '');
    }
    return null;
}

function RandomGuid() {
    return s4() + s4() + '' + s4() + '' + s4() + '' +
        s4() + '' + s4() + s4() + s4();
}

function s4() {
    return Math.floor((1 + Math.random()) * 0x10000)
        .toString(16)
        .substring(1);
}

function showAPIErrorMessage() {
    try {
        var APIMessage = parent.Xrm.Page.getAttribute("msnfp_apierrormessage").getValue();
        if (!isNullOrUndefined(APIMessage)) {
            var confirmStrings = { text: "An error has occured with syncing this Transaction to the Azure DB.\n\n" + APIMessage, title: "API Error Message", cancelButtonLabel: "Cancel", confirmButtonLabel: "Okay" };
            var confirmOptions = { height: 300, width: 950 };
            xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
                function (success) {
                    if (success.confirmed) {
                        confirmAPIErrorMessage();
                    }
                    else {
                        console.log("Dialog closed using Cancel button or X.");
                    }
                });
        }
    }
    catch (error) {
        console.log("Error in showAPIErrorMessage(): " + error);
    }
}

function confirmAPIErrorMessage() {
    console.log("Message Confirmed");
}

function getUrlParameter(name) {
    var param = parent.Xrm.Page.context.getQueryStringParameters();
    return param[name];
}

function isEmail(email) {
    var regex = /^([a-zA-Z0-9_.+-])+\@(([a-zA-Z0-9-])+\.)+([a-zA-Z0-9]{2,4})+$/;
    return regex.test(email);
}

function getFormattedDate(date) {
    return ("0" + (date.getMonth() + 1)).slice(-2)
        + "/"
        + ("0" + date.getDate()).slice(-2)
        + "/"
        + date.getFullYear();
}

function getUTCFormattedDate(date) {
    return ("0" + (date.getUTCMonth() + 1)).slice(-2)
        + "/"
        + ("0" + date.getUTCDate()).slice(-2)
        + "/"
        + date.getUTCFullYear();
}

// Note this is used for Date Only fields:
function getFormattedDateYYYYMMDD(date) {
    return date ? date.getFullYear() + "-" + ("0" + (date.getMonth() + 1)).slice(-2) + "-" + ("0" + date.getDate()).slice(-2) : null;
}

function DSTOffsetYYYYMMDD(str) {
    let dt = new Date(str);
    let start = new Date(dt.getYear(), userSettings.timezonedaylightmonth - 1, userSettings.timezonedaylightday);
    let end = new Date(dt.getYear(), userSettings.timezonestandardmonth - 1, userSettings.timezonestandardday);
    if (dt >= start && dt < end) dt.setMinutes(-userSettings.timezonedaylightbias);
    return dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate();
}

function DSTOffset(str) {
    let dt = new Date(str);
    let start = new Date(dt.getYear(), userSettings.timezonedaylightmonth - 1, userSettings.timezonedaylightday);
    let end = new Date(dt.getYear(), userSettings.timezonestandardmonth - 1, userSettings.timezonestandardday);
    if (dt >= start && dt < end) dt.setMinutes(-userSettings.timezonedaylightbias);
    return dt;
}

function isNullOrUndefined(value) {
    return (typeof (value) === "undefined" || value === null || value === "");
}

function listSorter(a, b) {
    var x = a.msnfp_groupname.toLowerCase();
    var y = b.msnfp_groupname.toLowerCase();
    return ((x < y) ? -1 : ((x > y) ? 1 : 0));
}

function validateFloatKeyPress(el, evt) {
    var charCode = (evt.which) ? evt.which : event.keyCode;
    var number = el.value.split('.');
    if (charCode != 46 && charCode > 31 && (charCode < 48 || charCode > 57)) {
        return false;
    }
    //just one dot
    if (number.length > 1 && charCode == 46) {
        return false;
    }
    //get the carat position
    var caratPos = getSelectionStart(el);
    var dotPos = el.value.indexOf(".");
    if (caratPos > dotPos && dotPos > -1 && (number[1].length > 1)) {
        return false;
    }
    return true;
}

function validateFloatKeyPressWithNegative(el, evt) {
    var charCode = (evt.which) ? evt.which : event.keyCode;
    var number = el.value.split('.');
    if (charCode != 45 && charCode != 46 && charCode > 31 && (charCode < 48 || charCode > 57)) {
        return false;
    }
    //just one dot
    if (number.length > 1 && charCode == 46) {
        return false;
    }
    //get the carat position
    var caratPos = getSelectionStart(el);
    var dotPos = el.value.indexOf(".");
    if (caratPos > dotPos && dotPos > -1 && (number[1].length > 1)) {
        return false;
    }
    return true;
}

function getSelectionStart(o) {
    if (o.createTextRange) {
        var r = document.selection.createRange().duplicate();
        r.moveEnd('character', o.value.length);
        if (r.text === '') return o.value.length;
        return o.value.lastIndexOf(r.text);
    } else return o.selectionStart;
}

function isNumber(evt) {
    evt = (evt) ? evt : window.event;
    var charCode = (evt.which) ? evt.which : evt.keyCode;
    if (charCode > 31 && (charCode < 48 || charCode > 57)) {
        return false;
    }
    return true;
}

function addCommasonLoad(nStr) {
    if (nStr != 0) {
        nStr = nStr.replace(currencySymbol, '').replace(',', '');
        nStr += '';
        x = nStr.split('.');
        x1 = x[0];
        x2 = x.length > 1 ? '.' + x[1] : '';
        var rgx = /(\d+)(\d{3})/;
        while (rgx.test(x1)) {
            x1 = x1.replace(rgx, '$1' + ',' + '$2');
        }
        return x1 + x2;
    }
    else return '0.00';
}

function setColorScheme(chosenColor) {
    $(".form-container").css({ "border-top": "0.6em solid " + chosenColor });
    $(".badge-success").css({ "background-color": chosenColor });
    $(".main-button:hover").css({ "background-color": chosenColor, "border": "1px solid " + chosenColor });
    $(".switch-field input:checked + label").css({ "border": "1px solid " + chosenColor, "background-color": chosenColor });
    $(".switch-field label:hover").css({ "border": "1px solid " + chosenColor, "background-color": chosenColor });
}

function CheckUserInRole(roleName, userId) {
    var roleFound = false;

    var context = XrmUtility.get_Xrm();

    if (context !== undefined && context !== null) {
        var roles = context.Utility.getGlobalContext().userSettings.roles;
        if (roles !== undefined || roles !== null) {
            $.each(roles._collection, function () {
                roleFound = this.name.toLowerCase() === roleName.toLowerCase();
                if (roleName)
                    return false;
            });
        }
    }

    return roleFound;
}

function RefreshPaymentSchedule() {
    let paymentschedule = parent.Xrm.Page.getAttribute("msnfp_transaction_paymentscheduleid").getValue();
    let btn = $('#btn-Payment-Schedule'), container = $('#btn-Payment-Schedule-Container');
    if (paymentschedule) {
        btn.val(paymentschedule[0].name ?? 'No Name');
        btn.click(() => xrm.Utility.openEntityForm(paymentschedule[0].entityType, paymentschedule[0].id));
        container.show();
    }
    else {
        container.hide();
        btn.val('');
        btn.unbind("click");
    }
}

function getConstituent() {
    var cons = parent.Xrm.Page.data.entity.attributes.get("msnfp_relatedconstituentid").getValue();
    if (cons)
        return `/${cons[0].entityType}s(${XrmUtility.CleanGuid(cons[0].id)})`;
    else if (!isNullOrUndefined(selectedConstitute))
        return `/contacts(${selectedConstitute.contactid})`;
    return null;
}

function loadAppraiser() {
    var selectAccount = "accounts?$select=accountid,name,msnfp_isappraiser,address1_line1,address1_line2,address1_line3,address1_city,";
    selectAccount += "address1_stateorprovince,address1_postalcode,address1_country&$orderby=name";
    selectAccount += "&$filter=msnfp_isappraiser eq true";
    appraiserResult = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + selectAccount);

    if (!isNullOrUndefined(appraiserResult)) {
        selectedAppraiserAccount = true;

        $('#ddlAppraiser').find('option').remove().end().append('<option value="0">--Select--</option>');
        $.each(appraiserResult, function (i, item) {
            if (!isNullOrUndefined(item.address1_postalcode))
                var postalcode = " - " + item.address1_postalcode;

            $('#ddlAppraiser').append($('<option>',
                {
                    value: item.accountid,
                    text: item.name + (!isNullOrUndefined(item.address1_postalcode) ? postalcode : ""),
                }));
        });
    }
}

function setMandatoryAdvDescription() {
    var appraiserAmount; //= $('#txtAppraiserAmount').val().replace(currencySymbol, '').replace(/,/g, '');
    //var eligibleAmount; //= $('#txtEligibleAmount').val().replace(currencySymbol, '').replace(/,/g, '');
    var advantage = 0;

    if (!isNullOrUndefined($('#txtAppraiserAmount').val()))
        appraiserAmount = parseFloat($('#txtAppraiserAmount').val().replace(currencySymbol, '').replace(/,/g, ''));

    //if (!isNullOrUndefined($('#txtEligibleAmount').val()))
    //    eligibleAmount = parseFloat($('#txtEligibleAmount').val().replace(currencySymbol, '').replace(/,/g, ''));

    if (!isNullOrUndefined($('#txtAppraiserNonReceiptable').val()))
        advantage = parseFloat($('#txtAppraiserNonReceiptable').val().replace(currencySymbol, '').replace(/,/g, ''));

    if (advantage != 0)
        $('#txtAdvDescription').closest('div').addClass('mandatory');
    else
        $('#txtAdvDescription').closest('div').removeClass('mandatory');
}

function addRemoveSecuritiesFieldsValidation(addMandatory) {
    if (addMandatory == true) {
        $('#txtStockDescription').closest('div').addClass('mandatory');
        $('#txtStockSymbol').closest('div').addClass('mandatory');
        $('#txtDateofSale').closest('div').addClass('mandatory');
        $('#txtCostPerStock').closest('div').addClass('mandatory');
        $('#txtNoOfShares').closest('div').addClass('mandatory');
        $('#txtStockAmount').closest('div').addClass('mandatory');
        $('#txtStockAdvantageAmount').closest('div').addClass('mandatory');
    } else {
        $('#txtStockDescription').closest('div').removeClass('mandatory');
        $('#txtStockSymbol').closest('div').removeClass('mandatory');
        $('#txtDateofSale').closest('div').removeClass('mandatory');
        $('#txtCostPerStock').closest('div').removeClass('mandatory');
        $('#txtNoOfShares').closest('div').removeClass('mandatory');
        $('#txtStockAmount').closest('div').removeClass('mandatory');
        $('#txtStockAdvantageAmount').closest('div').removeClass('mandatory');
    }
}

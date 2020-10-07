/*************************************************************************
* © Microsoft. All rights reserved.
*/

function setCurrentDateDonationPledge() {
    var formType = parent.Xrm.Page.ui.getFormType();
    if (formType == 1) {
        parent.Xrm.Page.getAttribute('msnfp_date').setValue(new Date());
    }
}


//Membership Instance Page
function setCurrentDateMembershipInstance() {
    var formType = parent.Xrm.Page.ui.getFormType();
    if (formType == 1) {
        parent.Xrm.Page.getAttribute('msnfp_datefrom').setValue(new Date());
    }
}

function setMembershipInstanceToDate()
{
    var dateFrom = parent.Xrm.Page.data.entity.attributes.get('msnfp_datefrom').getValue();
    var dateTo = parent.Xrm.Page.data.entity.attributes.get("msnfp_dateto");

    if (Xrm.Page.data.entity.attributes.get('msnfp_membershipid').getValue() != null && dateFrom != null)
    {
        var membershipId = parent.Xrm.Page.data.entity.attributes.get('msnfp_membershipid').getValue()[0].id;
        membershipId = membershipId.replace('{', '').replace('}', '');

        var result = getMembershipById(membershipId);
        if (result != null && result.msnfp_membershipduration != null)
        {
            if (MembershipDuration.Month3.value == result.msnfp_membershipduration) {
                dateFrom.setMonth(dateFrom.getMonth() + 3);
            }
            else if (MembershipDuration.Month6.value == result.msnfp_membershipduration) {
                dateFrom.setMonth(dateFrom.getMonth() + 6);
            }
            else if (MembershipDuration.Month12.value == result.msnfp_membershipduration)
            {
                dateFrom = new Date(result.msnfp_renewaldate);
                if (result.msnfp_goodwilldate != null && dateTo >= result.msnfp_goodwilldate)
                    dateFrom.setMonth(dateFrom.getMonth() + 12);
                else
                  dateFrom = new Date(result.msnfp_renewaldate);
            }
            else if (MembershipDuration.Month24.value == result.msnfp_membershipduration)
            {
                dateFrom = new Date(result.msnfp_renewaldate);
                if (result.msnfp_goodwilldate != null && dateTo >= result.msnfp_goodwilldate)
                    dateFrom.setMonth(dateFrom.getMonth() + 24);
                else
                    dateFrom.setMonth(dateFrom.getMonth() + 12);
            }
            else if (MembershipDuration.Year3.value == result.msnfp_membershipduration)
            {
                dateFrom = new Date(result.msnfp_renewaldate);
                if (result.msnfp_goodwilldate != null && dateTo >= result.msnfp_goodwilldate)
                    dateFrom.setMonth(dateFrom.getMonth() + 36);
                else
                    dateFrom.setMonth(dateFrom.getMonth() + 24);
            }
            else if (MembershipDuration.Year4.value == result.msnfp_membershipduration)
            {
                dateFrom = new Date(result.msnfp_renewaldate);
                if (result.msnfp_goodwilldate != null && dateTo >= result.msnfp_goodwilldate)
                    dateFrom.setMonth(dateFrom.getMonth() + 48);
                else
                    dateFrom.setMonth(dateFrom.getMonth() + 36);
            }
            else if (MembershipDuration.Year5.value == result.msnfp_membershipduration)
            {
                dateFrom = new Date(result.msnfp_renewaldate);
                if (result.msnfp_goodwilldate != null && dateTo >= result.msnfp_goodwilldate)
                    dateFrom.setMonth(dateFrom.getMonth() + 60);
                else
                    dateFrom.setMonth(dateFrom.getMonth() + 48);
            }
            else if (MembershipDuration.Year10.value == result.msnfp_membershipduration)
            {
                dateFrom = new Date(result.msnfp_renewaldate);
                if (result.msnfp_goodwilldate != null && dateTo >= result.msnfp_goodwilldate)
                    dateFrom.setMonth(dateFrom.getMonth() + 120);
                else
                    dateFrom.setMonth(dateFrom.getMonth() + 108);
            }

            if (MembershipDuration.Lifetime.value == result.msnfp_membershipduration) {
                dateTo.setValue(new Date('2099-12-31'));
            }
            else
                dateTo.setValue(dateFrom);
        }
    } else {
        dateTo.setValue(null);
    }
}


function getMembershipById(id) {
    var dataUrl = XrmServiceUtility.GetWebAPIUrl();
    var selectQuery = "msnfp_memberships?$select=msnfp_membershipduration,msnfp_goodwilldate,msnfp_renewaldate&$filter=msnfp_membershipid eq " + id + "";
    var result = MetadataQuery.GetOptionSetValues(dataUrl + selectQuery);
    if (result != null && result.length > 0) {
        return result[0];
    }
    return null;
}

//Category Form
function changeDefaultSchema() {
    var name = parent.Xrm.Page.data.entity.attributes.get('msnfp_name').getValue();
    if (name != null && name != '') {
        var schemaName = name.replace(/[^a-z0-9\s_]/gi, '').replace(/[\s]/g, '');
        parent.Xrm.Page.getAttribute('msnfp_schema').setValue("msnfp_" + schemaName.toLowerCase());
    }
}

var MembershipDuration = {
    Lifetime: { value: 844060000, name: "Lifetime" },
    Year10: { value: 844060007, name: "10 Years" },
    Year5: { value: 84406060, name: "5 Years" },
    Year4: { value: 84406048, name: "4 Years" },
    Year3: { value: 84406036, name: "3 Years" },
    Month24: { value: 84406024, name: "24 Month" },
    Month12: { value: 84406012, name: "12 Month" },
    Month6: { value: 84406006, name: "6 Month" },
    Month3: { value: 84406003, name: "3 Month" },
    NotApplicable: { value: 84406000, name: "Not Applicable" }
};

//Batch Gift
var batchGiftSaved;
function BatchGiftSaveAction(execObj) {
    var formType = parent.Xrm.Page.ui.getFormType();
    var saveMode = execObj.getEventArgs().getSaveMode();
    if (formType == 1 && saveMode == 1) {
        batchGiftSaved = true;
    }
}

function BatchGiftOnLoadAction() {
    if (batchGiftSaved) {
        batchGiftSaved = false;
        var parameters = {};
        var id = parent.Xrm.Page.data.entity.getId().replace('{', '').replace('}', '');
        Xrm.Utility.openEntityForm("msnfp_giftbatch", id, parameters, true);
    }
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

Date.prototype.toDateInputValue = (function () {
    var local = new Date(this);
    local.setMinutes(this.getMinutes() - this.getTimezoneOffset());
    return local.toJSON().slice(0, 10);
});

//set configuration and Owing team on Campaign Page, Donation Page, Event Page
function setConfigurationandOwingTeam()
{
    var configRecord = null;
    var formType = parent.Xrm.Page.ui.getFormType();
    if (formType == 1)
    {
        var currentuserID = parent.Xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
        var userSelect = "systemusers?$select=systemuserid,_msnfp_configurationid_value";
        userSelect += "&$filter=systemuserid eq " + currentuserID;
        var user = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);
        user = user[0];

        if (!isNullOrUndefined(user._msnfp_configurationid_value)) {
            var select = "msnfp_configurations(" + user._msnfp_configurationid_value + ")?";
            var expand = "$expand=msnfp_TeamOwnerId($select=teamid,name)";

            var configresult = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);

            if (!isNullOrUndefined(configresult))
            {
                if (!isNullOrUndefined(Xrm.Page.getAttribute("msnfp_configurationid")))
                    parent.Xrm.Page.getAttribute("msnfp_configurationid").setValue([{ id: configresult.msnfp_configurationid, name: configresult.msnfp_identifier, entityType: "msnfp_configuration" }]);

                if (!isNullOrUndefined(configresult._msnfp_teamownerid_value))
                {
                    var teamID = configresult.msnfp_TeamOwnerId.teamid;
                    var teamName = configresult.msnfp_TeamOwnerId.name;
                    if (!isNullOrUndefined(Xrm.Page.getAttribute("msnfp_teamownerid")))
                        parent.Xrm.Page.getAttribute("msnfp_teamownerid").setValue([{ id: teamID, name: teamName, entityType: "team" }]);
                }
            }
        }
    }
}

function isNullOrUndefined(value) {
    return (typeof (value) === "undefined" || value === null || value === "");
}

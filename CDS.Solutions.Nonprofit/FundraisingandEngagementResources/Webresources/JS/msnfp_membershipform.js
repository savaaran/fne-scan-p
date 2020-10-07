var duration_month = {
    "844060000": 3,
    "844060001": 6,
    "844060002": 12,
    "844060003": 24,
    "844060004": 36,
    "844060005": 48,
    "844060006": 60,
    "844060007": 120,
    "844060008": null,
    "844060009": null
};

function onFormLoad(executionContext) {
    let formContext = executionContext.getFormContext();
    if (formContext.ui.getFormType() != 1) return;
    formContext.getAttribute("msnfp_startdate").setValue(new Date());
    //let data = new URLSearchParams(new URL(document.referrer).search).get("data");
    var xrmObject = parent.Xrm.Page.context.getQueryStringParameters();
    //let json = JSON.parse(decodeURIComponent(data));
    //if (!data) return;

    if (xrmObject["param_msnfp_customerid"] != null && xrmObject["param_msnfp_customeridname"] != null && xrmObject["param_msnfp_customeridtype"] != null) {
        formContext.getAttribute("msnfp_customer").setValue([{ id: xrmObject["param_msnfp_customerid"], name: xrmObject["param_msnfp_customeridname"], entityType: xrmObject["param_msnfp_customeridtype"] }]);
    }

}

function onMembershipcategoryidChange(executionContext) {
    let formContext = executionContext.getFormContext();
    let mcrm_membershipcategoryid = formContext.getAttribute("msnfp_membershipcategoryid").getValue();
    if (!mcrm_membershipcategoryid) {
        formContext.getAttribute("msnfp_name").setValue(null);
        formContext.getAttribute("msnfp_enddate").setValue(null);
        return;
    }
    formContext.getAttribute("msnfp_name").setValue(mcrm_membershipcategoryid ? mcrm_membershipcategoryid[0].name : null);
    Xrm.WebApi.online.retrieveRecord("msnfp_membershipcategory", mcrm_membershipcategoryid[0].id, "?$select=msnfp_membershipduration").then(
        function success(result) {
            let duration = result.msnfp_membershipduration;
            var enddate = formContext.getAttribute("msnfp_enddate");
            if (!duration || !duration_month[duration])
                enddate.setValue(null);
            else {
                var start = formContext.getAttribute("msnfp_startdate").getValue();
                enddate.setValue(new Date(start.setMonth(start.getMonth() + duration_month[duration])));
            }
        }
    );
}
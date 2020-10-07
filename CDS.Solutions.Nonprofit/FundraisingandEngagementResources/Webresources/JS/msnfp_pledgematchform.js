function onFormLoad(executionContext) {
    let formContext = executionContext.getFormContext();
    let formType = formContext.ui.getFormType();
    if (formType == 1) {
        let p = parent.Xrm.Page.context.getQueryStringParameters();
        if (p.hasOwnProperty("param_msnfp_customerfromid"))
            formContext.getAttribute("msnfp_customerfromid").setValue([{ id: p.param_msnfp_customerfromid, name: p.param_msnfp_customerfromidname, entityType: p.param_msnfp_customerfromidtype }]);
    }
}
function onFormLoad(executionContext) {
    let formContext = executionContext.getFormContext();
    formContext.getAttribute("parentcustomerid").setValue(null);
    formContext.getAttribute("msnfp_householdid").setValue(null);
}
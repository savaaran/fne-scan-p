function lockDisplayNameField(executionContext) {

    let formContext = executionContext.getFormContext();
    var identifierField = formContext.getControl("msnfp_identifier");

    if (identifierField != null) {
        var formType = formContext.ui.getFormType();
        if (formType === 1) {
            identifierField.setDisabled(false);
        } else {
            identifierField.setDisabled(true);
        }
    }
}
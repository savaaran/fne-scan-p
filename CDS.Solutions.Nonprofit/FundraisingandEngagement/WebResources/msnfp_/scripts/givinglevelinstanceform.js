/*************************************************************************
* © Microsoft. All rights reserved.
*/

function onGivingLevelChange(executionContext) {
    var formContext = executionContext.getFormContext();
    var msnfp_givinglevelid = formContext.getAttribute("msnfp_givinglevelid");
    var msnfp_identifier = formContext.getAttribute("msnfp_identifier");

    if (msnfp_givinglevelid !== null && msnfp_identifier !== null) {
        if (msnfp_givinglevelid.getValue() !== null) {
            msnfp_identifier.setValue(msnfp_givinglevelid.getValue()[0].name);
        }
        else {
            msnfp_identifier.setValue(null);
        }
    }
}
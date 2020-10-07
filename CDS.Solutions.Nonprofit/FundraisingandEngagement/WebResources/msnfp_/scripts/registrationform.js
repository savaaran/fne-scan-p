/*************************************************************************
* © Microsoft. All rights reserved.
*/

function OnRegistrationFormLoad() {

    var userId = parent.Xrm.Page.context.getUserId();

    if (userId !== null || userId !== undefined) {

        var userId = userId.replace('{', '').replace('}', '').toUpperCase();
        var userSelect = "systemusers?$select=_msnfp_configurationid_value&$expand=msnfp_ConfigurationId($select=msnfp_event_showname, msnfp_event_showemail,";
        userSelect += "msnfp_event_showphone, msnfp_event_showaddress, msnfp_event_mandatename, msnfp_event_mandateemail, msnfp_event_mandatephone, msnfp_event_mandateaddress, ";
        userSelect += "msnfp_event_matchcontact, msnfp_event_autocreatecontact, msnfp_event_showcreatecontact, msnfp_event_matchemail, msnfp_event_matchphone, ";
        userSelect += "msnfp_event_matchaddress)&$filter=systemuserid%20eq%20" + userId + "%20and%20msnfp_ConfigurationId/msnfp_configurationid%20ne%20null";
        var configRec = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + userSelect);

        if (configRec !== null) {

            var showname = configRec[0].msnfp_ConfigurationId.msnfp_event_showname;
            var showphone = configRec[0].msnfp_ConfigurationId.msnfp_event_showphone;
            var showemail = configRec[0].msnfp_ConfigurationId.msnfp_event_showemail;
            var showaddress = configRec[0].msnfp_ConfigurationId.msnfp_event_showaddress;
            var mandatename = configRec[0].msnfp_ConfigurationId.msnfp_event_mandatename;
            var mandateemail = configRec[0].msnfp_ConfigurationId.msnfp_event_mandateemail;
            var mandatephone = configRec[0].msnfp_ConfigurationId.msnfp_event_mandatephone;
            var mandateaddress = configRec[0].msnfp_ConfigurationId.msnfp_event_mandateaddress;

            if (showname != null && showname == true) {
                parent.Xrm.Page.getControl("msnfp_firstname").setVisible(true);
                parent.Xrm.Page.getControl("msnfp_lastname").setVisible(true);

                if (mandatename) {
                    parent.Xrm.Page.getAttribute("msnfp_firstname").setRequiredLevel("required");
                    parent.Xrm.Page.getAttribute("msnfp_lastname").setRequiredLevel("required");
                }
            } 

            if (showphone != null && showphone == true) {
                parent.Xrm.Page.getControl("msnfp_telephone").setVisible(true);

                if (mandatephone) {
                    parent.Xrm.Page.getAttribute("msnfp_telephone").setRequiredLevel("required");
                } 
            }

            if (showemail != null && showemail == true) {
                parent.Xrm.Page.getControl("msnfp_email").setVisible(true);

                if (mandateemail) {
                    parent.Xrm.Page.getAttribute("msnfp_email").setRequiredLevel("required");
                } 
            }

            if (showaddress != null && showaddress == true) {
                parent.Xrm.Page.getControl("msnfp_address_line1").setVisible(true);
                parent.Xrm.Page.getControl("msnfp_address_line2").setVisible(true);
                parent.Xrm.Page.getControl("msnfp_address_city").setVisible(true);
                parent.Xrm.Page.getControl("msnfp_address_province").setVisible(true);
                parent.Xrm.Page.getControl("msnfp_address_postalcode").setVisible(true);
                parent.Xrm.Page.getControl("msnfp_address_country").setVisible(true);

                if (mandateaddress) {
                    parent.Xrm.Page.getAttribute("msnfp_address_line1").setRequiredLevel("required");
                    parent.Xrm.Page.getAttribute("msnfp_address_line2").setRequiredLevel("required");
                    parent.Xrm.Page.getAttribute("msnfp_address_city").setRequiredLevel("required");
                    parent.Xrm.Page.getAttribute("msnfp_address_province").setRequiredLevel("required");
                    parent.Xrm.Page.getAttribute("msnfp_address_postalcode").setRequiredLevel("required");
                    parent.Xrm.Page.getAttribute("msnfp_address_country").setRequiredLevel("required");
                } 
            }
        }
    }
}
/*************************************************************************
* © Microsoft. All rights reserved.
*/

function OnConfigurationFormLoad() {
    OnMatchContactChange();
    OnAutocreateContactChange();
}

function OnMatchContactChange() {

    var matchContact = parent.Xrm.Page.getAttribute("msnfp_event_matchcontact").getValue();

    // show/hide match fields
    if (matchContact != null && matchContact == true) {
        parent.Xrm.Page.getControl("msnfp_event_matchnames").setVisible(true);
        parent.Xrm.Page.getControl("msnfp_event_matchemail").setVisible(true);
        parent.Xrm.Page.getControl("msnfp_event_matchphone").setVisible(true);
        parent.Xrm.Page.getControl("msnfp_event_matchaddress").setVisible(true);
    }
    else {
        parent.Xrm.Page.getControl("msnfp_event_matchnames").setVisible(false);
        parent.Xrm.Page.getControl("msnfp_event_matchemail").setVisible(false);
        parent.Xrm.Page.getControl("msnfp_event_matchphone").setVisible(false);
        parent.Xrm.Page.getControl("msnfp_event_matchaddress").setVisible(false);
    }
}


function OnAutocreateContactChange() {

    var autocreateContact = parent.Xrm.Page.getAttribute("msnfp_event_autocreatecontact").getValue();

    // show/hide match fields
    if (autocreateContact != null && autocreateContact == true) {
        parent.Xrm.Page.getControl("msnfp_event_showcreatecontact").setVisible(false);
    }
    else {
        parent.Xrm.Page.getControl("msnfp_event_showcreatecontact").setVisible(true);
    }
}
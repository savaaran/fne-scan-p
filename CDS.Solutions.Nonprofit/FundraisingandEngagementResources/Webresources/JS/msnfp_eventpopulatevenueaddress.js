function onVenueChange(executionContext) {
    console.log("onVenueChange();");
    let formContext = executionContext.getFormContext();

    var msnfp_map_line1 = formContext.getAttribute("msnfp_map_line1");
    var msnfp_map_line2 = formContext.getAttribute("msnfp_map_line2");
    var msnfp_map_line3 = formContext.getAttribute("msnfp_map_line3");
    var msnfp_map_city = formContext.getAttribute("msnfp_map_city");
    var msnfp_stateorprovince = formContext.getAttribute("msnfp_stateorprovince");
    var msnfp_map_postalcode = formContext.getAttribute("msnfp_map_postalcode");

    let venueId = formContext.getAttribute("msnfp_venueid").getValue();
    if (!venueId) {
        if (!isNullOrUndefined(msnfp_map_line1)) {
            msnfp_map_line1.setValue(null);
        }
        if (!isNullOrUndefined(msnfp_map_line2)) {
            msnfp_map_line2.setValue(null);
        }
        if (!isNullOrUndefined(msnfp_map_line3)) {
            msnfp_map_line3.setValue(null);
        }
        if (!isNullOrUndefined(msnfp_map_city)) {
            msnfp_map_city.setValue(null);
        }
        if (!isNullOrUndefined(msnfp_stateorprovince)) {
            msnfp_stateorprovince.setValue(null);
        }
        if (!isNullOrUndefined(msnfp_map_postalcode)) {
            msnfp_map_postalcode.setValue(null);
        }

        return;
    }
    Xrm.WebApi.online.retrieveRecord("account", venueId[0].id, "?$select=accountid,address1_line1,address1_line2,address1_line3,address1_city,address1_stateorprovince,address1_postalcode").then(
        function success(result) {
            console.log("Returned account = " + result.accountid);

            let address1_line1 = result.address1_line1;
            let address1_line2 = result.address1_line2;
            let address1_line3 = result.address1_line3;
            let address1_city = result.address1_city;
            let address1_stateorprovince = result.address1_stateorprovince;
            let address1_postalcode = result.address1_postalcode;

            if (!isNullOrUndefined(address1_line1) && !isNullOrUndefined(msnfp_map_line1)) {
                msnfp_map_line1.setValue(address1_line1);
            }
            else if (!isNullOrUndefined(msnfp_map_line1)) {
                msnfp_map_line1.setValue(null);
            }

            if (!isNullOrUndefined(address1_line2) && !isNullOrUndefined(msnfp_map_line2)) {
                msnfp_map_line2.setValue(address1_line2);
            }
            else if (!isNullOrUndefined(msnfp_map_line2)) {
                msnfp_map_line2.setValue(null);
            }

            if (!isNullOrUndefined(address1_line3) && !isNullOrUndefined(msnfp_map_line3)) {
                msnfp_map_line3.setValue(address1_line3);
            }
            else if (!isNullOrUndefined(msnfp_map_line3)) {
                msnfp_map_line3.setValue(null);
            }

            if (!isNullOrUndefined(address1_city) && !isNullOrUndefined(msnfp_map_city)) {
                msnfp_map_city.setValue(address1_city);
            }
            else if (!isNullOrUndefined(msnfp_map_city)) {
                msnfp_map_city.setValue(null);
            }

            if (!isNullOrUndefined(address1_stateorprovince) && !isNullOrUndefined(msnfp_stateorprovince)) {
                msnfp_stateorprovince.setValue(address1_stateorprovince);
            }
            else if (!isNullOrUndefined(msnfp_stateorprovince)) {
                msnfp_stateorprovince.setValue(null);
            }

            if (!isNullOrUndefined(address1_postalcode) && !isNullOrUndefined(msnfp_map_postalcode)) {
                msnfp_map_postalcode.setValue(address1_postalcode);
            }
            else if (!isNullOrUndefined(msnfp_map_postalcode)) {
                msnfp_map_postalcode.setValue(null);
            }
        }
    );
}
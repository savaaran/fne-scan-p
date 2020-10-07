/*************************************************************************
* © Microsoft. All rights reserved.
*/

function autoPopulateAppealDependingOnCampaign(ExecutionContext, _campaignFieldSchemaName, _appealFieldSchemaName, _packageFieldSchemaName) { // msnfp_originatingcampaignid , msnfp_appealid , msnfp_packageid
	let formContext = ExecutionContext.getFormContext();
	if (formContext.getAttribute(_campaignFieldSchemaName) && formContext.getAttribute(_appealFieldSchemaName)) {
		if (formContext.getAttribute(_campaignFieldSchemaName).getValue()) {
			var campaignId = formContext.getAttribute(_campaignFieldSchemaName).getValue()[0].id;

			var query = "msnfp_appeals?";
			query += "$select=msnfp_appealid,msnfp_identifier,createdon&$orderby=createdon desc&";
			query += "$filter=_msnfp_campaignid_value eq " + XrmUtility.CleanGuid(campaignId);

			// Get all Appeals under the current Campaign
			var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query);

			if (!(result === undefined || result === null) && result.length == 1) { // If there is only 1 Appeal record associated => auto populate it into the form
				var id = result[0].msnfp_appealid;
				var name = result[0].msnfp_identifier;
				var type = "msnfp_appeal";

				formContext.getAttribute(_appealFieldSchemaName).setValue([{ id: XrmUtility.CleanGuid(id), name: name, entityType: type }]);

				autoPopulatePackageDependingOnAppeal(ExecutionContext, _appealFieldSchemaName, _packageFieldSchemaName);
			} else { // If there is none or more than 1 Appeal records => clear out the Appeal field
				formContext.getAttribute(_appealFieldSchemaName).setValue();
			}
		}
	}
}

function autoPopulatePackageDependingOnAppeal(ExecutionContext, _appealFieldSchemaName, _packageFieldSchemaName) { // msnfp_appealid , msnfp_packageid
	let formContext = ExecutionContext.getFormContext();
	if (formContext.getAttribute(_appealFieldSchemaName) && formContext.getAttribute(_packageFieldSchemaName)) {
		if (formContext.getAttribute(_appealFieldSchemaName).getValue()) {
			var appealId = formContext.getAttribute(_appealFieldSchemaName).getValue()[0].id;

			var query = "msnfp_packages?";
			query += "$select=msnfp_packageid,msnfp_identifier,createdon&$orderby=createdon desc&";
			query += "$filter=_msnfp_appealid_value eq " + XrmUtility.CleanGuid(appealId);

			// Get all Packages the current Appeal
			var result = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + query);

			if (!(result === undefined || result === null) && result.length == 1) { // If there is only 1 Package record associated => auto populate it into the form
				var id = result[0].msnfp_packageid;
				var name = result[0].msnfp_identifier;
				var type = "msnfp_package";

				formContext.getAttribute(_packageFieldSchemaName).setValue([{ id: XrmUtility.CleanGuid(id), name: name, entityType: type }]);
			} else { // If there is none or more than 1 Package records => clear out the Package field
				formContext.getAttribute(_packageFieldSchemaName).setValue();
			}
		}
	}
}

function autoPopulateDesignationDependingOnCampaign(ExecutionContext, _campaignFieldSchemaName, _designationFieldSchemaName) { // msnfp_originatingcampaignid , msnfp_designationid
	let formContext = ExecutionContext.getFormContext();
	if (formContext.getAttribute(_campaignFieldSchemaName) && formContext.getAttribute(_designationFieldSchemaName)) {
		if (formContext.getAttribute(_campaignFieldSchemaName).getValue()) {
			var campaignId = formContext.getAttribute(_campaignFieldSchemaName).getValue()[0].id;

			var select = "campaigns(" + XrmUtility.CleanGuid(campaignId) + ")?";
			var expand = "$expand=msnfp_Campaign_DefaultDesignation($select=msnfp_designationid,msnfp_name)";

			// Get the associated Designation record of the current Campaign
			var result = XrmServiceUtility.ExecuteQueryWithExpand(XrmServiceUtility.GetWebAPIUrl() + select + expand);

			if (!(result === undefined || result === null) && result.msnfp_Campaign_DefaultDesignation) { // If there is a record associated => auto populate it into the form
				var id = result.msnfp_Campaign_DefaultDesignation["msnfp_designationid"];
				var name = result.msnfp_Campaign_DefaultDesignation["msnfp_name"];
				var type = "msnfp_designation";

				formContext.getAttribute(_designationFieldSchemaName).setValue([{ id: XrmUtility.CleanGuid(id), name: name, entityType: type }]);
			} else { // If there is no record => clear out the Designation field
				formContext.getAttribute(_designationFieldSchemaName).setValue();
			}
		}
	}
}

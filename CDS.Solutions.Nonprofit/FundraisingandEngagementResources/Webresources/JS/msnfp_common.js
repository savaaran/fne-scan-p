/*************************************************************************
* © Microsoft. All rights reserved.
*/
// Mocking the Console object if it is not defined by the browser.
if (typeof console == "undefined") { console = { log: function (A) { var B = false; if (B) { alert(A) } } } }

//if (typeof ($) === 'undefined') {
//    $ = parent.$;
//    jQuery = parent.jQuery;
//}

/// <summary>
/// Valid modes of a CRM form.
/// </summary>
FormType = {
    Undefined: 0,
    Create: 1,
    Update: 2,
    ReadOnly: 3,
    Disabled: 4,
    QuickCreated: 5,
    BulkEdit: 6
};

/// <summary>
/// Here are useful JS functions to be used within any web resource. To be added to overtime as needed.
/// Usage: After importing (using <script src="msnfp_common.js" type="text/javascript"></script>), functions can be called like so: console.log(MissionFunctions.getAbsoluteUrl("/test1"));
/// </summary>
MissionFunctions = {
    // Logs key value pairs for a given object.
    logKeyValuePairs: function (anObject) {
        Object.keys(anObject).forEach(e => console.log(`key=${e}  value=${anObject[e]}`));
    },

    /* Executes a given function once. Useful if a function is supposed to be executed exactly one time:
       Usage:
        var canOnlyFireOnce = MissionFunctions.once(function() {
            console.log('Fired!');
        });

        canOnlyFireOnce(); // "Fired!"
        canOnlyFireOnce(); // Nothing.
     */
    once: function (fn, context) {
        var result;

        return function () {
            if (fn) {
                result = fn.apply(context || this, arguments);
                fn = null;
            }

            return result;
        };
    },

    // Usage:
    getAbsoluteUrl: function (url) {

        var a;
        if (!a) a = document.createElement('a');
        a.href = url;

        return a.href;
    },

    // Used to generate random GUID's in web resources (taken from manual donation HTML page).
    // Note that "Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1)" was once a function but using it as a child function below will cause referencing issues:
    RandomGuid: function () {
        return Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + '-' + Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + '-' + Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + '-' +
            Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + '-' + Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1) + Math.floor((1 + Math.random()) * 0x10000).toString(16).substring(1);
    },

    // Get the current Dynamics user id in the form "D364E1A1-XXXX-XXXX-XXXX-000D3AF3F17E":
    GetCurrentUserID: function () {
        var xrm = XrmUtility.get_Xrm();
        var userSettings = xrm.Utility.getGlobalContext().userSettings; // userSettings is an object with user information.
        var currentuserID = userSettings.userId.replace('{', '').replace('}', '').toUpperCase();// Deprecated XRM Method as of V9: xrm.Page.context.getUserId().replace('{', '').replace('}', '').toUpperCase();
        return currentuserID;
    },

    // Get a parameter from the query string by key name.
    // Usage:
    GetQueryStringParam: function (keyToFind) {
        console.log("Triggered: GetQueryStringParam()");
        //Get the any query string parameters and load them into the vals array
        var vals = new Array();
        var found = false;
        if (location.search != "") {
            // For some reason the query is being double encoded. Need to investigate further to see if Dynamics is now doing auto-encoding in Xrm.Utility.openWebResource params field.
            vals = decodeURIComponent(location.search).substr(1).split("&");
            //vals = location.search.substr(1).split("&");
            for (var i in vals) {
                vals[i] = vals[i].replace(/\+/g, " ").split("=");
            }
            // Look for the Dynamics 365 parameter named 'data'
            for (var i in vals) {
                if (vals[i][0].toLowerCase() == "data") {
                    var paramvals = new Array();
                    // From the 'data' string, get the parameters into a new array seperated by & symbol:
                    paramvals = decodeURIComponent(vals[i][1]).split("&");
                    for (var i in paramvals) {
                        // For each parameter, split it on '=' symbol to get key-value pairs:
                        paramvals[i] = paramvals[i].replace(/\+/g, " ").split("=");

                        // If this is the keyToFind, return the value and exit:
                        if (paramvals[i][0].toLowerCase() == keyToFind.toLowerCase()) {
                            found = true;
                            return paramvals[i][1];
                        }
                    }
                }
            }
        }
        if (!found) {
            alert("Data parameter '" + keyToFind + "' was not passed to this page. Please try again.")
            return null;
        }
    },

    // Return the key in a given dictionary object given the value:
    GetKeyByValue: function (object, value) {
        return Object.keys(object).find(key => object[key] === value);
    },

    // Get the lookup GUID by field name.
    // Usage: MissionFunctions.GetLookupGuid("customerid"); //(assuming the lookup is on the page).
    GetLookupGuid: function (fieldName) {
        var xrm = XrmUtility.get_Xrm();
        var field = xrm.Page.data.entity.attributes.get(fieldName);
        if (field != null && field.getValue() != null) {
            return xrm.Page.data.entity.attributes.get(fieldName).getValue()[0].id.replace('{', '').replace('}', '');
        }
        return null;
    },

    // Return the optionset label text of a given optionset value/entity.
    // Usage MissionFunctions.GetOptionsetLabelText("msnfp_paymentmethod","msnfp_ccbrandcode","queryResult[0].msnfp_ccbrandcode"); // Returns the card brand for the selected value (Example: "Visa").
    GetOptionsetLabelText: function (entityName, optionSetName, selectedOptionSetValueId) {
        var typeText = "";
        var metaDataSelect = "EntityDefinitions(LogicalName='" + entityName.toLowerCase() + "')/Attributes/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options),GlobalOptionSet($select=Options)";
        var metaDataResults = XrmServiceUtility.ExecuteQuery(XrmServiceUtility.GetWebAPIUrl() + metaDataSelect);

        // For all optionsets for the selected entity:
        for (var j = 0; j < metaDataResults.length; j++) {
            // If the name matches what we want:
            if (metaDataResults[j].LogicalName == optionSetName.toLowerCase()) {
                // Now look through the option values for this field and compare to the selectedOptionSetValueId we have. If it matches assign and break:
                for (var k = 0; k < metaDataResults[j].OptionSet.Options.length; k++) {
                    if (metaDataResults[j].OptionSet.Options[k].Value == selectedOptionSetValueId) {
                        typeText = metaDataResults[j].OptionSet.Options[k].Label.LocalizedLabels[0].Label;
                        break;
                    }
                }
            }
        }

        return typeText;
    },

    GetEnityValue: name => {
        let attr = XrmUtility.get_Xrm().Page.getAttribute(name);
        return attr ? attr.getValue() : null;
    },

    GetEntityLookup: function (name) {
        let val = this.GetEnityValue(name);
        return val && `/${val[0].entityType}s(${XrmUtility.CleanGuid(val[0].id)})`;
    },

    GetEntityLookups: function (names) {
        let obj = Object.assign({}, ...names.map(n => {
            let val = this.GetEntityLookup(n.toLowerCase());
            return val && { [`${n}@odata.bind`]: val };
        }));
        return obj;
    }
};

XrmUtility = {
    get_Xrm: function () {
        /// <summary>
        /// Retrieves the Xrm client-side object.
        /// </summary>
        var xrm;

        //if (!isNullOrUndefined(xrm)) {
        if (typeof (Xrm) !== 'undefined') {
            xrm = Xrm;
        }
        else if (window.parent.Xrm) {
            xrm = window.parent.Xrm; // If called from an IFRAME-embedded HTML web resource.
        }
        else if (window.opener.Xrm) {
            xrm = window.opener.Xrm; // If called from an HTML web resource opened in a new window.
        }
        else {
            throw new Error('Unable to retrieve Xrm object');
        }
        return xrm;
        //}
    },

    CleanGuid: function (sId) {
        /// <summary>
        /// Removes the curly brace delimiters from the supplied ID value.
        /// </summary>
        /// <param name="sId" type="String" mayBeNull="false" optional="false" />
        return sId.replace('{', '').replace('}', '').toUpperCase();
    },

    GetObjectTypeCode: function (entityName) {
        var lookupService = new RemoteCommand("LookupService", "RetrieveTypeCode");
        lookupService.SetParameter("entityName", entityName);
        var result = lookupService.Execute();

        if (result.Success && typeof result.ReturnValue == "number") {
            return result.ReturnValue;
        }
        else {
            return null;
        }
    }
};

XrmServiceUtility = {
    GetWebAPIUrl: function () {
        /// <summary>
        /// Retrieves the absolute URL of the CRM OrganizationData service endpoint.
        /// </summary>
        var xrm = XrmUtility.get_Xrm();
        //return xrm.Page.context.getClientUrl() + "/XRMServices/2011/OrganizationData.svc";
        return xrm.Page.context.getClientUrl() + "/api/data/v8.2/";

        // Temporarily reverting to old technique above, as the method below is causing errors in some web resources:

        //var apiVersion = Xrm.Utility.getGlobalContext().getVersion();
        //var shortVersion = apiVersion.substring(0, apiVersion.indexOf(".") + 2);
        //return Xrm.Utility.getGlobalContext().getClientUrl() + "/api/data/" + shortVersion + "/";
    },

    ExecuteQuery: function (oDataQuery) {
        /// <summary>
        /// Executes the supplied OData query and returns the results.
        /// </summary>
        /// <param name="oDataQuery" type="String" mayBeNull="false" optional="false" />
        var results;
        $.ajax({
            type: "GET",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            async: false,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Accept", "application/json");
            },
            success: function (data, textStatus, xhr) {
                if (data.value != null && data.value.length) {
                    results = data.value;
                }
            },
            error: function (xhr, textStatus, errorThrown) {
                alert(xhr.responseJSON["error"].message);
                //if (xhr && xhr.responseText) {
                //    console.error('OData Query Failed: \r\n' + oDataQuery + '\r\n' + xhr.responseText);
                //}
                //throw new Error(errorThrown);
            }
        });

        return results;
    },

    ExecuteQueryAsync: function (apiQuery, successFunction) {
        /// <summary>
        /// Returns an XML DOM object containing entity metadata.
        /// </summary>
        var results;
        $.ajax({
            type: "GET",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: apiQuery,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Accept", "application/json");
            },
            success: function (data, textStatus, xhr) {
                //if (data.d.results && data.d.results.length > 0) {
                if (data.value != null && data.value.length) {
                    results = data.value;
                }
                successFunction(results);
            },
            error: function (xhr, textStatus, errorThrown) {
                alert(xhr.responseJSON["error"].message);
                //if (xhr && xhr.responseText) {
                //    console.error('OData Query Failed: \r\n' + apiQuery + '\r\n' + xhr.responseText);
                //}
                //throw new Error(errorThrown);
            }
        });

        //console.log('ExecuteQuery()<= ' + new Date() + '; oDataQuery=' + oDataQuery);

        return results;
    },

    ExecuteQueryWithExpand: function (oDataQuery) {
        /// <summary>
        /// Executes the supplied OData query and returns the results.
        /// </summary>
        /// <param name="oDataQuery" type="String" mayBeNull="false" optional="false" />
        var results;
        $.ajax({
            type: "GET",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            async: false,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Accept", "application/json");
                xhr.setRequestHeader("Prefer", "odata.include-annotations=*");
            },
            success: function (data, textStatus, xhr) {
                if (data != null) {
                    results = data;
                }
            },
            error: function (xhr, textStatus, errorThrown) {
                alert(xhr.responseJSON["error"].message);
                //if (xhr && xhr.responseText) {
                //    console.error('OData Query Failed: \r\n' + oDataQuery + '\r\n' + xhr.responseText);
                //}
                //throw errorThrown;
            }
        });

        return results;
    },

    AssociateEntities: function (entity1Id,
        entity1Name,
        entity2Id,
        entity2Name,
        relationshipName,
        successCallback,
        errorCallback) {
        var req = new XMLHttpRequest();
        var Xrm = XrmUtility.get_Xrm();
        var odatapath = XrmServiceUtility.GetWebAPIUrl();
        var query = "";
        query += odatapath + entity1Name + "(" + entity1Id + ")/" + relationshipName + "/$ref";
        var requestBody = { "@odata.id": "" + odatapath + entity2Name + "(" + entity2Id + ")" };
        req.open("POST", encodeURI(query), true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.onreadystatechange = function () {
            if (this.readyState == 4 /* complete */) {
                req.onreadystatechange = null;
                if (this.status == 204 || this.status == 1223 || this.status == 200) {
                    successCallback();
                } else {
                    var xml = this.responseText;
                    var xmlDoc = $.parseXML(xml);
                    if ($(xmlDoc).find("faultstring").length > 0) {
                        alert($(xmlDoc).find("faultstring").text());
                    } else {
                        alert(xml);
                    }
                }
            }
        };
        req.send(JSON.stringify(requestBody));
    },

    //DisassociateCollectionEntities: function (entity1Name, entity1Id, relationshipName, entity2Id, successCallback, errorCallback) {
    DeleteLookupReference: function (entity1Id, entity1Name, lookupfieldName, successCallback, errorCallback) {
        var req = new XMLHttpRequest();
        var Xrm = XrmUtility.get_Xrm();
        var odatapath = XrmServiceUtility.GetWebAPIUrl();
        var query = odatapath + entity1Name + '(' + entity1Id + ')/' + lookupfieldName + '/$ref';
        req.open("DELETE", encodeURI(query), true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        //req.setRequestHeader("X-HTTP-Method", "DELETE");
        req.onreadystatechange = function () {
            if (this.readyState == 4) {
                req.onreadystatechange = null;
                if (this.status == 204 || this.status == 1223) {
                    if (successCallback !== null && successCallback !== undefined)
                        successCallback();
                } else {
                    var xml = req.responseText;
                    var xmlDoc = $.parseXML(xml);
                }
            }
        };
        req.send();
    },

    DisassociateEntities: function (entity1Id,
        entity1Name,
        entity2Id,
        relationshipName,
        successCallback,
        errorCallback) {
        var req = new XMLHttpRequest();
        var Xrm = XrmUtility.get_Xrm();
        var odatapath = XrmServiceUtility.GetWebAPIUrl();
        var query = odatapath + entity1Name + '(' + entity1Id + ')/' + relationshipName + '/$ref';
        req.open("DELETE", encodeURI(query), true);
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.setRequestHeader("X-HTTP-Method", "DELETE");
        req.onreadystatechange = function () {
            if (this.readyState == 4 /* complete */) {
                req.onreadystatechange = null;
                if (this.status == 204 || this.status == 1223) {
                    if (successCallback !== null && successCallback !== undefined)
                        successCallback();
                } else {
                    var xml = req.responseText;
                    alert($.parseJSON(xml).error.message);
                }
            }
        };
        req.send();
    },

    CreateRecord: function (oDataQuery, entity) {
        var jsonEntityDetail = JSON.stringify(entity);

        var recordID;
        $.ajax({
            type: "POST",
            async: false,
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            data: jsonEntityDetail,
            beforeSend: function (XMLHttpRequest) {
                //Specifying this header ensures that the results will be returned as JSON.
                XMLHttpRequest.setRequestHeader("Accept", "application/json");
                XMLHttpRequest.setRequestHeader("Content-Type", "application/json; charset=utf-8");
                XMLHttpRequest.setRequestHeader("Prefer", "odata.include-annotations=*");
                XMLHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
                XMLHttpRequest.setRequestHeader("OData-Version", "4.0");
            },
            success: function (data, textStatus, XmlHttpRequest) {
                var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
                recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
            },
            error: function (XMLHttpRequest, textStatus, errorThrown) {
                alert(XMLHttpRequest.responseJSON["error"].message);
                //alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
            }
        });

        return recordID;
    },

    CreateRecordAsync: function (oDataQuery, entity, successFunction, failureFunction, optData = null) {
        var jsonEntityDetail = JSON.stringify(entity);

        var recordID;
        $.ajax({
            type: "POST",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            data: jsonEntityDetail,
            beforeSend: function (XMLHttpRequest) {
                //Specifying this header ensures that the results will be returned as JSON.
                XMLHttpRequest.setRequestHeader("Accept", "application/json");
                XMLHttpRequest.setRequestHeader("Content-Type", "application/json; charset=utf-8");
                XMLHttpRequest.setRequestHeader("Prefer", "odata.include-annotations=*");
                XMLHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
                XMLHttpRequest.setRequestHeader("OData-Version", "4.0");
            },
            success: function (data, textStatus, XmlHttpRequest) {
                var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
                recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
                if (successFunction != null) {
                    if (optData != null) {
                        successFunction(recordID, optData);
                    } else {
                        successFunction(recordID);
                    }
                }
            },
            error: function (XMLHttpRequest, textStatus, errorThrown) {
                failureFunction(XMLHttpRequest.responseJSON["error"].message);
                //alert(XMLHttpRequest.responseJSON["error"].message);
                //alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
            }
        });

        return recordID;
    },

    UpdateRecord: function (oDataQuery, entity) {
        var jsonEntityDetail = JSON.stringify(entity);

        var recordID;
        $.ajax({
            type: "PATCH",
            async: false,
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            data: jsonEntityDetail,
            beforeSend: function (XMLHttpRequest) {
                //Specifying this header ensures that the results will be returned as JSON.
                XMLHttpRequest.setRequestHeader("Accept", "application/json");
            },
            success: function (data, textStatus, XmlHttpRequest) {
                var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
                recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
            },
            error: function (XMLHttpRequest, textStatus, errorThrown) {
                alert(XMLHttpRequest.responseJSON["error"].message);
                //alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
            }
        });

        return recordID;
    },

    UpdateRecordAsync: function (oDataQuery, entity, successFunction, failureFunction) {
        var jsonEntityDetail = JSON.stringify(entity);

        var recordID;
        $.ajax({
            type: "PATCH",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            data: jsonEntityDetail,
            beforeSend: function (XMLHttpRequest) {
                //Specifying this header ensures that the results will be returned as JSON.
                XMLHttpRequest.setRequestHeader("Accept", "application/json");
            },
            success: function (data, textStatus, XmlHttpRequest) {
                var recordUri = XmlHttpRequest.getResponseHeader("OData-EntityId");
                recordID = recordUri.substr(recordUri.length - 38).substring(1, 37);
                if (successFunction != null) {
                    successFunction(recordID);
                }
            },
            error: function (XMLHttpRequest, textStatus, errorThrown) {
                failureFunction(XMLHttpRequest.responseJSON["error"].message);
                //alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
            }
        });

        return recordID;
    },

    DeleteRecord: function (id, entityName) {
        var oDataQuery = XrmServiceUtility.GetWebAPIUrl() + "/" + entityName + "(" + id + ")";
        $.ajax({
            type: "DELETE",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            async: false,
            url: oDataQuery,
            beforeSend: function (XMLHttpRequest) {
                //Specifying this header ensures that the results will be returned as JSON.
                XMLHttpRequest.setRequestHeader("Accept", "application/json");

                //Specify the HTTP method DELETE to perform a delete operation.
                //XMLHttpRequest.setRequestHeader("X-HTTP-Method", "DELETE");
            },
            success: function (data, textStatus, XmlHttpRequest) {

            },
            error: function (XMLHttpRequest, textStatus, errorThrown) {
                alert(XMLHttpRequest.responseJSON["error"].message);
                //alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
            }
        });
    },

    DeleteRecordAsync: function (id, entityName, successFunction, failureFunction) {
        var oDataQuery = XrmServiceUtility.GetWebAPIUrl() + "/" + entityName + "(" + id + ")";
        $.ajax({
            type: "DELETE",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: oDataQuery,
            beforeSend: function (XMLHttpRequest) {
                //Specifying this header ensures that the results will be returned as JSON.
                XMLHttpRequest.setRequestHeader("Accept", "application/json");

                //Specify the HTTP method DELETE to perform a delete operation.
                //XMLHttpRequest.setRequestHeader("X-HTTP-Method", "DELETE");
            },
            success: function (data, textStatus, XmlHttpRequest) {
                if (successFunction !== undefined && successFunction !== null)
                    successFunction();
            },
            error: function (XMLHttpRequest, textStatus, errorThrown) {
                if (failureFunction !== undefined && failureFunction !== null)
                    failureFunction(XMLHttpRequest.responseJSON["error"].message);
                //alert("failure :" + errorThrown + " " + XMLHttpRequest + " " + textStatus);
            }
        });
    },
};

MetadataQuery = {
    /// <summary>
    /// Allows querying the CRM Metadata tables for Entity and Relationship information.
    /// </summary>
    GetAllEntities: function () {
        var query = XrmServiceUtility.GetWebAPIUrl() + "/EntityDefinitions?$select=LogicalName,DisplayName,IsCustomizable&$filter=IsCustomizable/Value eq true";
        var entityResults = XrmServiceUtility.ExecuteQuery(query);
        return entityResults;
    },

    GetOptionSetValues: function (apiQuery) {
        /// <summary>
        /// Returns an XML DOM object containing entity metadata.
        /// </summary>
        var results;
        $.ajax({
            type: "GET",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: apiQuery,
            async: false,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Accept", "application/json");
            },
            success: function (data, textStatus, xhr) {
                //if (data.d.results && data.d.results.length > 0) {

                if (data.value != null && data.value.length) {
                    results = data.value;
                }
            },
            error: function (xhr, textStatus, errorThrown) {
                alert(xhr.responseJSON["error"].message);
                //if (xhr && xhr.responseText) {
                //    console.error('OData Query Failed: \r\n' + apiQuery + '\r\n' + xhr.responseText);
                //}
                //throw new Error(errorThrown);
            }
        });

        //console.log('ExecuteQuery()<= ' + new Date() + '; oDataQuery=' + oDataQuery);

        return results;
    },

    GetOptionSetValuesAsync: function (apiQuery, successFunction) {
        /// <summary>
        /// Returns an XML DOM object containing entity metadata.
        /// </summary>
        var results;
        $.ajax({
            type: "GET",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            url: apiQuery,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Accept", "application/json");
            },
            success: function (data, textStatus, xhr) {
                //if (data.d.results && data.d.results.length > 0) {

                if (data.value != null && data.value.length) {
                    results = data.value;
                }
                successFunction(results);
            },
            error: function (xhr, textStatus, errorThrown) {
                alert(xhr.responseJSON["error"].message);
                //if (xhr && xhr.responseText) {
                //    console.error('OData Query Failed: \r\n' + apiQuery + '\r\n' + xhr.responseText);
                //}
                //throw new Error(errorThrown);
            }
        });

        //console.log('ExecuteQuery()<= ' + new Date() + '; oDataQuery=' + oDataQuery);

        return results;
    },
};

//download.js v3.0, by dandavis; 2008-2014. [CCBY2] see http://danml.com/download.html for tests/usage
// v1 landed a FF+Chrome compat way of downloading strings to local un-named files, upgraded to use a hidden frame and optional mime
// v2 added named files via a[download], msSaveBlob, IE (10+) support, and window.URL support for larger+faster saves than dataURLs
// v3 added dataURL and Blob Input, bind-toggle arity, and legacy dataURL fallback was improved with force-download mime and base64 support

// data can be a string, Blob, File, or dataURL

function download(data, strFileName, strMimeType) {

    var self = window, // this script is only for browsers anyway...
        u = "application/octet-stream", // this default mime also triggers iframe downloads
        m = strMimeType || u,
        x = data,
        D = document,
        a = D.createElement("a"),
        z = function (a) { return String(a); },


        B = self.Blob || self.MozBlob || self.WebKitBlob || z,
        BB = self.MSBlobBuilder || self.WebKitBlobBuilder || self.BlobBuilder,
        fn = strFileName || "download",
        blob,
        b,
        ua,
        fr;

    //if(typeof B.bind === 'function' ){ B=B.bind(self); }

    if (String(this) === "true") { //reverse arguments, allowing download.bind(true, "text/xml", "export.xml") to act as a callback
        x = [x, m];
        m = x[0];
        x = x[1];
    }



    //go ahead and download dataURLs right away
    if (String(x).match(/^data\:[\w+\-]+\/[\w+\-]+[,;]/)) {
        return navigator.msSaveBlob ?  // IE10 can't do a[download], only Blobs:
            navigator.msSaveBlob(d2b(x), fn) :
            saver(x); // everyone else can save dataURLs un-processed
    }//end if dataURL passed?

    try {

        blob = x instanceof B ?
            x :
            new B([x], { type: m });
    } catch (y) {
        if (BB) {
            b = new BB();
            b.append([x]);
            blob = b.getBlob(m); // the blob
        }

    }



    function d2b(u) {
        var p = u.split(/[:;,]/),
            t = p[1],
            dec = p[2] == "base64" ? atob : decodeURIComponent,
            bin = dec(p.pop()),
            mx = bin.length,
            i = 0,
            uia = new Uint8Array(mx);

        for (i; i < mx; ++i) uia[i] = bin.charCodeAt(i);

        return new B([uia], { type: t });
    }

    function saver(url, winMode) {


        if ('download' in a) { //html5 A[download] 			
            a.href = url;
            a.setAttribute("download", fn);
            a.innerHTML = "downloading...";
            D.body.appendChild(a);
            setTimeout(function () {
                a.click();
                D.body.removeChild(a);
                if (winMode === true) { setTimeout(function () { self.URL.revokeObjectURL(a.href); }, 250); }
            }, 66);
            return true;
        }

        //do iframe dataURL download (old ch+FF):
        var f = D.createElement("iframe");
        D.body.appendChild(f);
        if (!winMode) { // force a mime that will download:
            url = "data:" + url.replace(/^data:([\w\/\-\+]+)/, u);
        }


        f.src = url;
        setTimeout(function () { D.body.removeChild(f); }, 333);

    }//end saver 


    if (navigator.msSaveBlob) { // IE10+ : (has Blob, but not a[download] or URL)
        return navigator.msSaveBlob(blob, fn);
    }

    if (self.URL) { // simple fast and modern way using Blob and URL:
        saver(self.URL.createObjectURL(blob), true);
    } else {
        // handle non-Blob()+non-URL browsers:
        if (typeof blob === "string" || blob.constructor === z) {
            try {
                return saver("data:" + m + ";base64," + self.btoa(blob));
            } catch (y) {
                return saver("data:" + m + "," + encodeURIComponent(blob));
            }
        }

        // Blob but not URL:
        fr = new FileReader();
        fr.onload = function (e) {
            saver(this.result);
        };
        fr.readAsDataURL(blob);
    }
    return true;
} /* end download() */

function cardnumber() {
    var AE = /^(?:3[47][0-9]{13})$/;                 //Amercican Express
    var visa = /^(?:4[0-9]{12}(?:[0-9]{3})?)$/;      //Visa
    var MC = /^(?:5[1-5][0-9]{14})$/;                //Mastercard
    var Dis = /^(?:6(?:011|5[0-9][0-9])[0-9]{12})$/; //Discover
    var DC = /^(?:3(?:0[0-5]|[68][0-9])[0-9]{11})$/; //Dinners Club
    var JCB = /^(?:2131|1800|35\d{3})\d{11}$/;       //JCB
    var card_val = {
        "AE": 844060004,
        "visa": 844060000,
        "MC": 844060001,
        "Dis": 844060008,
        "DC": 844060005,
        "JCB": 844060006
    };

    var num = $(this).val();
    var match = true;
    var card_type = "";

    if (num.match(AE))
        card_type = "AE";
    else if (num.match(visa))
        card_type = "visa";
    else if (num.match(MC))
        card_type = "MC";
    else if (num.match(Dis))
        card_type = "Dis";
    else if (num.match(DC))
        card_type = "DC";
    else if (num.match(JCB))
        card_type = "JCB";
    else
        match = false;

    if (match) {
        $(this).removeClass('red-border');
        $('#ddlCardType').val(card_val[card_type]);
    }
    return match;
}

if (typeof $ === "function") {
    $(function () {
        if ($('#txtCardNumber').length && $('#ddlCardType').length)
            $('#txtCardNumber').change(cardnumber);
        else
            console.log('#txtCardNumber, #ddlCardType not found.');
    });
}
/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2021-10-29
 */
using Hl7.Fhir.Model;
using Microsoft.IdentityModel.Tokens;
using RestSrvr;
using RestSrvr.Exceptions;
using RestSrvr.Message;
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Rest.Serialization;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Security;
using static Hl7.Fhir.Model.OperationOutcome;

namespace SanteDB.Messaging.FHIR.Rest.Behavior
{
    /// <summary>
    /// Service behavior
    /// </summary>
    [ExcludeFromCodeCoverage]
    [DisplayName("FHIR R4 OperationOutcome Error Responses")]
    public class FhirErrorEndpointBehavior :  IServiceBehavior, IServiceErrorHandler
    {

        private Tracer m_tracer = new Tracer(FhirConstants.TraceSourceName);

        /// <summary>
        /// Classify the error code
        /// </summary>
        public static int ClassifyErrorCode(Exception error)
        {
            if (error is FhirException fhirException)
            {
                return (int)fhirException.Status;
            }
            else
            {
                // Get to the root of the error
                while (error.InnerException != null)
                {
                    error = error.InnerException;
                }

                // Formulate appropriate response
                if (error is DomainStateException)
                    return (int)System.Net.HttpStatusCode.ServiceUnavailable;
                else if (error is PolicyViolationException)
                {
                    var pve = error as PolicyViolationException;
                    if (pve.PolicyDecision == PolicyGrantType.Elevate)
                    {
                        // Ask the user to elevate themselves
                        return 401;
                    }
                    else
                    {
                        return 403;
                    }
                }
                else if (error is SecurityException || error is UnauthorizedAccessException)
                    return (int)System.Net.HttpStatusCode.Forbidden;
                else if (error is SecurityTokenException)
                {
                    return (int)System.Net.HttpStatusCode.Unauthorized;
                }
                else if (error is SecuritySessionException ses)
                {
                    switch (ses.Type)
                    {
                        case SessionExceptionType.Expired:
                        case SessionExceptionType.NotYetValid:
                        case SessionExceptionType.NotEstablished:
                            return (int)System.Net.HttpStatusCode.Unauthorized;
                        default:
                            return (int)System.Net.HttpStatusCode.Forbidden;
                          
                    }
                }
                else if (error is ArgumentException)
                    return (int)400;
                else if (error is FaultException)
                    return (int)(error as FaultException).StatusCode;
                else if (error is Newtonsoft.Json.JsonException ||
                    error is System.Xml.XmlException)
                    return (int)System.Net.HttpStatusCode.BadRequest;
                else if (error is FileNotFoundException || error is KeyNotFoundException)
                    return (int)System.Net.HttpStatusCode.NotFound;
                else if (error is DbException || error is ConstraintException)
                    return (int)(System.Net.HttpStatusCode)422;
                else if (error is PatchException)
                    return (int)System.Net.HttpStatusCode.Conflict;
                else if (error is NotImplementedException)
                    return (int)System.Net.HttpStatusCode.NotImplemented;
                else if (error is NotSupportedException)
                    return (int)System.Net.HttpStatusCode.MethodNotAllowed;
                else if (error is DetectedIssueException)
                    return (int)422;
                else
                    return (int)System.Net.HttpStatusCode.InternalServerError;

            }
        }

        /// <summary>
        /// Apply the service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.ErrorHandlers.Clear();
            dispatcher.ErrorHandlers.Add(this);
        }

        /// <summary>
        /// This error handle can handle all errors
        /// </summary>
        public bool HandleError(Exception error)
        {
            return true;
        }

        /// <summary>
        /// Provide a fault
        /// </summary>
        public bool ProvideFault(Exception error, RestResponseMessage response)
        {
            this.m_tracer.TraceEvent(EventLevel.Error, "Error on WCF FHIR Pipeline: {0}", error);

            RestOperationContext.Current.OutgoingResponse.StatusCode = ClassifyErrorCode(error);
            if(RestOperationContext.Current.OutgoingResponse.StatusCode == 401)
            {
                // Get to the root of the error
                while (error.InnerException != null)
                    error = error.InnerException;

                if (error is PolicyViolationException pve)
                {
                    var method = RestOperationContext.Current.AppliedPolicies.Any(o => o.GetType().Name.Contains("Basic")) ? "Basic" : "Bearer";
                    response.AddAuthenticateHeader(method, RestOperationContext.Current.IncomingRequest.Url.Host, "insufficient_scope", pve.PolicyId, error.Message);
                }
                else if(error is SecurityTokenException ste)
                {
                    response.AddAuthenticateHeader("Bearer", RestOperationContext.Current.IncomingRequest.Url.Host, "token_error", description: ste.Message);
                }
                else if (error is SecuritySessionException ses)
                {
                    switch (ses.Type)
                    {
                        case SessionExceptionType.Expired:
                        case SessionExceptionType.NotYetValid:
                        case SessionExceptionType.NotEstablished:
                            response.StatusCode = (int)System.Net.HttpStatusCode.Unauthorized;
                            response.AddAuthenticateHeader("Bearer", RestOperationContext.Current.IncomingRequest.Url.Host, "unauthorized", PermissionPolicyIdentifiers.Login, ses.Message);
                            break;
                        default:
                            response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
                            break;
                    }
                }

            }
            
            var errorResult = DataTypeConverter.CreateErrorResult(error);

            // Return error in XML only at this point
            new FhirMessageDispatchFormatter().SerializeResponse(response, null, errorResult);
            return true;
        }
    }
}

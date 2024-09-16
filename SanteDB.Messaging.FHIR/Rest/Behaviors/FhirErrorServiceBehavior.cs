/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using RestSrvr;
using RestSrvr.Message;
using SanteDB.Core.Diagnostics;
using SanteDB.Messaging.FHIR.Rest.Serialization;
using SanteDB.Messaging.FHIR.Util;
using SanteDB.Rest.Common;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;

namespace SanteDB.Messaging.FHIR.Rest.Behavior
{
    /// <summary>
    /// Service behavior
    /// </summary>
    [ExcludeFromCodeCoverage]
    [DisplayName("FHIR R4 OperationOutcome Error Responses")]
    public class FhirErrorEndpointBehavior : IServiceBehavior, IServiceErrorHandler
    {
        private readonly Tracer m_tracer = new Tracer(FhirConstants.TraceSourceName);

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

            response.StatusCode = error.GetHttpStatusCode();

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    var authService = RestOperationContext.Current.AppliedPolicies.OfType<IAuthorizationServicePolicy>().FirstOrDefault();
                    authService.AddAuthenticateChallengeHeader(response, error);
                    break;
                case (HttpStatusCode)429:
                    response.Headers.Add("Retry-After", "3600");
                    break;
            }

            var errorResult = DataTypeConverter.CreateErrorResult(error);

            // Return error in XML only at this point
            new FhirMessageDispatchFormatter().SerializeResponse(response, null, errorResult);
            return true;
        }
    }
}
/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-5
 */
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using static Hl7.Fhir.Model.OperationOutcome;

namespace SanteDB.Messaging.FHIR.Exceptions
{
    /// <summary>
    /// Represents an exception with a specific code and severity
    /// </summary>
    public class FhirException : Exception
    {

        /// <summary>
        /// Creates a new FHIR exception with specified code
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <param name="fhirCode">The FHIR text</param>
        /// <param name="diagnostics">The diagnostic text</param>
        public FhirException(HttpStatusCode statusCode, IssueType fhirCode, String diagnostics) : this(statusCode, fhirCode, diagnostics, null)
        {

        }

        /// <summary>
        /// Creates a new FHIR exception with specified code
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <param name="fhirCode">The FHIR text</param>
        /// <param name="diagnostics">The diagnostic text</param>
        /// <param name="innerException">The cause of this exception</param>
        public FhirException(HttpStatusCode statusCode, IssueType fhirCode, String diagnostics, Exception innerException) : base(diagnostics, innerException)
        {
            this.Status = statusCode;
            this.Code = fhirCode;
        }

        /// <summary>
        /// Creates a new FHIR exception with specified code
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <param name="diagnostics">The diagnostic text</param>
        public FhirException(HttpStatusCode statusCode, Resource responseResource, String diagnostics, Exception innerException) : base(diagnostics, innerException)
        {
            this.Status = statusCode;
            this.Resource = responseResource;
        }

        /// <summary>
        /// Gets the HTTP status code
        /// </summary>
        public HttpStatusCode Status { get; }

        /// <summary>
        /// Gets the resource the thrower wants to return
        /// </summary>
        public Resource Resource { get; }

        /// <summary>
        /// Gets the FHIR code
        /// </summary>
        public IssueType Code { get; }
    }
}

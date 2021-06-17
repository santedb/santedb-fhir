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
        /// Gets the HTTP status code
        /// </summary>
        public HttpStatusCode Status { get; }

        /// <summary>
        /// Gets the FHIR code
        /// </summary>
        public IssueType Code { get; }
    }
}

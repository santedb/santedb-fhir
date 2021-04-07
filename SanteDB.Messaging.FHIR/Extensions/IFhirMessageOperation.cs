using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// FHIR Message Operations
    /// </summary>
    public interface IFhirMessageOperation
    {

        /// <summary>
        /// Gets the event URI which this operation handles
        /// </summary>
        Uri EventUri { get; }

        /// <summary>
        /// Invoke this message with the specified request header component and the non-message infrastructure related entries
        /// </summary>
        /// <param name="requestHeader">The request header indicating the transaction details</param>
        /// <param name="entries">The entries for the operation</param>
        /// <returns>The response details for the message</returns>
        Resource Invoke(MessageHeader requestHeader, params Bundle.EntryComponent[] entries);
    }
}

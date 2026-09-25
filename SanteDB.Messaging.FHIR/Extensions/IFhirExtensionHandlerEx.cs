using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// FHIR extension handler extended interface
    /// </summary>
    public interface IFhirExtensionHandlerEx : IFhirExtensionHandler
    {

        /// <summary>
        /// Gets the type of extension that the handler returns / uses
        /// </summary>
        FHIRAllTypes ValueType { get; }

    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Configuration
{
    /// <summary>
    /// FHIR Response format configuration
    /// </summary>
    [XmlType(nameof(FhirResponseFormatConfiguration), Namespace = "http://santedb.org/configuration")]
    public enum FhirResponseFormatConfiguration
    {
        /// <summary>
        /// JSON format
        /// </summary>
        [XmlEnum("application/fhir+json")]
        Json,

        /// <summary>
        /// XML format
        /// </summary>
        [XmlEnum("application/fhir+xml")]
        Xml
    }
}

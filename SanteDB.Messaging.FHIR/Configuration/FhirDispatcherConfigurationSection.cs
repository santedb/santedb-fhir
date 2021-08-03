using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Configuration
{

    /// <summary>
    /// Get the dispatcher target configuration
    /// </summary>
    public class FhirDispatcherTargetConfiguration
    {

        /// <summary>
        /// Gets the name of the target
        /// </summary>
        [XmlElement("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the endpoint where audits should be sent
        /// </summary>
        [XmlElement("endpoint")]
        public String Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the username or authentication data
        /// </summary>
        [XmlElement("user")]
        public String UserName { get; set; }

        /// <summary>
        /// Gets or sets the password for authentication data
        /// </summary>
        [XmlElement("password")]
        public String Password { get; set; }

        /// <summary>
        /// Gets or sets the class which authenticates requests
        /// </summary>
        [XmlElement("authenticator")]
        public TypeReferenceConfiguration Authenticator { get; set; }
    }

    // <summary>
    /// Configuration section for the dispatching of FHIR messages
    /// </summary>
    [XmlType(nameof(FhirDispatcherConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class FhirDispatcherConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// Creates a new dispatch configuration section
        /// </summary>
        public FhirDispatcherConfigurationSection()
        {
            this.Targets = new List<FhirDispatcherTargetConfiguration>();
        }

        /// <summary>
        /// Targets for the dispatcher
        /// </summary>
        [XmlArray("targets"), XmlArrayItem("add")]
        public List<FhirDispatcherTargetConfiguration> Targets { get; set; }
       
    }
}

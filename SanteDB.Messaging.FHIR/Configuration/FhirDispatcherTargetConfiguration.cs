using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Attributes;
using SanteDB.Messaging.FHIR.Rest;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Configuration
{
    /// <summary>
    /// Get the dispatcher target configuration
    /// </summary>
    [ExcludeFromCodeCoverage]
    [XmlType(nameof(FhirDispatcherTargetConfiguration), Namespace = "http://santedb.org/configuration")]
    public class FhirDispatcherTargetConfiguration
    {
        /// <summary>
        /// Gets or sets the endpoint where audits should be sent
        /// </summary>
        [XmlElement("endpoint")]
        [DisplayName("Endpoint URL")]
        [Description("The remote endpoint for the FHIR endpoint")]
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the username or authentication data
        /// </summary>
        [XmlElement("user")]
        [DisplayName("Authentication")]
        [Description("If the remote endpoint requires an authentication scheme, this is the username to pass to the authenticator")]
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password for authentication data
        /// </summary>
        [XmlElement("password")]
        [DisplayName("Secret")]
        [Description("If the remote endpoint requires authentication, this is the secret to pass to the authenticator")]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the class which authenticates requests
        /// </summary>
        [XmlElement("authenticator")]
        [DisplayName("Authenticator")]
        [Description("The authentication plugin to use to pre-authenticate this SanteDB server against the master server")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing")]
        [Binding(typeof(IFhirClientAuthenticator))]
        public TypeReferenceConfiguration Authenticator { get; set; }

        /// <summary>
        /// Gets the name of the target
        /// </summary>
        [XmlElement("name")]
        [DisplayName("Name")]
        [Description("A unique name for the endpoint configuration. If you're setting up special endpoint settings for a particular " +
                     " endpoint subscription, this should match the name of the endpoint configuration")]
        public string Name { get; set; }
    }
}

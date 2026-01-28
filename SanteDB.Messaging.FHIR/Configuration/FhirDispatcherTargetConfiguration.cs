/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-6-21
 */
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Attributes;
using SanteDB.Messaging.FHIR.Rest;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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

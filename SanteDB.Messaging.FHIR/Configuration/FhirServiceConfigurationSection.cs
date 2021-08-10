/*
 * Portions Copyright 2019-2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE)
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
 * User: fyfej (Justin Fyfe)
 * Date: 2019-11-27
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Attributes;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Configuration
{
    /// <summary>
    /// FHIR service configuration
    /// </summary>
    [XmlType(nameof(FhirServiceConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class FhirServiceConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// Creates a new instance of the WcfEndpoint
        /// </summary>
        public FhirServiceConfigurationSection()
        {
        }

        /// <summary>
        /// Gets the WCF endpoint name that the FHIR service listens on
        /// </summary>
        [XmlAttribute("restEndpoint"), JsonProperty("restEndpoint"), DisplayName("REST API Service Name"), Description("If you're using a service name other than FHIR for the REST API registration, enter it here")]
        public string WcfEndpoint { get; set; }

        /// <summary>
        /// The landing page file
        /// </summary>
        [Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
        [XmlAttribute("index"), JsonProperty("index"), DisplayName("Landing Page"), Description("If you would like the FHIR service to serve out an HTML page on root access instead of the default page, select it here") ]
        public string LandingPage { get; set; }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("resourceHandlers"), XmlArrayItem("add"), JsonProperty("resourceHandlers")]
        [DisplayName("Custom Resources"), Description("If using a custom set of resource handlers (i.e. custom FHIR resource mapping) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirResourceHandler))]
        public List<TypeReferenceConfiguration> ResourceHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("operationHandlers"), XmlArrayItem("add"), JsonProperty("operationHandlers")]
        [DisplayName("Custom Operations"), Description("If using a custom set of operation handlers (i.e. custom FHIR $operations) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirOperationHandler))]
        public List<TypeReferenceConfiguration> OperationHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("messageHandlers"), XmlArrayItem("add"), JsonProperty("messageHandlers")]
        [DisplayName("Custom Messages"), Description("If using a custom set of message handlers (i.e. custom FHIR message events) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirMessageOperation))]
        public List<TypeReferenceConfiguration> MessageHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("extensionHandlers"), XmlArrayItem("add"), JsonProperty("extensionHandlers")]
        [DisplayName("Custom Extensions"), Description("If using a custom set of FHIR extensions handlers (i.e. mapping extensions to RIM) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirExtensionHandler))]
        public List<TypeReferenceConfiguration> ExtensionHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("profileHandlers"), XmlArrayItem("add"), JsonProperty("profileHandlers")]
        [DisplayName("Custom Extensions"), Description("If using a custom set of FHIR profile validators (i.e. custom validation rules) select the validators here. If empty, all validators will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirProfileValidationHandler))]
        public List<TypeReferenceConfiguration> ProfileHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("resources"), XmlArrayItem("add"), JsonProperty("resources")]
        [DisplayName("Allowed Resources"), Description("List of resources which are permitted/exposed on this server (note: empty means all resources are permitted)")]
        public List<String> Resources
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("profiles"), XmlArrayItem("add"), JsonProperty("profiles")]
        [DisplayName("Allowed Profiles"), Description("List of profiles (urls) which are permitted/exposed on this server (note: empty means all profiles are permitted)")]
        public List<String> Profiles
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("messages"), XmlArrayItem("add"), JsonProperty("messages")]
        [DisplayName("Allowed Message Events"), Description("List of message events (URI) which are permitted/exposed on this server (note: empty means all message events are permitted)")]
        public List<String> Messages
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("operations"), XmlArrayItem("add"), JsonProperty("operations")]
        [DisplayName("Allowed Operations"), Description("List of operation names which are permitted/exposed on this server (note: empty means all loaded operations are permitted)")]
        public List<String> Operations
        {
            get; set;
        }

        /// <summary>
        /// Allows configuration of extensions which are active
        /// </summary>
        [XmlArray("extensions"), XmlArrayItem("add"), JsonProperty("extensions")]
        [DisplayName("Allowed Extensions"), Description("List of extension URIs which are permitted/exposed on this server (note: empty means all loaded extensions are permitted)")]
        public List<String> Extensions
        {
            get; set;
        }

        /// <summary>
        /// When set, describes the base uri for all resources on this FHIR service.
        /// </summary>
        [XmlElement("base"), JsonProperty("base")]
        [DisplayName("Operation Base URL"), Description("Used as the base URL for this server. Use this if the incoming HOST header will be different than the external host header (i.e. if running behind a reverse proxy)")]
        public String ResourceBaseUri { get; set; }

        /// <summary>
        /// Behavior modifiers
        /// </summary>
        [XmlArray("behaviorModifiers"), XmlArrayItem("add"), JsonProperty("behaviorModifiers")]
        [DisplayName("Behavior Modifiers"), Description("Behaviors which are are permitted for this operation")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirRestBehaviorModifier))]
        public List<TypeReferenceConfiguration> BehaviorModifiers { get; set; }

        /// <summary>
        /// Default content type
        /// </summary>
        [XmlAttribute("defaultContentType"), JsonProperty("defaultContentType")]
        [DisplayName("Default Format"), Description("The default format to use when the client does not specify a perferred format")]
        public FhirResponseFormatConfiguration DefaultResponseFormat { get; set; }

    }

    /// <summary>
    /// FHIR Response format configuration
    /// </summary>
    [XmlType(nameof(FhirResponseFormatConfiguration), Namespace = "http://santedb.org/configuration")]
    public enum FhirResponseFormatConfiguration
    {
        [XmlEnum("application/fhir+json")]
        Json,
        [XmlEnum("application/fhir+xml")]
        Xml
    }
}

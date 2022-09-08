/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-5-30
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Attributes;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Configuration
{
    /// <summary>
    /// FHIR service configuration
    /// </summary>
    [ExcludeFromCodeCoverage]
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
        [XmlAttribute("index"), JsonProperty("index"), DisplayName("Landing Page"), Description("If you would like the FHIR service to serve out an HTML page on root access instead of the default page, select it here")]
        public string LandingPage { get; set; }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("resourceHandlers"), XmlArrayItem("add"), JsonProperty("resourceHandlers")]
        [DisplayName("Resource Handlers"), Description("If using a custom set of resource handlers (i.e. custom FHIR resource mapping) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirResourceHandler))]
        public List<TypeReferenceConfiguration> ResourceHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("operationHandlers"), XmlArrayItem("add"), JsonProperty("operationHandlers")]
        [DisplayName("Operation Handlers"), Description("If using a custom set of operation handlers (i.e. custom FHIR $operations) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirOperationHandler))]
        public List<TypeReferenceConfiguration> OperationHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("messageHandlers"), XmlArrayItem("add"), JsonProperty("messageHandlers")]
        [DisplayName("Message Handlers"), Description("If using a custom set of message handlers (i.e. custom FHIR message events) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirMessageOperation))]
        public List<TypeReferenceConfiguration> MessageHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("extensionHandlers"), XmlArrayItem("add"), JsonProperty("extensionHandlers")]
        [DisplayName("Extension Handlers"), Description("If using a custom set of FHIR extensions handlers (i.e. mapping extensions to RIM) select the handlers here. If empty, all handlers will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirExtensionHandler))]
        public List<TypeReferenceConfiguration> ExtensionHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("profileHandlers"), XmlArrayItem("add"), JsonProperty("profileHandlers")]
        [DisplayName("Profile Handlers"), Description("If using a custom set of FHIR profile validators (i.e. custom validation rules) select the validators here. If empty, all validators will be loaded and used")]
        [Editor("SanteDB.Configuration.Editors.TypeSelectorEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing"), Binding(typeof(IFhirProfileValidationHandler))]
        public List<TypeReferenceConfiguration> ProfileHandlers
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("resources"), XmlArrayItem("add"), JsonProperty("resources")]
        [Browsable(false)]
        public List<String> Resources
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("profiles"), XmlArrayItem("add"), JsonProperty("profiles")]
        [Browsable(false)]
        public List<String> Profiles
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("messages"), XmlArrayItem("add"), JsonProperty("messages")]
        [Browsable(false)]
        public List<String> Messages
        {
            get; set;
        }

        /// <summary>
        /// XML for resource handlers
        /// </summary>
        [XmlArray("operations"), XmlArrayItem("add"), JsonProperty("operations")]
        [Browsable(false)]
        public List<String> Operations
        {
            get; set;
        }

        /// <summary>
        /// Allows configuration of extensions which are active
        /// </summary>
        [XmlArray("extensions"), XmlArrayItem("add"), JsonProperty("extensions")]
        [Browsable(false)]
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
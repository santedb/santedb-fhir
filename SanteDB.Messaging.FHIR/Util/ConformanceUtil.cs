﻿/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Rest.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Conformance utility
    /// </summary>
    public static class ConformanceUtil
    {
        // Conformance built
        private static CapabilityStatement s_conformance;

        // Sync lock
        private static readonly object s_syncLock = new object();

        // FHIR trace source
        private static readonly Tracer s_traceSource = new Tracer(FhirConstants.TraceSourceName);

        // Configuration section
        private static readonly FhirServiceConfigurationSection s_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<FhirServiceConfigurationSection>();

        /// <summary>
        /// Get Conformance Statement from FHIR service
        /// </summary>
        public static CapabilityStatement GetConformanceStatement()
        {
            if (s_conformance == null)
            {
                lock (s_syncLock)
                {
                    BuildConformanceStatement();
                    RestOperationContext.Current.OutgoingResponse.SetLastModified(DateTime.Now);
                }
            }

            return s_conformance;
        }

        /// <summary>
        /// Build conformance statement
        /// </summary>
        private static void BuildConformanceStatement()
        {
            try
            {

                // No output of any exceptions
                Assembly entryAssembly = Assembly.GetEntryAssembly();

                // First assign the basic attributes
                s_conformance = new CapabilityStatement()
                {
                    Software = new SoftwareComponent()
                    {
                        Name = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>().Product,
                        Version = entryAssembly.GetName().Version.ToString()
                    },
                    DateElement = new FhirDateTime(DateTimeOffset.Now),
                    Description = new Markdown("Automatically generated by ServiceCore FHIR framework"),
                    Experimental = true,
                    FhirVersion = FHIRVersion.N4_0_0,
                    Format = new List<string>() { "xml", "json" },
                    Implementation = new ImplementationComponent()
                    {
                        Url = s_configuration.ResourceBaseUri,
                        Description = entryAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description
                    },
                    Name = "SVC-CORE FHIR",
                    Publisher = entryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company,
                    Title = $"Auto-Generated statement - {entryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product} v{entryAssembly.GetName().Version} ({entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion})",
                    Status = PublicationStatus.Active,
                    Copyright = new Markdown(entryAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright)
                };

                // Generate the rest description
                // TODO: Reflect the current WCF context and get all the types of communication supported
                s_conformance.Rest.Add(CreateRestDefinition());
                s_conformance.Text = null;
            }
            catch (Exception e)
            {
                s_traceSource.TraceEvent(EventLevel.Error, "Error building conformance statement: {0}", e.Message);
                throw;
            }
        }

        /// <summary>
        /// Rest definition creator
        /// </summary>
        private static RestComponent CreateRestDefinition()
        {
            // Security settings
            String security = null;
            //var m_masterConfig = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<RestConfigurationSection>();
            //var authorizationPolicy = m_masterConfig.Services.FirstOrDefault(o => o.Name == "FHIR").Behaviors.Select(o => o.GetCustomAttribute<AuthenticationSchemeDescriptionAttribute>()).FirstOrDefault(o => o != null)?.Scheme;
            if (ApplicationServiceContext.Current.GetService<FhirMessageHandler>().Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth))
            {
                security = "Basic";
            }

            if (ApplicationServiceContext.Current.GetService<FhirMessageHandler>().Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth))
            {
                security = "OAuth";
            }

            var retVal = new RestComponent()
            {
                Mode = RestfulCapabilityMode.Server,
                Documentation = new Markdown("SanteDB REST Instance"),
                Security = new SecurityComponent()
                {
                    Cors = true,
                    Service = security == null ? null : new List<CodeableConcept>() { new CodeableConcept("http://hl7.org/fhir/restful-security-service", security) }
                },
                Resource = FhirResourceHandlerUtil.GetRestDefinition().ToList(),
                Operation = ExtensionUtil.OperationHandlers.Where(o => o.AppliesTo == null).Select(o => new OperationComponent()
                {
                    Name = o.Name,
                    Definition = o.Uri.ToString()
                }).ToList()
            };

            foreach (var itm in retVal.Resource)
            {
                itm.Operation = ExtensionUtil.OperationHandlers.Where(o => o.AppliesTo?.Contains(itm.Type.Value) == true).Select(o => new OperationComponent()
                {
                    Name = o.Name,
                    Definition = o.Uri.ToString()
                }).ToList();
                itm.SupportedProfile = ExtensionUtil.ProfileHandlers.Where(o => o.AppliesTo == null || o.AppliesTo.Contains(itm.Type.Value)).Select(o => o.ProfileUri.ToString()).ToList();
            }
            return retVal;


        }
    }
}

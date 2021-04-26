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
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Messaging.FHIR.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Message processing tool
    /// </summary>
    public static class FhirResourceHandlerUtil
    {

      
        // Message processors
        private static IDictionary<ResourceType, IFhirResourceHandler> s_messageProcessors = new ConcurrentDictionary<ResourceType, IFhirResourceHandler>();

        // Resource handler
        private static Tracer s_tracer = Tracer.GetTracer(typeof(FhirResourceHandlerUtil));

        /// <summary>
        /// FHIR message processing utility
        /// </summary>
        static FhirResourceHandlerUtil()
        {
        }

        /// <summary>
        /// Register resource handler
        /// </summary>
        public static void RegisterResourceHandler(IFhirResourceHandler handler)
        {
            s_messageProcessors.Add(handler.ResourceType, handler);
        }

        /// <summary>
        /// Register resource handler
        /// </summary>
        public static void UnRegisterResourceHandler(IFhirResourceHandler handler)
        {
            s_messageProcessors.Remove(handler.ResourceType);
        }

        /// <summary>
        /// Get the message processor type based on resource name
        /// </summary>
        public static IFhirResourceHandler GetResourceHandler(string resourceType)
        {
            var rtEnum = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(resourceType);
            if (rtEnum.HasValue)
            {
                return GetResourceHandler(rtEnum.Value);
            }
            else
            {
                throw new KeyNotFoundException($"Resource type {resourceType} is invalid");
            }
        }

        /// <summary>
        /// Get the specified resource handler
        /// </summary>
        public static IFhirResourceHandler GetResourceHandler(ResourceType resourceType) {

            if (!s_messageProcessors.TryGetValue(resourceType, out IFhirResourceHandler retVal))
            {
                throw new NotSupportedException($"No handler registered for {resourceType}");
            }
            return retVal;
        }
      
        /// <summary>
        /// Gets the mapper for <paramref name="resourceOrModelType"/>
        /// </summary>
        /// <param name="resourceOrModelType">The FHIR type or CDR type</param>
        /// <returns>The mapper (if present)</returns>
        public static IFhirResourceMapper GetMapperFor(Type resourceOrModelType)
        {
            return s_messageProcessors.Select(o => o.Value).OfType<IFhirResourceMapper>().FirstOrDefault(o => o.CanonicalType == resourceOrModelType || o.ResourceClrType == resourceOrModelType);
        }

        /// <summary>
        /// Get REST definition
        /// </summary>
        public static IEnumerable<CapabilityStatement.ResourceComponent> GetRestDefinition()
        {
            return s_messageProcessors.Values.Select(o => {
                var resourceDef = o.GetResourceDefinition();
                var structureDef = o.GetStructureDefinition();
                return resourceDef;
            });
        }

        /// <summary>
        /// Initialize based on configuration
        /// </summary>
        public static void Initialize(FhirServiceConfigurationSection m_configuration , IServiceManager serviceManager)
        {
            // Configuration 
            if (m_configuration.Resources?.Any() == true)
            {
                foreach (var t in serviceManager.CreateInjectedOfAll<IFhirResourceHandler>())
                {
                    if (m_configuration.Resources.Any(r => r == t.ResourceType.ToString()))
                        FhirResourceHandlerUtil.RegisterResourceHandler(t);
                    else if (t is IDisposable disp)
                        disp.Dispose();
                }
            }
            else
            {
                // Old configuration
                foreach (Type t in m_configuration.ResourceHandlers.Select(o => o.Type))
                {
                    if (t != null)
                        FhirResourceHandlerUtil.RegisterResourceHandler(serviceManager.CreateInjected(t) as IFhirResourceHandler);
                }
            }
        }

        /// <summary>
        /// Get all resource handlers
        /// </summary>
        public static IEnumerable<IFhirResourceHandler> ResourceHandlers
        {
            get
            {
                return s_messageProcessors.Values;
            }
        }
    }
}

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
            s_messageProcessors.Add(ResourceType.StructureDefinition, new StructureDefinitionHandler());
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

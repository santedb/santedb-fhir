/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
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
        private static readonly ConcurrentDictionary<ResourceType, IFhirResourceHandler> s_messageProcessors = new ConcurrentDictionary<ResourceType, IFhirResourceHandler>();

        // Resource handler
        private static readonly Tracer s_tracer = Tracer.GetTracer(typeof(FhirResourceHandlerUtil));

        // Localization Service
        private static readonly ILocalizationService s_localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();

        /// <summary>
        /// FHIR message processing utility
        /// </summary>
        static FhirResourceHandlerUtil()
        {
        }

        /// <summary>
        /// Get all resource handlers.
        /// </summary>
        public static IEnumerable<IFhirResourceHandler> ResourceHandlers => s_messageProcessors.Values;

        /// <summary>
        /// Get mapper for the specified object
        /// </summary>
        public static IFhirResourceMapper GetMapperForInstance(object instance)
        {
            return GetMappersFor(instance.GetType()).FirstOrDefault(o => o.CanMapObject(instance));
        }

        /// <summary>
        /// Gets the mapper for <paramref name="resourceOrModelType"/>
        /// </summary>
        /// <param name="resourceOrModelType">The FHIR type or CDR type</param>
        /// <returns>The mapper (if present)</returns>
        public static IEnumerable<IFhirResourceMapper> GetMappersFor(Type resourceOrModelType)
        {
            return s_messageProcessors.Select(o => o.Value).OfType<IFhirResourceMapper>().Where(o => o.CanonicalType == resourceOrModelType || o.ResourceClrType == resourceOrModelType);
        }

        /// <summary>
        /// Gets the mapper for <paramref name="resourceType"/>
        /// </summary>
        /// <returns>The mapper (if present)</returns>
        public static IEnumerable<IFhirResourceMapper> GetMappersFor(ResourceType resourceType)
        {
            return s_messageProcessors.Select(o => o.Value).OfType<IFhirResourceMapper>().Where(o => o.ResourceType == resourceType);
        }

        /// <summary>
        /// Get the message processor type based on resource name
        /// </summary>
        public static IFhirResourceHandler GetResourceHandler(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType))
            {
                throw new ArgumentNullException(nameof(resourceType));
            }

            var rtEnum = EnumUtility.ParseLiteral<ResourceType>(resourceType);

            if (rtEnum.HasValue)
            {
                return GetResourceHandler(rtEnum.Value);
            }
            else
            {
                s_tracer.TraceError($"Resource type {resourceType} is invalid");
                throw new KeyNotFoundException(s_localizationService.GetString("error.messaging.fhir.handlers.invalidResourceType", new { param = resourceType }));
            }
        }

        /// <summary>
        /// Get the specified resource handler
        /// </summary>
        public static IFhirResourceHandler GetResourceHandler(ResourceType resourceType)
        {
            if (!s_messageProcessors.TryGetValue(resourceType, out var retVal))
            {
                s_tracer.TraceError($"No handler registered for {resourceType}");
                throw new NotSupportedException(s_localizationService.GetString("error.messaging.fhir.handlers.noRegisteredHandler", new { param = resourceType }));
            }

            return retVal;
        }

        /// <summary>
        /// Get REST definition
        /// </summary>
        public static IEnumerable<CapabilityStatement.ResourceComponent> GetRestDefinition()
        {
            return s_messageProcessors.Values.Select(o =>
            {
                var resourceDef = o.GetResourceDefinition();
                var structureDef = o.GetStructureDefinition();
                return resourceDef;
            });
        }

        /// <summary>
        /// Initialize based on configuration
        /// </summary>
        public static void Initialize(FhirServiceConfigurationSection configuration, IServiceManager serviceManager)
        {
            // Configuration 
            if (configuration.Resources?.Any() == true)
            {
                foreach (var t in serviceManager.CreateInjectedOfAll<IFhirResourceHandler>())
                {
                    if (configuration.Resources.Any(r => r == t.ResourceType.ToString()))
                    {
                        RegisterResourceHandler(t);
                    }
                    else if (t is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                }
            }
            else if (configuration.ResourceHandlers?.Any() == true)
            {
                // Old configuration
                foreach (var t in configuration.ResourceHandlers.Select(o => o.Type).Where(c => c != null))
                {
                    var rh = serviceManager.CreateInjected(t);

                    if (rh is IFhirResourceHandler resourceHandler)
                    {
                        RegisterResourceHandler(resourceHandler);
                    }
                }
            }
            else
            {
                AppDomain.CurrentDomain.GetAllTypes().Where(t => typeof(IFhirResourceHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).ToList().ForEach(o =>
                {
                    try
                    {
                        RegisterResourceHandler(serviceManager.CreateInjected(o) as IFhirResourceHandler);
                    }
                    catch
                    {

                    }
                });
            }
        }

        /// <summary>
        /// Register resource handler
        /// </summary>
        public static void RegisterResourceHandler(IFhirResourceHandler handler)
        {
            if (handler == null)
            {
                s_tracer.TraceError("Handler is required");
                throw new ArgumentNullException(nameof(handler), s_localizationService.GetString("error.messaging.fhir.handlers.handlerRequired"));
            }

            s_messageProcessors.TryAdd(handler.ResourceType, handler);
        }

        /// <summary>
        /// Register resource handler
        /// </summary>
        public static void UnRegisterResourceHandler(IFhirResourceHandler handler)
        {
            s_messageProcessors.TryRemove(handler.ResourceType, out _);
        }
    }
}
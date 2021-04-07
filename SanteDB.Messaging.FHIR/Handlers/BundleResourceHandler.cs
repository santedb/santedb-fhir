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
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Reflection;
using SanteDB.Core.Diagnostics;
using RestSrvr;
using static Hl7.Fhir.Model.CapabilityStatement;
using SanteDB.Core.Services;
using System.Linq.Expressions;
using Hl7.Fhir.Model;
using System.Collections.Specialized;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a FHIR resource handler for bundles
    /// </summary>
    public class BundleResourceHandler : IFhirResourceHandler
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(BundleResourceHandler));

        // Bundle repository
        private IRepositoryService<Core.Model.Collection.Bundle> m_bundleRepository;

        /// <summary>
        /// Creates a new bundle resource handler
        /// </summary>
        public BundleResourceHandler(IRepositoryService<Core.Model.Collection.Bundle> bundleRepository)
        {
            this.m_bundleRepository = bundleRepository;
        }

        /// <summary>
        /// The type that this resource handler operates on
        /// </summary>
        public ResourceType ResourceType => ResourceType.Bundle;

        /// <summary>
        /// Create the specified object
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
        {
            if (!(target is Hl7.Fhir.Model.Bundle fhirBundle))
                throw new ArgumentOutOfRangeException("Expected a FHIR bundle");

            switch(fhirBundle.Type.GetValueOrDefault())
            {
                case Hl7.Fhir.Model.Bundle.BundleType.Batch:
                    {
                        var sdbBundle = new Core.Model.Collection.Bundle();
                        foreach (var entry in fhirBundle.Entry)
                        {
                            var entryType = entry.Resource.ResourceType;
                            var handler = FhirResourceHandlerUtil.GetResourceHandler(entryType) as IBundleResourceHandler;
                            if (handler == null)
                            {
                                throw new NotSupportedException($"This repository cannot properly work with {entryType}");
                            }
                            sdbBundle.Add(handler.MapToModel(entry.Resource, RestOperationContext.Current, fhirBundle));
                        }
                        sdbBundle.Item.RemoveAll(o => o == null);

                        var sdbResult = this.m_bundleRepository.Insert(sdbBundle);
                        var retVal = new Hl7.Fhir.Model.Bundle()
                        {
                            Type = Hl7.Fhir.Model.Bundle.BundleType.BatchResponse
                        };
                        // Parse return value
                        foreach(var entry in sdbResult.Item)
                        {
                            var handler = FhirResourceHandlerUtil.GetMapperFor(entry.GetType());
                            if (handler == null) continue; // TODO: Warn
                            retVal.Entry.Add(new Hl7.Fhir.Model.Bundle.EntryComponent()
                            {
                                Resource = handler.MapToFhir(entry)
                            });
                        }
                        return retVal;
                    };
                case Hl7.Fhir.Model.Bundle.BundleType.Message:
                    {

                        var processMessageHandler = ExtensionUtil.GetOperation(null, "process-message");
                        return processMessageHandler.Invoke(new Parameters()
                        {
                            Parameter = new List<Parameters.ParameterComponent>()
                            {
                                new Parameters.ParameterComponent()
                                {
                                    Name = "content",
                                    Resource = fhirBundle
                                },
                                new Parameters.ParameterComponent()
                                {
                                    Name = "async",
                                    Value = new FhirBoolean(false)
                                }
                            }
                        });
                    }
                default:
                    throw new NotSupportedException($"Processing of bundles with type {fhirBundle.Type} is not supported");
            }
        }

        /// <summary>
        /// Delete the specified object
        /// </summary>
        /// <remarks>Not supported on this interface</remarks>
        public Resource Delete(string id, TransactionMode mode)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Get the resource definition
        /// </summary>
        /// <returns></returns>
        public ResourceComponent GetResourceDefinition()
        {
            return new ResourceComponent()
            {
                ConditionalCreate = false,
                ConditionalDelete = ConditionalDeleteStatus.NotSupported,
                ConditionalRead = ConditionalReadStatus.NotSupported,
                ConditionalUpdate = false,
                Type = ResourceType.Bundle,
                Interaction = new List<ResourceInteractionComponent>()
                {
                    new ResourceInteractionComponent()
                    {
                        Code = TypeRestfulInteraction.Create
                    }
                },
                UpdateCreate = false
            };
        }

        /// <summary>
        /// Get the structure definition
        /// </summary>
        /// <returns></returns>
        public StructureDefinition GetStructureDefinition()
        {
            return StructureDefinitionUtil.GetStructureDefinition(typeof(Hl7.Fhir.Model.Bundle), false);
        }

        /// <summary>
        /// History read
        /// </summary>
        public FhirQueryResult History(string id)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Query 
        /// </summary>
        public FhirQueryResult Query(NameValueCollection parameters)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Read operation
        /// </summary>
        public Resource Read(string id, string versionId)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Update 
        /// </summary>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            throw new NotSupportedException();
        }
    }
}

﻿/*
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
using SanteDB.Core.Model.Interfaces;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a FHIR resource handler for bundles
    /// </summary>
    public class BundleResourceHandler : IFhirResourceHandler, IFhirResourceMapper
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
        /// Get the canonical type
        /// </summary>
        public Type CanonicalType => typeof(SanteDB.Core.Model.Collection.Bundle);

        /// <summary>
        /// Get the CLR type
        /// </summary>
        public Type ResourceClrType => typeof(Hl7.Fhir.Model.Bundle);

        /// <summary>
        /// Create the specified object
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
        {
            if (!(target is Hl7.Fhir.Model.Bundle fhirBundle))
                throw new ArgumentOutOfRangeException("Expected a FHIR bundle");

            switch(fhirBundle.Type.GetValueOrDefault())
            {
                case Hl7.Fhir.Model.Bundle.BundleType.Transaction:
                    {
                        

                        var sdbResult = this.m_bundleRepository.Insert(this.MapToModel(target) as Core.Model.Collection.Bundle);
                        var retVal = this.MapToFhir(sdbResult) as Hl7.Fhir.Model.Bundle;
                        retVal.Type = Hl7.Fhir.Model.Bundle.BundleType.TransactionResponse;

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
        /// Map the specified <paramref name="modelInstance"/> to a bundle
        /// </summary>
        public Resource MapToFhir(IdentifiedData modelInstance)
        {
            if(!(modelInstance is Core.Model.Collection.Bundle sdbBundle))
            {
                throw new ArgumentException(nameof(modelInstance), "Argument must be a bundle");
            }

            var retVal = new Hl7.Fhir.Model.Bundle()
            {
                Type = Hl7.Fhir.Model.Bundle.BundleType.TransactionResponse,
                Meta = new Meta()
                {
                    LastUpdated = DateTimeOffset.Now
                }
            };
            // Parse return value
            foreach (var entry in sdbBundle.Item)
            {
                var handler = FhirResourceHandlerUtil.GetMapperFor(entry.GetType());
                if (handler == null) continue; // TODO: Warn
                retVal.Entry.Add(new Hl7.Fhir.Model.Bundle.EntryComponent()
                {
                    Resource = handler.MapToFhir(entry)
                });
            }
            return retVal;
        }

        /// <summary>
        /// Map the bundle to a model transaction
        /// </summary>
        public IdentifiedData MapToModel(Resource resourceInstance)
        {
            // Resource instance validation and convert
            if(!(resourceInstance is Hl7.Fhir.Model.Bundle fhirBundle))
            {
                throw new ArgumentException(nameof(resourceInstance), "Instance must be a bundle");
            }

            var sdbBundle = new Core.Model.Collection.Bundle();
            foreach (var entry in fhirBundle.Entry)
            {
                var entryType = entry.Resource.ResourceType;
                var handler = FhirResourceHandlerUtil.GetResourceHandler(entryType) as IFhirResourceMapper;

                // Allow this entry to know its context in the bundle
                entry.Resource.AddAnnotation(fhirBundle);
                entry.Resource.AddAnnotation(sdbBundle);

                // Map and add to bundle
                var itm = handler.MapToModel(entry.Resource);
                sdbBundle.Add(itm);

                // Add original URLs so that subsequent bundle entries (which might reference this entry) can resolve
                if (itm is ITaggable taggable)
                {
                    taggable.AddTag(FhirConstants.OriginalUrlTag, entry.FullUrl);
                    taggable.AddTag(FhirConstants.OriginalIdTag, entry.Resource.Id);
                }

            }
            sdbBundle.Item.RemoveAll(o => o == null);
            return sdbBundle;
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

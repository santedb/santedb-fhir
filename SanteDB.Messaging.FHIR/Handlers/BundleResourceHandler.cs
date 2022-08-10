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
 * Date: 2021-10-29
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
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
using System.Collections.Specialized;
using SanteDB.Core.i18n;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a FHIR resource handler for bundles
    /// </summary>
    public class BundleResourceHandler : IServiceImplementation, IFhirResourceHandler, IFhirResourceMapper
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(BundleResourceHandler));

        //Localization service
        private ILocalizationService m_localizationService;

        // Bundle repository
        private IRepositoryService<Core.Model.Collection.Bundle> m_bundleRepository;

        /// <summary>
        /// Creates a new bundle resource handler
        /// </summary>
        public BundleResourceHandler(IRepositoryService<Core.Model.Collection.Bundle> bundleRepository, ILocalizationService localizationService)
        {
            this.m_bundleRepository = bundleRepository;
            this.m_localizationService = localizationService;
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
        /// Get service name
        /// </summary>
        public string ServiceName => "Bundle Resource Handler";

        /// <summary>
        /// Can map an object
        /// </summary>
        public bool CanMapObject(object instance) => instance is Hl7.Fhir.Model.Bundle || instance is Core.Model.Collection.Bundle;

        /// <summary>
        /// Create the specified object
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
        {
            if (!(target is Hl7.Fhir.Model.Bundle fhirBundle))
            {
                throw new ArgumentOutOfRangeException(this.m_localizationService.GetString("error.messaging.fhir.bundle.fhirBundle"));
            }

            switch (fhirBundle.Type.GetValueOrDefault())
            {
                case Hl7.Fhir.Model.Bundle.BundleType.Transaction:
                    {
                        var sdbResult = this.m_bundleRepository.Insert(this.MapToModel(target) as Core.Model.Collection.Bundle);
                        var retVal = this.MapToFhir(sdbResult) as Hl7.Fhir.Model.Bundle;
                        retVal.Type = Hl7.Fhir.Model.Bundle.BundleType.TransactionResponse;

                        return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Create, ResourceType.Bundle, retVal);
                    };
                case Hl7.Fhir.Model.Bundle.BundleType.Message:
                    {
                        var processMessageHandler = ExtensionUtil.GetOperation(null, "process-message");
                        return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Create, ResourceType.Bundle, processMessageHandler.Invoke(new Parameters()
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
                        }));
                    }
                default:
                    this.m_tracer.TraceError($"Processing of bundles with type {fhirBundle.Type} is not supported");
                    throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
            }
        }

        /// <summary>
        /// Delete the specified object
        /// </summary>
        /// <remarks>Not supported on this interface</remarks>
        public Resource Delete(string id, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
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
            return typeof(Bundle).GetStructureDefinition(false);
        }

        /// <summary>
        /// History read
        /// </summary>
        public Hl7.Fhir.Model.Bundle History(string id)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Map the specified <paramref name="modelInstance"/> to a bundle
        /// </summary>
        public Resource MapToFhir(IdentifiedData modelInstance)
        {
            if (!(modelInstance is Core.Model.Collection.Bundle sdbBundle))
            {
                this.m_tracer.TraceError("Instance must be a bundle");
                throw new ArgumentException(nameof(modelInstance), this.m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "bundle"
                }));
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
                var handler = FhirResourceHandlerUtil.GetMapperForInstance(entry);
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
            if (!(resourceInstance is Hl7.Fhir.Model.Bundle fhirBundle))
            {
                this.m_tracer.TraceError("Argument must be a bundle");
                throw new ArgumentException(this.m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "bundle"
                }));
            }

            var sdbBundle = new Core.Model.Collection.Bundle();
            foreach (var entry in fhirBundle.Entry)
            {
                if (!entry.Resource.TryDeriveResourceType(out ResourceType entryType))
                {
                    continue;
                }
                var handler = FhirResourceHandlerUtil.GetResourceHandler(entryType) as IFhirResourceMapper;

                // Allow this entry to know its context in the bundle
                entry.Resource.AddAnnotation(fhirBundle);
                entry.Resource.AddAnnotation(sdbBundle);

                // Map and add to bundle
                var itm = handler.MapToModel(entry.Resource);
                sdbBundle.Remove(itm.Key.GetValueOrDefault());
                sdbBundle.Add(itm);

                // HACK: If the ITM is a relationship or participation insert it into the bundle
                if (itm is ITargetedAssociation targetedAssociation && targetedAssociation.TargetEntity != null)
                {
                    sdbBundle.Insert(sdbBundle.Item.Count - 1, targetedAssociation.TargetEntity as IdentifiedData);
                    itm = targetedAssociation.TargetEntity as IdentifiedData;
                }

                // Add original URLs so that subsequent bundle entries (which might reference this entry) can resolve
                if (itm is ITaggable taggable)
                {
                    taggable.AddTag(FhirConstants.OriginalUrlTag, entry.FullUrl);
                    taggable.AddTag(FhirConstants.OriginalIdTag, entry.Resource.Id);
                }

                if (entry.Request != null)
                {
                    sdbBundle.FocalObjects.Add(itm.Key.Value);
                }
            }

            sdbBundle.Item.RemoveAll(o => o == null || o is ITaggable taggable && taggable.GetTag(FhirConstants.PlaceholderTag) == "true");
            return sdbBundle;
        }

        /// <summary>
        /// Query
        /// </summary>
        public Hl7.Fhir.Model.Bundle Query(NameValueCollection parameters)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Read operation
        /// </summary>
        public Resource Read(string id, string versionId)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Update
        /// </summary>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }
    }
}
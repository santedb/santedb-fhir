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
using DocumentFormat.OpenXml.EMMA;
using Hl7.Fhir.Model;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents the default StructureDefinition handler
    /// </summary>
    [ResourceSensitivity(ResourceSensitivityClassification.Metadata)]
    public class StructureDefinitionHandler : IFhirResourceHandler, IServiceImplementation
    {
        // Localization service
        private ILocalizationService m_localizationService;

        // Structure definitions generated once
        private IQueryResultSet m_structureDefinitions;

        /// <summary>
        /// Gets the resource name
        /// </summary>
        public ResourceType ResourceType => ResourceType.StructureDefinition;

        /// <summary>
        /// Get service name
        /// </summary>
        public string ServiceName => "Structure Definition Handler";

        /// <summary>
        /// Initializes the structure definition handler
        /// </summary>
        /// <param name="localizationService"></param>
        public StructureDefinitionHandler(ILocalizationService localizationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Create the specified definition
        /// </summary>
        public Resource Create(Resource target, Core.Services.TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Delete
        /// </summary>
        public Resource Delete(string id, Core.Services.TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Get the resource definition
        /// </summary>
        public ResourceComponent GetResourceDefinition()
        {
            return new ResourceComponent()
            {
                ConditionalCreate = false,
                ConditionalDelete = ConditionalDeleteStatus.NotSupported,
                ConditionalUpdate = false,
                Interaction = new List<ResourceInteractionComponent>()
                {
                    new ResourceInteractionComponent()
                    {
                        Code = TypeRestfulInteraction.Read
                    },
                    new ResourceInteractionComponent()
                    {
                        Code = TypeRestfulInteraction.SearchType
                    }
                },
                Type = Hl7.Fhir.Model.ResourceType.StructureDefinition,
                ReadHistory = true,
                UpdateCreate = false,
                Versioning = ResourceVersionPolicy.Versioned,
                SearchParam = new List<SearchParamComponent>()
                {
                    new SearchParamComponent()
                    {
                        Name = "_offset",
                        Type = SearchParamType.Number
                    },
                    new SearchParamComponent()
                    {
                        Name = "_count",
                        Type = SearchParamType.Number
                    },
                    new SearchParamComponent()
                    {
                        Name = "_page", 
                        Type = SearchParamType.Number
                    }
                }
            };
        }

        /// <summary>
        /// Get structure definition
        /// </summary>
        public StructureDefinition GetStructureDefinition()
        {
            return typeof(StructureDefinition).GetStructureDefinition(false);
        }

        /// <summary>
        /// Not supported
        /// </summary>
        public Bundle History(string id)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Query for the specified search structure definition
        /// </summary>
        public Bundle Query(NameValueCollection parameters)
        {
            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(StructureDefinition), typeof(ServiceOptions), parameters, out var hdsiQuery);
            var results = this.GetAllStructures();

            // TODO: Filtering
            results = query.ApplyCommonQueryControls(results, out var totalResults);

            return MessageUtil.CreateBundle(new FhirQueryResult(this.ResourceType.ToString())
            {
                Results = results.OfType<StructureDefinition>().ToArray().AsParallel().Select(o =>
                {
                    return new Bundle.EntryComponent()
                    {
                        Resource = o,
                        Search = new Bundle.SearchComponent()
                        {
                            Mode = Bundle.SearchEntryMode.Match
                        }
                    };
                }).ToList(),
                Query = query,
                TotalResults = totalResults
            }, Bundle.BundleType.Searchset);

        }

        private IQueryResultSet GetAllStructures()
        {
            if (this.m_structureDefinitions == null)
            {
                var resourceStructures = FhirResourceHandlerUtil.ResourceHandlers.Select(o => o.GetStructureDefinition());
                var extensionStructures = ExtensionUtil.ExtensionHandlers.OfType<IFhirExtensionHandlerEx>().Select(o => StructureDefinitionUtil.GetStructureDefinition(o));
                this.m_structureDefinitions = resourceStructures.Concat(extensionStructures).AsResultSet();
            }
            return this.m_structureDefinitions;
        }

        /// <summary>
        /// Read the specified structure definition
        /// </summary>
        public Resource Read(string id, string versionId)
        {
            return this.GetAllStructures().OfType<StructureDefinition>().FirstOrDefault(o => o.Id == id) ??
                throw new KeyNotFoundException(id);
        }

        /// <summary>
        /// Update
        /// </summary>
        public Resource Update(string id, Resource target, Core.Services.TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }
    }
}

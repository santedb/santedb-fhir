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
using SanteDB.Core;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a resource handler that can handle substances
    /// </summary>
    public class SubstanceResourceHandler : RepositoryResourceHandlerBase<Substance, Material>
    {
        /// <summary>
        /// Create new resource handler
        /// </summary>
        public SubstanceResourceHandler(IRepositoryService<Material> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {
        }

        protected override IEnumerable<Resource> GetIncludes(Material resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException.userMessage"));
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new[]
            {
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Delete
            }.Select(o => new ResourceInteractionComponent
            { Code = o });
        }

        protected override IEnumerable<Resource> GetReverseIncludes(Material resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException.userMessage"));
        }

        /// <summary>
        /// Map the substance to FHIR
        /// </summary>
        protected override Substance MapToFhir(Material model)
        {
            var retVal = DataTypeConverter.CreateResource<Substance>(model);

            // Identifiers
            retVal.Identifier = model.Identifiers.Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

            // sTatus
            switch (model.StatusConceptKey.ToString().ToUpper())
            {
                case StatusKeyStrings.New:
                case StatusKeyStrings.Active:
                    retVal.Status = Substance.FHIRSubstanceStatus.Active;
                    break;
                case StatusKeyStrings.Nullified:
                    retVal.Status = Substance.FHIRSubstanceStatus.EnteredInError;
                    break;
                case StatusKeyStrings.Obsolete:
                    retVal.Status = Substance.FHIRSubstanceStatus.Inactive;
                    break;
            }

            // Category and code
            retVal.Category = new List<CodeableConcept>
            {
                DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey, "http://terminology.hl7.org/CodeSystem/substance-category")
            };

            retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey, "http://snomed.info/sct");
            retVal.Description = model.LoadCollection<EntityName>("Names").FirstOrDefault(o => o.NameUseKey == NameUseKeys.OfficialRecord)?.LoadCollection<EntityNameComponent>("Components")?.FirstOrDefault()?.Value;

            // TODO: Instance or kind
            if (model.DeterminerConceptKey == DeterminerKeys.Described)
            {
                retVal.Instance = model.GetRelationships().Where(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Instance).Select(s => s.LoadProperty<Material>(nameof(EntityRelationship.TargetEntity))).Select(m => new Substance.InstanceComponent
                {
                    ExpiryElement = DataTypeConverter.ToFhirDateTime(model.ExpiryDate),
                    Identifier = DataTypeConverter.ToFhirIdentifier(m.GetIdentifiers().FirstOrDefault()),
                    Quantity = DataTypeConverter.ToQuantity(m.Quantity, m.QuantityConceptKey)
                }).ToList();
            }
            else if (model.DeterminerConceptKey == DeterminerKeys.Specific)
            {
                var conceptRepo = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();

                retVal.Instance = new List<Substance.InstanceComponent>
                {
                    new Substance.InstanceComponent
                    {
                        ExpiryElement = DataTypeConverter.ToFhirDateTime(model.ExpiryDate),
                        Quantity = DataTypeConverter.ToQuantity(model.Quantity, model.QuantityConceptKey)
                    }
                };
            }

            return retVal;
        }

        /// <summary>
        /// Maps a FHIR based resource to a model based resource
        /// </summary>
        /// <param name="resource">The resource to be mapped</param>
        /// <returns>The mapped material</returns>
        protected override Material MapToModel(Substance resource)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException.userMessage"));
        }
    }
}
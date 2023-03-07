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
using Hl7.Fhir.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;
using Organization = Hl7.Fhir.Model.Organization;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a medication resource handler
    /// </summary>
    public class MedicationResourceHandler : RepositoryResourceHandlerBase<Medication, ManufacturedMaterial>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MedicationResourceHandler"/> class.
        /// </summary>
        /// <param name="repositoryService">The repository service.</param>
        /// <param name="localizationService">The localization service.</param>
        public MedicationResourceHandler(IRepositoryService<ManufacturedMaterial> repositoryService, ILocalizationService localizationService) : base(repositoryService, localizationService)
        {
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(ManufacturedMaterial resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new[]
            {
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Update,
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Delete
            }.Select(o => new ResourceInteractionComponent { Code = o });
        }

        /// <summary>
        /// Get reverse included resources
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(ManufacturedMaterial resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Maps a <see cref="ManufacturedMaterial"/> instance to a FHIR <see cref="Medication"/> instance.
        /// </summary>
        /// <param name="model">The instance to map.</param>
        /// <returns>Returns the mapped instance.</returns>
        protected override Medication MapToFhir(ManufacturedMaterial model)
        {
            var retVal = DataTypeConverter.CreateResource<Medication>(model);

            // Code of medication code
            retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey, "http://snomed.info/sct");
            retVal.Identifier = model.LoadProperty(o => o.Identifiers).Select(DataTypeConverter.ToFhirIdentifier).ToList();

            switch (model.StatusConceptKey.ToString().ToUpper())
            {
                case StatusKeyStrings.Active:
                case StatusKeyStrings.New:
                    retVal.Status = Medication.MedicationStatusCodes.Active;
                    break;
                case StatusKeyStrings.Obsolete:
                    retVal.Status = Medication.MedicationStatusCodes.Inactive;
                    break;
                case StatusKeyStrings.Nullified:
                    retVal.Status = Medication.MedicationStatusCodes.EnteredInError;
                    break;
            }

            // Is brand?
            var manufacturer = model.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.ManufacturedProduct);

            if (manufacturer != null)
            {
                retVal.Manufacturer = DataTypeConverter.CreateVersionedReference<Organization>(manufacturer.LoadProperty<Entity>(nameof(EntityRelationship.TargetEntity)));
            }

            // Form
            retVal.Form = DataTypeConverter.ToFhirCodeableConcept(model.FormConceptKey, "http://hl7.org/fhir/ValueSet/medication-form-codes");
            retVal.Batch = new Medication.BatchComponent
            {
                LotNumber = model.LotNumber,
                ExpirationDateElement = DataTypeConverter.ToFhirDateTime(model.ExpiryDate)
            };

            return retVal;
        }

        /// <summary>
        /// Maps a FHIR <see cref="Medication"/> to a <see cref="ManufacturedMaterial"/> instance.
        /// </summary>
        /// <param name="resource">The model resource to be mapped</param>
        /// <returns>Returns the mapped <see cref="ManufacturedMaterial"/> instance.</returns>
        protected override ManufacturedMaterial MapToModel(Medication resource)
        {
            ManufacturedMaterial manufacturedMaterial;

            if (Guid.TryParse(resource.Id, out var key))
            {
                manufacturedMaterial = this.m_repository.Get(key) ?? new ManufacturedMaterial
                {
                    Key = key
                };
            }
            else
            {
                manufacturedMaterial = new ManufacturedMaterial
                {
                    Key = Guid.NewGuid()
                };
            }

            manufacturedMaterial.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
            manufacturedMaterial.TypeConcept = DataTypeConverter.ToConcept(resource.Code?.Coding?.FirstOrDefault(), "http://snomed.info/sct");

            switch (resource.Status)
            {
                case Medication.MedicationStatusCodes.Active:
                    manufacturedMaterial.StatusConceptKey = StatusKeys.Active;
                    break;

                case Medication.MedicationStatusCodes.Inactive:
                    manufacturedMaterial.StatusConceptKey = StatusKeys.Obsolete;
                    break;

                case Medication.MedicationStatusCodes.EnteredInError:
                    manufacturedMaterial.StatusConceptKey = StatusKeys.Nullified;
                    break;
            }

            manufacturedMaterial.LotNumber = resource.Batch?.LotNumber;
            manufacturedMaterial.ExpiryDate = DataTypeConverter.ToDateTimeOffset(resource.Batch?.ExpirationDateElement)?.DateTime;

            if (resource.Manufacturer != null)
            {
                manufacturedMaterial.LoadProperty(o => o.Relationships).Add(new EntityRelationship(EntityRelationshipTypeKeys.ManufacturedProduct, DataTypeConverter.ResolveEntity<Core.Model.Entities.Organization>(resource.Manufacturer, resource)));
            }

            return manufacturedMaterial;
        }
    }
}
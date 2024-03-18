/*
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
 * User: fyfej
 * Date: 2023-6-21
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Resource handler for immunization classes.
    /// </summary>
    public class ImmunizationResourceHandler : RepositoryResourceHandlerBase<Immunization, SubstanceAdministration>
    {
        private readonly Guid INITIAL_IMMUNIZATION = Guid.Parse("f3be6b88-bc8f-4263-a779-86f21ea10a47");
        private readonly Guid IMMUNIZATION = Guid.Parse("6e7a3521-2967-4c0a-80ec-6c5c197b2178");
        private readonly Guid BOOSTER_IMMUNIZATION = Guid.Parse("0331e13f-f471-4fbd-92dc-66e0a46239d5");
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ImmunizationResourceHandler));
        private readonly IRepositoryService<Material> m_materialRepository;
        private readonly IRepositoryService<ManufacturedMaterial> m_manufacturedMaterialRepository;

        /// <summary>
        /// Create a new resource handler
        /// </summary>
        public ImmunizationResourceHandler(IRepositoryService<SubstanceAdministration> repo, ILocalizationService localizationService, IRepositoryService<Material> materialService, IRepositoryService<ManufacturedMaterial> manufactedMaterialService) : base(repo, localizationService)
        {
            this.m_materialRepository = materialService;
            this.m_manufacturedMaterialRepository = manufactedMaterialService;
        }

        /// <summary>
        /// Can map object
        /// </summary>
        public override bool CanMapObject(object instance) => instance is Immunization ||
            instance is SubstanceAdministration sbadm && (sbadm.TypeConceptKey == BOOSTER_IMMUNIZATION || sbadm.TypeConceptKey == INITIAL_IMMUNIZATION || sbadm.TypeConceptKey == IMMUNIZATION);

        /// <summary>
        /// Maps the substance administration to FHIR.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>Returns the mapped FHIR resource.</returns>
        protected override Immunization MapToFhir(SubstanceAdministration model)
        {
            var retVal = DataTypeConverter.CreateResource<Immunization>(model);

            retVal.DoseQuantity = new Quantity()
            {
                Unit = DataTypeConverter.ToFhirCodeableConcept(model.DoseUnitKey, FhirConstants.DefaultQuantityUnitSystem)?.GetCoding().Code,
                Value = model.DoseQuantity
            };
            retVal.RecordedElement = new FhirDateTime(model.ActTime.Value); // TODO: This is probably not the best place to put this?
            retVal.Route = DataTypeConverter.ToFhirCodeableConcept(model.RouteKey);
            retVal.Site = DataTypeConverter.ToFhirCodeableConcept(model.SiteKey);
            retVal.StatusReason = DataTypeConverter.ToFhirCodeableConcept(model.ReasonConceptKey);
            switch (model.StatusConceptKey?.ToString().ToUpper())
            {
                case StatusKeyStrings.Completed:
                    if (model.IsNegated)
                    {
                        retVal.Status = Immunization.ImmunizationStatusCodes.NotDone;
                    }
                    else
                    {
                        retVal.Status = Immunization.ImmunizationStatusCodes.Completed;
                    }

                    break;

                case StatusKeyStrings.Nullified:
                    retVal.Status = Immunization.ImmunizationStatusCodes.EnteredInError;
                    break;
            }

            // Material
            var matPtcpt = model.LoadProperty(o => o.Participations).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Consumable) ??
                model.LoadProperty(o => o.Participations).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Product);
            if (matPtcpt != null)
            {
                var matl = matPtcpt.LoadProperty<Material>(nameof(ActParticipation.PlayerEntity));
                retVal.VaccineCode = DataTypeConverter.ToFhirCodeableConcept(matl.TypeConceptKey);
                retVal.ExpirationDateElement = matl.ExpiryDate.HasValue ? DataTypeConverter.ToFhirDate(matl.ExpiryDate) : null;
                retVal.LotNumber = (matl as ManufacturedMaterial)?.LotNumber;
            }
            else
            {
                retVal.ExpirationDate = null;
            }

            // RCT
            var rct = model.Participations.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);
            if (rct != null)
            {
                retVal.Patient = DataTypeConverter.CreateVersionedReference<Patient>(rct.LoadProperty<Entity>("PlayerEntity"));
            }

            // Performer
            retVal.Performer.AddRange(model.Participations.Where(c => c.ParticipationRoleKey == ActParticipationKeys.Performer).Select(c => new Immunization.PerformerComponent
            {
                Actor = DataTypeConverter.CreateVersionedReference<Practitioner>(c.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)))
            }));

            // Protocol
            foreach (var itm in model.LoadProperty(o => o.Protocols))
            {
                Immunization.ProtocolAppliedComponent protocol = new Immunization.ProtocolAppliedComponent();
                var dbProtocol = itm.LoadProperty<Protocol>(nameof(ActProtocol.Protocol));
                protocol.DoseNumber = new Integer(model.SequenceId);

                // Protocol lookup
                protocol.Series = dbProtocol?.Name;
                retVal.ProtocolApplied.Add(protocol);
            }

            return retVal;
        }

        /// <summary>
        /// Map an immunization FHIR resource to a substance administration.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <returns>Returns the mapped model.</returns>
        protected override SubstanceAdministration MapToModel(Immunization resource)
        {
            var substanceAdministration = new SubstanceAdministration
            {
                ActTime = DataTypeConverter.ToDateTimeOffset(resource.RecordedElement).GetValueOrDefault(),
                Notes = DataTypeConverter.ToNote<ActNote>(resource.Text),
                DoseQuantity = resource.DoseQuantity?.Value ?? 0,
                DoseUnit = resource.DoseQuantity != null ? DataTypeConverter.ToConcept<String>(resource.DoseQuantity.Unit, string.IsNullOrWhiteSpace(resource.DoseQuantity.System) ? FhirConstants.DefaultQuantityUnitSystem : resource.DoseQuantity.System) : null,
                Extensions = resource.Extension?.Select(DataTypeConverter.ToActExtension).ToList(),
                Identifiers = resource.Identifier?.Select(DataTypeConverter.ToActIdentifier).ToList(),
                Key = Guid.NewGuid(),
                MoodConceptKey = ActMoodKeys.Eventoccurrence,
                StatusConceptKey = resource.Status == Immunization.ImmunizationStatusCodes.Completed ? StatusKeys.Completed : resource.Status == Immunization.ImmunizationStatusCodes.EnteredInError ? StatusKeys.Nullified : StatusKeys.Completed,
                IsNegated = resource.Status == Immunization.ImmunizationStatusCodes.NotDone,
                RouteKey = DataTypeConverter.ToConcept(resource.Route)?.Key,
                SiteKey = DataTypeConverter.ToConcept(resource.Site)?.Key,
                Participations = new List<ActParticipation>(),
                Relationships = new List<ActRelationship>()
            };

            Guid key;
            if (Guid.TryParse(resource.Id, out key))
            {
                substanceAdministration.Key = key;
            }

            // Patient
            if (resource.Patient != null)
            {
                var patient = DataTypeConverter.ResolveEntity<SanteDB.Core.Model.Roles.Patient>(resource.Patient, resource);
                substanceAdministration.Participations.Add(new ActParticipation(ActParticipationKeys.RecordTarget, patient));

            }

            // Encounter
            if (resource.Encounter != null)
            {
                var encounter = DataTypeConverter.ResolveEntity<PatientEncounter>(resource.Encounter, resource);
                substanceAdministration.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, substanceAdministration.Key)
                {
                    SourceEntityKey = encounter.Key
                });
            }

            substanceAdministration.Participations.AddRange(resource.Performer.Select(c => new ActParticipation(ActParticipationKeys.Performer, Guid.Parse(c.Actor.Reference.Substring(9)))));

            // Find the material that was issued
            if (resource.VaccineCode != null)
            {
                var concept = DataTypeConverter.ToConcept(resource.VaccineCode);

                if (concept == null)
                {
                    this.m_traceSource.TraceWarning("Ignoring administration {0} don't have concept mapped", resource.VaccineCode);
                    return null;
                }

                // Get the material
                var material = this.m_materialRepository.Find(m => m.TypeConceptKey == concept.Key).FirstOrDefault();
                if (material == null)
                {
                    this.m_traceSource.TraceWarning("Ignoring administration {0} don't have material registered for {1}", resource.VaccineCode, concept?.Mnemonic);
                    return null;
                }

                substanceAdministration.Participations.Add(new ActParticipation(ActParticipationKeys.Product, material.Key));

                if (resource.LotNumber != null)
                {
                    // TODO: Need to also find where the GTIN is kept
                    var manufacturedMaterial = this.m_manufacturedMaterialRepository
                        .Find(o => o.LotNumber == resource.LotNumber && o.Relationships.Any(r => r.SourceEntityKey == material.Key && r.RelationshipTypeKey == EntityRelationshipTypeKeys.Instance))
                        .FirstOrDefault();

                    if (manufacturedMaterial != null)
                    {
                        substanceAdministration.Participations.Add(new ActParticipation(ActParticipationKeys.Consumable, manufacturedMaterial.Key) { Quantity = 1 });
                    }

                }

                // Get dose units
                if (substanceAdministration.DoseQuantity == 0)
                {
                    substanceAdministration.DoseQuantity = 1;
                    substanceAdministration.DoseUnitKey = material.QuantityConceptKey;
                }

                substanceAdministration.TypeConceptKey = this.IMMUNIZATION;
            }

            return substanceAdministration;
        }

        /// <summary>
        /// Query for substance administrations.
        /// </summary>
        /// <param name="query">The query to be executed</param>
        /// <param name="fhirParameters">The fhir parameters provided in the query.</param>
        /// <param name="hdsiParameters">The translated hdsi parameters that can be executed by the query.</param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        protected override IQueryResultSet<SubstanceAdministration> QueryInternal(System.Linq.Expressions.Expression<Func<SubstanceAdministration, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
        {
            var obsoletionReference = System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Equal, System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(SubstanceAdministration.StatusConceptKey))), typeof(Guid)), System.Linq.Expressions.Expression.Constant(StatusKeys.Completed));
            var typeReference = System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Or,
                System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Or,
                    System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Equal, System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(SubstanceAdministration.TypeConceptKey))), typeof(Guid)), System.Linq.Expressions.Expression.Constant(INITIAL_IMMUNIZATION)),
                    System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Equal, System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(SubstanceAdministration.TypeConceptKey))), typeof(Guid)), System.Linq.Expressions.Expression.Constant(IMMUNIZATION))
                ),
                System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Equal, System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(SubstanceAdministration.TypeConceptKey))), typeof(Guid)), System.Linq.Expressions.Expression.Constant(BOOSTER_IMMUNIZATION))
            );

            query = System.Linq.Expressions.Expression.Lambda<Func<SubstanceAdministration, bool>>(System.Linq.Expressions.Expression.AndAlso(System.Linq.Expressions.Expression.AndAlso(obsoletionReference, query.Body), typeReference), query.Parameters);
            return this.m_repository.Find(query);
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Update,
                TypeRestfulInteraction.Delete
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get reverse included resources
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
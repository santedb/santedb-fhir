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
 * Date: 2023-5-19
 */
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using static Hl7.Fhir.Model.CapabilityStatement;
using Expression = System.Linq.Expressions.Expression;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a resource handler for medication administration resources
    /// </summary>
    public class MedicationAdministrationResourceHandler : RepositoryResourceHandlerBase<MedicationAdministration, SubstanceAdministration>
    {
        private readonly Guid[] IZ_TYPES =
        {
            Guid.Parse("f3be6b88-bc8f-4263-a779-86f21ea10a47"), Guid.Parse("6e7a3521-2967-4c0a-80ec-6c5c197b2178"), Guid.Parse("0331e13f-f471-4fbd-92dc-66e0a46239d5")
        };

        /// <summary>
        /// Create a new resource handler
        /// </summary>
        public MedicationAdministrationResourceHandler(IRepositoryService<SubstanceAdministration> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {
        }

        /// <summary>
        /// Can map the specified object
        /// </summary>
        public override bool CanMapObject(object instance)
        {
            return instance is Immunization || instance is SubstanceAdministration sbadm && !this.IZ_TYPES.Contains(sbadm.TypeConceptKey.GetValueOrDefault());
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> includePaths)
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
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Delete
            }.Select(o => new ResourceInteractionComponent
            { Code = o });
        }

        /// <summary>
        /// Get reverse included resources
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Maps the object to model to fhir
        /// </summary>
        protected override MedicationAdministration MapToFhir(SubstanceAdministration model)
        {
            var retVal = DataTypeConverter.CreateResource<MedicationAdministration>(model);

            retVal.Identifier = model.LoadCollection<ActIdentifier>(nameof(Act.Identifiers)).Select(DataTypeConverter.ToFhirIdentifier).ToList();
            retVal.StatusReason = new List<CodeableConcept> { DataTypeConverter.ToFhirCodeableConcept(model.ReasonConceptKey) };

            switch (model.StatusConceptKey.ToString().ToUpper())
            {
                case StatusKeyStrings.Active:
                    retVal.Status = MedicationAdministration.MedicationAdministrationStatusCodes.InProgress;
                    break;

                case StatusKeyStrings.Cancelled:
                    retVal.Status = MedicationAdministration.MedicationAdministrationStatusCodes.Stopped;
                    break;

                case StatusKeyStrings.Nullified:
                    retVal.Status = MedicationAdministration.MedicationAdministrationStatusCodes.EnteredInError;
                    break;

                case StatusKeyStrings.Completed:
                    retVal.Status = MedicationAdministration.MedicationAdministrationStatusCodes.Completed;
                    break;

                case StatusKeyStrings.Obsolete:
                    retVal.Status = MedicationAdministration.MedicationAdministrationStatusCodes.Unknown;
                    break;
            }

            if (model.IsNegated)
            {
                retVal.Status = MedicationAdministration.MedicationAdministrationStatusCodes.NotDone;
            }

            retVal.Category = DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey, "http://hl7.org/fhir/medication-admin-category");

            var consumableRelationship = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Consumable);
            var productRelationship = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Product);

            if (consumableRelationship != null)
            {
                retVal.Medication = DataTypeConverter.CreateVersionedReference<Medication>(consumableRelationship.LoadProperty<ManufacturedMaterial>("PlayerEntity"));
            }
            else if (productRelationship != null)
            {
                retVal.Medication = DataTypeConverter.CreateVersionedReference<Substance>(productRelationship.LoadProperty<Material>("PlayerEntity"));
                //retVal.Medication = DataTypeConverter.ToFhirCodeableConcept(productRelationship.LoadProperty<Material>("PlayerEntity").LoadProperty<Concept>("TypeConcept"));
            }

            var rct = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);
            if (rct != null)
            {
                retVal.Subject = DataTypeConverter.CreateVersionedReference<Hl7.Fhir.Model.Patient>(rct.LoadProperty<Entity>("PlayerEntity"));
            }

            // Encounter
            var erService = ApplicationServiceContext.Current.GetService<IDataPersistenceService<EntityRelationship>>();
            var enc = erService.Query(o => o.TargetEntityKey == model.Key && o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.ObsoleteVersionSequenceId == null, AuthenticationContext.Current.Principal)?.ToArray();
            if (enc?.Any() == true)
            {
                retVal.EventHistory = enc.Select(o => DataTypeConverter.CreateNonVersionedReference<Encounter>(o.TargetEntityKey)).ToList();
                // TODO: Encounter
            }

            // Effective time
            retVal.Effective = DataTypeConverter.ToPeriod(model.StartTime ?? model.ActTime, model.StopTime);

            // performer
            var performer = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).Where(o => o.ParticipationRoleKey == ActParticipationKeys.Performer || o.ParticipationRoleKey == ActParticipationKeys.Authororiginator);

            retVal.Performer = performer.Select(o => new MedicationAdministration.PerformerComponent
            {
                Actor = DataTypeConverter.CreateVersionedReference<Practitioner>(o.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)))
            }).ToList();


            retVal.Dosage = new MedicationAdministration.DosageComponent
            {
                Site = DataTypeConverter.ToFhirCodeableConcept(model.SiteKey),
                Route = DataTypeConverter.ToFhirCodeableConcept(model.RouteKey),
                Dose = DataTypeConverter.ToQuantity(model.DoseQuantity, model.DoseUnitKey)
            };

            return retVal;
        }

        /// <summary>
        /// Map from FHIR to model
        /// </summary>
        protected override SubstanceAdministration MapToModel(MedicationAdministration resource)
        {
            var retVal = new SubstanceAdministration
            {
                Relationships = new List<ActRelationship>(),
                Participations = new List<ActParticipation>(),
                Identifiers = resource.Identifier.Select(DataTypeConverter.ToActIdentifier).ToList(),
                MoodConceptKey = MoodConceptKeys.Eventoccurrence,
                Notes = DataTypeConverter.ToNote<ActNote>(resource.Text)
            };

            retVal.ReasonConcept = DataTypeConverter.ToConcept(resource.StatusReason.FirstOrDefault());

            switch (resource.Status)
            {
                case MedicationAdministration.MedicationAdministrationStatusCodes.InProgress:
                    retVal.StatusConceptKey = StatusKeys.Active;
                    break;
                case MedicationAdministration.MedicationAdministrationStatusCodes.Stopped:
                    retVal.StatusConceptKey = StatusKeys.Cancelled;
                    break;
                case MedicationAdministration.MedicationAdministrationStatusCodes.EnteredInError:
                    retVal.StatusConceptKey = StatusKeys.Nullified;
                    break;
                case MedicationAdministration.MedicationAdministrationStatusCodes.Completed:
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;
                case MedicationAdministration.MedicationAdministrationStatusCodes.Unknown:
                    retVal.StatusConceptKey = StatusKeys.Obsolete;
                    break;
                case MedicationAdministration.MedicationAdministrationStatusCodes.NotDone:
                    retVal.StatusConceptKey = StatusKeys.Cancelled;
                    retVal.IsNegated = true;
                    break;
                default:
                    throw new FhirException(System.Net.HttpStatusCode.BadRequest, OperationOutcome.IssueType.CodeInvalid, $"Status {resource.StatusElement.ObjectValue.ToString()} is not supported.");

            }

            retVal.TypeConcept = DataTypeConverter.ToConcept(resource.Category);

            if (resource.Medication is ResourceReference medicationreference)
            {
                var mmat = DataTypeConverter.ResolveEntity<ManufacturedMaterial>(medicationreference, resource);

                if (null != mmat)
                {
                    retVal.Participations.Add(new ActParticipation(ActParticipationKeys.Consumable, mmat));
                }
                else
                {
                    var mat = DataTypeConverter.ResolveEntity<Material>(medicationreference, resource);

                    if (null != mat)
                    {
                        retVal.Participations.Add(new ActParticipation(ActParticipationKeys.Product, mat));
                    }
                }
            }
            else if (resource.Medication is CodeableConcept medicationconcept)
            {

            }

            if (null != resource.Subject)
            {
                var rectarget = DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource);

                if (null != rectarget)
                {
                    retVal.Participations.Add(new ActParticipation(ActParticipationKeys.RecordTarget, rectarget));
                }
            }

            //TODO: Encounter
            if (resource.EventHistory?.Any() == true)
            {
                foreach (var evt in resource.EventHistory)
                {
                    retVal.Relationships.Add(new ActRelationship()
                    {
                        RelationshipTypeKey = ActRelationshipTypeKeys.HasComponent,
                        SourceEntity = DataTypeConverter.ResolveEntity<Act>(evt, resource),
                        TargetAct = retVal
                    });
                }
            }

            if (resource.Effective is Period effectiveperiod)
            {
                retVal.StartTime = DataTypeConverter.ToDateTimeOffset(effectiveperiod.Start);
                retVal.StopTime = DataTypeConverter.ToDateTimeOffset(effectiveperiod.End);
            }
            else if (resource.Effective is FhirDateTime effectivetime)
            {
                retVal.ActTime = DataTypeConverter.ToDateTimeOffset(effectivetime);
            }

            if (resource.Performer?.Any() == true)
            {
                foreach (var performer in resource.Performer)
                {
                    var actor = DataTypeConverter.ResolveEntity<Entity>(performer.Actor, resource);

                    if (null != actor)
                    {
                        retVal.Participations.Add(new ActParticipation(ActParticipationKeys.Performer, actor));
                    }
                }
            }

            if (null != resource.Dosage)
            {
                retVal.Site = DataTypeConverter.ToConcept(resource.Dosage.Site);
                retVal.Route = DataTypeConverter.ToConcept(resource.Dosage.Route);
                retVal.DoseQuantity = resource.Dosage.Dose.Value.GetValueOrDefault();
                retVal.DoseUnit = DataTypeConverter.ToConcept(resource.Dosage.Dose.Unit, string.IsNullOrWhiteSpace(resource.Dosage.Dose.System) ? "http://hl7.org/fhir/sid/ucum" : resource.Dosage.Dose.System);
            }

            return retVal;
        }

        /// <inheritdoc />
		protected override IQueryResultSet<SubstanceAdministration> QueryInternal(System.Linq.Expressions.Expression<Func<SubstanceAdministration, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
        {
            var drugTherapy = Guid.Parse("7D84A057-1FCC-4054-A51F-B77D230FC6D1");

            var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.Convert(Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(SubstanceAdministration.StatusConceptKey))), typeof(Guid)), Expression.Constant(StatusKeys.Completed));
            var typeReference = Expression.MakeBinary(ExpressionType.Equal, Expression.Convert(Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(SubstanceAdministration.TypeConceptKey))), typeof(Guid)), Expression.Constant(drugTherapy));

            query = Expression.Lambda<Func<SubstanceAdministration, bool>>(Expression.AndAlso(Expression.AndAlso(obsoletionReference, query.Body), typeReference), query.Parameters);

            return this.m_repository.Find(query);
        }


    }
}
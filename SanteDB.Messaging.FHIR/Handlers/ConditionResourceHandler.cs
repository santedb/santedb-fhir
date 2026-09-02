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
using DocumentFormat.OpenXml.Validation;
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
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
    /// Represents a handler for condition observations
    /// </summary>
    public class ConditionResourceHandler : RepositoryResourceHandlerBase<Condition, CodedObservation>
    {
        private readonly IRepositoryService<ActRelationship> m_actRelationship;

        /// <summary>
        /// Create new resource handler
        /// </summary>
        public ConditionResourceHandler(IRepositoryService<CodedObservation> repo, IRepositoryService<ActRelationship> actRelationship, ILocalizationService localizationService) : base(repo, localizationService)
        {
            this.m_actRelationship = actRelationship;
        }

        /// <summary>
        /// Can map
        /// </summary>
        public override bool CanMapObject(object instance) => instance is Condition || instance is CodedObservation cobs && cobs.TypeConceptKey == ObservationTypeKeys.Condition;

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(CodedObservation resource, IEnumerable<IncludeInstruction> includePaths)
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
            }.Select(o => new ResourceInteractionComponent
            { Code = o });
        }

        /// <summary>
        /// Get reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(CodedObservation resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Map to FHIR
        /// </summary>
        protected override Condition MapToFhir(CodedObservation model)
        {
            var retVal = DataTypeConverter.CreateResource<Condition>(model);

            retVal.Identifier = model.LoadCollection<ActIdentifier>("Identifiers").Select(DataTypeConverter.ToFhirIdentifier).ToList();

            // Clinical status of the condition
            if (model.StatusConceptKey == StatusKeys.Active)
            {
                retVal.ClinicalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "active");
            }
            else if (model.StatusConceptKey == StatusKeys.Completed)
            {
                retVal.ClinicalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "resolved");
            }
            else if (StatusKeys.InactiveStates.Contains(model.StatusConceptKey.Value))
            {
                retVal.ClinicalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "inactive");
            }
            else if (model.StatusConceptKey == StatusKeys.Nullified)
            {
                retVal.VerificationStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "entered-in-error");
            }
            else if (model.StatusConceptKey == StatusKeys.Obsolete)
            {
                retVal.ClinicalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "inactive");
            }

            // Category
            retVal.Category.Add(new CodeableConcept("http://hl7.org/fhir/condition-category", "encounter-diagnosis"));

            // Subject 
            var subjectObservation = model.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.RefersTo)?.LoadProperty(o => o.TargetAct) as CodedObservation;

            // Severity?
            var severity = subjectObservation?.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.Severity)?.LoadProperty(o => o.TargetAct) ??
                model.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.Severity)?.LoadProperty(o => o.TargetAct);

            // Severity
            if (severity is CodedObservation sevCode)
            {
                retVal.Severity = DataTypeConverter.ToFhirCodeableConcept(sevCode.ValueKey);
            }

            if (subjectObservation?.TypeConceptKey == model.ValueKey)
            {
                retVal.Category.Add(DataTypeConverter.ToFhirCodeableConcept(model.ValueKey));
                retVal.Code = DataTypeConverter.ToFhirCodeableConcept(subjectObservation.ValueKey);
            }
            else
            {
                retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.ValueKey);
            }


            // Encounter?
            var encounter = this.m_actRelationship.Find(o => (o.TargetActKey == model.Key || o.TargetActKey == subjectObservation.Key) && o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.SourceEntity.ClassConceptKey == ActClassKeys.Encounter).FirstOrDefault();
            if (encounter != null)
            {
                retVal.Encounter = DataTypeConverter.CreateNonVersionedReference<Encounter>(encounter.SourceEntityKey);
            }

            // Verification status 
            var verificationStatus = subjectObservation?.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.VerificationStatus)?.LoadProperty(o => o.TargetAct) ??
                model.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.VerificationStatus)?.LoadProperty(o => o.TargetAct);

            if (verificationStatus is CodedObservation verifyCode)
            {
                retVal.VerificationStatus = DataTypeConverter.ToFhirCodeableConceptPreferred(verifyCode.LoadProperty(o => o.Value), "http://terminology.hl7.org/CodeSystem/condition-ver-status");
            }

            // body sites?
            var sites = model.Relationships.Where(o => o.SourceEntityKey == model.Key && o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.TargetAct.TypeConceptKey == ObservationTypeKeys.FindingSite);
            retVal.BodySite = sites.ToArray().Select(o => DataTypeConverter.ToFhirCodeableConcept((o.LoadProperty(t => t.TargetAct) as CodedObservation).ValueKey)).ToList();

            // Subject
            var recordTarget = model.LoadCollection<ActParticipation>("Participations").FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);

            if (recordTarget != null)
            {
                this.m_traceSource.TraceInfo("RCT: {0}", recordTarget.PlayerEntityKey);
                retVal.Subject = DataTypeConverter.CreateNonVersionedReference<Patient>(recordTarget.LoadProperty<Entity>("PlayerEntity"));
            }

            // Onset observations
            var onsetObs = subjectObservation?.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.DateOfOnset)?.LoadProperty(o => o.TargetAct) ??
                model.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.DateOfOnset)?.LoadProperty(o => o.TargetAct);

            var conclusionObs = subjectObservation?.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.DateOfResolution)?.LoadProperty(o => o.TargetAct) ??
                model.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty(p => p.TargetAct).TypeConceptKey == ObservationTypeKeys.DateOfResolution)?.LoadProperty(o => o.TargetAct);
            // Onset
            if(onsetObs is DateObservation || conclusionObs is DateObservation)
            {
                retVal.Onset = new Period
                {
                    StartElement = DataTypeConverter.ToFhirDateTime((onsetObs as DateObservation)?.Value),
                    EndElement = DataTypeConverter.ToFhirDateTime((conclusionObs as DateObservation)?.Value),
                };
            }
            else if (model.StartTime.HasValue || model.StopTime.HasValue)
            {
                retVal.Onset = new Period
                {
                    StartElement = model.StartTime.HasValue ? DataTypeConverter.ToFhirDateTime(model.StartTime) : null,
                    EndElement = model.StopTime.HasValue ? DataTypeConverter.ToFhirDateTime(model.StopTime) : null
                };
            }

            retVal.RecordedDateElement = DataTypeConverter.ToFhirDateTime(model.CreationTime);

            var author = subjectObservation?.LoadProperty(o => o.Participations).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Authororiginator) ??
                model.LoadProperty(o=>o.Participations).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Authororiginator);

            if (author != null)
            {
                retVal.Recorder = DataTypeConverter.CreateNonVersionedReference<Practitioner>(author.LoadProperty<Entity>("PlayerEntity"));
            }

            // Manifestations
            var manifestations = subjectObservation?.Relationships.Where(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasManifestation) ??
                model.Relationships.Where(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasManifestation);
            if(manifestations.Any())
            {
                retVal.Evidence = manifestations.Select(o => o.LoadProperty(p => p.TargetAct) as CodedObservation).Select(o => new Condition.EvidenceComponent()
                {
                    Code = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConceptPreferred(o.LoadProperty(p => p.Value), "http://snomed.info/sct") },
                    Detail = new List<ResourceReference>() { DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Observation>(o.Key) }
                }).ToList();
            }

            return retVal;
        }

        /// <summary>
        /// Maps a FHIR <see cref="Condition"/> instance to a <see cref="CodedObservation"/> instance.
        /// </summary>
        /// <param name="resource">The FHIR condition to be mapped.</param>
        /// <returns>Returns the constructed model instance.</returns>
        protected override CodedObservation MapToModel(Condition resource)
        {
            var retVal = new CodedObservation()
            {
                Relationships = new List<ActRelationship>(),
                Participations = new List<ActParticipation>(),
                Notes = DataTypeConverter.ToNote<ActNote>(resource.Text),
                MoodConceptKey = MoodConceptKeys.Eventoccurrence,
                Identifiers = resource.Identifier.Select(DataTypeConverter.ToActIdentifier).ToList()
            };

            // Allow for fetching of existing via ID
            if (!Guid.TryParse(resource.Id, out var key))
            {
                key = Guid.NewGuid();
            }
            else
            {
                foreach (var vid in retVal.Identifiers.Where(i => i.LoadProperty(o => o.IdentityDomain).IsUnique))
                {
                    var existingKey = this.QueryInternal(o => o.Identifiers.Where(i => i.IdentityDomainKey == vid.IdentityDomainKey).Any(i => i.Value == vid.Value)).Select(o => o.Key).FirstOrDefault();
                    if (existingKey.HasValue)
                    {
                        key = existingKey.Value;
                        break;
                    }
                }
            }
            retVal.Key = key;
            DataTypeConverter.SetModelPolicies(retVal, resource.Meta?.Security);

            switch (resource.ClinicalStatus.TypeName)
            {
                case "active":
                    retVal.StatusConceptKey = StatusKeys.Active;
                    break;
                case "resolved":
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;
                case "inactive":
                    retVal.StatusConceptKey = StatusKeys.Inactive;
                    break;
            }

            if (resource.VerificationStatus.TypeName == "entered-in-error")
            {
                retVal.StatusConceptKey = StatusKeys.Nullified;
            }

            // Time
            if (resource.Onset is Period onset)
            {
                retVal.StartTime = DataTypeConverter.ToDateTimeOffset(onset.StartElement);
                retVal.StopTime = DataTypeConverter.ToDateTimeOffset(onset.EndElement);

            }
            else if (resource.Onset is FhirDateTime onsetdate)
            {
                retVal.StartTime = DataTypeConverter.ToDateTimeOffset(onsetdate).GetValueOrDefault();

                if (resource.Abatement is FhirDateTime abatementdate)
                {

                    retVal.StopTime = DataTypeConverter.ToDateTimeOffset(abatementdate).GetValueOrDefault();
                }
                else
                {
                    //TODO: Should we set act time here?
                }
            }
            //TODO: Map Age and calculate from the birthdate of the patient.

            if (resource.RecordedDateElement != null)
            {
                retVal.CreationTime = DataTypeConverter.ToDateTimeOffset(resource.RecordedDateElement).GetValueOrDefault();
            }

            // Code
            retVal.Value = DataTypeConverter.ToConcept(resource.Code);

            // Severity
            if (resource.Severity != null)
            {
                var severityTarget = new CodedObservation() { Value = DataTypeConverter.ToConcept(resource.Severity.Coding.FirstOrDefault(), "http://hl7.org/fhir/ValueSet/condition-severity"), TypeConceptKey = ObservationTypeKeys.Severity, MoodConceptKey = MoodConceptKeys.Eventoccurrence, StartTime = retVal.StartTime, StopTime = retVal.StopTime, CreationTime = retVal.CreationTime };
                retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, severityTarget));
            }

            //if (resource.VerificationStatus != null)
            //{
            //    var verificationTarget = new CodedObservation { Value = DataTypeConverter.ToConcept(resource.VerificationStatus.Coding.First(), "http://hl7.org/fhir/ValueSet/condition-ver-status"), TypeConceptKey = ObservationTypeKeys.VerificationStatus, MoodConceptKey = MoodConceptKeys.Eventoccurrence };
            //    retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, verificationTarget));
            //}

            // Site
            if (resource.BodySite.Any())
            {
                var bodySite = new CodedObservation() { Value = DataTypeConverter.ToConcept(resource.BodySite.First()), TypeConceptKey = ObservationTypeKeys.FindingSite, MoodConceptKey = MoodConceptKeys.Eventoccurrence, StartTime = retVal.StartTime, StopTime = retVal.StopTime, CreationTime = retVal.CreationTime };
                retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, bodySite));
            }

            // Subject
            if (resource.Subject != null)
            {
                retVal.Participations.Add(resource.Subject.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.RecordTarget, Guid.Parse(resource.Subject.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.RecordTarget, DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource)));
            }



            // Author
            if (resource.Asserter != null)
            {
                retVal.Participations.Add(resource.Asserter.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.Authororiginator, Guid.Parse(resource.Asserter.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.Authororiginator, DataTypeConverter.ResolveEntity<Core.Model.Roles.Provider>(resource.Asserter, resource))); ;
            }



            retVal.Value = DataTypeConverter.ToConcept(resource.Code);

            return retVal;
        }

        /// <summary>
        /// Query filter
        /// </summary>
        protected override IQueryResultSet<CodedObservation> QueryInternal(Expression<Func<CodedObservation, bool>> query, NameValueCollection fhirParameters = null, NameValueCollection hdsiParameters = null)
        {
            var anyRef = this.CreateConceptSetFilter(ConceptSetKeys.ProblemObservations, query.Parameters[0]);
            query = Expression.Lambda<Func<CodedObservation, bool>>(Expression.AndAlso(query.Body, anyRef), query.Parameters);

            return base.QueryInternal(query, fhirParameters, hdsiParameters);
        }
    }
}
/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using System.Linq.Expressions;
using static Hl7.Fhir.Model.CapabilityStatement;
using Expression = System.Linq.Expressions.Expression;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Adverse event resource handler
    /// </summary>
    public class AdverseEventResourceHandler : RepositoryResourceHandlerBase<AdverseEvent, Act>
    {
        /// <summary>
        /// Adverse event repo
        /// </summary>
        public AdverseEventResourceHandler(IRepositoryService<Act> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {
        }

        /// <inheritdoc/>
        public override bool CanMapObject(object instance)
        {
            return base.CanMapObject(instance);
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetIncludes(Act resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetReverseIncludes(Act resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <inheritdoc/>
        protected override AdverseEvent MapToFhir(Act model)
        {
            var retVal = DataTypeConverter.CreateResource<AdverseEvent>(model);

            retVal.Identifier = DataTypeConverter.ToFhirIdentifier(model.Identifiers.FirstOrDefault());
            retVal.Category = new List<CodeableConcept>
                {DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey)};

            var modelparticipations = model.LoadCollection(m => m.Participations);
            var modelrelationships = model.LoadCollection(m => m.Relationships);

            var recordTarget = modelparticipations?.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);
            if (recordTarget != null)
            {
                retVal.Subject = DataTypeConverter.CreateVersionedReference<Patient>(recordTarget.LoadProperty<Entity>(nameof(recordTarget.PlayerEntity)));
            }

            // Main topic of the concern
            var subject = modelrelationships?.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasSubject)?.LoadProperty<Act>(nameof(ActRelationship.TargetAct));
            if (subject == null)
            {
                throw new InvalidOperationException(this.m_localizationService.GetString("error.messaging.fhir.adverseEvent.act"));
            }

            retVal.DateElement = new FhirDateTime(subject.ActTime.GetValueOrDefault());

            var subjectrelationships = subject.LoadCollection(s => s.Relationships);

            // Reactions = HasManifestation
            var reactions = subjectrelationships?.Where(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasManifestation)?.FirstOrDefault();
            if (reactions != null)
            {
                retVal.Event = DataTypeConverter.ToFhirCodeableConcept(reactions.LoadProperty<CodedObservation>(nameof(ActRelationship.TargetAct)).ValueKey);
            }

            var location = modelparticipations?.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Location);
            if (location != null)
            {
                retVal.Location = DataTypeConverter.CreateVersionedReference<Location>(location.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)));
            }

            // Severity

            

            var severity = subjectrelationships?.Where(r => r.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent)
                ?.Select(r => (relationship: r, targetAct: r.LoadProperty<CodedObservation>(nameof(ActRelationship.TargetAct))))
                ?.Where(t => t.targetAct.TypeConceptKey == ObservationTypeKeys.Severity)
                ?.FirstOrDefault().targetAct;

            if (severity != null)
            {
                retVal.Severity = DataTypeConverter.ToFhirCodeableConcept(severity.ValueKey, "http://terminology.hl7.org/CodeSystem/adverse-event-severity");
            }

            // Did the patient die?

            var causeOfDeath = modelrelationships?.Where(r => r.RelationshipTypeKey == ActRelationshipTypeKeys.IsCauseOf)
                ?.Select(s => (relationship: s, targetAct: s.LoadProperty<CodedObservation>(nameof(ActRelationship.TargetAct))))
                ?.Where(t => t.targetAct?.TypeConceptKey == ObservationTypeKeys.ClinicalState && t.targetAct?.ValueKey == DischargeDispositionKeys.Died)
                ?.FirstOrDefault().relationship;

            if (causeOfDeath != null)
            {
                retVal.Outcome = new CodeableConcept("http://hl7.org/fhir/adverse-event-outcome", "fatal");
            }
            else if (model.StatusConceptKey == StatusKeys.Active)
            {
                retVal.Outcome = new CodeableConcept("http://hl7.org/fhir/adverse-event-outcome", "ongoing");
            }
            else if (model.StatusConceptKey == StatusKeys.Completed)
            {
                retVal.Outcome = new CodeableConcept("http://hl7.org/fhir/adverse-event-outcome", "resolved");
            }

            var author = modelparticipations?.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Authororiginator);
            if (author != null)
            {
                retVal.Recorder = DataTypeConverter.CreateNonVersionedReference<Practitioner>(author.LoadProperty(a => a.PlayerEntity));
            }

            // Suspect entities
            var refersTo = modelrelationships?.Where(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.RefersTo);
            if (refersTo?.Any() == true)
            {
                retVal.SuspectEntity = refersTo.Select(o => o.LoadProperty<SubstanceAdministration>(nameof(ActRelationship.TargetAct))).Select(o =>
                {
                    var consumable = o.LoadCollection<ActParticipation>("Participations").FirstOrDefault(x => x.ParticipationRoleKey == ActParticipationKeys.Consumable)?.LoadProperty<ManufacturedMaterial>("PlayerEntity");
                    if (consumable == null)
                    {
                        var product = o.LoadCollection<ActParticipation>("Participations").FirstOrDefault(x => x.ParticipationRoleKey == ActParticipationKeys.Product)?.LoadProperty<Material>("PlayerEntity");

                        return new AdverseEvent.SuspectEntityComponent
                        {
                            Instance = DataTypeConverter.CreateNonVersionedReference<Substance>(product)
                        };
                    }

                    return new AdverseEvent.SuspectEntityComponent
                    {
                        Instance = DataTypeConverter.CreateNonVersionedReference<Medication>(consumable)
                    };

                }).ToList();
            }

            return retVal;
        }

        /// <inheritdoc/>
        protected override Act MapToModel(AdverseEvent resource)
        {

#if !DEBUG
            throw new NotSupportedException();
#endif

            var retVal = new Act()
            {
                Identifiers = new List<ActIdentifier>(),
                Participations = new List<ActParticipation>(),
                Relationships = new List<ActRelationship>(),
                Notes = DataTypeConverter.ToNote<ActNote>(resource.Text)
            };

            if (!Guid.TryParse(resource.Id, out var key))
            {
                key = Guid.NewGuid();
            }

            retVal.ClassConceptKey = ActClassKeys.Act;

            //      retVal.StatusConceptKey = StatusKeys.Active;

            retVal.Key = key;

            retVal.MoodConceptKey = ActMoodKeys.Eventoccurrence;

            // map identifier to identifiers
            retVal.Identifiers.Add(DataTypeConverter.ToActIdentifier(resource.Identifier));
            /* retVal.Identifiers = new List<ActIdentifier>
            {
                DataTypeConverter.ToActIdentifier(resource.Identifier)
            };*/

            //map category to type concept
            retVal.TypeConcept = DataTypeConverter.ToConcept(resource.Category.FirstOrDefault());

            // map subject to patient
            if (resource.Subject != null)
            {
                retVal.Participations.Add(resource.Subject.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.RecordTarget, Guid.Parse(resource.Subject.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.RecordTarget, DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource)));
            }

            // map date element to act time
            var occurTime = (DateTimeOffset)DataTypeConverter.ToDateTimeOffset(resource.DateElement);
            var targetAct = new CodedObservation() { ActTime = occurTime };

            retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasSubject, targetAct));
            retVal.ActTime = occurTime;

            // map event to relationships
            var reactionTarget = new CodedObservation() { Value = DataTypeConverter.ToConcept(resource.Event) };
            targetAct.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasManifestation, reactionTarget));

            // map location to place
            if (resource.Location != null)
            {
                retVal.Participations.Add(resource.Location.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.Location, Guid.Parse(resource.Location.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.Location, DataTypeConverter.ResolveEntity<Core.Model.Entities.Place>(resource.Location, resource)));

                // retVal.Participations.Add(new ActParticipation(ActParticipationKey.Location, DataTypeConverter.ResolveEntity<Core.Model.Entities.Place>(resource.Location, resource)));
            }

            // map seriousness to relationships
            if (resource.Severity != null)
            {
                var severityTarget = new CodedObservation() { Value = DataTypeConverter.ToConcept(resource.Severity.Coding.FirstOrDefault(), "http://terminology.hl7.org/CodeSystem/adverse-event-severity"), TypeConceptKey = ObservationTypeKeys.Severity };
                targetAct.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, severityTarget));
            }

            // map recoder to provider
            if (resource.Recorder != null)
            {
                retVal.Participations.Add(resource.Recorder.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.Authororiginator, Guid.Parse(resource.Recorder.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.Authororiginator, DataTypeConverter.ResolveEntity<Core.Model.Roles.Provider>(resource.Recorder, resource)));

                //  retVal.Participations.Add(new ActParticipation(ActParticipationKey.Authororiginator, DataTypeConverter.ResolveEntity<Core.Model.Roles.Provider>(resource.Recorder, resource)));
            }

            // map outcome to status concept key or relationships

            if (resource.Outcome != null)
            {
                if (resource.Outcome.Coding.Any(o => o.System == "http://hl7.org/fhir/adverse-event-outcome"))
                {
                    if (resource.Outcome.Coding.Any(o => o.Code == "fatal"))
                    {
                        retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.IsCauseOf, new CodedObservation { TypeConceptKey = ObservationTypeKeys.ClinicalState, ValueKey = DischargeDispositionKeys.Died }));
                    }
                    else if (resource.Outcome.Coding.Any(o => o.Code == "ongoing"))
                    {
                        retVal.StatusConceptKey = StatusKeys.Active;
                    }
                    else if (resource.Outcome.Coding.Any(o => o.Code == "resolved"))
                    {
                        retVal.StatusConceptKey = StatusKeys.Completed;
                    }
                }
            }

            //  map instance to relationships and participations
            if (resource.SuspectEntity != null)
            {
                foreach (var component in resource.SuspectEntity)
                {
                    var adm = new SubstanceAdministration();
                    if (component.Instance.GetType() == typeof(Medication))
                    {
                        adm.Participations.Add(component.Instance.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.Consumable, Guid.Parse(component.Instance.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.Consumable, DataTypeConverter.ResolveEntity<Core.Model.Entities.ManufacturedMaterial>(component.Instance, resource)));

                        //  adm.Participations.Add(new ActParticipation(ActParticipationKey.Consumable, DataTypeConverter.ResolveEntity<Core.Model.Entities.ManufacturedMaterial>(component.Instance, resource)));

                        retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.RefersTo, adm));

                    }
                    else if (component.Instance.GetType() == typeof(Substance))
                    {
                        adm.Participations.Add((component.Instance.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.Product, Guid.Parse(component.Instance.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.Product, DataTypeConverter.ResolveEntity<Core.Model.Entities.Material>(component.Instance, resource))));

                        //  adm.Participations.Add(new ActParticipation(ActParticipationKey.Product, DataTypeConverter.ResolveEntity<Core.Model.Entities.Material>(component.Instance, resource)));

                        retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.RefersTo, adm));
                    }
                }

            }
            return retVal;
        }

        /// <summary>
        /// Query for specified adverse event
        /// </summary>
        protected override IQueryResultSet<Act> QueryInternal(Expression<Func<Act, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
        {
            var typeReference = Expression.MakeBinary(ExpressionType.Equal, Expression.Convert(Expression.MakeMemberAccess(query.Parameters[0], typeof(Act).GetProperty(nameof(Act.ClassConceptKey))), typeof(Guid)), Expression.Constant(ActClassKeys.Condition));

            var anyRef = this.CreateConceptSetFilter(ConceptSetKeys.AdverseEventActs, query.Parameters[0]);
            query = Expression.Lambda<Func<Act, bool>>(Expression.AndAlso(Expression.AndAlso(query.Body, anyRef), typeReference), query.Parameters);

            return base.QueryInternal(query, fhirParameters, hdsiParameters);
        }

    }
}
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
using DocumentFormat.OpenXml.Office2013.Excel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Snapshot;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
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
    /// Adverse event resource handler
    /// </summary>
    public class AdverseEventResourceHandler : RepositoryResourceHandlerBase<AdverseEvent, Act>
    {
        private readonly Guid?[] m_adverseEventTypes;
        private readonly IRepositoryService<ActRelationship> m_relationshipService;

        /// <summary>
        /// Adverse event repo
        /// </summary>
        public AdverseEventResourceHandler(IRepositoryService<Act> repo, IRepositoryService<ActRelationship> relationshipService, IConceptRepositoryService conceptRepositoryService, ILocalizationService localizationService) : base(repo, localizationService)
        {
            this.m_adverseEventTypes = conceptRepositoryService.ExpandConceptSet(ConceptSetKeys.AdverseEventActs).Select(o => o.Key).ToArray();
            this.m_relationshipService = relationshipService;
        }

        /// <inheritdoc/>
        public override bool CanMapObject(object instance)
        {
            return instance is AdverseEvent ||
                instance is Act act &&
                m_adverseEventTypes.Contains(act.TypeConceptKey.GetValueOrDefault());
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


            // TODO: Map this to allow suspected 
            retVal.Actuality = AdverseEvent.AdverseEventActuality.Actual;

            var modelparticipations = model.LoadCollection(m => m.Participations);
            var modelrelationships = model.LoadCollection(m => m.Relationships);

            var recordTarget = modelparticipations?.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);
            if (recordTarget != null)
            {
                retVal.Subject = DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Patient>(recordTarget.LoadProperty<Entity>(nameof(recordTarget.PlayerEntity)));
            }

            // Main topic of the concern
            var subject = modelrelationships?.FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasSubject)?.LoadProperty<Act>(nameof(ActRelationship.TargetAct));
            if (subject == null)
            {
                throw new InvalidOperationException(this.m_localizationService.GetString("error.messaging.fhir.adverseEvent.act"));
            }

            retVal.DateElement = new FhirDateTime((model.StartTime ?? model.ActTime).GetValueOrDefault());
            retVal.DetectedElement = new FhirDateTime(subject.ActTime.GetValueOrDefault());
            retVal.RecordedDateElement = new FhirDateTime(model.CreationTime);
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
                retVal.Location = DataTypeConverter.CreateNonVersionedReference<Location>(location.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)));
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
            retVal.Contributor = modelparticipations?.Where(o => o.ParticipationRoleKey == ActParticipationKeys.Performer || o.ParticipationRoleKey == ActParticipationKeys.SecondaryPerformer).Select(o=> DataTypeConverter.CreateRimReference(o.LoadProperty(p=>p.PlayerEntity))).ToList();

            // Caused Condition
            var condition = this.m_relationshipService.Find(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.RefersTo && o.SourceEntity.TypeConceptKey == ObservationTypeKeys.Condition && o.TargetActKey == subject.Key).FirstOrDefault();
            if(condition != null)
            {
                retVal.ResultingCondition = new List<ResourceReference>() { DataTypeConverter.CreateNonVersionedReference<Condition>(condition.SourceEntityKey) };
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

                        if(product == null)
                        {
                            return new AdverseEvent.SuspectEntityComponent()
                            {
                                Instance = DataTypeConverter.CreateRimReference(o)
                            };
                        }
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

            var encounter = this.m_relationshipService.Find(r => r.SourceEntity.ClassConceptKey == ActClassKeys.Encounter && r.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && (r.TargetActKey == subject.Key || r.TargetActKey == model.Key)).Select(o=>o.SourceEntityKey).FirstOrDefault();
            if (encounter != null)
            {
                retVal.Encounter = DataTypeConverter.CreateNonVersionedReference<Encounter>(encounter);
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

            // map identifier to identifiers
            var identifer = DataTypeConverter.ToActIdentifier(resource.Identifier);
            retVal.Identifiers.Add(identifer);
            // Allow for fetching of existing via ID
            if (!Guid.TryParse(resource.Id, out var key))
            {
                key = Guid.NewGuid();
            }
            else if (identifer.LoadProperty(o => o.IdentityDomain).IsUnique)
            {
                key = this.QueryInternal(o => o.Identifiers.Where(i => i.IdentityDomainKey == identifer.IdentityDomainKey).Any(i => i.Value == identifer.Value)).Select(o => o.Key).FirstOrDefault() ?? Guid.NewGuid();
            }
            retVal.Key = key;
            DataTypeConverter.SetModelPolicies(retVal, resource.Meta?.Security);

            retVal.ClassConceptKey = ActClassKeys.Act;
            retVal.MoodConceptKey = ActMoodKeys.Eventoccurrence;

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
        protected override IQueryResultSet<Act> QueryInternal(Expression<Func<Act, bool>> query, NameValueCollection fhirParameters = null, NameValueCollection hdsiParameters = null)
        {
            var typeReference = Expression.MakeBinary(ExpressionType.Equal, Expression.Convert(Expression.MakeMemberAccess(query.Parameters[0], typeof(Act).GetProperty(nameof(Act.ClassConceptKey))), typeof(Guid)), Expression.Constant(ActClassKeys.Cluster));

            var anyRef = this.CreateConceptSetFilter(ConceptSetKeys.AdverseEventActs, query.Parameters[0]);
            query = Expression.Lambda<Func<Act, bool>>(Expression.AndAlso(query.Body, Expression.AndAlso(Expression.AndAlso(query.Body, anyRef), typeReference)), query.Parameters);

            return base.QueryInternal(query, fhirParameters, hdsiParameters);
        }

        /// <inheritdoc/>
        public override StructureDefinition GetStructureDefinition()
        {
            var retVal = base.GetStructureDefinition();

            retVal.Description = new Markdown("An Act with a type concept in the AdvserEventTypes concept set to a FHIR AdverseEvent (examples: `AdervesEventFollowingProcedure`)");

            retVal.ConstrainIdentifier<Act>();
            retVal.ConstrainField("category")
                .WithMaxOccurs("1")
                .WithDefinition("Maps to the type concept of the underlying condition")
                .Mapping<Act>(o => o.TypeConcept);

            retVal.ConstrainField("actuality")
                .WithMaxOccurs("1")
                .WithFixedValue(new Code<AdverseEvent.AdverseEventActuality>(AdverseEvent.AdverseEventActuality.Actual));

            retVal.ConstrainField("subject")
                .WithDefinition("Reference to patient")
                .WithType(FHIRAllTypes.Reference, ResourceType.Patient)
                .Mapping<Act>(o => o.Participations.Where(p => p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.RecordTarget)).FirstOrDefault().PlayerEntity);

            retVal.ConstrainField("event")
                .WithDefinition("Mapped to the manifestation of the adverse event")
                .Mapping<Act>(o => (o.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasSubject)).FirstOrDefault().TargetAct.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasManifestation)).FirstOrDefault().TargetAct as CodedObservation).Value);

            retVal.ConstrainField("encounter")
                .WithDefinition("When part of an encounter")
                .Mapping<Act>(o => o.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasComponent) && r.SourceEntity.TypeConcept.Mnemonic == nameof(ActClassKeys.Encounter)).FirstOrDefault().SourceEntity as PatientEncounter);

            retVal.ConstrainField("date")
                .WithDefinition("Date of the original event which was the concern")
                .Mapping<Act>(o => o.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasSubject)).FirstOrDefault().TargetAct.ActTime);

            retVal.ConstrainField("recordedDate")
                .WithDefinition("Creation date of the adverse event")
                .Mapping<Act>(o => o.CreationTime);

            retVal.ConstrainField("resultingCondition")
                .WithMaxOccurs("1")
                .Mapping<Act>(o => o.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasSubject)).FirstOrDefault().TargetAct.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.RefersTo)).FirstOrDefault().SourceEntity);

            retVal.ConstrainField("location")
                .Mapping<Act>(o => o.Participations.Where(p => p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.Location)).FirstOrDefault().PlayerEntity);

            retVal.ConstrainField("seriousness")
                .NotSupported();

            retVal.ConstrainField("severity")
                .Mapping<Act>(o => (o.Relationships.Where(p => p.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasSubject)).FirstOrDefault().TargetAct.Relationships.Where(r => r.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.HasComponent) && r.TargetAct.TypeConcept.Mnemonic == nameof(ObservationTypeKeys.Severity)).FirstOrDefault().TargetAct as CodedObservation).Value);

            retVal.ConstrainField("recorder")
                .WithType(FHIRAllTypes.Reference, ResourceType.Practitioner)
                .Mapping<Act>(o => o.Participations.Where(p => p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.Authororiginator)).FirstOrDefault().PlayerEntity);

            retVal.ConstrainField("contributor")
                .WithType(FHIRAllTypes.Reference, ResourceType.Practitioner, ResourceType.Device)
                .Mapping<Act>(o => o.Participations.Where(p => p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.Performer) || p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.SecondaryPerformer)).FirstOrDefault().PlayerEntity);

            retVal.ConstrainField("suspectEntity");
            retVal.ConstrainField("suspectEntity.instance")
                .WithDefinition("The immunization instance / event which is suspected to have caused the event")
                .WithType(FHIRAllTypes.Reference, ResourceType.Immunization, ResourceType.Substance, ResourceType.Medication)
                .Mapping<Act>(o => o.Relationships.Where(p => p.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.RefersTo)).FirstOrDefault().TargetAct.Participations.Where(p => p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.Consumable)).FirstOrDefault().PlayerEntity)
                .Mapping<Act>(o => o.Relationships.Where(p => p.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.RefersTo)).FirstOrDefault().TargetAct.Participations.Where(p => p.ParticipationRole.Mnemonic == nameof(ActParticipationKeys.Product)).FirstOrDefault().PlayerEntity)
                .Mapping<Act>(o => o.Relationships.Where(p => p.RelationshipType.Mnemonic == nameof(ActRelationshipTypeKeys.RefersTo)).FirstOrDefault().TargetAct);

            retVal.ConstrainField("subjectMedicalHistory")
                .NotSupported();
            retVal.ConstrainField("referenceDocument")
                .NotSupported();
            retVal.ConstrainField("study")
                .NotSupported();
            return retVal;


        }

    }
}
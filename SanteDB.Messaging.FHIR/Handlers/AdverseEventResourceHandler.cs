﻿/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-5
 */

using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Can map this object
        /// </summary>
        public override bool CanMapObject(object instance)
        {
            return base.CanMapObject(instance);
        }
        
        /// <summary>
        /// Maps the specified act to an adverse event
        /// </summary>
        protected override AdverseEvent MapToFhir(Act model)
        {
            var retVal = DataTypeConverter.CreateResource<AdverseEvent>(model);

            retVal.Identifier = DataTypeConverter.ToFhirIdentifier(model.Identifiers.FirstOrDefault());
            retVal.Category = new List<CodeableConcept>
                {DataTypeConverter.ToFhirCodeableConcept(model.LoadProperty<Concept>("TypeConcept"))};

            var recordTarget = model.LoadCollection<ActParticipation>("Participations").FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);
            if (recordTarget != null)
            {
                retVal.Subject = DataTypeConverter.CreateVersionedReference<Patient>(recordTarget.LoadProperty<Entity>("PlayerEntity"));
            }

            // Main topic of the concern
            var subject = model.LoadCollection<ActRelationship>("Relationships").FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasSubject)?.LoadProperty<Act>("TargetAct");
            if (subject == null)
            {
                throw new InvalidOperationException(this.m_localizationService.GetString("error.messaging.fhir.adverseEvent.act"));
            }

            retVal.DateElement = new FhirDateTime(subject.ActTime.GetValueOrDefault());

            // Reactions = HasManifestation
            var reactions = subject.LoadCollection<ActRelationship>("Relationships").Where(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasManifestation).FirstOrDefault();
            if (reactions != null)
            {
                retVal.Event = DataTypeConverter.ToFhirCodeableConcept(reactions.LoadProperty<CodedObservation>("TargetAct").LoadProperty<Concept>(nameof(CodedObservation.Value)));
            }

            var location = model.LoadCollection<ActParticipation>("Participations").FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Location);
            if (location != null)
            {
                retVal.Location = DataTypeConverter.CreateVersionedReference<Location>(location.LoadProperty<Entity>("PlayerEntity"));
            }

            // Severity
            var severity = subject.LoadCollection<ActRelationship>("Relationships").First(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.LoadProperty<Act>("TargetAct").TypeConceptKey == ObservationTypeKeys.Severity);
            if (severity != null)
            {
                retVal.Seriousness = DataTypeConverter.ToFhirCodeableConcept(severity.LoadProperty<CodedObservation>("TargetAct").Value, "http://hl7.org/fhir/adverse-event-seriousness");
            }

            // Did the patient die?
            var causeOfDeath = model.LoadCollection<ActRelationship>("Relationships").FirstOrDefault(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.IsCauseOf && o.LoadProperty<Act>("TargetAct").TypeConceptKey == ObservationTypeKeys.ClinicalState && (o.TargetAct as CodedObservation)?.ValueKey == Guid.Parse("6df3720b-857f-4ba2-826f-b7f1d3c3adbb"));
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

            var author = model.LoadCollection<ActParticipation>("Participations").FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Authororiginator);
            if (author != null)
            {
                retVal.Recorder = DataTypeConverter.CreateNonVersionedReference<Practitioner>(author.LoadProperty<Entity>("PlayerEntity"));
            }

            // Suspect entities
            var refersTo = model.LoadCollection<ActRelationship>("Relationships").Where(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.RefersTo).ToArray();
            if (refersTo.Any())
            {
                retVal.SuspectEntity = refersTo.Select(o => o.LoadProperty<Act>("TargetAct")).OfType<SubstanceAdministration>().Select(o =>
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

        /// <summary>
        /// Map adverse events to the model
        /// </summary>
        protected override Act MapToModel(AdverseEvent resource)
        {
            var retVal = new Act();

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
                retVal.Participations.Add(resource.Subject.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.RecordTarget, Guid.Parse(resource.Subject.Reference.Substring(9))): new ActParticipation(ActParticipationKeys.RecordTarget, DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource)));
            }

            // map date element to act time
            var occurTime = (DateTimeOffset)DataTypeConverter.ToDateTimeOffset(resource.DateElement);
            var targetAct = new Act() { ActTime = occurTime };

            retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasSubject, targetAct));
            retVal.ActTime = occurTime;

            // map event to relationships
            var reactionTarget = new CodedObservation() {Value = DataTypeConverter.ToConcept(resource.Event)};
            targetAct.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasManifestation, reactionTarget));

            // map location to place
            if (resource.Location != null)
            {
                retVal.Participations.Add(resource.Location.Reference.StartsWith("urn:uuid:") ? new ActParticipation(ActParticipationKeys.Location, Guid.Parse(resource.Location.Reference.Substring(9))) : new ActParticipation(ActParticipationKeys.Location, DataTypeConverter.ResolveEntity<Core.Model.Entities.Place>(resource.Location, resource)));

               // retVal.Participations.Add(new ActParticipation(ActParticipationKey.Location, DataTypeConverter.ResolveEntity<Core.Model.Entities.Place>(resource.Location, resource)));
            }

            // map seriousness to relationships

            var severityTarget = new CodedObservation() { Value = DataTypeConverter.ToConcept(resource.Seriousness.Coding.FirstOrDefault(), "http://hl7.org/fhir/adverse-event-seriousness"), TypeConceptKey = ObservationTypeKeys.Severity };
            targetAct.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, severityTarget));

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
                        retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.IsCauseOf, new CodedObservation { TypeConceptKey = ObservationTypeKeys.ClinicalState, ValueKey = Guid.Parse("6df3720b-857f-4ba2-826f-b7f1d3c3adbb") }));
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
                    else if(component.Instance.GetType() == typeof(Substance))
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
        protected override IQueryResultSet<Act> Query(Expression<Func<Act, bool>> query)
        {
            var typeReference = Expression.MakeBinary(ExpressionType.Equal, Expression.Convert(Expression.MakeMemberAccess(query.Parameters[0], typeof(Act).GetProperty(nameof(Act.ClassConceptKey))), typeof(Guid)), Expression.Constant(ActClassKeys.Condition));

            var anyRef = this.CreateConceptSetFilter(ConceptSetKeys.AdverseEventActs, query.Parameters[0]);
            query = Expression.Lambda<Func<Act, bool>>(Expression.AndAlso(Expression.AndAlso(query.Body, anyRef), typeReference), query.Parameters);

            return base.Query(query);
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
                TypeRestfulInteraction.Delete
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        protected override IEnumerable<Resource> GetIncludes(Act resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        protected override IEnumerable<Resource> GetReverseIncludes(Act resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
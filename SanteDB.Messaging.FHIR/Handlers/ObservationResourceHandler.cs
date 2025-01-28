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
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static Hl7.Fhir.Model.CapabilityStatement;
using NameValueCollection = System.Collections.Specialized.NameValueCollection;
using Observation = Hl7.Fhir.Model.Observation;
using Patient = Hl7.Fhir.Model.Patient;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Observation handler
    /// </summary>
    public class ObservationResourceHandler : RepositoryResourceHandlerBase<Observation, Core.Model.Acts.Observation>
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ObservationResourceHandler));

        readonly List<Guid> m_AllergyTypes;
        readonly List<Guid> m_ListTypes;

        readonly IRepositoryService<Act> m_ActRepository;
        readonly IRepositoryService<ActRelationship> m_ActRelationshipRepository;

        const string UUID_PREFIX = "urn:uuid:";

        /// <summary>
        /// Create new resource handler
        /// </summary>
        public ObservationResourceHandler(IRepositoryService<Core.Model.Acts.Observation> repository, ILocalizationService localizationService, IConceptRepositoryService conceptRepository, IRepositoryService<Act> actRepository, IRepositoryService<ActRelationship> actRelationshipRepository)
            : base(repository, localizationService)
        {
            m_AllergyTypes = conceptRepository.Find(o => o.ConceptSets.Any(cs => cs.Mnemonic == "AllergyIntoleranceCode")).Select(o => o.Key.Value).ToList();
            m_ListTypes = conceptRepository.Find(o => o.ConceptSets.Any(cs => cs.Mnemonic == "ObservationCategoryCodes")).Select(o => o.Key.Value).ToList();
            m_ActRepository = actRepository;
            m_ActRelationshipRepository = actRelationshipRepository;
        }

        /// <inheritdoc />
        public override bool CanMapObject(object instance) => instance is Observation ||
            (
                instance is Core.Model.Acts.Observation obs &&
                null != obs.TypeConceptKey &&
                (!m_AllergyTypes.Contains(obs.TypeConceptKey.Value) || obs.TypeConceptKey == ObservationTypeKeys.Condition)
            );


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
                TypeRestfulInteraction.Delete,
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Update
            }.Select(o => new ResourceInteractionComponent
            { Code = o });
        }

        /// <summary>
        /// Get reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Acts.Observation resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Map to FHIR
        /// </summary>
        protected override Observation MapToFhir(Core.Model.Acts.Observation model)
        {
            var retVal = DataTypeConverter.CreateResource<Observation>(model);
            retVal.Identifier = model.LoadProperty(o => o.Identifiers).Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

            switch (model.StatusConceptKey.ToString().ToUpper())
            {
                case StatusKeyStrings.New:
                case StatusKeyStrings.Active:
                    retVal.Status = ObservationStatus.Preliminary;
                    break;

                case StatusKeyStrings.Cancelled:
                    retVal.Status = ObservationStatus.Cancelled;
                    break;

                case StatusKeyStrings.Nullified:
                    retVal.Status = ObservationStatus.EnteredInError;
                    break;

                case StatusKeyStrings.Completed:
                    if (model.LoadProperty(o => o.Relationships).Any(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.Replaces)) // was amended
                    {
                        retVal.Status = ObservationStatus.Amended;
                    }
                    else
                    {
                        retVal.Status = ObservationStatus.Final;
                    }

                    break;

                case StatusKeyStrings.Obsolete:
                    retVal.Status = ObservationStatus.Unknown;
                    break;
            }

            if (model.StartTime.HasValue || model.StopTime.HasValue)
            {
                retVal.Effective = DataTypeConverter.ToPeriod(model.StartTime, model.StopTime);
            }
            else
            {
                retVal.Effective = DataTypeConverter.ToFhirDateTime(model.ActTime);
            }

            retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey);

            // RCT
            var rct = model.LoadProperty(o => o.Participations).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget);

            if (rct != null)
            {
                retVal.Subject = DataTypeConverter.CreateNonVersionedReference<Patient>(rct.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)));
            }

            // Performer
            retVal.Performer = model.LoadProperty(o => o.Participations).Where(o => o.ParticipationRoleKey == ActParticipationKeys.Performer)
                .Select(o => DataTypeConverter.CreateNonVersionedReference<Practitioner>(o.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity))))
                .ToList();

            retVal.Issued = model.CreationTime;

            var categoryrelationships = m_ActRelationshipRepository.Find(o => o.TargetActKey == model.Key && o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.SourceEntity.ClassConceptKey == ActClassKeys.List).ToList();
            categoryrelationships.ForEach(r => r.LoadProperty(selector => selector.SourceEntity));

            retVal.Category = categoryrelationships
                    ?.Select(ar => ar.SourceEntity.TypeConceptKey)
                    ?.Select(t => DataTypeConverter.ToFhirCodeableConcept(t, FhirConstants.DefaultObservationCategorySystem))?.ToList();

            // Value
            switch (model.ValueType)
            {
                case "CD":
                    retVal.Value = DataTypeConverter.ToFhirCodeableConcept((model as CodedObservation).ValueKey);
                    break;

                case "PQ":
                    var qty = model as QuantityObservation;
                    retVal.Value = DataTypeConverter.ToQuantity(qty.Value, qty.UnitOfMeasureKey);
                    break;

                case "ED":
                case "ST":
                    retVal.Value = new FhirString((model as TextObservation).Value);
                    break;
            }

            if (model.InterpretationConceptKey.HasValue)
            {
                retVal.Interpretation = new List<CodeableConcept>
                {
                    DataTypeConverter.ToFhirCodeableConcept(model.InterpretationConceptKey)
                };
            }

            return retVal;
        }

        /// <summary>
        /// Map to model
        /// </summary>
        protected override Core.Model.Acts.Observation MapToModel(Observation resource)
        {
            //value type and value
            Core.Model.Acts.Observation retVal;
            switch (resource.Value)
            {
                case CodeableConcept codeableConcept:
                    retVal = new CodedObservation
                    {
                        ValueType = "CD",
                        Value = DataTypeConverter.ToConcept(codeableConcept),
                        Relationships = new List<ActRelationship>(),
                        Participations = new List<ActParticipation>()
                    };
                    break;

                case Quantity quantity:
                    retVal = new QuantityObservation
                    {
                        ValueType = "PQ",
                        Value = quantity.Value.Value,
                        UnitOfMeasure = DataTypeConverter.ToConcept(quantity.Unit, string.IsNullOrWhiteSpace(quantity.System) ? FhirConstants.DefaultQuantityUnitSystem : quantity.System),
                        Relationships = new List<ActRelationship>(),
                        Participations = new List<ActParticipation>()
                    };
                    break;

                case FhirString fhirString:
                    retVal = new TextObservation
                    {
                        ValueType = "ST",
                        Value = fhirString.Value,
                        Participations = new List<ActParticipation>()
                    };
                    break;

                default:
                    retVal = new Core.Model.Acts.Observation();
                    break;
            }

            retVal.Extensions = resource.Extension.Select(o=>DataTypeConverter.ToActExtension(o, retVal)).OfType<ActExtension>().ToList();
            retVal.Identifiers = resource.Identifier.Select(DataTypeConverter.ToActIdentifier).ToList();
            retVal.Notes = DataTypeConverter.ToNote<ActNote>(resource.Text);

            retVal.MoodConceptKey = MoodConceptKeys.Eventoccurrence;

            retVal.Key = Guid.TryParse(resource.Id, out var id) ? id : Guid.NewGuid();

            if (null == retVal.Relationships)
            {
                retVal.Relationships = new List<ActRelationship>();
            }

            if (null == retVal.Participations)
            {
                retVal.Participations = new List<ActParticipation>();
            }

            // Observation
            var status = resource.Status;

            //status concept key

            switch (status)
            {
                case ObservationStatus.Preliminary:
                    retVal.StatusConceptKey = StatusKeys.Active;
                    break;
                case ObservationStatus.Cancelled:
                    retVal.StatusConceptKey = StatusKeys.Cancelled;
                    break;
                case ObservationStatus.EnteredInError:
                    retVal.StatusConceptKey = StatusKeys.Nullified;
                    break;
                case ObservationStatus.Final:
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;
                case ObservationStatus.Amended:
                case ObservationStatus.Corrected:
                    throw new NotSupportedException(this.m_localizationService.GetString("error.messaging.fhir.observationStatus"));
                case ObservationStatus.Unknown:
                    retVal.StatusConceptKey = StatusKeys.Obsolete;
                    break;

            }

            //Effective 

            switch (resource.Effective)
            {
                case Period period:
                    retVal.StartTime = DataTypeConverter.ToDateTimeOffset(period.Start);
                    retVal.StopTime = DataTypeConverter.ToDateTimeOffset(period.End);
                    break;

                case FhirDateTime fhirDateTime:
                    retVal.ActTime = DataTypeConverter.ToDateTimeOffset(fhirDateTime) ?? DateTimeOffset.MinValue;
                    break;
            }


            retVal.TypeConcept = DataTypeConverter.ToConcept(resource.Code);


            //issued
            if (resource.Issued.HasValue)
            {
                retVal.CreationTime = (DateTimeOffset)resource.Issued;
            }

            //interpretation
            if (resource.Interpretation.Any())
            {
                retVal.InterpretationConcept = resource.Interpretation.Select(DataTypeConverter.ToConcept).Where(c => null != c).FirstOrDefault();
            }


            //subject
            retVal.Participations = new List<ActParticipation>();
            if (resource.Subject != null)
            {


                // if the subject is a UUID then add the record target key
                // otherwise attempt to resolve the reference
                var subject = resource.Subject.Reference.StartsWith(UUID_PREFIX) ?
                    new ActParticipation(ActParticipationKeys.RecordTarget, Guid.Parse(resource.Subject.Reference.Substring(UUID_PREFIX.Length))) :
                    new ActParticipation(ActParticipationKeys.RecordTarget, DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource));

                retVal.Participations.Add(subject);
                //else 
                //{
                //    this.m_tracer.TraceError("Only UUID references are supported");
                //    throw new NotSupportedException(this.m_localizationService.FormatString("error.type.NotSupportedException.paramOnlySupported", new 
                //    { 
                //        param = "UUID"
                //    }));
                //}

                var patientkey = subject.PlayerEntityKey.Value;

                if (resource.Category?.Count > 0)
                {
                    var patientlists = m_ActRepository.Find(act => act.ClassConceptKey == ActClassKeys.List
                            && act.Participations.Any(p => p.ParticipationRoleKey == ActParticipationKeys.RecordTarget && p.PlayerEntityKey == patientkey));

                    foreach (var category in resource.Category)
                    {
                        if (null == category)
                        {
                            continue;
                        }

                        var listtypeconcept = DataTypeConverter.ToConcept(category);

                        if (null == listtypeconcept) //List type is unknown.
                        {
                            throw new KeyNotFoundException(m_localizationService.GetString("error.messaging.fhir.observationResource.categoryNotFound", new
                            {
                                code = category.GetCoding()?.Code,
                                system = category.GetCoding()?.System
                            }));
                        }

                        var categorylist = patientlists.Where(l => l.TypeConceptKey == listtypeconcept.Key).FirstOrDefault();

                        if (null == categorylist)
                        {
                            categorylist = new Act
                            {
                                ClassConceptKey = ActClassKeys.List,
                                TypeConceptKey = listtypeconcept.Key,
                                MoodConceptKey = MoodConceptKeys.Eventoccurrence,
                                CreationTime = DateTimeOffset.Now,
                                ActTime = DateTimeOffset.Now
                            };

                            categorylist.Participations = new List<ActParticipation>()
                            {
                                new ActParticipation { ParticipationRoleKey = ActParticipationKeys.RecordTarget, PlayerEntityKey = patientkey }
                            };

                            categorylist = m_ActRepository.Insert(categorylist);
                        }

                        //categorylist.Relationships.Add(new ActRelationship
                        //{
                        //    TargetAct = retVal,
                        //    RelationshipTypeKey = ActRelationshipTypeKeys.HasComponent
                        //});

                        retVal.Relationships.Add(new ActRelationship { SourceEntity = categorylist, TargetAct = retVal, RelationshipTypeKey = ActRelationshipTypeKeys.HasComponent });
                    }
                }
            }



            //performer
            if (resource.Performer.Any())
            {
                foreach (var res in resource.Performer)
                {
                    retVal.Participations.Add(res.Reference.StartsWith(UUID_PREFIX) ?
                        new ActParticipation(ActParticipationKeys.Performer, Guid.Parse(res.Reference.Substring(UUID_PREFIX.Length))) :
                        new ActParticipation(ActParticipationKeys.Performer, DataTypeConverter.ResolveEntity<Provider>(res, resource)));

                    //if (res.Reference.StartsWith("urn:uuid:"))
                    //{
                    //    retVal.Participations.Add(new ActParticipation(ActParticipationKey.Performer, Guid.Parse(res.Reference.Substring(9))));
                    //}
                    //else 
                    //{
                    //    this.m_tracer.TraceError("Only UUID references are supported");
                    //    throw new NotSupportedException(this.m_localizationService.FormatString("error.type.NotSupportedException.paramOnlySupported", new 
                    //    { 
                    //        param = "UUID"
                    //    }));
                    //}
                }
            }

            // to bypass constraint at function 'CK_IS_CD_SET_MEM'
            retVal.MoodConceptKey = ActMoodKeys.Eventoccurrence;

            return retVal;
        }

        ///<inheritdoc />
        protected override IQueryResultSet<Core.Model.Acts.Observation> QueryInternal(Expression<Func<Core.Model.Acts.Observation, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
        {
            if (fhirParameters["value-concept"] != null)
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<CodedObservation>(hdsiParameters);
                return this.QueryInternalEx<CodedObservation>(predicate, fhirParameters, hdsiParameters).AsResultSet<Core.Model.Acts.Observation>();
            }
            else if (fhirParameters["value-quantity"] != null)
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<QuantityObservation>(hdsiParameters);
                return this.QueryInternalEx<QuantityObservation>(predicate, fhirParameters, hdsiParameters).AsResultSet<Core.Model.Acts.Observation>();
            }
            else
            {
                return base.QueryInternal(query, fhirParameters, hdsiParameters);
            }
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Acts.Observation resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }


    }
}
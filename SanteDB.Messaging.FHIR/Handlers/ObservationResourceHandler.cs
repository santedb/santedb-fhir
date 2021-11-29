/*
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
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

        /// <summary>
        /// Create new resource handler
        /// </summary>
        public ObservationResourceHandler(IRepositoryService<Core.Model.Acts.Observation> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Acts.Observation resource, IEnumerable<IncludeInstruction> includePaths)
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
                {Code = o});
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
            retVal.Identifier = model.LoadCollection<ActIdentifier>(nameof(Act.Identifiers)).Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

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
                    if (model.LoadCollection<ActRelationship>(nameof(Act.Relationships)).Any(o => o.RelationshipTypeKey == ActRelationshipTypeKeys.Replaces)) // was amended
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
                retVal.Effective = DataTypeConverter.ToFhirDate(model.ActTime.DateTime);
            }

            retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.LoadProperty<Concept>(nameof(Act.TypeConcept)));

            // RCT
            var rct = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKey.RecordTarget);

            if (rct != null)
            {
                retVal.Subject = DataTypeConverter.CreateNonVersionedReference<Patient>(rct.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)));
            }

            // Performer
            retVal.Performer = model.Participations.Where(o => o.ParticipationRoleKey == ActParticipationKey.Performer)
                .Select(o => DataTypeConverter.CreateNonVersionedReference<Practitioner>(o.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity))))
                .ToList();

            retVal.Issued = model.CreationTime;

            // Value
            switch (model.ValueType)
            {
                case "CD":
                    retVal.Value = DataTypeConverter.ToFhirCodeableConcept((model as CodedObservation).Value);
                    break;
                case "PQ":
                    var qty = model as QuantityObservation;
                    retVal.Value = DataTypeConverter.ToQuantity(qty.Value, qty.LoadProperty<Concept>(nameof(QuantityObservation.UnitOfMeasure)));
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
                    DataTypeConverter.ToFhirCodeableConcept(model.LoadProperty<Concept>(nameof(QuantityObservation.InterpretationConcept)))
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
                        Value = DataTypeConverter.ToConcept(codeableConcept)
                    };
                    break;
                case Quantity quantity:
                    retVal = new QuantityObservation
                    {
                        ValueType = "PQ",
                        Value = quantity.Value.Value,
                        UnitOfMeasure = DataTypeConverter.ToConcept(quantity.Unit, "http://hl7.org/fhir/sid/ucum")
                    };
                    break;
                case FhirString fhirString:
                    retVal = new TextObservation
                    {
                        ValueType = "ST",
                        Value = fhirString.Value
                    };
                    break;
                default:
                    retVal = new Core.Model.Acts.Observation();
                    break;
            }

            retVal.Extensions = resource.Extension.Select(DataTypeConverter.ToActExtension).ToList();
            retVal.Identifiers = resource.Identifier.Select(DataTypeConverter.ToActIdentifier).ToList();

            retVal.Key = Guid.TryParse(resource.Id, out var id) ? id : Guid.NewGuid();

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
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;
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
                retVal.CreationTime = (DateTimeOffset) resource.Issued;
            }

            //interpretation 
            if (resource.Interpretation.Any())
            {
                retVal.InterpretationConcept = DataTypeConverter.ToConcept(resource.Interpretation.First());
            }


            //subject

            if (resource.Subject != null)
            {
                // if the subject is a UUID then add the record target key
                // otherwise attempt to resolve the reference
                retVal.Participations.Add(resource.Subject.Reference.StartsWith("urn:uuid:") ? 
                    new ActParticipation(ActParticipationKey.RecordTarget, Guid.Parse(resource.Subject.Reference.Substring(9))) : 
                    new ActParticipation(ActParticipationKey.RecordTarget, DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource)));
                //else 
                //{
                //    this.m_tracer.TraceError("Only UUID references are supported");
                //    throw new NotSupportedException(this.m_localizationService.FormatString("error.type.NotSupportedException.paramOnlySupported", new 
                //    { 
                //        param = "UUID"
                //    }));
                //}
            }

            //performer
            if (resource.Performer.Any())
            {
                foreach (var res in resource.Performer)
                {
                    retVal.Participations.Add(res.Reference.StartsWith("urn:uuid:") ?
                        new ActParticipation(ActParticipationKey.Performer, Guid.Parse(res.Reference.Substring(9))) :
                        new ActParticipation(ActParticipationKey.Performer, DataTypeConverter.ResolveEntity<Provider>(res, resource)));

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

        /// <summary>
        /// Parameters
        /// </summary>
        public override Bundle Query(NameValueCollection parameters)
        {
            if (parameters == null)
            {
                this.m_tracer.TraceError(nameof(parameters));
                throw new ArgumentNullException(nameof(parameters), this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            Core.Model.Query.NameValueCollection hdsiQuery = null;
            var query = QueryRewriter.RewriteFhirQuery(typeof(Observation), typeof(Core.Model.Acts.Observation), parameters, out hdsiQuery);

            // Do the query
            var totalResults = 0;

            IEnumerable<Core.Model.Acts.Observation> hdsiResults = null;

            if (parameters["value-concept"] != null)
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<CodedObservation>(hdsiQuery);
                hdsiResults = this.QueryEx(predicate, query.QueryId, query.Start, query.Quantity, out totalResults).OfType<Core.Model.Acts.Observation>();
            }
            else if (parameters["value-quantity"] != null)
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<QuantityObservation>(hdsiQuery);
                hdsiResults = this.QueryEx(predicate, query.QueryId, query.Start, query.Quantity, out totalResults).OfType<Core.Model.Acts.Observation>();
            }
            else
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<Core.Model.Acts.Observation>(hdsiQuery);
                hdsiResults = this.Query(predicate, query.QueryId, query.Start, query.Quantity, out totalResults);
            }

            // Return FHIR query result
            var retVal = new FhirQueryResult("Observation")
            {
                Results = hdsiResults.Select(this.MapToFhir).Select(o => new Bundle.EntryComponent
                {
                    Resource = o,
                    Search = new Bundle.SearchComponent
                        {Mode = Bundle.SearchEntryMode.Match}
                }).ToList(),
                Query = query,
                TotalResults = totalResults
            };

            base.ProcessIncludes(hdsiResults, parameters, retVal);

            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.SearchType, this.ResourceType, MessageUtil.CreateBundle(retVal, Bundle.BundleType.Searchset)) as Bundle;
        }
    }
}
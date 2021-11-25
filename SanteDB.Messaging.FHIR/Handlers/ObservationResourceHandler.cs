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
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
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
using Observation = SanteDB.Core.Model.Acts.Observation;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Observation handler
    /// </summary>
    public class ObservationResourceHandler : RepositoryResourceHandlerBase<Hl7.Fhir.Model.Observation, Core.Model.Acts.Observation>
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ObservationResourceHandler));

        /// <summary>
		/// Create new resource handler
		/// </summary>
		public ObservationResourceHandler(IRepositoryService<Core.Model.Acts.Observation> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {
        }

        /// <summary>
        /// Map to FHIR
        /// </summary>
        protected override Hl7.Fhir.Model.Observation MapToFhir(Core.Model.Acts.Observation model)
        {
            var retVal = DataTypeConverter.CreateResource<Hl7.Fhir.Model.Observation>(model);
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
                        retVal.Status = ObservationStatus.Amended;
                    else
                        retVal.Status = ObservationStatus.Final;
                    break;

                case StatusKeyStrings.Obsolete:
                    retVal.Status = ObservationStatus.Unknown;
                    break;
            }

            if (model.StartTime.HasValue || model.StopTime.HasValue)
                retVal.Effective = DataTypeConverter.ToPeriod(model.StartTime, model.StopTime);
            else
                retVal.Effective = DataTypeConverter.ToFhirDate(model.ActTime.DateTime);

            retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.LoadProperty<Concept>(nameof(Act.TypeConcept)));

            // RCT
            var rct = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKey.RecordTarget);
            if (rct != null)
            {
                retVal.Subject = DataTypeConverter.CreateNonVersionedReference<Patient>(rct.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)));
            }

            // Performer
            var prf = model.Participations.Where(o => o.ParticipationRoleKey == ActParticipationKey.Performer);
            if (prf != null)
            {
                retVal.Performer = prf.Select(o => DataTypeConverter.CreateNonVersionedReference<Practitioner>(o.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity)))).ToList();
            }

            retVal.Issued = model.CreationTime;

            // Value
            switch (model.ValueType)
            {
                case "CD":
                    retVal.Value = DataTypeConverter.ToFhirCodeableConcept((model as Core.Model.Acts.CodedObservation).Value);
                    break;

                case "PQ":
                    var qty = model as Core.Model.Acts.QuantityObservation;
                    retVal.Value = DataTypeConverter.ToQuantity(qty.Value, qty.LoadProperty<Concept>(nameof(QuantityObservation.UnitOfMeasure)));
                    break;

                case "ED":
                case "ST":
                    retVal.Value = new FhirString((model as Core.Model.Acts.TextObservation).Value);
                    break;
            }

            if (model.InterpretationConceptKey.HasValue)
                retVal.Interpretation = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConcept(model.LoadProperty<Concept>(nameof(QuantityObservation.InterpretationConcept))) };

            return retVal;
        }

        /// <summary>
        /// Map to model
        /// </summary>
        protected override Core.Model.Acts.Observation MapToModel(Hl7.Fhir.Model.Observation resource)
        {
            //value type and value
            Observation retVal;
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
                    retVal = new QuantityObservation()
                    {
                        ValueType = "PQ",
                        Value = quantity.Value.Value,
                        UnitOfMeasure = DataTypeConverter.ToConcept(quantity.Unit, "http://hl7.org/fhir/sid/ucum")
                    };
                    break;

                case FhirString fhirString:
                    retVal = new TextObservation()
                    {
                        ValueType = "ST",
                        Value = fhirString.Value
                    };
                    break;

                default:
                    retVal = new Observation();
                    break;
            }
            // Observation
            var status = resource.Status.Value;

            retVal.Extensions = resource.Extension.Select(DataTypeConverter.ToActExtension).OfType<ActExtension>()
                .ToList();
            retVal.Identifiers = resource.Identifier.Select(o => DataTypeConverter.ToActIdentifier(o)).ToList();
            //retVal.Key = Guid.NewGuid();

            //status concept key
            switch (status)
            {
                case (ObservationStatus.Preliminary):
                    retVal.StatusConceptKey = StatusKeys.Active;
                    break;

                case (ObservationStatus.Cancelled):
                    retVal.StatusConceptKey = StatusKeys.Cancelled;
                    break;

                case (ObservationStatus.EnteredInError):
                    retVal.StatusConceptKey = StatusKeys.Nullified;
                    break;

                case (ObservationStatus.Final):
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;

                case (ObservationStatus.Amended):
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;

                case (ObservationStatus.Unknown):
                    retVal.StatusConceptKey = StatusKeys.Obsolete;
                    break;
            }

            //Effective

            switch (resource.Effective)
            {
                case Period period:
                    retVal.StartTime = period.StartElement.ToDateTimeOffset();
                    retVal.StopTime = period.EndElement.ToDateTimeOffset();
                    break;

                case FhirDateTime fhirDateTime:
                    retVal.ActTime = fhirDateTime.ToDateTimeOffset();
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
                retVal.InterpretationConcept = DataTypeConverter.ToConcept(resource.Interpretation.First());
            }

            //subject

            if (resource.Subject != null)
            {
                // Is the subject a uuid
                if (resource.Subject.Reference.StartsWith("urn:uuid:"))
                    retVal.Participations.Add(new ActParticipation(ActParticipationKey.RecordTarget, Guid.Parse(resource.Subject.Reference.Substring(9))));
                else
                {
                    this.m_tracer.TraceError("Only UUID references are supported");
                    throw new NotSupportedException(this.m_localizationService.GetString("error.type.NotSupportedException.paramOnlySupported", new
                    {
                        param = "UUID"
                    }));
                }
            }

            //performer
            if (resource.Performer.Any())
            {
                foreach (var res in resource.Performer)
                {
                    if (res.Reference.StartsWith("urn:uuid:"))
                    {
                        retVal.Participations.Add(new ActParticipation(ActParticipationKey.Performer, Guid.Parse(res.Reference.Substring(9))));
                    }
                    else
                    {
                        this.m_tracer.TraceError("Only UUID references are supported");
                        throw new NotSupportedException(this.m_localizationService.GetString("error.type.NotSupportedException.paramOnlySupported", new
                        {
                            param = "UUID"
                        }));
                    }
                }
            }

            // to bypass constraint at function 'CK_IS_CD_SET_MEM'

            retVal.MoodConceptKey = ActMoodKeys.Eventoccurrence;

            return retVal;
        }

        /// <summary>
        /// Parameters
        /// </summary>
        public override Bundle Query(System.Collections.Specialized.NameValueCollection parameters)
        {
            if (parameters == null)
            {
                this.m_tracer.TraceError(nameof(parameters));
                throw new ArgumentNullException(nameof(parameters), this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            Core.Model.Query.NameValueCollection hdsiQuery = null;
            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(Hl7.Fhir.Model.Observation), typeof(Core.Model.Acts.Observation), parameters, out hdsiQuery);

            // Do the query
            IQueryResultSet hdsiResults = null;

            if (parameters["value-concept"] != null)
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<Core.Model.Acts.CodedObservation>(hdsiQuery);
                hdsiResults = this.QueryEx<Core.Model.Acts.CodedObservation>(predicate);
            }
            else if (parameters["value-quantity"] != null)
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<Core.Model.Acts.QuantityObservation>(hdsiQuery);
                hdsiResults = this.QueryEx<Core.Model.Acts.QuantityObservation>(predicate);
            }
            else
            {
                var predicate = QueryExpressionParser.BuildLinqExpression<Core.Model.Acts.Observation>(hdsiQuery);
                hdsiResults = this.Query(predicate);
            }

            // TODO: Sorting
            var results = query.ApplyCommonQueryControls(hdsiResults, out int totalResults).OfType<SanteDB.Core.Model.Acts.Observation>();
            var restOperationContext = RestOperationContext.Current;

            // Return FHIR query result
            var retVal = new FhirQueryResult("Observation")
            {
                Results = results.Select(this.MapToFhir).Select(o => new Bundle.EntryComponent()
                {
                    Resource = o,
                    Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Match },
                }).ToList(),
                Query = query,
                TotalResults = totalResults
            };

            base.ProcessIncludes(results, parameters, retVal);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.SearchType, this.ResourceType, MessageUtil.CreateBundle(retVal, Bundle.BundleType.Searchset)) as Bundle;
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

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Acts.Observation resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Acts.Observation resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
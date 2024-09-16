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
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
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

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents an immunization recommendation handler.
    /// </summary>
    public class ImmunizationRecommendationResourceHandler : ResourceHandlerBase<ImmunizationRecommendation, SubstanceAdministration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImmunizationRecommendationResourceHandler"/> class.
        /// </summary>
        public ImmunizationRecommendationResourceHandler(ILocalizationService localizationService) : base(localizationService)
        {
        }

        /// <inheritdoc/>

        protected override SubstanceAdministration Create(SubstanceAdministration modelInstance, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        protected override SubstanceAdministration Delete(Guid modelId)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        protected override ImmunizationRecommendation MapToFhir(SubstanceAdministration model)
        {
            ImmunizationRecommendation retVal = new ImmunizationRecommendation();

            retVal.Id = model.Key.ToString();
            retVal.DateElement = new FhirDateTime(DateTimeOffset.Now);
            retVal.Identifier = model.Identifiers.Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

            var rct = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget)?.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity));
            if (rct != null)
            {
                retVal.Patient = DataTypeConverter.CreateNonVersionedReference<Patient>(rct);
            }

            var mat = model.Participations.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Product).PlayerEntity;

            // Recommend
            string status = (model.StopTime ?? model.ActTime) < DateTimeOffset.Now ? "overdue" : "due";
            var recommendation = new ImmunizationRecommendation.RecommendationComponent()
            {
                DoseNumber = new PositiveInt(model.SequenceId),
                VaccineCode = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConcept(mat?.TypeConceptKey) },
                ForecastStatus = new CodeableConcept("http://hl7.org/fhir/conceptset/immunization-recommendation-status", status),
                DateCriterion = new List<ImmunizationRecommendation.DateCriterionComponent>()
                {
                    new ImmunizationRecommendation.DateCriterionComponent()
                    {
                        Code = new CodeableConcept("http://hl7.org/fhir/conceptset/immunization-recommendation-date-criterion", "recommended"),
                        ValueElement = new FhirDateTime(model.ActTime.GetValueOrDefault())
                    }
                }
            };
            if (model.StartTime.HasValue)
            {
                recommendation.DateCriterion.Add(new ImmunizationRecommendation.DateCriterionComponent()
                {
                    Code = new CodeableConcept("http://hl7.org/fhir/conceptset/immunization-recommendation-date-criterion", "earliest"),
                    ValueElement = new FhirDateTime(model.StartTime.Value)
                });
            }

            if (model.StopTime.HasValue)
            {
                recommendation.DateCriterion.Add(new ImmunizationRecommendation.DateCriterionComponent()
                {
                    Code = new CodeableConcept("http://hl7.org/fhir/conceptset/immunization-recommendation-date-criterion", "overdue"),
                    ValueElement = new FhirDateTime(model.StopTime.Value)
                });
            }

            retVal.Recommendation = new List<ImmunizationRecommendation.RecommendationComponent>() { recommendation };
            return retVal;
        }

        /// <inheritdoc/>
        protected override SubstanceAdministration MapToModel(ImmunizationRecommendation resource)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        //protected override IQueryResultSet<SubstanceAdministration> QueryInternal(Expression<Func<SubstanceAdministration, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
        //{
        //    // TODO: Hook this up to the forecaster
        //   // var obsoletionReference = System.Linq.Expressions.Expression.MakeBinary(ExpressionType.NotEqual, System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(BaseEntityData.ObsoletionTime))), System.Linq.Expressions.Expression.Constant(null));
        //    //query = System.Linq.Expressions.Expression.Lambda<Func<SubstanceAdministration, bool>>(System.Linq.Expressions.Expression.AndAlso(obsoletionReference, query), query.Parameters);
        //    //return this.repository.Find<SubstanceAdministration>(query, offset, count, out totalResults);
        //    // TODO: Call care planner or call the stored cp
        //    return base.QueryInternal(;
        //}

        /// <inheritdoc/>
        protected override SubstanceAdministration Read(Guid id, Guid versionId)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <inheritdoc/>
        protected override SubstanceAdministration Update(SubstanceAdministration model, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION);
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetReverseIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION);
        }

        /// <inheritdoc/>
        protected override IQueryResultSet<SubstanceAdministration> QueryInternal(Expression<Func<SubstanceAdministration, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
        {
            throw new NotImplementedException(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION);
        }
    }
}
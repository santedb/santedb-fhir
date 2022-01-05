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
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Creates the specified model instance.
        /// </summary>
        /// <param name="modelInstance">The model instance.</param>
        /// <param name="issues">The issues.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>Returns the created model.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected override SubstanceAdministration Create(SubstanceAdministration modelInstance, TransactionMode mode)
        {
            throw new NotSupportedException(m_localizationService.GetString("error.type.NotSupportedException"));
        }

        /// <summary>
        /// Deletes the specified model identifier.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="details">The details.</param>
        /// <returns>Returns the deleted model.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected override SubstanceAdministration Delete(Guid modelId)
        {
            throw new NotSupportedException(m_localizationService.GetString("error.type.NotSupportedException"));
        }

        /// <summary>
        /// Maps the outbound resource to FHIR.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>Returns the mapped FHIR resource.</returns>
        protected override ImmunizationRecommendation MapToFhir(SubstanceAdministration model)
        {
            ImmunizationRecommendation retVal = new ImmunizationRecommendation();

            retVal.Id = model.Key.ToString();
            retVal.DateElement = new FhirDateTime(DateTimeOffset.Now);
            retVal.Identifier = model.Identifiers.Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

            var rct = model.LoadCollection<ActParticipation>(nameof(Act.Participations)).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget)?.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity));
            if (rct != null)
                retVal.Patient = DataTypeConverter.CreateNonVersionedReference<Patient>(rct);

            var mat = model.Participations.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Product).PlayerEntity;

            // Recommend
            string status = (model.StopTime ?? model.ActTime) < DateTimeOffset.Now ? "overdue" : "due";
            var recommendation = new ImmunizationRecommendation.RecommendationComponent()
            {
                DoseNumber = new PositiveInt(model.SequenceId),
                VaccineCode = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConcept(mat?.TypeConcept) },
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
                recommendation.DateCriterion.Add(new ImmunizationRecommendation.DateCriterionComponent()
                {
                    Code = new CodeableConcept("http://hl7.org/fhir/conceptset/immunization-recommendation-date-criterion", "earliest"),
                    ValueElement = new FhirDateTime(model.StartTime.Value)
                });
            if (model.StopTime.HasValue)
                recommendation.DateCriterion.Add(new ImmunizationRecommendation.DateCriterionComponent()
                {
                    Code = new CodeableConcept("http://hl7.org/fhir/conceptset/immunization-recommendation-date-criterion", "overdue"),
                    ValueElement = new FhirDateTime(model.StopTime.Value)
                });

            retVal.Recommendation = new List<ImmunizationRecommendation.RecommendationComponent>() { recommendation };
            return retVal;
        }

        /// <summary>
        /// Maps a FHIR resource to a model instance.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <returns>Returns the mapped model.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected override SubstanceAdministration MapToModel(ImmunizationRecommendation resource)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Query for immunization recommendations.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        protected override IQueryResultSet<SubstanceAdministration> Query(Expression<Func<SubstanceAdministration, bool>> query)
        {
            // TODO: Hook this up to the forecaster
            var obsoletionReference = System.Linq.Expressions.Expression.MakeBinary(ExpressionType.NotEqual, System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(SubstanceAdministration).GetProperty(nameof(BaseEntityData.ObsoletionTime))), System.Linq.Expressions.Expression.Constant(null));
            query = System.Linq.Expressions.Expression.Lambda<Func<SubstanceAdministration, bool>>(System.Linq.Expressions.Expression.AndAlso(obsoletionReference, query), query.Parameters);
            //return this.repository.Find<SubstanceAdministration>(query, offset, count, out totalResults);
            // TODO: Call care planner or call the stored cp
            return null;
        }

        /// <summary>
        /// Reads the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="details">The details.</param>
        /// <returns>Returns the model which matches the given id.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected override SubstanceAdministration Read(Guid id, Guid versionId)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Updates the specified model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="details">The details.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>Returns the updated model.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected override SubstanceAdministration Update(SubstanceAdministration model, TransactionMode mode)
        {
            throw new NotSupportedException(m_localizationService.GetString("error.type.NotSupportedException"));
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType
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
        /// Get reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(SubstanceAdministration resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
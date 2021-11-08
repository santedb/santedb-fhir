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
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using SanteDB.Core;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Allergy / intolerance resource handler
    /// </summary>
    public class AllergyIntoleranceResourceHandler : RepositoryResourceHandlerBase<AllergyIntolerance, CodedObservation>
    {

        // Applicable type concepts
        private List<Guid> m_typeConcepts;

        /// <summary>
        /// Type concepts
        /// </summary>
        public AllergyIntoleranceResourceHandler(IRepositoryService<CodedObservation> repo, IRepositoryService<Concept> conceptRepo, ILocalizationService localizationService) : base(repo, localizationService)
        {

            this.m_typeConcepts = conceptRepo.Find(o => o.ConceptSets.Any(cs => cs.Mnemonic == "AllergyIntoleranceCode")).Select(o => o.Key.Value).ToList();
        }

        /// <summary>
        /// Can map this object
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public override bool CanMapObject(object instance) => instance is AllergyIntolerance ||
            instance is CodedObservation cobs && this.m_typeConcepts.Contains(cobs.TypeConceptKey.GetValueOrDefault());

        /// <summary>
        /// Map coded allergy intolerance resource to FHIR
        /// </summary>
		protected override AllergyIntolerance MapToFhir(CodedObservation model)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Map allergy intolerance from FHIR to a coded observation
        /// </summary>
		protected override CodedObservation MapToModel(AllergyIntolerance resource)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Query which filters only allergies and intolerances
        /// </summary>
        protected override IEnumerable<CodedObservation> Query(Expression<Func<CodedObservation, bool>> query, Guid queryId, int offset, int count, out int totalResults)
        {
            var anyRef = base.CreateConceptSetFilter(ConceptSetKeys.AllergyIntoleranceTypes, query.Parameters[0]);
            query = System.Linq.Expressions.Expression.Lambda<Func<CodedObservation, bool>>(System.Linq.Expressions.Expression.AndAlso(query.Body, anyRef), query.Parameters);
            return base.Query(query, queryId, offset, count, out totalResults);
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(CodedObservation resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get reverse included resources
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(CodedObservation resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
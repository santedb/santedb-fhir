/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-10-29
 */
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Resource handler for acts base
    /// </summary>
    public abstract class RepositoryResourceHandlerBase<TFhirResource, TModel> : ResourceHandlerBase<TFhirResource, TModel>
        where TFhirResource : Resource, new()
        where TModel : IdentifiedData, new()
    {
        // Repository service model
        protected IRepositoryService<TModel> m_repository;

        private IRepositoryService<Core.Model.Collection.Bundle> m_bundleRepository;

        /// <summary>
        /// CTOR
        /// </summary>
        public RepositoryResourceHandlerBase(IRepositoryService<TModel> repository, ILocalizationService localizationService) : base(localizationService)
        {
            this.m_repository = repository;
            this.m_bundleRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Collection.Bundle>>();
        }

        /// <summary>
        /// Create the object
        /// </summary>
        protected override TModel Create(TModel modelInstance, TransactionMode mode)
        {
            if (modelInstance is ITargetedAssociation targetedAssociation)
            {
                // We may be creating multiple objects so let's do that
                var transaction = new Core.Model.Collection.Bundle()
                {
                    Item = new List<IdentifiedData>() { modelInstance }
                };
                if (targetedAssociation.TargetEntity != null)
                {
                    transaction.Item.Insert(0, targetedAssociation.TargetEntity as IdentifiedData);
                }
                if (targetedAssociation.SourceEntity != null)
                {
                    transaction.Item.Insert(0, targetedAssociation.SourceEntity as IdentifiedData);
                }

                // Create
                this.m_bundleRepository.Insert(transaction);
                return modelInstance;
            }
            else
            {
                return this.m_repository.Insert(modelInstance);
            }
        }

        /// <summary>
        /// Create concept set filter based on act type
        /// </summary>
        protected System.Linq.Expressions.Expression CreateConceptSetFilter(Guid conceptSetKey, ParameterExpression queryParameter)
        {
            var conceptSetRef = System.Linq.Expressions.Expression.MakeMemberAccess(System.Linq.Expressions.Expression.MakeMemberAccess(queryParameter, typeof(Act).GetProperty(nameof(Act.TypeConcept))), typeof(Concept).GetProperty(nameof(Concept.ConceptSets)));
            var lParam = System.Linq.Expressions.Expression.Parameter(typeof(ConceptSet));
            var conceptSetFilter = System.Linq.Expressions.Expression.MakeBinary(ExpressionType.Equal, System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(lParam, typeof(ConceptSet).GetProperty(nameof(ConceptSet.Key))), typeof(Guid)), System.Linq.Expressions.Expression.Constant(conceptSetKey));
            return System.Linq.Expressions.Expression.Call((MethodInfo)typeof(Enumerable).GetGenericMethod("Any", new Type[] { typeof(ConceptSet) }, new Type[] { typeof(IEnumerable<ConceptSet>), typeof(Func<ConceptSet, bool>) }), conceptSetRef, System.Linq.Expressions.Expression.Lambda(conceptSetFilter, lParam));
        }

        /// <summary>
        /// Perform a delete operation
        /// </summary>
        protected override TModel Delete(Guid modelId)
        {
            return this.m_repository.Obsolete(modelId);
        }

        /// <summary>
        /// Query for patients.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="issues">The issues.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="totalResults">The total results.</param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        protected override IEnumerable<TModel> Query(Expression<Func<TModel, bool>> query, Guid queryId, int offset, int count, out int totalResults)
        {
            return this.QueryEx<TModel>(query, queryId, offset, count, out totalResults);
        }

        /// <summary>
        /// Represents the predicate model
        /// </summary>
        protected virtual IEnumerable<TPredicate> QueryEx<TPredicate>(Expression<Func<TPredicate, bool>> query, Guid queryId, int offset, int count, out int totalResults)
            where TPredicate : IdentifiedData
        {
            if (typeof(TPredicate).GetProperty(nameof(Entity.StatusConceptKey)) != null)
            {
                foreach (var itm in StatusKeys.InactiveStates)
                {
                    var obsoletionReference = System.Linq.Expressions.Expression.MakeBinary(ExpressionType.NotEqual, System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(TPredicate).GetProperty(nameof(Entity.StatusConceptKey))), typeof(Guid)), System.Linq.Expressions.Expression.Constant(itm));
                    query = System.Linq.Expressions.Expression.Lambda<Func<TPredicate, bool>>(System.Linq.Expressions.Expression.AndAlso(obsoletionReference, query.Body), query.Parameters);
                }
            }

            var repo = ApplicationServiceContext.Current.GetService<IRepositoryService<TPredicate>>();
            if (queryId == Guid.Empty)
                return repo.Find(query, offset, count, out totalResults);
            else
                return (repo as IPersistableQueryRepositoryService<TPredicate>).Find(query, offset, count, out totalResults, queryId);
        }

        /// <summary>
        /// Perform a read operation
        /// </summary>
        protected override TModel Read(Guid id, Guid versionId)
        {
            return this.m_repository.Get(id, versionId);
        }

        /// <summary>
        /// Perform an update operation
        /// </summary>
        protected override TModel Update(TModel modelInstance, TransactionMode mode)
        {
            if (modelInstance is ITargetedAssociation targetedAssociation)
            {
                // We may be creating multiple objects so let's do that
                var transaction = new Core.Model.Collection.Bundle()
                {
                    Item = new List<IdentifiedData>() { modelInstance }
                };
                if (targetedAssociation.TargetEntity != null)
                {
                    transaction.Item.Insert(0, targetedAssociation.TargetEntity as IdentifiedData);
                }
                if (targetedAssociation.SourceEntity != null)
                {
                    transaction.Item.Insert(0, targetedAssociation.SourceEntity as IdentifiedData);
                }

                // Create
                this.m_bundleRepository.Save(transaction);
                return modelInstance;
            }
            else
            {
                return this.m_repository.Save(modelInstance);
            }
        }
    }
}
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
 * User: fyfej
 * Date: 2023-6-21
 */
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// Extension methods for various classes that support the FHIR interface.
    /// </summary>
    internal static class FhirExtensions
    {
        public static IQueryResultSet<ActRelationship> QueryRelationships(this IDataPersistenceService<ActRelationship> persistenceService, Guid sourceActKey, Guid relationshipTypeKey, Guid? targetTypeConceptKey = null)
        {
            if (targetTypeConceptKey != null)
            {
                return persistenceService.Query(actr => actr.SourceEntityKey == sourceActKey && actr.RelationshipTypeKey == relationshipTypeKey && actr.TargetAct.TypeConceptKey == targetTypeConceptKey, AuthenticationContext.Current.Principal);
            }
            else
            {
                return persistenceService.Query(actr => actr.SourceEntityKey == sourceActKey && actr.RelationshipTypeKey == relationshipTypeKey, AuthenticationContext.Current.Principal);
            }
        }

        public static TAct GetFirstOrDefaultRelatedAct<TAct>(this IDataPersistenceService<ActRelationship> persistenceService, Guid sourceActKey, Guid relationshipTypeKey, Guid? targetTypeConceptKey = null) where TAct : Act
        {
            return QueryRelationships(persistenceService, sourceActKey, relationshipTypeKey, targetTypeConceptKey).FirstOrDefault() as TAct;
        }

        public static IEnumerable<TAct> GetRelatedActs<TAct>(this IDataPersistenceService<ActRelationship> persistenceService, Guid sourceActKey, Guid relationshipTypeKey, Guid? targetTypeConceptKey) where TAct : Act
        {
            var relationships = QueryRelationships(persistenceService, sourceActKey, relationshipTypeKey, targetTypeConceptKey)?.ToList();

            if (null == relationships)
            {
                yield break;
            }

            foreach (var relationship in relationships)
            {
                yield return (TAct)relationship.TargetAct;
            }
        }

        public static IEnumerable<TAct> GetRelatedActs<TAct>(this (IDataPersistenceService<ActRelationship> persistenceService, Act sourceAct) source, Guid relationshipTypeKey, Guid? targetTypeConceptKey) where TAct : Act
        {
            var relationships = QueryRelationships(source.persistenceService, source.sourceAct.Key.Value, relationshipTypeKey, targetTypeConceptKey)?.ToList();

            if (null == relationships)
            {
                yield break;
            }

            foreach (var relationship in relationships)
            {
                yield return (TAct)relationship.TargetAct;
            }
        }

        public static TAct GetFirstOrDefaultRelatedAct<TAct>(this (IDataPersistenceService<ActRelationship> persistenceService, Act sourceAct) source, Guid relationshipTypeKey, Guid? targetTypeConceptKey = null) where TAct : Act
        {
            return GetFirstOrDefaultRelatedAct<TAct>(source.persistenceService, source.sourceAct.Key.Value, relationshipTypeKey, targetTypeConceptKey);
        }
    }
}

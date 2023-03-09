using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public static TAct GetFirstOrDefaultRelatedAct<TAct>(this IDataPersistenceService<ActRelationship> persistenceService, Guid sourceActKey, Guid relationshipTypeKey, Guid? targetTypeConceptKey = null) where TAct: Act
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

        public static TAct GetFirstOrDefaultRelatedAct<TAct>(this (IDataPersistenceService<ActRelationship> persistenceService, Act sourceAct) source, Guid relationshipTypeKey, Guid? targetTypeConceptKey = null) where TAct: Act
        {
            return GetFirstOrDefaultRelatedAct<TAct>(source.persistenceService, source.sourceAct.Key.Value, relationshipTypeKey, targetTypeConceptKey);
        }
    }
}

using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Related person resource handler
    /// </summary>
    public class RelatedPersonResourceHandler : RepositoryResourceHandlerBase<RelatedPerson, Core.Model.Entities.EntityRelationship>
    {
        // Relationships to family members
        private List<Guid> m_relatedPersons;

        // Patient repository
        private IRepositoryService<Core.Model.Roles.Patient> m_patientRepository;

        private IRepositoryService<Core.Model.Entities.Person> m_personRepository;
        /// <summary>
        /// Create related person resource handler
        /// </summary>
        public RelatedPersonResourceHandler(IRepositoryService<Core.Model.Entities.Person> personRepo, IRepositoryService<Core.Model.Roles.Patient> patientRepository, IRepositoryService<EntityRelationship> repo, IRepositoryService<Concept> conceptRepository) : base(repo)
        {
            this.m_relatedPersons = conceptRepository.Find(x => x.ReferenceTerms.Any(r => r.ReferenceTerm.CodeSystem.Url == "http://terminology.hl7.org/CodeSystem/v2-0131" || r.ReferenceTerm.CodeSystem.Url == "http://terminology.hl7.org/CodeSystem/v3-RoleCode")).Select(c => c.Key.Value).ToList();
            this.m_patientRepository = patientRepository;
            this.m_personRepository = personRepo;
        }

        /// <summary>
        /// Can map object
        /// </summary>
        public override bool CanMapObject(object instance) => instance is RelatedPerson ||
            instance is EntityRelationship er && this.m_relatedPersons.Contains(er.RelationshipTypeKey.Value);

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Entities.EntityRelationship resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<CapabilityStatement.ResourceInteractionComponent> GetInteractions() =>
            new CapabilityStatement.TypeRestfulInteraction[]
            {
                CapabilityStatement.TypeRestfulInteraction.Create,
                CapabilityStatement.TypeRestfulInteraction.Update,
                CapabilityStatement.TypeRestfulInteraction.Delete,
                CapabilityStatement.TypeRestfulInteraction.Read,
                CapabilityStatement.TypeRestfulInteraction.SearchType
            }.Select(o => new CapabilityStatement.ResourceInteractionComponent() { Code = o });

        /// <summary>
        /// Get reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Entities.EntityRelationship resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Map the object to FHIR
        /// </summary>
        /// <returns></returns>
        protected override RelatedPerson MapToFhir(Core.Model.Entities.EntityRelationship model)
        {
            var relModel = model.LoadProperty(o => o.TargetEntity) as Core.Model.Entities.Person;
            if (relModel == null)
            {
                throw new InvalidOperationException("Cannot create unless source and target are Person");
            }

            // Create the relative object
            var relative = DataTypeConverter.CreateResource<RelatedPerson>(relModel);
            relative.Relationship = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConcept(model.LoadProperty(o => o.RelationshipType), new string[] { "http://terminology.hl7.org/CodeSystem/v2-0131", "http://terminology.hl7.org/CodeSystem/v3-RoleCode" }, false) };
            relative.Address = relModel.LoadCollection(o => o.Addresses).Select(o => DataTypeConverter.ToFhirAddress(o)).ToList();
            relative.Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(relModel.LoadProperty(o => o.GenderConcept), "http://hl7.org/fhir/administrative-gender", true);
            relative.Identifier = relModel.LoadCollection(o => o.Identifiers).Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();
            relative.Name = relModel.LoadCollection(o => o.Names).Select(o => DataTypeConverter.ToFhirHumanName(o)).ToList();
            relative.Patient = DataTypeConverter.CreateNonVersionedReference<Patient>(model.SourceEntityKey);
            relative.Telecom = relModel.LoadCollection(o => o.Telecoms).Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();
            relative.Id = model.Key.ToString();
            return relative;
        }

        /// <summary>
        /// Query for substance administrations that aren't immunizations
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="totalResults">The total results.</param>
        /// <param name="queryId">The unique query state identifier</param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        protected override IEnumerable<EntityRelationship> Query(System.Linq.Expressions.Expression<Func<EntityRelationship, bool>> query, Guid queryId, int offset, int count, out int totalResults)
        {

            System.Linq.Expressions.Expression typeReference = null;
            System.Linq.Expressions.Expression typeProperty = System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(EntityRelationship).GetProperty(nameof(EntityRelationship.RelationshipTypeKey)));
            foreach (var tr in this.m_relatedPersons)
            {
                var checkExpression = System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.Equal, System.Linq.Expressions.Expression.Convert(typeProperty, typeof(Guid)), System.Linq.Expressions.Expression.Constant(tr));
                if (typeReference == null)
                {
                    typeReference = checkExpression;
                }
                else
                {
                    typeReference = System.Linq.Expressions.Expression.MakeBinary(System.Linq.Expressions.ExpressionType.OrElse, typeReference, checkExpression);
                }
            }

            var classReference = System.Linq.Expressions.Expression.MakeBinary(
                System.Linq.Expressions.ExpressionType.Equal,
                System.Linq.Expressions.Expression.Convert(
                    System.Linq.Expressions.Expression.MakeMemberAccess(
                        System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(EntityRelationship).GetProperty(nameof(EntityRelationship.TargetEntity))),
                        typeof(Entity).GetProperty(nameof(Entity.ClassConceptKey))
                    ), typeof(Guid)), System.Linq.Expressions.Expression.Constant(EntityClassKeys.Person));

            query = System.Linq.Expressions.Expression.Lambda<Func<EntityRelationship, bool>>(System.Linq.Expressions.Expression.AndAlso(typeReference, System.Linq.Expressions.Expression.AndAlso(classReference, query.Body)), query.Parameters);

            if (queryId == Guid.Empty)
                return this.m_repository.Find(query, offset, count, out totalResults);
            else
                return (this.m_repository as IPersistableQueryRepositoryService<EntityRelationship>).Find(query, offset, count, out totalResults, queryId);
        }

        /// <summary>
        /// Map the related person to model
        /// </summary>
        protected override Core.Model.Entities.EntityRelationship MapToModel(RelatedPerson resource)
        {

            var relationshipTypes = resource.Relationship.Select(o => DataTypeConverter.ToConcept(o)).Select(o => o?.Key).Distinct();

            // patient to which this person is related
            if (resource.Patient == null)
                throw new ArgumentException("RelatedPerson requires Patient relationship");
            else if (relationshipTypes.Count(o => o != null) != 1)
                throw new ArgumentException("RelatedPerson nust have EXACTLY ONE relationship type");

            EntityRelationship relationship = null;
            // Attempt to find the existing ER 
            if (Guid.TryParse(resource.Id, out Guid key))
            {
                relationship = this.m_repository.Get(key);
            }

            // Find the source of the relationship
            var sourceEntity = DataTypeConverter.ResolveEntity(resource.Patient, resource);
            if (sourceEntity == null)
            {
                throw new KeyNotFoundException($"Could not resolve {resource.Patient.Reference}");
            }

            // Relationship not found
            if (relationship == null)
            {
                relationship = new EntityRelationship()
                {
                    Key = Guid.NewGuid(),
                    TargetEntity = new SanteDB.Core.Model.Entities.Person(),
                    SourceEntityKey = sourceEntity.Key
                };
            }
            else if (sourceEntity.Key != relationship.SourceEntityKey)
            {
                // HACK: Is there a relationship that exists between the existing source entity and the purported source entity (like an MDM or replaces?)
                if (!this.m_repository.Find(o => o.TargetEntityKey == sourceEntity.Key && o.SourceEntityKey == relationship.SourceEntityKey).Any())
                {
                    throw new InvalidOperationException($"Cannot change the source of relationship from {relationship.SourceEntityKey} to {sourceEntity.Key}");
                }
            }

            // The source of the object is not a patient (perhaps an MDM entity)
            if (sourceEntity is Core.Model.Roles.Patient patientSource)
            {
                relationship.SourceEntity = patientSource;
            }
            else
            {
                sourceEntity = relationship.SourceEntity = patientSource = this.m_patientRepository.Get(sourceEntity.Key.Value);
            }

            var person = relationship.LoadProperty(o => o.TargetEntity) as Core.Model.Entities.Person;

            // Set the relationship
            relationship.ClassificationKey = RelationshipClassKeys.ReferencedObjectLink;
            relationship.RelationshipTypeKey = relationshipTypes.First();

            // Attempt to find the relationship this is talking about
            person.Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList();
            person.DateOfBirthXml = resource.BirthDate;
            // TODO: See DSM-42 Correction
            person.GenderConceptKey = DataTypeConverter.ToConcept(new Coding("http://hl7.org/fhir/administrative-gender", Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key;
            person.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
            person.LanguageCommunication = resource.Communication.Select(DataTypeConverter.ToLanguageCommunication).ToList();
            person.Names = resource.Name.Select(DataTypeConverter.ToEntityName).ToList();
            person.StatusConceptKey = resource.Active == null || resource.Active == true ? StatusKeys.Active : StatusKeys.Obsolete;
            person.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList();

            // Identity
            person.Extensions = resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, person)).ToList();

            relationship.TargetEntity = person;
            return relationship;

        }
    }
}

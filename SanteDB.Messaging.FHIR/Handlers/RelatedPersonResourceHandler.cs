using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Text;
using SanteDB.Core.Model;
using SanteDB.Core;
using SanteDB.Core.Services;
using SanteDB.Core.Model.DataTypes;
using System.Linq;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Security;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Related person resource handler
    /// </summary>
    public class RelatedPersonResourceHandler : RepositoryResourceHandlerBase<RelatedPerson, Core.Model.Entities.EntityRelationship>
    {
        // Relationships to family members
        private List<Guid> m_relatedPersons;

        /// <summary>
        /// Create related person resource handler
        /// </summary>
        public RelatedPersonResourceHandler()
        {
            this.m_relatedPersons = ApplicationServiceContext.Current.GetService<IRepositoryService<Concept>>().Find(x => x.ConceptSets.Any(c => c.Mnemonic == "FamilyMember")).Select(c => c.Key.Value).ToList();
        }

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
            // TODO: Refactor this (see DSM-42 issue ticket)
            relative.Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(relModel.LoadProperty(o => o.GenderConcept), "http://hl7.org/fhir/administrative-gender", true);
            relative.Identifier = relModel.LoadCollection(o => o.Identifiers).Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();
            relative.Name = relModel.LoadCollection(o => o.Names).Select(o => DataTypeConverter.ToFhirHumanName(o)).ToList();
            relative.Patient = DataTypeConverter.CreateNonVersionedReference<Patient>(model.SourceEntityKey);
            relative.Telecom = relModel.LoadCollection(o => o.Telecoms).Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();
            relative.Id = model.Key.ToString();
            // TODO: Add other relationship extensions
            return relative;
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
                    TargetEntity = new SanteDB.Core.Model.Entities.Person(),
                    SourceEntityKey = sourceEntity.Key
                };
            }
            else if (sourceEntity.Key != relationship.SourceEntityKey)
            {
                throw new InvalidOperationException($"Cannot change the source of relationship from {relationship.SourceEntityKey} to {sourceEntity.Key}");
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

            // Attempt to find the patient/person that is being referenced
            person.Key = Guid.NewGuid();
            foreach (var id in person.Identifiers) // try to lookup based on reliable id for the record to update
            {
                if (id.LoadProperty(o => o.Authority).IsUnique)
                {
                    using (AuthenticationContext.EnterSystemContext())
                    {
                        var match = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Roles.Patient>>().Find(o => o.Identifiers.Any(i => i.Authority.DomainName == id.Authority.DomainName && i.Value == id.Value), 0, 1, out int tr);
                        if (tr == 1)
                            person.Key = match.FirstOrDefault()?.Key ?? Guid.NewGuid();
                        else if (tr > 1)
                            this.m_traceSource.TraceWarning($"The identifier {id} resulted in ambiguous results ({tr} matches) - the FHIR layer will treat this as an INSERT rather than UPDATE");
                    }
                }
            }
            relationship.TargetEntityKey = person.Key;
            return relationship;

        }
    }
}

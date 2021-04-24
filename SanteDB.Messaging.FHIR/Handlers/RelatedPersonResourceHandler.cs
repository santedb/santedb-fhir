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
    public class RelatedPersonResourceHandler : RepositoryResourceHandlerBase<RelatedPerson, Core.Model.Entities.Person>
    {
        // Relationships to family members
        private List<Guid> m_relatedPersons;

        // ER relationship
        private IRepositoryService<EntityRelationship> m_relatedRepository;

        /// <summary>
        /// Create related person resource handler
        /// </summary>
        public RelatedPersonResourceHandler()
        {
            this.m_relatedPersons = ApplicationServiceContext.Current.GetService<IRepositoryService<Concept>>().Find(x => x.ConceptSets.Any(c => c.Mnemonic == "FamilyMember")).Select(c => c.Key.Value).ToList();
            this.m_relatedRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<EntityRelationship>>();
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
                CapabilityStatement.TypeRestfulInteraction.SearchType,
                CapabilityStatement.TypeRestfulInteraction.Vread
            }.Select(o => new CapabilityStatement.ResourceInteractionComponent() { Code = o });


        /// <summary>
        /// Map the object to FHIR
        /// </summary>
        /// <param name="model"></param>
        /// <param name="restOperationContext"></param>
        /// <returns></returns>
        protected override RelatedPerson MapToFhir(Core.Model.Entities.Person model, RestOperationContext restOperationContext)
        {
            // Create the relative object
            var relative = DataTypeConverter.CreateResource<RelatedPerson>(model, restOperationContext);

            // Attempt to find a relationship to a patient (note: this person can only be related to one other person or else the FHIR model breaks
            var patientRels = this.m_relatedRepository.Find(r => r.TargetEntityKey == model.Key && r.ObsoleteVersionSequenceId == null && r.SourceEntity.ClassConceptKey == EntityClassKeys.Patient).GroupBy(o=>o.SourceEntityKey);
            if (patientRels.Count() > 1)
                throw new InvalidOperationException($"FHIR only allows a RelatedPerson to be related to ONE Patient. This person is related to {patientRels.Count()} patients");
            relative.Relationship = patientRels.First().Select(rel=>DataTypeConverter.ToFhirCodeableConcept(rel.LoadProperty(o=>o.RelationshipType), new string[] { "http://terminology.hl7.org/CodeSystem/v2-0131", "http://terminology.hl7.org/CodeSystem/v3-RoleCode" }, false)).ToList();
            relative.Address = model.LoadCollection(o=>o.Addresses).Select(o => DataTypeConverter.ToFhirAddress(o)).ToList();
            // TODO: Refactor this (see DSM-42 issue ticket)
            relative.Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(model.LoadProperty(o => o.GenderConcept), "http://hl7.org/fhir/administrative-gender", true);
            relative.Identifier = model.LoadCollection(o=>o.Identifiers).Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();
            relative.Name = model.LoadCollection(o => o.Names).Select(o => DataTypeConverter.ToFhirHumanName(o)).ToList();
            relative.Patient = DataTypeConverter.CreateNonVersionedReference<Patient>(patientRels.First().Key, restOperationContext);
            relative.Telecom = model.LoadCollection(o=>o.Telecoms).Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();

            // TODO: Add other relationship extensions
            return relative;
        }

        /// <summary>
        /// Map the related person to model
        /// </summary>
        protected override Core.Model.Entities.Person MapToModel(RelatedPerson resource, RestOperationContext restOperationContext)
        {
            var person = new Core.Model.Entities.Person
            {
                Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList(),
                CreationTime = DateTimeOffset.Now,
                DateOfBirthXml = resource.BirthDate,
                // TODO: See DSM-42 Correction
                GenderConceptKey = DataTypeConverter.ToConcept(new Coding("http://hl7.org/fhir/administrative-gender", Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key,
                Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList(),
                LanguageCommunication = resource.Communication.Select(DataTypeConverter.ToLanguageCommunication).ToList(),
                Names = resource.Name.Select(DataTypeConverter.ToEntityName).ToList(),
                StatusConceptKey = resource.Active == null || resource.Active == true ? StatusKeys.Active : StatusKeys.Obsolete,
                Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList()
            };

            // Identity
            person.Extensions = resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, person)).ToList();
            Guid key;
            if (!Guid.TryParse(resource.Id, out key))
            {
                foreach (var id in person.Identifiers) // try to lookup based on reliable id for the record to update
                {
                    if (id.LoadProperty(o=>o.Authority).IsUnique)
                    {
                        using (AuthenticationContext.EnterSystemContext())
                        {
                            var match = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Roles.Patient>>().Find(o => o.Identifiers.Any(i => i.Authority.DomainName == id.Authority.DomainName && i.Value == id.Value), 0, 1, out int tr);
                            if (tr == 1)
                                key = match.FirstOrDefault()?.Key ?? Guid.NewGuid();
                            else if (tr > 1)
                                this.m_traceSource.TraceWarning($"The identifier {id} resulted in ambiguous results ({tr} matches) - the FHIR layer will treat this as an INSERT rather than UPDATE");
                        }
                    }
                }

            }

            // KEY?
            if (key == Guid.Empty) // No key create a new one
                key = Guid.NewGuid();

            person.Key = key;

            // patient to which this person is related
            if (resource.Patient == null)
                throw new ArgumentException("RelatedPerson requires Patient relationship");

            // Lookup the patient or use uuid
            person.Relationships.AddRange(resource.Relationship.SelectMany(r=>r.Coding).Select(o => DataTypeConverter.ToConcept(o))
                .GroupBy(c=>c.Key.Value).Select(rel=>new EntityRelationship()
            {
                TargetEntityKey = person.Key,
                SourceEntityKey = DataTypeConverter.ResolveEntity(resource.Patient, resource)?.Key,
                RelationshipTypeKey = rel.Key,
                ClassificationKey = RelationshipClassKeys.ReferencedObjectLink
            }));

            return person;
        }
    }
}

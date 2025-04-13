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
using DocumentFormat.OpenXml.Office2016.Drawing.Command;
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Related person resource handler
    /// </summary>
    public class RelatedPersonResourceHandler : RepositoryResourceHandlerBase<RelatedPerson, Core.Model.Entities.EntityRelationship>
    {
        // MDM MAster key
        private readonly Guid MDM_MASTER_CLASS_KEY = Guid.Parse("49328452-7e30-4dcd-94cd-fd532d111578");

        // Relationships to family members
        private List<Guid> m_relatedPersons;

        // Patient repository
        private IRepositoryService<Core.Model.Roles.Patient> m_patientRepository;

        private IRepositoryService<Core.Model.Entities.Person> m_personRepository;

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(RelatedPersonResourceHandler));

        /// <summary>
        /// Create related person resource handler
        /// </summary>
        public RelatedPersonResourceHandler(IRepositoryService<Core.Model.Entities.Person> personRepo, IRepositoryService<Core.Model.Roles.Patient> patientRepository, IRepositoryService<EntityRelationship> repo, IRepositoryService<Concept> conceptRepository, ILocalizationService localizationService) : base(repo, localizationService)
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
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
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
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Map the object to FHIR
        /// </summary>
        /// <returns></returns>
        protected override RelatedPerson MapToFhir(Core.Model.Entities.EntityRelationship model)
        {
            var relModel = model.LoadProperty(o => o.TargetEntity);

            if (relModel.ClassConceptKey == MDM_MASTER_CLASS_KEY) // Is the target a MASTER record?
            {
                relModel = this.m_patientRepository.Get(relModel.Key.Value) ??
                    this.m_personRepository.Get(relModel.Key.Value);
            }

            if (!(relModel is Core.Model.Entities.Person person))
            {
                m_tracer.TraceError("Cannot create unless source and target are Person");
                throw new InvalidOperationException(m_localizationService.GetString("error.type.InvalidOperation.cannotCreate", new
                {
                    param = "Person"
                }));
            }

            var relative = DataTypeConverter.CreateResource<RelatedPerson>(relModel);
            relative.Active = StatusKeys.ActiveStates.Contains(relModel.StatusConceptKey.Value) && model.ObsoleteVersionSequenceId.HasValue == false;
            relative.Relationship = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConcept(model.RelationshipTypeKey, "http://terminology.hl7.org/CodeSystem/v2-0131", "http://terminology.hl7.org/CodeSystem/v3-RoleCode") };
            relative.Address = relModel.LoadCollection(o => o.Addresses).Select(o => DataTypeConverter.ToFhirAddress(o)).ToList();
            relative.Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(person.GenderConceptKey, "http://hl7.org/fhir/administrative-gender");
            relative.Identifier = relModel.LoadCollection(o => o.Identifiers).Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();
            relative.Name = relModel.LoadCollection(o => o.Names).Select(o => DataTypeConverter.ToFhirHumanName(o)).ToList();
            relative.Patient = DataTypeConverter.CreateNonVersionedReference<Patient>(model.SourceEntityKey);
            relative.Telecom = relModel.LoadCollection(o => o.Telecoms).Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();
            relative.Id = model.Key.ToString();
            return relative;
        }

        /// <inheritdoc />
        protected override IQueryResultSet<EntityRelationship> QueryInternal(System.Linq.Expressions.Expression<Func<EntityRelationship, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters)
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

            return this.m_repository.Find(query);
        }

        /// <summary>
        /// Map the related person to model
        /// </summary>
        protected override Core.Model.Entities.EntityRelationship MapToModel(RelatedPerson resource)
        {
            var relationshipTypes = resource.Relationship.Select(o => DataTypeConverter.ToConcept(o)).Select(o => o?.Key).Distinct();

            // patient to which this person is related
            if (resource.Patient == null)
            {
                this.m_tracer.TraceError("RelatedPerson requires Patient relationship");
                throw new ArgumentException(this.m_localizationService.GetString("error.messaging.fhir.relatedPerson.patientRelationship"));
            }
            else if (relationshipTypes.Count(o => o != null) != 1)
            {
                this.m_tracer.TraceError("RelatedPerson nust have EXACTLY ONE relationship type");
                throw new ArgumentException(this.m_localizationService.GetString("error.messaging.fhir.relatedPerson.oneRelationshipType"));
            }

            EntityRelationship relationship = null;
            // Attempt to find the existing ER
            if (Guid.TryParse(resource.Id, out Guid key))
            {
                relationship = this.m_repository.Get(key);

                // HACK: We couldn't find the relationship via key - but it might still be an external reference to a patient
                // <rant>
                // RelatedPerson is evil - it combines the concept of a "Relationship" with a "Person" so the ID might point to the person which is related,
                // Take for example a mother who's given birth to twins, a submission may be:
                //      Patient/1 -> Baby 1
                //      Patient/2 -> Baby 2
                //      RelatedPerson/1 -> Mother for Patient/1
                //      RelatedPerson/2 -> Mother for Patient/2
                //      Patient/3 -> Mother Patient which links to RelatedPerson/1 and RelatedPerson/2
                // Here, FHIR is indicating RelatedPerson/1 and RelatedPerson/2 are two different relationships, even though they're the same freaking person
                // which is why RelatedPerson is evil.
                //
                // SanteDB, when disclosing the ID of a RelatedPerson will always use the key of the relationship , but for an initial registration it depends whatever
                // random identifiers the sender uses and how the sender views the world.
                // </rant>
                // <tldr>RelatedPerson is evil and this is a total hack to overcome FHIR's hacking of RelatedPerson</tldr>
                if (relationship == null)
                {
                    var lookupPerson = this.m_personRepository.Get(key);
                    if (lookupPerson != null)
                    {
                        relationship = new EntityRelationship()
                        {
                            Key = Guid.NewGuid(),
                            TargetEntity = lookupPerson
                        };
                    }
                }
                
                if(relationship == null)
                {
                    relationship = new EntityRelationship()
                    {
                        Key = key
                    };
                }
            }

            // Find the source of the relationship
            Entity sourceEntity = DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Patient, resource);
            if (sourceEntity == null)
            {
                this.m_tracer.TraceError($"Could not resolve {resource.Patient.Reference}");
                throw new KeyNotFoundException(m_localizationService.GetString("error.type.KeyNotFoundException.couldNotResolve", new
                {
                    param = resource.Patient.Reference
                }));
            }

            // Relationship not found
            if (relationship == null)
            {
                // Attempt to find a relationship between these two entities
                relationship = new EntityRelationship()
                {
                    Key = Guid.NewGuid(),
                    SourceEntityKey = sourceEntity.Key
                };
            }
            else if (!relationship.SourceEntityKey.HasValue) // no source entity
            {
                relationship.SourceEntityKey = sourceEntity.Key;
            }
            else if (sourceEntity.Key != relationship.SourceEntityKey)
            {
                // HACK: Is there a relationship that exists between the existing source entity and the purported source entity (like an MDM or replaces?)
                if (!this.m_repository.Find(o => o.TargetEntityKey == sourceEntity.Key && o.SourceEntityKey == relationship.SourceEntityKey).Any())
                {
                    this.m_tracer.TraceError($"Cannot change the source of relationship from {relationship.SourceEntityKey} to {sourceEntity.Key}");

                    throw new InvalidOperationException(m_localizationService.GetString("error.type.InvalidOperation.cannotChange", new
                    {
                        param = relationship.SourceEntityKey,
                        param2 = sourceEntity.Key
                    }));
                }
            }

            // The source of the object is not a patient (perhaps an MDM entity)
            // Here the GET has returned an entity
            if (sourceEntity is Core.Model.Roles.Patient patientSource)
            {
                relationship.SourceEntity = patientSource;
            }
            else
            {
                sourceEntity = relationship.SourceEntity = patientSource = this.m_patientRepository.Get(sourceEntity.Key.Value);
            }

            relationship.ClassificationKey = RelationshipClassKeys.ReferencedObjectLink;
            relationship.RelationshipTypeKey = relationshipTypes.First();

            Core.Model.Entities.Person person = relationship.LoadProperty(o => o.TargetEntity) as Core.Model.Entities.Person;
            if (person == null && resource.Identifier?.Count > 0)
            {
                foreach (var ii in resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier))
                {
                    if (ii.LoadProperty(o => o.IdentityDomain).IsUnique)
                    {
                        person = this.m_patientRepository.Find(o => o.Identifiers.Where(i => i.IdentityDomainKey == ii.IdentityDomainKey).Any(i => i.Value == ii.Value)).FirstOrDefault() ??
                            this.m_personRepository.Find(o => o.Identifiers.Where(i => i.IdentityDomainKey == ii.IdentityDomainKey).Any(i => i.Value == ii.Value)).FirstOrDefault();
                    }
                    if (person != null)
                    {
                        person.AddTag(FhirConstants.PlaceholderTag, "true"); // This is just a placeholder entry in case there are other patients
                        break;
                    }
                }
            }
            // We couldn't find any matching patients via business identifiers
            if (person == null)
            {
                relationship.TargetEntity = person = new Core.Model.Entities.Person()
                {
                    Key = Guid.NewGuid()
                };
            }

            // HACK: Try to figure out what tf the client is trying to convey and map it into our worldview
            // <rant>
            // If you needed further evidence of how RelatedPerson is evil, this bit of code shows it - basically some clients (and examples)
            // will send RelatedPerson as a combo of Person+Relationship, however other clients (and examples) use RelatedPerson as only a Relationship
            // so we have to handle both cases. Basically, if the client sent us a RelatedPerson with only an identifier and relationship type then we
            // treat it as *only* a relationship indicator (i.e. the client is relying on us to reference or resolve an existing person to relate) , however
            // if the client sends us name, address, or telecom, we can assume the client is attempting to create (or perhaps update, who really knows?)
            // the person to which the relationship points to.
            // </rant>
            // <tldr>RelatedPerson is evil - it could be a person + relationship or just a relationship and we have to handle both cases which is why this ugly code is here</tldr>
            if (resource.Name.Any() || resource.Address.Any() || resource.Telecom.Any())
            {
                // Set the relationship
                // Attempt to find the relationship this is talking about
                person.Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList();
                person.DateOfBirthXml = resource.BirthDate;
                // TODO: See DSM-42 Correction
                person.GenderConceptKey = DataTypeConverter.ToConcept(new Coding("http://hl7.org/fhir/administrative-gender", Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key;

                // TODO: Cross reference via identifiers
                person.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
                person.LanguageCommunication = resource.Communication.Select(DataTypeConverter.ToLanguageCommunication).ToList();
                person.Names = resource.Name.Select(DataTypeConverter.ToEntityName).ToList();
                person.StatusConceptKey = resource.Active == null || resource.Active == true ? StatusKeys.Active : StatusKeys.Inactive;
                person.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList();
                // Identity
                person.LoadProperty(o=>o.Extensions).AddRange(resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, person)).OfType<EntityExtension>());
            }
            else
            {
                // TODO: Cross reference via identifiers
                person.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
                person.AddTag(FhirConstants.PlaceholderTag, "true");
            }
            relationship.TargetEntity = person;

            // One last check for an existing
            var existingRel = this.m_repository.Find(o => o.SourceEntityKey == relationship.SourceEntityKey && o.RelationshipTypeKey == relationship.RelationshipTypeKey && o.TargetEntityKey == person.Key);
            if (existingRel.Any())
            {
                relationship.Key = existingRel.Select(o => o.Key).First();
            }


            return relationship;
        }
    }
}
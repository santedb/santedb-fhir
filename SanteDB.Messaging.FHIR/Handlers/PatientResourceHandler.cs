﻿/*
 * Portions Copyright 2019-2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE)
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
 * User: fyfej (Justin Fyfe)
 * Date: 2019-11-27
 */
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;
using DatePrecision = SanteDB.Core.Model.DataTypes.DatePrecision;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a resource handler which can handle patients.
    /// </summary>
    public class PatientResourceHandler : RepositoryResourceHandlerBase<Patient, Core.Model.Roles.Patient>
    {


        // IDs of family members
        private readonly Guid MDM_MASTER_LINK = Guid.Parse("97730a52-7e30-4dcd-94cd-fd532d111578");

        // ER repository
        private IRepositoryService<EntityRelationship> m_erRepository;
        private List<Guid> m_relatedPersons;

        /// <summary>
        /// Resource handler subscription
        /// </summary>
        public PatientResourceHandler(IRepositoryService<Core.Model.Roles.Patient> repo, IRepositoryService<EntityRelationship> erRepository, IRepositoryService<Concept> conceptRepository) : base(repo)
        {
            this.m_erRepository = erRepository;

            var relTypes = conceptRepository.Find(x => x.ReferenceTerms.Any(r => r.ReferenceTerm.CodeSystem.Url == "http://terminology.hl7.org/CodeSystem/v2-0131" || r.ReferenceTerm.CodeSystem.Url == "http://terminology.hl7.org/CodeSystem/v3-RoleCode"));
            this.m_relatedPersons = relTypes.Select(c => c.Key.Value).ToList();

        }

        /// <summary>
        /// Map a patient object to FHIR.
        /// </summary>
        /// <param name="model">The patient to map to FHIR</param>
        /// <param name="restOperationContext">The current REST operation context</param>
        /// <returns>Returns the mapped FHIR resource.</returns>
        protected override Patient MapToFhir(Core.Model.Roles.Patient model)
        {
            var retVal = DataTypeConverter.CreateResource<Patient>(model);
            retVal.Active = model.StatusConceptKey == StatusKeys.Active || model.StatusConceptKey == StatusKeys.New;
            retVal.Address = model.GetAddresses().Select(o => DataTypeConverter.ToFhirAddress(o)).ToList();
            if (model.DateOfBirth.HasValue)
                switch (model.DateOfBirthPrecision.GetValueOrDefault())
                {
                    case DatePrecision.Day:
                        retVal.BirthDate = model.DateOfBirth.Value.ToString("yyyy-MM-dd");
                        break;
                    case DatePrecision.Month:
                        retVal.BirthDate = model.DateOfBirth.Value.ToString("yyyy-MM");
                        break;
                    case DatePrecision.Year:
                        retVal.BirthDate = model.DateOfBirth.Value.ToString("yyyy");
                        break;
                }

            // Deceased precision
            if (model.DeceasedDate.HasValue)
            {
                if (model.DeceasedDate == DateTime.MinValue)
                {
                    retVal.Deceased = new FhirBoolean(true);
                }
                else
                {
                    switch (model.DeceasedDatePrecision)
                    {
                        case DatePrecision.Day:
                            retVal.Deceased = new Date(model.DeceasedDate.Value.Year, model.DeceasedDate.Value.Month, model.DeceasedDate.Value.Day);
                            break;
                        case DatePrecision.Month:
                            retVal.Deceased = new Date(model.DeceasedDate.Value.Year, model.DeceasedDate.Value.Month);
                            break;
                        case DatePrecision.Year:
                            retVal.Deceased = new Date(model.DeceasedDate.Value.Year);
                            break;
                        default:
                            retVal.Deceased = new FhirDateTime(model.DeceasedDate.Value);
                            break;
                    }
                }
            }

            if (model.GenderConceptKey.HasValue)
            {
                retVal.Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(model.LoadProperty(o => o.GenderConcept), "http://hl7.org/fhir/administrative-gender", true);
            }

            retVal.Identifier = model.Identifiers?.Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();
            retVal.MultipleBirth = model.MultipleBirthOrder == 0 ? (Element)new FhirBoolean(true) : model.MultipleBirthOrder.HasValue ? new Integer(model.MultipleBirthOrder.Value) : null;
            retVal.Name = model.GetNames().Select(o => DataTypeConverter.ToFhirHumanName(o)).ToList();
            retVal.Telecom = model.GetTelecoms().Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();
            retVal.Communication = model.GetPersonLanguages().Select(o => DataTypeConverter.ToFhirCommunicationComponent(o)).ToList();
            foreach (var rel in model.GetRelationships().Where(o => !o.InversionIndicator))
            {
                // Contact => Person
                if (rel.LoadProperty(o => o.TargetEntity) is SanteDB.Core.Model.Roles.Patient && rel.ClassificationKey == RelationshipClassKeys.ContainedObjectLink)
                {
                    var relative = FhirResourceHandlerUtil.GetMappersFor(ResourceType.RelatedPerson).First().MapToFhir(rel);
                    relative.Meta.Security = null;
                    retVal.Contained.Add(relative);
                }
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Contact)
                {
                    var person = rel.LoadProperty(o => o.TargetEntity);

                    var contact = new Patient.ContactComponent()
                    {
                        ElementId = $"{person.Key}",
                        Address = DataTypeConverter.ToFhirAddress(person.GetAddresses().FirstOrDefault()),
                        Relationship = new List<CodeableConcept>() {
                            DataTypeConverter.ToFhirCodeableConcept(rel.LoadProperty(o=>o.RelationshipRole), "http://terminology.hl7.org/CodeSystem/v2-0131", true),
                            DataTypeConverter.ToFhirCodeableConcept(rel.LoadProperty(o => o.RelationshipType), "http://terminology.hl7.org/CodeSystem/v2-0131", true)
                        }.OfType<CodeableConcept>().ToList(),
                        Name = DataTypeConverter.ToFhirHumanName(person.GetNames().FirstOrDefault()),
                        // TODO: Gender
                        Telecom = person.GetTelecoms().Select(t => DataTypeConverter.ToFhirTelecom(t)).ToList(),
                    };

                    DataTypeConverter.AddExtensions(person, contact);

                    retVal.Contact.Add(contact);

                    // TODO: 
                    //retVal.Link.Add(new Patient.LinkComponent()
                    //{
                    //    Other = DataTypeConverter.CreateNonVersionedReference<RelatedPerson>(rel.TargetEntityKey),
                    //    Type = Patient.LinkType.Seealso
                    //});
                }
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper)
                    retVal.ManagingOrganization = DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Organization>(rel.LoadProperty(o => o.TargetEntity));
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.HealthcareProvider)
                    retVal.GeneralPractitioner.Add(DataTypeConverter.CreateVersionedReference<Practitioner>(rel.LoadProperty(o => o.TargetEntity)));
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Replaces)
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Replaces));
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Duplicate)
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Seealso));
                else if (rel.RelationshipTypeKey == MDM_MASTER_LINK) // HACK: MDM Master Record
                {
                    if (rel.SourceEntityKey.HasValue && rel.SourceEntityKey != model.Key)
                        retVal.Link.Add(this.CreateLink<Patient>(rel.SourceEntityKey.Value, Patient.LinkType.Seealso));
                    else // Is a local
                        retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Refer));
                }
                else if (rel.ClassificationKey == EntityRelationshipTypeKeys.EquivalentEntity)
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Refer));
            }

            // Reverse relationships of family member?
            var reverseRelationships = this.m_erRepository.Find(o => o.TargetEntityKey == model.Key && o.RelationshipType.ConceptSets.Any(cs => cs.Mnemonic == "FamilyMember") && o.ObsoleteVersionSequenceId == null);
            foreach (var rrv in reverseRelationships)
            {
                retVal.Link.Add(new Patient.LinkComponent()
                {
                    Type = Patient.LinkType.Seealso,
                    Other = DataTypeConverter.CreateNonVersionedReference<RelatedPerson>(rrv)
                });
            }

            var photo = model.LoadCollection(o => o.Extensions).FirstOrDefault(o => o.ExtensionTypeKey == ExtensionTypeKeys.JpegPhotoExtension);
            if (photo != null)
                retVal.Photo = new List<Attachment>() {
                    new Attachment()
                    {
                        ContentType = "image/jpg",
                        Data = photo.ExtensionValueXml
                    }
                };

            return retVal;
        }

        /// <summary>
        /// Create patient link
        /// </summary>
        private Patient.LinkComponent CreateLink<TLink>(Guid targetEntityKey, Patient.LinkType type) where TLink : DomainResource, new() => new Patient.LinkComponent()
        {
            Type = type,
            Other = DataTypeConverter.CreateNonVersionedReference<TLink>(targetEntityKey)
        };

        /// <summary>
        /// Maps a FHIR patient resource to an HDSI patient.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <returns>Returns the mapped model.</returns>
        protected override Core.Model.Roles.Patient MapToModel(Patient resource)
        {
            Core.Model.Roles.Patient patient = null;

            // Attempt to XRef
            if (Guid.TryParse(resource.Id, out Guid key))
            {
                patient = this.m_repository.Get(key);

                // Patient doesn't exist?
                if (patient == null)
                {
                    patient = new Core.Model.Roles.Patient()
                    {
                        Key = key
                    };
                }
            }
            else if (resource.Identifier?.Count > 0)
            {
                foreach (var ii in resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier))
                {
                    if (ii.LoadProperty(o => o.Authority).IsUnique)
                    {
                        patient = this.m_repository.Find(o => o.Identifiers.Where(i => i.AuthorityKey == ii.AuthorityKey).Any(i => i.Value == ii.Value)).FirstOrDefault();
                    }
                    if (patient != null)
                    {
                        break;
                    }
                }

                if (patient == null)
                {
                    patient = new Core.Model.Roles.Patient()
                    {
                        Key = Guid.NewGuid()
                    };
                }
            }
            else
            {
                patient = new Core.Model.Roles.Patient()
                {
                    Key = Guid.NewGuid()
                };
            }

            patient.Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList();
            patient.CreationTime = DateTimeOffset.Now;
            patient.DateOfBirthXml = resource.BirthDate;
            patient.DateOfBirthPrecision = DatePrecision.Day;
            patient.GenderConceptKey = resource.Gender == null ? NullReasonKeys.Unknown : DataTypeConverter.ToConcept(new Coding("http://hl7.org/fhir/administrative-gender", Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key;
            patient.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
            patient.LanguageCommunication = resource.Communication.Select(DataTypeConverter.ToLanguageCommunication).ToList();
            patient.Names = resource.Name.Select(DataTypeConverter.ToEntityName).ToList();
            patient.StatusConceptKey = resource.Active == null || resource.Active == true ? StatusKeys.Active : StatusKeys.Obsolete;
            patient.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList();
            patient.Relationships = resource.Contact.Select(r => DataTypeConverter.ToEntityRelationship(r, resource)).ToList();
            patient.Extensions = resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, patient)).ToList();

            if (resource.Deceased is FhirDateTime dtValue && !String.IsNullOrEmpty(dtValue.Value))
            {
                patient.DeceasedDate = dtValue.ToDateTime();
            }
            else if (resource.Deceased is FhirBoolean boolValue && boolValue.Value.GetValueOrDefault() == true)
            {
                // we don't have a field for "deceased indicator" to say that the patient is dead, but we don't know that actual date/time of death
                // should find a better way to do this
                patient.DeceasedDate = DateTime.MinValue;
                patient.DeceasedDatePrecision = DatePrecision.Year;
            }

            if (resource.MultipleBirth is FhirBoolean boolBirth && boolBirth.Value.GetValueOrDefault() == true)
            {
                patient.MultipleBirthOrder = 0;
            }
            else if (resource.MultipleBirth is Integer intBirth)
            {
                patient.MultipleBirthOrder = intBirth.Value;
            }

            if (resource.GeneralPractitioner != null)
            {
                patient.Relationships.AddRange(resource.GeneralPractitioner.Select(r =>
                {
                    var referenceKey = DataTypeConverter.ResolveEntity(r, resource) as Entity;
                    if (referenceKey == null)
                        throw new KeyNotFoundException("Can't locate a registered general practitioner");
                    return new EntityRelationship(EntityRelationshipTypeKeys.HealthcareProvider, referenceKey);
                }));
            }
            if (resource.ManagingOrganization != null)
            {
                var referenceKey = DataTypeConverter.ResolveEntity(resource.ManagingOrganization, resource) as Entity;
                if (referenceKey == null)
                    throw new KeyNotFoundException("Can't locate a registered managing organization");
                else
                {
                    patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Scoper, referenceKey));
                }
            }

            // Process contained related persons
            foreach (var itm in resource.Contained.OfType<RelatedPerson>())
            {
                var er = FhirResourceHandlerUtil.GetMappersFor(ResourceType.RelatedPerson).First().MapToModel(itm) as Core.Model.Entities.EntityRelationship;

                // Relationship bindings
                er.ClassificationKey = RelationshipClassKeys.ContainedObjectLink;
                er.SourceEntityKey = patient.Key;

                // Now add rels to me
                patient.Relationships.Add(er);

            }

            // Links
            foreach (var lnk in resource.Link)
            {
                switch (lnk.Type.Value)
                {
                    case Patient.LinkType.Replaces:
                        {
                            // Find the victim
                            var replacee = DataTypeConverter.ResolveEntity(lnk.Other, resource) as Core.Model.Roles.Patient;
                            if (replacee == null)
                                throw new KeyNotFoundException($"Cannot locate patient referenced by {lnk.Type} relationship");
                            replacee.StatusConceptKey = StatusKeys.Obsolete;
                            patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Replaces, replacee));
                            break;
                        }
                    case Patient.LinkType.ReplacedBy:
                        {
                            // Find the new
                            var replacer = DataTypeConverter.ResolveEntity(lnk.Other, resource) as Core.Model.Roles.Patient;
                            if (replacer == null)
                                throw new KeyNotFoundException($"Cannot locate patient referenced by {lnk.Type} relationship");
                            patient.StatusConceptKey = StatusKeys.Obsolete;
                            replacer.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Replaces, patient));
                            break;
                        }
                    case Patient.LinkType.Seealso:
                        {
                            var referee = DataTypeConverter.ResolveEntity(lnk.Other, resource) as Entity;
                            // Is this a current MDM link?
                            if (referee.GetTag(FhirConstants.PlaceholderTag) == "true") // The referee wants us to become the data
                            {
                                patient.Key = referee.Key;
                            }
                            else if (referee.LoadCollection(o => o.Relationships).Any(r => r.RelationshipTypeKey == MDM_MASTER_LINK)
                                && referee.GetTag("$mdm.type") == "M") // HACK: This is a master and someone is attempting to point another record at it
                            {
                                patient.Relationships.Add(new EntityRelationship()
                                {
                                    RelationshipTypeKey = MDM_MASTER_LINK,
                                    SourceEntityKey = referee.Key,
                                    TargetEntityKey = patient.Key
                                });
                            }
                            else
                            {
                                patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.EquivalentEntity, referee));
                            }
                            break;
                        }
                    case Patient.LinkType.Refer: // This points to a more detailed view of the patient
                        {
                            var referee = DataTypeConverter.ResolveEntity(lnk.Other, resource) as Entity;
                            if (referee.GetTag("$mdm.type") == "M") // HACK: MDM User is attempting to point this at another Master (note: THE MDM LAYER WON'T LIKE THIS)
                                patient.Relationships.Add(new EntityRelationship(MDM_MASTER_LINK, referee));
                            else
                                throw new NotSupportedException($"Setting refer relationships to source of truth is not supported");

                            break; // TODO: These are special cases of references
                        }
                }
            }

            // TODO: Photo
            if (resource.Photo != null && resource.Photo.Any())
            {
                patient.Extensions.RemoveAll(o => o.ExtensionTypeKey == ExtensionTypeKeys.JpegPhotoExtension);
                patient.Extensions.Add(new EntityExtension(ExtensionTypeKeys.JpegPhotoExtension, resource.Photo.First().Data));
            }
            return patient;
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Delete,
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Update
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <summary>
        /// Get included objects
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Roles.Patient resource, IEnumerable<IncludeInstruction> includePaths)
        {
            return includePaths.SelectMany(includeInstruction =>
            {
                switch (includeInstruction.Type)
                {

                    case ResourceType.Practitioner:
                        {
                            var rpHandler = FhirResourceHandlerUtil.GetMappersFor(ResourceType.Practitioner).FirstOrDefault();
                            switch (includeInstruction.JoinPath)
                            {
                                case "generalPractitioner":
                                    return resource.LoadCollection(o => o.Relationships)
                                        .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                            o.RelationshipTypeKey == EntityRelationshipTypeKeys.HealthcareProvider &&
                                            o.LoadProperty(r => r.TargetEntity) is Core.Model.Roles.Provider)
                                        .Select(o => rpHandler.MapToFhir(o.TargetEntity));
                                default:
                                    throw new InvalidOperationException($"Cannot determine how to include {includeInstruction}");
                            }
                        }
                    case ResourceType.Organization:
                        {
                            // Load all related persons and convert them
                            var rpHandler = FhirResourceHandlerUtil.GetMappersFor(ResourceType.Organization).FirstOrDefault();

                            switch (includeInstruction.JoinPath)
                            {
                                case "managingOrganization":
                                    return resource.LoadCollection(o => o.Relationships)
                                        .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                            o.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper &&
                                            o.LoadProperty(r => r.TargetEntity) is Core.Model.Entities.Organization)
                                        .Select(o => rpHandler.MapToFhir(o.TargetEntity));
                                case "generalPractitioner":
                                    return resource.LoadCollection(o => o.Relationships)
                                        .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                            o.RelationshipTypeKey == EntityRelationshipTypeKeys.HealthcareProvider &&
                                            o.LoadProperty(r => r.TargetEntity) is Core.Model.Entities.Organization)
                                        .Select(o => rpHandler.MapToFhir(o.TargetEntity));
                                default:
                                    throw new InvalidOperationException($"Cannot determine how to include {includeInstruction}");
                            }
                        }
                    default:
                        throw new InvalidOperationException($"{includeInstruction.Type} is not supported reverse include");
                }
            });
        }

        /// <summary>
        /// Get included objects
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Roles.Patient resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            return reverseIncludePaths.SelectMany(includeInstruction =>
            {
                switch (includeInstruction.Type)
                {
                    case ResourceType.RelatedPerson:
                        // Load all related persons and convert them
                        var rpHandler = FhirResourceHandlerUtil.GetMappersFor(ResourceType.RelatedPerson).FirstOrDefault();
                        return resource.LoadCollection(o => o.Relationships)
                            .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                o.RelationshipRoleKey == null &&
                                this.m_relatedPersons.Contains(o.RelationshipTypeKey.Value))
                            .Select(o => rpHandler.MapToFhir(o));
                    default:
                        throw new InvalidOperationException($"{includeInstruction.Type} is not supported reverse include");
                }
            });
        }
    }
}
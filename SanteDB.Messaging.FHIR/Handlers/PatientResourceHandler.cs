/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using DynamicExpresso;
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
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
        // ER repository
        private readonly IRepositoryService<EntityRelationship> m_EntityRelationshipRepository;
        private readonly IAdhocCacheService m_AdhocCacheService;
        private readonly IRepositoryService<Concept> m_ConceptRepository;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(PatientResourceHandler));

        /// <summary>
        /// Resource handler subscription
        /// </summary>
        public PatientResourceHandler(IRepositoryService<Core.Model.Roles.Patient> repo, IRepositoryService<EntityRelationship> entityRelationshipRepository, IRepositoryService<Concept> conceptRepository, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null) : base(repo, localizationService)
        {
            this.m_EntityRelationshipRepository = entityRelationshipRepository;
            this.m_AdhocCacheService = adhocCacheService;
            this.m_ConceptRepository = conceptRepository;
        }

        /// <summary>
        /// Gets all related person uuids
        /// </summary>
        private Guid[] GetRelatedPersonConceptUuids()
        {
            var retVal = this.m_AdhocCacheService?.Get<Guid[]>("fhir.patient.relatedPersonsUuids");
            if (retVal == null)
            {
                retVal = this.m_ConceptRepository.Find(x => x.ReferenceTerms.Any(r => r.ReferenceTerm.CodeSystem.Url == "http://terminology.hl7.org/CodeSystem/v2-0131" || r.ReferenceTerm.CodeSystem.Url == "http://terminology.hl7.org/CodeSystem/v3-RoleCode")).Select(o => o.Key.Value).ToArray();
                this.m_AdhocCacheService?.Add("fhir.patient.relatedPersonsUuids", retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Get all family member uuids
        /// </summary>
        private Guid[] GetFamilyMemberUuids()
        {
            var retVal = this.m_AdhocCacheService?.Get<Guid[]>("fhir.patient.familyMemberUuids");
            if (retVal == null)
            {
                retVal = this.m_ConceptRepository.Find(o => o.ConceptSets.Any(cs => cs.Mnemonic == "FamilyMember")).Select(o => o.Key.Value).ToArray();
                this.m_AdhocCacheService?.Add("fhir.patient.familyMemberUuids", retVal);
            }
            return retVal;

        }

        /// <summary>
        /// Map a patient object to FHIR.
        /// </summary>
        /// <param name="model">The patient to map to FHIR</param>
        /// <returns>Returns the mapped FHIR resource.</returns>
        protected override Patient MapToFhir(Core.Model.Roles.Patient model)
        {
            // If the model is being constructed as part of a bundle, then the caller
            // should have included the bundle so we can add any related resources
            var partOfBundle = model.GetAnnotations<Bundle>().FirstOrDefault();

            var retVal = DataTypeConverter.CreateResource<Patient>(model);

            retVal.Active = StatusKeys.ActiveStates.Contains(model.StatusConceptKey.Value);

            _ = model.LoadProperty(p => p.Addresses);
            retVal.Address = model.Addresses?.Select(DataTypeConverter.ToFhirAddress)?.ToList();


            if (model.DateOfBirth.HasValue)
            {
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
                            retVal.Deceased = new FhirDateTime(model.DeceasedDate.Value.Year, model.DeceasedDate.Value.Month, model.DeceasedDate.Value.Day);
                            break;

                        case DatePrecision.Month:
                            retVal.Deceased = new FhirDateTime(model.DeceasedDate.Value.Year, model.DeceasedDate.Value.Month);
                            break;

                        case DatePrecision.Year:
                            retVal.Deceased = new FhirDateTime(model.DeceasedDate.Value.Year);
                            break;

                        default:
                            retVal.Deceased = DataTypeConverter.ToFhirDateTime(model.DeceasedDate);
                            break;
                    }
                }
            }

            if (model.GenderConceptKey.HasValue)
            {
                retVal.Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(model.GenderConceptKey, FhirConstants.CodeSystem_AdministrativeGender, true);
            }

            _ = model.LoadProperty(m => m.Names);
            _ = model.LoadProperty(m => m.Addresses);
            _ = model.LoadProperty(m => m.LanguageCommunication);

            retVal.Identifier = model.Identifiers?.Select(DataTypeConverter.ToFhirIdentifier).ToList();
            retVal.MultipleBirth = model.MultipleBirthOrder == 0 ? (DataType)new FhirBoolean(true) : model.MultipleBirthOrder.HasValue ? new Integer(model.MultipleBirthOrder.Value) : null;
            retVal.Name = model.Names?.Select(DataTypeConverter.ToFhirHumanName)?.ToList();
            retVal.Telecom = model.LoadProperty(o => o.Telecoms)?.Select(DataTypeConverter.ToFhirTelecom)?.ToList();
            retVal.Communication = model.LanguageCommunication?.Select(DataTypeConverter.ToFhirCommunicationComponent)?.ToList();

            if (model.MaritalStatusKey.HasValue || model.MaritalStatus != null)
            {
                retVal.MaritalStatus = DataTypeConverter.ToFhirCodeableConcept(model.MaritalStatusKey ?? model.MaritalStatus?.Key, "http://hl7.org/fhir/ValueSet/marital-status");
            }


            _ = model.LoadProperty(m => m.Relationships);
            foreach (var rel in model.Relationships)
            {
                if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Contact)
                {
                    var relEntity = rel.LoadProperty(o => o.TargetEntity);

                    if (relEntity is Core.Model.Entities.Person person)
                    {
                        _ = person.LoadProperty(p => p.Names);
                        _ = person.LoadProperty(p => p.Addresses);
                        _ = person.LoadProperty(p => p.Telecoms);

                        var contact = new Patient.ContactComponent()
                        {
                            ElementId = $"{person.Key}",
                            Address = DataTypeConverter.ToFhirAddress(person.Addresses?.FirstOrDefault()),
                            Relationship = new List<CodeableConcept>() {
                                DataTypeConverter.ToFhirCodeableConcept(rel.RelationshipRoleKey, "http://terminology.hl7.org/CodeSystem/v2-0131"),
                                DataTypeConverter.ToFhirCodeableConcept(rel.RelationshipTypeKey, "http://terminology.hl7.org/CodeSystem/v2-0131")
                            }.OfType<CodeableConcept>().ToList(),
                            Name = DataTypeConverter.ToFhirHumanName(person.Names?.FirstOrDefault()),
                            // TODO: Gender
                            Gender = DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(person.GenderConceptKey, "http://hl7.org/fhir/administrative-gender"),
                            Telecom = person.Telecoms?.Select(t => DataTypeConverter.ToFhirTelecom(t))?.ToList(),
                        };

                        var scoper = person.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper);
                        if (scoper != null)
                        {
                            contact.Organization = DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Organization>(scoper.LoadProperty(o => o.TargetEntity));
                        }
                        DataTypeConverter.AddExtensions(person, contact);
                        retVal.Contact.Add(contact);
                    }
                    else if (relEntity is Core.Model.Entities.Organization org) // it *IS* an organization
                    {
                        var contact = new Patient.ContactComponent()
                        {
                            ElementId = $"{org.Key}",
                            Relationship = new List<CodeableConcept>() {
                                DataTypeConverter.ToFhirCodeableConcept(rel.RelationshipRoleKey, "http://terminology.hl7.org/CodeSystem/v2-0131"),
                                DataTypeConverter.ToFhirCodeableConcept(rel.RelationshipTypeKey, "http://terminology.hl7.org/CodeSystem/v2-0131")
                            }.OfType<CodeableConcept>().ToList(),
                            Organization = DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Organization>(org),
                        };
                        retVal.Contact.Add(contact);
                    }
                }
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper)
                {
                    var scoper = rel.LoadProperty(o => o.TargetEntity);

                    retVal.ManagingOrganization = DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Organization>(scoper);

                    // If this is part of a bundle, include it
                    partOfBundle?.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = $"{MessageUtil.GetBaseUri()}/Organization/{scoper.Key}",
                        Resource = FhirResourceHandlerUtil.GetMapperForInstance(scoper).MapToFhir(scoper),
                    });
                }
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.HealthcareProvider)
                {
                    var practitioner = rel.LoadProperty(o => o.TargetEntity);

                    retVal.GeneralPractitioner.Add(DataTypeConverter.CreateVersionedReference<Practitioner>(practitioner));

                    // If this is part of a bundle, include it
                    partOfBundle?.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = $"{MessageUtil.GetBaseUri()}/Practitioner/{practitioner.Key}",
                        Resource = FhirResourceHandlerUtil.GetMapperForInstance(practitioner).MapToFhir(practitioner),
                    });
                }
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Replaces)
                {
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Replaces));
                }
                else if (rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Duplicate)
                {
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Seealso));
                }
                else if (rel.ClassificationKey == EntityRelationshipTypeKeys.EquivalentEntity)
                {
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Refer));
                }
                else if (partOfBundle != null) // This is part of a bundle and we need to include it
                {
                    // HACK: This piece of code is used to add any RelatedPersons to the container bundle if it is part of a bundle
                    if (this.GetRelatedPersonConceptUuids().Contains(rel.RelationshipTypeKey.Value))
                    {
                        var relative = FhirResourceHandlerUtil.GetMapperForInstance(rel).MapToFhir(rel);
                        partOfBundle.Entry.Add(new Bundle.EntryComponent()
                        {
                            FullUrl = $"{MessageUtil.GetBaseUri()}/RelatedPerson/{rel.Key}",
                            Resource = relative,
                        });
                    }
                }
            }

            // MDM links
            model.Relationships.FilterManagedReferenceLinks< SanteDB.Core.Model.Roles.Patient>().ToList().ForEach(rel =>
            {
                if (rel.SourceEntityKey.HasValue && rel.SourceEntityKey != model.Key)
                {
                    retVal.Link.Add(this.CreateLink<Patient>(rel.SourceEntityKey.Value, Patient.LinkType.Seealso));
                }
                else // Is a local
                {
                    retVal.Link.Add(this.CreateLink<Patient>(rel.TargetEntityKey.Value, Patient.LinkType.Refer));
                }
            });

            // Reverse relationships of family member?
            var uuids = model.Relationships.FilterManagedReferenceLinks<SanteDB.Core.Model.Roles.Patient>().Select(r => r.SourceEntityKey).Union(new Guid?[] { model.Key }).ToArray();
            var familyMemberConcepts = this.GetFamilyMemberUuids();
            var reverseRelationships = this.m_EntityRelationshipRepository.Find(o => uuids.Contains(o.TargetEntityKey) && familyMemberConcepts.Contains(o.RelationshipTypeKey.Value) && o.ObsoleteVersionSequenceId == null);
            foreach (var rrv in reverseRelationships)
            {
                retVal.Link.Add(new Patient.LinkComponent
                {
                    Type = Patient.LinkType.Seealso,
                    Other = DataTypeConverter.CreateNonVersionedReference<RelatedPerson>(rrv),
                });

                // If this is part of a bundle, include it
                partOfBundle?.Entry.Add(new Bundle.EntryComponent
                {
                    FullUrl = $"{MessageUtil.GetBaseUri()}/RelatedPerson/{rrv.Key}",
                    Resource = FhirResourceHandlerUtil.GetMappersFor(ResourceType.RelatedPerson).First().MapToFhir(rrv),
                });
            }

            // Was this record replaced?
            if (!retVal.Active.GetValueOrDefault())
            {
                var replacedRelationships = this.m_EntityRelationshipRepository.Find(o => uuids.Contains(o.TargetEntityKey) && o.RelationshipTypeKey == EntityRelationshipTypeKeys.Replaces && o.ObsoleteVersionSequenceId == null);

                foreach (var repl in replacedRelationships)
                {
                    retVal.Link.Add(new Patient.LinkComponent()
                    {
                        Type = Patient.LinkType.ReplacedBy,
                        Other = DataTypeConverter.CreateNonVersionedReference<Patient>(repl.LoadProperty(o => o.SourceEntity)),
                    });
                }
            }

            var photo = model.LoadCollection(o => o.Extensions).FirstOrDefault(o => o.ExtensionTypeKey == ExtensionTypeKeys.JpegPhotoExtension);

            if (photo != null)
            {
                retVal.Photo = new List<Attachment>
                {
                    new Attachment
                    {
                        ContentType = "image/jpg",
                        Data = photo.ExtensionValueData,
                    }
                };
            }

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
                    patient = new Core.Model.Roles.Patient
                    {
                        Key = key
                    };
                }
            }
            else if (resource.Identifier.Any())
            {
                foreach (var ii in resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier))
                {
                    if (ii.LoadProperty(o => o.IdentityDomain).IsUnique)
                    {
                        patient = this.m_repository.Find(o => o.Identifiers.Where(i => i.IdentityDomainKey == ii.IdentityDomainKey).Any(i => i.Value == ii.Value)).FirstOrDefault();
                    }
                    if (patient != null)
                    {
                        break;
                    }
                }

                if (patient == null)
                {
                    patient = new Core.Model.Roles.Patient
                    {
                        Key = Guid.NewGuid()
                    };
                }
            }
            else
            {
                patient = new Core.Model.Roles.Patient
                {
                    Key = Guid.NewGuid()
                };
            }

            patient.Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList();
            patient.CreationTime = DateTimeOffset.Now;
            patient.GenderConceptKey = resource.Gender == null ? null : DataTypeConverter.ToConcept(new Coding(FhirConstants.CodeSystem_AdministrativeGender, Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key;
            patient.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
            patient.LanguageCommunication = resource.Communication.Select(DataTypeConverter.ToLanguageCommunication).ToList();
            patient.Names = resource.Name.Select(DataTypeConverter.ToEntityName).ToList();
            patient.StatusConceptKey = resource.Active == null || resource.Active == true ? StatusKeys.Active : StatusKeys.Inactive;
            patient.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList();
            patient.Relationships = resource.Contact.Select(r => DataTypeConverter.ToEntityRelationship(r, resource)).ToList();
            patient.DateOfBirth = DataTypeConverter.ToDateTimeOffset(resource.BirthDate, out var dateOfBirthPrecision)?.DateTime;
            patient.LoadProperty(o=>o.Extensions).AddRange(resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, patient)).OfType<EntityExtension>());
            patient.Notes = DataTypeConverter.ToNote<EntityNote>(resource.Text);
            patient.Policies = resource.Meta?.Security?.Select(o => DataTypeConverter.ToSecurityPolicy(o)).ToList();
            patient.MaritalStatus = resource.MaritalStatus == null ? null : DataTypeConverter.ToConcept(resource.MaritalStatus);

            // TODO: fix
            // HACK: the date of birth precision CK only allows "Y", "M", or "D" for the precision value
            patient.DateOfBirthPrecision = dateOfBirthPrecision == DatePrecision.Full ? DatePrecision.Day : dateOfBirthPrecision;

            switch (resource.Deceased)
            {
                case FhirDateTime dtValue when !String.IsNullOrEmpty(dtValue.Value):
                    patient.DeceasedDate = DataTypeConverter.ToDateTimeOffset(dtValue.Value, out var datePrecision)?.DateTime;
                    // TODO: fix
                    // HACK: the deceased date precision CK only allows "Y", "M", or "D" for the precision value
                    patient.DeceasedDatePrecision = datePrecision == DatePrecision.Full ? DatePrecision.Day : datePrecision;
                    break;

                case FhirBoolean boolValue when boolValue.Value.GetValueOrDefault():
                    // we don't have a field for "deceased indicator" to say that the patient is dead, but we don't know that actual date/time of death
                    // should find a better way to do this
                    patient.DeceasedDate = DateTime.MinValue;
                    patient.DeceasedDatePrecision = DatePrecision.Year;
                    break;
            }

            switch (resource.MultipleBirth)
            {
                case FhirBoolean boolBirth when boolBirth.Value.GetValueOrDefault():
                    patient.MultipleBirthOrder = 0;
                    break;

                case Integer intBirth:
                    patient.MultipleBirthOrder = intBirth.Value;
                    break;
            }

            if (resource.GeneralPractitioner != null)
            {
                patient.Relationships.AddRange(resource.GeneralPractitioner.Select(r =>
                {
                    var referenceKey = DataTypeConverter.ResolveEntity<Core.Model.Roles.Provider>(r, resource) as Entity ?? DataTypeConverter.ResolveEntity<Core.Model.Entities.Organization>(r, resource);

                    if (referenceKey == null)
                    {
                        this.m_tracer.TraceError("Can't locate a registered general practitioner");
                        throw new KeyNotFoundException(m_localizationService.GetString("error.type.KeyNotFoundException.cannotLocateRegistered", new
                        {
                            param = "general practitioner"
                        }));
                    }

                    return new EntityRelationship(EntityRelationshipTypeKeys.HealthcareProvider, referenceKey);
                }));
            }
            if (resource.ManagingOrganization != null)
            {
                var referenceKey = DataTypeConverter.ResolveEntity<Core.Model.Entities.Organization>(resource.ManagingOrganization, resource);

                if (referenceKey == null)
                {
                    this.m_tracer.TraceError("Can't locate a registered managing organization");
                    throw new KeyNotFoundException(m_localizationService.GetString("error.type.KeyNotFoundException.cannotLocateRegistered", new
                    {
                        param = "managing organization"
                    }));
                }

                patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Scoper, referenceKey));
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
                            var replacee = DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(lnk.Other, resource);

                            if (replacee == null)
                            {
                                this.m_tracer.TraceError($"Cannot locate patient referenced by {lnk.Type} relationship");
                                throw new KeyNotFoundException(m_localizationService.GetString("error.messaging.fhir.patientResource.cannotLocatePatient", new
                                {
                                    param = lnk.Type
                                }));
                            }

                            replacee.StatusConceptKey = StatusKeys.Obsolete;
                            patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Replaces, replacee));
                            break;
                        }
                    case Patient.LinkType.ReplacedBy:
                        {
                            // Find the new
                            var replacer = DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(lnk.Other, resource);

                            if (replacer == null)
                            {
                                this.m_tracer.TraceError($"Cannot locate patient referenced by {lnk.Type} relationship");
                                throw new KeyNotFoundException(m_localizationService.GetString("error.messaging.fhir.patientResource.cannotLocatePatient", new
                                {
                                    param = lnk.Type
                                }));
                            }

                            patient.StatusConceptKey = StatusKeys.Obsolete;
                            patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Replaces, patient)
                            {
                                HolderKey = replacer.Key
                            });
                            break;
                        }
                    case Patient.LinkType.Seealso:
                        {
                            var referee = DataTypeConverter.ResolveEntity<Entity>(lnk.Other, resource); // We use Entity here in lieu Patient since the code below can handle the MDM layer

                            // Is this a current MDM link?
                            if (referee.GetTag(FhirConstants.PlaceholderTag) == "true") // The referee wants us to become the data
                            {
                                patient.Key = referee.Key;
                            }
                            else if (referee.LoadCollection(o => o.Relationships).FilterManagedReferenceLinks< SanteDB.Core.Model.Roles.Patient>().Any()
                                && referee.GetTag("$mdm.type") == "M") // HACK: This is a master and someone is attempting to point another record at it
                            {
                                var managedLink = referee.LoadCollection(o => o.Relationships).FilterManagedReferenceLinks<SanteDB.Core.Model.Roles.Patient>().First() as EntityRelationship;
                                patient.Relationships.Add(new EntityRelationship()
                                {
                                    RelationshipTypeKey = managedLink.RelationshipTypeKey,
                                    RelationshipRoleKey = managedLink.RelationshipRoleKey,
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
                            var referee = DataTypeConverter.ResolveEntity<Entity>(lnk.Other, resource);
                            if (referee.GetTag("$mdm.type") == "M") // HACK: MDM User is attempting to point this at another Master (note: THE MDM LAYER WON'T LIKE THIS)
                            {
                                patient.AddManagedReferenceLink(referee);
                            }
                            else
                            {
                                this.m_tracer.TraceError($"Setting refer relationships to source of truth is not supported");
                                throw new NotSupportedException(m_localizationService.GetString("error.type.NotSupportedException.userMessage"));
                            }

                            break; // TODO: These are special cases of references
                        }
                }
            }

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
                                    return resource.LoadProperty(o => o.Relationships)
                                        .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                            o.RelationshipTypeKey == EntityRelationshipTypeKeys.HealthcareProvider &&
                                            o.LoadProperty(r => r.TargetEntity) is Core.Model.Roles.Provider)
                                        .Select(o => rpHandler.MapToFhir(o.TargetEntity));

                                default:
                                    this.m_tracer.TraceError($"Cannot determine how to include {includeInstruction}");
                                    throw new InvalidOperationException(this.m_localizationService.GetString("error.type.InvalidOperation.cannotDetermine", new
                                    {
                                        param = includeInstruction
                                    }));
                            }
                        }
                    case ResourceType.Organization:
                        {
                            // Load all related persons and convert them
                            var rpHandler = FhirResourceHandlerUtil.GetMappersFor(ResourceType.Organization).FirstOrDefault();

                            switch (includeInstruction.JoinPath)
                            {
                                case "managingOrganization":
                                    return resource.LoadProperty(o => o.Relationships)
                                        .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                            o.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper &&
                                            o.LoadProperty(r => r.TargetEntity) is Core.Model.Entities.Organization)
                                        .Select(o => rpHandler.MapToFhir(o.TargetEntity));

                                case "generalPractitioner":
                                    return resource.LoadProperty(o => o.Relationships)
                                        .Where(o => o.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                            o.RelationshipTypeKey == EntityRelationshipTypeKeys.HealthcareProvider &&
                                            o.LoadProperty(r => r.TargetEntity) is Core.Model.Entities.Organization)
                                        .Select(o => rpHandler.MapToFhir(o.TargetEntity));

                                default:
                                    this.m_tracer.TraceError($"Cannot determine how to include {includeInstruction}");
                                    throw new InvalidOperationException(this.m_localizationService.GetString("error.type.InvalidOperation.cannotDetermine", new
                                    {
                                        param = includeInstruction
                                    }));
                            }
                        }
                    default:
                        this.m_tracer.TraceError($"{includeInstruction.Type} is not supported reverse include");
                        throw new InvalidOperationException(this.m_localizationService.GetString("error.type.NotSupportedException.userMessage"));
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
                                this.GetRelatedPersonConceptUuids().Contains(o.RelationshipTypeKey.Value))
                            .Select(o => rpHandler.MapToFhir(o));

                    default:
                        this.m_tracer.TraceError($"{includeInstruction.Type} is not supported reverse include");
                        throw new InvalidOperationException(this.m_localizationService.GetString("error.type.NotSupportedException.userMessage"));
                }
            });
        }
    }
}
#pragma warning restore
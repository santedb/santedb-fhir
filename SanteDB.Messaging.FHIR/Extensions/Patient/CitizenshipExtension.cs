using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Represents a citizenship extension handler.
    /// </summary>
    public class CitizenshipExtension : IFhirExtensionHandler
    {
        /// <summary>
        /// The place repository service.
        /// </summary>
        private readonly IRepositoryService<Place> m_placeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CitizenshipExtension"/> class.
        /// </summary>
        /// <param name="placeRepository">The place repository service.</param>
        public CitizenshipExtension(IRepositoryService<Place> placeRepository)
        {
            this.m_placeRepository = placeRepository;
        }

        /// <summary>
        /// Applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Get the profile URI
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-citizenship");

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IIdentifiedEntity modelObject)
        {
            if (modelObject is Core.Model.Roles.Patient patient)
            {
                // citizenship?
                foreach (var citizenshipExtension in patient.LoadCollection(o => o.Relationships).Where(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Citizen))
                {
                    var citizenPlace = citizenshipExtension.LoadProperty(o => o.TargetEntity);
                    var isoCode = citizenPlace.GetIdentifiers().FirstOrDefault(o => o.AuthorityKey == AssigningAuthorityKeys.Iso3166CountryCode);

                    if (isoCode != null)
                    {
                        yield return new Extension(this.Uri.ToString(), new CodeableConcept($"urn:oid:{isoCode.AuthorityXml.Oid}", isoCode.Value));
                    }
                }
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is Core.Model.Roles.Patient patient && fhirExtension.Value is CodeableConcept cc)
            {
                var isoCode = cc.Coding.FirstOrDefault(o => o.System == "urn:iso:std:iso:3166:1" || o.System == "urn:oid:1.0.3166.1.2.3");

                if (isoCode != null)
                {
                    var country = this.m_placeRepository.Find(o => o.Identifiers.Where(a => a.AuthorityKey == AssigningAuthorityKeys.Iso3166CountryCode).Any(i => i.Value == isoCode.Code) && StatusKeys.ActiveStates.Contains(o.StatusConceptKey.Value)).SingleOrDefault();

                    if (country != null && !patient.Relationships.Any(c => c.TargetEntityKey == country.Key))
                    {
                        patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Citizen, country));

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
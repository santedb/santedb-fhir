using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Implementation of the birthplace extension
    /// </summary>
    public class BirthPlaceExtension : IFhirExtensionHandler
    {
        // Place repository
        private IRepositoryService<SanteDB.Core.Model.Entities.Place> m_placeRepository;

        /// <summary>
        /// DI injection
        /// </summary>
        public BirthPlaceExtension(IRepositoryService<SanteDB.Core.Model.Entities.Place> placeRepository)
        {
            this.m_placeRepository = placeRepository;
        }

        /// <summary>
        /// URI which appears on the extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-birthPlace");

        /// <summary>
        /// Profile URI
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Resource this extension applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IIdentifiedEntity modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient)
            {
                // Birthplace?
                var birthPlaceRelationship = patient.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace);
                if (birthPlaceRelationship != null)
                {
                    var address = DataTypeConverter.ToFhirAddress(birthPlaceRelationship.LoadProperty(o => o.TargetEntity).LoadCollection(o => o.Addresses)?.FirstOrDefault());
                    address.Text = birthPlaceRelationship.TargetEntity.LoadCollection(o => o.Names)?.FirstOrDefault()?.ToDisplay();
                    yield return new Extension(this.Uri.ToString(), address);
                }
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && fhirExtension.Value is Hl7.Fhir.Model.Address address)
            {
                // TODO: Cross reference birthplace to an entity relationship (see the HL7v2 PID segment handler for example)
            }
            return false;
        }
    }
}
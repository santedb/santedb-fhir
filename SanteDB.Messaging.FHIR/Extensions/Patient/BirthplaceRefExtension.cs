using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Birthplace explicit reference
    /// </summary>
    [DisplayName("Birthplace explicit reference extension")]
    public class BirthplaceRefExtension : IFhirExtensionHandlerEx
    {

        // Place repository
        private IRepositoryService<SanteDB.Core.Model.Entities.Place> m_placeRepository;

        /// <inhertidoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.Reference;

        /// <summary>
        /// DI injection
        /// </summary>
        public BirthplaceRefExtension(IRepositoryService<SanteDB.Core.Model.Entities.Place> placeRepository)
        {
            this.m_placeRepository = placeRepository;
        }


        /// <summary>
        /// URI which appears on the extension
        /// </summary>
        public Uri Uri => new Uri("http://santedb.org/fhir/profile#patient-birthPlace-ref");

        /// <summary>
        /// Profile URI
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Resource this extension applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <inheritdic/>
        public bool IsModifier => false;

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient)
            {
                // Birthplace?
                var birthPlaceRelationship = patient.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace);
                if (birthPlaceRelationship != null)
                {
                    yield return new Extension(this.Uri.ToString(), DataTypeConverter.CreateRimReference(birthPlaceRelationship.TargetEntity));
                }
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && fhirExtension.Value is ResourceReference rr)
            {

                // TODO: Update this to use the new more efficient method of getting data from database
                // Something like: Query by names, then order by the address hierarchy?
                var birthPlaceRelationship = patient.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace);
                var birthPlace = DataTypeConverter.ResolveEntity<Place>(rr, fhirExtension.Annotation<Hl7.Fhir.Model.Patient>());
                if (birthPlace == null)
                {
                    throw new ArgumentOutOfRangeException($"Cannot find {rr.Reference}");
                }
                else if (birthPlaceRelationship != null)
                {
                    birthPlaceRelationship.TargetEntityKey = birthPlace.Key;
                    return true;
                }
                else
                {
                    patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Birthplace, birthPlace.Key));
                    return true;
                }
            }
            return false;
        }
    }
}

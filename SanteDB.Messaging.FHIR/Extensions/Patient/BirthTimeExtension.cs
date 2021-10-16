using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Birth-time extension
    /// </summary>
    public class BirthTimeExtension : IFhirExtensionHandler
    {
        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-birthTime");

        /// <summary>
        /// Gets the profile definition
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Gets the resource type this applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Construct the extentsion
        /// </summary>
        public IEnumerable<Extension> Construct(IIdentifiedEntity modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Entities.Person person && person.DateOfBirthPrecision > Core.Model.DataTypes.DatePrecision.Day)
            {
                yield return new Extension(this.Uri.ToString(), new FhirDateTime(person.DateOfBirth.Value));
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (fhirExtension.Value is FhirDateTime dateTime && modelObject is SanteDB.Core.Model.Entities.Person person)
            {
                person.DateOfBirth = dateTime.ToDateTime();
                person.DateOfBirthPrecision = Core.Model.DataTypes.DatePrecision.Full;
                return true;
            }
            return false;
        }
    }
}
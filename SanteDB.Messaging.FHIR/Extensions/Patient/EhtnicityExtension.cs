using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Patient ehtnicity
    /// </summary>
    public class EhtnicityExtension : IFhirExtensionHandler
    {
        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/patient-ethnicity");

        /// <inheritdoc/>
        public Uri ProfileUri => this.Uri;

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is Core.Model.Roles.Patient patient && patient.EthnicGroupKey.HasValue)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirCodeableConcept(patient.EthnicGroupKey ?? patient.EthnicGroup?.Key));
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && fhirExtension.Value is CodeableConcept cc)
            {
                patient.EthnicGroup = DataTypeConverter.ToConcept(cc);
                return true;
            }

            return false;
        }
    }
}

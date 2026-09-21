using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Occupation
    /// </summary>
    [DisplayName("Patient Occupation")]
    [Description("When enabled, provides a codified representation of the Patient's occupation ")]
    public class OccupationExtension : IFhirExtensionHandlerEx
    {
        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/patient-occupation");

        /// <inheritdoc/>
        public Uri ProfileUri => this.Uri;

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <inhertidoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.CodeableConcept;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is Core.Model.Entities.Person person && person.OccupationKey.HasValue)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirCodeableConcept(person.OccupationKey ?? person.Occupation?.Key));
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Entities.Person person && fhirExtension.Value is CodeableConcept cc)
            {
                person.Occupation = DataTypeConverter.ToConcept(cc);
                return true;
            }

            return false;
        }
    }
}

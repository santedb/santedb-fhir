using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Medication
{
    /// <summary>
    /// Identifies the classification of the medication
    /// </summary>
    [DisplayName("Medication Classification")]
    [System.ComponentModel.Description("Identifies whether this Medication entry represents a `Kind` of medication, a `Specific` instnace of Medication")]
    public class ProductClassExtensionTypeHandler : IFhirExtensionHandlerEx
    {
        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/Medication/classification");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Medication;

        /// <inhertidoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.CodeableConcept;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is Material mat)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirCodeableConcept(mat.DeterminerConceptKey));
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is Material mat && fhirExtension.Value is CodeableConcept cc)
            {
                mat.DeterminerConcept = DataTypeConverter.ToConcept(cc);
                return true;
            }
            return false;
        }
    }
}

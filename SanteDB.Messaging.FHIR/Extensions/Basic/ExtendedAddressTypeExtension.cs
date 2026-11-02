using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Basic
{
    /// <summary>
    /// Extended telecome address type extension
    /// </summary>
    public class ExtendedAddressTypeExtension : IFhirExtensionHandlerEx
    {

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/base/ContactPoint-type");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.CodeableConcept;

        /// <inheritdoc/>
        public bool IsModifier => false;

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Basic;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is EntityTelecomAddress eta && 
                eta.TypeConceptKey.HasValue &&
                DataTypeConverter.ToFhirEnumeration<ContactPoint.ContactPointSystem>(eta.TypeConceptKey, "http://hl7.org/fhir/contact-point-system") == null)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirCodeableConcept(eta.TypeConceptKey));
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(fhirExtension.Value is CodeableConcept cc &&
                modelObject is EntityTelecomAddress eta)
            {
                eta.TypeConceptKey = DataTypeConverter.ToConcept(cc)?.Key;
                return true;
            }
            return false;
        }
    }
}

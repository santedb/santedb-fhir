using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Location
{
    /// <summary>
    /// FHIR extension handler for mobile locations
    /// </summary>
    public class LocationMobileIndicatorExtension : IFhirExtensionHandler
    {
        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/Location/mobileIndicator");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Location;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is Place plc)
            {
                yield return new Extension(this.Uri.ToString(), new FhirBoolean(plc.IsMobile));
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(fhirExtension.Value is FhirBoolean fb && modelObject is Place plc)
            {
                plc.IsMobile = fb.Value.GetValueOrDefault();
                return true;
            }
            return false;
        }
    }
}

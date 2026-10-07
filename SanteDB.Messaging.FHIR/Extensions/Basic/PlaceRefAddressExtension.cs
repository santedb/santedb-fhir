using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Basic
{
    /// <summary>
    /// Represents ISO21090 address extensions
    /// </summary>
    public class PlaceRefAddressExtension : IFhirExtensionHandlerEx
    {
       
        /// <inheritdoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.Reference;

        /// <inheritdoc/>
        public bool IsModifier => false;

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/address-PlaceRef");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Basic;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is EntityAddress addr)
            {
                var addrCmp = addr.LoadCollection(o => o.Component).FirstOrDefault(o => o.ComponentTypeKey == AddressComponentKeys.PlaceRef);
                if (addrCmp != null)
                {
                    yield return new Extension(this.Uri.ToString(), new ResourceReference($"Location/{addrCmp.Value}"));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (fhirExtension.Value is FhirString fhs && modelObject is EntityAddress addr)
            {
                addr.LoadCollection(o => o.Component);
                addr.Component.Add(new EntityAddressComponent(AddressComponentKeys.PlaceRef, fhs.Value));
                return true;
            }
            return false;
        }

    }
}
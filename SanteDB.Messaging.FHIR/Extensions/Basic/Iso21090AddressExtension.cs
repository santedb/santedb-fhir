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
    public abstract class Iso21090AddressExtension : IFhirExtensionHandlerEx
    {

        private readonly Guid m_componentType;
        private readonly String m_addressPartName;

        protected Iso21090AddressExtension(String partName, String extensionSuffix = null)
        {
            this.m_addressPartName = extensionSuffix ?? partName.Uncapitalize();

            var field = typeof(AddressComponentKeys).GetField(partName.Capitalize(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if(field == null)
            {
                throw new ArgumentOutOfRangeException(nameof(partName), partName);
            }

            this.m_componentType = (Guid)field.GetValue(null);
        }

        /// <inheritdoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.String;

        /// <inheritdoc/>
        public bool IsModifier => false;

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.Iso21090Extensions}-ADXP-{this.m_addressPartName}");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.Iso21090Extensions);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Basic;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is EntityAddress addr)
            {
                var addrCmp = addr.LoadCollection(o => o.Component).FirstOrDefault(o => o.ComponentTypeKey == this.m_componentType);
                if(addrCmp != null)
                {
                    yield return new Extension(this.Uri.ToString(), new FhirString(addrCmp.Value));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(fhirExtension.Value is FhirString fhs && modelObject is EntityAddress addr)
            {
                addr.LoadCollection(o => o.Component);
                addr.Component.Add(new EntityAddressComponent(this.m_componentType, fhs.Value));
                return true;
            }
            return false;
        }

    }

    /// <inheritdoc/>
    public class Iso21090AddressExtensionBuildNumberNumeric : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionBuildNumberNumeric() : base(nameof(AddressComponentKeys.BuildingNumberNumeric)) { }
    }


    /// <inheritdoc/>
    public class Iso21090AddressExtensionCensusTract : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionCensusTract() : base(nameof(AddressComponentKeys.CensusTract)) { }
    }

    /// <inheritdoc/>
    public class Iso21090AddressExtensionUnitId : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionUnitId() : base(nameof(AddressComponentKeys.UnitIdentifier), "unitID") { }
    }
    
    /// <inheritdoc/>
    public class Iso21090AddressExtensionStreetName : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionStreetName() : base(nameof(AddressComponentKeys.StreetName)) { }
    }

    /// <inheritdoc/>
    public class Iso21090AddressExtensionPrecinct : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionPrecinct() : base(nameof(AddressComponentKeys.Precinct)) { }
    }

    /// <inheritdoc/>
    public class Iso21090AddressExtensionPostBox : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionPostBox() : base(nameof(AddressComponentKeys.PostBox)) { }
    }

    /// <inheritdoc/>
    public class Iso21090AddressExtensionCareOf : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionCareOf() : base(nameof(AddressComponentKeys.CareOf)) { }
    }

    /// <inheritdoc/>
    public class Iso21090AddressExtensionAdditionalLocator : Iso21090AddressExtension
    {
        public Iso21090AddressExtensionAdditionalLocator() : base(nameof(AddressComponentKeys.AdditionalLocator)) { }
    }
}


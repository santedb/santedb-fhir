using Hl7.Fhir.Model;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Medication
{
    /// <summary>
    /// Trade name for a medication
    /// </summary>
    [DisplayName("Trade/Common Names")]
    [System.ComponentModel.Description("Common or trade name(s) for the medication")]
    public class NameExtensionHandler : IFhirExtensionHandlerEx
    {
        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/Medication/name");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Medication;

        /// <inhertidoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.String;


        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is ManufacturedMaterial mmat && mmat.LoadProperty(o=>o.Names).Any())
            {
                // Grab the instance relationship source
                yield return new Extension()
                {
                    Url = this.Uri.ToString(),
                    Value = new FhirString(mmat.LoadProperty(o => o.Names).FirstOrDefault().ToDisplay())
                };
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(fhirExtension.Value is FhirString fstr && modelObject is ManufacturedMaterial mmat)
            {
                mmat.Names = new List<EntityName>()
                {
                    new EntityName(NameUseKeys.Assigned, fstr.Value)
                };
                return true;
            }
            return false;
        }
    }
}

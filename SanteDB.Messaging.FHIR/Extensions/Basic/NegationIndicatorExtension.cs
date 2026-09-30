using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Basic
{
    /// <summary>
    /// ISO 21090 Null Flavor Extension - https://hl7.org/fhir/R4B/extension-iso21090-nullflavor.html
    /// </summary>
    [DisplayName("Used for conveying NegationInd")]
    [Description("When an action or an observation is negated (i.e. inverse) this is conveyed")]
    public class NegationIndicatorExtension : IFhirExtensionHandlerEx
    {

        /// <inheritdoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.Boolean;

        /// <inheritdoc/>
        public bool IsModifier => true;

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/base/isNegated");

        /// <inheritdoc/>
        public Uri ProfileUri => this.Uri;

        /// <inheritdoc/>
        public ResourceType? AppliesTo => null;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is Act act && act.IsNegated)
            {
                yield return new Extension(this.Uri.ToString(), new FhirBoolean(true));
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(modelObject is Act act && fhirExtension.Value is FhirBoolean fb)
            {
                act.IsNegated = fb.Value.GetValueOrDefault();
                return true;
            }
            return false;
        }
    }
}

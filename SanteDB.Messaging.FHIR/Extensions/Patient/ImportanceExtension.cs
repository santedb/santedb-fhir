﻿using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Religion extension handler
    /// </summary>
    public class ImportanceExtension : IFhirExtensionHandler
    {
        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-importance");

        /// <summary>
        /// Get the profile URI
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IIdentifiedEntity modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && patient.VipStatusKey.HasValue)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirCodeableConcept(patient.LoadProperty(o => o.VipStatus)));
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && fhirExtension.Value is CodeableConcept cc)
            {
                patient.VipStatus = DataTypeConverter.ToConcept(cc);
            }
            return true;
        }
    }
}
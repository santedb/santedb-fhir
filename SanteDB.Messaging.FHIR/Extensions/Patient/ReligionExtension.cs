/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2023-6-21
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Religion extension handler
    /// </summary>
    public class ReligionExtension : IFhirExtensionHandler
    {
        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-religion");

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
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && patient.ReligiousAffiliationKey.HasValue)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirCodeableConcept(patient.ReligiousAffiliationKey));
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && fhirExtension.Value is CodeableConcept cc)
            {
                patient.ReligiousAffiliation = DataTypeConverter.ToConcept(cc);
                return true;
            }

            return false;
        }
    }
}
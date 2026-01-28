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
 * Date: 2025-1-13
 */
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

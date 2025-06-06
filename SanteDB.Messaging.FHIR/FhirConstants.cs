﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using System;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// FHIR constants
    /// </summary>
    public static class FhirConstants
    {


        /// <summary>
        /// URI of the SanteDB FHIR profile
        /// </summary>
        public static String SanteDBProfile = "http://santedb.org/fhir/profile";

        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string ConfigurationSectionName = "santedb.messaging.fhir";

        /// <summary>
        /// Trace source name
        /// </summary>
        public const string TraceSourceName = "SanteDB.Messaging.FHIR";

        /// <summary>
        /// Original URL
        /// </summary>
        public const string OriginalUrlTag = "$fhir.originalUrl";

        /// <summary>
        /// Origina ID
        /// </summary>
        public const string OriginalIdTag = "$fhir.originalId";

        /// <summary>
        /// The object is empty and should be processed / updated at a later time
        /// </summary>
        public const string PlaceholderTag = "$fhir.placeholder";

        /// <summary>
        /// Provenance header name
        /// </summary>
        public const string ProvenanceHeaderName = "X-Provenance";

        /// <summary>
        /// Default quantity concept system
        /// </summary>
        public const string DefaultQuantityUnitSystem = "http://unitsofmeasure.org";

        /// <summary>
        /// Default category concepts
        /// </summary>
        public const string DefaultObservationCategorySystem = "http://terminology.hl7.org/CodeSystem/observation-category";

        /// <summary>
        /// Security policy applied
        /// </summary>
        public const string SecurityPolicySystem = "http://santedb.org/security/policy";

        /// <summary>
        /// Dispatcher class setting name
        /// </summary>
        public const string DispatcherClassSettingName = "$authenticationProvider";
    }
}

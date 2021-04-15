﻿/*
 * Portions Copyright 2019-2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE)
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
 * User: fyfej (Justin Fyfe)
 * Date: 2019-11-27
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

    }
}

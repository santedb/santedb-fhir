/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using Hl7.Fhir.Rest;
using SanteDB.Messaging.FHIR.Configuration;
using System;
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR.Rest
{
    /// <summary>
    /// Authenticator implementation used to take a settings array and a configuration and authenticate the client
    /// </summary>
    public interface IFhirClientAuthenticator
    {

        /// <summary>
        /// Gets the configuration name of the authenticator
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Add authentication headers to the specified client
        /// </summary>
        /// <param name="client">The FHIR client to which the authentication headers should be added</param>
        /// <param name="userName">The username to use for authentication</param>
        /// <param name="password">The password to use for authentication</param>
        /// <param name="additionalSettings">Additional settings that should be added to the authentication</param>
        void AddAuthenticationHeaders(FhirClient client, string userName, string password, IDictionary<String, String> additionalSettings);
    }
}

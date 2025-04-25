/*
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
 * Date: 2025-2-3
 */
using Hl7.Fhir.Rest;
using SanteDB.Messaging.FHIR.Rest;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Authenticators
{
    /// <summary>
    /// Authenticator for HTTP basic
    /// </summary>
    public class BasicAuthenticator : IFhirClientAuthenticator
    {
        public const string UserNameSettingName = "user";
        public const string PasswordSettingName = "password";

        /// <inheritdoc/>
        public string Name => "basic";

        /// <inheritdoc/>
        public void AddAuthenticationHeaders(FhirClient client, string userName, string password, IDictionary<string, string> additionalSettings)
        {

            _ = String.IsNullOrEmpty(userName) ? additionalSettings.TryGetValue(UserNameSettingName, out userName) : false;
            _ = String.IsNullOrEmpty(password) ? additionalSettings.TryGetValue(UserNameSettingName, out password) : false;

            // Add to header
            var authnData = Encoding.UTF8.GetBytes($"{userName}:{password}");
            client.RequestHeaders.Remove("Authorization");
            client.RequestHeaders.Add("Authorization", $"basic {Convert.ToBase64String(authnData)}");
        }
    }
}

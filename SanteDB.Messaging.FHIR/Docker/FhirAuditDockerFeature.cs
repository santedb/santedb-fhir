/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-10-29
 */
using SanteDB.Core.Configuration;
using SanteDB.Docker.Core;
using SanteDB.Messaging.FHIR.Auditing;
using SanteDB.Messaging.FHIR.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Docker
{
    /// <summary>
    /// A Docker feature flag which enables the FHIR audit dispatcher
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class FhirAuditDockerFeature : IDockerFeature
    {
        /// <summary>
        /// Gets the identifier of the feature
        /// </summary>
        public string Id => "FHIR_AUDIT";

        /// <summary>
        /// Setting ID for resources
        /// </summary>
        public const string EndpointSetting = "EP";
        
        /// <summary>
        /// Message settings
        /// </summary>
        public const string UserSetting = "UN";

        /// <summary>
        /// Operation settings
        /// </summary>
        public const string PasswordSetting = "PWD";
        /// <summary>
        /// Profile settings
        /// </summary>
        public const string AuthenticatorSetting = "AUTHN";

        /// <summary>
        /// Gets the settings permitted
        /// </summary>
        public IEnumerable<string> Settings => new string[] { AuthenticatorSetting, EndpointSetting, UserSetting, PasswordSetting };

        /// <summary>
        /// Configure the feature
        /// </summary>
        public void Configure(SanteDBConfiguration configuration, IDictionary<string, string> settings)
        {
            var fhirConfiguration = configuration.GetSection<FhirDispatcherConfigurationSection>();
            if (fhirConfiguration == null)
            {
                fhirConfiguration = new FhirDispatcherConfigurationSection()
                {
                };
                configuration.AddSection(fhirConfiguration);
            }

            if(!settings.TryGetValue(EndpointSetting, out string endpoint))
            {
                throw new ArgumentNullException($"SDB_FHIR_AUDIT_EP is required");
            }
            var dispatcher = new FhirDispatcherTargetConfiguration();
            dispatcher.Name = "audit";
            dispatcher.Endpoint = endpoint;

            if(settings.TryGetValue(UserSetting, out string user) && settings.TryGetValue(PasswordSetting, out string password))
            {
                dispatcher.UserName = user;
                dispatcher.Password = password;
            }

            if(settings.TryGetValue(AuthenticatorSetting, out string authenticator))
            {
                dispatcher.Authenticator = new TypeReferenceConfiguration(authenticator);
            }

            fhirConfiguration.Targets.Add(dispatcher);

            // Add services
            var serviceConfiguration = configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders;
            if (!serviceConfiguration.Any(s => s.Type == typeof(FhirAuditDispatcher)))
            {
                serviceConfiguration.Add(new TypeReferenceConfiguration(typeof(FhirAuditDispatcher)));
            }
        }
    }
}

/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-5
 */
using Hl7.Fhir.Rest;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Rest;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Text;
using SanteDB.Core.Auditing;

namespace SanteDB.Messaging.FHIR.Auditing
{
    /// <summary>
    /// Audit dispatch service which sends audits using HL7 FHIR
    /// </summary>
    public class FhirAuditDispatcher : IAuditDispatchService
    {

        // Get tracer for the audit dispatcher
        private Tracer m_tracer = Tracer.GetTracer(typeof(FhirAuditDispatcher));

        // Configuration for the audit dispatcher
        private FhirDispatcherTargetConfiguration m_configuration;

        // Fhir Client
        private FhirClient m_client;

        // Gets the authenticator to use for this object
        private IFhirClientAuthenticator m_authenticator;

        /// <summary>
        /// Creates a new audit dispatcher
        /// </summary>
        public FhirAuditDispatcher(IConfigurationManager configurationManager, IServiceManager serviceManager)
        {
            this.m_configuration = configurationManager.GetSection<FhirDispatcherConfigurationSection>().Targets.Find(o => o.Name.Equals("audit", StringComparison.OrdinalIgnoreCase));
            if (this.m_configuration == null)
            {
                throw new InvalidOperationException("Cannot find a dispatcher configuration named Audit");
            }

            // The client for this object
            this.m_client = new FhirClient(this.m_configuration.Endpoint, new FhirClientSettings()
            {
                ParserSettings = new Hl7.Fhir.Serialization.ParserSettings()
                {
                    AllowUnrecognizedEnums = true,
                    PermissiveParsing = true
                },
                PreferredFormat = ResourceFormat.Xml,
                PreferCompressedResponses = true,
                VerifyFhirVersion = false
            });
            this.m_client.PreferredFormat = ResourceFormat.Json;
            this.m_client.ParserSettings = new Hl7.Fhir.Serialization.ParserSettings()
            {
                AcceptUnknownMembers = true,
                AllowUnrecognizedEnums = true
            };

            // Attach authenticator
            if (this.m_configuration.Authenticator?.Type != null)
            {
                this.m_authenticator = serviceManager.CreateInjected(this.m_configuration.Authenticator.Type) as IFhirClientAuthenticator;
                this.m_authenticator.AttachClient(this.m_client, this.m_configuration, null);
            }
        }

        /// <summary>
        /// Gets the name of the service
        /// </summary>
        public string ServiceName => "HL7 FHIR Audit Dispatch Service";

        /// <summary>
        /// Send an audit to the repository
        /// </summary>
        public void SendAudit(AuditData audit)
        {
            try
            {
                var fhirAudit = DataTypeConverter.ToSecurityAudit(audit);
                this.m_client.Create(fhirAudit);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error dispatching FHIR Audit - {0}", e.Message);
            }
        }
    }
}

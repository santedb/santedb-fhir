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
using SanteDB.Core.Services;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Rest;
using SanteDB.Messaging.FHIR.Rest.Behavior;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SanteDB.Core.Diagnostics;
using System.Diagnostics.Tracing;
using SanteDB.Core.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using SanteDB.Core.Security;
using System.ComponentModel;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// Message handler for FHIR
    /// </summary>
    [Description("Allows SanteDB iCDR to send and receive HL7 FHIR R4 messages")]
    [ApiServiceProvider("HL7 FHIR R4 API Endpoint", typeof(FhirServiceBehavior), configurationType: typeof(FhirServiceConfigurationSection))]
    public class FhirMessageHandler : IDaemonService, IApiEndpointProvider
    {

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "HL7 FHIR R3 API Endpoint";

        /// <summary>
        /// Gets the contract type
        /// </summary>
        public Type BehaviorType => typeof(FhirServiceBehavior);

        #region IMessageHandlerService Members

        private Tracer m_traceSource = new Tracer(FhirConstants.TraceSourceName);

        // Configuration
        private FhirServiceConfigurationSection m_configuration;

        // Web host
        private RestService m_webHost;

        // Service manager
        private IServiceManager m_serviceManager;

        /// <summary>
        /// Fired when the FHIR message handler is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the FHIR message handler is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when the FHIR message handler has started 
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the FHIR message handler has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Constructor, load configuration
        /// </summary>
        public FhirMessageHandler(IServiceManager serviceManager)
        {
            this.m_serviceManager = serviceManager;
            this.m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<FhirServiceConfigurationSection>();
        }

        /// <summary>
        /// Start the FHIR message handler
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            // This service relies on other services so let's wait until the entire context starts

            ApplicationServiceContext.Current.Started += (o, evt) =>
            {
                try
                {

                    using (AuthenticationContext.EnterSystemContext())
                    {
                        this.m_webHost = ApplicationServiceContext.Current.GetService<IRestServiceFactory>().CreateService(typeof(FhirServiceBehavior));
                        this.m_webHost.AddServiceBehavior(new FhirErrorEndpointBehavior());
                        foreach (var endpoint in this.m_webHost.Endpoints)
                        {
                            endpoint.AddEndpointBehavior(new FhirMessageDispatchFormatterEndpointBehavior());
                            this.m_traceSource.TraceInfo("Starting FHIR on {0}...", endpoint.Description.ListenUri);
                        }

                        MessageUtil.SetBaseLocation(this.m_configuration.ResourceBaseUri ?? this.m_webHost.Endpoints.First().Description.ListenUri.ToString());
                        FhirResourceHandlerUtil.Initialize(this.m_configuration, this.m_serviceManager);
                        ExtensionUtil.Initialize(this.m_configuration);

                        // Start the web host
                        this.m_webHost.Start();

                        this.m_traceSource.TraceInfo("FHIR On: {0}", this.m_webHost.Endpoints.First().Description.ListenUri);
                    }

                }
                catch (Exception e)
                {
                    this.m_traceSource.TraceEvent(EventLevel.Error, e.ToString());
                    throw;
                }
            };

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stop the FHIR message handler
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            if (this.m_webHost != null)
            {
                this.m_webHost.Stop();
                this.m_webHost = null;
            }

            this.Stopped?.Invoke(this, EventArgs.Empty);

            return true;
        }

        #endregion

        /// <summary>
        /// True if the FHIR message handler is active and running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return this.m_webHost != null;
            }
        }

        /// <summary>
        /// Endpoint API type
        /// </summary>
        public ServiceEndpointType ApiType => ServiceEndpointType.Hl7FhirInterface;

        /// <summary>
        /// Url 
        /// </summary>
        public string[] Url => this.m_webHost.Endpoints.Select(o=>o.Description.ListenUri.ToString()).ToArray();

        /// <summary>
        /// Capabilities 
        /// </summary>
        public ServiceEndpointCapabilities Capabilities
        {
            get
            {
                return (ServiceEndpointCapabilities)ApplicationServiceContext.Current.GetService<IRestServiceFactory>().GetServiceCapabilities(this.m_webHost);
            }
        }

    }
}

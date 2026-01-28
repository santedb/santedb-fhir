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
 * Date: 2023-7-12
 */
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using RestSrvr;
using RestSrvr.Message;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Security.Signing;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Rest.Behaviors
{
    /// <summary>
    /// This implementation of an endpoint behavior is responsible for interpreting the X-Provenance header, validating
    /// digital signatures, and 
    /// </summary>
    public class FhirProvenanceHeaderBehavior : IMessageInspector, IEndpointBehavior
    {
        private readonly FhirProvenanceHeaderConfiguration m_settings;
        private readonly ISecurityRepositoryService m_securityRespository;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FhirProvenanceHeaderBehavior));
        private readonly IDataSigningService m_dataSigningService;

        /// <summary>
        /// Provenance header configuration
        /// </summary>
        [XmlRoot(nameof(FhirProvenanceHeaderConfiguration), Namespace = "http://santedb.org/configuration")]
        [XmlType(nameof(FhirProvenanceHeaderConfiguration), Namespace = "http://santedb.org/configuration")]
        public class FhirProvenanceHeaderConfiguration
        {

            /// <summary>
            /// Gets the methods which the provenance header is required for
            /// </summary>
            [XmlAttribute("required")]
            public String[] RequiredMethods { get; set; }

            /// <summary>
            /// Gets the methods which the provenance header is forbidden
            /// </summary>
            [XmlAttribute("forbidden")]
            public String[] ForbiddenMethods { get; set; }

            /// <summary>
            /// Validate agents
            /// </summary>
            [XmlAttribute("validateAgents")]
            public bool ValidateAgents { get; set; }

            /// <summary>
            /// Validate the digital signatures
            /// </summary>
            [XmlAttribute("validateSignatures")]
            public bool ValidateSignatures { get; set; }
        }

        /// <summary>
        /// Provennace header ctor
        /// </summary>
        public FhirProvenanceHeaderBehavior(XElement xe)
        {
            this.m_securityRespository = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();
            this.m_dataSigningService = ApplicationServiceContext.Current.GetService<IDataSigningService>();
            if (xe != null)
            {
                using (var sr = new StringReader(xe.ToString()))
                {
                    this.m_settings = XmlModelSerializerFactory.Current.CreateSerializer(typeof(FhirProvenanceHeaderConfiguration)).Deserialize(sr) as FhirProvenanceHeaderConfiguration;
                }
            }
        }

        /// <inheritdoc/>
        public void AfterReceiveRequest(RestRequestMessage request)
        {
            var provenanceHeaderData = request.Headers[FhirConstants.ProvenanceHeaderName];
            var hasProvenanceHeaderData = !String.IsNullOrEmpty(provenanceHeaderData);

            if (this.m_settings?.ForbiddenMethods.Contains(request.Method) == true && hasProvenanceHeaderData)
            {
                throw new NotSupportedException(String.Format(ErrorMessages.FORBIDDEN_HEADER, FhirConstants.ProvenanceHeaderName));
            }
            else if (this.m_settings?.RequiredMethods.Contains(request.Method) == true && !hasProvenanceHeaderData)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.REQUIRED_HEADER, FhirConstants.ProvenanceHeaderName));
            }

            if (hasProvenanceHeaderData)
            {
                var parser = new FhirJsonParser();
                using (var sr = new StreamReader(request.Body))
                using (var jr = new JsonTextReader(sr))
                {
                    var provenanceData = parser.Parse<Provenance>(jr);

                    RestOperationContext.Current.Data.Add(FhirConstants.ProvenanceHeaderName, provenanceData);

                    if (this.m_settings.ValidateAgents)
                    {
                        foreach (var agent in provenanceData.Agent)
                        {
                            if (agent.Who == null)
                            {
                                throw new ArgumentException(ErrorMessages.DEPENDENT_PROPERTY_NULL, nameof(agent.Who));
                            }

                            var entity = DataTypeConverter.ResolveEntity<Entity>(agent.Who, provenanceData);
                            if (entity == null)
                            {
                                throw new KeyNotFoundException(String.Format(ErrorMessages.REFERENCE_NOT_FOUND, agent.Who.Identifier));
                            }

                            // Assert that the user is who they claim to be
                            switch (entity)
                            {
                                case Provider p1:
                                case UserEntity p2:
                                case Core.Model.Roles.Patient p3:
                                case Core.Model.Entities.Person p4:
                                    var cdrAuthenticatedObject = this.m_securityRespository.GetUserEntity(AuthenticationContext.Current.GetUserIdentity()); // TODO: Change this to the more generic method
                                    if (cdrAuthenticatedObject.Key != entity.Key &&
                                        !entity.LoadCollection(o => o.Relationships).Any(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.EquivalentEntity && o.TargetEntityKey == entity.Key))
                                    {
                                        throw new InvalidOperationException(String.Format(ErrorMessages.ASSERTION_MISMATCH, entity.Key, cdrAuthenticatedObject.Key));
                                    }
                                    break;
                                case DeviceEntity dev:
                                    var cdrAuthenticatedDevice = this.m_securityRespository.GetDevice(AuthenticationContext.Current.GetDeviceIdentity());
                                    if (cdrAuthenticatedDevice.Key != dev.SecurityDeviceKey)
                                    {
                                        throw new InvalidOperationException(String.Format(ErrorMessages.ASSERTION_MISMATCH, dev.LoadProperty(o => o.SecurityDevice).Name, cdrAuthenticatedDevice.Name));
                                    }
                                    break;
                                default:
                                    throw new NotSupportedException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Entity), entity.GetType()));
                            }
                        }
                    }

                    // Validate signature(s)
                    if (this.m_settings.ValidateSignatures)
                    {
                        foreach (var sig in provenanceData.Signature)
                        {
                            if (sig.SigFormat == "application/jose" && JsonWebSignature.TryParseDetached(Encoding.UTF8.GetString(sig.Data), out var jsonWebSignature) == JsonWebSignatureParseResult.Success)
                            {
                                if (this.m_dataSigningService.TryGetSignatureSettings(jsonWebSignature.Header, out var settings))
                                {
                                    using (request.Body)
                                    {
                                        var ms = new MemoryStream();
                                        request.Body.CopyTo(ms);
                                        // Compute a digital signature 
                                        if (!this.m_dataSigningService.Verify(ms.ToArray(), jsonWebSignature.Signature, settings))
                                        {
                                            throw new InvalidOperationException(ErrorMessages.SIGNATURE_VALIDATION_ERROR);
                                        }
                                        ms.Seek(0, SeekOrigin.Begin);
                                        request.Body = ms; // reset the body
                                    }
                                }
                                else
                                {
                                    throw new ArgumentNullException(nameof(jsonWebSignature.Header.Algorithm));

                                }
                            }
                            else
                            {
                                this.m_tracer.TraceWarning("Cannot validate digital signature {0} - {1} signature format not supported", sig.Type, sig.SigFormat);
                            }
                        }
                    }
                }
            }

        }

        /// <inheritdoc/>
        public void ApplyEndpointBehavior(ServiceEndpoint endpoint, EndpointDispatcher dispatcher)
        {
            dispatcher.MessageInspectors.Add(this);
        }

        /// <inheritdoc/>
        public void BeforeSendResponse(RestResponseMessage response)
        {
        }
    }
}

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
 * Date: 2022-5-30
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Rest.Behavior;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Operations
{
    /// <summary>
    /// Process a FHIR message operation
    /// </summary>
    public class FhirProcessMessageOperation : IFhirOperationHandler, IServiceImplementation
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FhirProcessMessageOperation));

        // Localization service
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI constructor
        /// </summary>
        /// <param name="localizationService"></param>
        public FhirProcessMessageOperation(ILocalizationService localizationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Get the name of the operation
        /// </summary>
        public string Name => "process-message";

        /// <summary>
        /// Gets the URI of the operation
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/OperationDefinition/MessageHeader-process-message");

        /// <summary>
        /// Gets the resource type
        /// </summary>
        public ResourceType[] AppliesTo => null;

        /// <summary>
        /// Get the parameters
        /// </summary>
        public IDictionary<string, FHIRAllTypes> Parameters => new Dictionary<String, FHIRAllTypes>()
        {
            { "content", FHIRAllTypes.Bundle },
            { "async", FHIRAllTypes.Boolean }
        };

        /// <summary>
        /// True if operation is get
        /// </summary>
        public bool IsGet => false;

        public string ServiceName => "Fhir Process Message Operation";

        /// <summary>
        /// Invoke the process message operation
        /// </summary>
        public Resource Invoke(Parameters parameters)
        {
            // Extract the parameters
            var contentParameter = parameters.Parameter.Find(o => o.Name == "content")?.Resource as Bundle;
            var asyncParameter = parameters.Parameter.Find(o => o.Name == "async")?.Value as FhirBoolean;
            if (contentParameter == null)
            {
                this.m_tracer.TraceError("Missing content parameter");
                throw new ArgumentNullException(m_localizationService.GetString("error.type.ArgumentNullException"));
            }
            else if (asyncParameter?.Value.GetValueOrDefault() == true)
            {
                this.m_tracer.TraceError("Asynchronous messaging is not supported by this repository");
                throw new InvalidOperationException(ErrorMessages.NOT_SUPPORTED);
            }

            // Message must have a message header
            var messageHeader = contentParameter.Entry.Find(o => o.Resource.TryDeriveResourceType(out ResourceType rt) && rt == ResourceType.MessageHeader)?.Resource as MessageHeader;
            if (messageHeader == null)
            {
                this.m_tracer.TraceError("Message bundle does not contain a MessageHeader");
                throw new ArgumentException(m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            // Determine the appropriate action handler
            if (messageHeader.Event is FhirUri eventUri)
            {
                var handler = ExtensionUtil.GetMessageOperationHandler(new Uri(eventUri.Value));
                if (handler == null)
                {
                    this.m_tracer.TraceError($"There is no message handler for event {eventUri}");
                    throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
                }

                var retVal = new Bundle(); // Return for operation
                retVal.Meta = new Meta()
                {
                    LastUpdated = DateTimeOffset.Now
                };
                var uuid = Guid.NewGuid();
                try
                {
                    // HACK: The .EndsWith is a total hack - FHIR wants .FullUrl to be absolute, but many senders will send relative references which
                    var opReturn = handler.Invoke(messageHeader, contentParameter.Entry.Where(o => messageHeader.Focus.Any(f => o.FullUrl == f.Reference || $"{o.Resource.TypeName}/{o.Resource.Id}" == f.Reference)).ToArray());

                    retVal.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = $"urn:uuid:{uuid}",
                        Resource = new MessageHeader()
                        {
                            Id = uuid.ToString(),
                            Response = new MessageHeader.ResponseComponent()
                            {
                                Code = MessageHeader.ResponseType.Ok,
                                Details = new ResourceReference($"urn:uuid:{opReturn.Id}")
                            }
                        }
                    });

                    // HACK: Another hack - FullUrl is assumed to be a UUID because I'm not turning an id of XXX and trying to derive a fullUrl for something that is in a
                    // bundle anyways
                    retVal.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = $"urn:uuid:{opReturn.Id}",
                        Resource = opReturn
                    });
                }
                catch (Exception e)
                {
                    var outcome = DataTypeConverter.CreateErrorResult(e);
                    outcome.Id = Guid.NewGuid().ToString();
                    retVal.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = $"urn:uuid:{uuid}",
                        Resource = new MessageHeader()
                        {
                            Id = uuid.ToString(),
                            Response = new MessageHeader.ResponseComponent()
                            {
                                Code = MessageHeader.ResponseType.FatalError,
                                Details = new ResourceReference($"urn:uuid:{outcome.Id}")
                            }
                        }
                    });

                    retVal.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = $"urn:uuid:{outcome.Id}",
                        Resource = outcome
                    });

                    throw new FhirException((System.Net.HttpStatusCode)FhirErrorEndpointBehavior.ClassifyErrorCode(e), retVal, m_localizationService.GetString("error.type.FhirException"), e);
                }
                finally
                {
                    retVal.Timestamp = DateTime.Now;
                    retVal.Type = Bundle.BundleType.Message;
                }
                return retVal;
            }
            else
            {
                this.m_tracer.TraceError("Currently message headers with EventCoding are not supported");
                throw new InvalidOperationException(m_localizationService.GetString("error.type.NotSupportedException.userMessage"));
            }
        }
    }
}
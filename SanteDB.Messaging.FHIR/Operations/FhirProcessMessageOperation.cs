using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Rest.Behavior;
using SanteDB.Messaging.FHIR.Rest.Serialization;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Operations
{
    /// <summary>
    /// Process a FHIR message operation
    /// </summary>
    public class FhirProcessMessageOperation : IFhirOperationHandler
    {
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

        /// <summary>
        /// Invoke the process message operation
        /// </summary>
        public Resource Invoke(Parameters parameters)
        {

            // Extract the parameters
            var contentParameter = parameters.Parameter.Find(o => o.Name == "content")?.Resource as Bundle;
            var asyncParameter = parameters.Parameter.Find(o => o.Name == "async")?.Value as FhirBoolean;
            if(contentParameter == null)
            {
                throw new ArgumentNullException("Missing content parameter");
            }    
            else if(asyncParameter?.Value.GetValueOrDefault() == true)
            {
                throw new InvalidOperationException("Asynchronous messaging is not supported by this repository");
            }

            // Message must have a message header
            var messageHeader = contentParameter.Entry.Find(o => o.Resource.ResourceType == ResourceType.MessageHeader)?.Resource as MessageHeader;
            if (messageHeader == null)
            {
                throw new ArgumentException("Message bundle does not contain a MessageHeader");
            }

            // Determine the appropriate action handler
            if (messageHeader.Event is FhirUri eventUri)
            {
                var handler = ExtensionUtil.GetMessageOperationHandler(new Uri(eventUri.Value));
                if (handler == null)
                {
                    throw new NotSupportedException($"There is no message handler for event {eventUri}");
                }

                var retVal = new Bundle(); // Return for operation
                retVal.Meta = new Meta()
                {
                    LastUpdated = DateTimeOffset.Now
                };
                var uuid = Guid.NewGuid();
                try
                {
                    // HACK: The .EndsWith is a total hack - FHIR wants .FullUrl to be absolute, but many senders will send relative references which is stupid AF
                    var opReturn = handler.Invoke(messageHeader, contentParameter.Entry.Where(o => messageHeader.Focus.Any(f => o.FullUrl == f.Reference || $"{o.Resource.ResourceType}/{o.Resource.Id}" == f.Reference)).ToArray());


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
                catch(Exception e)
                {
                    retVal.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = $"urn:uuid:{uuid}",
                        Resource = new MessageHeader()
                        {
			    Id = uuid.ToString(),
                            Response = new MessageHeader.ResponseComponent()
                            {
                                Code = MessageHeader.ResponseType.FatalError,
                                Details = new ResourceReference($"urn:uuid:{uuid}")
                            }
                        }
                    });
                    var outcome = DataTypeConverter.CreateErrorResult(e);
                    outcome.Id = uuid.ToString();
                    retVal.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = $"urn:uuid:{uuid}",
                        Resource = outcome
                    });

                    throw new FhirException((System.Net.HttpStatusCode)FhirErrorEndpointBehavior.ClassifyErrorCode(e), retVal, e);

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
                throw new InvalidOperationException($"Currently message headers with EventCoding are not supported");
            }
        }
    }
}

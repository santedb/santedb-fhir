using Hl7.Fhir.Model;
using SanteDB.Messaging.FHIR.Extensions;
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
                return handler.Invoke(messageHeader, contentParameter.Entry.Where(o => o.Resource != messageHeader).ToArray());
            }
            else
            {
                throw new InvalidOperationException($"Currently message headers with EventCoding are not supported");
            }
        }
    }
}

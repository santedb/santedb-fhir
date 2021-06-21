using Hl7.Fhir.Rest;
using SanteDB.Messaging.FHIR.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Rest
{
    /// <summary>
    /// Authenticator implementation used to take a settings array and a configuration and authenticate the client
    /// </summary>
    public interface IFhirClientAuthenticator
    {
        /// <summary>
        /// Attache to the client and authenticate on each request
        /// </summary>
        void AttachClient(FhirClient client, FhirDispatcherTargetConfiguration dispatchConfiguration, IDictionary<String, String> settings);

    }
}

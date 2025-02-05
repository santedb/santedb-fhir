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

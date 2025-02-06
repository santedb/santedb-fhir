using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Rest;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SanteDB.Core.Configuration.Http;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Http.Description;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Rest;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace SanteDB.Messaging.FHIR.Authenticator
{
    /// <summary>
    /// Client authenticator that performs an OAUTH authentication
    /// </summary>
    /// TODO: Migrate these to a common area
    public class OAuthClientAuthenticator : IFhirClientAuthenticator
    {

        /// <inheritdoc/>
        public string Name => "oauth";

        /// <summary>
        /// Auth server root
        /// </summary>
        public const string OAuthBaseEndpointUrlSettingName = "$oauth.server";
        /// <summary>
        /// Authentication client identifier
        /// </summary>
        public const string OAuthClientIdSettingName = "$oauth.client_id";
        /// <summary>
        /// Authentication client secret setting
        /// </summary>
        public const string OAuthClientSecretSettingName = "$oauth.client_secret";
        /// <summary>
        /// Authentication type setting name
        /// </summary>
        public const string OAuthGrantTypeSettingName = "$oauth.grant_type";
        /// <summary>
        /// Authentication username 
        /// </summary>
        public const string OAuthUsernameSettingName = "$oauth.username";
        /// <summary>
        /// authentication password 
        /// </summary>
        public const string OAuthPasswordSettingName = "$oauth.password";
        /// <summary>
        /// Get the scope configuration setting
        /// </summary>
        public const string OAuthGrantScopeSettingName = "$oauth.scope";
        /// <summary>
        /// No validating tokens
        /// </summary>
        public const string OAuthNoValidateToken = "$oauth.novalidate";
        /// <summary>
        /// Token to be used for authentication / refreshing
        /// </summary>
        public const string OAuthAuthTokenCode = "$oauth.token";

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(OAuthClientAuthenticator));
        private IRestClientDescription m_configuredRestDescription;
        private string m_bearerToken;
        private string m_refreshToken;
        private DateTimeOffset m_bearerTokenExpiration;
        private OpenIdConnectDiscoveryDocument m_discoveryDocument;
        private IRestClient m_restClient;
        private readonly int[] m_retryTimes = { 1, 10, 30 };
        private readonly JsonWebTokenHandler m_tokenHandler = new JsonWebTokenHandler();
        private TokenValidationParameters m_tokenValidationParameters;

        /// <summary>
        /// Create a new client authenticator
        /// </summary>
        public OAuthClientAuthenticator()
        {
        }

        /// <inheritdoc />
        public void AddAuthenticationHeaders(FhirClient client, string userName, string password, IDictionary<string, string> additionalSettings)
        {
            this.Configure(additionalSettings);
            var bearerToken = this.AuthenticateRefreshBearerToken(userName, password, additionalSettings);

            if (!String.IsNullOrEmpty(bearerToken))
            {
                client.RequestHeaders.Add("Authorization", $"bearer {bearerToken}");
            }
        }

        /// <summary>
        /// Executes <paramref name="func"/> with retry specified in <see cref="GetRetryWaitTimes"/>, sleeping the thread in between.
        /// </summary>
        /// <typeparam name="T">The result type of func.</typeparam>
        /// <param name="func">The callback to execute and retry.</param>
        /// <param name="errorCallback">An optional callback for when an exception ocurrs. This will typically log an error of some kind.</param>
        /// <returns>The result of the call or null.</returns>
        protected virtual T ExecuteWithRetry<T>(Func<T> func, Func<Exception, bool> errorCallback = null) where T : class
        {
            if (null == errorCallback)
            {
                errorCallback = ex => true;
            }

            T result = null;

            int c = 0;
            while (result == null && c < this.m_retryTimes.Length)
            {
                try
                {
                    result = func();
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    if (ex.IsCommunicationException() || !errorCallback(ex))
                    {
                        break; // if the exception is a communication exception (socket error, proxy missing, etc.) and not an application layer error or if the error callback wants a cancel we break out
                    }
                    Thread.Sleep(this.m_retryTimes[c]);
                }
                c++;
            }
            return result;
        }

        /// <summary>
        /// Get the <see cref="OpenIdConnectDiscoveryDocument"/> from the remote OAUTH server
        /// </summary>
        /// <returns>The configured <see cref="OpenIdConnectDiscoveryDocument"/> which was emitted by the OAUTH server</returns>
        private OpenIdConnectDiscoveryDocument GetDiscoveryDocument()
        {
            if (null != this.m_discoveryDocument)
            {
                return this.m_discoveryDocument;
            }

            this.m_discoveryDocument = ExecuteWithRetry(() =>
            {
                return this.m_restClient.Get<OpenIdConnectDiscoveryDocument>(".well-known/openid-configuration");
            },
            ex =>
            {
                this.m_tracer.TraceError("Exception fetching discovery document: {0}", ex.ToHumanReadableString());
                return true;
            });

            return this.m_discoveryDocument;
        }


        /// <summary>
        /// Get the JWKS information from the server
        /// </summary>
        /// <param name="jwksEndpoint">The endpoint from which the JWKS data should be fetched</param>
        /// <returns>The <see cref="JsonWebKeySet"/> from the server</returns>
        private JsonWebKeySet GetJsonWebKeySet(string jwksEndpoint)
        {

            var jwksjson = ExecuteWithRetry(() =>
            {
                var bytes = this.m_restClient.Get(jwksEndpoint);

                if (null == bytes)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(bytes);
            }, ex =>
            {
                this.m_tracer.TraceInfo("Exception getting jwks endpoint: {0}", ex);
                return true;
            });

            if (null == jwksjson)
            {
                this.m_tracer.TraceError("Failed to fetch jwks endpoint data from OAuth service.");
            }

            var jwks = new JsonWebKeySet(jwksjson);

            // This needs to be false for HS256 keys
            jwks.SkipUnresolvedJsonWebKeys = false;

            return jwks;
        }


        /// <summary>
        /// Set the token validation parameter to be used 
        /// </summary>
        private void SetTokenValidationParameters(String validAudiences)
        {
            var discoverydocument = this.GetDiscoveryDocument();

            if (discoverydocument == null)
            {
                return;
            }

            this.m_tokenValidationParameters = this.m_tokenValidationParameters ?? new TokenValidationParameters();

            this.m_tokenValidationParameters.ValidIssuers = new[] { discoverydocument.Issuer };
            this.m_tokenValidationParameters.ValidAudiences = new[] { validAudiences };
            this.m_tokenValidationParameters.ValidateAudience = true;
            this.m_tokenValidationParameters.ValidateIssuer = true;
            this.m_tokenValidationParameters.ValidateIssuerSigningKey = true;
            this.m_tokenValidationParameters.ValidateLifetime = true;
            this.m_tokenValidationParameters.TryAllIssuerSigningKeys = true;

            var jwksendpoint = discoverydocument.SigningKeyEndpoint;

            this.m_tokenValidationParameters.IssuerSigningKeys = GetJsonWebKeySet(jwksendpoint)?.GetSigningKeys();

        }

        /// <summary>
        /// Configure the authentication headers
        /// </summary>
        private void Configure(IDictionary<string, string> additionalSettings)
        {
            if(additionalSettings.TryGetValue(OAuthBaseEndpointUrlSettingName, out var basePath))
            {
                this.m_restClient = new RestClient(new RestClientDescriptionConfiguration()
                {
                    Accept = "application/json",
                    Name = "FHIR_OAUTH_HANDLER_INJECTION",
                    Binding = new RestClientBindingConfiguration()
                    {
                        CompressRequests = false,
                        ContentTypeMapper = new DefaultContentTypeMapper(),
                        OptimizationMethod = HttpCompressionAlgorithm.Gzip
                    },
                    Endpoint = new List<RestClientEndpointConfiguration>()
                {
                    new RestClientEndpointConfiguration(basePath)
                }
                });

                if(additionalSettings.TryGetValue(OAuthClientIdSettingName, out var clientId) && (!additionalSettings.TryGetValue(OAuthNoValidateToken, out var noValStr) || bool.TryParse(noValStr, out var noVal) && !noVal))
                {
                    this.SetTokenValidationParameters(clientId);
                }
            }
            else
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.DEPENDENT_CONFIGURATION_MISSING, OAuthBaseEndpointUrlSettingName));
            }
        }

        /// <summary>
        /// Authenticate or refresh the bearer token
        /// </summary>
        private string AuthenticateRefreshBearerToken(string userName, string password, IDictionary<string, string> additionalSettings)
        {
            if (!String.IsNullOrEmpty(this.m_bearerToken) && this.m_bearerTokenExpiration > DateTimeOffset.Now)
            {
                return this.m_bearerToken;
            }

            _ = additionalSettings.TryGetValue(OAuthBaseEndpointUrlSettingName, out var endpointBase);
            _ = additionalSettings.TryGetValue(OAuthClientIdSettingName, out var client_id);
            _ = additionalSettings.TryGetValue(OAuthClientSecretSettingName, out var client_secret);
            _ = additionalSettings.TryGetValue(OAuthGrantTypeSettingName, out var grant_type);
            _ = additionalSettings.TryGetValue(OAuthGrantScopeSettingName, out var scopes);
            // Is there a username or password
            _ = String.IsNullOrEmpty(userName) ? additionalSettings.TryGetValue(OAuthUsernameSettingName, out userName) : false;
            _ = String.IsNullOrEmpty(password) ? additionalSettings.TryGetValue(OAuthPasswordSettingName, out password) : false;
            
            OAuthTokenResponse response = null;
            if (!String.IsNullOrEmpty(this.m_refreshToken))
            {
                response = this.Refresh(this.m_refreshToken);
            }
            else switch (grant_type.ToLowerInvariant())
                {
                    case "password":
                        response = this.AuthenticateUser(userName, password, client_id, scopes);
                        break;
                    case "client_credentials":
                        response = this.AuthenticateApp(client_id, client_secret, scopes);
                        break;
                    
                    default:
                        throw new InvalidOperationException();
                }

            this.ValidateToken(response);

            this.m_bearerToken = response.AccessToken;
            this.m_refreshToken = response.RefreshToken;
            this.m_bearerTokenExpiration = DateTime.Now.AddMilliseconds(response.ExpiresIn);
            return this.m_bearerToken;
        }

        /// <summary>
        /// Validate the specified token
        /// </summary>
        /// <param name="tokenResponse">The token response to be validated</param>
        private void ValidateToken(OAuthTokenResponse tokenResponse)
        {

            if(this.m_tokenValidationParameters == null)
            {
                return;
            } // validation of the token 

            var tokenvalidationresult = this.m_tokenHandler.ValidateToken(tokenResponse.IdToken, this.m_tokenValidationParameters);
            if (tokenvalidationresult?.IsValid != true)
            {
                throw tokenvalidationresult.Exception ?? new SecurityTokenException("Token validation failed");
            }

        }

        /// <summary>
        /// Client credentials authentication
        /// </summary>
        private OAuthTokenResponse AuthenticateApp(string client_id, string client_secret, string scope)
        {
            var request = new OAuthTokenRequest()
            {
                ClientId = client_id,
                ClientSecret = client_secret,
                Scope = scope,
                GrantType = "client_credentials"
            };
            return this.m_restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(this.m_discoveryDocument.TokenEndpoint, "application/x-www-form-urlencoded", request);
        }

        /// <summary>
        /// Authenticate user
        /// </summary>
        private OAuthTokenResponse AuthenticateUser(string userName, string password, string clientId, string scope)
        {
            var request = new OAuthTokenRequest()
            {
                ClientId = clientId,
                Username = userName,
                Password = password,
                Scope = scope,
                GrantType = "password"
            };
            return this.m_restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(this.m_discoveryDocument.TokenEndpoint, "application/x-www-form-urlencoded", request);
        }

        /// <summary>
        /// Extend
        /// </summary>
        private OAuthTokenResponse Refresh(string m_refreshToken)
        {
            var request = new OAuthTokenRequest()
            {
                RefreshToken = m_refreshToken,
                GrantType = "refresh_token"
            };
            return this.m_restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(this.m_discoveryDocument.TokenEndpoint, "application/x-www-form-urlencoded", request);
        }


    }
}

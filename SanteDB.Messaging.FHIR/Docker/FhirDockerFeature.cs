using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Docker.Core;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Rest;
using SanteDB.Rest.Common.Behavior;
using SanteDB.Rest.Common.Configuration;
using SanteDB.Rest.Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Docker
{
    /// <summary>
    /// Exposes the FHIR service to the Docker container
    /// </summary>
    public class FhirDockerFeature : IDockerFeature
    {
        /// <summary>
        /// Setting ID for resources
        /// </summary>
        public const string ResourceSetting = "RESOURCE";
        /// <summary>
        /// Message settings
        /// </summary>
        public const string MessageSetting = "MESSAGE";
        /// <summary>
        /// Operation settings
        /// </summary>
        public const string OperationSetting = "OPERATION";
        /// <summary>
        /// Profile settings
        /// </summary>
        public const string ProfileSetting = "PROFILE";

        /// <summary>
        /// Set id for base URI
        /// </summary>
        public const string BaseUriSetting = "BASE";
        /// <summary>
        /// Setting ID for listen address
        /// </summary>
        public const string ListenUriSetting = "LISTEN";
        /// <summary>
        /// Setting ID for CORS enable
        /// </summary>
        public const string CorsSetting = "CORS";
        /// <summary>
        /// Set ID for authentication
        /// </summary>
        public const string AuthenticationSetting = "AUTH";
        private readonly IDictionary<String, Type> authSettings = new Dictionary<String, Type>()
        {
            { "TOKEN", typeof(TokenAuthorizationAccessBehavior) },
            { "BASIC", typeof(BasicAuthorizationAccessBehavior) },
            { "NONE", null }
        };

        /// <summary>
        /// Gets the id of this feature
        /// </summary>
        public string Id => "FHIR";

        /// <summary>
        /// Get the settings for this docker feature
        /// </summary>
        public IEnumerable<string> Settings => new String[] { AuthenticationSetting, ResourceSetting, BaseUriSetting, CorsSetting, ListenUriSetting, AuthenticationSetting };

        /// <summary>
        /// Create an endpoint config
        /// </summary>
        private RestEndpointConfiguration CreateEndpoint(String endpointUrl) => new RestEndpointConfiguration()
        {
            Address = endpointUrl,
            Contract = typeof(IFhirServiceContract),
            Behaviors = new List<RestEndpointBehaviorConfiguration>()
                            {
                                new RestEndpointBehaviorConfiguration(typeof(MessageLoggingEndpointBehavior)),
                                new RestEndpointBehaviorConfiguration(typeof(MessageCompressionEndpointBehavior)),
                                new RestEndpointBehaviorConfiguration(typeof(MessageDispatchFormatterBehavior)),
                                new RestEndpointBehaviorConfiguration(typeof(AcceptLanguageEndpointBehavior))
                            }
        };

        /// <summary>
        /// Configure the service
        /// </summary>
        public void Configure(SanteDBConfiguration configuration, IDictionary<string, string> settings)
        {
            var restConfiguration = configuration.GetSection<SanteDB.Rest.Common.Configuration.RestConfigurationSection>();
            if(restConfiguration == null)
            {
                throw new ConfigurationException("Error retrieving REST configuration", configuration);
            }

            var fhirRestConfiguration = restConfiguration.Services.FirstOrDefault(o => o.ServiceType == typeof(IFhirServiceContract));
            if(fhirRestConfiguration == null) // add fhir rest config
            {
                fhirRestConfiguration = new RestServiceConfiguration()
                {
                    Behaviors = new List<RestServiceBehaviorConfiguration>()
                    {
                        new RestServiceBehaviorConfiguration(typeof(TokenAuthorizationAccessBehavior))
                    },
                    Name = "FHIR",
                    Endpoints = new List<RestEndpointConfiguration>()
                    {
                        this.CreateEndpoint("http://0.0.0.0:8080/fhir")
                    }
                };

                RestServiceConfiguration.Load(typeof(IFhirServiceContract).Assembly.GetManifestResourceStream("SanteDB.Messaging.FHIR.Configuration.Default.xml"));
                restConfiguration.Services.Add(fhirRestConfiguration);
            }

            var fhirConfiguration = configuration.GetSection<FhirServiceConfigurationSection>();
            if(fhirConfiguration == null)
            {
                fhirConfiguration = new FhirServiceConfigurationSection()
                {
                    ResourceBaseUri = "http://127.0.0.1:8080/fhir",
                    ResourceHandlers = typeof(FhirDockerFeature).Assembly.ExportedTypes.Where(o => typeof(IFhirResourceHandler).IsAssignableFrom(o) && !o.IsAbstract && o.IsClass).Select(o => new TypeReferenceConfiguration(o)).ToList()
                };
                configuration.AddSection(fhirConfiguration);
            }

            // Listen address
            if(settings.TryGetValue(ListenUriSetting, out string listen))
            {
                if(!Uri.TryCreate(listen, UriKind.Absolute, out Uri listenUri))
                {
                    throw new ArgumentOutOfRangeException($"{listen} is not a valid URL");
                }

                // Setup the endpoint
                fhirRestConfiguration.Endpoints.Clear();
                fhirRestConfiguration.Endpoints.Add(new RestEndpointConfiguration()
                {
                    Address = listen,
                    ContractXml = typeof(IFhirServiceContract).AssemblyQualifiedName,
                    Behaviors = new List<RestEndpointBehaviorConfiguration>()
                    {
                        new RestEndpointBehaviorConfiguration(typeof(MessageLoggingEndpointBehavior)),
                        new RestEndpointBehaviorConfiguration(typeof(MessageCompressionEndpointBehavior)),
                        new RestEndpointBehaviorConfiguration(typeof(AcceptLanguageEndpointBehavior))
                    }
                });
            }

            // Authentication
            if(settings.TryGetValue(AuthenticationSetting, out string auth))
            {
                if(!this.authSettings.TryGetValue(auth.ToUpperInvariant(), out Type authType))
                {
                    throw new ArgumentOutOfRangeException($"Don't understand auth option {auth} allowed values {String.Join(",", this.authSettings.Keys)}");
                }

                // Add behavior
                if (authType != null)
                {
                    fhirRestConfiguration.Behaviors.Add(new RestServiceBehaviorConfiguration() { Type = authType });
                }
                else
                {
                    fhirRestConfiguration.Behaviors.RemoveAll(o => this.authSettings.Values.Any(v => v == o.Type));
                }
            }

            // Has the user set CORS?
            if (settings.TryGetValue(CorsSetting, out string cors))
            {
                if(!Boolean.TryParse(cors, out bool enabled))
                {
                    throw new ArgumentOutOfRangeException($"{cors} is not a valid boolean value");
                }

                // Cors is disabled?
                if(!enabled)
                {
                    fhirRestConfiguration.Endpoints.ForEach(ep => ep.Behaviors.RemoveAll(o => o.Type == typeof(CorsEndpointBehavior)));
                }
                else
                {
                    fhirRestConfiguration.Endpoints.ForEach(ep => ep.Behaviors.RemoveAll(o => o.Type == typeof(CorsEndpointBehavior)));
                    fhirRestConfiguration.Endpoints.ForEach(ep => ep.Behaviors.Add(new RestEndpointBehaviorConfiguration()
                    {
                        Type = typeof(CorsEndpointBehavior)
                    }));
                }
            }

            // Base URI
            if(settings.TryGetValue(BaseUriSetting, out string baseUri))
            {
                fhirConfiguration.ResourceBaseUri = baseUri;
            }

            // Custom resource list?
            if(settings.TryGetValue(ResourceSetting, out string resource))
            {
                fhirConfiguration.Resources = new List<string>();
                foreach(var res in resource.Split(';'))
                {
                    if(!Enum.TryParse<Hl7.Fhir.Model.ResourceType>(res, out Hl7.Fhir.Model.ResourceType rt))
                    {
                        throw new ArgumentOutOfRangeException($"{res} is not a valid FHIR resource");
                    }

                    // Add resource setting
                    fhirConfiguration.Resources.Add(res);
                }
            }

            // Add services
            var serviceConfiguration = configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders;
            if(!serviceConfiguration.Any(s=>s.Type == typeof(FhirMessageHandler)))
            {
                serviceConfiguration.Add(new TypeReferenceConfiguration(typeof(FhirMessageHandler)));
            }

        }
    }
}

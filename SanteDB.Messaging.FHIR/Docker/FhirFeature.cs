﻿using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Docker.Core;
using SanteDB.Messaging.FHIR.Configuration;
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
    public class FhirFeature : IDockerFeature
    {
        /// <summary>
        /// Setting ID for resources
        /// </summary>
        public const string ResourceSetting = "RESOURCE";
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
            { "TOKEN", Type.GetType("SanteDB.Server.Core.Rest.Security.TokenAuthorizationAccessBehavior, SanteDB.Server.Core") },
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
                fhirRestConfiguration = RestServiceConfiguration.Load(typeof(IFhirServiceContract).Assembly.GetManifestResourceStream("SanteDB.Messaging.FHIR.Configuration.Default.xml"));
                restConfiguration.Services.Add(fhirRestConfiguration);
            }

            var fhirConfiguration = configuration.GetSection<FhirServiceConfigurationSection>();
            if(fhirConfiguration == null)
            {
                fhirConfiguration = DockerFeatureUtils.LoadConfigurationResource<FhirServiceConfigurationSection>("SanteDB.Messaging.FHIR.Docker.FhirFeature.xml");
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
                foreach(var res in resource.Split(','))
                {
                    if(!Enum.TryParse<ResourceType>(res, out ResourceType rt))
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
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.PubSub;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.PubSub
{
    /// <summary>
    /// A pub-sub dispatch factory which can send rest hook 
    /// </summary>
    public class FhirPubSubRestHookDispatcherFactory : IPubSubDispatcherFactory
    {
        /// <summary>
        /// Gets the schemes for this factory
        /// </summary>
        public IEnumerable<string> Schemes => new String[] { "fhir-rest-http", "fhir-rest-https" };

        /// <summary>
        /// The dispatcher
        /// </summary>
        private class Dispatcher : IPubSubDispatcher
        {

            /// <summary>
            /// Tracer
            /// </summary>
            private Tracer m_tracer = Tracer.GetTracer(typeof(Dispatcher));

            // Client for FHIR
            private FhirClient m_client;

            // Configurationfor the dispatcher
            private FhirDispatcherTargetConfiguration m_configuration;

            /// <summary>
            /// Creates a new dispatcher for the channel
            /// </summary>
            public Dispatcher(Guid channelKey, Uri endpoint, IDictionary<String, String> settings)
            {
                this.Key = channelKey;
                this.Endpoint = endpoint;
                this.Settings = settings;

                this.m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>()?.GetSection<FhirDispatcherConfigurationSection>()?.Targets.Find(o => o.Endpoint == endpoint.ToString());
                
                // The client for this object
                this.m_client = new FhirClient(this.Endpoint, false);
                this.m_client.ParserSettings = new Hl7.Fhir.Serialization.ParserSettings()
                {
                    AcceptUnknownMembers = true,
                    AllowUnrecognizedEnums = true
                };
                
                if(settings.TryGetValue("Content-Type", out string contentType))
                {
                    this.m_client.PreferredFormat = ContentType.GetResourceFormatFromFormatParam(contentType);
                }
                
                // Add settings from the service
                this.m_client.OnBeforeRequest += (o, e) =>
                {
                    foreach (var kv in this.Settings.Where(z=>z.Key != "Content-Type"))
                        e.RawRequest.Headers.Add(kv.Key, kv.Value);
                };

                if(this.m_configuration?.Authenticator != null)
                {
                    var authenticator = ApplicationServiceContext.Current.GetService<IServiceManager>().CreateInjected(this.m_configuration.Authenticator.Type) as IFhirClientAuthenticator;
                    authenticator.AttachClient(this.m_client, this.m_configuration, settings);
                }
            }

            /// <summary>
            /// Gets the key
            /// </summary>
            public Guid Key { get; }

            /// <summary>
            /// Gets the endpoint
            /// </summary>
            public Uri Endpoint { get; }

            /// <summary>
            /// Gets the settings
            /// </summary>
            public IDictionary<string, string> Settings { get; }

            /// <summary>
            /// Convert <paramref name="data"/> to a FHIR resource
            /// </summary>
            /// <typeparam name="TModel">The type of model</typeparam>
            /// <param name="data">The data to be converted</param>
            /// <returns>The converted resource</returns>
            private Resource ConvertToResource<TModel>(TModel data)
            {
                var mapper = FhirResourceHandlerUtil.GetMapperForInstance(data);
                if(mapper == null)
                {
                    throw new InvalidOperationException("Cannot determine how to convert resource for notification");
                }
                return mapper.MapToFhir(data as IdentifiedData);
            }

            /// <summary>
            /// Notify that an object was created
            /// </summary>
            public void NotifyCreated<TModel>(TModel data)
            {
                try
                {
                    var resource = this.ConvertToResource(data);
                    this.m_client.Create(resource);
                }
                catch(Exception e)
                {
                    this.m_tracer.TraceError("Could not send create to {0} for {1} - {2}", this.Endpoint, data, e);
                }
            }

            /// <summary>
            /// Notify object was merged
            /// </summary>
            public void NotifyMerged<TModel>(TModel survivor, TModel[] subsumed)
            {
                this.m_tracer.TraceWarning("TODO: Implement notification");
            }

            /// <summary>
            /// Notify obsoleted
            /// </summary>
            public void NotifyObsoleted<TModel>(TModel data)
            {
                try
                {
                    var resource = this.ConvertToResource(data);
                    this.m_client.Delete(resource);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not send obsolete to {0} for {1} - {2}", this.Endpoint, data, e);
                }
            }

            /// <summary>
            /// Notify unmerged
            /// </summary>
            public void NotifyUnMerged<TModel>(TModel primary, TModel[] unMerged)
            {
                this.m_tracer.TraceWarning("TODO: Implement notification");
            }

            /// <summary>
            /// Notify updated
            /// </summary>
            public void NotifyUpdated<TModel>(TModel data)
            {
                try
                {
                    var resource = this.ConvertToResource(data);
                    this.m_client.Update(resource);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not send update to {0} for {1} - {2}", this.Endpoint, data, e);
                }
            }
        }

        /// <summary>
        /// Create the specified dispatcher
        /// </summary>
        public IPubSubDispatcher CreateDispatcher(Guid channelKey, Uri endpoint, IDictionary<string, string> settings)
        {
            return new Dispatcher(channelKey, endpoint, settings);
        }
    }
}

using SanteDB.Core.Diagnostics;
using SanteDB.Core.PubSub;
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

            /// <summary>
            /// Creates a new dispatcher for the channel
            /// </summary>
            public Dispatcher(Guid channelKey, Uri endpoint, IDictionary<String, String> settings)
            {
                this.Key = channelKey;
                this.Endpoint = endpoint;
                this.Settings = settings;
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
            /// Notify that an object was created
            /// </summary>
            public void NotifyCreated<TModel>(TModel data)
            {
                this.m_tracer.TraceWarning("TODO: Implement notification");
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
                this.m_tracer.TraceWarning("TODO: Implement notification");
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
                this.m_tracer.TraceWarning("TODO: Implement notification");
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

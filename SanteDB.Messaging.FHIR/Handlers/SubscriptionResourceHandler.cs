﻿using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.PubSub;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.PubSub;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Resource handler for Subscription management
    /// </summary>
    public class SubscriptionResourceHandler : IFhirResourceHandler
    {

        // Pub-Sub Manager
        private IPubSubManagerService m_pubSubManager;

        // Configuration Manager
        private IConfigurationManager m_configurationManager;

        // Trace source
        private Tracer m_tracer = Tracer.GetTracer(typeof(SubscriptionResourceHandler));

        /// <summary>
        /// Create a new subscription resource handler
        /// </summary>
        public SubscriptionResourceHandler(IPubSubManagerService manager, IConfigurationManager configManager)
        {
            this.m_pubSubManager = manager;
            this.m_configurationManager = configManager;
        }

        /// <summary>
        /// Gets the resource type
        /// </summary>
        public ResourceType ResourceType => ResourceType.Subscription;

        /// <summary>
        /// Create the specified resource target
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
        {

            // Check type
            if (!(target is Subscription subscription))
            {
                throw new ArgumentOutOfRangeException("Subscription registration requires a subscription body");
            }
            else if (String.IsNullOrEmpty(target.Id))
            {
                throw new ArgumentNullException($"Subscription requires an ID");
            }
            else if (this.m_pubSubManager.GetSubscriptionByName(target.Id) != null)
            {
                throw new InvalidOperationException($"Subscription {target.Id} already registered");
            }

            this.m_tracer.TraceInfo("Will create a new subscription {0}...", target.Id);

            var queryObject = new Uri($"http://nil/{subscription.Criteria}");
            var enumType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(queryObject.LocalPath.Substring(1));
            var cdrType = FhirResourceHandlerUtil.GetResourceHandler(enumType.Value) as IFhirResourceMapper;
            if (cdrType == null)
            {
                throw new NotSupportedException($"Resource type {enumType.Value} is not supported by this service");
            }

            QueryRewriter.RewriteFhirQuery(cdrType.ResourceClrType, cdrType.CanonicalType, NameValueCollection.ParseQueryString(queryObject.Query.Substring(1)).ToNameValueCollection(), out NameValueCollection hdsiQuery);

            // Create the pub-sub definition
            var channel = this.CreateChannel($"Channel for {subscription.Id}", subscription.Channel, mode);
            var retVal = this.m_pubSubManager.RegisterSubscription(cdrType.CanonicalType, subscription.Id, subscription.Reason, PubSubEventType.Create | PubSubEventType.Update | PubSubEventType.Delete, hdsiQuery.ToString(), channel.Key.Value, supportAddress: subscription.Contact?.FirstOrDefault()?.Value, notAfter: subscription.End);

            if (subscription.Status == Subscription.SubscriptionStatus.Active)
                this.m_pubSubManager.ActivateSubscription(retVal.Key.Value, true);

            return this.MapToFhir(retVal, RestOperationContext.Current);
        }

        /// <summary>
        /// Delete the specified subscription
        /// </summary>
        public Resource Delete(string id, TransactionMode mode)
        {

            var key = this.m_pubSubManager.GetSubscriptionByName(id)?.Key;
            if (key == null)
                throw new KeyNotFoundException($"Subscription {id} not found");

            PubSubSubscriptionDefinition retVal = null;
            if (mode == TransactionMode.Commit)
            {
                retVal = this.m_pubSubManager.RemoveSubscription(key.Value);
                this.m_pubSubManager.RemoveChannel(retVal.ChannelKey);
            }

            return this.MapToFhir(retVal, RestOperationContext.Current);
        }

        /// <summary>
        /// Get the resource definition
        /// </summary>
        public ResourceComponent GetResourceDefinition()
        {
            return new ResourceComponent()
            {
                Type = ResourceType.Subscription,
                ConditionalCreate = false,
                ConditionalDelete = ConditionalDeleteStatus.NotSupported,
                ConditionalRead = ConditionalReadStatus.NotSupported,
                ConditionalUpdate = false,
                Interaction = new List<ResourceInteractionComponent>()
                {
                    new ResourceInteractionComponent() { Code = TypeRestfulInteraction.Read },
                    new ResourceInteractionComponent() { Code = TypeRestfulInteraction.Create },
                    new ResourceInteractionComponent() { Code = TypeRestfulInteraction.Delete },
                    new ResourceInteractionComponent() { Code = TypeRestfulInteraction.Update },
                    new ResourceInteractionComponent() { Code = TypeRestfulInteraction.SearchType }
                },
                ReadHistory = false,
                Versioning = ResourceVersionPolicy.NoVersion
            };
        }

        /// <summary>
        /// Get the structure definition
        /// </summary>
        public StructureDefinition GetStructureDefinition()
        {
            return typeof(Subscription).GetStructureDefinition(false);
        }

        /// <summary>
        /// Get the history of this object
        /// </summary>
        public FhirQueryResult History(string id)
        {
            throw new NotSupportedException("Versioning is not supported on this object");
        }

        /// <summary>
        /// Query the subscription object
        /// </summary>
        public FhirQueryResult Query(System.Collections.Specialized.NameValueCollection parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            Core.Model.Query.NameValueCollection hdsiQuery = null;
            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(Subscription), typeof(PubSubSubscriptionDefinition), parameters, out hdsiQuery);
            hdsiQuery.Add("obsoletionTime", "null");
            // Do the query
            int totalResults = 0;
            var predicate = QueryExpressionParser.BuildLinqExpression<PubSubSubscriptionDefinition>(hdsiQuery);
            var hdsiResults = this.m_pubSubManager.FindSubscription(predicate, query.Start, query.Quantity, out totalResults);
            var restOperationContext = RestOperationContext.Current;

            var auth = AuthenticationContext.Current;
            // Return FHIR query result
            return new FhirQueryResult(nameof(Subscription))
            {
                Results = hdsiResults.AsParallel().Select(o =>
                {
                    try
                    {
                        AuthenticationContext.Current = auth;
                        return this.MapToFhir(o, restOperationContext);
                    }
                    finally
                    {
                        AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.AnonymousPrincipal);
                    }
                }).OfType<Resource>().ToList(),
                Query = query,
                TotalResults = totalResults
            };
        }

        /// <summary>
        /// Fetch the specified query identifier
        /// </summary>
        public Resource Read(string id, string versionId)
        {
            var retVal = this.m_pubSubManager.GetSubscriptionByName(id);
            return this.MapToFhir(retVal, RestOperationContext.Current);
        }

        /// <summary>
        /// Updates the specified subscription
        /// </summary>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            
            if (!(target is Subscription subscription))
            {
                throw new ArgumentException("Payload must be a subscription resource");
            }
            var key = this.m_pubSubManager.GetSubscriptionByName(id)?.Key;
            if(key == null)
            {
                throw new KeyNotFoundException($"Subscription {id} not found");
            }

            // Now update the data
            this.m_tracer.TraceInfo("Will update subscription {0}...", target.Id);

            var queryObject = new Uri($"http://nil/{subscription.Criteria}");
            var enumType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(queryObject.LocalPath.Substring(1));
            var cdrType = FhirResourceHandlerUtil.GetResourceHandler(enumType.Value) as IFhirResourceMapper;
            if (cdrType == null)
            {
                throw new NotSupportedException($"Resource type {enumType.Value} is not supported by this service");
            }

            QueryRewriter.RewriteFhirQuery(cdrType.ResourceClrType, cdrType.CanonicalType, NameValueCollection.ParseQueryString(queryObject.Query.Substring(1)).ToNameValueCollection(), out NameValueCollection hdsiQuery);

            // Update the channel
            var retVal = this.m_pubSubManager.UpdateSubscription(key.Value, subscription.Id, subscription.Reason, PubSubEventType.Create | PubSubEventType.Update | PubSubEventType.Delete, hdsiQuery.ToString(), supportAddress: subscription.Contact?.FirstOrDefault()?.Value, notAfter: subscription.End);
            this.m_pubSubManager.ActivateSubscription(key.Value, subscription.Status == Subscription.SubscriptionStatus.Active);

            var settings = subscription.Channel.Header.Select(o => o.Split(':')).ToDictionary(o => o[0], o => o[1]);
            settings.Add("Content-Type", subscription.Channel.Payload);
            this.m_pubSubManager.UpdateChannel(retVal.ChannelKey, $"Channel for {subscription.Id}", new Uri(subscription.Channel.Endpoint), settings);
            return this.MapToFhir(retVal, RestOperationContext.Current);
        }

        /// <summary>
        /// Map the model pub-sub description to FHIR
        private Subscription MapToFhir(PubSubSubscriptionDefinition model, RestOperationContext restOperationContext)
        {

            // Construct the return subscription
            var retVal = DataTypeConverter.CreateResource<Subscription>(model, restOperationContext);

            // Map status based on current state in CDR
            retVal.Id = model.Name;
            retVal.Reason = model.Description;
            retVal.Contact = new List<ContactPoint>()
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Other, ContactPoint.ContactPointUse.Temp, model.SupportContact)
            };

            retVal.Status = model.IsActive ? Subscription.SubscriptionStatus.Active : Subscription.SubscriptionStatus.Off;
            if (model.NotBefore > DateTime.Now)
                retVal.Status = Subscription.SubscriptionStatus.Requested;
            if (model.NotAfter.HasValue)
                retVal.End = model.NotAfter.Value;

            var channel = this.m_pubSubManager.GetChannel(model.ChannelKey);
            // Map channel information
            retVal.Channel = new Subscription.ChannelComponent()
            {
                Type = channel.Endpoint.Scheme == "sms" ? Subscription.SubscriptionChannelType.Sms :
                    channel.Endpoint.Scheme == "mailto" ? Subscription.SubscriptionChannelType.Email :
                   typeof(FhirPubSubMessageDispatcherFactory ) == channel.DispatcherFactoryType ? Subscription.SubscriptionChannelType.Message :
                   Subscription.SubscriptionChannelType.RestHook,
                Endpoint = channel.Endpoint.ToString(),
                Header = channel.Settings.Where(o => !o.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Select(o => $"{o.Name}: {o.Value}"),
                Payload = channel.Settings.FirstOrDefault(o => o.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value,
            };

            // TODO: Map the HDSI query syntax to FHIR PATH
            var mapper = FhirResourceHandlerUtil.GetMapperFor(model.ResourceType);
            retVal.Criteria = $"{mapper.ResourceType}?";

            return retVal;
        }

        /// <summary>
        /// Create channel
        /// </summary>
        /// <param name="fhirChannel"></param>
        /// <returns></returns>
        private PubSubChannelDefinition CreateChannel(String name, Subscription.ChannelComponent fhirChannel, TransactionMode mode)
        {
            var settings = fhirChannel.Header.Select(o => o.Split(':')).ToDictionary(o => o[0], o => o[1]);
            settings.Add("Content-Type", fhirChannel.Payload);

            // TODO: Whitelist check (or the sub manager should do that?)
            PubSubChannelDefinition channel = null;
            switch (fhirChannel.Type)
            {
                case Subscription.SubscriptionChannelType.Email:
                    // TODO: E-mail dispatcher
                    if (!fhirChannel.Endpoint.StartsWith("mailto:"))
                    {
                        throw new ArgumentOutOfRangeException("Scheme of the endpoint for e-mail subscriptions is mailto:");
                    }
                    if (mode == TransactionMode.Commit) // Actually register the channel
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;
                case Subscription.SubscriptionChannelType.Sms:
                    // TODO: E-mail dispatcher
                    if (!fhirChannel.Endpoint.StartsWith("sms:"))
                    {
                        throw new ArgumentOutOfRangeException("Scheme of the endpoint for e-mail subscriptions is sms:");
                    }
                    if (mode == TransactionMode.Commit) // Actually register the channel
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;
                case Subscription.SubscriptionChannelType.RestHook:
                    if (mode == TransactionMode.Commit) // Actually register the channel
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, typeof(FhirPubSubRestHookDispatcherFactory), new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;
                case Subscription.SubscriptionChannelType.Message:
                    if(mode == TransactionMode.Commit)
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, typeof(FhirPubSubMessageDispatcherFactory), new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Resource channel type {fhirChannel.Type} not supported ");
            }

            return channel;
        }

    }
}
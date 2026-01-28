/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2023-6-21
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using SanteDB.Core.PubSub;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.PubSub;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Resource handler for Subscription management
    /// </summary>
    public class SubscriptionResourceHandler : IFhirResourceHandler, IServiceImplementation
    {
        // Pub-Sub Manager
        private IPubSubManagerService m_pubSubManager;

        // Configuration Manager
        private IConfigurationManager m_configurationManager;

        // Trace source
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(SubscriptionResourceHandler));

        //Localization service
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// Create a new subscription resource handler
        /// </summary>
        public SubscriptionResourceHandler(IPubSubManagerService manager, IConfigurationManager configManager, ILocalizationService localizationService)
        {
            this.m_pubSubManager = manager;
            this.m_configurationManager = configManager;
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Gets the resource type
        /// </summary>
        public ResourceType ResourceType => ResourceType.Subscription;

        /// <summary>
        /// Get service name
        /// </summary>
        public string ServiceName => "Subscription Resource Handler";

        /// <summary>
        /// Create the specified resource target
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
        {
            // Check type
            if (!(target is Subscription subscription))
            {
                this.m_tracer.TraceError("Subscription registration requires a subscription body");
                throw new ArgumentOutOfRangeException(this.m_localizationService.GetString("error.type.InvalidDataException.userMessage", new
                {
                    param = "subscription body"
                }));
            }
            else if (String.IsNullOrEmpty(target.Id))
            {
                this.m_tracer.TraceError("Subscription requires an ID");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException.userMessage"));
            }
            else if (this.m_pubSubManager.GetSubscriptionByName(target.Id) != null)
            {
                this.m_tracer.TraceError($"Subscription {target.Id} already registered");
                throw new InvalidOperationException(this.m_localizationService.GetString("error.type.InvalidOperation"));
            }

            this.m_tracer.TraceInfo("Will create a new subscription {0}...", target.Id);

            var queryObject = new Uri($"http://nil/{subscription.Criteria}");
            var enumType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(queryObject.LocalPath.Substring(1));
            var cdrType = FhirResourceHandlerUtil.GetResourceHandler(enumType.Value) as IFhirResourceMapper;
            if (cdrType == null)
            {
                this.m_tracer.TraceError($"Resource type {enumType.Value} is not supported by this service");
                throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
            }

            var hdsiQuery = new NameValueCollection();
            if (!String.IsNullOrEmpty(queryObject.Query))
            {
                QueryRewriter.RewriteFhirQuery(cdrType.ResourceClrType, cdrType.CanonicalType, queryObject.Query.Substring(1).ParseQueryString(), out hdsiQuery);
            }

            // Create the pub-sub definition
            var channel = this.CreateChannel($"Channel for {subscription.Id}", subscription.Channel, mode);
            var retVal = this.m_pubSubManager.RegisterSubscription(cdrType.CanonicalType, subscription.Id, subscription.Reason, PubSubEventType.Create | PubSubEventType.Update | PubSubEventType.Delete | PubSubEventType.Merge, hdsiQuery.ToHttpString(), channel.Key.Value, supportAddress: subscription.Contact?.FirstOrDefault()?.Value, notAfter: subscription.End);

            if (subscription.Status == SubscriptionStatusCodes.Active)
            {
                retVal = this.m_pubSubManager.ActivateSubscription(retVal.Key.Value, true);
            }

            return this.MapToFhir(retVal);
        }

        /// <summary>
        /// Delete the specified subscription
        /// </summary>
        public Resource Delete(string id, TransactionMode mode)
        {
            var key = this.m_pubSubManager.GetSubscriptionByName(id)?.Key;
            if (key == null)
            {
                this.m_tracer.TraceError($"Subscription {id} not found");
                throw new KeyNotFoundException(this.m_localizationService.GetString("error.type.KeyNotFoundException"));
            }

            PubSubSubscriptionDefinition retVal = null;
            if (mode == TransactionMode.Commit)
            {
                retVal = this.m_pubSubManager.RemoveSubscription(key.Value);
                this.m_pubSubManager.RemoveChannel(retVal.ChannelKey);
            }

            return this.MapToFhir(retVal);
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
        public Bundle History(string id)
        {
            this.m_tracer.TraceError("Versioning is not supported on this object");
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <summary>
        /// Query the subscription object
        /// </summary>
        public Bundle Query(System.Collections.Specialized.NameValueCollection parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(ErrorMessages.ARGUMENT_NULL);
            }

            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(Subscription), typeof(PubSubSubscriptionDefinition), parameters, out var hdsiQuery);
            hdsiQuery.Add("obsoletionTime", "null");
            // Do the query
            var predicate = QueryExpressionParser.BuildLinqExpression<PubSubSubscriptionDefinition>(hdsiQuery);
            IQueryResultSet hdsiResults = this.m_pubSubManager.FindSubscription(predicate);
            var results = query.ApplyCommonQueryControls(hdsiResults, out int totalResults).OfType<PubSubSubscriptionDefinition>();

            var auth = AuthenticationContext.Current.Principal;
            // Return FHIR query result
            var retVal = new FhirQueryResult(nameof(Subscription))
            {
                Results = results.AsParallel().Select(o =>
                {
                    using (AuthenticationContext.EnterContext(auth))
                    {
                        return new Bundle.EntryComponent()
                        {
                            Resource = this.MapToFhir(o),
                            Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Match }
                        };
                    }
                }).ToList(),
                Query = query,
                TotalResults = totalResults
            };
            return MessageUtil.CreateBundle(retVal, Bundle.BundleType.Searchset);
        }

        /// <summary>
        /// Fetch the specified query identifier
        /// </summary>
        public Resource Read(string id, string versionId)
        {
            var retVal = this.m_pubSubManager.GetSubscriptionByName(id);
            return this.MapToFhir(retVal);
        }

        /// <summary>
        /// Updates the specified subscription
        /// </summary>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            if (!(target is Subscription subscription))
            {
                throw new ArgumentException(this.m_localizationService.GetString("error.type.InvalidDataException.userMessage", new
                {
                    param = "subscription resource"
                }));
            }
            var key = this.m_pubSubManager.GetSubscriptionByName(id)?.Key;
            if (key == null)
            {
                this.m_tracer.TraceError($"Subscription {id} not found");
                throw new KeyNotFoundException(this.m_localizationService.GetString("error.type.KeyNotFoundException"));
            }

            // Now update the data
            this.m_tracer.TraceInfo("Will update subscription {0}...", target.Id);

            var queryObject = new Uri($"http://nil/{subscription.Criteria}");
            var enumType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(queryObject.LocalPath.Substring(1));
            var cdrType = FhirResourceHandlerUtil.GetResourceHandler(enumType.Value) as IFhirResourceMapper;
            if (cdrType == null)
            {
                this.m_tracer.TraceError($"Resource type {enumType.Value} is not supported by this service");
                throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
            }

            QueryRewriter.RewriteFhirQuery(cdrType.ResourceClrType, cdrType.CanonicalType, queryObject.Query.Substring(1).ParseQueryString(), out NameValueCollection hdsiQuery);

            // Update the channel
            var retVal = this.m_pubSubManager.UpdateSubscription(key.Value, subscription.Id, subscription.Reason, PubSubEventType.Create | PubSubEventType.Update | PubSubEventType.Delete, hdsiQuery.ToHttpString(), supportAddress: subscription.Contact?.FirstOrDefault()?.Value, notAfter: subscription.End);
            this.m_pubSubManager.ActivateSubscription(key.Value, subscription.Status == SubscriptionStatusCodes.Active);

            var settings = subscription.Channel.Header.Select(o => o.Split(':')).ToDictionary(o => o[0], o => o[1]);
            settings.Add("Content-Type", subscription.Channel.Payload);
            this.m_pubSubManager.UpdateChannel(retVal.ChannelKey, $"Channel for {subscription.Id}", new Uri(subscription.Channel.Endpoint), settings);
            return this.MapToFhir(retVal);
        }

        /// <summary>
        /// Map the model pub-sub description to FHIR
        /// </summary>
        private Subscription MapToFhir(PubSubSubscriptionDefinition model)
        {
            // Construct the return subscription
            var retVal = DataTypeConverter.CreateResource<Subscription>(model);

            // Map status based on current state in CDR
            retVal.Id = model.Name;
            retVal.Reason = model.Description;
            retVal.Contact = new List<ContactPoint>()
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Other, ContactPoint.ContactPointUse.Temp, model.SupportContact)
            };

            retVal.Status = model.IsActive ? SubscriptionStatusCodes.Active : SubscriptionStatusCodes.Off;
            if (model.NotBefore > DateTime.Now)
            {
                retVal.Status = SubscriptionStatusCodes.Requested;
            }

            if (model.NotAfter.HasValue)
            {
                retVal.End = model.NotAfter.Value;
            }

            var channel = this.m_pubSubManager.GetChannel(model.ChannelKey);
            // Map channel information
            retVal.Channel = new Subscription.ChannelComponent()
            {
                Type = new Uri(channel.Endpoint).Scheme == "sms" ? Subscription.SubscriptionChannelType.Sms :
                    new Uri(channel.Endpoint).Scheme == "mailto" ? Subscription.SubscriptionChannelType.Email :
                   "fhir-message" == channel.DispatcherFactoryId ? Subscription.SubscriptionChannelType.Message :
                   Subscription.SubscriptionChannelType.RestHook,
                Endpoint = channel.Endpoint.ToString(),
                Header = channel.Settings.Where(o => !o.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Select(o => $"{o.Name}: {o.Value}"),
                Payload = channel.Settings.FirstOrDefault(o => o.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value,
            };

            retVal.End = model.NotAfter;
            // TODO: Map the HDSI query syntax to FHIR PATH
            var mapper = FhirResourceHandlerUtil.GetMappersFor(model.ResourceType).FirstOrDefault();
            retVal.Criteria = $"{mapper.ResourceType}?";

            return retVal;
        }

        /// <summary>
        /// Create channel
        /// </summary>
        /// <param name="name">The pub-sub channel definition name.</param>
        /// <param name="fhirChannel"></param>
        /// <param name="mode">Pass <see cref="TransactionMode.Commit"/> to register the channel. Other values will validate the parameters only.</param>
        /// <returns>The channel definition that is created. If <paramref name="mode"/> is not <see cref="TransactionMode.Commit"/>, this value will be <c>null</c>.</returns>
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
                        throw new ArgumentOutOfRangeException(this.m_localizationService.GetString("error.messaging.fhir.subscription.emailScheme", new
                        {
                            param = "mailto:"
                        }));
                    }
                    if (mode == TransactionMode.Commit) // Actually register the channel
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, String.Empty, new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;

                case Subscription.SubscriptionChannelType.Sms:
                    // TODO: E-mail dispatcher
                    if (!fhirChannel.Endpoint.StartsWith("sms:"))
                    {
                        throw new ArgumentOutOfRangeException(this.m_localizationService.GetString("error.messaging.fhir.subscription.emailScheme", new
                        {
                            param = "sms:"
                        }));
                    }
                    if (mode == TransactionMode.Commit) // Actually register the channel
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, String.Empty, new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;

                case Subscription.SubscriptionChannelType.RestHook:
                    if (mode == TransactionMode.Commit) // Actually register the channel
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, typeof(FhirPubSubRestHookDispatcherFactory), new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;

                case Subscription.SubscriptionChannelType.Message:
                    if (mode == TransactionMode.Commit)
                    {
                        channel = this.m_pubSubManager.RegisterChannel(name, typeof(FhirPubSubMessageDispatcherFactory), new Uri(fhirChannel.Endpoint), settings);
                    }
                    break;

                default:
                    this.m_tracer.TraceError($"Resource channel type {fhirChannel.Type} not supported ");
                    throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
            }

            return channel;
        }
    }
}
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
using DocumentFormat.OpenXml.Office2010.Excel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using SanteDB;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.PubSub;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Rest;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.PubSub
{
    /// <summary>
    /// A pub-sub dispatch factory which can send rest hook
    /// </summary>
    public class FhirPubSubRestHookDispatcherFactory : IPubSubDispatcherFactory
    {

        /// <summary>
        /// Notify the bundles
        /// </summary>
        public const string NotifyBundlesSettingName = "notify.bundle";

        /// <summary>
        /// Notify local instances
        /// </summary>
        public const string NotifyLocalSettingName = "notify.local";

        /// <summary>
        /// True if managed links should be sent as merges
        /// </summary>
        public const string LinkAsMergeSettingName = "linkAsMerge";

        // Created authenticators for each channel
        private static readonly IDictionary<Guid, IFhirClientAuthenticator> m_createdAuthenticators = new ConcurrentDictionary<Guid, IFhirClientAuthenticator>();

        /// <summary>
        /// Fhir rest based
        /// </summary>
        public string Id => "fhir-rest-hook";

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
            private readonly Tracer m_tracer = Tracer.GetTracer(typeof(Dispatcher));
            private readonly IFhirClientAuthenticator m_authenticator;

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

                this.m_relationshipMapper = FhirResourceHandlerUtil.GetMapperForInstance(new EntityRelationship());
                this.m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>()?.GetSection<FhirDispatcherConfigurationSection>()?.Targets.Find(o => o.Endpoint == endpoint.ToString());

                settings.TryGetValue("Content-Type", out string contentType);

                // The client for this object
                this.m_client = new FhirClient(this.Endpoint, new FhirClientSettings()
                {
                    ParserSettings = new Hl7.Fhir.Serialization.ParserSettings()
                    {
                        AllowUnrecognizedEnums = true,
                        PermissiveParsing = true,
                        AcceptUnknownMembers = true
                    },
                    PreferredFormat = ContentType.GetResourceFormatFromFormatParam(contentType ?? "xml"),
                    PreferCompressedResponses = true,
                    VerifyFhirVersion = false
                });

                foreach (var kv in this.Settings.Where(z => z.Key != "Content-Type" && !z.Key.StartsWith("$")))
                {
                    this.m_client.RequestHeaders.Add(kv.Key, kv.Value);
                }


                if (!m_createdAuthenticators.TryGetValue(channelKey, out this.m_authenticator))
                {
                    if (this.m_configuration?.Authenticator != null)
                    {
                        this.m_authenticator = this.m_configuration.Authenticator.Type.CreateInjected() as IFhirClientAuthenticator;
                    }
                    else if (this.Settings.TryGetValue(FhirConstants.DispatcherClassSettingName, out var dispatcher) && MessageUtil.TryCreateAuthenticator(dispatcher, out this.m_authenticator))
                    {
                        m_createdAuthenticators.Add(channelKey, this.m_authenticator);
                    }
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

            private readonly IFhirResourceMapper m_relationshipMapper;

            /// <summary>
            /// Convert <paramref name="data"/> to a FHIR resource
            /// </summary>
            /// <typeparam name="TModel">The type of model</typeparam>
            /// <param name="data">The data to be converted</param>
            /// <returns>The converted resource</returns>
            private Resource ConvertToResource<TModel>(TModel data)
            {
                // First we want to ensure that this is the correct type
                if ((!this.Settings.TryGetValue(FhirPubSubRestHookDispatcherFactory.NotifyLocalSettingName, out var notifyLocalStr) || !Boolean.TryParse(notifyLocalStr, out bool notifiyLocal) || !notifiyLocal) && data is IdentifiedData id)
                {
                    data = (TModel)(object)id.ResolveGoldenRecord();
                }

                var mapper = FhirResourceHandlerUtil.GetMapperForInstance(data);
                if (mapper == null)
                {
                    throw new InvalidOperationException("Cannot determine how to convert resource for notification");
                }

                if (!this.Settings.TryGetValue(FhirPubSubRestHookDispatcherFactory.NotifyBundlesSettingName, out var notifyBundleStr) || !Boolean.TryParse(notifyBundleStr, out bool notifyBundle) || !notifyBundle)
                {
                    return mapper.MapToFhir(data as IdentifiedData);
                }
                else if (data is IdentifiedData id2)
                {
                    // Create the transaction bundle 
                    var retVal = new Bundle()
                    {
                        Type = Bundle.BundleType.Transaction,
                        Entry = new List<Bundle.EntryComponent>() {
                            new Bundle.EntryComponent()
                            {
                                Request = new Bundle.RequestComponent()
                                {
                                    Method = DataTypeConverter.ConvertBatchOperationToHttpVerb(id2.BatchOperation),
                                    Url = $"{mapper.ResourceType}/{id2.Key}",
                                },
                                FullUrl = $"urn:uuid:{id2.Key}",
                                Resource = mapper.MapToFhir(id2)
                            }
                        }
                    };

                    this.AddRelatedObjectsToBundle(id2, retVal);

                    return retVal;
                }
                else
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.MAP_INCOMPATIBLE_TYPE, data.GetType(), typeof(IdentifiedData)));
                }
            }

            /// <summary>
            /// Add all related objects to the bundle as FHIR objects
            /// </summary>
            private void AddRelatedObjectsToBundle(IdentifiedData data, Bundle bundleToAddTo)
            {
                switch (data)
                {
                    case Entity ent:
                        foreach (var er in ent.LoadProperty(o => o.Relationships))
                        {
                            if (this.m_relationshipMapper.CanMapObject(er))
                            {
                                bundleToAddTo.Entry.Add(new Bundle.EntryComponent()
                                {
                                    Request = new Bundle.RequestComponent()
                                    {
                                        Method = Bundle.HTTPVerb.POST,
                                        Url = $"{this.m_relationshipMapper.ResourceType}/{er.Key}",
                                    },
                                    FullUrl = $"urn:uuid:{er.Key}",
                                    Resource = this.m_relationshipMapper.MapToFhir(er)
                                });
                            }
                            else
                            {
                                var entity = er.LoadProperty(o => o.TargetEntity);
                                var mapper = FhirResourceHandlerUtil.GetMapperForInstance(entity);
                                if (mapper == null)
                                {
                                    bundleToAddTo.Entry.Add(new Bundle.EntryComponent()
                                    {
                                        FullUrl = $"urn:uuid:{entity.Key}",
                                        Request = new Bundle.RequestComponent()
                                        {
                                            Url = $"{mapper.ResourceType}/{entity.Key}",
                                            Method = Bundle.HTTPVerb.POST
                                        },
                                        Resource = mapper.MapToFhir(entity)
                                    });
                                }
                            }
                        }
                        break;
                    case Act act:
                        foreach (var ar in act.LoadProperty(o => o.Relationships))
                        {
                            var tact = ar.LoadProperty(o => o.TargetAct);
                            var mapper = FhirResourceHandlerUtil.GetMapperForInstance(tact);
                            if (mapper == null)
                            {
                                bundleToAddTo.Entry.Add(new Bundle.EntryComponent()
                                {
                                    FullUrl = $"urn:uuid:{tact.Key}",
                                    Request = new Bundle.RequestComponent()
                                    {
                                        Url = $"{mapper.ResourceType}/{tact.Key}",
                                        Method = Bundle.HTTPVerb.POST
                                    },
                                    Resource = mapper.MapToFhir(tact)
                                });
                            }
                        }
                        foreach (var ap in act.LoadProperty(o => o.Participations))
                        {
                            var entity = ap.LoadProperty(o => o.PlayerEntity);
                            var mapper = FhirResourceHandlerUtil.GetMapperForInstance(entity);
                            if (mapper == null)
                            {
                                bundleToAddTo.Entry.Add(new Bundle.EntryComponent()
                                {
                                    FullUrl = $"urn:uuid:{entity.Key}",
                                    Request = new Bundle.RequestComponent()
                                    {
                                        Url = $"{mapper.ResourceType}/{entity.Key}",
                                        Method = Bundle.HTTPVerb.POST
                                    },
                                    Resource = mapper.MapToFhir(entity)
                                });
                            }
                        }
                        break;
                }

            }

            /// <summary>
            /// Notify that an object was created
            /// </summary>
            public void NotifyCreated<TModel>(TModel data) where TModel : IdentifiedData
            {
                try
                {
                    var resource = this.ConvertToResource(data);
                    this.m_authenticator?.AddAuthenticationHeaders(this.m_client, this.m_configuration?.UserName, this.m_configuration?.Password, this.Settings);
                    this.m_client.Create(resource);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not send create to {0} for {1} - {2}", this.Endpoint, data, e);
                    throw new DataDispatchException($"Could not send REST create to {this.Endpoint}", e);
                }
            }

            /// <summary>
            /// Notify object was merged
            /// </summary>
            public void NotifyMerged<TModel>(TModel survivor, IEnumerable<TModel> subsumed) where TModel : IdentifiedData
            {
                if (survivor is Core.Model.Roles.Patient patient)
                {
                    try
                    {
                        var resource = this.ConvertToResource(patient) as Patient;

                        // Add a replaces link 
                        subsumed.Where(ssb => !resource.Link.Any(rl => rl.Other.Reference.Contains(ssb.Key.ToString()))).ForEach(rs => resource.Link.Add(new Patient.LinkComponent()
                        {
                            Type = Patient.LinkType.Replaces,
                            Other = DataTypeConverter.CreateRimReference(rs)
                        }));
                        this.m_authenticator?.AddAuthenticationHeaders(this.m_client, this.m_configuration?.UserName, this.m_configuration?.Password, this.Settings);
                        this.m_client.Update(resource);
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Could not send update to {0} for {1} - {2}", this.Endpoint, survivor, e);
                        throw new DataDispatchException($"Could not send REST update to {this.Endpoint}", e);
                    }
                }
                else
                {
                    throw new NotSupportedException("FHIR cannot express merge of this type of data");
                }
            }

            /// <summary>
            /// Notify obsoleted
            /// </summary>
            public void NotifyObsoleted<TModel>(TModel data) where TModel : IdentifiedData
            {
                try
                {
                    var resource = this.ConvertToResource(data);
                    this.m_authenticator?.AddAuthenticationHeaders(this.m_client, this.m_configuration?.UserName, this.m_configuration?.Password, this.Settings);
                    this.m_client.Delete(resource);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not send obsolete to {0} for {1} - {2}", this.Endpoint, data, e);
                    throw new DataDispatchException($"Could not send REST delete to {this.Endpoint}", e);
                }
            }

            /// <summary>
            /// Notify unmerged
            /// </summary>
            public void NotifyUnMerged<TModel>(TModel primary, IEnumerable<TModel> unMerged) where TModel : IdentifiedData
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Notify updated
            /// </summary>
            public void NotifyUpdated<TModel>(TModel data) where TModel : IdentifiedData
            {
                try
                {
                    var resource = this.ConvertToResource(data);
                    this.m_authenticator?.AddAuthenticationHeaders(this.m_client, this.m_configuration?.UserName, this.m_configuration?.Password, this.Settings);
                    this.m_client.Update(resource);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not send update to {0} for {1} - {2}", this.Endpoint, data, e);
                    throw new DataDispatchException($"Could not send REST update to {this.Endpoint}", e);
                }
            }

            /// <inheritdoc/>
            public void NotifyLinked<TModel>(TModel holder, TModel target) where TModel : IdentifiedData
            {
                if (this.Settings.TryGetValue(FhirPubSubRestHookDispatcherFactory.LinkAsMergeSettingName, out var linkAsMergeStr) &&
                    Boolean.TryParse(linkAsMergeStr, out var linkAsMerge) &&
                    linkAsMerge)
                {
                    this.NotifyMerged(holder, new TModel[] { target });
                }
                else
                {
                    this.NotifyUpdated(holder);
                }
            }

            /// <inheritdoc/>
            public void NotifyUnlinked<TModel>(TModel holder, TModel target) where TModel : IdentifiedData
            {
                if (this.Settings.TryGetValue(FhirPubSubRestHookDispatcherFactory.LinkAsMergeSettingName, out var linkAsMergeStr) &&
                    Boolean.TryParse(linkAsMergeStr, out var linkAsMerge) &&
                    linkAsMerge)
                {
                    this.NotifyUnMerged(holder, new TModel[] { target });
                }
                else
                {
                    this.NotifyUpdated(holder);
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
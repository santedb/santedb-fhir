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
using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.BI.Model;
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Extensions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Text;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using static Hl7.Fhir.Model.OperationOutcome;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Represents a data type converter.
    /// </summary>
    public static class DataTypeConverter
    {
        /// <summary>
        /// The trace source.
        /// </summary>
        private static readonly Tracer traceSource = new Tracer(FhirConstants.TraceSourceName);

        // Source device
        private static SecurityDevice m_sourceDevice;

        // Policy information service
        private static IPolicyInformationService m_pipService = ApplicationServiceContext.Current.GetService<IPolicyInformationService>();

        // Security repository
        private static ISecurityRepositoryService m_secService = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();

        // CX Devices
        private static readonly Regex m_cxDevice = new Regex(@"^(.*?)\^\^\^([A-Z_0-9]*)(?:&(.*?)&ISO)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Refernece regex
        private static readonly Regex m_referenceRegex = new Regex(@"^(?:urn:uuid:([A-F0-9\-]{36})|\/?(\w+?)\/([A-F0-9\-]{36}))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Configuration
        private static readonly FhirServiceConfigurationSection m_configuration;

        // This regex is used because we want to be more forgiving for parsing telecom URIs which may have 
        // symbols like: tel:+1(905)293-4039
        private static readonly Regex m_telecomUri = new Regex(@"^(\w+:\/{0,2})(.*)");

        /// <summary>
        /// Static ctor
        /// </summary>
        static DataTypeConverter()
        {
            m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<FhirServiceConfigurationSection>();
        }

        /// <summary>
        /// Convert the audit data to a security audit
        /// </summary>
        public static AuditEvent ToSecurityAudit(AuditEventData audit)
        {
            var conceptService = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();

            var retVal = new AuditEvent()
            {
                Id = audit.Key.ToString(),
                Recorded = audit.Timestamp,
            };

            // Event type primary classifier
            var idConcept = conceptService.GetConceptReferenceTerm($"SecurityAuditCode-{audit.EventIdentifier}", "DCM");
            if (idConcept != null)
            {
                retVal.Type = ToCoding(idConcept);
            }

            // Event sub-type
            if (audit.EventTypeCode != null)
            {
                var refTerm = conceptService.GetConceptReferenceTerm(audit.EventTypeCode.Code, "DCM");
                if (refTerm != null)
                {
                    retVal.Subtype.Add(ToCoding(refTerm));
                }
                else
                {
                    retVal.Subtype.Add(new Coding(audit.EventTypeCode.CodeSystem, audit.EventTypeCode.Code) { Display = audit.EventTypeCode.DisplayName });
                }
            }

            // Outcome
            switch (audit.Outcome)
            {
                case OutcomeIndicator.EpicFail:
                    retVal.Outcome = AuditEvent.AuditEventOutcome.N12;
                    break;

                case OutcomeIndicator.SeriousFail:
                    retVal.Outcome = AuditEvent.AuditEventOutcome.N8;
                    break;

                case OutcomeIndicator.MinorFail:
                    retVal.Outcome = AuditEvent.AuditEventOutcome.N4;
                    break;

                case OutcomeIndicator.Success:
                    retVal.Outcome = AuditEvent.AuditEventOutcome.N0;
                    break;
            }

            // Action type
            switch (audit.ActionCode)
            {
                case ActionType.Create:
                    retVal.Action = AuditEvent.AuditEventAction.C;
                    break;

                case ActionType.Delete:
                    retVal.Action = AuditEvent.AuditEventAction.D;
                    break;

                case ActionType.Execute:
                    retVal.Action = AuditEvent.AuditEventAction.E;
                    break;

                case ActionType.Read:
                    retVal.Action = AuditEvent.AuditEventAction.R;
                    break;

                case ActionType.Update:
                    retVal.Action = AuditEvent.AuditEventAction.U;
                    break;
            }

            // POU?
            var pou = audit.AuditableObjects.FirstOrDefault(o => o.ObjectId == SanteDBClaimTypes.PurposeOfUse)?.NameData;
            if (pou != null && Guid.TryParse(pou, out Guid pouKey))
            {
                retVal.PurposeOfEvent = new List<CodeableConcept>() { ToFhirCodeableConcept(pouKey) };
            }

            // Actors
            foreach (var act in audit.Actors)
            {
                var ntype = AuditEvent.AuditEventAgentNetworkType.N1;
                switch (act.NetworkAccessPointType)
                {
                    case NetworkAccessPointType.IPAddress:
                        ntype = AuditEvent.AuditEventAgentNetworkType.N2;
                        break;

                    case NetworkAccessPointType.MachineName:
                        ntype = AuditEvent.AuditEventAgentNetworkType.N1;
                        break;

                    case NetworkAccessPointType.TelephoneNumber:
                        ntype = AuditEvent.AuditEventAgentNetworkType.N3;
                        break;

                    default:
                        ntype = AuditEvent.AuditEventAgentNetworkType.N5;
                        break;
                }

                retVal.Agent.Add(new AuditEvent.AgentComponent()
                {
                    AltId = act.AlternativeUserId,
                    Role = act.ActorRoleCode.Skip(1).Select(o => ToFhirCodeableConcept(conceptService.GetConceptByReferenceTerm(o.Code, o.CodeSystem).Key)).ToList(),
                    Type = act.ActorRoleCode.Take(1).Select(o => ToFhirCodeableConcept(conceptService.GetConceptByReferenceTerm(o.Code, o.CodeSystem).Key)).FirstOrDefault(),
                    Name = act.UserName,
                    Network = new AuditEvent.NetworkComponent()
                    {
                        Address = act.NetworkAccessPointId,
                        Type = ntype
                    },
                    Requestor = act.UserIsRequestor
                });
            }

            // Source
            String enterprise = audit.Metadata.Find(o => o.Key == AuditMetadataKey.EnterpriseSiteID)?.Value,
                sourceId = audit.Metadata.Find(o => o.Key == AuditMetadataKey.AuditSourceID)?.Value,
                sourceType = audit.Metadata.Find(o => o.Key == AuditMetadataKey.AuditSourceType)?.Value;
            if (!String.IsNullOrEmpty(sourceId) && Guid.TryParse(sourceId, out Guid originalSource))
            {
                AuditSourceType sourceTypeEnum = 0;
                if (Int32.TryParse(sourceType, out int sType))
                {
                    sourceTypeEnum = (AuditSourceType)sType;
                }
                else if (!Enum.TryParse<AuditSourceType>(sourceType, out sourceTypeEnum))
                {
                    sourceTypeEnum = AuditSourceType.Other;
                }

                retVal.Source = new AuditEvent.SourceComponent()
                {
                    Observer = CreateNonVersionedReference<Device>(originalSource),
                    Type = new List<Coding>() { new Coding("http://terminology.hl7.org/CodeSystem/security-source-type", ((int)sourceTypeEnum).ToString()) }
                };
            }
            else
            {
                if (m_sourceDevice == null)
                {
                    using (AuthenticationContext.EnterSystemContext())
                    {
                        var configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<AuditAccountabilityConfigurationSection>();
                        if (configuration.SourceInformation?.EnterpriseDeviceKey != null)
                        {
                            m_sourceDevice = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityDevice>>()?.Get(configuration.SourceInformation.EnterpriseDeviceKey);
                        }
                        if (m_sourceDevice == null)
                        {
                            m_sourceDevice = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>()?.GetDevice(Environment.MachineName);
                            if (m_sourceDevice == null)
                            {
                                m_sourceDevice = new SecurityDevice()
                                {
                                    Name = Environment.MachineName,
                                    DeviceSecret = Guid.NewGuid().ToString(),
                                    Lockout = DateTimeOffset.MaxValue
                                };
                                m_sourceDevice = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityDevice>>().Insert(m_sourceDevice);
                            }
                        }
                    }
                }

                retVal.Source = new AuditEvent.SourceComponent()
                {
                    Observer = CreateNonVersionedReference<Device>(m_sourceDevice),
                    Type = new List<Coding>() { new Coding("http://terminology.hl7.org/CodeSystem/security-source-type", "4") }
                };
            }

            // Objects / entities
            foreach (var itm in audit.AuditableObjects)
            {
                var add = new AuditEvent.EntityComponent()
                {
                    Name = itm.NameData,
                    Query = String.IsNullOrEmpty(itm.QueryData) ? null : System.Text.Encoding.UTF8.GetBytes(itm.QueryData)
                };

                if (itm.Type != AuditableObjectType.NotSpecified)
                {
                    add.Type = new Coding("http://terminology.hl7.org/CodeSystem/audit-entity-type", ((int)itm.Type).ToString());
                }
                if (itm.Role.HasValue)
                {
                    add.Role = new Coding("http://terminology.hl7.org/CodeSystem/object-role", ((int)itm.Role).ToString());
                }
                if (itm.LifecycleType.HasValue)
                {
                    add.Lifecycle = new Coding("http://terminology.hl7.org/CodeSystem/dicom-audit-lifecycle", ((int)itm.LifecycleType).ToString());
                }

                foreach (var dtl in itm.ObjectData)
                {
                    add.Detail.Add(new AuditEvent.DetailComponent()
                    {
                        Type = dtl.Key,
                        Value = new Base64Binary(dtl.Value)
                    });
                }

                if (!String.IsNullOrEmpty(itm.ObjectId))
                {
                    var objectId = itm.ObjectId;
                    var cxMatch = m_cxDevice.Match(objectId);
                    if (cxMatch.Success)
                    {
                        objectId = cxMatch.Groups[1].Value;
                    }

                    var isUuid = Guid.TryParse(objectId?.Replace("urn:uuid:", ""), out Guid objectIdKey);

                    if (itm.IDTypeCode.HasValue)
                    {
                        switch (itm.IDTypeCode)
                        {
                            case AuditableObjectIdType.PatientNumber:

                                if (isUuid)
                                {
                                    add.What = CreateNonVersionedReference<Patient>(objectIdKey);
                                }
                                else
                                {
                                    goto default; // HACK: Fallthrough to default case
                                }
                                break;

                            case AuditableObjectIdType.UserIdentifier:
                                if (isUuid)
                                {
                                    add.What = CreateNonVersionedReference<Practitioner>(objectIdKey);
                                }
                                else
                                {
                                    goto default; // HACK: Fallthrough to default case
                                }
                                break;

                            case AuditableObjectIdType.EncounterNumber:
                                if (isUuid)
                                {
                                    add.What = CreateNonVersionedReference<Encounter>(objectIdKey);
                                }
                                else
                                {
                                    goto default; // HACK: Fallthrough to default case
                                }
                                break;

                            default:
                                add.What = new ResourceReference()
                                {
                                    Identifier = new Identifier(null, isUuid ? $"urn:uuid:{objectIdKey}" : itm.ObjectId)
                                    {
                                        Type = new CodeableConcept()
                                        {
                                            Text = itm.CustomIdTypeCode?.DisplayName,
                                            Coding = itm.IDTypeCode == AuditableObjectIdType.Custom ? new List<Coding>()
                                        {
                                            new Coding(itm.CustomIdTypeCode.CodeSystem, itm.CustomIdTypeCode.Code)
                                        } : new List<Coding>()
                                        {
                                            new Coding("http://santedb.org/conceptset/audit-idtype-code", ((int)itm.IDTypeCode).ToString())
                                        }
                                        },
                                        System = cxMatch.Success && cxMatch.Groups.Count > 3 ? $"urn:oid:{cxMatch.Groups[3].Value}" : null,
                                        Value = cxMatch.Success && cxMatch.Groups.Count > 3 ? cxMatch.Groups[1].Value : itm.ObjectId
                                    }
                                };
                                break;
                        }
                    }
                }
                retVal.Entity.Add(add);
            }

            return retVal;
        }


        internal static List<T> ToNote<T>(Hl7.Fhir.Model.Narrative text) where T : INote, new()
        {
            if (text == null || String.IsNullOrEmpty(text.Div))
            {
                return new List<T>();
            }

            switch (text.Status)
            {
                case Hl7.Fhir.Model.Narrative.NarrativeStatus.Additional:
                    // Get the relevant identification
                    var authorEntity = m_secService.GetCdrEntity(AuthenticationContext.Current.Principal);
                    if (authorEntity == null)
                    {
                        if (m_configuration.StrictProcessing)
                        {
                            throw new FhirException(System.Net.HttpStatusCode.BadRequest, IssueType.NotFound, $"{AuthenticationContext.Current.Principal.Identity.Name} is unknown");
                        }
                        else
                        {
                            traceSource.TraceWarning("Could not find authorship information for {0} - narrative text cannot be saved", AuthenticationContext.Current.Principal);
                            return new List<T>();
                        }
                    }
                    return new List<T>() {  new T()
                        {
                            AuthorKey = authorEntity.Key,
                            Text = text.Div
                        }
                    };
                case Hl7.Fhir.Model.Narrative.NarrativeStatus.Extensions:
                    if (m_configuration.StrictProcessing)
                    {
                        throw new FhirException(System.Net.HttpStatusCode.BadRequest, IssueType.NotSupported, $"Cannot understand narrative text with status extensions");
                    }
                    else
                    {
                        traceSource.TraceWarning("Cannot understand narrative text with status extensions", AuthenticationContext.Current.Principal);
                        return new List<T>();
                    }
                default:
                    traceSource.TraceWarning("Will not store generated narrative text");
                    return new List<T>();
            }
        }

        /// <summary>
        /// Creates a FHIR reference.
        /// </summary>
        /// <typeparam name="TResource">The type of the t resource.</typeparam>
        /// <param name="targetEntity">The target entity.</param>
        /// <returns>Returns a reference instance.</returns>
        public static ResourceReference CreateVersionedReference<TResource>(IVersionedData targetEntity)
            where TResource : DomainResource, new()
        {
            if (targetEntity == null)
            {
                throw new ArgumentNullException(nameof(targetEntity));
            }

            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);

            var refer = new ResourceReference($"{fhirType}/{targetEntity.Key}/_history/{targetEntity.VersionKey}");

            // Add an identifier to the object
            if (targetEntity is IHasIdentifiers ident)
            {
                var uqIdentifier = ident.LoadCollection(x => x.Identifiers).FirstOrDefault(i => i.IdentityDomain.IsUnique);
                if (uqIdentifier != null)
                {
                    refer.Identifier = new Identifier(uqIdentifier.IdentityDomain.Url, uqIdentifier.Value);
                }
            }

            if (targetEntity is IdentifiedData id)
            {
                refer.Display = id.ToDisplay();
            }
            else
            {
                refer.Display = targetEntity.ToString();
            }
            return refer;
        }

        /// <summary>
        /// Creates a FHIR reference from a target object
        /// </summary>
        public static ResourceReference CreateRimReference(IdentifiedData targetObject)
        {

            if (targetObject == null)
            {
                throw new ArgumentNullException(nameof(targetObject));
            }

            var mapper = FhirResourceHandlerUtil.GetMappersFor(targetObject.GetType()).FirstOrDefault();
            if (mapper == null)
            {
                throw new InvalidOperationException("Configuration for mapper is not available");
            }


            var refer = new ResourceReference($"{mapper.ResourceType}/{targetObject.Key}");

            // Add an identifier to the object
            if (targetObject is IHasIdentifiers ident)
            {
                var uqIdentifier = ident.LoadCollection(x => x.Identifiers).FirstOrDefault(i => i.IdentityDomain.IsUnique);
                if (uqIdentifier != null)
                {
                    refer.Identifier = new Identifier(uqIdentifier.IdentityDomain.Url, uqIdentifier.Value);
                }
            }

            refer.Display = targetObject.ToDisplay();
            return refer;
        }

        /// <summary>
        /// Creates a FHIR reference.
        /// </summary>
        /// <typeparam name="TResource">The type of the resource.</typeparam>
        /// <param name="targetEntity">The target entity.</param>
        /// <returns>Returns a reference instance.</returns>
        public static ResourceReference CreateNonVersionedReference<TResource>(IdentifiedData targetEntity) where TResource : DomainResource, new()
        {

            if (targetEntity == null)
            {
                throw new ArgumentNullException(nameof(targetEntity));
            }

            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);

            var refer = new ResourceReference($"{fhirType}/{targetEntity.Key}");

            // Add an identifier to the object
            if (targetEntity is IHasIdentifiers ident)
            {
                var uqIdentifier = ident.LoadCollection(x => x.Identifiers).FirstOrDefault(i => i.IdentityDomain.IsUnique);
                if (uqIdentifier != null)
                {
                    refer.Identifier = new Identifier(uqIdentifier.IdentityDomain.Url, uqIdentifier.Value);
                }
            }

            refer.Display = targetEntity.ToDisplay();
            return refer;
        }

        /// <summary>
        /// Convert to a fhir code from a codeable concept.
        /// </summary>
        /// <typeparam name="T">The code type to convert to.</typeparam>
        /// <param name="conceptKey">The concept key to convert.</param>
        /// <param name="preferredCodeSystem">Any preferred code systems to use to retrieve the representation from.</param>
        /// <returns>A <see cref="Code{T}"/> instance for the concept.</returns>
        public static Code<T> ToFhirCode<T>(Guid? conceptKey, params string[] preferredCodeSystem) where T : struct, Enum
        {
            if (null == conceptKey)
            {
                return null;
            }

            return new Code<T>
            {
                ObjectValue = ToFhirCodeableConcept(conceptKey, preferredCodeSystem)?.Coding?.FirstOrDefault()?.Code
            };
        }

        /// <summary>
        /// Convert to issue
        /// </summary>
        internal static OperationOutcome.IssueComponent ToIssue(Core.BusinessRules.DetectedIssue issue)
        {
            return new OperationOutcome.IssueComponent()
            {
                Severity = issue.Priority == Core.BusinessRules.DetectedIssuePriorityType.Error ? OperationOutcome.IssueSeverity.Error :
                           issue.Priority == Core.BusinessRules.DetectedIssuePriorityType.Warning ? OperationOutcome.IssueSeverity.Warning :
                           OperationOutcome.IssueSeverity.Information,
                Code = OperationOutcome.IssueType.NoStore,
                Diagnostics = issue.Text
            };
        }

        /// <summary>
        /// Creates a FHIR reference.
        /// </summary>
        /// <typeparam name="TResource">The type of the resource.</typeparam>
        /// <param name="targetEntity">The target entity.</param>
        /// <returns>Returns a reference instance.</returns>
        public static ResourceReference CreateInternalReference<TResource>(IdentifiedData targetEntity) where TResource : DomainResource, new()
        {
            if (targetEntity == null)
            {
                throw new ArgumentNullException(nameof(targetEntity));
            }

            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);
            var refer = new ResourceReference($"#{targetEntity.Key}");
            refer.Display = targetEntity.ToDisplay();
            return refer;
        }

        /// <summary>
        /// To quantity
        /// </summary>
        public static Quantity ToQuantity(decimal? quantity, Guid? unitConceptKey)
        {
            return new Quantity()
            {
                Value = quantity,
                Unit = DataTypeConverter.ToFhirCodeableConcept(unitConceptKey, FhirConstants.DefaultQuantityUnitSystem)?.GetCoding().Code
            };
        }

        /// <summary>
        /// Create an operation outcome from the error
        /// </summary>
        public static Resource CreateErrorResult(Exception error)
        {
            if (error is FhirException fhirException)
            {
                if (fhirException.Resource != null)
                {
                    return fhirException.Resource;
                }
                return new OperationOutcome()
                {
                    Issue = new List<IssueComponent>()
                    {
                        new IssueComponent()
                        {
                            Severity = IssueSeverity.Error,
                            Code = fhirException.Code,
                            Diagnostics = fhirException.Message
                        }
                    }
                };
            }
            else
            {
                // Construct an error result
                var errorResult = new OperationOutcome()
                {
                    Issue = new List<IssueComponent>()
                };

                while (error != null)
                {
                    if (error is DetectedIssueException dte)
                    {
                        errorResult.Issue.AddRange(dte.Issues.Select(iss => new OperationOutcome.IssueComponent()
                        {
                            Diagnostics = iss.Text,
                            Code = ClassifyDetectedIssueKey(iss.TypeKey),
                            Severity = iss.Priority == DetectedIssuePriorityType.Error ? IssueSeverity.Error :
                                iss.Priority == DetectedIssuePriorityType.Warning ? IssueSeverity.Warning :
                                IssueSeverity.Information
                        }));
                    }
                    else
                    {
                        errorResult.Issue.Add(
                            new OperationOutcome.IssueComponent()
                            {
                                Diagnostics = error.Message,
                                Severity = IssueSeverity.Error,
                                Code = ClassifyExceptionCode(error)
                            }
                        );
                    }
                    error = error.InnerException;
                }
                return errorResult;
            }
        }

        /// <summary>
        /// Classify detected issue key
        /// </summary>
        private static IssueType? ClassifyDetectedIssueKey(Guid typeKey)
        {
            if (typeKey == DetectedIssueKeys.AlreadyDoneIssue)
            {
                return IssueType.Duplicate;
            }
            else if (typeKey == DetectedIssueKeys.BusinessRuleViolationIssue)
            {
                return IssueType.BusinessRule;
            }
            else if (typeKey == DetectedIssueKeys.CodificationIssue)
            {
                return IssueType.CodeInvalid;
            }
            else if (typeKey == DetectedIssueKeys.FormalConstraintIssue)
            {
                return IssueType.Required;
            }
            else if (typeKey == DetectedIssueKeys.InvalidDataIssue)
            {
                return IssueType.Required;
            }
            else if (typeKey == DetectedIssueKeys.OtherIssue)
            {
                return IssueType.Unknown;
            }
            else if (typeKey == DetectedIssueKeys.PrivacyIssue || typeKey == DetectedIssueKeys.SecurityIssue)
            {
                return IssueType.Security;
            }
            else if (typeKey == DetectedIssueKeys.SafetyConcernIssue)
            {
                return IssueType.Unknown;
            }
            else
            {
                return IssueType.BusinessRule;
            }
        }

        /// <summary>
        /// Classify exception code
        /// </summary>
        private static IssueType ClassifyExceptionCode(Exception error)
        {
            var retVal = IssueType.Exception;

            if (error is SecurityException || error is AuthenticationException ||
                error is PolicyViolationException)
            {
                return IssueType.Security;
            }
            else if (error is ConstraintException)
            {
                return IssueType.BusinessRule;
            }
            else if (error is DataPersistenceException || error is DbException)
            {
                return IssueType.NoStore;
            }
            else if (error is DuplicateNameException)
            {
                return IssueType.Duplicate;
            }
            else if (error is KeyNotFoundException || error is FileNotFoundException)
            {
                return IssueType.NotFound;
            }

            return retVal;
        }

        /// <summary>
        /// Creates a FHIR reference.
        /// </summary>
        /// <typeparam name="TResource">The type of the resource.</typeparam>
        /// <returns>Returns a reference instance.</returns>
        public static ResourceReference CreateNonVersionedReference<TResource>(Guid? targetKey) where TResource : DomainResource, new()
        {
            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);
            var refer = new ResourceReference($"{fhirType}/{targetKey}");
            return refer;
        }

        /// <summary>
        /// Create non versioned resource
        /// </summary>
        public static TResource CreateResource<TResource>(IAnnotatedResource resource) where TResource : Resource, new()
        {
            var retVal = new TResource();

            // Add annotations
            retVal.Id = resource.Key.ToString();

            // metadata
            retVal.Meta = new Meta()
            {
                LastUpdated = (resource as IdentifiedData).ModifiedOn.DateTime
            };


            if (resource is ITaggable taggable)
            {
                retVal.Meta.Tag = taggable.Tags.Where(o => !o.TagKey.StartsWith("$fhir")).Select(tag => new Coding("http://santedb.org/fhir/tags", $"{tag.TagKey}:{tag.Value}")).ToList();
                foreach (var tag in taggable.Tags)
                {
                    retVal.AddAnnotation(tag);
                }
            }

            // TODO: Configure this namespace / coding scheme
            if (resource is IHasPolicies ihp)
            {
                retVal.Meta.Security = ihp.Policies?.Select(o => new Coding(FhirConstants.SecurityPolicySystem, o.LoadProperty(a => a.Policy).Oid, o.Policy.Name)).ToList();
            }
            else
            {
                retVal.Meta.Security = m_pipService.GetPolicies(resource).Where(o => o.Rule == Core.Model.Security.PolicyGrantType.Grant).Select(o => new Coding(FhirConstants.SecurityPolicySystem, o.Policy.Oid)).ToList();
            }
            //retVal.Meta.Security.Add(new Coding("http://santedb.org/security/policy", PermissionPolicyIdentifiers.ReadClinicalData));

            if (retVal is Hl7.Fhir.Model.IExtendable fhirExtendable && resource is Core.Model.Interfaces.IExtendable extendableObject)
            {
                DataTypeConverter.AddExtensions(extendableObject, fhirExtendable);
            }


            if (resource is IVersionedData vd)
            {
                retVal.VersionId = vd.VersionKey.ToString();
                retVal.Meta.VersionId = vd.VersionKey?.ToString();
            }

            if (retVal is DomainResource dr)
            {
                if (resource.TryGetTextGenerator(out var textGenerator))
                {
                    using (var sw = new StringWriter())
                    {
                        using (var xw = XmlWriter.Create(sw, new XmlWriterSettings()
                        {
                            OmitXmlDeclaration = true,
                            ConformanceLevel = ConformanceLevel.Document,
                            WriteEndDocumentOnClose = true
                        }))
                        {
                            textGenerator.WriteSummary(xw, resource);
                        }
                        dr.Text = new Hl7.Fhir.Model.Narrative(sw.ToString());

                    }
                }
                else if (resource is IdentifiedData idd)
                {
                    dr.Text = new Hl7.Fhir.Model.Narrative(idd.ToDisplay());
                }
            }
            return retVal;
        }

        /// <summary>
        /// Add extensions from <paramref name="extendable"/> to <paramref name="fhirExtension"/>
        /// </summary>
        /// <returns>The extensions that were applied</returns>
        public static IEnumerable<String> AddExtensions(Core.Model.Interfaces.IExtendable extendable, Hl7.Fhir.Model.IExtendable fhirExtension)
        {
            var resource = fhirExtension as Resource;

            if (resource != null && resource.TryDeriveResourceType(out ResourceType rt))
            {
                fhirExtension.Extension = ExtensionUtil.CreateExtensions(extendable as IAnnotatedResource, rt, out IEnumerable<IFhirExtensionHandler> appliedExtensions).ToList();
                fhirExtension.Extension.AddRange(extendable.Extensions.Where(o => o.ExtensionTypeKey != ExtensionTypeKeys.JpegPhotoExtension).Select(DataTypeConverter.ToExtension));

                return appliedExtensions.Select(o => o.ProfileUri?.ToString()).Distinct();
            }
            else
            {
                return new List<String>();
            }
        }

        /// <summary>
        /// Converts a <see cref="DateTime"/> instance to a <see cref="Date"/> instance.
        /// </summary>
        /// <param name="date">The instance to convert.</param>
        /// <returns>Returns the converted instance.</returns>
        public static Date ToFhirDate(DateTime? date)
        {
            return date.HasValue ? new Date(date.Value.Year, date.Value.Month, date.Value.Day) : null;
        }

        /// <summary>
        /// Converts a <see cref="DateTimeOffset"/> instance to a <see cref="FhirDateTime"/> instance.
        /// </summary>
        /// <param name="date">The instance to convert.</param>
        /// <returns>Returns the converted instance.</returns>
        public static FhirDateTime ToFhirDateTime(DateTimeOffset? date)
        {
            return date.HasValue ? new FhirDateTime(date.Value) : null;
        }

        /// <summary>
        /// Convert two date ranges to a period
        /// </summary>
        public static Period ToPeriod(DateTimeOffset? startTime, DateTimeOffset? stopTime)
        {
            return new Period(startTime.HasValue ? new FhirDateTime(startTime.Value) : null, stopTime.HasValue ? new FhirDateTime(stopTime.Value) : null);
        }

        /// <summary>
        /// Converts a <see cref="Extension"/> instance to an <see cref="ActExtension"/> instance.
        /// </summary>
        /// <param name="fhirExtension">The FHIR extension.</param>
        /// <returns>Returns the converted act extension instance.</returns>
        /// <exception cref="System.ArgumentNullException">fhirExtension - Value cannot be null</exception>
        public static ActExtension ToActExtension(Extension fhirExtension, IdentifiedData context)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR extension");

            var extension = new ActExtension()
            {
                ExternalKey = m_configuration?.PersistElementId == true ? fhirExtension.ElementId : null
            };

            if (fhirExtension == null)
            {
                throw new ArgumentNullException(nameof(fhirExtension), "Value cannot be null");
            }
            else if (fhirExtension.Url == ExtensionTypeKeys.DataQualityExtensionName || fhirExtension.Url == ExtensionTypeKeys.JpegPhotoExtensionName)
            {
                return null;
            }

            // First attempt to parse the extension using a parser
            if (!fhirExtension.TryApplyExtension(context))
            {
                var extensionTypeService = ApplicationServiceContext.Current.GetService<IExtensionTypeRepository>();

                extension.ExtensionType = extensionTypeService.Get(new Uri(fhirExtension.Url));
                if (extension.ExtensionType == null)
                {
                    return null;
                }

                //extension.ExtensionValue = fhirExtension.Value;
                if (extension.ExtensionType.ExtensionHandler == typeof(DecimalExtensionHandler) && fhirExtension.Value is FhirDecimal fd)
                {
                    extension.ExtensionValue = fd.Value;
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(StringExtensionHandler) && fhirExtension.Value is FhirString fs)
                {
                    extension.ExtensionValue = fs.Value;
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(DateExtensionHandler) && fhirExtension.Value is FhirDateTime fdto)
                {
                    extension.ExtensionValue = ToDateTimeOffset(fdto);
                }
                // TODO: Implement binary incoming extensions
                else if ((extension.ExtensionType.ExtensionHandler == typeof(BinaryExtensionHandler) ||
                    extension.ExtensionType.ExtensionHandler == typeof(DictionaryExtensionHandler)) && fhirExtension.Value is Base64Binary fbo)
                {
                    extension.ExtensionValueData = fbo.Value;
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(ReferenceExtensionHandler))
                {
                    switch (fhirExtension.Value)
                    {
                        case ResourceReference frr:
                            if (TryResolveResourceReference(frr, null, out var refr))
                            {
                                extension.ExtensionValue = refr;
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format(ErrorMessages.REFERENCE_NOT_FOUND, frr));
                            }
                            break;
                        case CodeableConcept ccc:
                            var concept = ToConcept(ccc);
                            extension.ExtensionValue = concept;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
                else
                {
                    throw new NotImplementedException($"Extension type is not understood");
                }

                // Now will
                return extension;
            }

            return null;
        }

        /// <summary>
        /// Converts an <see cref="Extension"/> instance to an <see cref="ActExtension"/> instance.
        /// </summary>
        /// <param name="fhirExtension">The FHIR extension.</param>
        /// <param name="context">The context object which the extension is attached to.</param>
        /// <returns>Returns the converted act extension instance.</returns>
        /// <exception cref="System.ArgumentNullException">fhirExtension - Value cannot be null</exception>
        public static EntityExtension ToEntityExtension(Extension fhirExtension, IdentifiedData context)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR extension");

            var extension = new EntityExtension()
            {
                ExternalKey = m_configuration?.PersistElementId == true ? fhirExtension.ElementId : null
            };

            if (fhirExtension == null)
            {
                throw new ArgumentNullException(nameof(fhirExtension), "Value cannot be null");
            }
            else if (fhirExtension.Url == ExtensionTypeKeys.DataQualityExtensionName || fhirExtension.Url == ExtensionTypeKeys.JpegPhotoExtensionName)
            {
                return null;
            }

            // First attempt to parse the extension using a parser
            if (!fhirExtension.TryApplyExtension(context))
            {
                var extensionTypeService = ApplicationServiceContext.Current.GetService<IExtensionTypeRepository>();

                extension.ExtensionType = extensionTypeService.Get(new Uri(fhirExtension.Url));
                if (extension.ExtensionType == null)
                {
                    return null;
                }

                //extension.ExtensionValue = fhirExtension.Value;
                if (extension.ExtensionType.ExtensionHandler == typeof(DecimalExtensionHandler) && fhirExtension.Value is FhirDecimal fd)
                {
                    extension.ExtensionValue = fd.Value;
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(StringExtensionHandler) && fhirExtension.Value is FhirString fs)
                {
                    extension.ExtensionValue = fs.Value;
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(DateExtensionHandler) && fhirExtension.Value is FhirDateTime fdto)
                {
                    extension.ExtensionValue = ToDateTimeOffset(fdto);
                }
                // TODO: Implement binary incoming extensions
                else if ((extension.ExtensionType.ExtensionHandler == typeof(BinaryExtensionHandler) ||
                    extension.ExtensionType.ExtensionHandler == typeof(DictionaryExtensionHandler)) && fhirExtension.Value is Base64Binary fbo)
                {
                    extension.ExtensionValueData = fbo.Value;
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(ReferenceExtensionHandler))
                {
                    switch (fhirExtension.Value)
                    {
                        case ResourceReference frr:
                            if (TryResolveResourceReference(frr, null, out var refr))
                            {
                                extension.ExtensionValue = refr;
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format(ErrorMessages.REFERENCE_NOT_FOUND, frr));
                            }
                            break;
                        case CodeableConcept ccc:
                            var concept = ToConcept(ccc);
                            extension.ExtensionValue = concept;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
                else if (extension.ExtensionType.ExtensionHandler == typeof(BooleanExtensionHandler) &&
                    fhirExtension.Value is FhirBoolean fb)
                {
                    extension.ExtensionValue = fb.Value;
                }
                else
                {
                    throw new NotImplementedException($"Extension type {fhirExtension.Url} is not understood (or the data type being used is invalid for the registration)");
                }

                // Now will
                return extension;
            }

            return null;
        }

        /// <summary>
        /// Convert to language of communication
        /// </summary>
        public static PersonLanguageCommunication ToLanguageCommunication(Patient.CommunicationComponent lang)
        {
            return new PersonLanguageCommunication(lang.Language.GetCoding().Code, lang.Preferred.GetValueOrDefault())
            {
                ExternalKey = m_configuration?.PersistElementId == true ? lang.ElementId : null
            };
        }

        /// <summary>
        /// Convert to language of communication
        /// </summary>
        public static PersonLanguageCommunication ToLanguageCommunication(RelatedPerson.CommunicationComponent lang)
        {
            return new PersonLanguageCommunication(lang.Language.GetCoding().Code, lang.Preferred.GetValueOrDefault())
            {
                ExternalKey = m_configuration?.PersistElementId == true ? lang.ElementId : null
            };
        }

        /// <summary>
        /// Convert to language of communication
        /// </summary>
        public static PersonLanguageCommunication ToLanguageCommunication(CodeableConcept lang, bool preferred)
        {
            if (!lang.Coding.Any())
            {
                throw new InvalidOperationException("Codeable concept must contain a language code");
            }

            return new PersonLanguageCommunication(lang.Coding.First().Code, preferred)
            {
                ExternalKey = m_configuration?.PersistElementId == true ? lang.ElementId : null
            };
        }

        /// <summary>
        /// Convert to language of communication
        /// </summary>
        public static Patient.CommunicationComponent ToFhirCommunicationComponent(PersonLanguageCommunication lang)
        {
            return new Patient.CommunicationComponent
            {
                Language = new CodeableConcept("urn:ietf:bcp:47", lang.LanguageCode),
                Preferred = lang.IsPreferred,
                ElementId = m_configuration?.PersistElementId == true ? lang.ExternalKey : null
            };
        }

        /// <summary>
        /// Converts a <see cref="Identifier"/> instance to an <see cref="ActIdentifier"/> instance.
        /// </summary>
        /// <param name="fhirIdentifier">The FHIR identifier.</param>
        /// <returns>Returns the converted act identifier instance.</returns>
        public static ActIdentifier ToActIdentifier(Identifier fhirIdentifier) => ToIdentifier<ActIdentifier>(fhirIdentifier);

        /// <summary>
        /// Converts a <see cref="FhirUri"/> instance to an <see cref="IdentityDomain"/> instance.
        /// </summary>
        /// <param name="fhirSystem">The FHIR system.</param>
        /// <returns>Returns the converted instance.</returns>
        public static IdentityDomain ToAssigningAuthority(FhirUri fhirSystem)
        {
            return fhirSystem == null ? null : ToIdentityDomain(fhirSystem.Value);
        }

        /// <summary>
        /// Convert to assigning authority
        /// </summary>
        public static IdentityDomain ToIdentityDomain(String fhirSystem)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping assigning authority");

            var oidRegistrar = ApplicationServiceContext.Current.GetService<IIdentityDomainRepositoryService>();
            var oid = oidRegistrar.Get(new Uri(fhirSystem));

            if (oid == null)
            {
                throw new FhirException(System.Net.HttpStatusCode.BadRequest, IssueType.NotFound, $"Could not find identity domain {fhirSystem}");
            }

            return oid;
        }

        /// <summary>
        /// Converts a <see cref="ReferenceTerm"/> instance to a <see cref="Coding"/> instance.
        /// </summary>
        /// <param name="referenceTerm">The reference term.</param>
        /// <returns>Returns a FHIR coding instance.</returns>
        public static Coding ToCoding(ReferenceTerm referenceTerm)
        {
            if (referenceTerm == null)
            {
                return null;
            }

            var cs = referenceTerm.LoadProperty(o => o.CodeSystem);
            return new Coding(cs.Url ?? $"urn:oid:{cs.Oid}", referenceTerm.Mnemonic)
            {
                Display = referenceTerm.GetDisplayName()
            };
        }

        /// <summary>
        /// Act Extension to Fhir Extension
        /// </summary>
        public static Extension ToExtension(IModelExtension ext)
        {
            var extensionTypeService = ApplicationServiceContext.Current.GetService<IExtensionTypeRepository>();
            var eType = extensionTypeService.Get(ext.ExtensionTypeKey);

            var retVal = new Extension()
            {
                Url = eType.Uri
            };

            if (ext.Value is decimal || eType.ExtensionHandler == typeof(DecimalExtensionHandler))
            {
                retVal.Value = new FhirDecimal((decimal)(ext.Value ?? new DecimalExtensionHandler().DeSerialize(ext.Data)));
            }
            else if (ext.Value is String || eType.ExtensionHandler == typeof(StringExtensionHandler))
            {
                retVal.Value = new FhirString((string)(ext.Value ?? new StringExtensionHandler().DeSerialize(ext.Data)));
            }
            else if (ext.Value is bool || eType.ExtensionHandler == typeof(BooleanExtensionHandler))
            {
                retVal.Value = new FhirBoolean((bool)(ext.Value ?? new BooleanExtensionHandler().DeSerialize(ext.Data)));
            }
            else if (ext.Value is DateTime || ext.Value is DateTimeOffset || eType.ExtensionHandler == typeof(DateExtensionHandler))
            {
                retVal.Value = new FhirDateTime((DateTime)(ext.Value ?? new DateExtensionHandler().DeSerialize(ext.Data)));
            }
            else if (ext.Value is Concept concept)
            {
                retVal.Value = ToFhirCodeableConcept(concept.Key);
            }
            else if (ext.Value is IdentifiedData idd)
            {
                retVal.Value = DataTypeConverter.CreateRimReference(idd);
            }
            else
            {
                retVal.Value = new Base64Binary(ext.Data);
            }

            return retVal;
        }

        /// <summary>
        /// Gets the concept via the codeable concept
        /// </summary>
        /// <param name="codeableConcept">The codeable concept.</param>
        /// <returns>Returns a concept.</returns>
        public static Concept ToConcept(CodeableConcept codeableConcept)
        {
            // if there is no concept to map
            // we want to exit
            if (codeableConcept == null)
            {
                return null;
            }

            traceSource.TraceEvent(EventLevel.Verbose, "Mapping codeable concept");

            var retVal = codeableConcept?.Coding.Select(o => DataTypeConverter.ToConcept(o)).FirstOrDefault(o => o != null);

            if (retVal == null)
            {
                throw new ConstraintException($"Can't find any reference term mappings from '{codeableConcept.Coding?.FirstOrDefault()?.Code}' in {codeableConcept.Coding?.FirstOrDefault()?.System} to a Concept");
            }

            return retVal;
        }

        /// <summary>
        /// Convert from FHIR coding to concept
        /// </summary>
        /// <param name="coding">The coding.</param>
        /// <param name="defaultSystem">The default system.</param>
        /// <returns>Returns a concept which matches the given code and code system or null if no concept is found.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// Unable to locate service
        /// or
        /// Coding must have system attached
        /// </exception>
        public static Concept ToConcept(Coding coding, string defaultSystem = null)
        {
            return coding == null ? null : ToConcept(coding.Code, coding.System ?? defaultSystem);
        }

        /// <summary>
        /// Convert to concept
        /// </summary>
        public static Concept ToConcept(string code, string system)
        {
            var conceptService = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();

            if (String.IsNullOrEmpty(system))
            {
                throw new ArgumentException("Coding must have system attached");
            }

            Concept retVal = null;
            if (FhirConstants.SanteDBConceptSystem.Equals(system))
            {
                retVal = conceptService.GetConcept(code);
            }
            else
            {
                retVal = conceptService.GetConceptByReferenceTerm(code, system);
            }

            if (retVal == null)
            {
                throw new FhirException((System.Net.HttpStatusCode)422, IssueType.CodeInvalid, $"Could not map concept {system}#{code} to a concept");
            }

            return retVal;
        }

        /// <summary>
        /// Converts a fhir code instance to a <see cref="Concept"/> instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="code">The code.</param>
        /// <param name="system">The system.</param>
        /// <returns>Concept.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// code - Value cannot be null
        /// or
        /// system - Value cannot be null
        /// </exception>
        /// <exception cref="System.InvalidOperationException">Unable to locate service</exception>
        public static Concept ToConcept<T>(string code, string system)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code), "Value cannot be null");
            }

            if (system == null)
            {
                throw new ArgumentNullException(nameof(system), "Value cannot be null");
            }

            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR code");

            var retVal = ToConcept(new Coding(system, code));

            if (retVal == null)
            {
                throw new FhirException((System.Net.HttpStatusCode)422, IssueType.CodeInvalid, $"Could not find concept with reference term '{code}' in {system}");
            }

            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="FhirDateTime"/> instance to a <see cref="DateTimeOffset"/> instance.
        /// </summary>
        /// <param name="dateTimeOffset">The instance to convert.</param>
        /// <returns>Returns the converted instance.</returns>
        public static DateTimeOffset? ToDateTimeOffset(string dateTimeOffset)
        {
            return ToDateTimeOffset(dateTimeOffset, out _);
        }

        /// <summary>
        /// Converts a <see cref="FhirDateTime"/> instance to a <see cref="DateTimeOffset"/> instance.
        /// </summary>
        /// <param name="dateTimeOffset">The instance to convert.</param>
        /// <param name="datePrecision">The date precision as determined by the datetime offset format.</param>
        /// <returns>Returns the converted instance.</returns>
        public static DateTimeOffset? ToDateTimeOffset(string dateTimeOffset, out DatePrecision? datePrecision)
        {
            datePrecision = null;

            if (string.IsNullOrEmpty(dateTimeOffset) || string.IsNullOrWhiteSpace(dateTimeOffset))
            {
                return null;
            }

            DateTimeOffset? result = null;

            switch (dateTimeOffset.Length)
            {
                case 4:
                    {
                        if (DateTimeOffset.TryParseExact(dateTimeOffset, "yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                        {
                            result = value;
                            datePrecision = DatePrecision.Year;
                        }
                    }
                    break;
                case 7:
                    {
                        if (DateTimeOffset.TryParseExact(dateTimeOffset, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                        {
                            result = value;
                            datePrecision = DatePrecision.Month;
                        }
                    }
                    break;
                case 10:
                    {
                        if (DateTimeOffset.TryParseExact(dateTimeOffset, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                        {
                            result = value;
                            datePrecision = DatePrecision.Day;
                        }
                    }
                    break;
                default:
                    {
                        if (DateTimeOffset.TryParse(dateTimeOffset, out var value))
                        {
                            result = value;
                            datePrecision = DatePrecision.Full;
                        }
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// Converts a <see cref="FhirDateTime"/> instance to a <see cref="DateTimeOffset"/> instance.
        /// </summary>
        /// <param name="dateTimeOffset">The instance to convert.</param>
        /// <returns>Returns the converted instance.</returns>
        public static DateTimeOffset? ToDateTimeOffset(FhirDateTime dateTimeOffset)
        {
            return ToDateTimeOffset(dateTimeOffset?.Value);
        }

        /// <summary>
        /// Converts an <see cref="Address"/> instance to an <see cref="EntityAddress"/> instance.
        /// </summary>
        /// <param name="fhirAddress">The FHIR address.</param>
        /// <returns>Returns an entity address instance.</returns>
        public static EntityAddress ToEntityAddress(Address fhirAddress)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR address");

            var mnemonic = "home";
            if (fhirAddress.Use.HasValue)
            {
                mnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirAddress.Use);
            }

            var address = new EntityAddress
            {
                AddressUseKey = ToConcept(mnemonic, "http://hl7.org/fhir/address-use")?.Key,
                Component = new List<EntityAddressComponent>(),
                ExternalKey = m_configuration?.PersistElementId == true ? fhirAddress.ElementId : null
            };

            if (!string.IsNullOrEmpty(fhirAddress.City))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.City, fhirAddress.City));
            }

            if (!string.IsNullOrEmpty(fhirAddress.Country))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.Country, fhirAddress.Country));
            }

            if (fhirAddress.Line?.Any() == true)
            {
                address.Component.AddRange(fhirAddress.Line.Select(a => new EntityAddressComponent(AddressComponentKeys.StreetAddressLine, a)));
            }

            if (!string.IsNullOrEmpty(fhirAddress.State))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.State, fhirAddress.State));
            }

            if (!string.IsNullOrEmpty(fhirAddress.PostalCode))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.PostalCode, fhirAddress.PostalCode));
            }

            if (!string.IsNullOrEmpty(fhirAddress.District))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.County, fhirAddress.District));
            }

            // HACK: Apply extension to address
            fhirAddress.Extension.ForEach(p => p.TryApplyExtension(address));
            return address;
        }

        /// <summary>
        /// Convert a FHIR id
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fhirId"></param>
        /// <returns></returns>
        public static T ToIdentifier<T>(Identifier fhirId) where T : IdentifiedData, IExternalIdentifier, new()
        {

            if (fhirId == null)
            {
                return default(T);
            }

            T retVal = new T();
            if (retVal is IHasExternalKey id)
            {
                id.ExternalKey = m_configuration?.PersistElementId == true ? fhirId.ElementId : null;
            }

            if (fhirId.System != null)
            {
                retVal.IdentityDomain = DataTypeConverter.ToIdentityDomain(fhirId.System);
                retVal.IdentityDomainKey = retVal.IdentityDomain.Key;
            }
            else
            {
                throw new ArgumentException("Identifier must carry a coding system");
            }

            if (!String.IsNullOrEmpty(fhirId.Value))
            {
                retVal.Value = fhirId.Value;
            }
            else
            {
                throw new ArgumentException("Identifier must carry a value");
            }

            if (fhirId.Period != null)
            {
#pragma warning disable CS0618
                if (fhirId.Period.StartElement != null)
                {
                    retVal.IssueDate = fhirId.Period.StartElement.ToDateTimeOffset(TimeSpan.Zero).ToLocalTime();
                }

                if (fhirId.Period.EndElement != null)
                {
                    retVal.ExpiryDate = fhirId.Period.EndElement.ToDateTimeOffset(TimeSpan.Zero).ToLocalTime();
                }
#pragma warning restore CS0618

            }

            switch (fhirId.Use.GetValueOrDefault())
            {
                case Identifier.IdentifierUse.Secondary:
                    retVal.Reliability = IdentifierReliability.Informative;
                    break;
                case Identifier.IdentifierUse.Official:
                    retVal.Reliability = IdentifierReliability.Authoritative;
                    break;
            }

            // Identifier type 
            if (fhirId.Type != null)
            {
                var identifierTypeResolution = ToConcept(fhirId.Type);
                if (identifierTypeResolution == null)
                {
                    throw new KeyNotFoundException($"Cannot find identifier tyoe {fhirId.Type}");
                }
                retVal.IdentifierType = identifierTypeResolution;
                retVal.IdentifierTypeKey = identifierTypeResolution.Key;
            }

            // HACK: Apply extension to address
            fhirId.Extension.ForEach(p => p.TryApplyExtension(retVal));

            // TODO: Fill in use
            return retVal;

        }

        /// <summary>
        /// Convert a FhirIdentifier to an identifier
        /// </summary>
        /// <param name="fhirId">The fhir identifier.</param>
        /// <returns>Returns an entity identifier instance.</returns>
        public static EntityIdentifier ToEntityIdentifier(Identifier fhirId) => ToIdentifier<EntityIdentifier>(fhirId);

        /// <summary>
        /// Converts a <see cref="HumanName" /> instance to an <see cref="EntityName" /> instance.
        /// </summary>
        /// <param name="fhirHumanName">The name of the human.</param>
        /// <returns>Returns an entity name instance.</returns>
        /// <exception cref="System.InvalidOperationException">Unable to locate service</exception>
        public static EntityName ToEntityName(HumanName fhirHumanName)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR human name");

            var mnemonic = "official";

            if (fhirHumanName.Use.HasValue)
            {
                mnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirHumanName.Use);
            }

            var name = new EntityName
            {
                NameUseKey = ToConcept(mnemonic, "http://hl7.org/fhir/name-use")?.Key,
                Component = new List<EntityNameComponent>(),
                ExternalKey = m_configuration?.PersistElementId == true ? fhirHumanName.ElementId : null
            };

            if (fhirHumanName.Family != null)
            {
                name.Component.Add(new EntityNameComponent(NameComponentKeys.Family, fhirHumanName.Family));
            }

            name.Component.AddRange(fhirHumanName.Given.Select(g => new EntityNameComponent(NameComponentKeys.Given, g)));
            name.Component.AddRange(fhirHumanName.Prefix.Select(p => new EntityNameComponent(NameComponentKeys.Prefix, p)));
            name.Component.AddRange(fhirHumanName.Suffix.Select(s => new EntityNameComponent(NameComponentKeys.Suffix, s)));

            fhirHumanName.Extension.ForEach(e => e.TryApplyExtension(name));
            return name;
        }

        /// <summary>
        /// Try to convert resource reference
        /// </summary>
        public static bool TryResolveResourceReference(ResourceReference resourceRef, Resource containedWithin, out IdentifiedData data)
        {
            if (String.IsNullOrEmpty(resourceRef.Type) && Uri.TryCreate(resourceRef.Reference, UriKind.RelativeOrAbsolute, out var uri))
            {
                if (uri.IsAbsoluteUri)
                {
                    throw new InvalidOperationException("Only relative references are supported in SanteDB");
                }
                else
                {
                    var urlParts = resourceRef.Reference.Split('/');
                    if (urlParts.Length > 1)
                    {
                        resourceRef.Type = urlParts[urlParts.Length - 2];
                    }
                    else
                    {
                        throw new InvalidOperationException("References must be in form {resource}/UUID or must have a type");
                    }
                }
            }
            var resourceMapper = FhirResourceHandlerUtil.GetMappersFor(resourceRef.Type).FirstOrDefault();
            if (resourceMapper == null)
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.TYPE_NOT_FOUND, resourceRef.Type));
            }

            var resolveMethod = typeof(DataTypeConverter).GetGenericMethod(nameof(ResolveEntity), new Type[] { resourceMapper.CanonicalType }, new Type[] { typeof(ResourceReference), typeof(Resource) });
            if (resolveMethod == null)
            {
                data = null;
                return false;
            }
            else
            {
                data = resolveMethod.Invoke(null, new object[] { resourceRef, containedWithin }) as IdentifiedData;
                return data != null;
            }
        }


        /// <summary>
        /// Attempts to resolve a resource from the same bundle as the reference
        /// </summary>
        /// <param name="resourceRef">The resource reference</param>
        /// <param name="containedWithin">The resource which is being processed</param>
        /// <param name="resolvedResource">The resolved resource</param>
        /// <returns>True if the bundle contains the resolved resource</returns>
        public static bool TryResolveResourceFromContained(ResourceReference resourceRef, Resource containedWithin, out Resource resolvedResource)
        {

            // First is there a bundle in the contained within
            var fhirBundle = containedWithin?.Annotations(typeof(Bundle)).FirstOrDefault() as Bundle;
            if(fhirBundle != null)
            {
                if (resourceRef.Reference.StartsWith("#") && containedWithin is DomainResource domainResource) // Rel
                {
                    var contained = domainResource.Contained.Find(o => o.Id.Equals(resourceRef.Reference.Substring(1)));
                    if (contained == null)
                    {
                        throw new ArgumentException($"Relative reference provided but cannot find contained object {resourceRef.Reference}");
                    }
                    resolvedResource = contained;
                    return true;
                }
                else
                {
                    resolvedResource = fhirBundle?.Entry.FirstOrDefault(o => o.FullUrl == resourceRef.Reference || $"{o.Resource.TypeName}/{o.Resource.Id}" == resourceRef.Reference)?.Resource;
                    return resolvedResource != null;
                }
            }
            resolvedResource = null;
            return false;
        }

        /// <summary>
        /// Resolve the specified entity
        /// </summary>
        public static TEntity ResolveEntity<TEntity>(ResourceReference resourceRef, Resource containedWithin) where TEntity : BaseEntityData, ITaggable, IHasIdentifiers, new()
        {
            var repo = ApplicationServiceContext.Current.GetService<IRepositoryService<TEntity>>();

            // First is there a bundle in the contained within
            var sdbBundle = containedWithin?.Annotations(typeof(Core.Model.Collection.Bundle)).FirstOrDefault() as Core.Model.Collection.Bundle;
            var fhirBundle = containedWithin?.Annotations(typeof(Bundle)).FirstOrDefault() as Bundle;

            TEntity retVal = null;

            if (resourceRef.Identifier != null)
            {
                // Already exists in SDB bundle?
                var identifier = DataTypeConverter.ToEntityIdentifier(resourceRef.Identifier);
                retVal = sdbBundle?.Item.OfType<TEntity>().Where(e => e.Identifiers.Any(i => i.IdentityDomain.Key == identifier.IdentityDomainKey && i.Value == identifier.Value)).FirstOrDefault();
                if (retVal == null) // Not been processed in bundle
                {
                    retVal = repo.Find(o => o.Identifiers.Any(a => a.IdentityDomain.Key == identifier.IdentityDomainKey && a.Value == identifier.Value)).SingleOrDefault();
                    if (retVal == null)
                    {
                        throw new FhirException(System.Net.HttpStatusCode.NotFound, IssueType.NotFound, $"Could not locate {typeof(TEntity).Name} with identifier {identifier.Value} in domain {identifier.IdentityDomain.Url ?? identifier.IdentityDomain.Oid}");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(resourceRef.Reference))
            {
                if (resourceRef.Reference.StartsWith("#") && containedWithin is DomainResource domainResource) // Rel
                {
                    var contained = domainResource.Contained.Find(o => o.Id.Equals(resourceRef.Reference.Substring(1)));
                    if (contained == null)
                    {
                        throw new ArgumentException($"Relative reference provided but cannot find contained object {resourceRef.Reference}");
                    }

                    var mapper = FhirResourceHandlerUtil.GetMapperForInstance(contained);
                    if (mapper == null)
                    {
                        throw new ArgumentException($"Don't understand how to convert {contained.TypeName}");
                    }

                    retVal = (TEntity)mapper.MapToModel(contained);
                }
                else
                {
                    retVal = sdbBundle?.Item.OfType<TEntity>().FirstOrDefault(e => e.GetTag(FhirConstants.OriginalUrlTag) == resourceRef.Reference || e.GetTag(FhirConstants.OriginalIdTag) == resourceRef.Reference);

                    if (retVal == null) // attempt to resolve via fhir bundle
                    {
                        // HACK: the .FindEntry might not work since the fullUrl may be relative - we should be permissive on a reference resolution to allow for relative links
                        //var fhirResource = fhirBundle.FindEntry(resourceRef);
                        var fhirResource = fhirBundle?.Entry.Where(o => o.FullUrl == resourceRef.Reference || $"{o.Resource.TypeName}/{o.Resource.Id}" == resourceRef.Reference);
                        if (fhirResource?.Any() == true)
                        {
                            // TODO: Error trapping
                            retVal = (TEntity)FhirResourceHandlerUtil.GetMapperForInstance(fhirResource.FirstOrDefault().Resource).MapToModel(fhirResource.FirstOrDefault().Resource);
                            sdbBundle.Item.Add(retVal);
                        }
                    }

                    if (retVal == null)
                    {
                        // HACK: We don't care about the absoluteness of a URL
                        // Attempt to resolve the reference
                        var match = m_referenceRegex.Match(resourceRef.Reference);
                        if (!match.Success)
                        {
                            throw new FhirException(System.Net.HttpStatusCode.NotFound, IssueType.NotFound, $"Could not find {resourceRef.Reference} as a previous entry in this submission. Cannot resolve from database unless reference is either urn:uuid:UUID or Type/UUID");
                        }

                        if (!string.IsNullOrEmpty(match.Groups[2].Value) && Guid.TryParse(match.Groups[3].Value.Replace("urn:uuid:", string.Empty), out Guid relUuid)) // rel reference
                        {
                            retVal = repo.Get(relUuid); // Allow any triggers to fire
                        }
                        // HACK: Need to removed the urn:uuid: at the front of the guid.
                        else if (Guid.TryParse(match.Groups[1].Value.Replace("urn:uuid:", string.Empty), out Guid absRef))
                        {
                            retVal = repo.Get(absRef);
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentException("Could not understand resource reference");
            }

            // TODO: Weak references
            if (retVal == null)
            {
                throw new FhirException((System.Net.HttpStatusCode)422, IssueType.NotSupported, $"Weak references (to other servers) are not currently supported (ref: {resourceRef.Reference})");
            }

            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="Patient.ContactComponent"/> instance to an <see cref="EntityRelationship"/> instance.
        /// </summary>
        /// <param name="patientContact">The patient contact.</param>
        /// <param name="patient">The SanteDB <see cref="Patient"/> to attach to</param>
        /// <returns>Returns the mapped entity relationship instance..</returns>
        public static EntityRelationship ToEntityRelationship(Patient.ContactComponent patientContact, Patient patient)
        {
            var retVal = new EntityRelationship(EntityRelationshipTypeKeys.Contact, new Core.Model.Entities.Person()
            {
                Key = Guid.NewGuid(),
                Addresses = patientContact.Address != null ? new List<EntityAddress>() { ToEntityAddress(patientContact.Address) } : null,
                CreationTime = DateTimeOffset.Now,
                // TODO: Gender (after refactor)
                Names = patientContact.Name != null ? new List<EntityName>() { ToEntityName(patientContact.Name) } : null,
                Telecoms = patientContact.Telecom?.Select(ToEntityTelecomAddress).ToList(),
                Extensions = new List<EntityExtension>(),
                Relationships = new List<EntityRelationship>(),
            })
            {
                ClassificationKey = RelationshipClassKeys.ContainedObjectLink,
                RelationshipRoleKey = DataTypeConverter.ToConcept(patientContact.Relationship.FirstOrDefault())?.Key,
                ExternalKey = m_configuration?.PersistElementId == true ? patientContact.ElementId : null
            };

            retVal.TargetEntity.Extensions.AddRange(patientContact.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, retVal.TargetEntity)).OfType<EntityExtension>());
            if (patientContact.Organization != null)
            {
                var refObjectKey = DataTypeConverter.ResolveEntity<Core.Model.Entities.Organization>(patientContact.Organization, patient);
                if (refObjectKey == null)
                {
                    throw new FhirException(System.Net.HttpStatusCode.NotFound, IssueType.NotFound, $"Could not resolve reference to patientContext.Organization");
                }

                // It is just an organization
                if (patientContact.Name == null &&
                    patientContact.Address == null)
                {
                    retVal.TargetEntity = null;
                    retVal.TargetEntityKey = refObjectKey.Key;
                }
                else // It is a person which is scoped by an orgnaizati
                {
                    retVal.TargetEntity.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Scoper, refObjectKey.Key));
                }
            }

            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="ContactPoint"/> instance to an <see cref="EntityTelecomAddress"/> instance.
        /// </summary>
        /// <param name="fhirTelecom">The telecom.</param>
        /// <returns>Returns an entity telecom address.</returns>
        public static EntityTelecomAddress ToEntityTelecomAddress(ContactPoint fhirTelecom)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR telecom");

            if (!String.IsNullOrEmpty(fhirTelecom.Value))
            {
                var useMnemonic = "temp";
                if (fhirTelecom.Use.HasValue)
                {
                    useMnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirTelecom.Use);
                }

                string typeMnemonic = null;
                if (fhirTelecom.System.HasValue)
                {
                    typeMnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirTelecom.System);
                }

                // If the value is a URI and the scheme is OTHER
                EntityTelecomAddress retVal = null;
                var telecomSchemeMatch = m_telecomUri.Match(fhirTelecom.Value);
                if (telecomSchemeMatch.Success) // Escape the values
                {
                    var conceptService = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();
                    var claimedScheme = ToConcept(typeMnemonic, "http://hl7.org/fhir/contact-point-system")?.Key ?? NullReasonKeys.Other;
                    var scheme = telecomSchemeMatch.Groups[1].Value;
                    var schemeBin = Encoding.UTF8.GetBytes(scheme);

                    var registeredScheme = conceptService.Find(o => o.Extensions.Where(e => e.ExtensionTypeKey == ExtensionTypeKeys.Rfc3986SchemeExtension).Any(e => e.ExtensionValueData == schemeBin)).FirstOrDefault();

                    // We couldn't find the scheme 
                    if (registeredScheme == null)
                    {
                        throw new FhirException((HttpStatusCode)422, IssueType.NotSupported, $"Scheme {scheme} is not supported by this version of SanteDB");
                    }
                    else if (registeredScheme.Key != claimedScheme && m_configuration?.StrictProcessing == true)
                    {
                        throw new FhirException((HttpStatusCode)422, IssueType.CodeInvalid, $"Scheme {scheme} does not match the claimed {fhirTelecom.System}");
                    }

                    retVal = new EntityTelecomAddress()
                    {
                        IETFValue = fhirTelecom.Value,
                        AddressUseKey = ToConcept(useMnemonic, "http://hl7.org/fhir/contact-point-use")?.Key,
                        TypeConceptKey = registeredScheme.Key,
                        ExternalKey = m_configuration?.PersistElementId == true ? fhirTelecom.ElementId : null
                    };
                }
                else
                {
                    retVal = new EntityTelecomAddress
                    {
                        Value = fhirTelecom.Value,
                        AddressUseKey = ToConcept(useMnemonic, "http://hl7.org/fhir/contact-point-use")?.Key,
                        TypeConceptKey = ToConcept(typeMnemonic, "http://hl7.org/fhir/contact-point-system")?.Key,
                        ExternalKey = m_configuration?.PersistElementId == true ? fhirTelecom.ElementId : null
                    };
                }
                fhirTelecom.Extension.ForEach(p => p.TryApplyExtension(retVal));
                return retVal;
            }
            return null;
        }

        /// <summary>
        /// Converts an <see cref="EntityAddress"/> instance to a <see cref="Address"/> instance.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>Returns a FHIR address.</returns>
        public static Address ToFhirAddress(EntityAddress address)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity address");

            if (address == null)
            {
                return null;
            }

            // Return value
            var retVal = new Address()
            {
                Use = DataTypeConverter.ToFhirEnumeration<Address.AddressUse>(address.AddressUseKey, "http://hl7.org/fhir/address-use"),
                Line = new List<String>(),
                ElementId = address.ExternalKey
            };

            // Process components
            foreach (var com in address.LoadCollection<EntityAddressComponent>(nameof(EntityAddress.Component)))
            {
                if (com.ComponentTypeKey == AddressComponentKeys.City)
                {
                    retVal.City = com.Value;
                }
                else if (com.ComponentTypeKey == AddressComponentKeys.Country)
                {
                    retVal.Country = com.Value;
                }
                else if (com.ComponentTypeKey == AddressComponentKeys.AddressLine ||
                    com.ComponentTypeKey == AddressComponentKeys.StreetAddressLine)
                {
                    retVal.LineElement.Add(new FhirString(com.Value));
                }
                else if (com.ComponentTypeKey == AddressComponentKeys.State)
                {
                    retVal.State = com.Value;
                }
                else if (com.ComponentTypeKey == AddressComponentKeys.PostalCode)
                {
                    retVal.PostalCode = com.Value;
                }
                else if (com.ComponentTypeKey == AddressComponentKeys.County)
                {
                    retVal.District = com.Value;
                }
                else
                {
                    retVal.AddExtension(
                        FhirConstants.SanteDBProfile + "#address-" + com.LoadProperty<Concept>(nameof(EntityAddressComponent.ComponentType)).Mnemonic,
                        new FhirString(com.Value)
                    );
                }
            }

            retVal.Extension.AddRange(address.CreateExtensions(ResourceType.Basic, out _));
            return retVal;
        }

        /// <summary>
        /// Converts a reference term to the codeable concept
        /// </summary>
        public static CodeableConcept ToFhirCodeableConcept(Guid? conceptKey, params String[] preferredCodeSystem)
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                traceSource.TraceEvent(EventLevel.Verbose, "Mapping concept");

                if (conceptKey.GetValueOrDefault() == Guid.Empty)
                {
                    return null;
                }

                var codeSystemService = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();

                // No preferred CS then all
                if (!preferredCodeSystem.Any())
                {
                    var refTerms = codeSystemService.FindReferenceTermsByConcept(conceptKey.Value, String.Empty);
                    if (refTerms.Any())
                    {
                        return new CodeableConcept
                        {
                            Coding = refTerms.Where(o => o.RelationshipTypeKey == ConceptRelationshipTypeKeys.SameAs).Select(o => ToCoding(o.LoadProperty(t => t.ReferenceTerm))).ToList(),
                            Text = codeSystemService.GetName(conceptKey.Value, CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
                        };
                    }
                    else
                    {
                        var concept = codeSystemService.Get(conceptKey.Value);
                        return new CodeableConcept(FhirConstants.SanteDBConceptSystem, concept.Mnemonic)
                        {
                            Text = codeSystemService.GetName(conceptKey.Value, CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
                        };
                    }
                }
                else
                {
                    var refTerms = preferredCodeSystem.Select(o => codeSystemService.GetConceptReferenceTerm(conceptKey.Value, o, false)).OfType<ReferenceTerm>().ToArray();
                    if (refTerms.Any())
                    {
                        return new CodeableConcept
                        {
                            Coding = refTerms.Select(o => ToCoding(o)).ToList(),
                            Text = codeSystemService.GetName(conceptKey.Value, CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
                        };
                    }
                    else
                    {
                        return null;
                    }
                }

            }
        }

        /// <summary>
        /// Converts an <see cref="EntityName"/> instance to a <see cref="HumanName"/> instance.
        /// </summary>
        /// <param name="entityName">Name of the entity.</param>
        /// <returns>Returns the mapped FHIR human name.</returns>
        public static HumanName ToFhirHumanName(EntityName entityName)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity name");

            if (entityName == null)
            {
                return null;
            }

            // Return value
            var retVal = new HumanName
            {
                Use = DataTypeConverter.ToFhirEnumeration<HumanName.NameUse>(entityName.NameUseKey, "http://hl7.org/fhir/name-use"),
                ElementId = m_configuration?.PersistElementId == true ? entityName.ExternalKey : null
            };

            // Process components
            foreach (var com in entityName.LoadCollection<EntityNameComponent>(nameof(EntityName.Component)))
            {
                if (string.IsNullOrEmpty(com.Value))
                {
                    continue;
                }

                if (com.ComponentTypeKey == NameComponentKeys.Given)
                {
                    retVal.GivenElement.Add(new FhirString(com.Value));
                }
                else if (com.ComponentTypeKey == NameComponentKeys.Family)
                {
                    retVal.FamilyElement = new FhirString(com.Value);
                }
                else if (com.ComponentTypeKey == NameComponentKeys.Prefix)
                {
                    retVal.PrefixElement.Add(new FhirString(com.Value));
                }
                else if (com.ComponentTypeKey == NameComponentKeys.Suffix)
                {
                    retVal.SuffixElement.Add(new FhirString(com.Value));
                }
            }

            retVal.Extension.AddRange(entityName.CreateExtensions(ResourceType.Basic, out _));

            return retVal;
        }

        /// <summary>
        /// To FHIR enumeration
        /// </summary>
        public static TEnum? ToFhirEnumeration<TEnum>(Guid? conceptKey, string codeSystem, bool throwIfNotFound = false) where TEnum : struct, Enum
        {
            var coding = DataTypeConverter.ToFhirCodeableConcept(conceptKey, codeSystem);
            if (coding != null)
            {
                return Hl7.Fhir.Utility.EnumUtility.ParseLiteral<TEnum>(coding.Coding.First().Code, true);
            }
            else if (throwIfNotFound)
            {
                throw new ConstraintException($"Cannot find FHIR mapping for Concept {conceptKey}");
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a <see cref="IdentifierBase{TBoundModel}" /> instance to an <see cref="Identifier" /> instance.
        /// </summary>
        /// <typeparam name="TBoundModel">The type of the bound model.</typeparam>
        /// <param name="identifier">The identifier.</param>
        /// <returns>Returns the mapped FHIR identifier.</returns>
        public static Identifier ToFhirIdentifier<TBoundModel>(IdentifierBase<TBoundModel> identifier) where TBoundModel : VersionedEntityData<TBoundModel>, new()
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity identifier");

            if (identifier == null)
            {
                return null;
            }

            var imetaService = ApplicationServiceContext.Current.GetService<IIdentityDomainRepositoryService>();
            var authority = imetaService.Get(identifier.IdentityDomainKey.Value);
            var retVal = new Identifier
            {
                System = authority?.Url ?? $"urn:oid:{authority?.Oid}",
                Type = ToFhirCodeableConcept(identifier.IdentifierTypeKey),
                Value = identifier.Value,
                ElementId = m_configuration?.PersistElementId == true ? identifier.ExternalKey : null
            };

            if (identifier.ExpiryDate.HasValue || identifier.IssueDate.HasValue)
            {
                retVal.Period = new Period()
                {
                    StartElement = identifier.IssueDate.HasValue ? new FhirDateTime(identifier.IssueDate.Value) : null,
                    EndElement = identifier.ExpiryDate.HasValue ? new FhirDateTime(identifier.ExpiryDate.Value) : null
                };
            }

            return retVal;
        }

        /// <summary>
        /// Converts an <see cref="EntityTelecomAddress"/> instance to <see cref="ContactPoint"/> instance.
        /// </summary>
        /// <param name="telecomAddress">The telecom address.</param>
        /// <returns>Returns the mapped FHIR telecom.</returns>
        public static ContactPoint ToFhirTelecom(EntityTelecomAddress telecomAddress)
        {
            if (null == telecomAddress)
                return null;

            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity telecom address");

            var system = ToFhirEnumeration<ContactPoint.ContactPointSystem>(telecomAddress.TypeConceptKey, "http://hl7.org/fhir/contact-point-system") ?? ContactPoint.ContactPointSystem.Other;
            var value = system == ContactPoint.ContactPointSystem.Other ? telecomAddress.IETFValue : telecomAddress.Value;
            return new ContactPoint
            {
                System = system,
                Use = ToFhirEnumeration<ContactPoint.ContactPointUse>(telecomAddress.AddressUseKey, "http://hl7.org/fhir/contact-point-use"),
                Value = value,
                ElementId = m_configuration?.PersistElementId == true ? telecomAddress.ExternalKey : null,
                Extension = new List<Extension>(telecomAddress.CreateExtensions(ResourceType.Basic, out _))
            };
        }

        /// <summary>
        /// Add provenance information to the target entity
        /// </summary>
        public static void AddContextProvenanceData(IIdentifiedResource targetEntity)
        {
            object provenanceObject = null;
            if (RestOperationContext.Current?.Data.TryGetValue(FhirConstants.ProvenanceHeaderName, out provenanceObject) != true ||
                !(provenanceObject is Provenance prov))
            {
                return;
            }

            if (prov.Location != null)
            {
                var target = DataTypeConverter.ResolveEntity<Place>(prov.Location, null);
                switch (targetEntity)
                {
                    case Entity ent:
                        ent.LoadProperty(o => o.Relationships).Add(new EntityRelationship(EntityRelationshipTypeKeys.ServiceDeliveryLocation, target));
                        break;
                    case Act act:
                        act.LoadProperty(o => o.Participations).Add(new ActParticipation(ActParticipationKeys.Location, target));
                        break;
                }
            }

            if (prov.Agent != null)
            {
                foreach (var agnt in prov.Agent)
                {
                    if (agnt.Who == null)
                    {
                        throw new ArgumentNullException($"{nameof(prov.Agent)}.{nameof(agnt.Who)}");
                    }
                    var agent = DataTypeConverter.ResolveEntity<Entity>(agnt.Who, null);
                    if (agent == null)
                    {
                        throw new KeyNotFoundException(agnt.Who.Identifier.ToString());
                    }

                    var role = agnt.Role.Select(o => DataTypeConverter.ToConcept(o)).OfType<Concept>().FirstOrDefault();
                    if (role == null)
                    {
                        throw new FhirException(System.Net.HttpStatusCode.BadRequest, IssueType.CodeInvalid, $"{agnt.Role.First().Coding.First().Code} is not registered in SanteDB");
                    }

                    switch (targetEntity)
                    {
                        case Entity ent:
                            ent.LoadProperty(o => o.Relationships).Add(new EntityRelationship(role.Key, agent));
                            break;
                        case Act act:
                            act.LoadProperty(o => o.Participations).Add(new ActParticipation(role.Key, agent));
                            break;
                    }

                }
            }
        }

        /// <summary>
        /// To a FHIR measure type
        /// </summary>
        internal static CodeableConcept ToFhirMeasureType(BiMeasureComputationColumnReference comp)
        {
            string codeValue = String.Empty;
            switch (comp.GetType().Name)
            {
                case nameof(BiMeasureComputationDenominator):
                    codeValue = "denominator";
                    break;
                case nameof(BiMeasureComputationDenominatorExclusion):
                    codeValue = "denominator-exclusion";
                    break;
                case nameof(BiMeasureComputationNumerator):
                    codeValue = "numerator";
                    break;
                case nameof(BiMeasureComputationNumeratorExclusion):
                    codeValue = "numerator-exclusion";
                    break;
                case nameof(BiMeasureComputationScore):
                    codeValue = "measure-observation";
                    break;
                default:
                    throw new InvalidOperationException();
            }
            return new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", codeValue);
        }

        /// <summary>
        /// Convert to a security policy
        /// </summary>
        internal static SecurityPolicyInstance ToSecurityPolicy(Coding securityCoding)
        {
            if (!String.IsNullOrEmpty(securityCoding.System) && !securityCoding.System.Equals(FhirConstants.SecurityPolicySystem))
            {
                throw new ArgumentOutOfRangeException(nameof(securityCoding.System), $"Security policies must be drawn from the SanteDB {FhirConstants.SecurityPolicySystem}");
            }
            var policy = m_pipService.GetPolicy(securityCoding.Code);
            if (policy == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.DEPENDENT_CONFIGURATION_MISSING, securityCoding.Code));
            }

            return new SecurityPolicyInstance(new SecurityPolicy(policy.Name, policy.Oid, true, policy.CanOverride) { Key = policy.Key }, PolicyGrantType.Grant);
        }
    }
}
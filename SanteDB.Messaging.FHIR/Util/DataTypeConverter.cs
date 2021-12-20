/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-5
 */

using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Extensions;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using static Hl7.Fhir.Model.OperationOutcome;
using SanteDB.Core.Model.Audit;

using SanteDB.Core.Model.Security;

using System.Xml;

using System.Security;
using System.Security.Authentication;
using System.Data.Common;
using System.IO;

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

        // CX Devices
        private static readonly Regex m_cxDevice = new Regex(@"^(.*?)\^\^\^([A-Z_0-9]*)(?:&(.*?)&ISO)?");

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
            var pou = audit.AuditableObjects.Find(o => o.ObjectId == SanteDBClaimTypes.PurposeOfUse)?.NameData;
            if (pou != null && Guid.TryParse(pou, out Guid pouKey))
            {
                retVal.PurposeOfEvent = new List<CodeableConcept>() { ToFhirCodeableConcept(conceptService.Get(pouKey)) };
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
                    Role = act.ActorRoleCode.Skip(1).Select(o => ToFhirCodeableConcept(conceptService.GetConceptByReferenceTerm(o.Code, o.CodeSystem))).ToList(),
                    Type = act.ActorRoleCode.Take(1).Select(o => ToFhirCodeableConcept(conceptService.GetConceptByReferenceTerm(o.Code, o.CodeSystem))).FirstOrDefault(),
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

        /// <summary>
        /// Creates a FHIR reference.
        /// </summary>
        /// <typeparam name="TResource">The type of the t resource.</typeparam>
        /// <param name="targetEntity">The target entity.</param>
        /// <returns>Returns a reference instance.</returns>
        public static ResourceReference CreateVersionedReference<TResource>(IVersionedEntity targetEntity)
            where TResource : DomainResource, new()
        {
            if (targetEntity == null)
                throw new ArgumentNullException(nameof(targetEntity));

            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);

            var refer = new ResourceReference($"{fhirType}/{targetEntity.Key}/_history/{targetEntity.VersionKey}");

            // Add an identifier to the object
            if (targetEntity is IHasIdentifiers ident)
            {
                var uqIdentifier = ident.LoadCollection(x => x.Identifiers).FirstOrDefault(i => i.Authority.IsUnique);
                if (uqIdentifier != null)
                {
                    refer.Identifier = new Identifier(uqIdentifier.Authority.Url, uqIdentifier.Value);
                }
            }

            refer.Display = targetEntity.ToString();
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
                throw new ArgumentNullException(nameof(targetEntity));

            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);

            var refer = new ResourceReference($"{fhirType}/{targetEntity.Key}");

            // Add an identifier to the object
            if (targetEntity is IHasIdentifiers ident)
            {
                var uqIdentifier = ident.LoadCollection(x => x.Identifiers).FirstOrDefault(i => i.Authority.IsUnique);
                if (uqIdentifier != null)
                {
                    refer.Identifier = new Identifier(uqIdentifier.Authority.Url, uqIdentifier.Value);
                }
            }

            refer.Display = targetEntity.ToDisplay();
            return refer;
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
                throw new ArgumentNullException(nameof(targetEntity));

            var fhirType = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TResource).Name);
            var refer = new ResourceReference($"#{targetEntity.Key}");
            refer.Display = targetEntity.ToDisplay();
            return refer;
        }

        /// <summary>
        /// To quantity
        /// </summary>
        public static Quantity ToQuantity(decimal? quantity, Concept unitConcept)
        {
            return new Quantity()
            {
                Value = quantity,
                Unit = DataTypeConverter.ToFhirCodeableConcept(unitConcept, "http://hl7.org/fhir/sid/ucum")?.GetCoding().Code
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
        /// Creates the resource.
        /// </summary>
        /// <typeparam name="TResource">The type of the t resource.</typeparam>
        /// <param name="resource">The resource.</param>
        /// <returns>TResource.</returns>
        public static TResource CreateResource<TResource>(IVersionedEntity resource) where TResource : Resource, new()
        {
            var retVal = CreateResource<TResource>((IIdentifiedEntity)resource);
            retVal.VersionId = resource.VersionKey.ToString();
            retVal.Meta.VersionId = resource.VersionKey?.ToString();
            return retVal;
        }

        /// <summary>
        /// Create non versioned resource
        /// </summary>
        public static TResource CreateResource<TResource>(IIdentifiedEntity resource) where TResource : Resource, new()
        {
            var retVal = new TResource();

            // Add annotations
            retVal.Id = resource.Key.ToString();

            // metadata
            retVal.Meta = new Meta()
            {
                LastUpdated = (resource as IdentifiedData).ModifiedOn.DateTime,
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
            if (resource is ISecurable securable)
            {
                retVal.Meta.Security = securable?.Policies?.Where(o => o.GrantType == Core.Model.Security.PolicyGrantType.Grant).Select(o => new Coding("http://santedb.org/security/policy", o.Policy.Oid)).ToList();
                retVal.Meta.Security.Add(new Coding("http://santedb.org/security/policy", PermissionPolicyIdentifiers.ReadClinicalData));
            }

            if (retVal is Hl7.Fhir.Model.IExtendable fhirExtendable && resource is Core.Model.Interfaces.IExtendable extendableObject)
            {
                DataTypeConverter.AddExtensions(extendableObject, fhirExtendable);
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
            // TODO: Do we want to expose all internal extensions as external ones? Or do we just want to rely on the IFhirExtensionHandler?
            fhirExtension.Extension = extendable?.LoadCollection(o => o.Extensions).Where(o => o.ExtensionTypeKey != ExtensionTypeKeys.JpegPhotoExtension).Select(DataTypeConverter.ToExtension).ToList();

            if (resource != null && resource.TryDeriveResourceType(out ResourceType rt))
            {
                fhirExtension.Extension = ExtensionUtil.CreateExtensions(extendable as IIdentifiedEntity, rt, out IEnumerable<IFhirExtensionHandler> appliedExtensions).Union(fhirExtension.Extension).ToList();
                return appliedExtensions.Select(o => o.ProfileUri?.ToString()).Distinct();
            }
            else
                return new List<String>();
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
        public static ActExtension ToActExtension(Extension fhirExtension)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR extension");

            var extension = new ActExtension();

            if (fhirExtension == null)
            {
                throw new ArgumentNullException(nameof(fhirExtension), "Value cannot be null");
            }

            var extensionTypeService = ApplicationServiceContext.Current.GetService<IExtensionTypeRepository>();

            extension.ExtensionType = extensionTypeService.Get(new Uri(fhirExtension.Url));
            //extension.ExtensionValue = fhirExtension.Value;
            if (extension.ExtensionType.ExtensionHandler == typeof(DecimalExtensionHandler))
                extension.ExtensionValue = (fhirExtension.Value as FhirDecimal).Value;
            else if (extension.ExtensionType.ExtensionHandler == typeof(StringExtensionHandler))
                extension.ExtensionValue = (fhirExtension.Value as FhirString).Value;
            else if (extension.ExtensionType.ExtensionHandler == typeof(DateExtensionHandler))
                extension.ExtensionValue = (fhirExtension.Value as FhirDateTime).Value;
            // TODO: Implement binary incoming extensions
            //else if(extension.ExtensionType.ExtensionHandler == typeof(BinaryExtensionHandler))
            //    extension.ExtensionValueXml = (fhirExtension.Value as FhirBase64Binary).Value;
            else
                throw new NotImplementedException($"Extension type is not understood");
            // Now will
            return extension;
        }

        /// <summary>
        /// Converts an <see cref="Extension"/> instance to an <see cref="ActExtension"/> instance.
        /// </summary>
        /// <param name="fhirExtension">The FHIR extension.</param>
        /// <returns>Returns the converted act extension instance.</returns>
        /// <exception cref="System.ArgumentNullException">fhirExtension - Value cannot be null</exception>
        public static EntityExtension ToEntityExtension(Extension fhirExtension, IdentifiedData context)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR extension");

            var extension = new EntityExtension();

            if (fhirExtension == null)
            {
                throw new ArgumentNullException(nameof(fhirExtension), "Value cannot be null");
            }

            // First attempt to parse the extension using a parser
            if (!fhirExtension.TryApplyExtension(context))
            {
                var extensionTypeService = ApplicationServiceContext.Current.GetService<IExtensionTypeRepository>();

                extension.ExtensionType = extensionTypeService.Get(new Uri(fhirExtension.Url));
                if (extension.ExtensionType == null)
                    return null;

                //extension.ExtensionValue = fhirExtension.Value;
                if (extension.ExtensionType.ExtensionHandler == typeof(DecimalExtensionHandler))
                    extension.ExtensionValue = (fhirExtension.Value as FhirDecimal).Value;
                else if (extension.ExtensionType.ExtensionHandler == typeof(StringExtensionHandler))
                    extension.ExtensionValue = (fhirExtension.Value as FhirString).Value;
                else if (extension.ExtensionType.ExtensionHandler == typeof(DateExtensionHandler))
                    extension.ExtensionValue = ToDateTimeOffset(fhirExtension.Value as FhirDateTime);
                // TODO: Implement binary incoming extensions
                else if (extension.ExtensionType.ExtensionHandler == typeof(BinaryExtensionHandler) ||
                    extension.ExtensionType.ExtensionHandler == typeof(DictionaryExtensionHandler))
                    extension.ExtensionValueXml = (fhirExtension.Value as Base64Binary).Value;
                else
                    throw new NotImplementedException($"Extension type is not understood");

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
            return new PersonLanguageCommunication(lang.Language.GetCoding().Code, lang.Preferred.GetValueOrDefault());
        }

        /// <summary>
        /// Convert to language of communication
        /// </summary>
        public static PersonLanguageCommunication ToLanguageCommunication(RelatedPerson.CommunicationComponent lang)
        {
            return new PersonLanguageCommunication(lang.Language.GetCoding().Code, lang.Preferred.GetValueOrDefault());
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
                
            return new PersonLanguageCommunication(lang.Coding.First().Code, preferred);
        }

        /// <summary>
        /// Convert to language of communication
        /// </summary>
        public static Patient.CommunicationComponent ToFhirCommunicationComponent(PersonLanguageCommunication lang)
        {
            return new Patient.CommunicationComponent
            {
                Language = new CodeableConcept("urn:ietf:bcp:47", lang.LanguageCode),
                Preferred = lang.IsPreferred
            };
        }

        /// <summary>
        /// Converts a <see cref="FhirIdentifier"/> instance to an <see cref="ActIdentifier"/> instance.
        /// </summary>
        /// <param name="fhirIdentifier">The FHIR identifier.</param>
        /// <returns>Returns the converted act identifier instance.</returns>
        public static ActIdentifier ToActIdentifier(Identifier fhirIdentifier)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR identifier");

            if (fhirIdentifier == null)
            {
                return null;
            }

            ActIdentifier retVal;

            if (fhirIdentifier.System != null)
            {
                retVal = new ActIdentifier(ToAssigningAuthority(fhirIdentifier.System), fhirIdentifier.Value);
            }
            else
            {
                throw new ArgumentException("Identifier must carry a coding system");
            }

            // TODO: Fill in use
            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="FhirUri"/> instance to an <see cref="AssigningAuthority"/> instance.
        /// </summary>
        /// <param name="fhirSystem">The FHIR system.</param>
        /// <returns>Returns the converted instance.</returns>
        public static AssigningAuthority ToAssigningAuthority(FhirUri fhirSystem)
        {
            return fhirSystem == null ? null : ToAssigningAuthority(fhirSystem.Value);
        }

        /// <summary>
        /// Convert to assigning authority
        /// </summary>
        public static AssigningAuthority ToAssigningAuthority(String fhirSystem)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping assigning authority");

            var oidRegistrar = ApplicationServiceContext.Current.GetService<IAssigningAuthorityRepositoryService>();
            var oid = oidRegistrar.Get(new Uri(fhirSystem));

            if (oid == null)
                throw new FhirException(System.Net.HttpStatusCode.BadRequest, IssueType.NotFound, $"Could not find identity domain {fhirSystem}");
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

            traceSource.TraceEvent(EventLevel.Verbose, "Mapping reference term");

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
                Url = eType.Name
            };

            if (ext.Value is decimal || eType.ExtensionHandler == typeof(DecimalExtensionHandler))
                retVal.Value = new FhirDecimal((decimal)(ext.Value ?? new DecimalExtensionHandler().DeSerialize(ext.Data)));
            else if (ext.Value is String || eType.ExtensionHandler == typeof(StringExtensionHandler))
                retVal.Value = new FhirString((string)(ext.Value ?? new StringExtensionHandler().DeSerialize(ext.Data)));
            else if (ext.Value is bool || eType.ExtensionHandler == typeof(BooleanExtensionHandler))
                retVal.Value = new FhirBoolean((bool)(ext.Value ?? new BooleanExtensionHandler().DeSerialize(ext.Data)));
            else if (ext.Value is Concept concept)
                retVal.Value = ToFhirCodeableConcept(concept);
            else if (ext.Value is SanteDB.Core.Model.Roles.Patient patient)
                retVal.Value = DataTypeConverter.CreateVersionedReference<Patient>(patient);
            else
                retVal.Value = new Base64Binary(ext.Data);
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

            if (system == null)
            {
                throw new ArgumentException("Coding must have system attached");
            }

            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR coding");

            // Lookup
            var retVal = conceptService.GetConceptByReferenceTerm(code, system);
            if (retVal == null)
                throw new FhirException((System.Net.HttpStatusCode)422, IssueType.CodeInvalid, $"Could not map concept {system}#{code} to a concept");
            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="FhirCode{T}"/> instance to a <see cref="Concept"/> instance.
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
                throw new FhirException((System.Net.HttpStatusCode)422, IssueType.CodeInvalid, $"Could not find concept with reference term '{code}' in {system}");

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

            switch(dateTimeOffset.Length)
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
                mnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirAddress.Use);

            var address = new EntityAddress
            {
                AddressUseKey = ToConcept(mnemonic, "http://hl7.org/fhir/address-use")?.Key
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
                address.Component.AddRange(fhirAddress.Line.Select(a => new EntityAddressComponent(AddressComponentKeys.AddressLine, a)));
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

            return address;
        }

        /// <summary>
        /// Convert a FhirIdentifier to an identifier
        /// </summary>
        /// <param name="fhirId">The fhir identifier.</param>
        /// <returns>Returns an entity identifier instance.</returns>
        public static EntityIdentifier ToEntityIdentifier(Identifier fhirId)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR identifier");

            if (fhirId == null)
            {
                return null;
            }

            EntityIdentifier retVal;

            if (fhirId.System != null)
            {
                retVal = new EntityIdentifier(DataTypeConverter.ToAssigningAuthority(fhirId.System), fhirId.Value);
            }
            else
            {
                throw new ArgumentException("Identifier must carry a coding system");
            }

            if (fhirId.Period != null)
            {
                if (fhirId.Period.StartElement != null)
                    retVal.IssueDate = fhirId.Period.StartElement.ToDateTimeOffset().DateTime;
                if (fhirId.Period.EndElement != null)
                    retVal.ExpiryDate = fhirId.Period.EndElement.ToDateTimeOffset().DateTime;
            }

            // TODO: Fill in use
            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="FhirHumanName" /> instance to an <see cref="EntityName" /> instance.
        /// </summary>
        /// <param name="fhirHumanName">The name of the human.</param>
        /// <returns>Returns an entity name instance.</returns>
        /// <exception cref="System.InvalidOperationException">Unable to locate service</exception>
        public static EntityName ToEntityName(HumanName fhirHumanName)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping FHIR human name");

            var mnemonic = "official";

            if (fhirHumanName.Use.HasValue)
                mnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirHumanName.Use);

            var name = new EntityName
            {
                NameUseKey = ToConcept(mnemonic, "http://hl7.org/fhir/name-use")?.Key
            };

            if (fhirHumanName.Family != null)
            {
                name.Component.Add(new EntityNameComponent(NameComponentKeys.Family, fhirHumanName.Family));
            }

            name.Component.AddRange(fhirHumanName.Given.Select(g => new EntityNameComponent(NameComponentKeys.Given, g)));
            name.Component.AddRange(fhirHumanName.Prefix.Select(p => new EntityNameComponent(NameComponentKeys.Prefix, p)));
            name.Component.AddRange(fhirHumanName.Suffix.Select(s => new EntityNameComponent(NameComponentKeys.Suffix, s)));

            return name;
        }

        /// <summary>
        /// Resolve the specified entity
        /// </summary>
        public static TEntity ResolveEntity<TEntity>(ResourceReference resourceRef, Resource containedWithin) where TEntity : Entity, new()
        {
            var repo = ApplicationServiceContext.Current.GetService<IRepositoryService<TEntity>>();

            // First is there a bundle in the contained within
            var sdbBundle = containedWithin.Annotations(typeof(Core.Model.Collection.Bundle)).FirstOrDefault() as Core.Model.Collection.Bundle;
            var fhirBundle = containedWithin.Annotations(typeof(Bundle)).FirstOrDefault() as Bundle;

            TEntity retVal = null;

            if (resourceRef.Identifier != null)
            {
                // Already exists in SDB bundle?
                var identifier = DataTypeConverter.ToEntityIdentifier(resourceRef.Identifier);
                retVal = sdbBundle?.Item.OfType<TEntity>().Where(e => e.Identifiers.Any(i => i.Authority.Key == identifier.AuthorityKey && i.Value == identifier.Value)).FirstOrDefault();
                if (retVal == null) // Not been processed in bundle
                {
                    retVal = repo.Find(o => o.Identifiers.Any(a => a.Authority.Key == identifier.AuthorityKey && a.Value == identifier.Value)).SingleOrDefault();
                    if (retVal == null)
                    {
                        throw new FhirException(System.Net.HttpStatusCode.NotFound, IssueType.NotFound, $"Could not locate {typeof(TEntity).Name} with identifier {identifier.Value} in domain {identifier.Authority.Url ?? identifier.Authority.Oid}");
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
                        var refRegex = new Regex("^(urn:uuid:.{36}|\\/?(\\w*?)/(.{36}))$");
                        var match = refRegex.Match(resourceRef.Reference);
                        if (!match.Success)
                            throw new FhirException(System.Net.HttpStatusCode.NotFound, IssueType.NotFound, $"Could not find {resourceRef.Reference} as a previous entry in this submission. Cannot resolve from database unless reference is either urn:uuid:UUID or Type/UUID");

                        if (!string.IsNullOrEmpty(match.Groups[2].Value) && Guid.TryParse(match.Groups[3].Value.Replace("urn:uuid:", string.Empty), out Guid relUuid)) // rel reference
                            retVal = repo.Get(relUuid); // Allow any triggers to fire
                        // HACK: Need to removed the urn:uuid: at the front of the guid.
                        else if (Guid.TryParse(match.Groups[1].Value.Replace("urn:uuid:", string.Empty), out Guid absRef))
                            retVal = repo.Get(absRef);
                    }
                }
            }
            else
                throw new ArgumentException("Could not understand resource reference");

            // TODO: Weak references
            if (retVal == null)
                throw new FhirException((System.Net.HttpStatusCode)422, IssueType.NotSupported, $"Weak references (to other servers) are not currently supported (ref: {resourceRef.Reference})");
            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="PatientContact"/> instance to an <see cref="EntityRelationship"/> instance.
        /// </summary>
        /// <param name="patientContact">The patient contact.</param>
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
                Telecoms = patientContact.Telecom?.Select(ToEntityTelecomAddress).ToList()
            })
            {
                ClassificationKey = RelationshipClassKeys.ContainedObjectLink,
                RelationshipRoleKey = DataTypeConverter.ToConcept(patientContact.Relationship.FirstOrDefault())?.Key,
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
        /// Converts a <see cref="FhirTelecom"/> instance to an <see cref="EntityTelecomAddress"/> instance.
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
                    useMnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirTelecom.Use);

                string typeMnemonic = null;
                if (fhirTelecom.System.HasValue)
                    typeMnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirTelecom.System);

                return new EntityTelecomAddress
                {
                    Value = fhirTelecom.Value,
                    AddressUseKey = ToConcept(useMnemonic, "http://hl7.org/fhir/contact-point-use")?.Key,
                    TypeConceptKey = ToConcept(typeMnemonic, "http://hl7.org/fhir/contact-point-system")?.Key
                };
            }
            return null;
        }

        /// <summary>
        /// Converts an <see cref="EntityAddress"/> instance to a <see cref="FhirAddress"/> instance.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>Returns a FHIR address.</returns>
        public static Address ToFhirAddress(EntityAddress address)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity address");

            if (address == null) return null;

            // Return value
            var retVal = new Address()
            {
                Use = DataTypeConverter.ToFhirEnumeration<Address.AddressUse>(address.LoadProperty<Concept>(nameof(EntityAddress.AddressUse)), "http://hl7.org/fhir/address-use"),
                Line = new List<String>()
            };

            // Process components
            foreach (var com in address.LoadCollection<EntityAddressComponent>(nameof(EntityAddress.Component)))
            {
                if (com.ComponentTypeKey == AddressComponentKeys.City)
                    retVal.City = com.Value;
                else if (com.ComponentTypeKey == AddressComponentKeys.Country)
                    retVal.Country = com.Value;
                else if (com.ComponentTypeKey == AddressComponentKeys.AddressLine ||
                    com.ComponentTypeKey == AddressComponentKeys.StreetAddressLine)
                    retVal.LineElement.Add(new FhirString(com.Value));
                else if (com.ComponentTypeKey == AddressComponentKeys.State)
                    retVal.State = com.Value;
                else if (com.ComponentTypeKey == AddressComponentKeys.PostalCode)
                    retVal.PostalCode = com.Value;
                else if (com.ComponentTypeKey == AddressComponentKeys.County)
                    retVal.District = com.Value;
                else
                {
                    retVal.AddExtension(
                        FhirConstants.SanteDBProfile + "#address-" + com.LoadProperty<Concept>(nameof(EntityAddressComponent.ComponentType)).Mnemonic,
                        new FhirString(com.Value)
                    );
                }
            }

            return retVal;
        }

        /// <summary>
        /// Converts a reference term to the codeable concept
        /// </summary>
        public static CodeableConcept ToFhirCodeableConcept(Concept concept, String preferredCodeSystem = null, bool nullIfNoPreferred = false)
        {
            return ToFhirCodeableConcept(concept, new string[] { preferredCodeSystem }, nullIfNoPreferred);
        }

        /// <summary>
        /// Converts a <see cref="Concept"/> instance to an <see cref="FhirCodeableConcept"/> instance.
        /// </summary>
        /// <param name="concept">The concept to be converted to a <see cref="FhirCodeableConcept"/></param>
        /// <param name="preferredCodeSystems">The preferred code system for the codeable concept</param>
        /// <param name="nullIfNoPreferred">When true, instructs the system to return only code in preferred code system or nothing</param>
        /// <returns>Returns a FHIR codeable concept.</returns>
        public static CodeableConcept ToFhirCodeableConcept(Concept concept, String[] preferredCodeSystems, bool nullIfNoPreferred)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping concept");

            if (concept == null)
            {
                return null;
            }
            if (preferredCodeSystems == null || preferredCodeSystems.All(p => p == null))
            {
                var refTerms = concept.LoadCollection<ConceptReferenceTerm>(nameof(Concept.ReferenceTerms));
                if (refTerms.Any())
                    return new CodeableConcept
                    {
                        Coding = refTerms.Select(o => DataTypeConverter.ToCoding(o.LoadProperty<ReferenceTerm>(nameof(ConceptReferenceTerm.ReferenceTerm)))).ToList(),
                        Text = concept.LoadCollection<ConceptName>(nameof(Concept.ConceptNames)).FirstOrDefault()?.Name
                    };
                else
                    return new CodeableConcept("http://openiz.org/concept", concept.Mnemonic)
                    {
                        Text = concept.LoadCollection<ConceptName>(nameof(Concept.ConceptNames)).FirstOrDefault()?.Name
                    };
            }
            else
            {
                var codeSystemService = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();
                var refTerms = preferredCodeSystems.Select(cs => codeSystemService.GetConceptReferenceTerm(concept.Key.Value, cs)).ToArray();
                if (!refTerms.Any(o => o != null) && nullIfNoPreferred)
                    return null; // No code in the preferred system, ergo, we will instead use our own
                else if (!refTerms.Any(o => o != null))
                    return new CodeableConcept
                    {
                        Coding = concept.LoadCollection<ConceptReferenceTerm>(nameof(Concept.ReferenceTerms)).Select(o => DataTypeConverter.ToCoding(o.LoadProperty<ReferenceTerm>(nameof(ConceptReferenceTerm.ReferenceTerm)))).ToList(),
                        Text = concept.LoadCollection<ConceptName>(nameof(Concept.ConceptNames)).FirstOrDefault()?.Name
                    };
                else
                    return new CodeableConcept
                    {
                        Coding = refTerms.Select(o => ToCoding(o)).ToList(),
                        Text = concept.LoadCollection<ConceptName>(nameof(Concept.ConceptNames)).FirstOrDefault()?.Name
                    };
            }
        }

        /// <summary>
        /// Converts an <see cref="EntityName"/> instance to a <see cref="FhirHumanName"/> instance.
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
                Use = DataTypeConverter.ToFhirEnumeration<HumanName.NameUse>(entityName.LoadProperty<Concept>(nameof(EntityName.NameUse)), "http://hl7.org/fhir/name-use")
            };

            // Process components
            foreach (var com in entityName.LoadCollection<EntityNameComponent>(nameof(EntityName.Component)))
            {
                if (string.IsNullOrEmpty(com.Value)) continue;

                if (com.ComponentTypeKey == NameComponentKeys.Given)
                    retVal.GivenElement.Add(new FhirString(com.Value));
                else if (com.ComponentTypeKey == NameComponentKeys.Family)
                    retVal.FamilyElement = new FhirString(com.Value);
                else if (com.ComponentTypeKey == NameComponentKeys.Prefix)
                    retVal.PrefixElement.Add(new FhirString(com.Value));
                else if (com.ComponentTypeKey == NameComponentKeys.Suffix)
                    retVal.SuffixElement.Add(new FhirString(com.Value));
            }

            return retVal;
        }

        /// <summary>
        /// To FHIR enumeration
        /// </summary>
        public static TEnum? ToFhirEnumeration<TEnum>(Concept concept, string codeSystem, bool throwIfNotFound = false) where TEnum : struct
        {
            var coding = DataTypeConverter.ToFhirCodeableConcept(concept, codeSystem, true);
            if (coding != null)
                return Hl7.Fhir.Utility.EnumUtility.ParseLiteral<TEnum>(coding.Coding.First().Code, true);
            else if (throwIfNotFound && concept != null)
                throw new ConstraintException($"Cannot find FHIR mapping for Concept {concept}");
            else
                return null;
        }

        /// <summary>
        /// Converts a <see cref="IdentifierBase{TBoundModel}" /> instance to an <see cref="FhirIdentifier" /> instance.
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

            var imetaService = ApplicationServiceContext.Current.GetService<IAssigningAuthorityRepositoryService>();
            var authority = imetaService.Get(identifier.AuthorityKey.Value);
            var retVal = new Identifier
            {
                System = authority?.Url ?? $"urn:oid:{authority?.Oid}",
                Type = ToFhirCodeableConcept(identifier.LoadProperty<IdentifierType>(nameof(EntityIdentifier.IdentifierType))?.TypeConcept),
                Value = identifier.Value
            };

            if (identifier.ExpiryDate.HasValue || identifier.IssueDate.HasValue)
                retVal.Period = new Period()
                {
                    StartElement = identifier.IssueDate.HasValue ? new FhirDateTime(identifier.IssueDate.Value) : null,
                    EndElement = identifier.ExpiryDate.HasValue ? new FhirDateTime(identifier.ExpiryDate.Value) : null
                };

            return retVal;
        }

        /// <summary>
        /// Converts an <see cref="EntityTelecomAddress"/> instance to <see cref="ContactPoint"/> instance.
        /// </summary>
        /// <param name="telecomAddress">The telecom address.</param>
        /// <returns>Returns the mapped FHIR telecom.</returns>
        public static ContactPoint ToFhirTelecom(EntityTelecomAddress telecomAddress)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity telecom address");

            return new ContactPoint
            {
                System = ToFhirEnumeration<ContactPoint.ContactPointSystem>(telecomAddress.LoadProperty(o => o.TypeConcept), "http://hl7.org/fhir/contact-point-system"),
                Use = ToFhirEnumeration<ContactPoint.ContactPointUse>(telecomAddress.LoadProperty(o => o.AddressUse), "http://hl7.org/fhir/contact-point-use"),
                Value = telecomAddress.IETFValue
            };
        }
    }
}
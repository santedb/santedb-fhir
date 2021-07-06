/*
 * Portions Copyright 2019-2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE)
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
 * User: fyfej (Justin Fyfe)
 * Date: 2019-11-27
 */
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Auditing;
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
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Convert the audit data to a security audit
        /// </summary>
        public static AuditEvent ToSecurityAudit(AuditData audit)
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
                    Role = act.ActorRoleCode.Select(o => ToFhirCodeableConcept(conceptService.GetConceptByReferenceTerm(o.Code, o.CodeSystem))).ToList(),
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
                if(Int32.TryParse(sourceType, out int sType))
                {
                    sourceTypeEnum = (AuditSourceType)sType;
                }
                else if(!Enum.TryParse<AuditSourceType>(sourceType, out sourceTypeEnum))
                {
                    sourceTypeEnum = AuditSourceType.Other;
                }


                retVal.Source = new AuditEvent.SourceComponent()
                {
                    Observer = CreateNonVersionedReference<Device>(originalSource),
                    Type = new List<Coding>() {  new Coding(sourceTypeEnum.ToString(), "http://terminology.hl7.org/CodeSystem/security-source-type") }
                };
            }
            else
            {
                var configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<AuditAccountabilityConfigurationSection>();

                retVal.Source = new AuditEvent.SourceComponent()
                {
                    Observer = CreateNonVersionedReference<Device>(configuration.SourceInformation?.EnterpriseDeviceKey ?? Guid.Empty),
                    Type = new List<Coding>() { new Coding("http://terminology.hl7.org/CodeSystem/security-source-type", "4") }
                };
            }

            // Objects / entities
            foreach (var itm in audit.AuditableObjects)
            {
                var add = new AuditEvent.EntityComponent()
                {
                    Name = itm.NameData,
                    Query = System.Text.Encoding.UTF8.GetBytes(itm.QueryData)
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
                
                foreach(var dtl in itm.ObjectData)
                {
                    add.Detail.Add(new AuditEvent.DetailComponent()
                    {
                        Type = dtl.Key,
                        Value = new Base64Binary(dtl.Value)
                    });
                }

                if (Guid.TryParse(itm.ObjectId, out Guid objectIdKey)) {
                    switch (itm.IDTypeCode)
                    {
                        case AuditableObjectIdType.Custom:
                            break;
                        case AuditableObjectIdType.PatientNumber:
                            add.What = CreateNonVersionedReference<Patient>(objectIdKey);
                            break;
                        case AuditableObjectIdType.UserIdentifier:
                            add.What = CreateNonVersionedReference<Practitioner>(objectIdKey);
                            break;
                        case AuditableObjectIdType.EncounterNumber:
                            add.What = CreateNonVersionedReference<Encounter>(objectIdKey);
                            break;
                        default:
                            add.What = new ResourceReference()
                            {
                                Reference = $"urn:uuid:{objectIdKey}",
                                Display = $"RIM [Entity/{objectIdKey}"
                            };
                            break;
                    }

                    retVal.Entity.Add(add);
                }
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
            var refer = new ResourceReference(targetEntity.Key.ToString());
            refer.Display = targetEntity.ToDisplay();
            return refer;

        }

        /// <summary>
        /// To quantity 
        /// </summary>
        public static SimpleQuantity ToQuantity(decimal? quantity, Concept unitConcept)
        {
            return new SimpleQuantity()
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
                while (error.InnerException != null)
                {
                    error = error.InnerException;
                }

                // Construct an error result
                var errorResult = new OperationOutcome()
                {
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new OperationOutcome.IssueComponent() { Diagnostics  = error.Message, Severity = IssueSeverity.Error, Code = IssueType.Exception }
                    }
                };

                if (error is DetectedIssueException dte)
                {
                    errorResult.Issue = dte.Issues.Select(iss => new OperationOutcome.IssueComponent()
                    {
                        Diagnostics = iss.Text,
                        Severity = iss.Priority == DetectedIssuePriorityType.Error ? IssueSeverity.Error :
                            iss.Priority == DetectedIssuePriorityType.Warning ? IssueSeverity.Warning :
                            IssueSeverity.Information
                    }).ToList();
                }

                return errorResult;

            }
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

                retVal.Meta.Tag = taggable.Tags.Select(tag => new Coding("http://santedb.org/fhir/tags", $"{tag.TagKey}:{tag.Value}")).ToList();
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
                retVal.Meta.Profile = DataTypeConverter.AddExtensions(extendableObject, fhirExtendable);
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
            fhirExtension.Extension = extendable?.LoadCollection(o=>o.Extensions ).Where(o => o.ExtensionTypeKey != ExtensionTypeKeys.JpegPhotoExtension).Select(DataTypeConverter.ToExtension).ToList();

            if (resource != null)
            {
                fhirExtension.Extension = fhirExtension.Extension.Union(ExtensionUtil.CreateExtensions(extendable as IIdentifiedEntity, resource.ResourceType, out IEnumerable<IFhirExtensionHandler> appliedExtensions)).ToList();
                return appliedExtensions.Select(o => o.ProfileUri?.ToString()).Distinct();
            }
            else
                return new List<String>();
        }

        /// <summary>
        /// To FHIR date
        /// </summary>
        public static Date ToFhirDate(DateTime? date)
        {
            if (date.HasValue)
                return new Date(date.Value.Year, date.Value.Month, date.Value.Day);
            else
                return null;
        }

        /// <summary>
        /// Convert two date ranges to a period
        /// </summary>
        public static Period ToPeriod(DateTimeOffset? startTime, DateTimeOffset? stopTime)
        {
            return new Period(
                startTime.HasValue ? new FhirDateTime(startTime.Value) : null,
                stopTime.HasValue ? new FhirDateTime(stopTime.Value) : null
                );
        }

        /// <summary>
        /// Converts an <see cref="FhirExtension"/> instance to an <see cref="ActExtension"/> instance.
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
        /// Converts an <see cref="FhirExtension"/> instance to an <see cref="ActExtension"/> instance.
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
                    extension.ExtensionValue = (fhirExtension.Value as FhirDateTime).Value;
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
        public static Patient.CommunicationComponent ToFhirCommunicationComponent(PersonLanguageCommunication lang)
        {
            return new Patient.CommunicationComponent()
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
                retVal = new ActIdentifier(DataTypeConverter.ToAssigningAuthority(fhirIdentifier.System), fhirIdentifier.Value);
            }
            else
            {
                throw new ArgumentException("Identifier must carry a coding system");
            }

            // TODO: Fill in use
            return retVal;
        }

        /// <summary>
        /// Convert to assigning authority
        /// </summary>
        /// <param name="fhirSystem">The FHIR system.</param>
        /// <returns>AssigningAuthority.</returns>
        /// <exception cref="System.InvalidOperationException">Unable to locate service</exception>
        public static AssigningAuthority ToAssigningAuthority(FhirUri fhirSystem)
        {
            if (fhirSystem == null)
            {
                return null;
            }

            return DataTypeConverter.ToAssigningAuthority(fhirSystem.Value);
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
                throw new KeyNotFoundException($"Could not find identity domain {fhirSystem}");
            return oid;
        }

        /// <summary>
        /// Converts a <see cref="ReferenceTerm"/> instance to a <see cref="FhirCoding"/> instance.
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

            var cs = referenceTerm.LoadProperty<Core.Model.DataTypes.CodeSystem>(nameof(ReferenceTerm.CodeSystem));
            return new Coding(cs.Url ?? String.Format("urn:oid:{0}", cs.Oid), referenceTerm.Mnemonic);
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

            if (ext.Value is Decimal || eType.ExtensionHandler == typeof(DecimalExtensionHandler))
                retVal.Value = new FhirDecimal((Decimal)(ext.Value ?? new DecimalExtensionHandler().DeSerialize(ext.Data)));
            else if (ext.Value is String || eType.ExtensionHandler == typeof(StringExtensionHandler))
                retVal.Value = new FhirString((String)(ext.Value ?? new StringExtensionHandler().DeSerialize(ext.Data)));
            else if (ext.Value is Boolean || eType.ExtensionHandler == typeof(BooleanExtensionHandler))
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
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping codeable concept");
            var retVal = codeableConcept?.Coding.Select(o => DataTypeConverter.ToConcept(o)).FirstOrDefault(o => o != null);
            if (retVal == null)
                throw new ConstraintException($"Can't find any reference term mappings from '{codeableConcept.Coding.FirstOrDefault().Code}' in {codeableConcept.Coding.FirstOrDefault().System} to a Concept");
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
        public static Concept ToConcept(Coding coding, String defaultSystem = null)
        {
            if (coding == null)
            {
                return null;
            }

            return ToConcept(coding.Code, coding.System ?? defaultSystem);
        }

        /// <summary>
        /// Convert to concept
        /// </summary>
        public static Concept ToConcept(String code, String system)
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
                throw new KeyNotFoundException($"Could not map concept {system}#{code} to a concept");
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
        public static Concept ToConcept<T>(String code, string system)
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
                throw new ConstraintException($"Could not find concept with reference term '{code}' in {system}");
            return retVal;
        }

        /// <summary>
        /// Converts an <see cref="FhirAddress"/> instance to an <see cref="EntityAddress"/> instance.
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

            if (!String.IsNullOrEmpty(fhirAddress.City))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.City, fhirAddress.City));
            }

            if (!String.IsNullOrEmpty(fhirAddress.Country))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.Country, fhirAddress.Country));
            }

            if (fhirAddress.Line?.Any() == true)
            {
                address.Component.AddRange(fhirAddress.Line.Select(a => new EntityAddressComponent(AddressComponentKeys.AddressLine, a)));
            }

            if (!String.IsNullOrEmpty(fhirAddress.State))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.State, fhirAddress.State));
            }

            if (!String.IsNullOrEmpty(fhirAddress.PostalCode))
            {
                address.Component.Add(new EntityAddressComponent(AddressComponentKeys.PostalCode, fhirAddress.PostalCode));
            }

            if (!String.IsNullOrEmpty(fhirAddress.District))
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
            if(fhirHumanName.Use.HasValue)
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
        public static IdentifiedData ResolveEntity(ResourceReference resourceRef, Resource containedWithin)
        {
            var repo = ApplicationServiceContext.Current.GetService<IRepositoryService<Entity>>();

            // First is there a bundle in the contained within
            var sdbBundle = containedWithin.Annotations(typeof(Core.Model.Collection.Bundle)).FirstOrDefault() as Core.Model.Collection.Bundle;
            
            IdentifiedData retVal = null;

            if (resourceRef.Identifier != null)
            {
                // Already exists in SDB bundle?
                var identifier = DataTypeConverter.ToEntityIdentifier(resourceRef.Identifier);
                retVal = sdbBundle?.Item.OfType<IHasIdentifiers>().Where(e => e.Identifiers.Any(i => i.Authority.Key == identifier.AuthorityKey && i.Value == identifier.Value)) as IdentifiedData;
                if (retVal == null) // Not been processed in bundle
                {
                    retVal = repo.Find(o => o.Identifiers.Any(a => a.AuthorityKey == identifier.AuthorityKey && a.Value == identifier.Value), 0, 1, out int tr).FirstOrDefault();
                    if (tr > 1)
                        throw new InvalidOperationException($"Reference to {identifier} is ambiguous ({tr} records have this identity)");
                }

            }
            else if (!String.IsNullOrEmpty(resourceRef.Reference))
            {
                if (resourceRef.Reference.StartsWith("#") && containedWithin is DomainResource domainResource) // Rel
                {
                    var contained = domainResource.Contained.Find(o => o.Id.Equals(resourceRef.Reference.Substring(1)));
                    if(contained == null)
                    {
                        throw new ArgumentException($"Relative reference provided but cannot find contained object {resourceRef.Reference}");
                    }

                    var mapper = FhirResourceHandlerUtil.GetMapperForInstance(contained);
                    if(mapper == null)
                    {
                        throw new ArgumentException($"Don't understand how to convert {contained.ResourceType}");
                    }

                    retVal = mapper.MapToModel(contained);
                    
                }
                else
                {
                    retVal = sdbBundle?.Item.OfType<ITaggable>().FirstOrDefault(e => e.GetTag(FhirConstants.OriginalUrlTag) == resourceRef.Reference || e.GetTag(FhirConstants.OriginalIdTag) == resourceRef.Reference) as IdentifiedData;
                    if (retVal == null)
                    {
                        // Attempt to resolve the reference 
                        var refRegex = new Regex("^(urn:uuid:.{36}|(\\w*?)/(.{36}))$");
                        var match = refRegex.Match(resourceRef.Reference);
                        if (!match.Success)
                            throw new KeyNotFoundException($"Could not find {resourceRef.Reference} as a previous entry in this submission. Cannot resolve from database unless reference is either urn:uuid:UUID or Type/UUID");

                        if (!string.IsNullOrEmpty(match.Groups[2].Value) && Guid.TryParse(match.Groups[3].Value, out Guid relUuid)) // rel reference
                            retVal = repo.Get(relUuid); // Allow any triggers to fire
                        else if (Guid.TryParse(match.Groups[1].Value, out Guid absRef))
                            retVal = repo.Get(absRef);

                    }
                }
            }
            else
                throw new ArgumentException("Could not understand resource reference");

            // TODO: Weak references
            if (retVal == null)
                throw new NotSupportedException($"Weak references (to other servers) are not currently supported (ref: {resourceRef.Reference})");
            return retVal;
        }

        /// <summary>
        /// Converts a <see cref="PatientContact"/> instance to an <see cref="EntityRelationship"/> instance.
        /// </summary>
        /// <param name="patientContact">The patient contact.</param>
        /// <returns>Returns the mapped entity relationship instance..</returns>
        public static EntityRelationship ToEntityRelationship(Patient.ContactComponent patientContact, Patient patient)
        {
            var retVal = new EntityRelationship();

            // Relationship is a contact
            retVal.RelationshipTypeKey = EntityRelationshipTypeKeys.Contact;
            retVal.ClassificationKey = RelationshipClassKeys.ContainedObjectLink;
            retVal.RelationshipRole = DataTypeConverter.ToConcept(patientContact.Relationship.FirstOrDefault());
            retVal.TargetEntity = new Core.Model.Entities.Person()
            {
                Key = Guid.NewGuid(),
                Addresses = patientContact.Address != null ? new List<EntityAddress>() { DataTypeConverter.ToEntityAddress(patientContact.Address) } : null,
                CreationTime = DateTimeOffset.Now,
                // TODO: Gender (after refactor)
                Names = patientContact.Name != null ? new List<EntityName>() { DataTypeConverter.ToEntityName(patientContact.Name) } : null,
                Telecoms = patientContact.Telecom?.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList()
            };

            retVal.TargetEntity.Extensions.AddRange(patientContact.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, retVal.TargetEntity)).OfType<EntityExtension>());
            if (patientContact.Organization != null)

            // Is there an organization assigned?
            if (patientContact.Organization != null && patientContact.Name == null &&
                patientContact.Address == null)
            {
                var refObjectKey = DataTypeConverter.ResolveEntity(patientContact.Organization, patient);
                if (refObjectKey != null)
                    retVal.TargetEntity.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Scoper, refObjectKey.Key));
                else  // TODO: Implement
                    throw new KeyNotFoundException($"Could not resolve reference to patientContext.Organization");
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

            if (!String.IsNullOrEmpty(fhirTelecom.Value)) {
                var mnemonic = "temp";
                if(fhirTelecom.Use.HasValue)
                    mnemonic = Hl7.Fhir.Utility.EnumUtility.GetLiteral(fhirTelecom.Use);

                return new EntityTelecomAddress
                {
                    Value = fhirTelecom.Value,
                    AddressUseKey = ToConcept(mnemonic, "http://hl7.org/fhir/contact-point-use")?.Key
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

            if (preferredCodeSystems == null)
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
                if (!refTerms.Any(o=>o != null) && nullIfNoPreferred)
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
                        Coding = refTerms.Select(o=>ToCoding(o)).ToList(),
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
        /// Converts an <see cref="EntityTelecomAddress"/> instance to <see cref="FhirTelecom"/> instance.
        /// </summary>
        /// <param name="telecomAddress">The telecom address.</param>
        /// <returns>Returns the mapped FHIR telecom.</returns>
        public static ContactPoint ToFhirTelecom(EntityTelecomAddress telecomAddress)
        {
            traceSource.TraceEvent(EventLevel.Verbose, "Mapping entity telecom address");

            return new ContactPoint()
            {
                Use = DataTypeConverter.ToFhirEnumeration<ContactPoint.ContactPointUse>(telecomAddress.AddressUse, "http://hl7.org/fhir/contact-point-use"),
                Value = telecomAddress.IETFValue
            };
        }

    }
}
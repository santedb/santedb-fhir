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
using DocumentFormat.OpenXml.Math;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Structure definition utility
    /// </summary>
    public static class StructureDefinitionUtil
    {
        private static readonly ILocalizationService s_localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
        private static readonly String s_profileBase = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<FhirServiceConfigurationSection>()?.DefaultProfileBase;

        /// <summary>
        /// True if the extension is locally defined
        /// </summary>
        public static bool IsRemotelyDefined(this IFhirExtensionHandlerEx handler) => handler.ProfileUri.ToString() != FhirConstants.SanteDBProfile && 
                handler.ProfileUri.ToString() != s_profileBase &&
                !ExtensionUtil.ProfileHandlers.Any(r => r.ProfileUri == handler.ProfileUri);

        /// <summary>
        /// Get the structure definition for the specified handler
        /// </summary>
        public static StructureDefinition GetStructureDefinition(this IFhirExtensionHandlerEx handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            else if(handler.IsRemotelyDefined())
            {
                return null;
            }

            var retVal = new StructureDefinition()
            {
                Abstract = false,
                Description = new Markdown(handler.GetType().GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description),
                Url = handler.Uri.ToString(),
                Name = handler.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName,
                FhirVersion = FHIRVersion.N4_3_0,
                Id = handler.ProfileUri.ToString() == FhirConstants.SanteDBProfile ? handler.Uri.Segments.Last() : handler.ProfileUri.Segments.Last(),
                DateElement = DataTypeConverter.ToFhirDateTime(DateTimeOffset.Now),
                Kind = StructureDefinition.StructureDefinitionKind.ComplexType,
                Type = "Extension",
                Meta = new Meta()
                {
                    LastUpdated = String.IsNullOrEmpty(handler.GetType().Assembly.Location) ? DateTimeOffset.Now : new FileInfo(handler.GetType().Assembly.Location).LastWriteTime
                },
                Context = new List<StructureDefinition.ContextComponent>()
                {
                    new StructureDefinition.ContextComponent()
                    {
                        Type = StructureDefinition.ExtensionContextType.Element,
                        Expression = EnumUtility.GetLiteral(handler.AppliesTo ?? ResourceType.DomainResource)
                    }
                },
                BaseDefinition = "http://hl7.org/fhir/StructureDefinition/Extension",
                Derivation = StructureDefinition.TypeDerivationRule.Constraint,
                Differential = new StructureDefinition.DifferentialComponent()
                {
                    Element = new List<ElementDefinition>()
                    {
                        new ElementDefinition()
                        {
                            ElementId = "extension.url",
                            Path = "Extension.url",
                            Min = 1,
                            Max = "1",
                            Type = new List<ElementDefinition.TypeRefComponent>()
                            {
                                new ElementDefinition.TypeRefComponent()
                                {
                                    Code = "String"
                                }
                            },
                            Fixed = new FhirUri(handler.Uri),
                            IsModifier = false,
                            Definition = new Markdown($"Fixed to {handler.Uri} to indicate use of the {handler.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName} extension")
                        },
                        new ElementDefinition()
                        {
                            ElementId = "extension.value[x]",
                            Path = "Extension.value[x]",
                            Min = 1,
                            Max = "1",
                            Type = new List<ElementDefinition.TypeRefComponent>()
                            {
                                new ElementDefinition.TypeRefComponent()
                                {
                                    Code = EnumUtility.GetLiteral(handler.ValueType)
                                }
                            },
                            Definition = new Markdown($"The {handler.ValueType} which modifies the value of the {handler.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName} extension")
                        }
                    }
                },
                Version = handler.GetType()?.Assembly.GetName().Version.ToString(),
                VersionId = handler.GetType()?.Assembly.GetName().Version.ToString(),
                Copyright = new Markdown(handler.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright),
                Experimental = false,
                Publisher = handler.GetType().Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company,
                Status = PublicationStatus.Active,
            };

            if (!retVal.HasSnapshot)
            {
                retVal.Snapshot = typeof(Extension).GetFhirClassMapping().GenerateSnapshot(retVal.Differential?.Element);
            }
            return retVal;
        }

        /// <summary>
        /// Get structure definition
        /// </summary>
        public static StructureDefinition GetStructureDefinition(this Type source, bool isProfile = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source), s_localizationService.GetString("error.type.ArgumentNullException"));
            }

            // Base structure definition
            var entryAssembly = Assembly.GetEntryAssembly();

            var fhirType = source.GetCustomAttribute<FhirTypeAttribute>();

            // Create the structure definition
            var retVal = new StructureDefinition
            {
                Url = $"{s_profileBase}/{nameof(StructureDefinition)}/{fhirType.Name}",
                Abstract = source.IsAbstract,
                Meta = new Meta()
                {
                    LastUpdated = String.IsNullOrEmpty(source.Assembly.Location) ? DateTimeOffset.Now : new FileInfo(source.Assembly.Location).LastWriteTime
                },
                Title = source.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description,
                Contact = new List<ContactDetail>
                {
                    new ContactDetail
                    {
                        Name = source.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company
                    }
                },
                Name = source.Name,
                Description = new Markdown(source.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? source.Name),
                FhirVersion = FHIRVersion.N4_3_0,
                DateElement = DataTypeConverter.ToFhirDateTime(DateTimeOffset.Now),
                Kind = fhirType.IsResource ? StructureDefinition.StructureDefinitionKind.Resource : StructureDefinition.StructureDefinitionKind.ComplexType,
                Type = fhirType.Name,
                Derivation = StructureDefinition.TypeDerivationRule.Constraint,
                Id = source.GetCustomAttribute<XmlTypeAttribute>()?.TypeName ?? source.Name,
                Version = entryAssembly?.GetName().Version.ToString(),
                VersionId = source.Assembly.GetName().Version.ToString(),
                Copyright = new Markdown(source.Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright),
                Experimental = false,
                Publisher = source.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company,
                Status = PublicationStatus.Active,
                BaseDefinition = source.GetFhirClassMapping().Canonical
            };

            if (fhirType.IsResource) {
                var resourceType = EnumUtility.ParseLiteral<ResourceType>(fhirType.Name);
                var extensions = ExtensionUtil.ExtensionHandlers.Where(e => e.AppliesTo == resourceType || e.AppliesTo == null).OfType<IFhirExtensionHandlerEx>();
                if(extensions.Any()) // we need to slice the extension elements
                {
                    retVal.Differential = retVal.Differential ?? new StructureDefinition.DifferentialComponent();
                    retVal.Differential.Element.AddRange(extensions.Select(e => new ElementDefinition($"{retVal.Name}.{(e.IsModifier ? "modifierExtension" : "extension")}:{e.GetType().Name}")
                    {
                        ElementId = $"{retVal.Name}.{(e.IsModifier ? "modifierExtension" : "extension")}:{e.GetType().Name}",
                        SliceName = e.GetType().Name,
                        Short = e.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName,
                        Definition = new Markdown(e.GetType().GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description),
                        Min = 0, 
                        Max = "1",
                        Type = new List<ElementDefinition.TypeRefComponent>()
                        {
                            new ElementDefinition.TypeRefComponent()
                            {
                                Code = "Extension",
                                TargetProfile = new String[] { e.IsRemotelyDefined() ? e.ProfileUri.ToString() :  $"{s_profileBase}/StructureDefinition/{e.Uri.Segments.Last()}" }
                            }
                        },
                        IsModifier = e.IsModifier,
                        MustSupport = e.IsModifier
                    }));
                }

            }

            if (retVal.Differential != null) {
                retVal.Differential.Element = retVal.Differential.Element.OrderBy(o => o.Path).ToList();

            }

            if (!retVal.HasSnapshot)
            {
                retVal.Snapshot = source.GetFhirClassMapping().GenerateSnapshot(retVal.Differential?.Element);
            }
            return retVal;
        }

        public static StructureDefinition.SnapshotComponent GenerateSnapshot(this ClassMapping me, List<ElementDefinition> differential)
        {
            var retVal = new StructureDefinition.SnapshotComponent();

            foreach(var propertyMap in me.PropertyMappings)
            {
                retVal.Element.AddRange(propertyMap.GenerateElementDefinitions(me.Name, differential));
            }

            var notIncludedDiffs = differential?.Where(r => !retVal.Element.Any(e => e.Path == r.Path));
            if (notIncludedDiffs?.Any() == true)
            {
                retVal.Element.AddRange(notIncludedDiffs);
            }
            return retVal;
        }

        public static IEnumerable<ElementDefinition> GenerateElementDefinitions(this PropertyMapping propertyMap, String rootName, List<ElementDefinition> differential)
        {
            var pathName = $"{rootName}.{propertyMap.Name}";

            if(propertyMap.FhirType.Length > 1 || propertyMap.Choice != ChoiceType.None)
            {
                pathName += "[x]";
            }
            
            var diff = differential?.FirstOrDefault(o => o.Path == pathName);
            if (diff != null) yield return diff;
            else
            {
                yield return new ElementDefinition()
                {
                    Path = pathName,
                    ElementId = pathName,
                    Min = propertyMap.IsMandatoryElement ? 1 : 0,
                    Max = propertyMap.IsCollection ? "*" : "1",
                    Type = propertyMap.FhirType.Select(o=>o.GetFhirClassMapping().Name).Select(o=> new ElementDefinition.TypeRefComponent()
                    {
                        Code = o
                    }).ToList()
                };
            }

            if(propertyMap.PropertyTypeMapping.IsNestedType)
            {
                foreach(var cc in propertyMap.PropertyTypeMapping.PropertyMappings)
                {
                    foreach(var element in cc.GenerateElementDefinitions($"{rootName}.{propertyMap.Name}", differential))
                    {
                        yield return element;
                    }
                }
            }

        }
        public static ClassMapping GetFhirClassMapping(this ResourceType resourceType)
        {
            // Validate that the type matches the expected type
            var classMapping = ModelInfo.ModelInspector.FindClassMapping(EnumUtility.GetLiteral(resourceType));
            if (classMapping == null)
            {
                throw new InvalidOperationException();
            }
            return classMapping;
        }

        public static ClassMapping GetFhirClassMapping(this Type fhirType)
        {
            // Validate that the type matches the expected type
            var classMapping = ModelInfo.ModelInspector.FindClassMapping(fhirType);
            if (classMapping == null)
            {
                throw new InvalidOperationException();
            }
            return classMapping;
        }

        public static PropertyMapping ExtractPropertyMapping(this ClassMapping classMapping, String propertyPath)
        {
            var tokens = propertyPath.Split('.');
            if (tokens[0] != classMapping.Name || tokens.Length == 1)
            {
                return null;
            }
            var processStack = new Queue<String>(tokens.Skip(1));
            PropertyMapping propertyMapping = null;
            while(processStack.Any())
            {
                var stackName = processStack.Dequeue();
                propertyMapping = classMapping.FindMappedElementByName(stackName) ??
                    classMapping.FindMappedElementByChoiceName(stackName);
                if (propertyMapping == null)
                {
                    throw new InvalidOperationException($"{stackName} on {classMapping.Name} not found");
                }
                classMapping = propertyMapping.PropertyTypeMapping;
            }
            return propertyMapping;
        }

        /// <summary>
        /// Common constraint for identifier
        /// </summary>
        public static ElementDefinition ConstrainIdentifier<THasIdentifiers>(this StructureDefinition me)
            where THasIdentifiers : IdentifiedData, IHasIdentifiers
        {
            var retVal = me.ConstrainField("identifier")
                .Mapping<THasIdentifiers>(o => o.Identifiers);

            me.ConstrainField("identifier.system")
                .WithDefinition("Must be registered domain in SanteDB instance")
                .WithMustSupport()
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().IdentityDomain.Oid)
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().IdentityDomain.Url);
         
            me.ConstrainField("identifier.type")
                .WithDefinition("The type of identifier - maps to identifier type")
                .WithBinding("http://hl7.org/fhir/ValueSet/identifier-type", BindingStrength.Extensible)
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().IdentifierType);

            me.ConstrainField("identifier.period")
                .WithDefinition("Maps to the issue date and expiry date of the identifier")
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().IssueDate)
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().ExpiryDate);

            me.ConstrainField("identifier.value")
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().Value);

            me.ConstrainField("identifier.value")
                .Mapping<THasIdentifiers>(o => o.Identifiers.FirstOrDefault().Value);

            return retVal;

        }
        /// <summary>
        /// Constraint a single field according to the constraint 
        /// </summary>
        /// <returns></returns>
        public static ElementDefinition ConstrainField(this StructureDefinition me, String elementPath)
        {
            var resourceClass = ModelInfo.ModelInspector.FindClassMappingByCanonical(me.BaseDefinition);

            if (!elementPath.StartsWith($"{resourceClass.Name}."))
            {
                elementPath = $"{resourceClass.Name}.{elementPath}";
            }
            if (me.Differential == null)
            {
                me.Differential = new StructureDefinition.DifferentialComponent();
            }
            if (me.Differential.Element == null)
            {
                me.Differential.Element = new List<ElementDefinition>();
            }

            var pathElement = me.Differential.Element.FirstOrDefault(o => o.Path == elementPath);
            if(pathElement == null)
            {
                var elementMapping = resourceClass.ExtractPropertyMapping(elementPath);
                pathElement = new ElementDefinition(elementPath)
                {
                    Type = new List<ElementDefinition.TypeRefComponent>()
                    {
                        new ElementDefinition.TypeRefComponent() { Code = elementMapping.PropertyTypeMapping.Name }
                    },
                    Min = elementMapping.IsMandatoryElement ? 1 : 0,
                    Base = new ElementDefinition.BaseComponent()
                    {
                        Min = elementMapping.IsMandatoryElement ? 1: 0,
                        Max = elementMapping.IsCollection ? "*" : "1",
                        Path = elementPath
                    },
                    Max = elementMapping.IsCollection ? "*" : "1",
                };
                pathElement.ElementId = elementPath;
                me.Differential.Element.Add(pathElement);

                // Does this exist on the snapshot? if so we want to add it 
                if (me.HasSnapshot) {

                    var snapshotMe = me.Snapshot.Element.Find(o => o.Path == elementPath);
                    if (snapshotMe != null)
                    {
                        me.Snapshot.Element.Insert(me.Snapshot.Element.IndexOf(snapshotMe), pathElement);
                        me.Snapshot.Element.Remove(snapshotMe);
                    }
                    else
                    {
                        me.Snapshot.Element.Add(pathElement);
                    }
                }
            }
            return pathElement;
        }

        public static ElementDefinition WithFixedValue(this ElementDefinition me, DataType value)
        {
            me.Fixed = value;
            return me;
        }

        public static ElementDefinition WithMinOccurs(this ElementDefinition me, int minOccurs) {
            me.Min = minOccurs;
            return me;
        }

        public static ElementDefinition WithMaxOccurs(this ElementDefinition me, string maxOccurs)
        {
            me.Max = maxOccurs;
            return me;
        }

        public static ElementDefinition WithType(this ElementDefinition me, FHIRAllTypes type, params ResourceType[] profileResources)
        {
            me.Type = new List<ElementDefinition.TypeRefComponent>()
            {
                new ElementDefinition.TypeRefComponent()
                {
                    Code = type.GetLiteral(),
                    TargetProfile = profileResources.Select(o=> $"{s_profileBase}/StructureDefinition/{EnumUtility.GetLiteral(o)}")
                }
            };
            return me;
        }

        public static ElementDefinition WithDefinition(this ElementDefinition me, String comment)
        {
            me.Definition = new Markdown(comment);
            return me;
        }

        public static ElementDefinition WithMaxLength(this ElementDefinition me, int maxLength)
        {
            me.MaxLength = maxLength;
            return me;
        }

        public static ElementDefinition WithMustSupport(this ElementDefinition me, bool mustSupport = true)
        {
            me.MustSupport = mustSupport;
            return me;
        }

        public static ElementDefinition WithBinding(this ElementDefinition me, string valueSetBinding, BindingStrength strength)
        {
            me.Binding = me.Binding ?? new ElementDefinition.ElementDefinitionBindingComponent();
            me.Binding.ValueSet = valueSetBinding;
            me.Binding.Strength = strength;
            return me;
        }

        public static ElementDefinition Mapping<TResource>(this ElementDefinition me, System.Linq.Expressions.Expression<Func<TResource, Object>> selector)
            where TResource : IdentifiedData
        {
            me.Mapping = me.Mapping ?? new List<ElementDefinition.MappingComponent>();
            me.Mapping.Add(new ElementDefinition.MappingComponent()
            {
                Identity = "santedb+hdsi",
                Language = "http://santedb.org/model#hdsi",
                Map =$"{typeof(TResource).GetSerializationName()}.{QueryExpressionBuilder.BuildPropertySelector(selector)}"
            });
            return me;
        }

        public static ElementDefinition NotSupported(this ElementDefinition me)
        {
            return me.WithDefinition("Not Supported").WithMaxOccurs("0");
        }
    }
}
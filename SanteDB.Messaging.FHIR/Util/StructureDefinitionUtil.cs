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
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
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

        /// <summary>
        /// Get the structure definition for the specified handler
        /// </summary>
        public static StructureDefinition GetStructureDefinition(this IFhirExtensionHandlerEx handler)
        {
            if(handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return new StructureDefinition()
            {
                Abstract = false,
                Description = new Markdown(handler.GetType().GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description),
                Url = handler.ProfileUri.ToString(),
                Name = handler.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName,
                FhirVersion = FHIRVersion.N4_3_0,
                Id = handler.ProfileUri.Segments.Last(),
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
                        Expression = EnumUtility.GetLiteral(handler.AppliesTo)
                    }
                },
                BaseDefinition = "http://hl7.org/fhir/StructureDefinition/Extension",
                Derivation = StructureDefinition.TypeDerivationRule.Constraint,
                Snapshot = new StructureDefinition.SnapshotComponent()
                {
                    Element = new List<ElementDefinition>()
                    {
                        new ElementDefinition()
                        {
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
                        },
                        new ElementDefinition()
                        {
                            Path = "Extension.value[x]",
                            Min = 1,
                            Max = "1",
                            Type = new List<ElementDefinition.TypeRefComponent>()
                            {
                                new ElementDefinition.TypeRefComponent()
                                {
                                    Code = EnumUtility.GetLiteral(handler.ValueType)
                                }
                            }
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
                Url = $"/{nameof(StructureDefinition)}/{fhirType.Name}",
                Abstract = source.IsAbstract,
                Meta = new Meta()
                {
                    LastUpdated = String.IsNullOrEmpty(source.Assembly.Location) ? DateTimeOffset.Now : new FileInfo(source.Assembly.Location).LastWriteTime
                },
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
                BaseDefinition = $"http://hl7.org/fhir/StructureDefinition/{fhirType.Name}"
            };

            return retVal;
        }

        /// <summary>
        /// Constraint a single field according to the constraint 
        /// </summary>
        /// <returns></returns>
        public static ElementDefinition ConstrainField(this StructureDefinition me, String elementPath)
        {
            var resourceName = new Uri(me.BaseDefinition).Segments.Last();

            if (!elementPath.StartsWith($"{resourceName}."))
            {
                elementPath = $"{resourceName}.{elementPath}";
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
                pathElement = new ElementDefinition();
                pathElement.Path = elementPath;
                me.Differential.Element.Add(pathElement);
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

        public static ElementDefinition WithType(this ElementDefinition me, FHIRAllTypes type)
        {
            me.Type = new List<ElementDefinition.TypeRefComponent>()
            {
                new ElementDefinition.TypeRefComponent()
                {
                    Code = type.GetLiteral()
                }
            };
            return me;
        }

        public static ElementDefinition WithComment(this ElementDefinition me, String comment)
        {
            me.Comment = new Markdown(comment);
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
            return me.WithComment("Not Supported").WithMaxOccurs("0");
        }
    }
}
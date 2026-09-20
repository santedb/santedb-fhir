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
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public static StructureDefinition GetStructureDefinition(this IFhirExtensionHandler handler)
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
                Name = handler.Uri.Segments.Last(),
                FhirVersion = FHIRVersion.N4_3_0,
                DateElement = DataTypeConverter.ToFhirDateTime(DateTimeOffset.Now),
                Kind = StructureDefinition.StructureDefinitionKind.ComplexType,
                Type = "Extension",
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
                                    Code = "http://hl7.org/fhirpath/System.String"
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
                                    Code = handler.ValueType.GetCustomAttribute<FhirTypeAttribute>().Name
                                }
                            }
                        }
                    }
                },
                Version = handler.GetType()?.Assembly.GetName().Version.ToString(),
                VersionId = handler.GetType()?.Assembly.GetName().ToString(),
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
    }
}
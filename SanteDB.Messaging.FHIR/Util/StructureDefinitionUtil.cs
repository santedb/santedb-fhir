/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
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
                Abstract = source.IsAbstract,
                Contact = new List<ContactDetail>
                {
                    new ContactDetail
                    {
                        Name = source.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company
                    }
                },
                Name = source.Name,
                Description = new Markdown(source.GetCustomAttribute<DescriptionAttribute>()?.Description ?? source.Name),
                FhirVersion = FHIRVersion.N4_0_0,
                DateElement = DataTypeConverter.ToFhirDateTime(DateTimeOffset.Now),
                Kind = fhirType.IsResource ? StructureDefinition.StructureDefinitionKind.Resource : StructureDefinition.StructureDefinitionKind.ComplexType,
                Type = fhirType.Name,
                Derivation = StructureDefinition.TypeDerivationRule.Constraint,
                Id = source.GetCustomAttribute<XmlTypeAttribute>()?.TypeName ?? source.Name,
                Version = entryAssembly?.GetName().Version.ToString(),
                VersionId = source.Assembly.GetName().Version.ToString(),
                Copyright = new Markdown(source.Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright),
                Experimental = true,
                Publisher = source.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company,
                Status = PublicationStatus.Active
            };

            // TODO: Scan for profile handlers
            return retVal;
        }
    }
}
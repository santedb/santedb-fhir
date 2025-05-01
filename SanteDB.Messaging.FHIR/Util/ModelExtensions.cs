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
using SanteDB.Core.Interop.Description;
using System;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Get model extensions
    /// </summary>
    public static class ModelExtensions
    {

        /// <summary>
        /// Get primary code
        /// </summary>
        public static Coding GetCoding(this CodeableConcept me) => me.Coding.FirstOrDefault();

        /// <summary>
        /// Get the resource type
        /// </summary>
        /// <param name="me"></param>
        /// <returns></returns>
        public static ResourceType? GetResourceType(this Type me)
        {
            var fhirType = me.GetCustomAttribute<FhirTypeAttribute>();

            return fhirType?.IsResource == true && string.IsNullOrEmpty(fhirType.Name) ? null : Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(fhirType?.Name);
        }

        /// <summary>
        /// Create a description
        /// </summary>
        public static ResourceDescription CreateDescription(this ResourceType me)
        {
            return new ResourceDescription(me.ToString(), $"FHIR Resource {me}");
        }

        /// <summary>
        /// FHIR Resource
        /// </summary>
        public static ResourceDescription CreateDescription(this FHIRAllTypes me)
        {
            return new ResourceDescription(me.ToString(), $"FHIR Resource {me}");
        }
    }
}

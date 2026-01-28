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
using System;
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// Represents a profile handler that can validate whether a resource conforms to 
    /// a profile.
    /// </summary>
    /// <remarks>
    /// This interface, in combination with one or more IFhirOperationHandler and IFhirExtensionHandler
    /// interfaces is used to implement/override custom domain specific profiles in FHIR
    /// </remarks>
    public interface IFhirProfileValidationHandler
    {

        /// <summary>
        /// Gets the defined profile URI
        /// </summary>
        Uri ProfileUri { get; }

        /// <summary>
        /// Gets the type this applies to (or null if it applies to all)
        /// </summary>
        IEnumerable<ResourceType> AppliesTo { get; }

        /// <summary>
        /// Gets the structure definition
        /// </summary>
        StructureDefinition Definition { get; }

        /// <summary>
        /// Validate the resource and emit detected issues
        /// </summary>
        /// <param name="resource">The resource instance</param>
        /// <returns>The list of detected issues</returns>
        List<Core.BusinessRules.DetectedIssue> Validate(Resource resource);


    }
}

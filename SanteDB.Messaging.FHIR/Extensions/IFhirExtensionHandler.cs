/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-5-30
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using System;
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// FHIR Extension Handler
    /// </summary>
    /// <remarks>
    /// This interface is used when processing resources to/from FHIR and allow
    /// custom FHIR extensions to map data from extensions into the underlying
    /// objects in the CDR schema.
    /// </remarks>
    public interface IFhirExtensionHandler
    {
        /// <summary>
        /// Gets the URI of the extension
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Gets the URI of the profile that this extension is defined in
        /// </summary>
        Uri ProfileUri { get; }

        /// <summary>
        /// Gets the resource type that this applies to (or null if it applies to all types)
        /// </summary>
        ResourceType? AppliesTo { get; }

        /// <summary>
        /// Before returning the model object to the caller
        /// </summary>
        /// <param name="modelObject">The object which the construction occurs from</param>
        /// <returns>The constructed FHIR extension</returns>
        IEnumerable<Extension> Construct(IAnnotatedResource modelObject);

        /// <summary>
        /// Parse the specified extension
        /// </summary>
        /// <param name="fhirExtension">The FHIR Extension to parsed</param>
        /// <param name="modelObject">The current model object into which the extension data should be added</param>
        bool Parse(Extension fhirExtension, IdentifiedData modelObject);
    }
}
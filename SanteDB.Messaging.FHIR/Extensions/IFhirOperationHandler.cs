/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// Represents an extension point 
    /// </summary>
    /// <remarks>
    /// This interface is used to extend the FHIR interface for FHIR operations (like $validate, $match, etc.)
    /// and allows plugins to add behaviors to the API layer.
    /// </remarks>
    public interface IFhirOperationHandler
    {

        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Get URL of the operation
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The type that this operation handler applies to (or null if it applies to all)
        /// </summary>
        ResourceType[] AppliesTo { get; }

        /// <summary>
        /// Get the parameter list for this object
        /// </summary>
        IDictionary<String, FHIRAllTypes> Parameters { get; }

        /// <summary>
        /// True if the operation impacts the object state
        /// </summary>
        bool IsGet { get; }

        /// <summary>
        /// Invoke the specified operation
        /// </summary>
        /// <param name="parameters">The parameter set to action</param>
        /// <returns>The result of the operation</returns>
        Resource Invoke(Parameters parameters);

    }
}

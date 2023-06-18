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
using SanteDB.Core.Services;
using System.Collections.Specialized;

namespace SanteDB.Messaging.FHIR.Handlers
{

    /// <summary>
    /// Represents a class that can handle a FHIR resource query request
    /// </summary>
    public interface IFhirResourceHandler
    {

        /// <summary>
        /// Gets the type of resource this handler can perform operations on
        /// </summary>
        ResourceType ResourceType { get; }

        /// <summary>
        /// Read a specific version of a resource
        /// </summary>
        Resource Read(string id, string versionId);

        /// <summary>
        /// Update a resource
        /// </summary>
        Resource Update(string id, Resource target, TransactionMode mode);

        /// <summary>
        /// Delete a resource
        /// </summary>
        Resource Delete(string id, TransactionMode mode);

        /// <summary>
        /// Create a resource
        /// </summary>
        Resource Create(Resource target, TransactionMode mode);

        /// <summary>
        /// Query a FHIR resource
        /// </summary>
        Bundle Query(NameValueCollection parameters);

        /// <summary>
        /// Get the history of a specific FHIR object
        /// </summary>
        Bundle History(string id);

        /// <summary>
        /// Get the definition for this resource
        /// </summary>
        Hl7.Fhir.Model.CapabilityStatement.ResourceComponent GetResourceDefinition();

        /// <summary>
        /// Get the structure definition for this profile
        /// </summary>
        StructureDefinition GetStructureDefinition();
    }
}

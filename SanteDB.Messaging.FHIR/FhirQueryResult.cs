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
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// Query result form a FHIR query
    /// </summary>
    public class FhirQueryResult
    {

        /// <summary>
        /// Create a new fhir query result
        /// </summary>
        /// <param name="resourceType"></param>
        public FhirQueryResult(string resourceType)
        {
            this.ResourceType = resourceType;
            this.Results = new List<Bundle.EntryComponent>();
        }

        /// <summary>
        /// Gets or sets the results
        /// </summary>
        public List<Bundle.EntryComponent> Results { get; set; }

        /// <summary>
        /// Gets or sets the query that initiated the action
        /// </summary>
        public FhirQuery Query { get; set; }

        /// <summary>
        /// Gets the total results
        /// </summary>
        public int TotalResults { get; set; }

        /// <summary>
        /// Resource type
        /// </summary>
        public string ResourceType { get; }

    }
}

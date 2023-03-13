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
 * Date: 2023-3-10
 */
using SanteDB.Core.Model.Query;
using System;
using System.Collections.Generic;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// Internal query structure
    /// </summary>
    public class FhirQuery
    {
        /// <summary>
        /// FHIR query
        /// </summary>
        public FhirQuery()
        {
            this.ActualParameters = new System.Collections.Specialized.NameValueCollection();
            this.QueryId = Guid.Empty;
            this.IncludeHistory = false;
            this.MinimumDegreeMatch = 1.0f;
            this.Start = 0;
            this.Quantity = 25;
        }

        /// <summary>
        /// Get the actual parameters that could be serviced
        /// </summary>
        public System.Collections.Specialized.NameValueCollection ActualParameters { get; set; }

        /// <summary>
        /// Identifies the query identifier
        /// </summary>
        public Guid QueryId { get; set; }

        /// <summary>
        /// True if the query is merely a sumary
        /// </summary>
        public bool IncludeHistory { get; set; }

        /// <summary>
        /// True if the query should include contained resource
        /// </summary>
        public bool IncludeContained { get; set; }

        /// <summary>
        /// Include resources
        /// </summary>
        public List<String> IncludeResource { get; set; }

        /// <summary>
        /// Minimum degree match.
        /// </summary>
        public float MinimumDegreeMatch { get; set; }

        /// <summary>
        /// Start result
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// The Quantity
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// The resource type
        /// </summary>
        public String ResouceType { get; }

        /// <summary>
        /// True if exact total should be counted
        /// </summary>
        public bool ExactTotal { get; set; }

        /// <summary>
        /// Apply common query resuts to the specified <paramref name="hdsiResults"/>
        /// </summary>
        public IQueryResultSet ApplyCommonQueryControls(IQueryResultSet hdsiResults, out int totalResults)
        {
            // TODO: sorting
            if (this.QueryId != Guid.Empty)
            {
                hdsiResults = hdsiResults.AsStateful(this.QueryId);
                totalResults = hdsiResults.Count(); // it is a stateful query so we can just count them (no penalty to doing this)
            }
            else if (this.ExactTotal)
            {
                totalResults = hdsiResults.Count();
            }
            else
            {
                totalResults = hdsiResults.Skip(this.Start).Take(this.Quantity + 1).Count() + this.Start;
            }

            return hdsiResults.Skip(this.Start).Take(this.Quantity);
        }
    }
}
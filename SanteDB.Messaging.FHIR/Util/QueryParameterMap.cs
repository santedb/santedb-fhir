/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using System.Linq;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Util
{

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Identifies the type of rewrite
    /// </summary>
    [XmlType(nameof(QueryParameterRewriteType), Namespace = "http://santedb.org/model/fhir")]
    public enum QueryParameterRewriteType
    {
        [XmlEnum("none")]
        None,
        [XmlEnum("concept")]
        Concept,
        [XmlEnum("identifier")]
        Identifier,
        [XmlEnum("token")]
        Token,
        [XmlEnum("reference")]
        Reference,
        [XmlEnum("tag")]
        Tag,
        [XmlEnum("string")]
        String,
        [XmlEnum("number")]
        Int,
        [XmlEnum("date")]
        Date,
        [XmlEnum("quantity")]
        Quantity,
        [XmlEnum("indicator")]
        Indicator,
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Represents a query parameter map
    /// </summary>
    [XmlType(nameof(QueryParameterMap), Namespace = "http://santedb.org/model/fhir")]
    [XmlRoot(nameof(QueryParameterMap), Namespace = "http://santedb.org/model/fhir")]
    public class QueryParameterMap
    {

        /// <summary>
        /// The type of the map
        /// </summary>
        [XmlElement("type")]
        public List<QueryParameterType> Map { get; set; }

        /// <summary>
        /// Merges two query parameter maps together
        /// </summary>
        public void Merge(QueryParameterMap map)
        {

            foreach (var itm in map.Map)
            {
                var myMapping = this.Map.FirstOrDefault(p => p.ResourceSpecified == itm.ResourceSpecified && p.Resource == itm.Resource);

                // I have a local mapping
                if (myMapping != null)
                {
                    // Remove any overridden mappings
                    myMapping.Map.RemoveAll(o => itm.Map.Any(i => i.FhirQuery == o.FhirQuery));
                    // Add overridden mappings
                    myMapping.Map.AddRange(itm.Map);
                }
                else // we just add
                {
                    this.Map.Add(itm);
                }
            }
        }
    }

    /// <summary>
    /// Represents a query parameter map
    /// </summary>
    [XmlType(nameof(QueryParameterType), Namespace = "http://santedb.org/model/fhir")]
    public class QueryParameterType
    {


        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        [XmlAttribute("resource")]
        public ResourceType Resource { get; set; }

        /// <summary>
        /// True if resource is specified
        /// </summary>
        [XmlIgnore]
        public bool ResourceSpecified { get; set; }

        /// <summary>
        /// The model type
        /// </summary>
        [XmlAttribute("model")]
        public String SourceTypeXml
        {
            get => null;
            set
            {
                var resourceType = Type.GetType(value);
                if (resourceType == null)
                {
                    this.ResourceSpecified = true;
                    var inst = Activator.CreateInstance(resourceType) as Resource;
                    inst.TryDeriveResourceType(out ResourceType rt);
                    this.Resource = rt;
                }
            }
        }

        /// <summary>
        /// Map the query parameter
        /// </summary>
        [XmlElement("map")]
        public List<QueryParameterMapProperty> Map { get; set; }

    }

    /// <summary>
    /// Represents a query parameter map 
    /// </summary>
    [XmlType(nameof(QueryParameterMapProperty), Namespace = "http://santedb.org/model/fhir")]
    public class QueryParameterMapProperty
    {

        /// <summary>
        /// The model query parameter
        /// </summary>
        [XmlAttribute("model")]
        public String ModelQuery { get; set; }

        /// <summary>
        /// The FHIR name
        /// </summary>
        [XmlAttribute("fhir")]
        public String FhirQuery { get; set; }

        /// <summary>
        /// Gets or sets the type of the fhir parmaeter
        /// </summary>
        [XmlAttribute("type")]
        public QueryParameterRewriteType FhirType { get; set; }

        /// <summary>
        /// Gets or sets the textual description of the query parameter
        /// </summary>
        [XmlAttribute("desc")]
        public String Description { get; set; }

        /// <summary>
        /// Gets the function to apply to the match
        /// </summary>
        [XmlAttribute("function")]
        public string Function { get; set; }
    }

}

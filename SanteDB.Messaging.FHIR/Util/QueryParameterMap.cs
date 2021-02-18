﻿/*
 * Portions Copyright 2019-2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE)
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
 * User: fyfej (Justin Fyfe)
 * Date: 2019-11-27
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Util
{

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
                var myMapping = this.Map.FirstOrDefault(p => p.SourceType == itm.SourceType);

                // I have a local mapping
                if (myMapping != null)
                {
                    // Remove any overridden mappings
                    myMapping.Map.RemoveAll(o => itm.Map.Any(i => i.FhirName == o.FhirName));
                    // Add overridden mappings
                    myMapping.Map.AddRange(itm.Map);
                }
                else // we just add
                    this.Map.Add(itm);

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
        /// Gets or sets the source type
        /// </summary>
        [XmlIgnore]
        public Type SourceType { get; set; }

        /// <summary>
        /// The model type
        /// </summary>
        [XmlAttribute("model")]
        public String SourceTypeXml {
            get { return this.SourceType.AssemblyQualifiedName; }
            set { this.SourceType = Type.GetType(value); }
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
        public String ModelName { get; set; }

        /// <summary>
        /// The FHIR name
        /// </summary>
        [XmlAttribute("fhir")]
        public String FhirName { get; set; }

        /// <summary>
        /// Gets or sets the type of the fhir parmaeter
        /// </summary>
        [XmlAttribute("type")]
        public String FhirType { get; set; }

        /// <summary>
        /// Gets or sets the textual description of the query parameter
        /// </summary>
        [XmlAttribute("desc")]
        public String Description { get; set; }

    }

}

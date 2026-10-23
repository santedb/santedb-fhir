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
using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Model;
using Newtonsoft.Json;
using SanteDB.Messaging.FHIR.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
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

                    // Add any restrictions on query
                    if (itm.QueryRestriction != null)
                    {
                        if (myMapping.QueryRestriction == null)
                        {
                            myMapping.QueryRestriction = itm.QueryRestriction;
                        }
                        else
                        {
                            myMapping.QueryRestriction = new FhirQueryAnyAssertion()
                            {
                                Assertion = new List<FhirQueryAssertion>()
                                {
                                    itm.QueryRestriction,
                                    myMapping.QueryRestriction
                                }
                            };
                        }
                    }
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


        /// <summary>
        /// Gets or sets the assertions
        /// </summary>
        [XmlElement("queryControl")]
        [JsonProperty("queryControl")]
        public FhirQueryAnyAssertion QueryRestriction { get; set; }
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


    /// <summary>
    /// Query control assertion base class
    /// </summary>
    [ExcludeFromCodeCoverage]
    [XmlType(nameof(FhirQueryAssertion), Namespace = "http://santedb.org/model/fhir")]
    public abstract class FhirQueryAssertion
    {

        /// <summary>
        /// Validate the <paramref name="queryToValidate"/>
        /// </summary>
        /// <param name="queryToValidate">The query to be validated against this rule</param>
        /// <returns>True if the query to validate is valid</returns>
        public abstract bool Validate(NameValueCollection queryToValidate);

    }

    /// <summary>
    /// A composite query assertion made up of other assertions
    /// </summary>
    [ExcludeFromCodeCoverage]
    [XmlType(nameof(FhirCompositeQueryAssertion), Namespace = "http://santedb.org/model/fhir")]
    public abstract class FhirCompositeQueryAssertion : FhirQueryAssertion
    {

        /// <summary>
        /// Assertions that must be present
        /// </summary>
        [XmlElement("any", typeof(FhirQueryAnyAssertion)),
          XmlElement("all", typeof(FhirQueryAllAssertion)),
          XmlElement("none", typeof(FhirQueryNoneAssertion)),
          XmlElement("parameter", typeof(FhirParameterAssertion))]
        [JsonProperty("assertion")]
        public List<FhirQueryAssertion> Assertion { get; set; }

    }

    /// <summary>
    /// Any of the assertions must be true
    /// </summary>
    public class FhirQueryAnyAssertion : FhirCompositeQueryAssertion
    {

        /// <inheritdoc/>
        public override bool Validate(NameValueCollection queryToValidate) => this.Assertion?.Any(o => o.Validate(queryToValidate)) == true;

    }

    /// <summary>
    /// All assertions must be true
    /// </summary>
    public class FhirQueryAllAssertion : FhirCompositeQueryAssertion
    {
        /// <inheritdoc/>
        public override bool Validate(NameValueCollection queryToValidate) => this.Assertion?.All(o => o.Validate(queryToValidate)) == true;

    }

    /// <summary>
    /// All assertions must be true
    /// </summary>
    public class FhirQueryNoneAssertion : FhirQueryAnyAssertion
    {
        /// <inheritdoc/>
        public override bool Validate(NameValueCollection queryToValidate) => !base.Validate(queryToValidate);

    }

    /// <summary>
    /// Parameter assertion
    /// </summary>
    public class FhirParameterAssertion : FhirQueryAssertion
    {
        /// <summary>
        /// Pattern regex
        /// </summary>
        private Regex m_patternRegex;

        /// <summary>
        /// Modifiers like :approx, :exact which are permitted (null means no restriction)
        /// </summary>
        [XmlAttribute("modifiers"), JsonProperty("modifiers")]
        public string[] RestrictModifiers { get; set; }

        /// <summary>
        /// The minimum length of the query parameter
        /// </summary>
        [XmlAttribute("minLength"), JsonProperty("minLength")]
        public int MinLength { get; set; }

        /// <summary>
        /// The maximum length of the query parameter
        /// </summary>
        [XmlAttribute("maxLength"), JsonProperty("maxLength")]
        public int MaxLength { get; set; }

        /// <summary>
        /// The maximum length is specified
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public bool MaxLengthSpecified { get; set; }

        /// <summary>
        /// Minimum length is specified
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public bool MinLengthSpecified { get; set; }

        /// <summary>
        /// The parameter that is expected to be present
        /// </summary>
        [XmlText, JsonProperty("parameter")]
        public string ParameterName { get; set; }

        /// <summary>
        /// Gets or sets the pattern the value must match
        /// </summary>
        [XmlAttribute("pattern"), JsonProperty("pattern")]
        public string Pattern { get; set; }

        /// <summary>
        /// Get the pattern regex
        /// </summary>
        private Regex GetPatternRegex()
        {
            if(String.IsNullOrEmpty(this.Pattern))
            {
                return null;
            }
            else if(this.m_patternRegex == null)
            {
                this.m_patternRegex = new Regex(this.Pattern, RegexOptions.Compiled);
            }
            return this.m_patternRegex;
        }

        /// <inheritdoc/>
        public override bool Validate(NameValueCollection queryToValidate)
        {
            // Find all keys minus modifiers
            var matchedKeys = queryToValidate.AllKeys.Where(k => k == this.ParameterName || k.StartsWith($"{this.ParameterName}:"));

            var isValid = matchedKeys.Any(); // must be present
            foreach(var k in matchedKeys)
            {
                var kParts = k.Split(':'); // modifiers

                // Ensure that the modifiers are permitted
                isValid &= kParts.Length == 1 || this.RestrictModifiers?.Any() != true || this.RestrictModifiers.Contains(kParts[1]);

                // Ensure that bounds match
                var values = queryToValidate.GetValues(k);
                isValid &= !this.MinLengthSpecified || values.All(v => v.Length > this.MinLength);
                isValid &= !this.MaxLengthSpecified || values.All(v => v.Length < this.MaxLength);
                isValid &= this.GetPatternRegex() == null || values.All(v => this.GetPatternRegex().IsMatch(v));
            }
            return isValid;
        }

    }
}

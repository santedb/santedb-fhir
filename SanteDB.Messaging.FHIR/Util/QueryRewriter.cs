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
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Util
{

    /// <summary>
    /// A class which is responsible for translating a series of Query Parmaeters to a LINQ expression
    /// to be passed to the persistence layer
    /// </summary>
    public class QueryRewriter
    {
        private static Tracer s_tracer = new Tracer(FhirConstants.TraceSourceName);

        // The query parameter map
        private static QueryParameterMap s_map;

        // Default
        private static QueryParameterType s_default;

        /// <summary>
        /// Default parameters
        /// </summary>
        private static List<SearchParamComponent> s_defaultParameters = new List<SearchParamComponent>()
            {
                new SearchParamComponent() { Name = "_count", Type = SearchParamType.Number },
                new SearchParamComponent() { Name = "_lastUpdated", Type = SearchParamType.Date },
                new SearchParamComponent() { Name = "_format", Type = SearchParamType.Token },
                new SearchParamComponent() { Name = "_offset", Type = SearchParamType.Number },
                new SearchParamComponent() { Name = "_page", Type = SearchParamType.Number },
                new SearchParamComponent() { Name = "_stateid", Type = SearchParamType.Token },
                new SearchParamComponent() { Name = "_id", Type = SearchParamType.Token },
                new SearchParamComponent() { Name = "_pretty", Type = SearchParamType.Token },
                new SearchParamComponent() { Name = "_summary", Type = SearchParamType.Token },
                new SearchParamComponent() { Name = "_total", Type = SearchParamType.Token }
            };

        /// <summary>
        /// Static CTOR
        /// </summary>
        static QueryRewriter()
        {
            OpenMapping(typeof(QueryRewriter).Assembly.GetManifestResourceStream("SanteDB.Messaging.FHIR.FhirParameterMap.xml"));
            var externMap = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FhirParameterMap.xml");

            if (File.Exists(externMap))
            {
                using (var s = File.OpenRead(externMap))
                {
                    OpenMapping(s);
                }
            }
        }

        /// <summary>
        /// Open a query mapping
        /// </summary>
        private static void OpenMapping(Stream stream)
        {
            XmlSerializer xsz = XmlModelSerializerFactory.Current.CreateSerializer(typeof(QueryParameterMap));

            if (s_map == null)
            {
                s_map = xsz.Deserialize(stream) as QueryParameterMap;
            }
            else
            {
                // Merge
                var map = xsz.Deserialize(stream) as QueryParameterMap;
                s_map.Merge(map);
            }

            s_default = s_map.Map.FirstOrDefault(o => !o.ResourceSpecified);
        }

        /// <summary>
        /// Get a list of search parameters
        /// </summary>
        public static IEnumerable<SearchParamComponent> GetSearchParams<TFhirResource, TModelType>()
        {

            var resourceType = typeof(TFhirResource).GetResourceType();
            var map = s_map.Map.FirstOrDefault(o => resourceType.HasValue ? resourceType.Value == o.Resource : !o.ResourceSpecified);

            if (map == null)
            {
                return s_defaultParameters;
            }
            else
            {
                return map.Map.Select(o => new SearchParamComponent()
                {
                    Name = o.FhirQuery,
                    Type = MapFhirParameterType<TModelType>(o.FhirType, o.ModelQuery),
                    Documentation = new Markdown(o.Description),
                    Definition = $"/Profile/SanteDB#search-{map.Resource}.{o.FhirQuery}"
                }).Union(s_defaultParameters);
            }
        }

        /// <summary>
        /// Add search parameters
        /// </summary>
        public static void AddSearchParam<TFhirResource>(String fhirQueryParameter, String hdsiQueryParmeter, QueryParameterRewriteType type)
        {
            var resourceType = typeof(TFhirResource).GetResourceType();
            var mapConfig = s_map.Map.FirstOrDefault(o => resourceType.HasValue ? resourceType.Value == o.Resource : !o.ResourceSpecified);
            if (mapConfig == null)
            {
                mapConfig = new QueryParameterType() { Resource = resourceType.Value, ResourceSpecified = resourceType.HasValue };
                s_map.Map.Add(mapConfig);
            }

            // parm config
            var parmConfig = mapConfig.Map.FirstOrDefault(o => o.FhirQuery == fhirQueryParameter);
            if (parmConfig == null)
            {
                parmConfig = new QueryParameterMapProperty()
                {
                    FhirQuery = fhirQueryParameter,
                    ModelQuery = hdsiQueryParmeter,
                    FhirType = type
                };
            }
            else
            {
                parmConfig.ModelQuery = hdsiQueryParmeter;
                parmConfig.FhirType = type;
            }
            mapConfig.Map.Add(parmConfig);
        }

        /// <summary>
        /// Merge parameter configuration
        /// </summary>
        public static void MergeParamConfig(QueryParameterMap newMap)
        {
            s_map.Merge(newMap);
        }

        /// <summary>
        /// Remove the parameter configuration
        /// </summary>
        public static void RemoveParamConfig<TFhirResource>(String fhirQuery)
        {
            var resourceType = typeof(TFhirResource).GetResourceType();
            var mapConfig = s_map.Map.FirstOrDefault(o => resourceType.HasValue ? resourceType.Value == o.Resource : !o.ResourceSpecified);
            if (mapConfig == null)
            {
                mapConfig.Map.RemoveAll(o => o.FhirQuery == fhirQuery);
            }
        }
        /// <summary>
        /// Map FHIR parameter type
        /// </summary>
        private static SearchParamType MapFhirParameterType<TModelType>(QueryParameterRewriteType type, string definition)
        {
            switch (type)
            {
                case QueryParameterRewriteType.Concept:
                case QueryParameterRewriteType.Identifier:
                case QueryParameterRewriteType.Token:
                    return SearchParamType.Token;
                case QueryParameterRewriteType.Reference:
                    return SearchParamType.Reference;
                case QueryParameterRewriteType.Indicator:
                    return SearchParamType.Token;
                default:
                    try
                    {


                        switch (GetQueryType<TModelType>(definition).StripNullable().Name)
                        {
                            case "String":
                                return SearchParamType.String;
                            case "Uri":
                                return SearchParamType.Uri;
                            case "Int32":
                            case "Int64":
                            case "Int16":
                            case "Double":
                            case "Decimal":
                            case "Float":
                                return SearchParamType.Number;
                            case "DateTime":
                            case "DateTimeOffset":
                                return SearchParamType.Date;
                            default:
                                return SearchParamType.Composite;
                        }
                    }
                    catch
                    {
                        return SearchParamType.String;
                    }
            }
        }

        /// <summary>
        /// Follows the specified query definition and determines the type
        /// </summary>
        private static Type GetQueryType<TModelType>(string definition)
        {
            var pathParts = definition.Split('.');
            var scopeType = typeof(TModelType);
            foreach (var path in pathParts)
            {
                // Get actual path
                var vPath = path;
                if (vPath.Contains("["))
                {
                    vPath = vPath.Substring(0, vPath.IndexOf("["));
                }
                else if (vPath.Contains("@"))
                {
                    vPath = vPath.Substring(0, vPath.IndexOf("@"));
                }

                if (path.Contains("@")) // cast? 
                {
                    var cast = path.Substring(path.IndexOf("@") + 1);
                    scopeType = typeof(QueryExpressionParser).Assembly.GetExportedTypesSafe().FirstOrDefault(o => o.GetCustomAttribute<XmlTypeAttribute>()?.TypeName == cast);
                }
                else
                {
                    var property = scopeType.GetQueryProperty(vPath, true);
                    if (property == null)
                    {
                        return scopeType;
                    }

                    scopeType = property.PropertyType.StripGeneric();
                }
            }
            return scopeType;
        }

        /// <summary>
        /// Re-writes the FHIR query parameter to HDSI query parameter format
        /// </summary>
        /// <returns></returns>
        public static FhirQuery RewriteFhirQuery(Type fhirType, Type modelType, NameValueCollection fhirQuery, out NameValueCollection hdsiQuery)
        {
            // Try parse
            if (fhirQuery == null)
            {
                throw new ArgumentNullException(nameof(fhirQuery));
            }

            // Count and offset parameters
            int count = 0, offset = 0, page = 0;
            if (!Int32.TryParse(fhirQuery["_count"] ?? "25", out count))
            {
                throw new ArgumentException("_count");
            }

            if (!Int32.TryParse(fhirQuery["_offset"] ?? "0", out offset))
            {
                throw new ArgumentException("_offset");
            }

            if (fhirQuery["_page"] != null && Int32.TryParse(fhirQuery["_page"], out page))
            {
                offset = page * count;
            }

            Guid queryId = Guid.Empty;
            if (fhirQuery["_stateid"] != null)
            {
                queryId = Guid.Parse(fhirQuery["_stateid"]);
            }
            else if (fhirQuery["_total"] == "accurate") // to get an accurate total we have to persist query state
            {
                queryId = Guid.NewGuid();
            }
            else
            {
                queryId = Guid.Empty;
            }

            // Return new query
            FhirQuery retVal = new FhirQuery()
            {
                ActualParameters = new System.Collections.Specialized.NameValueCollection(),
                Quantity = count,
                Start = offset,
                MinimumDegreeMatch = 100,
                QueryId = queryId,
                IncludeHistory = false,
                IncludeContained = false,
                ExactTotal = fhirQuery["_total"] == "exact"
            };

            var hdsiInternal = hdsiQuery = new NameValueCollection();

            var resourceType = fhirType.GetResourceType();
            var map = s_map.Map.FirstOrDefault(o => resourceType.HasValue ? resourceType.Value == o.Resource : !o.ResourceSpecified);

            foreach (var kv in fhirQuery.AllKeys)
            {

                List<String> value = new List<string>(fhirQuery.GetValues(kv).Length);

                var parmComponents = kv.Split(':');

                // Is the name extension?
                var parmMap = map?.Map.FirstOrDefault(o => o.FhirQuery == parmComponents[0]);
                if (parmMap == null)
                {
                    parmMap = s_default.Map.FirstOrDefault(o => o.FhirQuery == parmComponents[0]);
                }

                if (parmMap == null && kv == "extension")
                {
                    parmMap = new QueryParameterMapProperty()
                    {
                        FhirQuery = "extension",
                        ModelQuery = "extension",
                        FhirType = QueryParameterRewriteType.Tag
                    };
                }
                else if (parmMap == null)
                {
                    continue;
                }

                // Valuse
                foreach (var v in fhirQuery.GetValues(kv).SelectMany(v => v.Split(',')))
                {
                    if (String.IsNullOrEmpty(v))
                    {
                        continue;
                    }

                    // Operands
                    bool chop = false;
                    string opValue = String.Empty;
                    string filterValue = v;
                    switch (parmMap.FhirType)
                    {
                        case QueryParameterRewriteType.Int:
                        case QueryParameterRewriteType.Quantity:
                        case QueryParameterRewriteType.Date:
                            if (v.Length > 2)
                            {
                                switch (v.Substring(0, 2))
                                {
                                    case "ap":
                                        chop = true;
                                        opValue = "~";
                                        break;
                                    case "gt":
                                        chop = true;
                                        opValue = ">";
                                        break;
                                    case "ge":
                                        chop = true;
                                        opValue = ">=";
                                        break;
                                    case "lt":
                                        chop = true;
                                        opValue = "<";
                                        break;
                                    case "le":
                                        chop = true;
                                        opValue = "<=";
                                        break;
                                    case "ne":
                                        chop = true;
                                        opValue = "!";
                                        break;
                                    case "eq":
                                        chop = true;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;

                    }

                    if (parmComponents.Length > 1)
                    {
                        switch (parmComponents[1])
                        {
                            case "fuzzy":
                            case "approx":
                                opValue = "";
                                filterValue = $":(approx|'{filterValue}')";
                                break;
                            case "contains":
                                opValue = "~";
                                filterValue = $"*{filterValue}*";
                                break;
                            case "exact":
                                opValue = "";
                                break;
                            case "missing":
                                filterValue = "null";
                                chop = false;
                                break;
                            default:
                                switch (parmMap.FhirType)
                                {
                                    case QueryParameterRewriteType.String: // Default string matching is wonky in FHIR but meh we can mimic it at least
                                        opValue = "~";
                                        filterValue = $"*{filterValue.Replace(' ', '?')}*";
                                        break;
                                }
                                break;
                        }
                    }

                    retVal.ActualParameters.Add(kv, filterValue);
                    value.Add(opValue + filterValue.Substring(chop ? 2 : 0));
                }

                if (value.Count(o => !String.IsNullOrEmpty(o)) == 0)
                {
                    continue;
                }

                // Apply a function
                if (!String.IsNullOrEmpty(parmMap.Function))
                {
                    value = value.Select(o => parmMap.Function.Replace("$1", o)).ToList();
                }

                // Query 
                switch (parmMap.FhirType)
                {
                    case QueryParameterRewriteType.Identifier:
                        foreach (var itm in value)
                        {
                            if (itm.Contains("|"))
                            {
                                var segs = itm.Split('|');
                                // Might be a URL
                                if (Uri.TryCreate(segs[0], UriKind.Absolute, out Uri data))
                                {
                                    var aa = ApplicationServiceContext.Current.GetService<IIdentityDomainRepositoryService>().Get(data);
                                    if (aa == null)
                                    {
                                        throw new FhirException(System.Net.HttpStatusCode.BadRequest, OperationOutcome.IssueType.NotFound, $"No authority for {data} found");
                                    }

                                    // Has an identifier
                                    if (!String.IsNullOrEmpty(segs[1]))
                                    {
                                        hdsiQuery.Add(String.Format("{0}[{1}].value", parmMap.ModelQuery, aa.DomainName), segs[1]);
                                    }
                                    else
                                    {
                                        hdsiQuery.Add(String.Format("{0}.domain", parmMap.ModelQuery), aa.Key.ToString());
                                    }
                                }
                                else
                                {
                                    hdsiQuery.Add(String.Format("{0}[{1}].value", parmMap.ModelQuery, segs[0]), segs[1]);
                                }
                            }
                            else
                            {
                                hdsiQuery.Add(parmMap.ModelQuery + ".value", itm);
                            }
                        }
                        break;
                    case QueryParameterRewriteType.Quantity:
                        foreach (var itm in value)
                        {
                            var queryPath = parmMap.ModelQuery;
                            if (queryPath.Equals("value"))
                            {
                                queryPath = String.Empty;
                            }
                            else
                            {
                                queryPath += ".";
                            }

                            if (itm.Contains("|"))
                            {
                                var segs = itm.Split('|');
                                // Might be a URL
                                if (segs.Length == 3)
                                {
                                    var uom = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>().GetConceptByReferenceTerm(segs[2], segs[1]);
                                    if (uom == null)
                                    {
                                        throw new FhirException(System.Net.HttpStatusCode.BadRequest, OperationOutcome.IssueType.NotFound, $"No concept for {itm} found");
                                    }

                                    hdsiQuery.Add(String.Format("{0}unitOfMeasure", queryPath), uom.Key.ToString());
                                }
                                else
                                {
                                    throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.INVALID_FORMAT, itm, "val|system|code"));
                                }
                            }
                            hdsiQuery.Add(queryPath + "value", itm);
                        }
                        break;
                    case QueryParameterRewriteType.Indicator:
                        var mq = parmMap.ModelQuery.ParseQueryString();
                        foreach (var itm in mq.AllKeys)
                        {
                            Array.ForEach(mq.GetValues(itm), v => hdsiInternal.Add(itm, v));
                        }
                        break;
                    case QueryParameterRewriteType.Concept:
                        foreach (var itm in value)
                        {
                            if (itm.Contains("|"))
                            {
                                var segs = itm.Split('|');

                                string codeSystemUri = segs[0];
                                Core.Model.DataTypes.CodeSystem codeSystem = null;

                                if (codeSystemUri.StartsWith("urn:oid:"))
                                {
                                    codeSystemUri = codeSystemUri.Substring(8);
                                    codeSystem = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.DataTypes.CodeSystem>>().Find(o => o.Oid == codeSystemUri).FirstOrDefault();
                                }
                                else if (codeSystemUri.StartsWith("urn:") || codeSystemUri.StartsWith("http:"))
                                {
                                    codeSystem = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.DataTypes.CodeSystem>>().Find(o => o.Url == codeSystemUri).FirstOrDefault();
                                }
                                else
                                {
                                    codeSystem = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.DataTypes.CodeSystem>>().Find(o => o.Name == codeSystemUri).FirstOrDefault();
                                }

                                s_tracer.TraceInfo("Have translated FHIR domain {0} to {1}", codeSystemUri, codeSystem?.Name);

                                if (codeSystem != null)
                                {
                                    hdsiQuery.Add(String.Format("{0}.referenceTerm[{1}].term.mnemonic", parmMap.ModelQuery, codeSystem.Name), segs[1]);
                                }
                                else
                                {
                                    hdsiQuery.Add(String.Format("{0}.mnemonic", parmMap.ModelQuery), segs[1]);
                                }
                            }
                            else
                            {
                                hdsiQuery.Add(parmMap.ModelQuery + ".referenceTerm.term.mnemonic", itm);
                            }
                        }
                        break;
                    case QueryParameterRewriteType.Reference:
                        foreach (var itm in value)
                        {
                            if (itm.Contains("/"))
                            {
                                var segs = itm.Split('/');
                                hdsiQuery.Add(parmMap.ModelQuery, segs[1]);
                            }
                            else
                            {
                                hdsiQuery.Add(parmMap.ModelQuery, itm);
                            }
                        }
                        break;
                    case QueryParameterRewriteType.Tag:
                        foreach (var itm in value)
                        {
                            if (itm.Contains("|"))
                            {
                                var segs = itm.Split('|');
                                hdsiQuery.Add(String.Format("{0}[{1}].value", parmMap.ModelQuery, segs[0]), segs[1]);
                            }
                            else
                            {
                                hdsiQuery.Add(parmMap.ModelQuery, itm);
                            }
                        }
                        break;

                    default:
                        value.ForEach(o => hdsiInternal.Add(parmMap.ModelQuery, o));
                        break;
                }
            }

            return retVal;
        }
    }
}

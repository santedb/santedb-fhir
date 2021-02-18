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
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Serialization;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Util
{

    /// <summary>
    /// SanteDB FHIR query
    /// </summary>
    public class SanteDBFhirQuery<TModel> : FhirQuery
    {

        /// <summary>
        /// Query expression
        /// </summary>
        public LambdaExpression QueryExpression { get; set; }

        /// <summary>
        /// Gets or sets the composed query
        /// </summary>
        public Expression<Func<TModel, bool>> ToPredicate()
        {
            return System.Linq.Expressions.Expression.Lambda<Func<TModel, bool>>(this.QueryExpression.Body, this.QueryExpression.Parameters);
        }
    }

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
        /// Static CTOR
        /// </summary>
        static QueryRewriter()
        {
            OpenMapping(typeof(QueryRewriter).Assembly.GetManifestResourceStream("SanteDB.Messaging.FHIR.FhirParameterMap.xml"));
            var externMap = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "FhirParameterMap.xml");

            if (File.Exists(externMap))
                using (var s = File.OpenRead(externMap))
                    OpenMapping(s);
        }

        /// <summary>
        /// Open a query mapping
        /// </summary>
        private static void OpenMapping(Stream stream)
        {
            XmlSerializer xsz = XmlModelSerializerFactory.Current.CreateSerializer(typeof(QueryParameterMap));

            if (s_map == null)
                s_map = xsz.Deserialize(stream) as QueryParameterMap;
            else
            {
                // Merge
                var map = xsz.Deserialize(stream) as QueryParameterMap;
                s_map.Merge(map);
            }

            s_default = s_map.Map.FirstOrDefault(o => o.SourceType == typeof(Resource));
        }

        /// <summary>
        /// Get a list of search parameters
        /// </summary>
        public static IEnumerable<SearchParamComponent> GetSearchParams<TFhirResource, TModelType>()
        {
            var map = s_map.Map.FirstOrDefault(o => o.SourceType == typeof(TFhirResource));
            if (map == null) return null;
            else
                return map.Map.Select(o => new SearchParamComponent()
                {
                    Name = o.FhirName,
                    Type = MapFhirParameterType<TModelType>(o.FhirType, o.ModelName),
                    Documentation = new Markdown(o.Description),
                    Definition = $"/Profile/SanteDB#search-{map.SourceType.Name}.{o.FhirName}"
                });

        }

        /// <summary>
        /// Map FHIR parameter type
        /// </summary>
        private static SearchParamType MapFhirParameterType<TModelType>(string type, string definition)
        {
            switch (type)
            {
                case "concept":
                case "identifier":
                    return SearchParamType.Token;
                case "reference":
                    return SearchParamType.Reference;
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
                    vPath = vPath.Substring(0, vPath.IndexOf("["));
                else if (vPath.Contains("@"))
                    vPath = vPath.Substring(0, vPath.IndexOf("@"));

                if (path.Contains("@")) // cast? 
                {
                    var cast = path.Substring(path.IndexOf("@") + 1);
                    scopeType = typeof(QueryExpressionParser).Assembly.ExportedTypes.FirstOrDefault(o => o.GetCustomAttribute<XmlTypeAttribute>()?.TypeName == cast);
                }
                else
                {
                    var property = scopeType.GetQueryProperty(vPath, true);
                    if (property == null)
                        return scopeType;
                    scopeType = property.PropertyType.StripGeneric();
                }
            }
            return scopeType;
        }

        /// <summary>
        /// Re-writes the FHIR query parameter to HDSI query parameter format
        /// </summary>
        /// <returns></returns>
        public static FhirQuery RewriteFhirQuery<TFhirResource, TModelType>(System.Collections.Specialized.NameValueCollection fhirQuery, out NameValueCollection hdsiQuery)
        {
            // Try parse
            if (fhirQuery == null) throw new ArgumentNullException(nameof(fhirQuery));

            // Count and offset parameters
            int count = 0, offset = 0, page = 0;
            if (!Int32.TryParse(fhirQuery["_count"] ?? "100", out count))
                throw new ArgumentException("_count");
            if (!Int32.TryParse(fhirQuery["_offset"] ?? "0", out offset))
                throw new ArgumentException("_offset");
            if (fhirQuery["_page"] != null && Int32.TryParse(fhirQuery["_page"], out page))
                offset = page * count;

            Guid queryId = Guid.Empty;
            if (fhirQuery["_stateid"] != null)
                queryId = Guid.Parse(fhirQuery["_stateid"]);
            else
                queryId = Guid.NewGuid();

            // Return new query
            FhirQuery retVal = new FhirQuery()
            {
                ActualParameters = new System.Collections.Specialized.NameValueCollection(),
                Quantity = count,
                Start = offset,
                MinimumDegreeMatch = 100,
                QueryId = queryId,
                IncludeHistory = false,
                IncludeContained = false
            };

            hdsiQuery = new NameValueCollection();

            var map = s_map.Map.FirstOrDefault(o => o.SourceType == typeof(TFhirResource));

            foreach (var kv in fhirQuery.AllKeys)
            {

                List<String> value = new List<string>(fhirQuery.GetValues(kv).Length);

                var parmComponents = kv.Split(':');

                // Is the name extension?
                var parmMap = map?.Map.FirstOrDefault(o => o.FhirName == parmComponents[0]);
                if (parmMap == null)
                    parmMap = s_default.Map.FirstOrDefault(o => o.FhirName == parmComponents[0]);
                if (parmMap == null && kv == "extension")
                    parmMap = new QueryParameterMapProperty()
                    {
                        FhirName = "extension",
                        ModelName = "extension",
                        FhirType = "tag"
                    };
                else if (parmMap == null)
                    continue;

                // Valuse
                foreach (var v in fhirQuery.GetValues(kv))
                {
                    if (String.IsNullOrEmpty(v)) continue;

                    // Operands
                    bool chop = false;
                    string opValue = String.Empty;
                    string filterValue = v;
                    if (v.Length > 2)
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

                    if(parmComponents.Length > 1)
                        switch(parmComponents[1])
                        {
                            case "contains":
                                opValue = "~";
                                filterValue = $"*{filterValue}*";
                                break;
                            case "missing":
                                filterValue = "null";
                                chop = false;
                                break;
                        }

                    retVal.ActualParameters.Add(kv, filterValue);
                    value.Add(opValue + filterValue.Substring(chop ? 2 : 0));
                }

                if (value.Count(o => !String.IsNullOrEmpty(o)) == 0)
                    continue;

                // Query 
                switch (parmMap.FhirType)
                {
                    case "identifier":
                        foreach (var itm in value)
                        {
                            if (itm.Contains("|"))
                            {
                                var segs = itm.Split('|');
                                // Might be a URL
                                if (Uri.TryCreate(segs[0], UriKind.Absolute, out Uri data))
                                {
                                    var aa = ApplicationServiceContext.Current.GetService<IAssigningAuthorityRepositoryService>().Get(data);
                                    hdsiQuery.Add(String.Format("{0}[{1}].value", parmMap.ModelName, aa.DomainName), segs[1]);
                                }
                                else
                                    hdsiQuery.Add(String.Format("{0}[{1}].value", parmMap.ModelName, segs[0]), segs[1]);

                            }
                            else
                                hdsiQuery.Add(parmMap.ModelName + ".value", itm);
                        }
                        break;
                    case "concept":
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
                                    codeSystem = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.DataTypes.CodeSystem>>().Find(o => o.Url == codeSystemUri).FirstOrDefault();
                                else
                                    codeSystem = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.DataTypes.CodeSystem>>().Find(o => o.Name == codeSystemUri).FirstOrDefault();


                                s_tracer.TraceInfo("Have translated FHIR domain {0} to {1}", codeSystemUri, codeSystem?.Name);

                                if (codeSystem != null)
                                    hdsiQuery.Add(String.Format("{0}.referenceTerm[{1}].term.mnemonic", parmMap.ModelName, codeSystem.Name), segs[1]);
                                else
                                    hdsiQuery.Add(String.Format("{0}.mnemonic", parmMap.ModelName), segs[1]);
                            }
                            else
                                hdsiQuery.Add(parmMap.ModelName + ".referenceTerm.term.mnemonic", itm);
                        }
                        break;
                    case "reference":
                        foreach (var itm in value)
                        {
                            if (itm.Contains("/"))
                            {
                                var segs = itm.Split('/');
                                hdsiQuery.Add(parmMap.ModelName, segs[1]);
                            }
                            else
                                hdsiQuery.Add(parmMap.ModelName, itm);
                        }
                        break;
                    case "tag":
                        foreach (var itm in value)
                        {
                            if (itm.Contains("|"))
                            {
                                var segs = itm.Split('|');
                                hdsiQuery.Add(String.Format("{0}[{1}].value", parmMap.ModelName, segs[0]), segs[1]);
                            }
                            else
                                hdsiQuery.Add(parmMap.ModelName, itm);
                        }
                        break;

                    default:
                        hdsiQuery.Add(parmMap.ModelName, value);
                        break;
                }
            }

            return retVal;
        }
    }
}

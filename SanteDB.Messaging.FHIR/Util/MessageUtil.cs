/*
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
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Represents a series of message processing utilities
    /// </summary>
    public static class MessageUtil
    {

        // Escape characters
        private static readonly Dictionary<String, String> s_escapeChars = new Dictionary<string, string>()
        {
            { "\\,", "\\#002C" },
            { "\\$", "\\#0024" },
            { "\\|", "\\#007C" },
            { "\\\\", "\\#005C" },
        };

        //Base path
        private static string s_basePath = String.Empty;

        /// <summary>
        /// Escape a string
        /// </summary>
        public static String Escape(String str)
        {
            string retVal = str;
            foreach (var itm in s_escapeChars)
                retVal = retVal.Replace(itm.Key, itm.Value);
            return retVal;
        }

        /// <summary>
        /// Un-escape a string
        /// </summary>
        public static string UnEscape(String str)
        {
            string retVal = str;
            foreach (var itm in s_escapeChars)
                retVal = retVal.Replace(itm.Value, itm.Key);
            return retVal;
        }

        /// <summary>
        /// Create a feed
        /// </summary>
        public static Bundle CreateBundle(FhirQueryResult result)
        {

            Bundle retVal = new Bundle();
            FhirQueryResult queryResult = result as FhirQueryResult;
            retVal.Id = String.Format("urn:uuid:{0}", Guid.NewGuid());

            // Make the Self uri
            String baseUri = $"{result.ResourceType}";

            if (queryResult.Query != null)
            {
                int pageNo = queryResult == null || queryResult.Query.Quantity == 0 ? 0 : queryResult.Query.Start / queryResult.Query.Quantity,
                    nPages = queryResult == null || queryResult.Query.Quantity == 0 ? 1 : (queryResult.TotalResults / queryResult.Query.Quantity);
                retVal.Type = Bundle.BundleType.Searchset;
                var queryUri = baseUri + "?";

                // Self uri
                if (queryResult != null)
                {
                    for (int i = 0; i < queryResult.Query.ActualParameters.Count; i++)
                        foreach (var itm in queryResult.Query.ActualParameters.GetValues(i))
                            switch (queryResult.Query.ActualParameters.GetKey(i))
                            {
                                case "_stateid":
                                case "_page":
                                case "_count":
                                    break;
                                default:
                                    queryUri += string.Format("{0}={1}&", queryResult.Query.ActualParameters.GetKey(i), itm);
                                    break;
                            }

                    if (!baseUri.Contains("_stateid=") && queryResult.Query.QueryId != Guid.Empty)
                        queryUri += String.Format("_stateid={0}&", queryResult.Query.QueryId);
                }

               
                // Self URI
                if (queryResult != null && queryResult.TotalResults > queryResult.Results.Count)
                {
                    retVal.Link.Add(new Bundle.LinkComponent() { Url = $"{queryUri}_page={pageNo}&_count={queryResult?.Query.Quantity ?? 100}", Relation = "self" });
                    if (pageNo > 0)
                    {
                        retVal.Link.Add(new Bundle.LinkComponent() { Url = $"{queryUri}_page=0&_count={queryResult?.Query.Quantity ?? 100}", Relation = "first" });
                        retVal.Link.Add(new Bundle.LinkComponent() { Url = $"{queryUri}_page={pageNo - 1}&_count={queryResult?.Query.Quantity ?? 100}", Relation = "previous" });
                    }
                    if (pageNo <= nPages)
                    {
                        retVal.Link.Add(new Bundle.LinkComponent() { Url = $"{queryUri}_page={pageNo + 1}&_count={queryResult?.Query.Quantity ?? 100}", Relation = "next" });
                        retVal.Link.Add(new Bundle.LinkComponent() { Url = $"{queryUri}_page={nPages}&_count={queryResult?.Query.Quantity ?? 100}", Relation = "last" });
                    }
                }
                else
                    retVal.Link.Add(new Bundle.LinkComponent() { Url = queryUri, Relation = "self" });
            }
            else //History 
            {
                // History type
                retVal.Type = Bundle.BundleType.History;
                // Self URI
                retVal.Link.Add(new Bundle.LinkComponent() { Url = $"{baseUri}/_history", Relation = "self" });

            }
            // Updated
            retVal.Timestamp = DateTime.Now;
            //retVal.Generator = "MARC-HI Service Core Framework";

            // HACK: Remove me
            if(queryResult != null)
                retVal.Total = queryResult.TotalResults;

            
            // Results
            if (result.Results != null)
            {
                retVal.Entry = result.Results.Select(itm =>
                {
                    var feedResult = new Bundle.EntryComponent(); //new Bundleentry(String.Format("{0} id {1} version {2}", itm.GetType().Name, itm.Id, itm.VersionId), null ,resourceUrl);
                    feedResult.Link = new List<Bundle.LinkComponent>() { new Bundle.LinkComponent() { Relation = "_self", Url = itm.HasVersionId ? $"{itm.ResourceType}/{itm.Id}/_history/{itm.VersionId}" : $"{itm.ResourceType}/{itm.Id}"  } };
                    feedResult.FullUrl = $"{GetBaseUri()}/{itm.ResourceType}/{itm.Id}";

                    // TODO: Generate the text with a util


                    // Add confidence if the attribute permits
                    var confidence = itm.Annotations(typeof(ITag)).OfType<ITag>().FirstOrDefault(t => t.TagKey == "$conf");
                    if (confidence != null)
                        feedResult.Search = new Bundle.SearchComponent()
                        {
                            Score = Decimal.Parse(confidence.Value)
                        };

                    feedResult.Resource = itm;
                    return feedResult;
                }).ToList();
            }

            // Outcome
            //if (result.Details.Count > 0 || result.Issues != null && result.Issues.Count > 0)
            //{
            //    var outcome = CreateOutcomeResource(result);
            //    retVal.ElementExtensions.Add(outcome, XmlModelSerializerFactory.Current.CreateSerializer(typeof(OperationOutcome)));
            //    retVal.Description = new TextSyndicationContent(outcome.Text.ToString(), TextSyndicationContentKind.Html);
            //}
            return retVal;

        }

        /// <summary>
        /// Sets the base location
        /// </summary>
        internal static void SetBaseLocation(string absolutePath)
        {
            s_basePath = absolutePath;
        }

        /// <summary>
        /// Get BASE URI
        /// </summary>
        internal static string GetBaseUri()
        {
            return s_basePath;
        }
    }
}

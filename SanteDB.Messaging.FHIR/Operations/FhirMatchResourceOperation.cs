﻿/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-5
 */
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Operations
{
    /// <summary>
    /// A FHIR operation handler which executs the matching logic of the CDR
    /// </summary>
    public class FhirMatchResourceOperation : IFhirOperationHandler
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FhirMatchResourceOperation));

        // Configuration
        private ResourceMergeConfigurationSection m_configuration;

        /// <summary>
        /// Configurations for the merge configuration
        /// </summary>
        public FhirMatchResourceOperation()
        {
            this.m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<ResourceMergeConfigurationSection>();
        }

        /// <summary>
        /// Get the parameters
        /// </summary>
        public IDictionary<string, FHIRAllTypes> Parameters => new Dictionary<String, FHIRAllTypes>()
        {
            { "resource", FHIRAllTypes.Any  },
            { "onlyCertainMatches", FHIRAllTypes.Boolean },
            { "count", FHIRAllTypes.Integer }
        };

        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        public string Name => "match";

        /// <summary>
        /// Gets the URI where this operation is defined
        /// </summary>
        public Uri Uri => new Uri("OperationDefinition/match", UriKind.Relative);

        /// <summary>
        /// Applies to which resources (all of them)
        /// </summary>
        public ResourceType[] AppliesTo => new ResourceType[]
        {
            ResourceType.Patient,
            ResourceType.Organization,
            ResourceType.Location
        };

        /// <summary>
        /// True if the operation is a get
        /// </summary>
        public bool IsGet => false;

        /// <summary>
        /// Invoke the specified operation
        /// </summary>
        public Resource Invoke(Parameters parameters)
        {

            // Validate parameters
            var resource = parameters.Parameter.FirstOrDefault(o => o.Name == "resource")?.Resource;
            var onlyCertainMatches = parameters.Parameter.FirstOrDefault(o => o.Name == "onlyCertainMatches")?.Value as FhirBoolean ?? new FhirBoolean(false);
            var count = parameters.Parameter.FirstOrDefault(o => o.Name == "count")?.Value as Integer;

            if (resource == null)
                throw new ArgumentNullException("Missing resource parameter");

            // Execute the logic
            try
            {
                // First we want to get the handler, as this will tell us the SanteDB CDR type 
                if (!resource.TryDeriveResourceType(out ResourceType rt))
                {
                    throw new InvalidOperationException($"Operation on {resource.TypeName} not supported");
                }

                var handler = FhirResourceHandlerUtil.GetResourceHandler(rt) as IFhirResourceMapper;
                if (handler == null)
                {
                    throw new NotSupportedException($"Operation on {rt} not supported");
                }

                var matchService = ApplicationServiceContext.Current.GetService<IRecordMatchingService>();
                if (matchService == null)
                    throw new NotSupportedException($"No match service is registered on this CDR");

                // Now we want to map the from FHIR to our CDR model
                var modelInstance = handler.MapToModel(resource);
                this.m_tracer.TraceInfo("Will execute match on {0}", modelInstance);

                // Next run the match
                var configurationName = RestOperationContext.Current.IncomingRequest.QueryString["_configurationName"];
                IEnumerable<IRecordMatchResult> results = null;
                var mergeService = ApplicationServiceContext.Current.GetService(typeof(IRecordMergingService<>).MakeGenericType(handler.CanonicalType)) as IRecordMergingService;

                if (!String.IsNullOrEmpty(configurationName))
                {
                    results = matchService.Match(modelInstance, configurationName, mergeService?.GetIgnoredKeys(modelInstance.Key.Value)).ToArray();
                }
                else // use the configured option
                {
                    var configBase = this.m_configuration.ResourceTypes.FirstOrDefault(o => o.ResourceType.Type == modelInstance.GetType());
                    if (configBase == null)
                    {
                        throw new InvalidOperationException($"No resource merge configuration for {modelInstance.GetType()} available. Use either ?_configurationName parameter to add a ResourceMergeConfigurationSection to your configuration file");
                    }

                    results = configBase.MatchConfiguration.SelectMany(o => matchService.Match(modelInstance, o.MatchConfiguration, mergeService?.GetIgnoredKeys(modelInstance.Key.Value))).ToArray();
                }

                // Only certain matches
                if (onlyCertainMatches?.Value == true)
                    results = results.Where(o => o.Classification == RecordMatchClassification.Match);


                // Iterate through the resources and convert them to FHIR
                // Grouping by the ID of the candidate and selecting the highest rated match
                var distinctResults = results.GroupBy(o => o.Record.Key).Select(o => o.OrderByDescending(m => m.Strength).First());


                // Next we want to convert to FHIR
                var retVal = new Bundle()
                {
                    Id = $"urn:uuid:{Guid.NewGuid()}",
                    Entry = distinctResults.Take((int?)count.Value ?? 10).Select(o => this.ConvertMatchResult(o, handler)).ToList(),
                    Meta = new Meta()
                    {
                        LastUpdated = DateTimeOffset.Now
                    },
                    Type = Bundle.BundleType.Searchset,
                    Total = distinctResults.Count(),
                };
                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Error running match on {resource.TypeName} - {e}");
                throw new Exception($"Error running match operation on {resource.TypeName}", e);
            }
        }

        /// <summary>
        /// Convert match result <paramref name="matchResult"/> to a FHIR resource
        /// </summary>
        private Bundle.EntryComponent ConvertMatchResult(IRecordMatchResult matchResult, IFhirResourceMapper mapper)
        {
            var result = mapper.MapToFhir(matchResult.Record);

            var resultExtension = matchResult.Vectors.Select(o => new Extension()
            {
                Url = "http://santedb.org/fhir/StructureDefinition/match-attribute",
                Value = new FhirString($"{o.Name} = {o.Score:0.0#}")
            });

            // Now publish search data
            return new Bundle.EntryComponent()
            {
                FullUrl = $"{MessageUtil.GetBaseUri()}/{result.TypeName}/{result.Id}/_history/{result.VersionId}",
                Resource = result,
                Search = new Bundle.SearchComponent()
                {
                    Mode = Bundle.SearchEntryMode.Match,
                    Score = (decimal)matchResult.Strength,
                    Extension = new List<Extension>(resultExtension)
                    {
                        new Extension()
                        {
                            Url = "http://hl7.org/fhir/StructureDefinition/match-grade",
                            Value = new Code(matchResult.Classification == RecordMatchClassification.Match ? "certain" : matchResult.Classification == RecordMatchClassification.Probable ? "possible" : "certainly-not")
                        },
                        new Extension()
                        {
                            Url = "http://santedb.org/fhir/StructureDefinition/match-method",
                            Value = new Code(matchResult.Method.ToString())
                        },
                        new Extension()
                        {
                            Url = "http://santedb.org/fhir/StructureDefinition/match-score",
                            Value = new FhirDecimal((decimal)matchResult.Score)
                        }
                    }
                }
            };
        }
    }
}

/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2025-1-12
 */
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.BI.Util;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.PubSub;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Implementation of a FHIR resource handler that can interact with the measure definitions found in the KPI definitions
    /// </summary>
    public class MeasureResourceHandler : IFhirResourceHandler
    {
        private readonly IBiMetadataRepository m_biMetadataRepository;
        private readonly ILocalizationService m_localizationService;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(MeasureResourceHandler));

        /// <summary>
        /// DI ctor
        /// </summary>
        public MeasureResourceHandler(IBiMetadataRepository biMetadataRepository, ILocalizationService localizationService)
        {
            this.m_biMetadataRepository = biMetadataRepository;
            this.m_localizationService = localizationService;
        }

        /// <inheritdoc/>
        public ResourceType ResourceType => ResourceType.Measure;

        /// <inheritdoc/>
        public Resource Create(Resource target, TransactionMode mode)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public Resource Delete(string id, TransactionMode mode)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public CapabilityStatement.ResourceComponent GetResourceDefinition()
        {
          
            return new ResourceComponent()
            {
                ConditionalCreate = false,
                ConditionalUpdate = false,
                ConditionalDelete = ConditionalDeleteStatus.NotSupported,
                ReadHistory = false,
                UpdateCreate = false,
                Versioning = ResourceVersionPolicy.NoVersion,
                Interaction = new TypeRestfulInteraction[]
                {
                    TypeRestfulInteraction.Read,
                    TypeRestfulInteraction.SearchType,
                }.Select(o => new ResourceInteractionComponent() { Code = o }).ToList(),
                SearchParam = QueryRewriter.GetSearchParams<Measure, BiIndicatorDefinition>().ToList(),
                Type = ResourceType.Measure,
                Profile = $"/StructureDefinition/SanteDB/_history/{Assembly.GetEntryAssembly().GetName().Version}"
            };
        }

        /// <inheritdoc/>
        public StructureDefinition GetStructureDefinition()
        {
            return StructureDefinitionUtil.GetStructureDefinition(typeof(Measure), false);
        }

        /// <inheritdoc/>
        public Bundle History(string id)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public Bundle Query(NameValueCollection parameters)
        {

            if (parameters == null)
            {
                throw new ArgumentNullException(ErrorMessages.ARGUMENT_NULL);
            }

            var query = QueryRewriter.RewriteFhirQuery(typeof(Measure), typeof(BiIndicatorDefinition), parameters, out var hdsiQuery);
           
            // Do the query
            var predicate = QueryExpressionParser.BuildLinqExpression<BiIndicatorDefinition>(hdsiQuery);
            IQueryResultSet hdsiResults = this.m_biMetadataRepository.Query<BiIndicatorDefinition>(predicate);
            var results = query.ApplyCommonQueryControls(hdsiResults, out int totalResults).OfType<BiIndicatorDefinition>();

            var auth = AuthenticationContext.Current.Principal;
            // Return FHIR query result
            var retVal = new FhirQueryResult(nameof(Measure))
            {
                Results = results.ToArray().AsParallel().Select(o =>
                {
                    using (AuthenticationContext.EnterContext(auth))
                    {
                        return new Bundle.EntryComponent()
                        {
                            Resource = this.MapToFhir(o),
                            Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Match }
                        };
                    }
                }).ToList(),
                Query = query,
                TotalResults = totalResults
            };
            return MessageUtil.CreateBundle(retVal, Bundle.BundleType.Searchset);

        }

        /// <summary>
        /// Map the BiIndicatorDefinition to a Measure
        /// </summary>
        private Measure MapToFhir(BiIndicatorDefinition definition)
        {
            definition = BiUtils.ResolveRefs(definition);
            var retVal = new Measure()
            {
                Id = definition.Id, 
                Url = $"http://santedb.org/bi/indicator/{definition.Id}",
                Identifier = new List<Identifier>()
                {
                    new Identifier("http://santedb.org/bi/indicator", definition.Id)
                },
                Version = definition.MetaData?.Version,
                Name = definition.Name,
                Title = this.m_localizationService.GetString(definition.Label),
                Subtitle = definition.Name,
                Description = new Markdown(definition.MetaData?.Annotation.Body.ToString()),
                Status = definition.StatusSpecified ? definition.Status == BiDefinitionStatus.Draft || definition.Status == BiDefinitionStatus.InReview ? PublicationStatus.Draft : definition.Status == BiDefinitionStatus.Deprecated || definition.Status == BiDefinitionStatus.Obsolete ? PublicationStatus.Retired : PublicationStatus.Active : PublicationStatus.Unknown,
                Experimental = definition.Status == BiDefinitionStatus.InReview,
                Publisher = String.Join(", ", definition.MetaData?.Authors.ToArray() ?? new string[] { "SYSTEM" }),
                Meta = new Meta()
                {
                    Security = definition.MetaData?.Demands?.Select(o => new Coding(FhirConstants.SecurityPolicySystem, o)).ToList(),
                    LastUpdated = definition.MetaData?.LastModified
                },
                Scoring = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-scoring", definition.Measures.Any(m=>m.Computation.Any(c=>c.GetType() == typeof(BiMeasureComputationScore))) ? "continuous-variable" : "proportion")
            };

            if (definition.Identifier.Any())
            {
                
                retVal.Identifier.AddRange(definition.Identifier.Select(o=> new Identifier(o.System, o.Value)));
            }

            retVal.Extension.Add(new Extension("http://santedb.org/fhir/bi/Measure/periodicity", new FhirString(definition.Period.Name)));
            var mapper = FhirResourceHandlerUtil.GetMappersFor(definition.Subject.ResourceType).First();
            retVal.Subject = retVal.Subject ?? new CodeableConcept(mapper.ResourceType.GetSystem(), mapper.ResourceType.GetLiteral()); // FHIR R4B does not support adding subjects to the group

            retVal.Group = new List<Measure.GroupComponent>();

            foreach (var measure in definition.Measures)
            {
                var group = new Measure.GroupComponent();
                group.ElementId = measure.Id ?? measure.Name;

                if (measure.Identifier.Any())
                {
                    group.Code = new CodeableConcept(measure.Identifier.First().System, measure.Identifier.First().Value);
                }

                group.Description = measure.MetaData?.Annotation?.JsonBody;
                group.Population = new List<Measure.PopulationComponent>();
                foreach(var comp in measure.Computation)
                {
                    group.Population.Add(new Measure.PopulationComponent()
                    {
                        Code = DataTypeConverter.ToFhirMeasureType(comp),
                        Criteria = this.ToExpression(comp)
                    });
                }


                group.Stratifier = new List<Measure.StratifierComponent>();
                foreach(var strat in measure.Stratifiers)
                {
                    var stratifier = new Measure.StratifierComponent();

                    stratifier.ElementId = strat.Name ?? strat.ColumnReference.Name;
                    if (strat.Identifier.Any())
                    {
                        stratifier.Code = new CodeableConcept(strat.Identifier.First().System, strat.Identifier.First().Value);
                    }

                    stratifier.Criteria = this.ToExpression(strat.ColumnReference);
                    stratifier.Description = strat.MetaData?.Annotation?.JsonBody;

                    var thenBy = strat.ThenBy;
                    stratifier.Component = new List<Measure.ComponentComponent>();
                    while(thenBy != null)
                    {
                        var comp = new Measure.ComponentComponent();
                        comp.Code = new CodeableConcept("http://santedb.org/fhir/bi/StratifierCode", thenBy.Name ?? thenBy.ColumnReference.Name);
                        comp.Description = thenBy.MetaData?.Annotation?.JsonBody;
                        comp.Criteria = this.ToExpression(thenBy.ColumnReference);
                        stratifier.Component.Add(comp);
                        thenBy = thenBy.ThenBy;
                    }
                    group.Stratifier.Add(stratifier);
                }

                retVal.Group.Add(group);
            }
            return retVal;
        }

        /// <summary>
        /// Convert to expression
        /// </summary>
        private Expression ToExpression(BiSqlColumnReference columnRef)
        {
            var retVal = new Expression()
            {
                Language = "text/sql",
                Expression_ = columnRef.ColumnSelector,
                Name = columnRef.Name
            };

            if(columnRef is BiAggregateSqlColumnReference agg)
            {
                switch(agg.Aggregation)
                {
                    case BiAggregateFunction.Average:
                        retVal.Expression_ = $"AVG({retVal.Expression_})";
                        break;
                    case BiAggregateFunction.Count:
                        retVal.Expression_ = $"COUNT({retVal.Expression_})";
                        break;
                    case BiAggregateFunction.CountDistinct:
                        retVal.Expression_ = $"COUNT(DISTINCT {retVal.Expression_})";
                        break;
                    case BiAggregateFunction.First:
                        retVal.Expression_ = $"FIRST({retVal.Expression_})";
                        break;
                    case BiAggregateFunction.Last:
                        retVal.Expression_ = $"LAST({retVal.Expression_})";
                        break;
                    case BiAggregateFunction.Max:
                        retVal.Expression_ = $"MAX({retVal.Expression_})";
                        break;
                    case BiAggregateFunction.Min:
                        retVal.Expression_ = $"MIN({retVal.Expression_})";
                        break;
                    case BiAggregateFunction.Sum:
                        retVal.Expression_ = $"SUM({retVal.Expression_})";
                        break;
                }
            }

            return retVal;
        }

        /// <inheritdoc/>
        public Resource Read(string id, string versionId)
        {
            var indicator = this.m_biMetadataRepository.Get<BiIndicatorDefinition>(id);

            if(indicator == null)
            {
                throw new KeyNotFoundException($"Measure/{id}");
            }
            return this.MapToFhir(indicator);
        }

        /// <inheritdoc/>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            throw new NotSupportedException();
        }
    }
}

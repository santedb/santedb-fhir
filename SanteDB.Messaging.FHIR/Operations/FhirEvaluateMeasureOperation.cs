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
using Hl7.Fhir.Model;
using RestSrvr.Attributes;
using SanteDB.BI;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.BI.Util;
using SanteDB.Core;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace SanteDB.Messaging.FHIR.Operations
{
    /// <summary>
    /// Expand a measure definition to get a measure report
    /// </summary>
    public class FhirEvaluateMeasureOperation : IFhirOperationHandler
    {
        private readonly IBiMetadataRepository m_repository;
        private readonly IBiDataSource m_defaultDataSource;
        private readonly IAuditDispatchService m_auditService;
        private readonly IServiceManager m_serviceManager;
        private const string MEASURE_PARM_NAME = "measure";
        private const string PERIOD_START_PARM_NAME = "periodStart";
        private const string PERIOD_END_PARM_NAME = "periodEnd";
        private const string SUBJECT_ID_PARM_NAME = "subject";

        /// <summary>
        /// DI ctor
        /// </summary>
        public FhirEvaluateMeasureOperation(IBiMetadataRepository biMetadataRepository, IServiceManager serviceManager, IAuditDispatchService auditService, IBiDataSource biDataSource = null)
        {
            this.m_repository = biMetadataRepository;
            this.m_defaultDataSource = biDataSource;
            this.m_auditService = auditService;
            this.m_serviceManager = serviceManager;
        }

        /// <inheritdoc/>
        public string Name => "evaluate-measure";

        /// <inheritdoc/>
        public Uri Uri => new Uri("http://hl7.org/fhir/OperationDefinition/Measure-evaluate-measure");


        /// <inheritdoc/>
        public ResourceType[] AppliesTo => new ResourceType[] { ResourceType.Measure };

        /// <inheritdoc/>
        public IDictionary<string, FHIRAllTypes> Parameters => new Dictionary<String, FHIRAllTypes>()
        {
            { MEASURE_PARM_NAME, FHIRAllTypes.String },
            { PERIOD_START_PARM_NAME, FHIRAllTypes.Date },
            { PERIOD_END_PARM_NAME, FHIRAllTypes.Date },
            { SUBJECT_ID_PARM_NAME, FHIRAllTypes.String }
        };

        /// <inheritdoc/>
        public bool IsGet => true;

        /// <inheritdoc/>
        public Resource Invoke(Parameters parameters)
        {
            var indicatorId = parameters[MEASURE_PARM_NAME]?.Value as FhirString;

            var indicatorDef = this.m_repository.Get<BiIndicatorDefinition>(indicatorId.Value);
            if (indicatorDef == null)
            {
                throw new FhirException(System.Net.HttpStatusCode.BadRequest, OperationOutcome.IssueType.NotFound, $"Measure {indicatorId} not registered");
            }

            indicatorDef = BiUtils.ResolveRefs(indicatorDef);

            var dsource = indicatorDef.Query?.DataSources.FirstOrDefault(o => o.Name == "main") ?? indicatorDef.Query?.DataSources.FirstOrDefault();
            if (dsource == null)
            {
                throw new KeyNotFoundException("Query does not contain a data source");
            }

            IBiDataSource providerImplementation = null;
            if (dsource.ProviderType != null && this.m_repository.IsLocal)
            {
                providerImplementation = this.m_serviceManager.CreateInjected(dsource.ProviderType) as IBiDataSource;
            }
            else
            {
                providerImplementation = m_defaultDataSource; // Global default
            }


            BiIndicatorPeriod period = BiIndicatorPeriod.Empty;
            // What parameter was passed?
            if (parameters[PERIOD_START_PARM_NAME]?.Value is FhirDateTime startDt)
            {
                // We want to do a custom period or nah?
                if (parameters[PERIOD_END_PARM_NAME]?.Value is FhirDateTime endDt)
                {
                    period = new BiIndicatorPeriod(startDt.ToDateTime().Value, endDt.ToDateTime().Value);
                }
                else if (!indicatorDef.Period.TryGetPeriod(startDt.ToDateTime().Value, out period))
                {
                    throw new ArgumentOutOfRangeException($"{PERIOD_START_PARM_NAME} is not within period validity constraints");
                }
            }
            else
            {
                throw new ArgumentException($"Either {PERIOD_START_PARM_NAME} must be specified");
            }

            if (!(parameters[SUBJECT_ID_PARM_NAME]?.Value is FhirString str))
            {
                throw new ArgumentException($"{SUBJECT_ID_PARM_NAME} must be specified");
            }
            var forSubjectId = str?.Value;

            // Resolve from FHIR
            if (!DataTypeConverter.TryResolveResourceReference(new ResourceReference(forSubjectId), null, out var subject))
            {
                var subjRepo = typeof(IRepositoryService<>).MakeGenericType(indicatorDef.Subject.ResourceType);
                var repo = ApplicationServiceContext.Current.GetService(subjRepo) as IRepositoryService;
                subject = repo.Get(Guid.Parse(forSubjectId));
                if (subject == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, forSubjectId));
                }
            }

            // Bundle
            var retVal = new MeasureReport()
            {
                Id = $"{indicatorDef.Id}#{forSubjectId}/{period.Index}",
                Status = MeasureReport.MeasureReportStatus.Complete,
                Type = MeasureReport.MeasureReportType.Individual,
                Measure = $"http://santedb.org/bi/indicator/{indicatorDef.Id}",
                Subject = DataTypeConverter.CreateRimReference(subject),
                DateElement = new FhirDateTime(DateTime.Now),
                Period = new Period(new FhirDateTime(period.Start), new FhirDateTime(period.End)),
                Group = new List<MeasureReport.GroupComponent>()
            };

            
            foreach (var indicatorResult in providerImplementation.ExecuteIndicator(indicatorDef, period, subject.Key.ToString()).GroupBy(o=>o.Measure))
            {
                var measureGroup = new MeasureReport.GroupComponent();

                foreach (var measureResult in indicatorResult)
                {
                    
                    // Are we in a stratifier?
                    if (String.IsNullOrEmpty(measureResult.StratifierPath))
                    {
                        var currentRecord = measureResult.Records.SingleOrDefault() as IDictionary<String, object>;

                        if (currentRecord == null) { continue; }

                        // Numerator and denominator
                        measureGroup.Population = this.ConvertComputation(currentRecord, measureResult.Measure, out var numeratorValue, out var denominatorValue, out var scoreObtained);
                        

                        measureGroup.ElementId = measureResult.Measure.Id ?? measureResult.Measure.Name;
                        if (measureResult.Measure.Identifier != null)
                        {
                            measureGroup.Code = new CodeableConcept(measureResult.Measure.Identifier.System, measureResult.Measure.Identifier.Value);
                        }

                        if (scoreObtained.HasValue)
                        {
                            measureGroup.MeasureScore = new Quantity(scoreObtained.Value, null, null);
                        }
                        else { 
                            if (denominatorValue.HasValue)
                            {
                                measureGroup.MeasureScore = new Quantity((decimal)((float)numeratorValue / (float)denominatorValue), null, null);
                            }
                            else
                            {
                                measureGroup.MeasureScore = new Quantity((decimal)numeratorValue, null, null);
                            }
                        }
                    }
                    else
                    {
                        var stratumPath = measureResult.StratifierPath.Split('/').Skip(1).ToArray();
                        if(stratumPath.Length > 2)
                        {
                            throw new NotSupportedException("The FHIR interface cannot process indicators with more than two levels of stratifiers");
                        }

                        var stratifier = new MeasureReport.StratifierComponent()
                        {
                            ElementId = String.Join("/", stratumPath)
                        };
                        var stratifierDefn = measureResult.Measure.Stratifiers.Find(o => o.Name == stratumPath[0]);
                        if (stratumPath.Length > 1) // TODO: Skip sub-stratifiers
                        {
                            stratifierDefn = stratifierDefn.ThenBy;
                        }

                        if (stratifierDefn.Identifier != null)
                        {
                            stratifier.Code.Add(new CodeableConcept(stratifierDefn.Identifier.System, stratifierDefn.Identifier.Value));
                        }

                        foreach (IDictionary<String, object> currentRecord in measureResult.Records)
                        {
                            var stratName = String.Join("/", Enumerable.Range(1, stratumPath.Length).Select(level => currentRecord[currentRecord.Keys.Skip(level).First()]));
                            var stratum = new MeasureReport.StratifierGroupComponent()
                            {
                                Value = new CodeableConcept(null, stratName.ToString()),
                            };
                            stratum.Population = this.ConvertComputation(currentRecord, measureResult.Measure, out var numeratorValue, out var denominatorValue, out var scoreObtained)
                                    .Select(o => new MeasureReport.StratifierGroupPopulationComponent() { Code = o.Code, Count = o.Count }).ToList();

                            if (scoreObtained.HasValue)
                            {
                                stratum.MeasureScore = new Quantity(scoreObtained.Value, null, null);
                            }
                            else
                            {
                                if (denominatorValue.HasValue)
                                {
                                    stratum.MeasureScore = new Quantity((decimal)((float)numeratorValue / (float)denominatorValue), null, null);
                                }
                                else
                                {
                                    stratum.MeasureScore = new Quantity((decimal)numeratorValue, null, null);
                                }
                            }

                            stratifier.Stratum.Add(stratum);
                        }

                        // TODO: Denominator on the stratum may need to be updated to match the numerator of the overall group 

                        measureGroup.Stratifier.Add(stratifier);
                    }
                }

                retVal.Group.Add(measureGroup);

            }

            return retVal;
        }

        private List<MeasureReport.PopulationComponent> ConvertComputation(IDictionary<String, object> currentRecord, BiIndicatorMeasureDefinition measure, out long? numeratorValue, out long? denominatorValue, out decimal? scoreObtained)
        {
            var retVal = new List<MeasureReport.PopulationComponent>(3);
            numeratorValue = null;
            denominatorValue = null;
            scoreObtained = null;
            foreach (var calc in measure.Computation)
            {
                switch (calc)
                {
                    case BiMeasureComputationNumerator cNumerator:
                        numeratorValue = (long)currentRecord[calc.Name];
                        break;
                    case BiMeasureComputationNumeratorExclusion cNumeratorExcl:
                        numeratorValue -= (long)currentRecord[calc.Name];
                        break;
                    case BiMeasureComputationDenominator cDenominator:
                        denominatorValue = (long)currentRecord[calc.Name];
                        break;
                    case BiMeasureComputationDenominatorExclusion cDenominator:
                        denominatorValue -= (long)currentRecord[calc.Name];
                        break;
                    case BiMeasureComputationScore cScore:
                        var rawValue = currentRecord[calc.Name];
                        if (rawValue == null)
                        {
                            scoreObtained = 0;
                        }
                        else if (rawValue is Decimal d || Decimal.TryParse(rawValue.ToString(), out d))
                        {
                            scoreObtained = d;
                        }
                        continue;
                    default:
                        continue;
                }

                retVal.Add(new MeasureReport.PopulationComponent()
                {
                    Code = DataTypeConverter.ToFhirMeasureType(calc),
                    Count = (int)(long)currentRecord[calc.Name]
                });

            }

            return retVal;
        }
    }
}

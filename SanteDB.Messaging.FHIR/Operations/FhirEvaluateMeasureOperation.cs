using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Model;
using RestSrvr.Attributes;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.BI.Util;
using SanteDB.Core;
using SanteDB.Core.i18n;
using SanteDB.Core.Services;
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
            var subjRepo = typeof(IRepositoryService<>).MakeGenericType(indicatorDef.Subject.ResourceType);
            var repo = ApplicationServiceContext.Current.GetService(subjRepo) as IRepositoryService;
            var subject = repo.Get(Guid.Parse(forSubjectId));
            if(subject == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, forSubjectId));
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


            foreach (var indicatorResult in providerImplementation.ExecuteIndicator(indicatorDef, period, forSubjectId).GroupBy(o=>o.Measure))
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
                        long numeratorValue = (long)currentRecord[measureResult.Measure.Numerator.Name ?? "numerator"],
                            denominatorValue = (long)currentRecord[measureResult.Measure.Denominator.Name ?? "denominator"];

                        measureGroup.ElementId = measureResult.Measure.Id ?? measureResult.Measure.Name;
                        if (measureResult.Measure.Identifier != null)
                        {
                            measureGroup.Code = new CodeableConcept(measureResult.Measure.Identifier.System, measureResult.Measure.Identifier.Value);
                        }
                        measureGroup.Population = new List<MeasureReport.PopulationComponent>()
                        {
                            new MeasureReport.PopulationComponent()
                            {
                                Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "numerator"),
                                Count = (int)numeratorValue
                            },
                            new MeasureReport.PopulationComponent()
                            {
                                Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "denominator"),
                                Count = (int)denominatorValue
                            }
                        };
                        measureGroup.MeasureScore = new Quantity((decimal)((float)numeratorValue / (float)denominatorValue), null);
                    }
                    else
                    {
                        var stratumPath = measureResult.StratifierPath.Split('/').Skip(1).ToArray();
                        if (stratumPath.Length > 1) // TODO: Skip sub-stratifiers
                        {
                            continue;
                        }

                        var stratifier = new MeasureReport.StratifierComponent()
                        {
                            ElementId = stratumPath[0]
                        };
                        var stratifierDefn = measureResult.Measure.Stratifiers.Find(o => o.Name == stratumPath[0]);
                        if (stratifierDefn.Identifier != null)
                        {
                            stratifier.Code.Add(new CodeableConcept(stratifierDefn.Identifier.System, stratifierDefn.Identifier.Value));
                        }

                        long stratumDenominator = 0;
                        foreach (IDictionary<String, object> result in measureResult.Records)
                        {
                            var stratName = result[result.Keys.Skip(stratumPath.Length).First()];
                            long numeratorValue = (long)result[measureResult.Measure.Numerator.Name ?? "numerator"],
                                denominatorValue = (long)result[measureResult.Measure.Denominator.Name ?? "denominator"];
                            stratumDenominator += numeratorValue;
                            var stratum = new MeasureReport.StratifierGroupComponent()
                            {
                                Value = new CodeableConcept(null, stratName.ToString()),
                                Population = new List<MeasureReport.StratifierGroupPopulationComponent>()
                                {
                                    new MeasureReport.StratifierGroupPopulationComponent()
                                    {
                                        Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "numerator"),
                                        Count = (int)numeratorValue
                                    }
                                }
                            };

                            stratifier.Stratum.Add(stratum);
                        }
                        stratifier.Stratum.ForEach(st => st.Population.Add(new MeasureReport.StratifierGroupPopulationComponent()
                        {
                            Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "denominator"),
                            Count = (int)stratumDenominator
                        }));

                        measureGroup.Stratifier.Add(stratifier);
                    }
                }

                retVal.Group.Add(measureGroup);

            }

            return retVal;
        }
    }
}

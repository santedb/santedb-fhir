using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
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
                var handler = FhirResourceHandlerUtil.GetResourceHandler(resource.ResourceType) as IFhirResourceMapper;
                if (handler == null)
                    throw new NotSupportedException($"Operation on {resource.ResourceType} not supported");

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
                    var configBase = this.m_configuration.ResourceTypes.FirstOrDefault(o => o.ResourceType == modelInstance.GetType());
                    if (configBase == null)
                    {
                        throw new InvalidOperationException($"No resource merge configuration for {modelInstance.GetType()} available. Use either ?_configurationName parameter to add a ResourceMergeConfigurationSection to your configuration file");
                    }

                    results = configBase.MatchConfiguration.SelectMany(o => matchService.Match(modelInstance, o.MatchConfiguration, mergeService?.GetIgnoredKeys(modelInstance.Key.Value))).ToArray();
                }

                // Only certain matches
                if (onlyCertainMatches?.Value == true)
                    results = results.Where(o => o.Classification == RecordMatchClassification.Match);

                // Next we want to convert to FHIR
                var retVal = new Bundle()
                {
                    Id = $"urn:uuid:{Guid.NewGuid()}",
                    Meta = new Meta()
                    {
                        LastUpdated = DateTimeOffset.Now
                    },
                    Type = Bundle.BundleType.Searchset,
                    Total = results.Count(),
                };

                // Iterate through the resources and convert them to FHIR
                // Grouping by the ID of the candidate and selecting the highest rated match
                retVal.Entry = results.GroupBy(o => o.Record.Key).Select(o => o.OrderByDescending(m => m.Score).First())
                    .Select(o => this.ConvertMatchResult(o, handler)).ToList();

                return retVal;
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError($"Error running match on {resource.ResourceType} - {e}");
                throw new Exception($"Error running match operation on {resource.ResourceType}", e);
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
                FullUrl = $"{result.ResourceType}/{result.Id}/_history/{result.VersionId}",
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

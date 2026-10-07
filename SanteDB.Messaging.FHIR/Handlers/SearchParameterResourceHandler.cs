using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents the default SearchParameter handler
    /// </summary>
    [ResourceSensitivity(ResourceSensitivityClassification.Metadata)]
    public class SearchParameterResourceHandler : IFhirResourceHandler
    {
        /// <inheritdoc/>
        public ResourceType ResourceType => ResourceType.SearchParameter;

        /// <inheritdoc/>
        public Resource Create(Resource target, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        public Resource Delete(string id, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        public CapabilityStatement.ResourceComponent GetResourceDefinition()
        {
            return new ResourceComponent()
            {
                ConditionalCreate = false,
                ConditionalDelete = ConditionalDeleteStatus.NotSupported,
                ConditionalUpdate = false,
                Interaction = new List<ResourceInteractionComponent>()
                {
                    new ResourceInteractionComponent()
                    {
                        Code = TypeRestfulInteraction.Read
                    },
                    new ResourceInteractionComponent()
                    {
                        Code = TypeRestfulInteraction.SearchType
                    }
                },
                Type = Hl7.Fhir.Model.ResourceType.SearchParameter,
                ReadHistory = false,
                UpdateCreate = false,
                Versioning = ResourceVersionPolicy.Versioned,
                SearchParam = new List<SearchParamComponent>()
                {
                    new SearchParamComponent()
                    {
                        Name = "_offset",
                        Type = SearchParamType.Number
                    },
                    new SearchParamComponent()
                    {
                        Name = "_count",
                        Type = SearchParamType.Number
                    },
                    new SearchParamComponent()
                    {
                        Name = "_page",
                        Type = SearchParamType.Number
                    }
                }
            };
        }

        /// <inheritdoc/>
        public StructureDefinition GetStructureDefinition()
        {
            return typeof(SearchParameter).GetStructureDefinition(false);
        }

        /// <inheritdoc/>
        public Bundle History(string id)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        public Bundle Query(NameValueCollection parameters)
        {
            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(SearchParameter), typeof(ServiceOptions), parameters, out var hdsiQuery);
            IQueryResultSet results = QueryRewriter.GetAllSearchParams().AsResultSet();

            // TODO: Filtering
            results = query.ApplyCommonQueryControls(results, out var totalResults);

            return MessageUtil.CreateBundle(new FhirQueryResult(this.ResourceType.ToString())
            {
                Results = results.OfType<SearchParamComponent>().ToArray().AsParallel().Select(o =>
                {
                    return new Bundle.EntryComponent()
                    {
                        Resource = this.ConvertSearchParameterComponent(o),
                        Search = new Bundle.SearchComponent()
                        {
                            Mode = Bundle.SearchEntryMode.Match
                        }
                    };
                }).ToList(),
                Query = query,
                TotalResults = totalResults
            }, Bundle.BundleType.Searchset);
        }

        private Resource ConvertSearchParameterComponent(SearchParamComponent componentObject) => new SearchParameter()
        {
            Meta = new Meta()
            {
                LastUpdated = String.IsNullOrEmpty(this.GetType().Assembly.Location) ? DateTimeOffset.Now : new FileInfo(this.GetType().Assembly.Location).LastWriteTime
            },
            Url = $"{FhirConstants.SanteDBProfile}/search/{componentObject.Definition}",
            Type = componentObject.Type,
            Name = componentObject.Name,
            Status = PublicationStatus.Active,
            Description = componentObject.Documentation,
            Id = componentObject.Definition,
            MultipleOr = true,
            Comparator = componentObject.Type == SearchParamType.String ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Ap, SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne } :
                            componentObject.Type == SearchParamType.Number ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne, SearchParameter.SearchComparator.Gt, SearchParameter.SearchComparator.Lt, SearchParameter.SearchComparator.Le, SearchParameter.SearchComparator.Ge } :
                            componentObject.Type == SearchParamType.Date ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Le, SearchParameter.SearchComparator.Ap, SearchParameter.SearchComparator.Le, SearchParameter.SearchComparator.Lt, SearchParameter.SearchComparator.Ge, SearchParameter.SearchComparator.Gt } :
                            componentObject.Type == SearchParamType.Token ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne } :
                            componentObject.Type == SearchParamType.Reference ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne } :
                            componentObject.Type == SearchParamType.Quantity ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne, SearchParameter.SearchComparator.Lt, SearchParameter.SearchComparator.Le, SearchParameter.SearchComparator.Gt, SearchParameter.SearchComparator.Ge } :
                            componentObject.Type == SearchParamType.Uri ? new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne } :
                            new SearchParameter.SearchComparator?[] { SearchParameter.SearchComparator.Eq, SearchParameter.SearchComparator.Ne },
        };

        /// <inheritdoc/>
        public Resource Read(string id, string versionId)
        {
            var retVal = QueryRewriter.GetAllSearchParams().AsResultSet().FirstOrDefault(o => o.Definition == id);

            if (retVal == null)
            {
                throw new KeyNotFoundException(id);
            }
            else
            {
                return this.ConvertSearchParameterComponent(retVal);
            }
        }

        /// <inheritdoc/>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }
    }
}

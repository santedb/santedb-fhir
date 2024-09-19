/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a base FHIR resource handler.
    /// </summary>
    /// <typeparam name="TFhirResource">The type of the t FHIR resource.</typeparam>
    /// <typeparam name="TModel">The type of the t model.</typeparam>
    /// <seealso cref="SanteDB.Messaging.FHIR.Handlers.IFhirResourceHandler" />
    public abstract class ResourceHandlerBase<TFhirResource, TModel> : IFhirResourceHandler, IFhirResourceMapper, IServiceImplementation
        where TFhirResource : Resource, new()
        where TModel : IdentifiedData, new()

    {
        /// <summary>
        /// Include instruction informs the FHIR handler of other resources to include in the result (_include=Patient:target)
        /// </summary>
        protected struct IncludeInstruction
        {
            /// <summary>
            /// Create an include instruction
            /// </summary>
            /// <param name="path">The path passed by the REST caller</param>
            /// <param name="type">The type of resource to include</param>
            public IncludeInstruction(ResourceType type, String path)
            {
                this.Type = type;
                this.JoinPath = path;
            }

            /// <summary>
            /// Parse from a query instruction passed on the REST API
            /// </summary>
            /// <param name="queryInstruction">The query instruction (example: ?_include=Patient:target)</param>
            public IncludeInstruction(String queryInstruction)
            {
                var parsed = queryInstruction.Split(':');
                if (parsed.Length != 2)
                {
                    var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
                    var tracer = Tracer.GetTracer(typeof(ResourceHandlerBase<TFhirResource, TModel>));

                    tracer.TraceError($"{queryInstruction} is not a valid include instruction");
                    throw new ArgumentOutOfRangeException(localizationService.GetString("error.type.InvalidDataException.userMessage", new
                    {
                        param = "include instruction"
                    }));
                }

                this.Type = EnumUtility.ParseLiteral<ResourceType>(parsed[0]).Value;
                this.JoinPath = parsed[1];
            }

            /// <summary>
            /// The type of include
            /// </summary>
            public ResourceType Type { get; set; }

            /// <summary>
            /// The path to join on
            /// </summary>
            public String JoinPath { get; set; }

            /// <summary>
            /// Represent as a string
            /// </summary>
            public override string ToString() => $"{this.Type}:{this.JoinPath}";
        }

        /// <summary>
        /// The trace source instance.
        /// </summary>
        protected readonly Tracer m_traceSource = new Tracer(FhirConstants.TraceSourceName);

        /// <summary>
        /// The localization service.
        /// </summary>
        protected readonly ILocalizationService m_localizationService;

        /// <summary>
		/// Creates the resource handler
		/// </summary>
        public ResourceHandlerBase(ILocalizationService localizationService)
        {
            this.m_localizationService = localizationService;
            // Get the string name of the resource
            var typeAttribute = typeof(TFhirResource).GetCustomAttribute<FhirTypeAttribute>();
            if (typeAttribute == null || !typeAttribute.IsResource || !Enum.TryParse<ResourceType>(typeAttribute.Name, out ResourceType resourceType))
            {
                this.m_traceSource.TraceError($"Type of {typeof(TFhirResource)} is not a resource");
                throw new InvalidOperationException(this.m_localizationService.GetString("error.type.InvalidDataException.userMessage", new
                {
                    param = "resource"
                }));
            }
            this.ResourceType = resourceType;
        }

        /// <summary>
        /// Gets the name of the resource.
        /// </summary>
        /// <value>The name of the resource.</value>
        public ResourceType ResourceType { get; }

        /// <summary>
        /// Gets the canonical type (the type SanteDB uses)
        /// </summary>
        public Type CanonicalType => typeof(TModel);

        /// <summary>
        /// Gets the CLR type of the FHIR resource
        /// </summary>
        public Type ResourceClrType => typeof(TFhirResource);

        /// <summary>
        /// Get service name
        /// </summary>
        public string ServiceName => "Resource Handler Base";

        /// <summary>
        /// Create the specified resource in the repository layer
        /// </summary>
        /// <param name="resource">The resource which should be created</param>
        /// <param name="mode">The mode in which the transaction should be committed</param>
        /// <returns>The created resource</returns>
        /// <exception cref="System.ArgumentNullException">When the resource is null</exception>
        /// <exception cref="ArgumentException">When the resource is not the correct type</exception>
        public virtual Resource Create(Resource resource, TransactionMode mode)
        {
            this.m_traceSource.TraceInfo("Creating resource {0} ({1})", this.ResourceType, resource);

            if (resource == null)
            {
                this.m_traceSource.TraceError($"Argument {nameof(resource)} null or empty");
                throw new ArgumentNullException(ErrorMessages.ARGUMENT_NULL);
            }
            else if (resource is TFhirResource fhirResource)
            {
                resource = ExtensionUtil.ExecuteAfterReceiveRequestBehavior(TypeRestfulInteraction.Create, this.ResourceType, resource);

                // We want to map from TFhirResource to TModel
                var modelInstance = this.MapToModel(fhirResource);
                if (modelInstance == null)
                {
                    throw new ArgumentException(this.m_localizationService.GetString("error.type.InvalidDataException.userMessage", new
                    {
                        param = "Model"
                    }));
                }
                DataTypeConverter.AddContextProvenanceData(modelInstance);

                var result = this.Create(modelInstance, mode);

                // Return fhir operation result
                var retVal = this.MapToFhir(result);
                return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Create, this.ResourceType, retVal);
            }
            else
            {
                throw new ArgumentException(nameof(resource), String.Format(ErrorMessages.ARGUMENT_INVALID_TYPE, typeof(TFhirResource), resource.GetType()));
            }
        }

        /// <summary>
        /// Deletes a specified resource.
        /// </summary>
        /// <param name="id">The identifier of the resource to delete</param>
        /// <param name="mode">The method of deletion</param>
        /// <returns>The resource that was deleted</returns>
        /// <exception cref="System.ArgumentNullException">The identifier is not available or is in the incorrect format</exception>
        public Resource Delete(string id, TransactionMode mode)
        {
            if (String.IsNullOrEmpty(id))
            {
                this.m_traceSource.TraceError($"Argument {nameof(id)} is null or empty");
                throw new ArgumentNullException(ErrorMessages.ARGUMENT_NULL);
            }

            this.m_traceSource.TraceInfo("Deleting resource {0}/{1}", this.ResourceType, id);

            // Delete
            var guidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
            {
                throw new ArgumentException(m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "id"
                }));
            }

            // Do the deletion
            var result = this.Delete(guidId);

            // Return fhir operation result
            var retVal = this.MapToFhir(result);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Delete, this.ResourceType, retVal);
        }

        /// <summary>
        /// Get definition for the specified resource
        /// </summary>
        public virtual ResourceComponent GetResourceDefinition()
        {
            return new ResourceComponent()
            {
                ConditionalCreate = false,
                ConditionalUpdate = true,
                ConditionalDelete = ConditionalDeleteStatus.NotSupported,
                ReadHistory = true,
                UpdateCreate = true,
                Versioning = typeof(IVersionedData).IsAssignableFrom(typeof(TModel)) ?
                    ResourceVersionPolicy.Versioned :
                    ResourceVersionPolicy.NoVersion,
                Interaction = this.GetInteractions().ToList(),
                SearchParam = QueryRewriter.GetSearchParams<TFhirResource, TModel>().ToList(),
                Type = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(typeof(TFhirResource).GetCustomAttribute<FhirTypeAttribute>().Name),
                Profile = $"/StructureDefinition/SanteDB/_history/{Assembly.GetEntryAssembly().GetName().Version}"
            };
        }

        /// <summary>
        /// Get structure definitions
        /// </summary>
        public virtual StructureDefinition GetStructureDefinition()
        {
            return StructureDefinitionUtil.GetStructureDefinition(typeof(TFhirResource), false);
        }

        /// <summary>
        /// Get interactions supported by this handler
        /// </summary>
        protected abstract IEnumerable<ResourceInteractionComponent> GetInteractions();

        /// <summary>
        /// Queries for a specified resource.
        /// </summary>
        /// <param name="parameters">The parameters for the queyr in FHIR format</param>
        /// <returns>Returns the FHIR query result containing the results of the query</returns>
        /// <exception cref="System.ArgumentNullException">Parameters have not been passed</exception>
        public virtual Bundle Query(System.Collections.Specialized.NameValueCollection parameters)
        {
            if (parameters == null)
            {
                this.m_traceSource.TraceError($"Argument {nameof(parameters)} null or empty");
                throw new ArgumentNullException(nameof(parameters), ErrorMessages.ARGUMENT_NULL);
            }

            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(TFhirResource), typeof(TModel), parameters, out var hdsiQuery);

            // Do the query
            var predicate = QueryExpressionParser.BuildLinqExpression<TModel>(hdsiQuery, null, false, false);

            var hdsiResults = this.QueryInternal(predicate, hdsiQuery, hdsiQuery);
            var results = query.ApplyCommonQueryControls(hdsiResults, out int totalResults).OfType<TModel>();

            var auth = AuthenticationContext.Current;

            using (DataPersistenceControlContext.Create(LoadMode.SyncLoad))
            {
                var retVal = new FhirQueryResult(typeof(TFhirResource).Name)
                {
                    Results = results.ToArray().AsParallel().Select(o =>
                    {

                        using (AuthenticationContext.EnterContext(auth.Principal))
                        {
                            return new Bundle.EntryComponent()
                            {
                                Resource = this.MapToFhir(o),
                                Search = new Bundle.SearchComponent()
                                {
                                    Mode = Bundle.SearchEntryMode.Match
                                }
                            };
                        }
                    }).ToList(),
                    Query = query,
                    TotalResults = totalResults
                };
                this.ProcessIncludes(hdsiResults, parameters, retVal);
                return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.SearchType, this.ResourceType, MessageUtil.CreateBundle(retVal, Bundle.BundleType.Searchset)) as Bundle;
            }
        }

        /// <summary>
        /// Process includes for the specified result set
        /// </summary>
        /// <param name="parameters">The parameters on the original query</param>
        /// <param name="queryResult">The query results control which dictate how related data should be loaded</param>
        /// <param name="results">The primary result set returned from the persistence layer</param>
        protected virtual void ProcessIncludes(IEnumerable<TModel> results, System.Collections.Specialized.NameValueCollection parameters, FhirQueryResult queryResult)
        {
            // Include or ref include?
            if (parameters["_include"] != null) // TODO: _include:iterate (fhir is crazy)
            {
                queryResult.Results = queryResult.Results.Union(results.SelectMany(h => this.GetIncludes(h, parameters["_include"].Split(',').Select(o => new IncludeInstruction(o)))).Select(o => new Bundle.EntryComponent()
                {
                    Resource = o,
                    Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Include }
                })).ToList();
            }
            if (parameters["_revinclude"] != null) // TODO: _revinclude:iterate (fhir is crazy)
            {
                queryResult.Results = queryResult.Results.Union(results.SelectMany(h => this.GetReverseIncludes(h, parameters["_revinclude"].Split(',').Select(o => new IncludeInstruction(o)))).Select(o => new Bundle.EntryComponent()
                {
                    Resource = o,
                    Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Include }
                })).ToList();
            }
        }

        /// <summary>
        /// Retrieves a specific resource.
        /// </summary>
        /// <param name="id">The identifier of the resource to retrieve</param>
        /// <param name="versionId">The version of the resource</param>
        /// <returns>Returns the FHIR operation result containing the retrieved resource.</returns>
        /// <exception cref="System.ArgumentNullException">The identifier is not present</exception>
        /// <exception cref="System.ArgumentException">The identifier is not a UUID</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The specified object could not be found</exception>
        public Resource Read(string id, string versionId)
        {
            if (String.IsNullOrEmpty(id))
            {
                this.m_traceSource.TraceError($"Argument {nameof(id)} null or empty");
                throw new ArgumentNullException(ErrorMessages.ARGUMENT_NULL);
            }

            Guid guidId = Guid.Empty, versionGuidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
            {
                throw new ArgumentException(this.m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "id"
                }));
            }

            if (!String.IsNullOrEmpty(versionId) && !Guid.TryParse(versionId, out versionGuidId))
            {
                throw new ArgumentException(this.m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "versionId"
                }));
            }

            var result = this.Read(guidId, versionGuidId);
            if (result == null)
            {
                this.m_traceSource.TraceError($"{this.ResourceType}/{id} not found");
                throw new KeyNotFoundException(this.m_localizationService.GetString("error.type.KeyNotFoundException"));
            }
            else if (result is BaseEntityData bed && bed.ObsoletionTime.HasValue &&
                versionGuidId == Guid.Empty) // The resource is logically deleted FHIR requires 410 gone
            {
                this.m_traceSource.TraceWarning($"{this.ResourceType}/{id} was deleted");
                throw new FhirException(System.Net.HttpStatusCode.Gone, OperationOutcome.IssueType.Deleted, $"{result} deleted on {bed.ObsoletionTime:o}");
            }

            // FHIR Operation result
            var retVal = this.MapToFhir(result);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(String.IsNullOrEmpty(versionId) ? TypeRestfulInteraction.Read : TypeRestfulInteraction.Vread, this.ResourceType, retVal);
        }

        /// <summary>
        /// Updates the specified resource with new data in <paramref name="resource"/>
        /// </summary>
        /// <param name="id">The identifier of the resource to update</param>
        /// <param name="resource">The The resource to update</param>
        /// <param name="mode">The mode of update (commit or rollback)</param>
        /// <returns>Returns the FHIR operation result containing the updated resource.</returns>
        /// <exception cref="System.ArgumentNullException">The resource has not been passed to the function</exception>
        /// <exception cref="System.IO.InvalidDataException">The resource is not valid according to its business constraints</exception>
        /// <exception cref="System.ArgumentException">The resource or id are in an invalid format</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">There are multiple resources which could be the target of this update</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The specified resource could not be found</exception>
        public Resource Update(string id, Resource resource, TransactionMode mode)
        {
            this.m_traceSource.TraceInfo("Updating resource {0}/{1} ({2})", this.ResourceType, id, resource);

            if (resource == null)
            {
                this.m_traceSource.TraceError($"Argument {nameof(resource)} is null or empty");
                throw new ArgumentNullException(nameof(resource), ErrorMessages.ARGUMENT_NULL);
            }
            else if (!(resource is TFhirResource))
            {
                throw new ArgumentException(nameof(resource), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(TFhirResource), resource?.GetType()));
            }

            resource = ExtensionUtil.ExecuteAfterReceiveRequestBehavior(TypeRestfulInteraction.Update, this.ResourceType, resource);

            // We want to map from TFhirResource to TModel
            resource.Id = id;
            var modelInstance = this.MapToModel(resource as TFhirResource);
            if (modelInstance == null)
            {
                throw new ArgumentException(this.m_localizationService.GetString("error.type.InvalidDataException.userMessage", new
                {
                    param = "Request"
                }));
            }
            DataTypeConverter.AddContextProvenanceData(modelInstance);

            // Guid identifier
            var guidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
            {
                throw new ArgumentException(this.m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "id"
                }));
            }

            // Model instance key does not equal path
            if (modelInstance.Key != Guid.Empty && modelInstance.Key != guidId)
            {
                throw new InvalidOperationException(this.m_localizationService.GetString("error.messaging.fhir.resourceBase.key"));
            }
            else if (modelInstance.Key == Guid.Empty)
            {
                modelInstance.Key = guidId;
            }

            var result = this.Update(modelInstance, mode);

            // Return fhir operation result
            var retVal = this.MapToFhir(result);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Update, this.ResourceType, retVal);
        }

        /// <summary>
        /// Creates the specified model instance.
        /// </summary>
        /// <param name="modelInstance">The model instance to create</param>
        /// <param name="mode">The mode of creation (rollback or commit)</param>
        /// <returns>Returns the created model</returns>
        protected abstract TModel Create(TModel modelInstance, TransactionMode mode);

        /// <summary>
        /// Deletes the specified model
        /// </summary>
        /// <param name="modelId">The model identifier</param>
        /// <returns>Returns the deleted object</returns>
        protected abstract TModel Delete(Guid modelId);

        /// <summary>
        /// Maps a model instance to a FHIR instance.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>Returns the mapped FHIR resource.</returns>
        protected abstract TFhirResource MapToFhir(TModel model);

        /// <summary>
        /// Maps a FHIR resource to a model instance.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <returns>Returns the mapped model.</returns>
        protected abstract TModel MapToModel(TFhirResource resource);

        /// <summary>
        /// Gets includes specified by the caller
        /// </summary>
        /// <param name="includePaths">The include paths to retrieve</param>
        /// <param name="resource">The primary resource on which includes are being retrieved</param>
        protected abstract IEnumerable<Resource> GetIncludes(TModel resource, IEnumerable<IncludeInstruction> includePaths);

        /// <summary>
        /// Gets the reverse include paths
        /// </summary>
        /// <param name="resource">The resource on which the reverse includes are being retrieved</param>
        /// <param name="reverseIncludePaths">The paths of reverse includes</param>
        protected abstract IEnumerable<Resource> GetReverseIncludes(TModel resource, IEnumerable<IncludeInstruction> reverseIncludePaths);

        /// <summary>
        /// Execute the specified query
        /// </summary>
        /// <param name="query">The query filter</param>
        /// <param name="fhirParameters"></param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        /// <param name="hdsiParameters"></param>
        protected abstract IQueryResultSet<TModel> QueryInternal(Expression<Func<TModel, bool>> query, NameValueCollection fhirParameters, NameValueCollection hdsiParameters);

        /// <summary>
        /// Read the specified FHIR object.
        /// </summary>
        /// <param name="id">The identifier of the object</param>
        /// <param name="versionId">The version of the object to retrieve</param>
        /// <returns>Returns the model which matches the given id.</returns>
        protected abstract TModel Read(Guid id, Guid versionId);

        /// <summary>
        /// Updates the specified fhir resource
        /// </summary>
        /// <param name="model">The resource to update</param>
        /// <param name="mode">The mode of update</param>
        /// <returns>Returns the updated model.</returns>
        protected abstract TModel Update(TModel model, TransactionMode mode);

        /// <summary>
        /// Reads the complete history of the specified identifier
        /// </summary>
        /// <param name="id">The identifier of the object to read history for</param>
        public Bundle History(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                this.m_traceSource.TraceError($"Argument {nameof(id)} null or empty");
                throw new ArgumentNullException(ErrorMessages.ARGUMENT_NULL);
            }

            Guid guidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
            {
                throw new ArgumentException(this.m_localizationService.GetString("error.type.ArgumentException", new
                {
                    param = "id"
                }));
            }

            var result = this.Read(guidId, Guid.Empty);
            if (result == null)
            {
                throw new KeyNotFoundException(this.m_localizationService.GetString("error.type.KeyNotFoundException"));
            }

            // Results
            List<TModel> results = new List<TModel>() { result };
            while ((result as IVersionedData)?.PreviousVersionKey.HasValue == true)
            {
                result = this.Read(guidId, (result as IVersionedData).PreviousVersionKey.Value);
                results.Add(result);
            }

            // FHIR Operation result
            var retVal = new FhirQueryResult(typeof(TFhirResource).Name)
            {
                Results = results.Select(this.MapToFhir).Select(o => new Bundle.EntryComponent()
                {
                    Resource = o,
                    Response = new Bundle.ResponseComponent()
                    {
                        Status = "200"
                    }
                }).ToList()
            };

            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.HistoryInstance, this.ResourceType, MessageUtil.CreateBundle(retVal, Bundle.BundleType.History)) as Bundle;
        }

        /// <summary>
        /// Map to FHIR
        /// </summary>
        /// <param name="modelInstance">The SanteDB model instance which should be mapped to FHIR</param>
        public Resource MapToFhir(IdentifiedData modelInstance)
        {
            return this.MapToFhir((TModel)modelInstance);
        }

        /// <summary>
        /// Map the object to model
        /// </summary>
        /// <param name="resourceInstance">The FHIR resource which should be mapped to SanteDB</param>
        public IdentifiedData MapToModel(Resource resourceInstance)
        {
            using (AuthenticationContext.EnterSystemContext()) // All queries under the mapping process are performed by the SYSTEM
            {
                var retVal = this.MapToModel((TFhirResource)resourceInstance);
                // Append the notice that this is a source model
                switch (retVal)
                {
                    case IResourceCollection irc:
                        irc.AddAnnotationToAll(SanteDBModelConstants.NoDynamicLoadAnnotation);
                        break;
                    case ITargetedAssociation tra:
                        (tra.TargetEntity as IdentifiedData)?.AddAnnotation(SanteDBModelConstants.NoDynamicLoadAnnotation);
                        (tra.SourceEntity as IdentifiedData)?.AddAnnotation(SanteDBModelConstants.NoDynamicLoadAnnotation);
                        break;
                    default:
                        retVal.AddAnnotation(SanteDBModelConstants.NoDynamicLoadAnnotation);
                        break;
                }

                return retVal;
            }
        }

        /// <summary>
        /// True if this handler can process the object
        /// </summary>
        /// <param name="instance">The instance to test for applicability for mapping</param>
        public virtual bool CanMapObject(object instance) => instance is TModel || instance is TFhirResource;
    }
}
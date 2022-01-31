/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-10-29
 */
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// Include instruction
        /// </summary>
        protected struct IncludeInstruction
        {
            /// <summary>
            /// Create an include instruction
            /// </summary>
            public IncludeInstruction(ResourceType type, String path)
            {
                this.Type = type;
                this.JoinPath = path;
            }

            /// <summary>
            /// Query instruction
            /// </summary>
            public IncludeInstruction(String queryInstruction)
            {
                var parsed = queryInstruction.Split(':');
                if (parsed.Length != 2)
                {
                    var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
                    var tracer = Tracer.GetTracer(typeof(ResourceHandlerBase<TFhirResource, TModel>));

                    tracer.TraceError($"{queryInstruction} is not a valid include instruction");
                    throw new ArgumentOutOfRangeException(localizationService.FormatString("error.type.InvalidDataException.userMessage", new
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
        protected Tracer m_traceSource = new Tracer(FhirConstants.TraceSourceName);

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
                throw new InvalidOperationException(this.m_localizationService.FormatString("error.type.InvalidDataException.userMessage", new
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
        /// Gets the canonical type
        /// </summary>
        public Type CanonicalType => typeof(TModel);

        /// <summary>
        /// Gets the CLR type
        /// </summary>
        public Type ResourceClrType => typeof(TFhirResource);

        /// <summary>
        /// Get service name
        /// </summary>
        public string ServiceName => "Resource Handler Base";

        /// <summary>
        /// Create the specified resource.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>FhirOperationResult.</returns>
        /// <exception cref="System.ArgumentNullException">target</exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.Data.SyntaxErrorException"></exception>
        public virtual Resource Create(Resource target, TransactionMode mode)
        {
            this.m_traceSource.TraceInfo("Creating resource {0} ({1})", this.ResourceType, target);

            if (target == null)
            {
                this.m_traceSource.TraceError($"Argument {nameof(target)} null or empty");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }
            else if (!(target is TFhirResource))
                throw new InvalidDataException(this.m_localizationService.GetString("error.type.InvalidDataException"));

            target = ExtensionUtil.ExecuteAfterReceiveRequestBehavior(TypeRestfulInteraction.Create, this.ResourceType, target);

            // We want to map from TFhirResource to TModel
            var modelInstance = this.MapToModel(target as TFhirResource);

            if (modelInstance == null)
                throw new ArgumentException(this.m_localizationService.FormatString("error.type.InvalidDataException.userMessage", new
                {
                    param = "Model"
                }));

            var result = this.Create(modelInstance, mode);

            // Return fhir operation result
            var retVal = this.MapToFhir(result);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Create, this.ResourceType, retVal);
        }

        /// <summary>
        /// Deletes a specified resource.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>FhirOperationResult.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        /// <exception cref="System.ArgumentException"></exception>
        public Resource Delete(string id, TransactionMode mode)
        {
            if (String.IsNullOrEmpty(id))
            {
                this.m_traceSource.TraceError($"Argument {nameof(id)} is null or empty");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            this.m_traceSource.TraceInfo("Deleting resource {0}/{1}", this.ResourceType, id);

            // Delete
            var guidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
                throw new ArgumentException(m_localizationService.FormatString("error.type.ArgumentException", new
                {
                    param = "id"
                }));

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
                Versioning = typeof(IVersionedEntity).IsAssignableFrom(typeof(TModel)) ?
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
        /// <param name="parameters">The parameters.</param>
        /// <returns>Returns the FHIR query result containing the results of the query.</returns>
        /// <exception cref="System.ArgumentNullException">parameters</exception>
        public virtual Bundle Query(System.Collections.Specialized.NameValueCollection parameters)
        {
            if (parameters == null)
            {
                this.m_traceSource.TraceError($"Argument {nameof(parameters)} null or empty");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            Core.Model.Query.NameValueCollection hdsiQuery = null;
            FhirQuery query = QueryRewriter.RewriteFhirQuery(typeof(TFhirResource), typeof(TModel), parameters, out hdsiQuery);

            // Do the query
            int totalResults = 0;
            var predicate = QueryExpressionParser.BuildLinqExpression<TModel>(hdsiQuery);
            var hdsiResults = this.Query(predicate, query.QueryId, query.Start, query.Quantity, out totalResults);

            var auth = AuthenticationContext.Current;
            // Return FHIR query result
            var retVal = new FhirQueryResult(typeof(TFhirResource).Name)
            {
                Results = hdsiResults.Select(o =>
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

        /// <summary>
        /// Process includes for the specified result set
        /// </summary>
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
        /// <param name="id">The identifier.</param>
        /// <param name="versionId">The version identifier.</param>
        /// <returns>Returns the FHIR operation result containing the retrieved resource.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException"></exception>
        public Resource Read(string id, string versionId)
        {
            if (String.IsNullOrEmpty(id))
            {
                this.m_traceSource.TraceError($"Argument {nameof(id)} null or empty");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            Guid guidId = Guid.Empty, versionGuidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
                throw new ArgumentException(this.m_localizationService.FormatString("error.type.ArgumentException", new
                {
                    param = "id"
                }));
            if (!String.IsNullOrEmpty(versionId) && !Guid.TryParse(versionId, out versionGuidId))
                throw new ArgumentException(this.m_localizationService.FormatString("error.type.ArgumentException", new
                {
                    param = "versionId"
                }));

            var result = this.Read(guidId, versionGuidId);
            if (result == null)
            {
                this.m_traceSource.TraceError($"{this.ResourceType}/{id} not found");
                throw new KeyNotFoundException(this.m_localizationService.GetString("error.type.KeyNotFoundException"));
            }

            // FHIR Operation result
            var retVal = this.MapToFhir(result);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(String.IsNullOrEmpty(versionId) ? TypeRestfulInteraction.Read : TypeRestfulInteraction.Vread, this.ResourceType, retVal);
        }

        /// <summary>
        /// Updates the specified resource.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="target">The target.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>Returns the FHIR operation result containing the updated resource.</returns>
        /// <exception cref="System.ArgumentNullException">target</exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.Data.SyntaxErrorException"></exception>
        /// <exception cref="System.ArgumentException"></exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException"></exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException"></exception>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            this.m_traceSource.TraceInfo("Updating resource {0}/{1} ({2})", this.ResourceType, id, target);

            if (target == null)
            {
                this.m_traceSource.TraceError($"Argument {nameof(target)} is null or empty");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }
            else if (!(target is TFhirResource))
                throw new InvalidDataException(this.m_localizationService.GetString("error.type.InvalidDataException"));

            target = ExtensionUtil.ExecuteAfterReceiveRequestBehavior(TypeRestfulInteraction.Update, this.ResourceType, target);

            // We want to map from TFhirResource to TModel
            target.Id = id;
            var modelInstance = this.MapToModel(target as TFhirResource);
            if (modelInstance == null)
                throw new ArgumentException(this.m_localizationService.FormatString("error.type.InvalidDataException.userMessage", new
                {
                    param = "Request"
                }));

            // Guid identifier
            var guidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
                throw new ArgumentException(this.m_localizationService.FormatString("error.type.ArgumentException", new
                {
                    param = "id"
                }));

            // Model instance key does not equal path
            if (modelInstance.Key != Guid.Empty && modelInstance.Key != guidId)
                throw new InvalidOperationException(this.m_localizationService.GetString("error.messaging.fhir.resourceBase.key"));
            else if (modelInstance.Key == Guid.Empty)
                modelInstance.Key = guidId;

            var result = this.Update(modelInstance, mode);

            // Return fhir operation result
            var retVal = this.MapToFhir(result);
            return ExtensionUtil.ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction.Update, this.ResourceType, retVal);
        }

        /// <summary>
        /// Creates the specified model instance.
        /// </summary>
        /// <param name="modelInstance">The model instance.</param>
        /// <param name="issues">The issues.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>Returns the created model.</returns>
        protected abstract TModel Create(TModel modelInstance, TransactionMode mode);

        /// <summary>
        /// Deletes the specified model identifier.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="details">The details.</param>
        /// <returns>Returns the deleted model.</returns>
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
        /// Gets includes
        /// </summary>
        protected abstract IEnumerable<Resource> GetIncludes(TModel resource, IEnumerable<IncludeInstruction> includePaths);

        /// <summary>
        /// Gets the revers include paths
        /// </summary>
        protected abstract IEnumerable<Resource> GetReverseIncludes(TModel resource, IEnumerable<IncludeInstruction> reverseIncludePaths);

        /// <summary>
        /// Queries the specified query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="issues">The issues.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="totalResults">The total results.</param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        protected abstract IEnumerable<TModel> Query(Expression<Func<TModel, bool>> query, Guid queryId, int offset, int count, out int totalResults);

        /// <summary>
        /// Reads the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="details">The details.</param>
        /// <returns>Returns the model which matches the given id.</returns>
        protected abstract TModel Read(Guid id, Guid versionId);

        /// <summary>
        /// Updates the specified model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="details">The details.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>Returns the updated model.</returns>
        protected abstract TModel Update(TModel model, TransactionMode mode);

        /// <summary>
        /// Reads the complete history of the specified identifier
        /// </summary>
        public Bundle History(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                this.m_traceSource.TraceError($"Argument {nameof(id)} null or empty");
                throw new ArgumentNullException(this.m_localizationService.GetString("error.type.ArgumentNullException"));
            }

            Guid guidId = Guid.Empty;
            if (!Guid.TryParse(id, out guidId))
                throw new ArgumentException(this.m_localizationService.FormatString("error.type.ArgumentException", new
                {
                    param = "id"
                }));

            var result = this.Read(guidId, Guid.Empty);
            if (result == null)
                throw new KeyNotFoundException(this.m_localizationService.GetString("error.type.KeyNotFoundException"));

            // Results
            List<TModel> results = new List<TModel>() { result };
            while ((result as IVersionedEntity)?.PreviousVersionKey.HasValue == true)
            {
                result = this.Read(guidId, (result as IVersionedEntity).PreviousVersionKey.Value);
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
        public Resource MapToFhir(IdentifiedData modelInstance)
        {
            return this.MapToFhir((TModel)modelInstance);
        }

        /// <summary>
        /// Map the object to model
        /// </summary>
        public IdentifiedData MapToModel(Resource resourceInstance)
        {
            using (AuthenticationContext.EnterSystemContext()) // All queries under the mapping process are performed by the SYSTEM
            {
                var retVal = this.MapToModel((TFhirResource)resourceInstance);
                // Append the notice that this is a source model
                if (retVal is IResourceCollection irc)
                {
                    irc.AddAnnotationToAll(SanteDBConstants.NoDynamicLoadAnnotation);
                }
                else
                {
                    retVal.AddAnnotation(SanteDBConstants.NoDynamicLoadAnnotation);
                }
                return retVal;
            }
        }

        /// <summary>
        /// True if this handler can process the object
        /// </summary>
        public virtual bool CanMapObject(object instance) => instance is TModel || instance is TFhirResource;
    }
}
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
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Auditing;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interop;
using SanteDB.Core.Interop.Description;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using SanteDB.Rest.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml.Schema;
using System.Xml.Serialization;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Rest
{
    /// <summary>
    /// HL7 Fast Health Interoperability Resources (FHIR) R4
    /// </summary>
    /// <remarks>SanteSB Server implementation of the HL7 FHIR R4 Contract</remarks>
    [ServiceBehavior(Name = "FHIR", InstanceMode = ServiceInstanceMode.PerCall)]
    public class FhirServiceBehavior : IFhirServiceContract, IServiceBehaviorMetadataProvider
    {

        private Tracer m_tracer = new Tracer(FhirConstants.TraceSourceName);

        #region IFhirServiceContract Members

        /// <summary>
        /// Get schema
        /// </summary>
        public XmlSchema GetSchema(int schemaId)
        {
            this.ThrowIfNotReady();

            XmlSchemas schemaCollection = new XmlSchemas();

            XmlReflectionImporter importer = new XmlReflectionImporter("http://hl7.org/fhir");
            XmlSchemaExporter exporter = new XmlSchemaExporter(schemaCollection);

            foreach (var cls in typeof(FhirServiceBehavior).Assembly.GetTypes().Where(o => o.GetCustomAttribute<XmlRootAttribute>() != null && !o.IsGenericTypeDefinition))
                exporter.ExportTypeMapping(importer.ImportTypeMapping(cls, "http://hl7.org/fhir"));

            return schemaCollection[schemaId];
        }

        /// <summary>
        /// Get the index
        /// </summary>
        public Stream Index()
        {
            this.ThrowIfNotReady();

            try
            {
                RestOperationContext.Current.OutgoingResponse.ContentType = "text/html";
                RestOperationContext.Current.OutgoingResponse.Headers.Add("Content-Disposition", "filename=\"index.html\"");
                RestOperationContext.Current.OutgoingResponse.SetLastModified(DateTime.UtcNow);
                FhirServiceConfigurationSection config = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<FhirServiceConfigurationSection>();
                if (!String.IsNullOrEmpty(config.LandingPage))
                {
                    using (var fs = File.OpenRead(config.LandingPage))
                    {
                        MemoryStream ms = new MemoryStream();
                        int br = 1024;
                        byte[] buffer = new byte[1024];
                        while (br == 1024)
                        {
                            br = fs.Read(buffer, 0, 1024);
                            ms.Write(buffer, 0, br);
                        }
                        ms.Seek(0, SeekOrigin.Begin);
                        return ms;
                    }
                }
                else
                    return typeof(FhirServiceBehavior).Assembly.GetManifestResourceStream("SanteDB.Messaging.FHIR.index.htm");
            }
            catch (IOException)
            {
                throw new FileNotFoundException();
            }
        }

        /// <summary>
        /// Read a reasource
        /// </summary>
        public Resource ReadResource(string resourceType, string id)
        {
            this.ThrowIfNotReady();

            try
            {

                // Setup outgoing content
                var result = this.PerformRead(resourceType, id, null);
                String baseUri = RestOperationContext.Current.IncomingRequest.Url.AbsoluteUri;
                RestOperationContext.Current.OutgoingResponse.Headers.Add("Content-Location", String.Format("{0}/_history/{1}", baseUri, result.VersionId));
                return result;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error reading FHIR resource {0}({1}): {2}", resourceType, id, e);
                throw;
            }
        }

        /// <summary>
        /// Read resource with version
        /// </summary>
        public Resource VReadResource(string resourceType, string id, string vid)
        {
            this.ThrowIfNotReady();

            try
            {
                // Setup outgoing content
                var result = this.PerformRead(resourceType, id, vid);
                return result;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error vreading FHIR resource {0}({1},{2}): {3}", resourceType, id, vid, e);
                throw;
            }
        }

        /// <summary>
        /// Update a resource
        /// </summary>
        public Resource UpdateResource(string resourceType, string id, Resource target)
        {
            this.ThrowIfNotReady();

            try
            {

                // Setup outgoing content/

                // Create or update?
                var handler = FhirResourceHandlerUtil.GetResourceHandler(resourceType);
                if (handler == null)
                    throw new FileNotFoundException(); // endpoint not found!

                var result = handler.Update(id, target, TransactionMode.Commit);

                this.AuditDataAction(TypeRestfulInteraction.Update, OutcomeIndicator.Success, result);

                String baseUri = MessageUtil.GetBaseUri();
                RestOperationContext.Current.OutgoingResponse.Headers.Add("Content-Location", String.Format("{0}/{1}/_history/{2}", baseUri, result.Id, result.VersionId));
                RestOperationContext.Current.OutgoingResponse.SetLastModified(result.Meta.LastUpdated.Value.DateTime);
                RestOperationContext.Current.OutgoingResponse.SetETag($"W/\"{result.VersionId}\"");

                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error updating FHIR resource {0}({1}): {2}", resourceType, id, e);
                this.AuditDataAction(TypeRestfulInteraction.Update, OutcomeIndicator.MinorFail);
                throw;
            }
        }

        /// <summary>
        /// Delete a resource
        /// </summary>
        public Resource DeleteResource(string resourceType, string id)
        {
            this.ThrowIfNotReady();

            try
            {

                // Setup outgoing content/
                RestOperationContext.Current.OutgoingResponse.StatusCode = (int)HttpStatusCode.NoContent;

                // Create or update?
                var handler = FhirResourceHandlerUtil.GetResourceHandler(resourceType);
                if (handler == null)
                    throw new FileNotFoundException(); // endpoint not found!

                var result = handler.Delete(id, TransactionMode.Commit);

                this.AuditDataAction(TypeRestfulInteraction.Delete, OutcomeIndicator.Success, result);
                return null;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error deleting FHIR resource {0}({1}): {2}", resourceType, id, e);
                this.AuditDataAction(TypeRestfulInteraction.Delete, OutcomeIndicator.MinorFail);
                throw;
            }
        }

        /// <summary>
        /// Create a resource
        /// </summary>
        public Resource CreateResource(string resourceType, Resource target)
        {
            this.ThrowIfNotReady();
            try
            {

                // Setup outgoing content

                // Create or update?
                var handler = FhirResourceHandlerUtil.GetResourceHandler(resourceType);
                if (handler == null)
                    throw new FileNotFoundException(); // endpoint not found!

                var result = handler.Create(target, TransactionMode.Commit);
                RestOperationContext.Current.OutgoingResponse.StatusCode = (int)HttpStatusCode.Created;


                this.AuditDataAction(TypeRestfulInteraction.Create, OutcomeIndicator.Success, result);

                String baseUri = MessageUtil.GetBaseUri();
                if (!(result is Bundle))
                {
                    RestOperationContext.Current.OutgoingResponse.Headers.Add("Content-Location", String.Format("{0}/{1}/{2}/_history/{3}", baseUri, resourceType, result.Id, result.VersionId));
                    RestOperationContext.Current.OutgoingResponse.SetLastModified(result.Meta.LastUpdated.Value.DateTime);
                    RestOperationContext.Current.OutgoingResponse.SetETag($"W/\"{result.VersionId}\"");
                }

                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error creating FHIR resource {0}: {1}", resourceType, e);
                this.AuditDataAction(TypeRestfulInteraction.Create, OutcomeIndicator.Success);
                throw;
            }
        }

        /// <summary>
        /// Validate a resource (really an update with debugging / non comit)
        /// </summary>
        public OperationOutcome ValidateResource(string resourceType, string id, Resource target)
        {
            this.ThrowIfNotReady();
            try
            {

                // Setup outgoing content

                // Create or update?
                var handler = FhirResourceHandlerUtil.GetResourceHandler(resourceType);
                if (handler == null)
                    throw new FileNotFoundException(); // endpoint not found!

                var result = handler.Update(id, target, TransactionMode.Rollback);
                if (result == null) // Create
                {
                    result = handler.Create(target, TransactionMode.Rollback);
                    RestOperationContext.Current.OutgoingResponse.StatusCode = (int)HttpStatusCode.Created;
                }

                // Return constraint
                return new OperationOutcome()
                {
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new OperationOutcome.IssueComponent() { Severity = OperationOutcome.IssueSeverity.Information, Diagnostics = "Resource validated" }
                    }
                };
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error validating FHIR resource: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Searches a resource from the client registry datastore 
        /// </summary>
        public Bundle SearchResource(string resourceType)
        {
            this.ThrowIfNotReady();

            try
            {

                // Get query parameters
                var queryParameters = RestOperationContext.Current.IncomingRequest.Url;
                var resourceProcessor = FhirResourceHandlerUtil.GetResourceHandler(resourceType);

                // Setup outgoing content
                RestOperationContext.Current.OutgoingResponse.SetLastModified(DateTime.Now);

                if (resourceProcessor == null) // Unsupported resource
                    throw new FileNotFoundException();

                // TODO: Appropriately format response
                // Process incoming request
                var result = resourceProcessor.Query(RestOperationContext.Current.IncomingRequest.QueryString);

                this.AuditDataAction(TypeRestfulInteraction.SearchType, OutcomeIndicator.Success, result.Entry.Select(o=>o.Resource).ToArray());
                // Create the Atom feed
                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error searching FHIR resource {0}: {1}", resourceType, e);
                this.AuditDataAction(TypeRestfulInteraction.SearchType, OutcomeIndicator.MinorFail);
                throw;
            }

        }

        /// <summary>
        /// Audit data action on FHIR interface
        /// </summary>
        private void AuditDataAction(TypeRestfulInteraction type, OutcomeIndicator outcome, params Resource[] objects)
        {
            AuditData audit = new AuditData(DateTime.Now, ActionType.Execute, outcome, EventIdentifierType.ApplicationActivity, new AuditCode(Hl7.Fhir.Utility.EnumUtility.GetLiteral(type), "http://hl7.org/fhir/ValueSet/type-restful-interaction"));
            AuditableObjectLifecycle lifecycle = AuditableObjectLifecycle.NotSet;
            switch(type)
            {
                case TypeRestfulInteraction.Create:
                    audit.ActionCode = ActionType.Create;
                    audit.EventIdentifier = EventIdentifierType.Import;
                    lifecycle = AuditableObjectLifecycle.Creation;
                    break;
                case TypeRestfulInteraction.Delete:
                    audit.ActionCode = ActionType.Delete;
                    audit.EventIdentifier = EventIdentifierType.Import;
                    lifecycle = AuditableObjectLifecycle.LogicalDeletion;
                    break;
                case TypeRestfulInteraction.HistoryInstance:
                case TypeRestfulInteraction.HistoryType:
                case TypeRestfulInteraction.SearchType:
                    audit.ActionCode = ActionType.Execute;
                    audit.EventIdentifier = EventIdentifierType.Query;
                    lifecycle = AuditableObjectLifecycle.Disclosure;
                    audit.AuditableObjects.Add(new AuditableObject()
                    {
                        QueryData = RestOperationContext.Current?.IncomingRequest.Url.ToString(),
                        Role = AuditableObjectRole.Query,
                        Type = AuditableObjectType.SystemObject,
                        ObjectData = RestOperationContext.Current?.IncomingRequest.Headers.AllKeys.Where(o=>o.Equals("accept", StringComparison.OrdinalIgnoreCase)).Select(o=>new ObjectDataExtension(o, RestOperationContext.Current.IncomingRequest.Headers.Get(o))).ToList()
                    });
                    break;
                case TypeRestfulInteraction.Update:
                case TypeRestfulInteraction.Patch:
                    audit.ActionCode = ActionType.Update;
                    audit.EventIdentifier = EventIdentifierType.Import;
                    lifecycle = AuditableObjectLifecycle.Amendment;
                    break;
                case TypeRestfulInteraction.Vread:
                case TypeRestfulInteraction.Read:
                    audit.ActionCode = ActionType.Read;
                    audit.EventIdentifier = EventIdentifierType.Query;
                    lifecycle = AuditableObjectLifecycle.Disclosure;
                    audit.AuditableObjects.Add(new AuditableObject()
                    {
                        QueryData = RestOperationContext.Current?.IncomingRequest.Url.ToString(),
                        Role = AuditableObjectRole.Query,
                        Type = AuditableObjectType.SystemObject,
                        ObjectData = RestOperationContext.Current?.IncomingRequest.Headers.AllKeys.Where(o => o.Equals("accept", StringComparison.OrdinalIgnoreCase)).Select(o => new ObjectDataExtension(o, RestOperationContext.Current.IncomingRequest.Headers.Get(o))).ToList()
                    });
                    break;
            }

            AuditUtil.AddLocalDeviceActor(audit);
            AuditUtil.AddUserActor(audit);

            audit.AuditableObjects.AddRange(objects.SelectMany(o=>this.CreateAuditObjects(o,lifecycle)));

            AuditUtil.SendAudit(audit);
        }

        /// <summary>
        /// Get conformance
        /// </summary>
        public CapabilityStatement GetOptions()
        {
            this.ThrowIfNotReady();
            var retVal = ConformanceUtil.GetConformanceStatement();
            RestOperationContext.Current.OutgoingResponse.Headers.Add("Content-Location", String.Format("{0}metadata", RestOperationContext.Current.IncomingRequest.Url));
            RestOperationContext.Current.OutgoingResponse.StatusCode = (int)HttpStatusCode.OK;
            return retVal;
        }

        /// <summary>
        /// Posting transaction is not supported
        /// </summary>
        public Bundle PostTransaction(Bundle feed)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get a resource's history
        /// </summary>
        public Bundle GetResourceInstanceHistory(string resourceType, string id)
        {
            this.ThrowIfNotReady();
            // Stuff for auditing and exception handling
            try
            {

                // Get query parameters
                var queryParameters = RestOperationContext.Current.IncomingRequest.QueryString;
                var resourceProcessor = FhirResourceHandlerUtil.GetResourceHandler(resourceType);

                if (resourceProcessor == null) // Unsupported resource
                    throw new FileNotFoundException("Specified resource type is not found");

                // TODO: Appropriately format response
                // Process incoming request
                var result = resourceProcessor.History(id);
                this.AuditDataAction(TypeRestfulInteraction.HistoryInstance, OutcomeIndicator.Success, result.Entry.Select(o => o.Resource).ToArray());

                // Create the result
                RestOperationContext.Current.OutgoingResponse.SetLastModified(result.Meta.LastUpdated?.DateTime ?? DateTime.Now);
                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting FHIR resource history {0}({1}): {2}", resourceType, id, e);
                this.AuditDataAction(TypeRestfulInteraction.HistoryInstance, OutcomeIndicator.MinorFail);
                throw;
            }
        }

        /// <summary>
        /// Not implemented result
        /// </summary>
        public Bundle GetResourceHistory(string resourceType)
        {
            this.ThrowIfNotReady();

            throw new NotSupportedException("For security reasons resource history is not supported");

        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public Bundle GetHistory(string mimeType)
        {
            this.ThrowIfNotReady();

            throw new NotSupportedException("For security reasons system history is not supported");
        }


        /// <summary>
        /// Perform a read against the underlying IFhirResourceHandler
        /// </summary>
        private Resource PerformRead(string resourceType, string id, string vid)
        {
            this.ThrowIfNotReady();

            // Stuff for auditing and exception handling

            try
            {

                // Get query parameters
                var queryParameters = RestOperationContext.Current.IncomingRequest.QueryString;
                var resourceProcessor = FhirResourceHandlerUtil.GetResourceHandler(resourceType);

                if (resourceProcessor == null) // Unsupported resource
                    throw new FileNotFoundException("Specified resource type is not found");

                // TODO: Appropriately format response
                // Process incoming request
                var result = resourceProcessor.Read(id, vid);

                this.AuditDataAction(String.IsNullOrEmpty(vid) ? TypeRestfulInteraction.Read : TypeRestfulInteraction.Vread, OutcomeIndicator.Success, result);

                // Create the result
                RestOperationContext.Current.OutgoingResponse.SetLastModified(result.Meta.LastUpdated.Value.DateTime);
                RestOperationContext.Current.OutgoingResponse.SetETag($"W/\"{result.VersionId}\"");

                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error reading FHIR resource {0}({1},{2}): {3}", resourceType, id, vid, e);
                this.AuditDataAction(String.IsNullOrEmpty(vid) ? TypeRestfulInteraction.Read : TypeRestfulInteraction.Vread, OutcomeIndicator.MinorFail);
                throw;
            }
        }

        /// <summary>
        /// Get meta-data
        /// </summary>
        public CapabilityStatement GetMetaData()
        {
            return this.GetOptions();
        }

        /// <summary>
        /// Get the current time
        /// </summary>
        public DateTime Time()
        {
            return DateTime.Now;
        }

        #endregion

        /// <summary>
        /// Create or update
        /// </summary>
        public Resource CreateUpdateResource(string resourceType, string id, Resource target)
        {
            return this.UpdateResource(resourceType, id, target);
        }

        /// <summary>
        /// Alternate search
        /// </summary>
        public Bundle SearchResourceAlt(string resourceType)
        {
            return this.SearchResource(resourceType);
        }

        /// <summary>
        /// Throws an exception if the service is not yet ready
        /// </summary>
        private void ThrowIfNotReady()
        {
            if (!ApplicationServiceContext.Current.IsRunning)
                throw new DomainStateException();

        }

        /// <summary>
        /// Executes the specified operation name on the specified resource type
        /// </summary>
        /// <param name="resourceType">The type of resource this operation </param>
        /// <param name="operationName">The name of the operation</param>
        /// <param name="parameters">The parameters for the operation</param>
        /// <returns>The result of executing the operation</returns>
        public Resource ExecuteOperationPost(string resourceType, string operationName, Parameters parameters)
        {
            this.ThrowIfNotReady();

            try
            {

                // Get the operation handler
                var handler = ExtensionUtil.GetOperation(resourceType, operationName);

                // No handler?
                if (handler == null)
                    throw new FileNotFoundException(); // endpoint not found!

                var result = handler.Invoke(parameters);
                this.AuditOperationAction(resourceType, operationName, OutcomeIndicator.Success, result);
                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error executing FHIR operation {0}/{1}: {2}", resourceType, operationName, e);
                this.AuditOperationAction(resourceType, operationName, OutcomeIndicator.MinorFail);
                throw;
            }

        }

        /// <summary>
        /// Executes the specified operation name on the specified resource type
        /// </summary>
        /// <param name="resourceType">The type of resource this operation </param>
        /// <param name="operationName">The name of the operation</param>
        /// <returns>The result of executing the operation</returns>
        public Resource ExecuteOperationGet(string resourceType, string operationName)
        {
            this.ThrowIfNotReady();

            try
            {

                // Get the operation handler
                var handler = ExtensionUtil.GetOperation(resourceType, operationName);

                // No handler?
                if (handler == null)
                    throw new FileNotFoundException(); // endpoint not found!

                var result = handler.Invoke(null);
                this.AuditOperationAction(resourceType, operationName, OutcomeIndicator.Success, result);

                return result;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error executing FHIR operation {0}/{1}: {2}", resourceType, operationName, e);
                this.AuditOperationAction(resourceType, operationName, OutcomeIndicator.MinorFail);
                throw;
            }

        }

        /// <summary>
        /// Audit an operation that was exceuted
        /// </summary>
        private void AuditOperationAction(string resourceType, string operationName, OutcomeIndicator outcome, params Resource[] objects)
        {
            var audit = new AuditData(DateTime.Now, ActionType.Execute, outcome, EventIdentifierType.ApplicationActivity, new AuditCode(Hl7.Fhir.Utility.EnumUtility.GetLiteral(SystemRestfulInteraction.Batch), "http://hl7.org/fhir/ValueSet/system-restful-interaction"));
            AuditUtil.AddLocalDeviceActor(audit);
            AuditUtil.AddUserActor(audit);

            var handler = ExtensionUtil.GetOperation(resourceType, operationName);

            audit.AuditableObjects.Add(new AuditableObject()
            {
                IDTypeCode = AuditableObjectIdType.Uri,
                ObjectId = handler?.Uri.ToString() ?? $"urn:uuid:{Guid.Empty}",
                QueryData = RestOperationContext.Current?.IncomingRequest.Url.ToString(),
                ObjectData = RestOperationContext.Current?.IncomingRequest.Headers.AllKeys.Select(o => new ObjectDataExtension(o, RestOperationContext.Current.IncomingRequest.Headers.Get(o))).ToList(),
                Role = AuditableObjectRole.Job,
                Type = AuditableObjectType.SystemObject
            });
                
            audit.AuditableObjects.AddRange(objects.SelectMany(o=>this.CreateAuditObjects(o, AuditableObjectLifecycle.NotSet)));
            AuditUtil.SendAudit(audit);

        }

        /// <summary>
        /// Create auditable object
        /// </summary>
        private IEnumerable<AuditableObject> CreateAuditObjects(Resource resource, AuditableObjectLifecycle lifecycle)
        {
            var obj = new AuditableObject()
            {
                ObjectId = $"urn:uuid:{resource.Id}",
                IDTypeCode = AuditableObjectIdType.Uri,
                LifecycleType = lifecycle
            };

            switch (resource.ResourceType)
            {
                case ResourceType.Patient:
                    obj.Type = AuditableObjectType.Person;
                    obj.Role = AuditableObjectRole.Patient;
                    obj.IDTypeCode = AuditableObjectIdType.PatientNumber;
                    obj.ObjectId = resource.Id;
                    return new AuditableObject[] { obj };
                case ResourceType.Organization:
                    obj.Type = AuditableObjectType.Organization;
                    obj.Role = AuditableObjectRole.Resource;
                    return new AuditableObject[] { obj };

                case ResourceType.Practitioner:
                    obj.Type = AuditableObjectType.Person;
                    obj.Role = AuditableObjectRole.Provider;
                    return new AuditableObject[] { obj };
                case ResourceType.Bundle:
                    return (resource as Bundle).Entry.SelectMany(o => CreateAuditObjects(o.Resource, lifecycle));
                default:
                    return new AuditableObject[0];
            }

        }

        /// <summary>
        /// Execute operation on the global (no resource) context
        /// </summary>
        public Resource Execute(string operationName, Parameters parameters)
        {
            return this.ExecuteOperationPost(null, operationName, parameters);
        }


        /// <summary>
        /// Get the description of the service
        /// </summary>
        public ServiceDescription Description
        {
            get
            {
                ServiceDescription retVal = new ServiceDescription();
                String[] acceptProduces = new string[] { "application/fhir+json", "application/fhir+xml" };

                foreach (var def in FhirResourceHandlerUtil.GetRestDefinition())
                {
                    foreach (var op in def.Interaction)
                    {

                        ServiceOperationDescription operationDescription = null;
                        switch (op.Code.Value)
                        {
                            case CapabilityStatement.TypeRestfulInteraction.Create:
                                operationDescription = new ServiceOperationDescription("POST", $"/{def.Type.Value}", acceptProduces, true);
                                operationDescription.Responses.Add(HttpStatusCode.Created, def.Type.Value.CreateDescription());
                                operationDescription.Parameters.Add(new OperationParameterDescription("body", def.Type.Value.CreateDescription(), OperationParameterLocation.Body));
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.Delete:
                                operationDescription = new ServiceOperationDescription("DELETE", $"/{def.Type.Value}/{{id}}", acceptProduces, true);
                                operationDescription.Responses.Add(HttpStatusCode.OK, def.Type.Value.CreateDescription());
                                operationDescription.Parameters.Add(new OperationParameterDescription("id", typeof(String), OperationParameterLocation.Path));
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.HistoryInstance:
                                operationDescription = new ServiceOperationDescription("GET", $"/{def.Type.Value}/{{id}}/_history", acceptProduces, true);
                                operationDescription.Responses.Add(HttpStatusCode.OK, ResourceType.Bundle.CreateDescription());
                                operationDescription.Parameters.Add(new OperationParameterDescription("id", typeof(String), OperationParameterLocation.Path));
                                operationDescription.Parameters.Add(new OperationParameterDescription("_pretty", typeof(bool), OperationParameterLocation.Query));
                                operationDescription.Parameters.Add(new OperationParameterDescription("_summary", typeof(String), OperationParameterLocation.Query));
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.HistoryType:
                                operationDescription = new ServiceOperationDescription("GET", $"/{def.Type.Value}/_history", acceptProduces, true);
                                operationDescription.Responses.Add(HttpStatusCode.OK, ResourceType.Bundle.CreateDescription());
                                operationDescription.Parameters.Add(new OperationParameterDescription("_pretty", typeof(bool), OperationParameterLocation.Query));
                                operationDescription.Parameters.Add(new OperationParameterDescription("_summary", typeof(String), OperationParameterLocation.Query));
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.Read:
                                operationDescription = new ServiceOperationDescription("GET", $"/{def.Type.Value}/{{id}}", acceptProduces, true);
                                operationDescription.Parameters.Add(new OperationParameterDescription("id", typeof(String), OperationParameterLocation.Path));
                                operationDescription.Parameters.Add(new OperationParameterDescription("_pretty", typeof(bool), OperationParameterLocation.Query));
                                operationDescription.Parameters.Add(new OperationParameterDescription("_summary", typeof(String), OperationParameterLocation.Query));
                                operationDescription.Responses.Add(HttpStatusCode.OK, def.Type.Value.CreateDescription());
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.SearchType:
                                operationDescription = new ServiceOperationDescription("GET", $"/{def.Type.Value}", acceptProduces, true);
                                operationDescription.Responses.Add(HttpStatusCode.OK, ResourceType.Bundle.CreateDescription());
                                foreach (var itm in def.SearchParam)
                                {
                                    var parmType = typeof(Object);
                                    switch (itm.Type.Value)
                                    {

                                        case SearchParamType.Date:
                                            parmType = typeof(DateTime);
                                            break;
                                        case SearchParamType.Number:
                                        case SearchParamType.Quantity:
                                            parmType = typeof(Int32);
                                            break;
                                        case SearchParamType.Reference:
                                        case SearchParamType.String:
                                        case SearchParamType.Composite:
                                        case SearchParamType.Token:
                                        case SearchParamType.Uri:
                                            parmType = typeof(String);
                                            break;
                                    }
                                    operationDescription.Parameters.Add(new OperationParameterDescription(itm.Name, parmType, OperationParameterLocation.Query));
                                }
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.Update:
                                operationDescription = new ServiceOperationDescription("PUT", $"/{def.Type.Value}/{{id}}", acceptProduces, true);
                                operationDescription.Parameters.Add(new OperationParameterDescription("id", typeof(String), OperationParameterLocation.Path));
                                operationDescription.Parameters.Add(new OperationParameterDescription("body", def.Type.Value.CreateDescription(), OperationParameterLocation.Body));
                                operationDescription.Responses.Add(HttpStatusCode.OK, def.Type.Value.CreateDescription());
                                break;
                            case CapabilityStatement.TypeRestfulInteraction.Vread:
                                operationDescription = new ServiceOperationDescription("GET", $"/{def.Type.Value}/{{id}}/_history/{{versionId}}", acceptProduces, true);
                                operationDescription.Parameters.Add(new OperationParameterDescription("id", typeof(String), OperationParameterLocation.Path));
                                operationDescription.Parameters.Add(new OperationParameterDescription("versionId", typeof(String), OperationParameterLocation.Path));
                                operationDescription.Responses.Add(HttpStatusCode.OK, def.Type.Value.CreateDescription());
                                break;
                        }

                        operationDescription.Tags.Add(def.Type.ToString());
                        operationDescription.Responses.Add(HttpStatusCode.InternalServerError, ResourceType.OperationOutcome.CreateDescription());
                        retVal.Operations.Add(operationDescription);
                    }

                    // Add operation handlers
                    foreach(var op in ExtensionUtil.OperationHandlers.Where(o=> o.AppliesTo?.Contains(def.Type.Value) == true))
                    {
                        var operationDescription = new ServiceOperationDescription(op.IsGet ? "GET" : "POST", $"/{def.Type.Value}/${op.Name}", acceptProduces, true);

                        if (!op.IsGet)
                        {
                            operationDescription.Parameters.Add(new OperationParameterDescription("parameters", ResourceType.Parameters.CreateDescription(), OperationParameterLocation.Body));
                        }
                        operationDescription.Responses.Add(HttpStatusCode.OK, def.Type.Value.CreateDescription());
                        operationDescription.Tags.Add(def.Type.ToString());
                        operationDescription.Responses.Add(HttpStatusCode.InternalServerError, ResourceType.OperationOutcome.CreateDescription());
                        
                        if(op.IsGet)
                        {
                            foreach(var i in op.Parameters)
                            {
                                operationDescription.Parameters.Add(new OperationParameterDescription(i.Key, typeof(String), OperationParameterLocation.Query));
                            }
                        }
                        retVal.Operations.Add(operationDescription);

                    }
                }

                retVal.Operations.Add(new ServiceOperationDescription("GET", "/CapabilityStatement", acceptProduces, true));
                retVal.Operations.Add(new ServiceOperationDescription("OPTIONS", "/", acceptProduces, false));

                // Add operation handlers
                foreach (var op in ExtensionUtil.OperationHandlers.Where(o => o.AppliesTo == null))
                {
                    var operationDescription = new ServiceOperationDescription("POST", $"/${op.Name}", acceptProduces, true);
                    operationDescription.Parameters.Add(new OperationParameterDescription("parameters", ResourceType.Parameters.CreateDescription(), OperationParameterLocation.Body));
                    operationDescription.Responses.Add(HttpStatusCode.OK, ResourceType.Bundle.CreateDescription());
                    operationDescription.Responses.Add(HttpStatusCode.InternalServerError, ResourceType.OperationOutcome.CreateDescription());
                    retVal.Operations.Add(operationDescription);

                    operationDescription = new ServiceOperationDescription("GET", $"/${op.Name}", acceptProduces, true);
                    operationDescription.Responses.Add(HttpStatusCode.OK, ResourceType.Bundle.CreateDescription());
                    operationDescription.Responses.Add(HttpStatusCode.InternalServerError, ResourceType.OperationOutcome.CreateDescription());
                    retVal.Operations.Add(operationDescription);
                }
                return retVal;
            }
        }
    }

}

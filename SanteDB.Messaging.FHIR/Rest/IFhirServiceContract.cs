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
 * Date: 2022-5-30
 */
using Hl7.Fhir.Model;
using RestSrvr.Attributes;
using SanteDB.Rest.Common;
using System;
using System.IO;
using System.Xml.Schema;

namespace SanteDB.Messaging.FHIR.Rest
{
    /// <summary>
    /// HL7 Fast Health Interoperability Resources (FHIR)
    /// </summary>
    /// <remarks>
    /// This contract provides a wrapper for HL7 Fast Health Interoperability Resources (FHIR) STU3 resources.
    /// </remarks>
    [ServiceContract(Name = "FHIR")]
    [ServiceConsumes("application/fhir+json")]
    [ServiceConsumes("application/fhir+xml")]
    [ServiceProduces("application/fhir+json")]
    [ServiceProduces("application/fhir+xml")]
    public interface IFhirServiceContract : IRestApiContractImplementation
    {

        /// <summary>
        /// Options for this service
        /// </summary>
        [RestInvoke(UriTemplate = "/", Method = "OPTIONS")]
        [Get("/CapabilityStatement")]
        CapabilityStatement GetOptions();

        /// <summary>
        /// Execute the specified operation name
        /// </summary>
        /// <param name="operationName">The name of the operation</param>
        /// <returns>The result of the operation</returns>
        /// <param name="parameters">The body to pass as a parameter to the operation</param>
        [Post("/${operationName}")]
        Resource Execute(string operationName, Parameters parameters);

        /// <summary>
        /// Gets the current time on the service
        /// </summary>
        /// <returns></returns>
        [Get("/time")]
        DateTime Time();


        /// <summary>
        /// Options for this service
        /// </summary>
        [RestInvoke(UriTemplate = "/metadata", Method = "GET")]
        CapabilityStatement GetMetaData();

        /// <summary>
        /// Read a resource
        /// </summary>
        [Get("/{resourceType}/{id}")]
        Resource ReadResource(string resourceType, string id);

        /// <summary>
        /// Version read a resource
        /// </summary>
        [Get("/{resourceType}/{id}/_history/{vid}")]
        Resource VReadResource(string resourceType, string id, string vid);

        /// <summary>
        /// Update a resource
        /// </summary>
        [RestInvoke(UriTemplate = "/{resourceType}/{id}", Method = "PUT")]
        Resource UpdateResource(string resourceType, string id, Resource target);

        /// <summary>
        /// Delete a resource
        /// </summary>
        [RestInvoke(UriTemplate = "/{resourceType}/{id}", Method = "DELETE")]
        Resource DeleteResource(string resourceType, string id);

        /// <summary>
        /// Create a resource
        /// </summary>
        [RestInvoke(UriTemplate = "/{resourceType}", Method = "POST")]
        Resource CreateResource(string resourceType, Resource target);

        /// <summary>
        /// Execute the specified operation name
        /// </summary>
        /// <param name="resourceType">The type of resource the operation is on</param>
        /// <param name="operationName">The name of the operation</param>
        /// <returns>The result of the operation</returns>
        /// <param name="parameters">The body to pass as a parameter to the operation</param>
        [Post("/{resourceType}/${operationName}")]
        Resource ExecuteOperationPost(string resourceType, string operationName, Parameters parameters);

        /// <summary>
        /// Execute the specified operation name using a GET
        /// </summary>
        /// <param name="resourceType">The type of resource the operation is on</param>
        /// <param name="operationName">The name of the operation</param>
        /// <returns>The result of the operation</returns>
        [Get("/{resourceType}/${operationName}")]
        Resource ExecuteOperationGet(string resourceType, string operationName);

        /// <summary>
        /// Create a resource
        /// </summary>
        [RestInvoke(UriTemplate = "/{resourceType}/{id}", Method = "POST")]
        Resource CreateUpdateResource(string resourceType, string id, Resource target);

        /// <summary>
        /// Validate a resource
        /// </summary>
        [RestInvoke(UriTemplate = "/{resourceType}/_validate/{id}", Method = "POST")]
        OperationOutcome ValidateResource(string resourceType, string id, Resource target);

        /// <summary>
        /// Version read a resource
        /// </summary>
        [Get("/{resourceType}")]
        Bundle SearchResource(string resourceType);


        /// <summary>
        /// Version read a resource
        /// </summary>
        [Get("/{resourceType}/_search")]
        Bundle SearchResourceAlt(string resourceType);

        /// <summary>
        /// Post a transaction
        /// </summary>
        [RestInvoke(UriTemplate = "/", Method = "POST")]
        Bundle PostTransaction(Bundle feed);

        /// <summary>
        /// Get history
        /// </summary>
        [Get("/{resourceType}/{id}/_history")]
        Bundle GetResourceInstanceHistory(string resourceType, string id);

        /// <summary>
        /// Get history
        /// </summary>
        [Get("/{resourceType}/_history")]
        Bundle GetResourceHistory(string resourceType);

        /// <summary>
        /// Get history for all
        /// </summary>
        [Get("/_history")]
        Bundle GetHistory(string mimeType);

        /// <summary>
        /// Get index page
        /// </summary>
        [Get("/")]
        Stream Index();

    }

}

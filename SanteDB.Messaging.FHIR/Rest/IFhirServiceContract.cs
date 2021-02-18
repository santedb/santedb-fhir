﻿/*
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
using RestSrvr.Attributes;
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
    [ServiceProduces("application/fhir+json")]
    [ServiceProduces("application/fhir+xml")]
    [ServiceConsumes("application/fhir+json")]
    [ServiceConsumes("application/fhir+xml")]
    public interface IFhirServiceContract
    {

     
        /// <summary>
        /// Get the schema
        /// </summary>
        [Get("/?xsd={schemaId}")]
        XmlSchema GetSchema(int schemaId);

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
        /// Options for this service
        /// </summary>
        [RestInvoke(UriTemplate = "/", Method = "OPTIONS")]
        CapabilityStatement GetOptions();


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

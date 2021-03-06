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
using SanteDB.Core.Model;
using System;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a resource handler than can map objects
    /// </summary>
    public interface IFhirResourceMapper : IFhirResourceHandler
    {

        /// <summary>
        /// Gets the canonical type
        /// </summary>
        Type CanonicalType { get; }

        /// <summary>
        /// Map <paramref name="modelInstance"/> to FHIR
        /// </summary>
        /// <param name="modelInstance">The object to map to fhir</param>
        /// <returns>The mapped FHIR instance</returns>
        Resource MapToFhir(IdentifiedData modelInstance);

        /// <summary>
        /// Map the specified <paramref name="resourceInstance"/> to model
        /// </summary>
        /// <param name="resourceInstance">The resource to map</param>
        /// <returns>The model instance</returns>
        IdentifiedData MapToModel(Resource resourceInstance);

    }
}
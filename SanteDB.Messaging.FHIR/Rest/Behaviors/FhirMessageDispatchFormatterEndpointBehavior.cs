﻿/*
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
 * Date: 2023-6-21
 */
using RestSrvr;
using SanteDB.Messaging.FHIR.Rest.Serialization;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.FHIR.Rest.Behavior
{
    /// <summary>
    /// Dispatch formatter behavior
    /// </summary>
    [ExcludeFromCodeCoverage]
    [DisplayName("FHIR R4 Message Format Serialization")]
    public class FhirMessageDispatchFormatterEndpointBehavior : IEndpointBehavior, IOperationBehavior
    {
        /// <summary>
        /// Apply the behavior
        /// </summary>
        public void ApplyEndpointBehavior(ServiceEndpoint endpoint, EndpointDispatcher dispatcher)
        {
            foreach (var op in endpoint.Description.Contract.Operations)
            {
                op.AddOperationBehavior(this);
            }
        }

        /// <summary>
        /// Apply operation behavior
        /// </summary>
        public void ApplyOperationBehavior(EndpointOperation operation, OperationDispatcher dispatcher)
        {
            dispatcher.DispatchFormatter = new FhirMessageDispatchFormatter();
        }
    }
}

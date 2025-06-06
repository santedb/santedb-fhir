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
using Hl7.Fhir.Model;
using System;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// FHIR Message Operations
    /// </summary>
    public interface IFhirMessageOperation
    {

        /// <summary>
        /// Gets the event URI which this operation handles
        /// </summary>
        Uri EventUri { get; }

        /// <summary>
        /// Invoke this message with the specified request header component and the non-message infrastructure related entries
        /// </summary>
        /// <param name="requestHeader">The request header indicating the transaction details</param>
        /// <param name="entries">The entries for the operation</param>
        /// <returns>The response details for the message</returns>
        Resource Invoke(MessageHeader requestHeader, params Bundle.EntryComponent[] entries);
    }
}

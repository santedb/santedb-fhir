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
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Extensions
{

    /// <summary>
    /// This interface allows implementers to modify the behavior of the 
    /// the core FHIR service. This is useful if you wish to follow certain 
    /// specification rules which specify known return codes, etc.
    /// </summary>
    public interface IFhirRestBehaviorModifier
    {

        /// <summary>
        /// Determines whether this behavior applies
        /// </summary>
        /// <param name="interaction">The interaction that is being executed</param>
        /// <param name="resource">The resource which is being actioned on/returned</param>
        /// <returns>True if this behavior is interested in the resource</returns>
        bool CanApply(TypeRestfulInteraction interaction, Resource resource);

        /// <summary>
        /// Called when any FHIR operation is being invoked
        /// </summary>
        /// <param name="requestResource">The resource that is being created</param>
        /// <param name="resourceType">The resource that is being operated</param>
        /// <param name="interaction">The interaction that is being executed</param>
        /// <returns>A modified resource</returns>
        /// <exception cref="SanteDB.Core.Exceptions.DetectedIssueException">If the processing is to be stopped (with reasons why processing was halted)</exception>
        Resource AfterReceiveRequest(TypeRestfulInteraction interaction, ResourceType resourceType, Resource requestResource);

        /// <summary>
        /// Called before any FHIR operation returns
        /// </summary>
        /// <param name="interaction">The interaction that was executed</param>
        /// <param name="resourceType">The resource that is being operated</param>
        /// <param name="responseResource">The response resource</param>
        /// <returns>The modified/updated resource</returns>
        Resource BeforeSendResponse(TypeRestfulInteraction interaction, ResourceType resourceType, Resource responseResource);

    }
}

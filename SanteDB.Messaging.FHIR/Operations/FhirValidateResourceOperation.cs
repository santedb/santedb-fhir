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
 * User: fyfej
 * Date: 2023-6-21
 */
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Operations
{
    /// <summary>
    /// FHIR Operation for validating resources
    /// </summary>
    public class FhirValidateResourceOperation : IFhirOperationHandler
    {
        /// <summary>
        /// Gets the name
        /// </summary>
        public string Name => "validate";

        /// <summary>
        /// Applies to all resources
        /// </summary>
        public ResourceType[] AppliesTo => FhirResourceHandlerUtil.ResourceHandlers.Select(o => o.ResourceType).Distinct().ToArray();


        /// <summary>
        /// Gets the URI to the definition
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/OperationDefinition/Resource-validate");

        /// <summary>
        /// True if this impacts state
        /// </summary>
        public bool IsGet => false;

        /// <summary>
        /// Get the parameters
        /// </summary>
        public IDictionary<string, FHIRAllTypes> Parameters => new Dictionary<String, FHIRAllTypes>()
        {
            { "resource", FHIRAllTypes.Any }
        };

        /// <summary>
        /// Invoke the operation
        /// </summary>
        public Resource Invoke(Parameters parameters)
        {
            var resource = parameters.Parameter.FirstOrDefault(o => o.Name == "resource");
            var profile = parameters.Parameter.FirstOrDefault(o => o.Name == "profile");
            var mode = parameters.Parameter.FirstOrDefault(o => o.Name == "mode");

            var retVal = new OperationOutcome();

            // Get the profile handler for the specified profile, if no profile then just perform a profile mode
            if (!resource.Resource.TryDeriveResourceType(out ResourceType rt))
            {
                retVal.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.NotSupported,
                    Severity = OperationOutcome.IssueSeverity.Fatal,
                    Diagnostics = $"Resource {resource.Resource.TypeName} not supported"
                });
            }
            else
            {
                var hdlr = FhirResourceHandlerUtil.GetMapperForInstance(rt);
                if (hdlr == null)
                {
                    retVal.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Code = OperationOutcome.IssueType.NotSupported,
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Diagnostics = $"Resource {resource.Resource.TypeName} not supported"
                    });
                }
                else
                {
                    // Find all profiles and validate
                    if (profile?.Value != null)
                    {
                        retVal.Issue = ExtensionUtil.ProfileHandlers.Where(o => (profile.Value as FhirUri).Value == o.ProfileUri.ToString()).Select(o => o.Validate(resource.Resource))
                            .SelectMany(i => i.Select(o => DataTypeConverter.ToIssue(o))).ToList();
                    }

                    try
                    {

                        // Instruct the handler to map to RIM and then to call BRE validation
                        var rimModel = hdlr.MapToModel(resource.Resource);
                        retVal.Issue.AddRange(ApplicationServiceContext.Current.GetBusinessRuleService(rimModel.GetType())?.Validate(hdlr)?.Select(o => new OperationOutcome.IssueComponent()
                        {
                            Diagnostics = o.Text,
                            Severity = o.Priority == Core.BusinessRules.DetectedIssuePriorityType.Error ? OperationOutcome.IssueSeverity.Error : o.Priority == Core.BusinessRules.DetectedIssuePriorityType.Warning ? OperationOutcome.IssueSeverity.Warning : OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.BusinessRule
                        }) ?? new OperationOutcome.IssueComponent[0]);

                        //hdlr.Update(resource.Resource.Id, resource.Resource, Core.Services.TransactionMode.Rollback);

                        retVal.Issue.Add(new OperationOutcome.IssueComponent()
                        {
                            Diagnostics = "Validation Completed Successfully",
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Unknown
                        });
                    }
                    catch (DetectedIssueException e)
                    {
                        retVal.Issue.AddRange(e.Issues.Select(o => DataTypeConverter.ToIssue(o)));
                    }
                    catch (Exception e)
                    {
                        retVal.Issue.Add(new OperationOutcome.IssueComponent()
                        {
                            Severity = OperationOutcome.IssueSeverity.Error,
                            Code = OperationOutcome.IssueType.NoStore,
                            Diagnostics = e.Message
                        });
                    }
                }
            }
            return retVal;

        }
    }
}

using Hl7.Fhir.Model;
using SanteDB.Core.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// Invoke the operation
        /// </summary>
        public Resource Invoke(Parameters parameters)
        {
            var resource = parameters.Parameter.FirstOrDefault(o => o.Name == "resource");
            var profile = parameters.Parameter.FirstOrDefault(o => o.Name == "profile");
            var mode = parameters.Parameter.FirstOrDefault(o => o.Name == "mode");

            var retVal = new OperationOutcome();

            // Get the profile handler for the specified profile, if no profile then just perform a profile mode
            var hdlr = FhirResourceHandlerUtil.GetResourceHandler(resource.Resource.ResourceType);
            if (hdlr == null)
                retVal.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.NotSupported,
                    Severity = OperationOutcome.IssueSeverity.Fatal,
                    Diagnostics = $"Resource {resource.Resource.ResourceType} not supported"
                });
            else
            {
                // Find all profiles and validate
                if (profile?.Value != null)
                    retVal.Issue = ExtensionUtil.ProfileHandlers.Where(o => (profile.Value as FhirUri).Value == o.ProfileUri.ToString()).Select(o => o.Validate(resource.Resource))
                        .SelectMany(i => i.Select(o => DataTypeConverter.ToIssue(o))).ToList();
                
                try
                {

                    // Instruct the handler to perform an update with 
                    hdlr.Update(resource.Resource.Id, resource.Resource, Core.Services.TransactionMode.Rollback);
                    retVal.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Diagnostics = "Resource Valid",
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
            return retVal;

        }
    }
}

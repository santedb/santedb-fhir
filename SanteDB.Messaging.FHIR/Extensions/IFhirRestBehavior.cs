using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Extensions
{

    /// <summary>
    /// This interface allows implementers to modify the behavior of the 
    /// the core FHIR service. This is useful if you wish to follow certain 
    /// specification rules which specify known return codes, etc.
    /// </summary>
    public interface IFhirRestBehavior
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
        /// <param name="interaction">The interaction that is being executed</param>
        /// <returns>A modified resource</returns>
        /// <exception cref="SanteDB.Core.Exceptions.DetectedIssueException">If the processing is to be stopped (with reasons why processing was halted)</exception>
        Resource AfterReceiveRequest(TypeRestfulInteraction interaction, Resource requestResource);

        /// <summary>
        /// Called before any FHIR operation returns
        /// </summary>
        /// <param name="interaction">The interaction that was executed</param>
        /// <param name="responseResource">The response resource</param>
        /// <returns>The modified/updated resource</returns>
        Resource BeforeSendResponse(TypeRestfulInteraction interaction, Resource responseResource);

    }
}

using Hl7.Fhir.Model;
using SanteDB.Core.BusinessRules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// Represents a simple profile handler which allows third party operations
    /// </summary>
    public interface IFhirProfileHandler
    {

        /// <summary>
        /// Gets the defined profile URI
        /// </summary>
        Uri ProfileUri { get; }

        /// <summary>
        /// Gets the type this applies to (or null if it applies to all)
        /// </summary>
        ResourceType? AppliesTo { get; }

        /// <summary>
        /// Gets the structure definition
        /// </summary>
        List<ElementDefinition> Differential { get; }

        /// <summary>
        /// Validate the resource and emit detected issues
        /// </summary>
        /// <param name="resource">The resource instance</param>
        /// <returns>The list of detected issues</returns>
        List<Core.BusinessRules.DetectedIssue> Validate(Resource resource);

    }
}

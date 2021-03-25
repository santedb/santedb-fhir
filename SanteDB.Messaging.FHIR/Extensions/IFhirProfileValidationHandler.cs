using Hl7.Fhir.Model;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// Represents a profile handler that can validate whether a resource conforms to 
    /// a profile.
    /// </summary>
    /// <remarks>
    /// This interface, in combination with one or more IFhirOperationHandler and IFhirExtensionHandler
    /// interfaces is used to implement/override custom domain specific profiles in FHIR
    /// </remarks>
    public interface IFhirProfileValidationHandler
    {

        /// <summary>
        /// Gets the defined profile URI
        /// </summary>
        Uri ProfileUri { get; }

        /// <summary>
        /// Gets the type this applies to (or null if it applies to all)
        /// </summary>
        IEnumerable<ResourceType> AppliesTo { get; }

        /// <summary>
        /// Gets the structure definition
        /// </summary>
        StructureDefinition Definition { get; }

        /// <summary>
        /// Validate the resource and emit detected issues
        /// </summary>
        /// <param name="resource">The resource instance</param>
        /// <returns>The list of detected issues</returns>
        List<Core.BusinessRules.DetectedIssue> Validate(Resource resource);


    }
}

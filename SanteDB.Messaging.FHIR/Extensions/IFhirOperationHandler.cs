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
    /// Represents an extension point 
    /// </summary>
    /// <remarks>
    /// This interface is used to extend the FHIR interface for FHIR operations (like $validate, $match, etc.)
    /// and allows plugins to add behaviors to the API layer.
    /// </remarks>
    public interface IFhirOperationHandler
    {

        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Get URL of the operation
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The type that this operation handler applies to (or null if it applies to all)
        /// </summary>
        ResourceType[] AppliesTo { get; }

        /// <summary>
        /// True if the operation impacts the object state
        /// </summary>
        bool IsGet { get; }

        /// <summary>
        /// Invoke the specified operation
        /// </summary>
        /// <param name="parameters">The parameter set to action</param>
        /// <returns>The result of the operation</returns>
        Resource Invoke(Parameters parameters);

    }
}

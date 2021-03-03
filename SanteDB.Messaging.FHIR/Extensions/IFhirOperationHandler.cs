using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Extensions
{
    /// <summary>
    /// Represents an extension point 
    /// </summary>
    public interface IFhirOperationHandler
    {

        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The type that this operation handler applies to (or null if it applies to all)
        /// </summary>
        ResourceType? AppliesTo { get; }

        /// <summary>
        /// Invoke the specified operation
        /// </summary>
        /// <param name="parameters">The parameter set to action</param>
        /// <returns>The result of the operation</returns>
        Resource Invoke(Parameters parameters);

    }
}

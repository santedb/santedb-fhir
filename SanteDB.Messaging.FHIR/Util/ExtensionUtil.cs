using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Profile utility which has methods for profile
    /// </summary>
    public static class ExtensionUtil
    {

        // Handlers
        private static IEnumerable<IFhirExtensionHandler> s_extensionHandlers;

        // Operations handlers
        private static IEnumerable<IFhirOperationHandler> s_operationHandlers;

        // Profile handlers
        private static IEnumerable<IFhirProfileHandler> s_profileHandlers;

        /// <summary>
        /// Creates a profile utility
        /// </summary>
        static ExtensionUtil ()
        {
            var svcManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            s_extensionHandlers = svcManager
                .CreateInjectedOfAll<IFhirExtensionHandler>()
                .AsEnumerable();
            s_operationHandlers = svcManager
                .CreateInjectedOfAll<IFhirOperationHandler>()
                .AsEnumerable();
            s_profileHandlers = svcManager
                .CreateInjectedOfAll<IFhirProfileHandler>()
                .AsEnumerable();

        }

        /// <summary>
        /// Runs all registered extensions on the object
        /// </summary>
        /// <param name="appliedExtensions">The extensions that were applied to the object</param>
        /// <param name="applyTo">The object to which the extensions are being applied</param>
        /// <param name="me">The SanteDB canonical model to apply to</param>
        public static IEnumerable<Extension> CreateExtensions(this IIdentifiedEntity me, ResourceType applyTo, out IEnumerable<IFhirExtensionHandler> appliedExtensions)
        {
            appliedExtensions = s_extensionHandlers.Where(o => o.AppliesTo == null || o.AppliesTo == applyTo);
            return appliedExtensions.Select(o => o.Construct(me));
        }

        /// <summary>
        /// Try to apply the specified extension to the specified object
        /// </summary>
        public static bool TryApplyExtension(this Extension me, IdentifiedData applyTo)
        {
            return s_extensionHandlers.Where(o => o.Uri.ToString() == me.Url).Select(r => r.Parse(me, applyTo)).All(o=>o);
        }

        /// <summary>
        /// Get the specified operation type
        /// </summary>
        /// <param name="resourceType">The type of resource to fetch the operation handler for</param>
        /// <param name="operationName">The operation name</param>
        /// <returns>The operation handler</returns>
        public static IFhirOperationHandler GetOperation(string resourceType, string operationName)
        {
            if (!Enum.TryParse<ResourceType>(resourceType, out ResourceType rtEnum))
                throw new KeyNotFoundException($"Resource {resourceType} is not valid");
            return s_operationHandlers.FirstOrDefault(o => (o.AppliesTo == rtEnum || o.AppliesTo == null) && o.Name == operationName);
        }
    }
}

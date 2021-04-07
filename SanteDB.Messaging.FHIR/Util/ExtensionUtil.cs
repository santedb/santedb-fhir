using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Configuration;
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
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Util
{
    /// <summary>
    /// Profile utility which has methods for profile
    /// </summary>
    public static class ExtensionUtil
    {

        // Handlers
        private static ICollection<IFhirExtensionHandler> s_extensionHandlers;

        // Operations handlers
        private static ICollection<IFhirOperationHandler> s_operationHandlers;

        // Profile handlers
        private static ICollection<IFhirProfileValidationHandler> s_profileHandlers;

        // Behavior modifiers
        private static ICollection<IFhirRestBehaviorModifier> s_behaviorModifiers;

        // Message operations
        private static IDictionary<Uri, IFhirMessageOperation> s_messageOperations;

        /// <summary>
        /// Creates a profile utility
        /// </summary>
        static ExtensionUtil ()
        {
            var svcManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            s_extensionHandlers = svcManager
                .CreateInjectedOfAll<IFhirExtensionHandler>()
                .ToList();
            s_operationHandlers = svcManager
                .CreateInjectedOfAll<IFhirOperationHandler>()
                .ToList();
            s_profileHandlers = svcManager
                .CreateInjectedOfAll<IFhirProfileValidationHandler>()
                .ToList();
            s_behaviorModifiers = svcManager
                .CreateInjectedOfAll<IFhirRestBehaviorModifier>()
                .ToList();
            s_messageOperations = svcManager
                .CreateInjectedOfAll<IFhirMessageOperation>()
                .ToDictionary(o=>o.EventUri, o=>o);
        }

        /// <summary>
        /// Intialize handlers from the configuration file
        /// </summary>
        internal static void InitializeHandlers(IEnumerable<TypeReferenceConfiguration> extensions)
        {
            var svcManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            foreach (var ext in extensions.Where(o=>o.Type != null))
            {
                if (typeof(IFhirExtensionHandler).IsAssignableFrom(ext.Type))
                {
                    s_extensionHandlers.Add(svcManager.CreateInjected(ext.Type) as IFhirExtensionHandler);
                }
                if (typeof(IFhirProfileValidationHandler).IsAssignableFrom(ext.Type))
                {
                    s_profileHandlers.Add(svcManager.CreateInjected(ext.Type) as IFhirProfileValidationHandler);
                }
                if (typeof(IFhirRestBehaviorModifier).IsAssignableFrom(ext.Type))
                {
                    s_behaviorModifiers.Add(svcManager.CreateInjected(ext.Type) as IFhirRestBehaviorModifier);
                }
                if (typeof(IFhirOperationHandler).IsAssignableFrom(ext.Type))
                {
                    s_operationHandlers.Add(svcManager.CreateInjected(ext.Type) as IFhirOperationHandler);
                }
            }
        }


        /// <summary>
        /// Operation Handlers
        /// </summary>
        public static IEnumerable<IFhirOperationHandler> OperationHandlers => s_operationHandlers;

        /// <summary>
        /// Profile handlers
        /// </summary>
        public static IEnumerable<IFhirProfileValidationHandler> ProfileHandlers => s_profileHandlers;

        /// <summary>
        /// Get the specified message operation handler for the specified event uri
        /// </summary>
        public static IFhirMessageOperation GetMessageOperationHandler(Uri eventUri)
        {
            s_messageOperations.TryGetValue(eventUri, out IFhirMessageOperation retVal);
            return retVal;
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
            if (resourceType == null)
                return s_operationHandlers.FirstOrDefault(o => (o.AppliesTo == null || o.AppliesTo == null) && o.Name == operationName);
            else
            {
                if (!Enum.TryParse<ResourceType>(resourceType, out ResourceType rtEnum))
                    throw new KeyNotFoundException($"Resource {resourceType} is not valid");
                return s_operationHandlers.FirstOrDefault(o => (o.AppliesTo?.Contains(rtEnum) == true) && o.Name == operationName);
            }
        }

        /// <summary>
        /// Execute the BeforeSendResponse function allowing the behavior pipeline to inspect the message
        /// </summary>
        /// <param name="interaction">The interaction being called</param>
        /// <param name="resource">The resource being actioned</param>
        /// <returns>The updated resource</returns>
        /// <exception cref="SanteDB.Core.Exceptions.DetectedIssueException">If there is a detected/validation issue</exception>
        public static Resource ExecuteBeforeSendResponseBehavior(TypeRestfulInteraction interaction, ResourceType resourceType, Resource resource)
        {
            foreach (var behavior in s_behaviorModifiers.Where(o => o.CanApply(interaction, resource)))
                resource = behavior.BeforeSendResponse(interaction, resourceType, resource);
            return resource;
        }

        /// <summary>
        /// Execute the AfterReceive function allowing the behavior pipeline to inspect the message
        /// </summary>
        /// <param name="interaction">The interaction being called</param>
        /// <param name="resource">The resource being actioned</param>
        /// <returns>The updated resource</returns>
        /// <exception cref="SanteDB.Core.Exceptions.DetectedIssueException">If there is a detected/validation issue</exception>
        public static Resource ExecuteAfterReceiveRequestBehavior(TypeRestfulInteraction interaction, ResourceType resourceType, Resource resource)
        {
            foreach (var behavior in s_behaviorModifiers.Where(o => o.CanApply(interaction, resource)))
                resource = behavior.AfterReceiveRequest(interaction, resourceType, resource);
            return resource;
        }
    }
}

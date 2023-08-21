using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Docker.Core;
using SanteDB.Messaging.FHIR.Rest;
using SanteDB.Messaging.FHIR.Rest.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Messaging.FHIR.Docker
{
    /// <summary>
    /// FHIR provenance header feature
    /// </summary>
    public class FhirProvenanceHeaderFeature : IDockerFeature
    {
        /// <summary>
        /// Require X-Provenance on http verbs
        /// </summary>
        public const string SETTING_REQUIRED = "REQUIRE";

        /// <summary>
        /// Forbid X-Provenance on http verbs
        /// </summary>
        public const string SETTING_PROHIBIT = "PROHIBIT";

        /// <summary>
        /// Validate signatures in the http header
        /// </summary>
        public const string SETTING_VALIDATE_SIGS = "VALIDATE_SIGANTURES";

        /// <summary>
        /// Validate agents exist in SanteDB
        /// </summary>
        public const string SETTING_VALIDATE_AGENTS = "VALIDATE_AGENT";

        /// <inheritdoc/>
        public string Id => "FHIR_PROV";

        /// <inheritdoc/>
        public IEnumerable<string> Settings => new string[]
        {
            SETTING_REQUIRED,
            SETTING_PROHIBIT,
            SETTING_VALIDATE_SIGS,
            SETTING_VALIDATE_AGENTS
        };

        /// <inheritdoc/>
        public void Configure(SanteDBConfiguration configuration, IDictionary<string, string> settings)
        {
            var restConfiguration = configuration.GetSection<SanteDB.Rest.Common.Configuration.RestConfigurationSection>();
            if (restConfiguration == null)
            {
                throw new ConfigurationException("Error retrieving REST configuration", configuration);
            }

            var fhirRestConfiguration = restConfiguration.Services.FirstOrDefault(o => o.ServiceType == typeof(IFhirServiceContract));
            if(fhirRestConfiguration == null) {
                return;
            }

            // Create the settings object
            var provConfiguration = new FhirProvenanceHeaderBehavior.FhirProvenanceHeaderConfiguration();
            if(settings.TryGetValue(SETTING_PROHIBIT, out var settingListValue))
            {
                provConfiguration.ForbiddenMethods = settingListValue.Split(',');
            }
            if (settings.TryGetValue(SETTING_REQUIRED, out settingListValue))
            {
                provConfiguration.RequiredMethods = settingListValue.Split(',');
            }
            if (settings.TryGetValue(SETTING_VALIDATE_AGENTS, out settingListValue) && Boolean.TryParse(settingListValue, out var boolSetting))
            {
                provConfiguration.ValidateAgents = boolSetting;
            }
            if (settings.TryGetValue(SETTING_VALIDATE_SIGS, out settingListValue) && Boolean.TryParse(settingListValue, out boolSetting))
            {
                provConfiguration.ValidateSignatures = boolSetting;
            }

            var xsz = new XmlSerializer(provConfiguration.GetType());
            using (var sw = new StringWriter()) {
                xsz.Serialize(sw, provConfiguration);
                // Add the necessary behaviors to the endpoints
                foreach (var epc in fhirRestConfiguration.Endpoints)
                {
                    epc.Behaviors.Add(new SanteDB.Rest.Common.Configuration.RestEndpointBehaviorConfiguration(typeof(FhirProvenanceHeaderBehavior))
                    {
                        ConfigurationString = sw.ToString()
                    });
                }
            }
        }
    }
}

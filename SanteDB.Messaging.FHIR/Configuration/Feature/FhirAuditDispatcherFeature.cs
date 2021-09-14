using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Features;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Configuration.Feature
{
    /// <summary>
    /// FHIR audit dispatcher feature
    /// </summary>
    public class FhirAuditDispatcherFeature : IFeature
    {

        // Feature configuration
        private FhirDispatcherTargetConfiguration m_configuration;

        /// <summary>
        /// Gets the configuration 
        /// </summary>
        public object Configuration
        {
            get => this.m_configuration;
            set => this.m_configuration = (FhirDispatcherTargetConfiguration)value;
        }

        /// <summary>
        /// Configuration type
        /// </summary>
        public Type ConfigurationType => typeof(FhirDispatcherTargetConfiguration);

        /// <summary>
        /// Description of the audit dispatcher
        /// </summary>
        public string Description => "Enables dispatching of audits to a remote FHIR server";

        /// <summary>
        /// Flags of this feature
        /// </summary>
        public FeatureFlags Flags => FeatureFlags.None;

        /// <summary>
        /// Group of this feature
        /// </summary>
        public string Group => FeatureGroup.Security;

        /// <summary>
        /// Gets the name of this feature
        /// </summary>
        public string Name => "FHIR Audit Dispatch";

        /// <summary>
        /// Create installation tasks
        /// </summary>
        public IEnumerable<IConfigurationTask> CreateInstallTasks()
        {
            yield return new InstallFhirAuditDispatcher(this, this.m_configuration);
        }

        /// <summary>
        /// Create removal tasks
        /// </summary>
        public IEnumerable<IConfigurationTask> CreateUninstallTasks()
        {
            yield return new UninstallFhirAuditDispatcher(this, this.m_configuration);

        }

        /// <summary>
        /// Query the status of this feature
        /// </summary>
        public FeatureInstallState QueryState(SanteDBConfiguration configuration)
        {
            var fhirServiceConfig = configuration.GetSection<FhirDispatcherConfigurationSection>();
            if(fhirServiceConfig == null)
            {
                fhirServiceConfig = new FhirDispatcherConfigurationSection();
                configuration.AddSection(fhirServiceConfig);
            }

            // Get the configuration
            var tConfiguration = this.m_configuration = fhirServiceConfig.Targets.Find(o => o.Name.Equals("audit", StringComparison.OrdinalIgnoreCase));
            if(this.m_configuration == null)
            {
                this.m_configuration = new FhirDispatcherTargetConfiguration()
                {
                    Name = "audit"
                };
            }

            var service = configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(r => r.Type == typeof(FhirAuditDispatcher));
            return service && tConfiguration != null ? FeatureInstallState.Installed : tConfiguration != null || service ? FeatureInstallState.PartiallyInstalled : FeatureInstallState.NotInstalled;
            
        }
    }


    internal class UninstallFhirAuditDispatcher : IConfigurationTask
    {
        private FhirDispatcherTargetConfiguration m_configuration;

        /// <summary>
        /// Create a new instance of this task
        /// </summary>
        public UninstallFhirAuditDispatcher(FhirAuditDispatcherFeature hostFeature, FhirDispatcherTargetConfiguration configuration)
        {
            this.m_configuration = configuration;
            this.Feature = hostFeature;
        }

        /// <summary>
        /// Gets the description of this feature
        /// </summary>
        public string Description => "Removes the FHIR audit dispatcher from the system. After this task is executed, the SanteDB server will no longer dispatch audit events to the FHIR audit server";

        /// <summary>
        /// Gets the feature to which this task belongs
        /// </summary>
        public IFeature Feature { get; }

        /// <summary>
        /// Gets the name of this feature
        /// </summary>
        public string Name => "Remove FHIR Audit Dispatch";

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        
        /// <summary>
        /// Execute the removal of the feature
        /// </summary>
        public bool Execute(SanteDBConfiguration configuration)
        {
            var dispatcherConfiguration = configuration.GetSection<FhirDispatcherConfigurationSection>();
            dispatcherConfiguration.Targets.RemoveAll(o => o.Name.Equals("audit", StringComparison.OrdinalIgnoreCase));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(r => r.Type == typeof(FhirAuditDispatcher));
            return true;
        }

        /// <summary>
        /// Rollback the configuration
        /// </summary>
        public bool Rollback(SanteDBConfiguration configuration)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Verify the state
        /// </summary>
        public bool VerifyState(SanteDBConfiguration configuration) => configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(r => r.Type == typeof(FhirMessageHandler));
    }

    /// <summary>
    /// Install the FHIR dispatcher configured in this service
    /// </summary>
    internal class InstallFhirAuditDispatcher : IConfigurationTask
    {
        private FhirDispatcherTargetConfiguration m_configuration;

        /// <summary>
        /// Creates a new installation task
        /// </summary>
        public InstallFhirAuditDispatcher(FhirAuditDispatcherFeature hostFeature, FhirDispatcherTargetConfiguration configuration)
        {
            this.Feature = hostFeature;
            this.m_configuration = configuration;
        }

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description => $"Installs the FHIR audit dispatcher. Once complete the SanteDB server will send audits in FHIR format {this.m_configuration.Endpoint}";

        /// <summary>
        /// Gets the host feature
        /// </summary>
        public IFeature Feature { get; }

        /// <summary>
        /// Get the name of this feature
        /// </summary>
        public string Name => "Install FHIR Audit Dispatch";

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Execute the configuration option
        /// </summary>
        public bool Execute(SanteDBConfiguration configuration)
        {
            var dispatcherConfiguration = configuration.GetSection<FhirDispatcherConfigurationSection>();
            if(dispatcherConfiguration == null)
            {
                dispatcherConfiguration = new FhirDispatcherConfigurationSection();
                configuration.AddSection(dispatcherConfiguration);
            }

            dispatcherConfiguration.Targets.RemoveAll(o => o.Name.Equals("audit", StringComparison.OrdinalIgnoreCase));
            this.m_configuration.Name = "audit";
            dispatcherConfiguration.Targets.Add(this.m_configuration);

            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(r => typeof(IAuditDispatchService).IsAssignableFrom(r.Type));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(FhirAuditDispatcher)));
            return true;
        }

        /// <summary>
        /// Rollback the configuation
        /// </summary>
        public bool Rollback(SanteDBConfiguration configuration)
        {
            var dispatcherConfiguration = configuration.GetSection<FhirDispatcherConfigurationSection>();
            dispatcherConfiguration.Targets.RemoveAll(o => o.Name.Equals("audit", StringComparison.OrdinalIgnoreCase));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(r => r.Type == typeof(FhirAuditDispatcher));
            return true;
        }

        /// <summary>
        /// Verify the state of this object
        /// </summary>
        public bool VerifyState(SanteDBConfiguration configuration) => true;
    }
}

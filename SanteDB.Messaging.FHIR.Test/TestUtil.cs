using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Test utility classes
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class TestUtil
    {
        /// <summary>
        /// Mimic an authentication
        /// </summary>
        internal static IDisposable AuthenticateFhir(string appId, byte[] appSecret)
        {
            var appIdService = ApplicationServiceContext.Current.GetService<IApplicationIdentityProviderService>();
            var appPrincipal = appIdService.Authenticate(appId, BitConverter.ToString(appSecret).Replace("-", ""));
            var sesPvdService = ApplicationServiceContext.Current.GetService<ISessionProviderService>();
            var sesIdService = ApplicationServiceContext.Current.GetService<ISessionIdentityProviderService>();
            var session = sesPvdService.Establish(appPrincipal, "http://localhost", false, null, null, null);
            return AuthenticationContext.EnterContext(sesIdService.Authenticate(session));
        }

        /// <summary>
        /// Create the specified authority
        /// </summary>
        public static void CreateAuthority(string nsid, string oid, string url, string applicationName, byte[] deviceSecret)
        {
            // Create the test harness device / application
            var securityDevService = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityDevice>>();
            var securityAppService = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityApplication>>();
            var securityPipService = ApplicationServiceContext.Current.GetService<IPolicyInformationService>();
            var metadataService = ApplicationServiceContext.Current.GetService<IIdentityDomainRepositoryService>();

            using (AuthenticationContext.EnterSystemContext())
            {
                var pubId = $"{applicationName}|TEST";
                var device = securityDevService.Find(o => o.Name == pubId).FirstOrDefault();
                if (device == null)
                {
                    device = new SecurityDevice
                    {
                        DeviceSecret = BitConverter.ToString(deviceSecret).Replace("-", ""),
                        Name = $"{applicationName}|TEST"
                    };
                    device = securityDevService.Insert(device);
                    securityPipService.AddPolicies(device, PolicyGrantType.Grant, AuthenticationContext.Current.Principal, PermissionPolicyIdentifiers.LoginAsService);
                }

                // Application
                var app = securityAppService.Find(o => o.Name == applicationName).FirstOrDefault();
                if (app == null)
                {
                    app = new SecurityApplication
                    {
                        Name = applicationName,
                        ApplicationSecret = BitConverter.ToString(deviceSecret).Replace("-", "")
                    };

                    app = securityAppService.Insert(app);
                    securityPipService.AddPolicies(app, PolicyGrantType.Grant, AuthenticationContext.Current.Principal, PermissionPolicyIdentifiers.LoginAsService, PermissionPolicyIdentifiers.UnrestrictedClinicalData, PermissionPolicyIdentifiers.UnrestrictedMetadata);
                }

                // Create AA
                var aa = metadataService.Get(nsid);
                if (aa == null)
                {
                    aa = new IdentityDomain(nsid, nsid, oid)
                    {
                        AssigningAuthority = new System.Collections.Generic.List<AssigningAuthority>()
                        {
                            new AssigningAuthority()
                            {
                                AssigningApplicationKey = app.Key, 
                                Reliability = IdentifierReliability.Authoritative
                            }
                        },
                        IsUnique = true,
                        Url = url
                    };
                    metadataService.Insert(aa);
                }
            }
        }

        /// <summary>
        /// Get a FHIR message
        /// </summary>
        public static Resource GetFhirMessage(string messageName)
        {
            using (var s = typeof(TestUtil).Assembly.GetManifestResourceStream($"SanteDB.Messaging.FHIR.Test.Resources.{messageName}.json"))
            using (var sr = new StreamReader(s))
            using (var jr = new JsonTextReader(sr))
            {
                return new FhirJsonParser().Parse(jr) as Resource;
            }
        }

        /// <summary>
        /// Convert the <paramref name="message"/> to a string
        /// </summary>
        public static string MessageToString(Resource message)
        {
            return new FhirJsonSerializer(new SerializerSettings
            {
                Pretty = true
            }).SerializeToString(message);
        }
    }
}
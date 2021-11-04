using Hl7.Fhir.Model;
using Newtonsoft.Json;
using SanteDB.Core;
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
        /// Convert the <paramref name="message"/> to a string
        /// </summary>
        public static string MessageToString(Resource message)
        {
            return new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings()
            {
                Pretty = true
            }).SerializeToString(message);
        }

        /// <summary>
        /// Get a FHIR message
        /// </summary>
        public static Resource GetFhirMessage(String messageName)
        {
            using (var s = typeof(TestUtil).Assembly.GetManifestResourceStream($"SanteDB.Messaging.FHIR.Test.Resources.{messageName}.json"))
            using (var sr = new StreamReader(s))
            using (var jr = new JsonTextReader(sr))
                return new Hl7.Fhir.Serialization.FhirJsonParser().Parse(jr) as Resource;
        }

        /// <summary>
        /// Create the specified authority
        /// </summary>
        public static void CreateAuthority(string nsid, string oid, String url, string applicationName, byte[] deviceSecret)
        {
            // Create the test harness device / application
            var securityDevService = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityDevice>>();
            var securityAppService = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityApplication>>();
            var metadataService = ApplicationServiceContext.Current.GetService<IAssigningAuthorityRepositoryService>();

            using (AuthenticationContext.EnterSystemContext())
            {
                string pubId = $"{applicationName}|TEST";
                var device = securityDevService.Find(o => o.Name == pubId).FirstOrDefault();
                if (device == null)
                {
                    device = new SecurityDevice()
                    {
                        DeviceSecret = BitConverter.ToString(deviceSecret).Replace("-", ""),
                        Name = $"{applicationName}|TEST"
                    };
                    device.AddPolicy(PermissionPolicyIdentifiers.LoginAsService);
                    device = securityDevService.Insert(device);
                }

                // Application
                var app = securityAppService.Find(o => o.Name == applicationName).FirstOrDefault();
                if (app == null)
                {
                    app = new SecurityApplication()
                    {
                        Name = applicationName,
                        ApplicationSecret = BitConverter.ToString(deviceSecret).Replace("-", "")
                    };
                    app.AddPolicy(PermissionPolicyIdentifiers.LoginAsService);
                    app.AddPolicy(PermissionPolicyIdentifiers.UnrestrictedClinicalData);
                    app.AddPolicy(PermissionPolicyIdentifiers.ReadMetadata);
                    app = securityAppService.Insert(app);
                }

                // Create AA
                var aa = metadataService.Get(nsid);
                if (aa == null)
                {
                    aa = new SanteDB.Core.Model.DataTypes.AssigningAuthority(nsid, nsid, oid)
                    {
                        AssigningApplicationKey = app.Key,
                        IsUnique = true,
                        Url = url
                    };
                    metadataService.Insert(aa);
                }
            }

        }

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

    }
}

using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="PractitionerResourceHandler"/> class.
    /// </summary>
    public class TestPractitionerResourceHandler : DataTest
    {
        private readonly byte[] AUTH = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        private IRepositoryService<Core.Model.Roles.Patient> m_patientRepository;

        private IRepositoryService<Core.Model.Entities.Person> m_personRepository;

        private IRepositoryService<Core.Model.Entities.EntityRelationship> m_relationshipRepository;

        // Bundler 
        private IServiceManager m_serviceManager;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FirebirdSql.Data.FirebirdClient.FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestComplexRelationships).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            this.m_patientRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Roles.Patient>>();
            this.m_personRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Entities.Person>>();
            this.m_relationshipRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Entities.EntityRelationship>>();

            var testConfiguration = new SanteDB.Messaging.FHIR.Configuration.FhirServiceConfigurationSection()
            {
                Resources = new System.Collections.Generic.List<string>()
                {
                    "Practitioner"
                },
                OperationHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                ExtensionHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                ProfileHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                MessageHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PractitionerResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        [Test]
        public void TestCreatePractitioner()
        {
            // 1. set up the test data
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Shihab"
                },
                Family = "Khan"
            });

            practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            practitioner.Gender = AdministrativeGender.Male;
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

            //FhirJsonSerializer serializer = new FhirJsonSerializer();

            //var result = serializer.SerializeToString(practitioner);

            //Console.WriteLine(result);

            Resource result;

            // 2. execute the operation under test
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                // create the practitioner
                result = FhirResourceHandlerUtil.GetMappersFor(Hl7.Fhir.Model.ResourceType.Practitioner).First().Create(practitioner, Core.Services.TransactionMode.Commit);
                var messageString = TestUtil.MessageToString(result);

                Console.WriteLine(messageString);

                // retrieve the practitioner from the API
                result = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner).Read(result.Id, result.VersionId);
            }

            // 3. assert the results are correct
            Assert.NotNull(result);
            Assert.IsInstanceOf<Practitioner>(result);

            var actual = (Practitioner) result;

            Assert.AreEqual("Khan", actual.Name.Single().Family);
            Assert.AreEqual("Shihab", actual.Name.Single().Given.Single());
        }
    }
}

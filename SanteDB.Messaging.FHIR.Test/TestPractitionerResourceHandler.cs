using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Patient = SanteDB.Core.Model.Roles.Patient;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="PractitionerResourceHandler"/> class.
    /// </summary>
    public class TestPractitionerResourceHandler : DataTest
    {
        private readonly byte[] AUTH = {0x01, 0x02, 0x03, 0x04, 0x05};

        private IRepositoryService<Patient> m_patientRepository;

        private IRepositoryService<Person> m_personRepository;

        private IRepositoryService<EntityRelationship> m_relationshipRepository;

        // Bundler 
        private IServiceManager m_serviceManager;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestRelatedPersonResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            this.m_patientRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Patient>>();
            this.m_personRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Person>>();
            this.m_relationshipRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<EntityRelationship>>();

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Practitioner"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
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
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                result = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                //var messageString = TestUtil.MessageToString(result);

                //Console.WriteLine(messageString);

                // retrieve the practitioner using the resource handler
                result = practitionerResourceHandler.Read(result.Id, result.VersionId);
            }

            // 3. assert the results are correct
            Assert.NotNull(result);
            Assert.IsInstanceOf<Practitioner>(result);

            var actual = (Practitioner) result;

            Assert.AreEqual("Khan", actual.Name.Single().Family);
            Assert.AreEqual("Shihab", actual.Name.Single().Given.Single());
        }

        [Test]
        public void TestCreateInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // expect that the create method throws an InvalidDataException
                Assert.Throws<InvalidDataException>(() => practitionerResourceHandler.Create(new Account(), TransactionMode.Commit));
            }
        }
    }
}
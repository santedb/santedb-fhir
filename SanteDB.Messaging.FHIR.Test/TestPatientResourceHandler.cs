using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using Patient = SanteDB.Core.Model.Roles.Patient;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="PatientResourceHandler"/> class.
    /// </summary>
    class TestPatientResourceHandler : DataTest
    {
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

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
                    "Patient",
                    "Bundle"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PractitionerResourceHandler)),
                    new TypeReferenceConfiguration(typeof(BundleResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        [Test]
        public void TestCreatePatient()
        {
            var patient = new Hl7.Fhir.Model.Patient();
            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Jordan"
                },
                Family = "Webber"
            });

            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Address = new List<Address>
            {
                new Address
                {
                    Country = "Canada",
                    PostalCode = "L3D 1B4",
                    City = "Hamilton"
                }
            };
            

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the practitioner using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);
                var messageString = TestUtil.MessageToString(result);

                Console.WriteLine(messageString);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Hl7.Fhir.Model.Patient>(result);

            var actual = (Hl7.Fhir.Model.Patient) result;

            Assert.AreEqual("Webber", actual.Name.Single().Family);
            Assert.AreEqual("Jordan", actual.Name.Single().Given.Single());
            Assert.AreEqual("Canada", actual.Address.Single().Country);
        }

        [Test]
        public void TestDeletePatient()
        {
            var patient = new Hl7.Fhir.Model.Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "John"
                },
                Family = "Smith"
            });
            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home,
                    "905 555 1212")
            };
            patient.Gender = AdministrativeGender.Male;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);
                var messageString = TestUtil.MessageToString(result);

                Console.WriteLine(messageString);

                // retrieve the practitioner using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);

                Assert.NotNull(result);
                Assert.IsInstanceOf<Hl7.Fhir.Model.Patient>(result);

                var actual = (Hl7.Fhir.Model.Patient)result;

                result = patientResourceHandler.Delete(actual.Id, TransactionMode.Commit);

                Assert.Throws<KeyNotFoundException>(() => patientResourceHandler.Read(result.Id, result.VersionId));

            }
        }

        [Test]
        public void TestUpdatePatient()
        {
            var patient = new Hl7.Fhir.Model.Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Jessica"
                },
                Family = "Comeau"
            });
            patient.Gender = AdministrativeGender.Female;
            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Active = true;
            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Sms, ContactPoint.ContactPointUse.Mobile,
                    "123 123 1234")
            };

            Resource result; 

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);
                var messageString = TestUtil.MessageToString(result);

                Console.WriteLine(messageString);

                // retrieve the practitioner using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Hl7.Fhir.Model.Patient>(result);

            var actual = (Hl7.Fhir.Model.Patient) result;

            Assert.AreEqual("Jessica", actual.Name.Single().Given.Single());
            Assert.AreEqual(AdministrativeGender.Female, actual.Gender);
            Assert.IsTrue(actual.Active);

            actual.Gender = AdministrativeGender.Male;
            actual.Active = false;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Update(actual.Id, actual, TransactionMode.Commit);
                var messageString = TestUtil.MessageToString(result);

                Console.WriteLine(messageString);

                // retrieve the practitioner using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Hl7.Fhir.Model.Patient>(result);

            actual = (Hl7.Fhir.Model.Patient) result;

            Assert.AreEqual(AdministrativeGender.Male, actual.Gender);
            Assert.IsFalse(actual.Active);
        }

        [Test]
        public void TestQueryPatient()
        {
            var patient = new Hl7.Fhir.Model.Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Matthew"
                },
                Family = "Smith"
            });
            patient.Gender = AdministrativeGender.Male;
            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Active = true;
            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Sms, ContactPoint.ContactPointUse.Mobile,
                    "123 123 1234")
            };

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);
                var messageString = TestUtil.MessageToString(result);

                Console.WriteLine(messageString);


                // retrieve the practitioner using the resource handler
                result = patientResourceHandler.Query(new NameValueCollection()
                {
                    {"id", result.Id}
                });
            }

            Assert.IsInstanceOf<Bundle>(result);

            var bundle = (Bundle) result;
            
            var createdPatient = bundle.Entry.Select(c => c.Resource).OfType<Hl7.Fhir.Model.Patient>().FirstOrDefault();

            Assert.IsInstanceOf<Hl7.Fhir.Model.Patient>(createdPatient);
            Assert.AreEqual("Smith", createdPatient?.Name.First().Family);
            Assert.AreEqual("Matthew", createdPatient?.Name.First().Given.First());
            Assert.AreEqual(AdministrativeGender.Male, createdPatient?.Gender);
            Assert.NotNull(createdPatient?.Telecom.First());
        }

        [Test]
        public void TestCreatePatientInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                Assert.Throws<InvalidDataException>(() => patientResourceHandler.Create(new Practitioner(), TransactionMode.Commit));

            }
        }
    }
}

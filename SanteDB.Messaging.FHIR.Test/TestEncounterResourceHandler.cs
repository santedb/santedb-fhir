using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Patient = SanteDB.Core.Model.Roles.Patient;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    class TestEncounterResourceHandler : DataTest
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
                    "Encounter",
                    "Bundle",
                    "Patient"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(EncounterResourceHandler)),
                    new TypeReferenceConfiguration(typeof(BundleResourceHandler)),
                    new TypeReferenceConfiguration(typeof(PatientResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        [Test]
        public void TestCreateEncounter()
        {
            var bundle = TestUtil.GetFhirMessage("CreateEncounterBundle");

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                actual = bundleResourceHandler.Create(bundle, TransactionMode.Commit);
            }

            Assert.NotNull(actual);

            Assert.IsInstanceOf<Bundle>(actual);

            var actualBundle = (Bundle)actual;

            Assert.AreEqual(2, actualBundle.Entry.Count);
            Assert.AreEqual(1, actualBundle.Entry.Count(c => c.Resource is Hl7.Fhir.Model.Patient));
            Assert.AreEqual(1, actualBundle.Entry.Count(c => c.Resource is Encounter));

            var createdPatient = actualBundle.Entry.Select(c => c.Resource).OfType<Hl7.Fhir.Model.Patient>().FirstOrDefault();

            Assert.NotNull(createdPatient);

            var createdEncounter = actualBundle.Entry.Select(c => c.Resource).OfType<Encounter>().FirstOrDefault();

            Assert.NotNull(createdEncounter);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actual = encounterResourceHandler.Read(createdEncounter.Id, null);
            }

            Assert.NotNull(actual);

            Assert.IsInstanceOf<Encounter>(actual);

            var actualEncounter = (Encounter)actual;

            Assert.AreEqual(createdEncounter.Id, actualEncounter.Id);
            Assert.AreEqual(createdEncounter.Status, actualEncounter.Status);
        }

        /// <summary>
        /// Tests the create functionality of the <see cref="EncounterResourceHandler"/> class.
        /// add additional descriptions here
        /// </summary>
        [Test]
        public void TestDeleteEncounter()
        {
            var bundle = new Bundle
            {
                Type = Bundle.BundleType.Transaction
            };

            var patient = new Hl7.Fhir.Model.Patient
            {
                Id = Guid.NewGuid().ToString(),
                Name = new List<HumanName>
                {
                    new HumanName
                    {
                        Given = new List<string>
                        {
                            "Jordan",
                            "Final"
                        },
                        Family = "Webber"
                    }
                }
            };

            var encounter = new Encounter()
            {
                Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "HH"),
                Status = Encounter.EncounterStatus.Finished,
                Subject = new ResourceReference($"urn:uuid:{patient.Id}")
            };

            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = patient
            });

            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = encounter
            });

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                actual = bundleResourceHandler.Create(bundle, TransactionMode.Commit);
            }

            Assert.NotNull(actual);

            Assert.IsInstanceOf<Bundle>(actual);

            var actualBundle = (Bundle)actual;

            Assert.AreEqual(2, actualBundle.Entry.Count);
            Assert.AreEqual(1, actualBundle.Entry.Count(c => c.Resource is Hl7.Fhir.Model.Patient));
            Assert.AreEqual(1, actualBundle.Entry.Count(c => c.Resource is Encounter));

            var createdPatient = actualBundle.Entry.Select(c => c.Resource).OfType<Hl7.Fhir.Model.Patient>().FirstOrDefault();

            Assert.NotNull(createdPatient);

            var createdEncounter = actualBundle.Entry.Select(c => c.Resource).OfType<Encounter>().FirstOrDefault();

            Assert.NotNull(createdEncounter);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actual = encounterResourceHandler.Read(createdEncounter.Id, null);

                Assert.NotNull(actual);

                Assert.IsInstanceOf<Encounter>(actual);

                var actualEncounter = (Encounter) actual;

                var result = encounterResourceHandler.Delete(actualEncounter.Id, TransactionMode.Commit);

                var test = encounterResourceHandler.Read(result.Id, null);

                var obsoletedEncounter = (Encounter)test;
                Assert.AreEqual(Encounter.EncounterStatus.Unknown, obsoletedEncounter.Status);
            }

        }

        [Test]
        public void TestUpdateEncounter()
        {
            var bundle = new Bundle
            {
                Type = Bundle.BundleType.Transaction
            };

            var patient = new Hl7.Fhir.Model.Patient
            {
                Id = Guid.NewGuid().ToString(),
                Name = new List<HumanName>
                {
                    new HumanName
                    {
                        Given = new List<string>
                        {
                            "Jordan"
                        },
                        Family = "Webber"
                    }
                }
            };

            var encounter = new Encounter()
            {
                Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "HH"),
                Status = Encounter.EncounterStatus.Finished,
                Subject = new ResourceReference($"urn:uuid:{patient.Id}")
            };

            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = patient
            });

            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = encounter
            });

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                actual = bundleResourceHandler.Create(bundle, TransactionMode.Commit);
            }

            Assert.NotNull(actual);

            Assert.IsInstanceOf<Bundle>(actual);

            var actualBundle = (Bundle)actual;

            Assert.AreEqual(2, actualBundle.Entry.Count);
            Assert.AreEqual(1, actualBundle.Entry.Count(c => c.Resource is Hl7.Fhir.Model.Patient));
            Assert.AreEqual(1, actualBundle.Entry.Count(c => c.Resource is Encounter));

            var createdPatient = actualBundle.Entry.Select(c => c.Resource).OfType<Hl7.Fhir.Model.Patient>().FirstOrDefault();

            Assert.NotNull(createdPatient);

            var createdEncounter = actualBundle.Entry.Select(c => c.Resource).OfType<Encounter>().FirstOrDefault();

            Assert.NotNull(createdEncounter);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actual = encounterResourceHandler.Read(createdEncounter.Id, null);
            }

            Assert.IsInstanceOf<Encounter>(actual);

            var retrievedEncounter = (Encounter) actual;

            retrievedEncounter.Status = Encounter.EncounterStatus.Cancelled;
            retrievedEncounter.Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "HH");


            Resource updatedEncounter;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                updatedEncounter = encounterResourceHandler.Update(retrievedEncounter.Id, retrievedEncounter, TransactionMode.Commit);

            }

            Assert.NotNull(updatedEncounter);
            Assert.IsInstanceOf<Encounter>(updatedEncounter);

            var actualEncounter = (Encounter)actual;

            Assert.AreEqual(Encounter.EncounterStatus.Cancelled, actualEncounter.Status);
        }

        [Test]
        public void TestCreateEnounterInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                // create the patient using the resource handler
                Assert.Throws<InvalidDataException>(() => encounterResourceHandler.Create(new Practitioner(), TransactionMode.Commit));

            }
        }
    }
}

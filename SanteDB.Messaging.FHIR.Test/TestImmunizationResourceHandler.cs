using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;

namespace SanteDB.Messaging.FHIR.Test
{
    public class TestImmunizationResourceHandler : DataTest
    {
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Bundler 
        private IServiceManager m_serviceManager;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestOrganizationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Immunization",
                    "Patient",
                    "Encounter"
                },
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(ImmunizationResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        [Test]
        public void TestCreateImmunization()
        {
            var patient = new Patient
            {
                Name = new List<HumanName>
                {
                    new HumanName
                    {
                        Given = new List<string>
                        {
                            "Test"
                        },
                        Family = "Patient"
                    }
                }
            };

            var encounter = new Encounter
            {
                Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "HH"),
                Status = Encounter.EncounterStatus.Finished,
                Length = new Duration
                {
                    Value = 25
                },
                Period = new Period
                {
                    StartElement = FhirDateTime.Now(),
                    EndElement = FhirDateTime.Now()
                }
            };


            //set up resource for create request
            var immunization = new Immunization
            {
                DoseQuantity = new Quantity(12, "mmHg"),
                RecordedElement = new FhirDateTime(DateTimeOffset.Now),
                Route = new CodeableConcept("http://hl7.org/fhir/sid/ROUTE", "AMNINJ"),
                Site = new CodeableConcept("http://hl7.org/fhir/ValueSet/v3-ActSite", "LA"),
                Status = Immunization.ImmunizationStatusCodes.Completed,
                StatusReason = new CodeableConcept("http://hl7.org/fhir/ValueSet/v3-ActReason", "PATCAR"),
                VaccineCode = new CodeableConcept("http://hl7.org/fhir/sid/cvx", "112"),
                ExpirationDateElement = Date.Today(),
                LotNumber = "4"
            };

            immunization.ProtocolApplied.Add(new Immunization.ProtocolAppliedComponent
            {
                DoseNumber = new Integer(1),
                Series = "test"
            });

            Resource actualPatient;
            Resource actualEncounter;
            Resource actualImmunization;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);
                var immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);

                immunization.Patient = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                immunization.Encounter = new ResourceReference($"urn:uuid:{actualEncounter.Id}");

                actualImmunization = immunizationResourceHandler.Create(immunization, TransactionMode.Commit);

                var retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);

                Assert.AreEqual(retrievedImmunization.Id, actualImmunization.Id);
                Assert.AreEqual(12, retrievedImmunization.DoseQuantity.Value);
            }
        }
    }
}
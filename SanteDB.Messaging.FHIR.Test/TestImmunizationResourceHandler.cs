using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="ImmunizationResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestImmunizationResourceHandler : DataTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        /// <summary>
        /// The observation repository service.
        /// </summary>
        private IRepositoryService<Core.Model.Acts.SubstanceAdministration> m_substanceAdministrationRepositoryService;

        /// <summary>
        /// The service manager.
        /// </summary>
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
                    "Encounter",
                    "Practitioner"
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

        /// <summary>
        /// Tests the create functionality in <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
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

        /// <summary>
        /// Tests the update functionality in <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestUpdateImmunization()
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

            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

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
            Resource actualPractitioner;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);
                var immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);
               

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                actualPractitioner = (Practitioner)practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);

                immunization.Patient = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                immunization.Encounter = new ResourceReference($"urn:uuid:{actualEncounter.Id}");

                immunization.Performer = new List<Immunization.PerformerComponent>
                {
                    new Immunization.PerformerComponent
                    {
                        Actor = new ResourceReference($"urn:uuid:{actualPractitioner.Id}")
                    }
                };

                actualImmunization = immunizationResourceHandler.Create(immunization, TransactionMode.Commit);

                var retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);
                
                Assert.AreEqual(retrievedImmunization.Id, actualImmunization.Id);
                Assert.AreEqual(12, retrievedImmunization.DoseQuantity.Value);

                immunization.DoseQuantity.Value = 24;
                immunization.Status = Immunization.ImmunizationStatusCodes.NotDone;
                immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
                actualImmunization = immunizationResourceHandler.Update(actualImmunization.Id,immunization, TransactionMode.Commit);
                retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);

                Assert.AreEqual(24, retrievedImmunization.DoseQuantity.Value);
                Assert.AreEqual(Immunization.ImmunizationStatusCodes.NotDone, retrievedImmunization.Status);
            }
        }

        /// <summary>
        /// Tests the Entered In Error status update functionality in <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestUpdateImmunizationStatusEnteredInError()
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

            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

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
            Resource actualPractitioner;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);
                var immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);


                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                actualPractitioner = (Practitioner)practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);

                immunization.Patient = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                immunization.Encounter = new ResourceReference($"urn:uuid:{actualEncounter.Id}");

                immunization.Performer = new List<Immunization.PerformerComponent>
                {
                    new Immunization.PerformerComponent
                    {
                        Actor = new ResourceReference($"urn:uuid:{actualPractitioner.Id}")
                    }
                };

                actualImmunization = immunizationResourceHandler.Create(immunization, TransactionMode.Commit);

                var retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);

                Assert.AreEqual(retrievedImmunization.Id, actualImmunization.Id);
                Assert.AreEqual(12, retrievedImmunization.DoseQuantity.Value);

                immunization.Status = Immunization.ImmunizationStatusCodes.EnteredInError;
                immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
                actualImmunization = immunizationResourceHandler.Update(actualImmunization.Id, immunization, TransactionMode.Commit);
                retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);

                Assert.AreEqual(Immunization.ImmunizationStatusCodes.EnteredInError, retrievedImmunization.Status);
            }
        }


        /// <summary>
        /// Tests the delete functionality in <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestDeleteImmunization()
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

            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

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
            Resource actualPractitioner;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);
                var immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);


                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                actualPractitioner = (Practitioner)practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);

                immunization.Patient = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                immunization.Encounter = new ResourceReference($"urn:uuid:{actualEncounter.Id}");

                immunization.Performer = new List<Immunization.PerformerComponent>
                {
                    new Immunization.PerformerComponent
                    {
                        Actor = new ResourceReference($"urn:uuid:{actualPractitioner.Id}")
                    }
                };

                actualImmunization = immunizationResourceHandler.Create(immunization, TransactionMode.Commit);

                var retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);

                Assert.NotNull(retrievedImmunization);

                // TODO: Currently the status is not being mapped when the Immunization is deleted. The StatusConceptKey is coming back as obsolete
                // but the MapToFhir does not map the status when the code is obsolete. See line 82 in the ImmunizationResourceHandler class.
                var deletedImmunization = (Immunization)immunizationResourceHandler.Delete(retrievedImmunization.Id, TransactionMode.Commit);

                Assert.IsNull(deletedImmunization.Status);
            }
        }

        /// <summary>
        /// Tests the object mapping ability <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestCanMapObject()
        {
                var initialImmunizationKey = Guid.Parse("f3be6b88-bc8f-4263-a779-86f21ea10a47");
                var immunizationKey = Guid.Parse("6e7a3521-2967-4c0a-80ec-6c5c197b2178");
                var boosterImmunizationKey = Guid.Parse("0331e13f-f471-4fbd-92dc-66e0a46239d5");
                var randomGuidKey = Guid.NewGuid();

                var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
                var immunizationResourceHandler = new ImmunizationResourceHandler(m_substanceAdministrationRepositoryService, localizationService);

                //check to ensure immunization instance can be mapped
                var result = immunizationResourceHandler.CanMapObject(new Immunization());
                Assert.True(result);

                //check to ensure an invalid instance cannot be mapped
                result = immunizationResourceHandler.CanMapObject(new Medication());
                Assert.False(result);

                var substanceAdministration = new SubstanceAdministration()
                {
                    TypeConcept = new Concept() { Key = initialImmunizationKey }
                };

                //check to ensure substance instance can be mapped with valid type keys
                result = immunizationResourceHandler.CanMapObject(substanceAdministration);
                Assert.True(result);
                substanceAdministration.TypeConcept = new Concept() { Key = boosterImmunizationKey };
                result = immunizationResourceHandler.CanMapObject(substanceAdministration);
                Assert.True(result);
                substanceAdministration.TypeConcept = new Concept() { Key = immunizationKey };
                result = immunizationResourceHandler.CanMapObject(substanceAdministration);
                Assert.True(result);

                //check to ensure substance instance cannot be mapped without valid key 
                substanceAdministration.TypeConcept = new Concept() { Key = randomGuidKey };
                result = immunizationResourceHandler.CanMapObject(substanceAdministration);
                Assert.False(result);
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
            var immunizationResourceHandler = new ImmunizationResourceHandler(this.m_substanceAdministrationRepositoryService, localizationService);
            var methodInfo = typeof(ImmunizationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);
            var interactions = methodInfo.Invoke(immunizationResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);
            var resourceInteractionComponents = (IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions;
            Assert.AreEqual(7, resourceInteractionComponents.Count());
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.HistoryInstance));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Vread));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.SearchType));
        }
    }
}
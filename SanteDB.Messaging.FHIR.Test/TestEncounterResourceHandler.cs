/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you
 * may not use this file except in compliance with the License. You may
 * obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 *
 */
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Patient = Hl7.Fhir.Model.Patient;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Test class for <see cref="EncounterResourceHandler"/>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestEncounterResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };


        /// <summary>
        /// Tests the create functionality in <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateEncounter()
        {
            var patient = TestUtil.GetFhirMessage("CreateEncounter-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("CreateEncounter-Encounter") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.NotNull(actualPatient);
            Assert.NotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdPatient = (Patient)actualPatient;
            var createdEncounter = (Encounter)actualEncounter;

            Assert.NotNull(createdPatient);

            Assert.NotNull(createdEncounter);

            Resource actual;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actual = encounterResourceHandler.Read(createdEncounter.Id, null);
            }

            Assert.NotNull(actual);

            Assert.IsInstanceOf<Encounter>(actual);

            var retrievedEncounter = (Encounter)actual;

            Assert.AreEqual(createdEncounter.Id, retrievedEncounter.Id);
            Assert.AreEqual(createdEncounter.Status, retrievedEncounter.Status);
            Assert.IsNotNull(retrievedEncounter.Subject);
            Assert.AreEqual(DateTimeOffset.Parse(createdEncounter.Period.Start), DateTimeOffset.Parse(retrievedEncounter.Period.Start));
            Assert.AreEqual(DateTimeOffset.Parse(createdEncounter.Period.End), DateTimeOffset.Parse(retrievedEncounter.Period.End));
        }

        /// <summary>
        /// Tests the delete functionality in <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeleteEncounter()
        {
            var patient = TestUtil.GetFhirMessage("DeleteEncounter-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("DeleteEncounter-Encounter") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.IsNotNull(actualPatient);
            Assert.IsNotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Resource actual;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actual = encounterResourceHandler.Read(createdEncounter.Id, null);

                Assert.NotNull(actual);

                Assert.IsInstanceOf<Encounter>(actual);

                var retrievedEncounter = (Encounter)actual;

                var result = encounterResourceHandler.Delete(retrievedEncounter.Id, TransactionMode.Commit);

                try
                {
                    result = encounterResourceHandler.Read(result.Id, null);
                    Assert.Fail("Should throw gone exception");
                }
                catch (FhirException f) when (f.Status == System.Net.HttpStatusCode.Gone)
                {

                }
                catch
                {
                    Assert.Fail("Wrong exception type thrown!");
                }

            }
        }

        /// <summary>
        /// Tests the update functionality in <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdateEncounter()
        {
            var patient = new Patient
            {
                Name = new List<HumanName>
                {
                    new HumanName
                    {
                        Given = new List<string>
                        {
                            "Test",
                            "Update"
                        },
                        Family = "Patient"
                    }
                },
                Gender = AdministrativeGender.Male,
                Active = true,
                Address = new List<Address>
                {
                    new Address
                    {
                        State = "Ontario",
                        Country = "Canada",
                        Line = new List<string>
                        {
                            "123 King Street"
                        },
                        City = "Hamilton"
                    }
                }
            };

            var encounter = new Encounter()
            {
                Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "HH"),
                Status = Encounter.EncounterStatus.Finished,
                Period = new Period
                {
                    StartElement = FhirDateTime.Now(),
                    EndElement = FhirDateTime.Now()
                },
            };

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.NotNull(actualPatient);
            Assert.NotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Assert.NotNull(createdEncounter);

            createdEncounter.Status = Encounter.EncounterStatus.Cancelled;
            createdEncounter.Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "FLD");

            Resource actual;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actual = encounterResourceHandler.Update(createdEncounter.Id, createdEncounter, TransactionMode.Commit);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Encounter>(actual);

            var updatedEncounter = (Encounter)actual;

            Assert.AreEqual(Encounter.EncounterStatus.Cancelled, updatedEncounter.Status);
            Assert.AreEqual("FLD", updatedEncounter.Class.Code);
        }

        /// <summary>
        /// Tests the create method in <see cref="EncounterResourceHandler"/> confirming an invalid resource is not used.
        /// </summary>
        [Test]
        public void TestCreateEncounterInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                // create the encounter using the resource handler with an incorrect resource
                Assert.Throws<ArgumentException>(() => encounterResourceHandler.Create(new Practitioner(), TransactionMode.Commit));

            }
        }

        /// <summary>
        /// Test updating encounter with invalid resource in <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdateEncounterInvalidResource()
        {
            var patient = TestUtil.GetFhirMessage("UpdateEncounterInvalidResource-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("UpdateEncounterInvalidResource-Encounter") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.NotNull(actualPatient);
            Assert.NotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Assert.NotNull(createdEncounter);

            createdEncounter.Status = Encounter.EncounterStatus.Cancelled;
            createdEncounter.Class = new Coding("http://santedb.org/conceptset/v3-ActEncounterCode", "FLD");

            Resource updatedEncounter;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                Assert.Throws<ArgumentException>(() => updatedEncounter = encounterResourceHandler.Update(createdEncounter.Id, new Account(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the delete functionality in the <see cref="EncounterResourceHandler"/> class with an invalid id.
        /// </summary>
        [Test]
        public void TestDeleteEncounterInvalidId()
        {
            var patient = TestUtil.GetFhirMessage("DeleteEncounter-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("DeleteEncounter-Encounter") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.IsNotNull(actualPatient);
            Assert.IsNotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Assert.NotNull(createdEncounter);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                Assert.Throws<KeyNotFoundException>(() => encounterResourceHandler.Delete(Guid.NewGuid().ToString(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the create functionality for an Encounter with a status of in progress in the <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateEncounterInProgress()
        {
            var patient = TestUtil.GetFhirMessage("CreateEncounterInProgress-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("CreateEncounterInProgress-Encounter") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.IsNotNull(actualPatient);
            Assert.IsNotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Assert.IsNotNull(createdEncounter.Status);
            Assert.AreEqual(Encounter.EncounterStatus.InProgress, createdEncounter.Status);
        }

        /// <summary>
        /// Tests the create functionality of an Encounter with a status of planned in the <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateEncounterStatusPlanned()
        {
            var patient = TestUtil.GetFhirMessage("CreateEncounter-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("CreateEncounterStatusPlanned") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.IsNotNull(actualPatient);
            Assert.IsNotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Assert.IsNotNull(createdEncounter.Status);
            Assert.AreEqual(Encounter.EncounterStatus.Planned, createdEncounter.Status);
        }

        /// <summary>
        /// Tests the create functionality of an Encounter with a status of entered in error in the <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateEncounterStatusEnteredInError()
        {
            var patient = TestUtil.GetFhirMessage("CreateEncounter-Patient") as Patient;

            var encounter = TestUtil.GetFhirMessage("CreateEncounterStatusEnteredInError") as Encounter;

            Resource actualPatient;
            Resource actualEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);

                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);
            }

            Assert.IsNotNull(actualPatient);
            Assert.IsNotNull(actualEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Encounter>(actualEncounter);

            var createdEncounter = (Encounter)actualEncounter;

            Assert.IsNotNull(createdEncounter.Status);
            Assert.AreEqual(Encounter.EncounterStatus.EnteredInError, createdEncounter.Status);
        }

        /// <summary>
        /// Tests the creation of a encounter with a service provider in the <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateEncounterWithServiceProvider()
        {
            var patient = TestUtil.GetFhirMessage("CreateEncounter-Patient") as Patient;
            var organization = TestUtil.GetFhirMessage("Organization") as Organization;
            var encounter = TestUtil.GetFhirMessage("CreateEncounter-Encounter") as Encounter;

            Resource actualPatient;
            Resource actualOrganization;
            Resource actualEncounter;
            Resource readEncounter;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);

                // Create the patient and set it as the subject of the encounter
                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
                encounter.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");

                // Create the patient and set it as the service provider of the encounter
                actualOrganization = organizationResourceHandler.Create(organization, TransactionMode.Commit);
                encounter.ServiceProvider = new ResourceReference($"urn:uuid:{actualOrganization.Id}");

                // Create the encounter
                actualEncounter = encounterResourceHandler.Create(encounter, TransactionMode.Commit);

                readEncounter = encounterResourceHandler.Read(actualEncounter.Id, null);

            }

            Assert.IsNotNull(actualPatient);
            Assert.IsNotNull(actualOrganization);
            Assert.IsNotNull(actualEncounter);
            Assert.IsNotNull(readEncounter);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Organization>(actualOrganization);
            Assert.IsInstanceOf<Encounter>(actualEncounter);
            Assert.IsInstanceOf<Encounter>(readEncounter);

            var createdEncounter = (Encounter)actualEncounter;
            var retrievedEncounter = (Encounter)readEncounter;

            Assert.IsNotNull(createdEncounter.ServiceProvider);
            Assert.AreEqual(createdEncounter.ServiceProvider.Reference, retrievedEncounter.ServiceProvider.Reference);
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="EncounterResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var encounterResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Encounter);
            var methodInfo = typeof(EncounterResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(methodInfo);

            var interactions = methodInfo.Invoke(encounterResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);

            var resourceInteractionComponents = ((IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions).ToArray();

            Assert.AreEqual(7, resourceInteractionComponents.Length);
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Create));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.HistoryInstance));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Read));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.SearchType));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Vread));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Delete));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Update));
        }
    }
}

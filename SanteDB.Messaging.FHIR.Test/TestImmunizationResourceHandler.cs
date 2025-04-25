/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="ImmunizationResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestImmunizationResourceHandler : FhirTest
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
        /// Setup the test fixture.
        /// </summary>
        [SetUp]
        public void DoSetup()
        {
            this.m_substanceAdministrationRepositoryService = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Acts.SubstanceAdministration>>();
        }


        /// <summary>
        /// Tests the create functionality in <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestCreateImmunization()
        {
            var patient = TestUtil.GetFhirMessage("ObservationSubject");
            var encounter = (Encounter)TestUtil.GetFhirMessage("CreateEncounter-Encounter");
            var immunization = (Immunization)TestUtil.GetFhirMessage("CreateImmunization");

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

                Assert.IsNotNull(retrievedImmunization);
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
            var patient = TestUtil.GetFhirMessage("ObservationSubject");
            var encounter = (Encounter)TestUtil.GetFhirMessage("CreateEncounter-Encounter");
            var immunization = (Immunization)TestUtil.GetFhirMessage("CreateImmunization");
            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

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
                actualImmunization = immunizationResourceHandler.Update(actualImmunization.Id, immunization, TransactionMode.Commit);
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
            var patient = TestUtil.GetFhirMessage("ObservationSubject");
            var encounter = (Encounter)TestUtil.GetFhirMessage("CreateEncounter-Encounter");
            var immunization = (Immunization)TestUtil.GetFhirMessage("CreateImmunization");
            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

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
            var patient = TestUtil.GetFhirMessage("ObservationSubject");
            var encounter = (Encounter)TestUtil.GetFhirMessage("CreateEncounter-Encounter");
            var immunization = (Immunization)TestUtil.GetFhirMessage("CreateImmunization");
            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

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

                try
                {
                    immunizationResourceHandler.Read(retrievedImmunization.Id, null);
                    Assert.Fail("Should throw appropriate exception");
                }
                catch (FhirException e) when (e.Status == System.Net.HttpStatusCode.Gone)
                {

                }
                catch
                {
                    Assert.Fail("Improper exception thrown");
                }
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
            var immunizationResourceHandler = new ImmunizationResourceHandler(m_substanceAdministrationRepositoryService, localizationService, ApplicationServiceContext.Current.GetService<IRepositoryService<Material>>(), ApplicationServiceContext.Current.GetService<IRepositoryService<ManufacturedMaterial>>());

            //check to ensure immunization instance can be mapped
            var result = immunizationResourceHandler.CanMapObject(new Immunization());
            Assert.True(result);

            //check to ensure an invalid instance cannot be mapped
            result = immunizationResourceHandler.CanMapObject(new Medication());
            Assert.False(result);

            //check to ensure substance instance can be mapped with valid type keys
            var substanceAdministration = new SubstanceAdministration()
            {
                TypeConceptKey = initialImmunizationKey
            };
            result = immunizationResourceHandler.CanMapObject(substanceAdministration);
            Assert.True(result);

            //check to ensure substance instance can be mapped with valid type keys
            substanceAdministration.TypeConceptKey = boosterImmunizationKey;
            result = immunizationResourceHandler.CanMapObject(substanceAdministration);
            Assert.True(result);

            //check to ensure substance instance can be mapped with valid type keys
            substanceAdministration.TypeConceptKey = immunizationKey;
            result = immunizationResourceHandler.CanMapObject(substanceAdministration);
            Assert.True(result);

            //check to ensure substance instance cannot be mapped without valid key 
            substanceAdministration.TypeConceptKey = randomGuidKey;
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
            var immunizationResourceHandler = new ImmunizationResourceHandler(m_substanceAdministrationRepositoryService, localizationService, ApplicationServiceContext.Current.GetService<IRepositoryService<Material>>(), ApplicationServiceContext.Current.GetService<IRepositoryService<ManufacturedMaterial>>());
            var methodInfo = typeof(ImmunizationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);
            var interactions = methodInfo.Invoke(immunizationResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);

            var resourceInteractionComponents = (IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions;

            Assert.AreEqual(7, resourceInteractionComponents.Count());
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.HistoryInstance));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Read));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.SearchType));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Vread));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Delete));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Create));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Update));
        }

        /// <summary>
        /// Tests the query functionality of <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestQueryImmunization()
        {
            var patient = TestUtil.GetFhirMessage("ObservationSubject");
            var encounter = (Encounter)TestUtil.GetFhirMessage("CreateEncounter-Encounter");
            var immunization = (Immunization)TestUtil.GetFhirMessage("CreateImmunization");

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
                var bundle = immunizationResourceHandler.Query(new NameValueCollection()
                {
                    {"_id", actualImmunization.Id}
                });
                var retrievedImmunization = (Immunization)bundle.Entry.FirstOrDefault()?.Resource;

                Assert.IsNotNull(retrievedImmunization);
                Assert.AreEqual(retrievedImmunization.Id, actualImmunization.Id);
                Assert.AreEqual(12, retrievedImmunization.DoseQuantity.Value);
            }
        }

        /// <summary>
        /// Tests non UUID references <see cref="ImmunizationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestCreateImmunizationWithNonUUIDReference()
        {
            var immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
            var immunization = new Immunization
            {
                Patient = new ResourceReference(Guid.NewGuid().ToString())
            };

            Assert.Throws<FhirException>(() => immunizationResourceHandler.Create(immunization, TransactionMode.Commit));

            immunization = new Immunization()
            {
                Encounter = new ResourceReference(Guid.NewGuid().ToString())
            };

            Assert.Throws<FhirException>(() => immunizationResourceHandler.Create(immunization, TransactionMode.Commit));
        }

        /// <summary>
        /// Tests create functionality of <see cref="ImmunizationResourceHandler" /> class given dose qty of 0.
        /// </summary>
        [Test]
        public void TestCreateImmunizationGivenDoseQuantityOfZero()
        {
            //set up resource for create request
            var immunization = new Immunization
            {
                DoseQuantity = new Quantity(0, "mmHg"),
                VaccineCode = new CodeableConcept("http://hl7.org/fhir/sid/cvx", "112"),
            };

            Resource actualImmunization;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var immunizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Immunization);
                actualImmunization = immunizationResourceHandler.Create(immunization, TransactionMode.Commit);
                var retrievedImmunization = (Immunization)immunizationResourceHandler.Read(actualImmunization.Id, null);

                Assert.IsNotNull(retrievedImmunization);
                Assert.AreEqual(retrievedImmunization.Id, actualImmunization.Id);
                Assert.AreEqual(1, retrievedImmunization.DoseQuantity.Value);
            }
        }
    }
}

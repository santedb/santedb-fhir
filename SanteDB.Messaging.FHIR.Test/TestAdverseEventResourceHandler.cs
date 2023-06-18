/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 * User: fyfej
 * Date: 2023-5-19
 */
using NUnit.Framework;
using SanteDB.Messaging.FHIR.Handlers;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="AdverseEventResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestAdverseEventResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };


        [Test]
        public void TestCreateAdverseEvent()
        {
            // var patient = TestUtil.GetFhirMessage("ObservationSubject") as Patient;
            // var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;
            // var location = TestUtil.GetFhirMessage("CreateLocation") as Location;
            // var adverseEvent = new AdverseEvent
            // {
            //     Identifier = new Identifier
            //     {
            //         System = "http://santedb.org/fhir/test",
            //         Value = "6344"
            //     }
            // };

            //// adverseEvent.Category.Add(new CodeableConcept("http://hl7.org/fhir/ValueSet/adverse-event-category", "product-use-error"));
            // adverseEvent.Event = new CodeableConcept("http://santedb.org/conceptset/v3-FamilyMember", "BRO");
            // adverseEvent.Outcome = new CodeableConcept("http://hl7.org/fhir/ValueSet/adverse-event-outcome", "recovering");
            // adverseEvent.Seriousness = new CodeableConcept("http://hl7.org/fhir/ValueSet/adverse-event-seriousness", "Non-serious");

            // adverseEvent.DateElement = new FhirDateTime(DateTimeOffset.Parse("2021-11-29T08:06:32-05:00"));
            // Console.WriteLine(TestUtil.MessageToString(adverseEvent));

            // Resource actualPatient;
            // Resource actualPractitioner;
            // Resource actualAdverseEvent;
            // Resource actualLocation;

            // TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            // using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            // {
            //     var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
            //     actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

            //     var practionnerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);
            //     actualPractitioner = practionnerResourceHandler.Create(practitioner, TransactionMode.Commit);

            //     var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);
            //     actualLocation = locationResourceHandler.Create(location, TransactionMode.Commit);

            //     var adverseEventResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.AdverseEvent);

            //     adverseEvent.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
            //     adverseEvent.Recorder = new ResourceReference($"urn:uuid:{actualPractitioner.Id}");
            //     adverseEvent.Location = new ResourceReference($"urn:uuid:{actualLocation.Id}");

            //     actualAdverseEvent = adverseEventResourceHandler.Create(adverseEvent, TransactionMode.Commit);
            // }

            // Assert.NotNull(actualPatient);
            // Assert.NotNull(actualPractitioner);
            // Assert.NotNull(actualLocation);
            // Assert.NotNull(actualAdverseEvent);

            // Assert.IsInstanceOf<Patient>(actualPatient);
            // Assert.IsInstanceOf<Practitioner>(actualPractitioner);
            // Assert.IsInstanceOf<AdverseEvent>(actualAdverseEvent);

            // var createdPatient = (Patient)actualPatient;
            // var createdPractitioner = (Practitioner)actualPractitioner;
            // var createdAdverseEvent = (AdverseEvent)actualAdverseEvent;

            // Assert.NotNull(createdPatient);
            // Assert.NotNull(createdPractitioner);
            // Assert.NotNull(createdAdverseEvent);

            // Resource actual;

            // using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            // {
            //     var adverseEventResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.AdverseEvent);

            //     actual = adverseEventResourceHandler.Read(createdAdverseEvent.Id, null);
            // }

            // Assert.NotNull(actual);

            // Assert.IsInstanceOf<AdverseEvent>(actual);

            // var retrievedAdverseEvent = (AdverseEvent)actual;

            // Assert.AreEqual(createdAdverseEvent.Id, retrievedAdverseEvent.Id);
            // Assert.IsNotNull(retrievedAdverseEvent.Subject);
            // Assert.IsNotNull(retrievedAdverseEvent.Recorder);
        }
    }
}

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
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core.Security;
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
    /// Contains tests for the <see cref="ObservationResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestObservationResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };


        private Observation m_observation;

        private Patient m_patient;

        private Practitioner m_practitioner;

        /// <summary>
        /// Runs setup before each test execution.
        /// </summary>
        [SetUp]
        public void DoSetup()
        {


            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (AuthenticationContext.EnterSystemContext())
            {

                //add practitioner to be used as performer
                var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;

                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                this.m_practitioner = (Practitioner)practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                //add patient to be used subject
                var patient = TestUtil.GetFhirMessage("ObservationSubject") as Patient;

                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                this.m_patient = (Patient)patientResourceHandler.Create(patient, TransactionMode.Commit);

                //add a general observation to be used for multiple tests
                var observation = TestUtil.GetFhirMessage("SetupObservation") as Observation;

                observation.Subject = new ResourceReference($"urn:uuid:{this.m_patient.Id}");
                observation.Performer.Add(new ResourceReference($"urn:uuid:{this.m_practitioner.Id}"));

                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                this.m_observation = (Observation)observationResourceHandler.Create(observation, TransactionMode.Commit);
            }
        }

        /// <summary>
        /// Tests the create functionality in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestCreateObservation()
        {
            var effectiveTime = new FhirDateTime(DateTimeOffset.Parse("2021-11-29T08:06:32-05:00"));

            var observation = TestUtil.GetFhirMessage("CreateObservation") as Observation;

            //subject
            observation.Subject = new ResourceReference($"urn:uuid:{this.m_patient.Id}");

            //performer
            observation.Performer.Add(new ResourceReference($"urn:uuid:{this.m_practitioner.Id}"));

            Resource createdResource, retrievedResource;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                // create the observation using the resource handler
                createdResource = observationResourceHandler.Create(observation, TransactionMode.Commit);

                // retrieve the observation using the resource handler
                retrievedResource = observationResourceHandler.Read(createdResource.Id, null);
            }

            Assert.NotNull(retrievedResource);
            Assert.IsInstanceOf<Observation>(retrievedResource);

            var actual = (Observation)retrievedResource;
            Assert.IsInstanceOf<Quantity>(actual.Value);

            var qty = actual.Value as Quantity;
            Assert.AreEqual(12, qty.Value);

            //Due to time zone not being supported by current version of test database
            //this following assert will only work if original effective time has local timezone
            //Assert.AreEqual(effectiveTime, actual.Effective);
        }

        /// <summary>
        /// Tests the delete functionality in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestDeleteObservation()
        {
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                var retrievedObservation = (Observation)observationResourceHandler.Read(this.m_observation.Id, null);

                //ensure that the status is not unknown
                Assert.IsNotNull(retrievedObservation.Status);
                Assert.AreNotEqual(ObservationStatus.Unknown, retrievedObservation.Status);

                _ = observationResourceHandler.Delete(this.m_observation.Id, TransactionMode.Commit);

                try
                {
                    retrievedObservation = (Observation)observationResourceHandler.Read(this.m_observation.Id, null);
                    Assert.Fail("Should throw exception");
                }
                catch (FhirException e) when (e.Status == System.Net.HttpStatusCode.Gone) { }
                catch
                {
                    Assert.Fail("Threw wrong exception type");
                }
            }
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);
            var methodInfo = typeof(ObservationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);
            var interactions = methodInfo.Invoke(observationResourceHandler, null);

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
        /// Tests the Query functionality in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestQueryObservation()
        {
            Resource retrievedResource;
            var observation = TestUtil.GetFhirMessage("SetupObservation") as Observation;

            observation.Subject = new ResourceReference($"urn:uuid:{this.m_patient.Id}");
            observation.Performer.Add(new ResourceReference($"urn:uuid:{this.m_practitioner.Id}"));

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);
                var createdObservation = (Observation)observationResourceHandler.Create(observation, TransactionMode.Commit);

                retrievedResource = observationResourceHandler.Query(new NameValueCollection
                {
                    { "_id", createdObservation.Id }
                });

                //ensure query returns correct result
                Assert.AreEqual(1, ((Bundle)retrievedResource).Entry.Count);
                Assert.IsInstanceOf<Observation>(((Bundle)retrievedResource).Entry.First().Resource);

                var retrievedObservation = ((Bundle)retrievedResource).Entry.First().Resource as Observation;

                var retrievedObservationValue = retrievedObservation.Value as Quantity;

                Assert.AreEqual(22, retrievedObservationValue.Value.Value);
            }
        }

        /// <summary>
        /// Tests the update functionality in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestUpdateObservation()
        {
            Resource result;
            var updatedEffectiveTime = new FhirDateTime(new DateTimeOffset(2021, 1, 1, 12, 30, 30, 30, new TimeSpan(-5, 0, 0)));

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                Resource retrievedResource = observationResourceHandler.Read(this.m_observation.Id, null);

                Assert.NotNull(retrievedResource);
                Assert.IsInstanceOf<Observation>(retrievedResource);

                var retrievedObservation = (Observation)retrievedResource;

                var retrievedObservationValue = retrievedObservation.Value as Quantity;

                Assert.AreEqual(22, retrievedObservationValue.Value.Value);

                Console.WriteLine(TestUtil.MessageToString(retrievedObservation));

                //update observation
                retrievedObservation.Value = new Quantity(10, "mmHg");

                retrievedObservation.Effective = updatedEffectiveTime;

                _ = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //read again
                result = observationResourceHandler.Read(retrievedObservation.Id, null);
            }

            //ensure update took place
            var updatedObservation = (Observation)result;
            var updatedObservationValue = updatedObservation.Value as Quantity;

            Assert.AreEqual(10, updatedObservationValue.Value);
            Assert.AreEqual(updatedEffectiveTime, updatedObservation.Effective);
        }

        /// <summary>
        /// Tests various status of Observation in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestObservationStatus()
        {
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                var retrievedObservation = (Observation)observationResourceHandler.Read(this.m_observation.Id, null);

                //check initial status
                Assert.AreEqual(ObservationStatus.Preliminary, retrievedObservation.Status);

                //updated status to final
                retrievedObservation.Status = ObservationStatus.Final;
                var updatedObservation = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //check for correct status change
                retrievedObservation = (Observation)observationResourceHandler.Read(updatedObservation.Id, null);
                Assert.AreEqual(ObservationStatus.Final, retrievedObservation.Status);

                //update status to amended
                retrievedObservation.Status = ObservationStatus.Amended;
                Assert.Throws<NotSupportedException>(() => observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit));

                //update status to corrected
                retrievedObservation.Status = ObservationStatus.Corrected;
                Assert.Throws<NotSupportedException>(() => observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit));

                //update status to entered in error
                retrievedObservation.Status = ObservationStatus.EnteredInError;
                updatedObservation = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //check for correct status change
                retrievedObservation = (Observation)observationResourceHandler.Read(updatedObservation.Id, null);
                Assert.AreEqual(ObservationStatus.EnteredInError, retrievedObservation.Status);

                //update status to cancelled
                retrievedObservation.Status = ObservationStatus.Cancelled;
                updatedObservation = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //check for correct status change
                retrievedObservation = (Observation)observationResourceHandler.Read(updatedObservation.Id, null);
                Assert.AreEqual(ObservationStatus.Cancelled, retrievedObservation.Status);
            }
        }

        /// <summary>
        /// Tests various value types of Observation in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestObservationValueType()
        {
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                var retrievedObservation = (Observation)observationResourceHandler.Read(this.m_observation.Id, null);

                //ensure the initial Quantity value is set correctly
                var quantityValue = retrievedObservation.Value as Quantity;
                Assert.AreEqual(22, quantityValue.Value.Value);

                //update  observation value to Textual type
                retrievedObservation.Value = new FhirString("test");
                var updatedObservation = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //ensure the update took place correctly
                retrievedObservation = (Observation)observationResourceHandler.Read(updatedObservation.Id, null);
                var stringValue = retrievedObservation.Value as FhirString;
                Assert.AreEqual("test", stringValue.Value);

                //update  observation value to Codeable type
                retrievedObservation.Value = new CodeableConcept("http://hl7.org/fhir/v3/ObservationInterpretation", "H");
                updatedObservation = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //ensure the update took place correctly
                retrievedObservation = (Observation)observationResourceHandler.Read(updatedObservation.Id, null);
                var codeValue = retrievedObservation.Value as CodeableConcept;
                Assert.AreEqual(1, codeValue.Coding.Count);
                Assert.AreEqual("H", codeValue.Coding.First().Code);
            }
        }
    }
}
/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * User: khans
 * Date: 2021-11-15
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;

namespace SanteDB.Messaging.FHIR.Test
{
    [ExcludeFromCodeCoverage]
    public class TestObservationResourceHandler : DataTest
    {
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        private Observation m_observation;

        private IRepositoryService<Core.Model.Acts.Observation> m_observationRepositoryService;

        private Patient m_patient;

        private Practitioner m_practitioner;

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

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Patient",
                    "Practitioner",
                    "Observation"
                }
            };

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);

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
            var effectiveTime = new FhirDateTime(DateTimeOffset.Parse("2021-10-01T08:06:32+01:00"));

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
            Assert.AreEqual(effectiveTime, actual.Effective);
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

                retrievedObservation = (Observation)observationResourceHandler.Read(this.m_observation.Id, null);
                
                //ensure observation status is now unknown since deletion was performed
                Assert.AreEqual(ObservationStatus.Unknown, retrievedObservation.Status);
            }
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="ObservationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
            var practitionerResourceHandler = new ObservationResourceHandler(this.m_observationRepositoryService, localizationService);
            var methodInfo = typeof(ObservationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);
            var interactions = methodInfo.Invoke(practitionerResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);
            var resourceInteractionComponents = (IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions;
            Assert.AreEqual(5, resourceInteractionComponents.Count());
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.HistoryInstance));
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
            Resource createdResource, retrievedResource, result;
            var updatedEffectiveTime = new FhirDateTime(new DateTimeOffset(2021, 1, 1, 12, 30, 30, 30, new TimeSpan(1, 0, 0)));

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                retrievedResource = observationResourceHandler.Read(this.m_observation.Id, null);

                Assert.NotNull(retrievedResource);
                Assert.IsInstanceOf<Observation>(retrievedResource);

                var retrievedObservation = (Observation)retrievedResource;

                var retrievedObservationValue = retrievedObservation.Value as Quantity;

                Assert.AreEqual(22, retrievedObservationValue.Value.Value);

                Console.WriteLine(TestUtil.MessageToString(retrievedObservation));

                //update observation
                retrievedObservation.Value = new Quantity(10, "mmHg");
                
                retrievedObservation.Effective = updatedEffectiveTime;

                _ = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation,
                    TransactionMode.Commit);

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
                updatedObservation = observationResourceHandler.Update(retrievedObservation.Id, retrievedObservation, TransactionMode.Commit);

                //check for correct status change
                retrievedObservation = (Observation)observationResourceHandler.Read(updatedObservation.Id, null);
                Assert.AreEqual(ObservationStatus.Amended, retrievedObservation.Status);

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

    }
}
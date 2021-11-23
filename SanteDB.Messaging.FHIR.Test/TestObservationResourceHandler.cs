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

using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using Observation = Hl7.Fhir.Model.Observation;


namespace SanteDB.Messaging.FHIR.Test
{
    public class TestObservationResourceHandler : DataTest
    {
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Bundler 
        private IServiceManager m_serviceManager;

        private Practitioner m_practitioner;

        private Patient m_patient;
      
        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FirebirdSql.Data.FirebirdClient.FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestRelatedPersonResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

            var testConfiguration = new SanteDB.Messaging.FHIR.Configuration.FhirServiceConfigurationSection()
            {
                Resources = new System.Collections.Generic.List<string>()
                {
                    "Patient",
                    "Practitioner",
                    "Observation",
                },

            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);


                //add practitioner to be used as performer
                var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;
                
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                m_practitioner = (Practitioner)practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                

                //add patient to be used subject
                var patient = TestUtil.GetFhirMessage("ObservationSubject") as Patient;

                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                m_patient = (Patient)patientResourceHandler.Create(patient, TransactionMode.Commit);

            }
        }

        /// <summary>
        /// Tests the create functionality in <see cref="ObservationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateObservation()
        {
            var observation = TestUtil.GetFhirMessage("CreateObservation") as Observation;

            //subject
            observation.Subject = new ResourceReference($"urn:uuid:{m_patient.Id}");

            //performer
            observation.Performer.Add(new ResourceReference($"urn:uuid:{m_practitioner.Id}"));

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            Resource createdResource, retrievedResource;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {

                // get the resource handler
                var observationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Observation);

                // create the observation using the resource handler
                createdResource = observationResourceHandler.Create(observation, TransactionMode.Commit);

                // retrieve the observation using the resource handler
                retrievedResource = observationResourceHandler.Read(createdResource.Id, createdResource.VersionId);
            }

            Assert.NotNull(retrievedResource);
            Assert.IsInstanceOf<Observation>(retrievedResource);

            var actual = (Observation) retrievedResource;

            Assert.AreEqual(12, actual.Value);
        }


    }
}

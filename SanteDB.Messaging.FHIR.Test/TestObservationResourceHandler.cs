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
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
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

        //Assigning authority service
        private IAssigningAuthorityRepositoryService m_assigningAuthorityService;

        //concept service
        private IConceptRepositoryService m_conceptRepositoryService;

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
            this.m_assigningAuthorityService = ApplicationServiceContext.Current.GetService<IAssigningAuthorityRepositoryService>();
            this.m_conceptRepositoryService = ApplicationServiceContext.Current.GetService<IConceptRepositoryService>();
            

            var testConfiguration = new SanteDB.Messaging.FHIR.Configuration.FhirServiceConfigurationSection()
            {
                Resources = new System.Collections.Generic.List<string>()
                {
                    "Patient",
                    "Practitioner",
                    "Observation",
                    "Bundle"
                },
                OperationHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                ExtensionHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                ProfileHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                MessageHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(ObservationResourceHandler)),
                    new TypeReferenceConfiguration(typeof(BundleResourceHandler)),
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);


                //add practitioner for a participant
                var practitioner = new Practitioner();

                practitioner.Name.Add(new HumanName
                {
                    Given = new List<string>
                    {
                        "Test"
                    },
                    Family = "Physician"
                });

                practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
                practitioner.Gender = AdministrativeGender.Male;
                practitioner.Telecom = new List<ContactPoint>
                {
                    new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
                };
                
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                m_practitioner = (Practitioner)practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                

                //add patient for subject
                var patient = new Patient();
                patient.Name.Add(new HumanName
                {
                    Given = new List<string>
                    {
                        "Test"
                    },
                    Family = "Patient"
                });

                patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
                patient.Gender = AdministrativeGender.Male;
                patient.Telecom = new List<ContactPoint>
                {
                    new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Home, "905 545 1234")
                };
                
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                m_patient = (Patient)patientResourceHandler.Create(patient, TransactionMode.Commit);



            }
        }

        [Test]
        public void TestCreateObservation()
        {

            var observation = new Observation();

            observation.Identifier.Add(new Identifier("http://santedb.org/fhir/test", "6324"));
            //based on

            //status element
            observation.StatusElement = new Code<ObservationStatus>(ObservationStatus.Registered);


            //subject
            observation.Subject = new ResourceReference($"urn:uuid:{m_patient.Id}");

            //performer
            observation.Performer.Add(new ResourceReference($"urn:uuid:{m_practitioner.Id}"));


            //effective
            observation.Effective = new Instant(new DateTimeOffset(2021, 10, 1, 8, 6, 32, new TimeSpan(1, 0, 0)));

            //issued at
            observation.IssuedElement = new Instant(new DateTimeOffset(2021, 8, 1, 8, 6, 32, new TimeSpan(1, 0, 0)));

            //interpretation
            observation.Interpretation.Add(new CodeableConcept("http://hl7.org/fhir/v3/ObservationInterpretation", "HH"));

            //value - fhirstring
            //observation.Value = new FhirString("Test patient felt well today");

            //value - concept
            //observation.Value = new CodeableConcept("http://hl7.org/fhir/v3/ObservationInterpretation", "HH");

            //value - quantity
            observation.Value = new Quantity(12, "mmHg");


            observation.Code = new CodeableConcept("http://santedb.org/conceptset/v3-ActClassClinicalDocument", "DOCCLIN",  "Clinical Document");


            //  execute  test operations
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

            // assert 
            Assert.NotNull(retrievedResource);
            Assert.IsInstanceOf<Observation>(retrievedResource);

            var actual = (Observation) retrievedResource;

            Assert.AreEqual(12, actual.Value);
        }


        

  
    }
}

﻿/*
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
 * User: Webber
 * Date: 2021-11-18
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Tests the <see cref="PatientResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal class TestPatientResourceHandler : DataTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        /// <summary>
        /// The service manager.
        /// </summary>
        private IServiceManager m_serviceManager;

        /// <summary>
        /// Set up method for the test methods.
        /// </summary>
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
                    "Organization",
                    "Practitioner"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PractitionerResourceHandler)),
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

        /// <summary>
        /// Tests the creation of a deceased patient in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateDeceasedPatient()
        {
            var patient = TestUtil.GetFhirMessage("CreateDeceasedPatient") as Patient;

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                actual = patientResourceHandler.Create(patient, TransactionMode.Commit);
            }

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<Patient>(actual);

            var createdPatient = (Patient) actual;

            Assert.IsNotNull(createdPatient.Deceased);
        }

        /// <summary>
        /// Tests the creation of a patient with a partial date (month and year) for the deceased date in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateDeceasedPatientPartialDate()
        {
            var patient = new Patient
            {
                Name = new List<HumanName>
                {
                    new HumanName
                    {
                        Given = new List<string>
                        {
                            "Alan"
                        },
                        Family = "Daniels"
                    }
                },
                Active = false,
                BirthDate = new FhirDateTime(1961, 4, 24).ToString(),
                Telecom = new List<ContactPoint>
                {
                    new ContactPoint(ContactPoint.ContactPointSystem.Phone,  ContactPoint.ContactPointUse.Home, "905 123 1234")
                },
                Deceased = new FhirDateTime(2021, 8)
            };

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                actual = patientResourceHandler.Create(patient, TransactionMode.Commit);
            }

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<Patient>(actual);

            var createdPatient = (Patient)actual;

            Assert.IsNotNull(createdPatient.Deceased);
            Assert.AreEqual(new FhirDateTime(2021, 8), createdPatient.Deceased);

            createdPatient.Deceased = new FhirDateTime(2021);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                actual = patientResourceHandler.Update(createdPatient.Id, createdPatient, TransactionMode.Commit);
            }

            Assert.IsNotNull(actual);

            Assert.IsInstanceOf<Patient>(actual);

            var updatedPatient = (Patient)actual;

            Assert.IsNotNull(updatedPatient.Deceased);
            Assert.AreEqual(new FhirDateTime(2021), updatedPatient.Deceased);
        }

        /// <summary>
        /// Tests the create functionality in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePatient()
        {
            var patient = TestUtil.GetFhirMessage("CreatePatient") as Patient;

            var patientLink = TestUtil.GetFhirMessage("CreatePatient-PatientLink") as Patient;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create link patient
                var createdPatientLink = patientResourceHandler.Create(patientLink, TransactionMode.Commit);

                // Link the created patient with the main patient
                patient.Link.First().Other = new ResourceReference($"urn:uuid:{createdPatientLink.Id}");

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Patient>(result);

            var actual = (Patient) result;

            Assert.AreEqual("Webber", actual.Name.Single().Family);
            Assert.AreEqual("Jordan", actual.Name.Single().Given.Single());
            Assert.AreEqual("Canada", actual.Address.Single().Country);
            Assert.AreEqual("mailto:Webber@gmail.com", actual.Telecom.First().Value);
            Assert.IsNotNull(actual.Photo.First().Data);
            Assert.AreEqual(patient.BirthDate, actual.BirthDate);
        }

        /// <summary>
        /// Tests the create functionality with an invalid resource in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePatientInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                Assert.Throws<InvalidDataException>(() => patientResourceHandler.Create(new Practitioner(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the creation of a patient with a multiple birth order in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePatientMultipleBirth()
        {
            var patient = TestUtil.GetFhirMessage("CreateMultipleBirthPatient") as Patient;

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                actual = patientResourceHandler.Create(patient, TransactionMode.Commit);
            }

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<Patient>(actual);

            var createdPatient = (Patient)actual;

            // HACK: the FHIR Integer equivalent doesn't implement value equality with the corresponding value type :/
            Assert.AreEqual(new Integer(3).Value, ((Integer)createdPatient.MultipleBirth).Value);

            createdPatient.MultipleBirth = new FhirBoolean(true);

            Resource updatedPatient;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                updatedPatient = patientResourceHandler.Update(createdPatient.Id, createdPatient, TransactionMode.Commit);
            }

            Assert.IsNotNull(updatedPatient);
            Assert.IsInstanceOf<Patient>(updatedPatient);

            var actualPatient = (Patient)updatedPatient;

            // HACK: the FHIR Boolean equivalent doesn't implement value equality with the corresponding value type :/
            Assert.IsTrue(((FhirBoolean)actualPatient.MultipleBirth).Value);
        }

        /// <summary>
        /// Tests the creation of a patient with an associated general practitioner in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePatientWithGeneralPractitioner()
        {
            var practitioner = TestUtil.GetFhirMessage("CreatePatientWithGeneralPractitioner-Practitioner") as Practitioner;

            var patient = TestUtil.GetFhirMessage("CreatePatientWithGeneralPractitioner-Patient") as Patient;

            Resource actualPatient;
            Resource actualPractitioner;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                actualPractitioner = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                patient.GeneralPractitioner = new List<ResourceReference>
                {
                    new ResourceReference($"urn:uuid:{actualPractitioner.Id}")
                };
                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
            }

            Assert.NotNull(actualPatient);
            Assert.NotNull(actualPractitioner);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Practitioner>(actualPractitioner);

            var createdPatient = (Patient) actualPatient;

            Assert.IsNotNull(createdPatient.GeneralPractitioner.First());
        }

        /// <summary>
        /// Tests the creation of a patient with a managing organization in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePatientWithOrganization()
        {
            var patient = TestUtil.GetFhirMessage("CreatePatient") as Patient;

            var patientLink = TestUtil.GetFhirMessage("CreatePatient-PatientLink") as Patient;

            var organization = TestUtil.GetFhirMessage("CreatePatientWithOrganization-Organization") as Organization;

            Resource actualPatient;
            Resource actualOrganization;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);

                // Create the independent resources for the main Resource.
                actualOrganization = organizationResourceHandler.Create(organization, TransactionMode.Commit);
                var createdPatientLink = patientResourceHandler.Create(patientLink, TransactionMode.Commit);

                // Connect the independent resources to the dependent resource
                patient.ManagingOrganization = new ResourceReference($"urn:uuid:{actualOrganization.Id}");
                patient.Link.First().Other = new ResourceReference($"urn:uuid:{createdPatientLink.Id}");

                // Create the dependent resource
                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

                Assert.IsNotNull(actualPatient);
                Assert.IsInstanceOf<Patient>(actualPatient);

                var createdPatient = (Patient)actualPatient;

                Assert.IsNotNull(patient.ManagingOrganization);
            }
        }

        /// <summary>
        /// Tests the delete functionality in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeletePatient()
        {
            var patient = TestUtil.GetFhirMessage("DeletePatient") as Patient;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);

                Assert.NotNull(result);
                Assert.IsInstanceOf<Patient>(result);

                var actual = (Patient) result;

                result = patientResourceHandler.Delete(actual.Id, TransactionMode.Commit);

                Assert.Throws<KeyNotFoundException>(() => patientResourceHandler.Read(result.Id, result.VersionId));
            }
        }

        /// <summary>
        /// Test deleting a patient with an invalid guid in <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeletePatientInvalidGuid()
        {
            var patient = TestUtil.GetFhirMessage("DeletePatient") as Patient;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);

                Assert.NotNull(result);
                Assert.IsInstanceOf<Patient>(result);

                var actual = (Patient) result;

                Assert.Throws<KeyNotFoundException>(() => patientResourceHandler.Delete(Guid.NewGuid().ToString(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the query functionality in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestQueryPatient()
        {
            var patient = TestUtil.GetFhirMessage("QueryPatient") as Patient;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                var queryResult = patientResourceHandler.Query(new NameValueCollection
                {
                    { "_id", result.Id },
                    { "versionId", result.VersionId }
                });

                Assert.NotNull(queryResult);
                Assert.IsInstanceOf<Bundle>(queryResult);

                var queriedPatient = (Patient) queryResult.Entry.Single().Resource;

                Assert.NotNull(queriedPatient);
                Assert.IsInstanceOf<Patient>(queriedPatient);

                Assert.AreEqual("Smith", queriedPatient.Name.First().Family);
                Assert.AreEqual("Matthew", queriedPatient.Name.First().Given.First());
                Assert.AreEqual(AdministrativeGender.Male, queriedPatient.Gender);
                Assert.NotNull(queriedPatient.Telecom.First());
                Assert.AreEqual(patient.Telecom.First().Value, queriedPatient.Telecom.First().Value);
                Assert.AreEqual(patient.BirthDate, queriedPatient.BirthDate);
            }
        }

        /// <summary>
        /// Tests the query functionality for a patient by querying for the general practitioner in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestQueryPatientByGeneralPractitioner()
        {
            var practitioner = TestUtil.GetFhirMessage("CreatePatientWithGeneralPractitioner-Practitioner") as Practitioner;

            var patient = TestUtil.GetFhirMessage("CreatePatientWithGeneralPractitioner-Patient") as Patient;

            Resource actualPatient;
            Resource actualPractitioner;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                actualPractitioner = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);
                patient.GeneralPractitioner = new List<ResourceReference>
                {
                    new ResourceReference($"urn:uuid:{actualPractitioner.Id}")
                };
                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);
            }

            Assert.NotNull(actualPatient);
            Assert.NotNull(actualPractitioner);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Practitioner>(actualPractitioner);

            var createdPatient = (Patient)actualPatient;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                var queryResult = patientResourceHandler.Query(new NameValueCollection
                {
                    { "_id", createdPatient.Id },
                    { "_include", "Practitioner:generalPractitioner" }
                });

                Assert.NotNull(queryResult);
                Assert.IsInstanceOf<Bundle>(queryResult);
                Assert.AreEqual(2, queryResult.Entry.Count);

                var queriedPatient = (Patient)queryResult.Entry.First(c => c.Resource is Patient).Resource;
                var includedPractitioner = (Practitioner)queryResult.Entry.First(c => c.Resource is Practitioner).Resource;

                Assert.IsNotNull(queriedPatient);
                Assert.IsNotNull(includedPractitioner);

                Assert.AreEqual("Jordan", queriedPatient.Name.First().Given.First());
                Assert.AreEqual("Final", queriedPatient.Name.First().Given.ToList()[1]);
                Assert.IsTrue(queriedPatient.Active);
                Assert.AreEqual("905 905 9055", queriedPatient.Telecom.First().Value);
                Assert.AreEqual("123 Main Street", queriedPatient.Address.First().Line.First());
            }
        }

        /// <summary>
        /// Tests the update functionality in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdatePatient()
        {
            var patient = TestUtil.GetFhirMessage("UpdatePatient") as Patient;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Patient>(result);

            var actual = (Patient) result;

            Assert.AreEqual("Jessica", actual.Name.Single().Given.Single());
            Assert.AreEqual(AdministrativeGender.Female, actual.Gender);
            Assert.IsTrue(actual.Active);

            actual.Gender = AdministrativeGender.Male;
            actual.Active = false;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Update(actual.Id, actual, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Patient>(result);

            actual = (Patient) result;

            Assert.AreEqual(AdministrativeGender.Male, actual.Gender);
            Assert.IsFalse(actual.Active);
        }

        /// <summary>
        /// Test update functionality with an invalid resource in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdatePatientInvalidResource()
        {
            var patient = TestUtil.GetFhirMessage("UpdatePatient") as Patient;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Patient>(result);

            var actual = (Patient) result;

            Assert.AreEqual("Jessica", actual.Name.Single().Given.Single());
            Assert.AreEqual(AdministrativeGender.Female, actual.Gender);
            Assert.IsTrue(actual.Active);

            actual.Gender = AdministrativeGender.Male;
            actual.Active = false;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                Assert.Throws<InvalidDataException>(() => patientResourceHandler.Update(actual.Id, new Practitioner(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Test update functionality with Organization and Person contact roles in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdatePatientContactRole()
        {
            var patient = TestUtil.GetFhirMessage("UpdatePatient") as Patient;
            
            //create a contact person for test
            var personContacts = new List<Patient.ContactComponent>()
            { 
                new Patient.ContactComponent()
                {
                    Name = new HumanName()
                    {
                        Family = "Person",
                        Given = new List<string>() { "Test" }
                    },
                    Address = new Address()
                    {
                        State = "Ontario",
                        Country = "Canada",
                        Line = new List<string>
                        {
                            "123 Test Street"
                        },
                        City = "Hamilton"
                    },
                    Telecom = new List<ContactPoint>()
                    {
                        new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home, "222 222 2222")
                    },
                    Relationship = new List<CodeableConcept>() { new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0131", "N") },
                }
            }; 

            patient.Contact = personContacts;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);

                // create the patient using the resource handler
                Resource result = patientResourceHandler.Create(patient, TransactionMode.Commit);

                // retrieve the patient using the resource handler
                result = patientResourceHandler.Read(result.Id, result.VersionId);

                Assert.NotNull(result);
                Assert.IsInstanceOf<Patient>(result);
                var actual = (Patient)result;
                //Ensure persistence of single contact and contact details 
                Assert.AreEqual(1, actual.Contact.Count);
                var contact = actual.Contact.Single();
                Assert.AreEqual("Test", contact.Name.Given.First());
                Assert.AreEqual("Person", contact.Name.Family);
                Assert.AreEqual("123 Test Street", contact.Address.Line.First());
                Assert.AreEqual("222 222 2222", contact.Telecom.First().Value);
                Assert.IsNotEmpty(contact.Relationship); 
                var relationshipConcept = contact.Relationship.First();
                Assert.AreEqual("N", relationshipConcept.Coding.First().Code);

                //update contact to same value
                actual.Contact = personContacts;
                result= patientResourceHandler.Update(actual.Id, actual, TransactionMode.Commit);

                //query for patient
                var patientBundle =  patientResourceHandler.Query(new NameValueCollection()
                {
                    { "_id", result.Id }
                });

                //Ensure persistence of single updated contact and contact details 
                var updatedPatient = (Patient)patientBundle.Entry.First().Resource;
                Assert.AreEqual(1, updatedPatient.Contact.Count);
                contact = updatedPatient.Contact.First();
                Assert.AreEqual("123 Test Street", contact.Address.Line.First());
                Assert.IsNotEmpty(contact.Relationship); 
                relationshipConcept = contact.Relationship.First();
                Assert.AreEqual("N", relationshipConcept.Coding.First().Code);

                //ensure read is also returning single updated contact with same contact details
                result = patientResourceHandler.Read(actual.Id, actual.VersionId);
                var readPatient = (Patient)result;
                Assert.AreEqual(1, readPatient.Contact.Count);
                contact = readPatient.Contact.First();
                Assert.AreEqual("123 Test Street", contact.Address.Line.First());

                //create organization to be used as contact
                var organization = TestUtil.GetFhirMessage("Organization") as Organization;
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                var orgResult = organizationResourceHandler.Create(organization, TransactionMode.Commit);

                //create contact
                var orgContacts =  new List<Patient.ContactComponent>()
                { 
                    new Patient.ContactComponent()
                    {
                        Organization = new ResourceReference($"urn:uuid:{orgResult.Id}")
                    }
                };

                //update contact
                readPatient.Contact = orgContacts;
                result = patientResourceHandler.Update(readPatient.Id, readPatient, TransactionMode.Commit);
                
                //Ensure read is returning single contact of organization
                actual = (Patient)patientResourceHandler.Read(result.Id, result.VersionId);
                 
                //confirm organization detail
                Assert.AreEqual(1, actual.Contact.Count);
                contact = actual.Contact.First();
                Assert.True(contact.Organization.Reference.Contains(orgResult.Id));

                //ensure  previous contact info is not present
                Assert.IsNull(contact.Name);
                Assert.IsNull(contact.Address);

                //Ensure query is also returning single contact of organization
                patientBundle =  patientResourceHandler.Query(new NameValueCollection()
                {
                    { "_id", result.Id }
                });

                updatedPatient = (Patient)patientBundle.Entry.First().Resource;
                contact = updatedPatient.Contact.First();

                //confirm organization detail
                Assert.True(contact.Organization.Reference.Contains(orgResult.Id));
                //ensure  previous contact info is not present
                Assert.IsNull(contact.Name);
                Assert.IsNull(contact.Address);
            }
        }
    }
}
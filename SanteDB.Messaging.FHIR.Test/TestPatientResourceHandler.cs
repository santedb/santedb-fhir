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
 * User: Webber
 * Date: 2021-11-18
 */

using System;
using System.Collections.Generic;
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
        private readonly byte[] AUTH = {0x01, 0x02, 0x03, 0x04, 0x05};

        // Bundler 
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
        /// Tests the create functionality in the <see cref="PatientResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePatient()
        {
            var patient = TestUtil.GetFhirMessage("CreatePatient") as Patient;

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

        [Test]
        public void TestCreatePatientMultipleBirth()
        {
            var patient = new Patient
            {
                Name = new List<HumanName>
                {
                    new HumanName
                    {
                        Given = new List<string>
                        {
                            "David"
                        },
                        Family = "Melnyk"
                    }
                },
                MultipleBirth = new FhirDecimal(3),
                Active = true,
                BirthDate = FhirDateTime.Now().ToString(),
                Gender = AdministrativeGender.Male,
                Telecom = new List<ContactPoint>
                {
                    new ContactPoint(ContactPoint.ContactPointSystem.Email, ContactPoint.ContactPointUse.Work, "David@gmail.com")
                }
            };

            Console.WriteLine(TestUtil.MessageToString(patient));

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

            Assert.AreEqual(3, createdPatient.MultipleBirth);
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

        [Test]
        public void TestCreatePatientWithOrganization()
        {
            var patient = TestUtil.GetFhirMessage("CreatePatient") as Patient;

            var organization = TestUtil.GetFhirMessage("CreatePatientWithOrganization-Organization") as Organization;

            Resource actualPatient;
            Resource actualOrganization;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);

                actualOrganization = organizationResourceHandler.Create(organization, TransactionMode.Commit);
                patient.ManagingOrganization = new ResourceReference($"urn:uuid:{actualOrganization.Id}");
                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

                Assert.IsNotNull(actualPatient);
                Assert.IsInstanceOf<Patient>(actualPatient);

                var createdPatient = (Patient) actualPatient;

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
                var queryResult = patientResourceHandler.Read(result.Id, result.VersionId);

                Assert.NotNull(queryResult);
                Assert.IsInstanceOf<Patient>(queryResult);

                var queriedPatient = (Patient) queryResult;

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
    }
}
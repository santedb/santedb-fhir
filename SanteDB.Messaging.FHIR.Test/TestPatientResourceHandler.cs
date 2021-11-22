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
 * User: webber
 * Date: 2021-11-18
 */

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
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="PatientResourceHandler"/> class.
    /// </summary>
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
                    "Bundle"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PractitionerResourceHandler)),
                    new TypeReferenceConfiguration(typeof(BundleResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        /// <summary>
        /// Tests the create functionality in the <see cref="PatientResourceHandler"/>
        /// </summary>
        [Test]
        public void TestCreatePatient()
        {
            var patient = new Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Jordan"
                },
                Family = "Webber"
            });

            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Address = new List<Address>
            {
                new Address
                {
                    Country = "Canada",
                    PostalCode = "L3D 1B4",
                    City = "Hamilton"
                }
            };

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
        }

        /// <summary>
        /// Tests the create functionality with an invalid resource in the <see cref="PatientResourceHandler"/>
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
        /// Tests the delete functionality in the <see cref="PatientResourceHandler"/>
        /// </summary>
        [Test]
        public void TestDeletePatient()
        {
            var patient = new Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "John"
                },
                Family = "Smith"
            });

            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home,
                    "905 555 1212")
            };

            patient.Gender = AdministrativeGender.Male;

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
        /// Test deleting a patient with an invalid guid in <see cref="PatientResourceHandler"/>
        /// </summary>
        [Test]
        public void TestDeletePatientInvalidGuid()
        {
            var patient = new Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "John"
                },
                Family = "Smith"
            });

            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home,
                    "905 555 1212")
            };

            patient.Gender = AdministrativeGender.Male;

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

                var actual = (Patient)result;

                Assert.Throws<KeyNotFoundException>(() => patientResourceHandler.Delete(Guid.NewGuid().ToString(), TransactionMode.Commit));

            }
        }

        /// <summary>
        /// Tests the query functionality in the <see cref="PatientResourceHandler"/>
        /// </summary>
        [Test]
        public void TestQueryPatient()
        {
            var patient = new Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Matthew"
                },
                Family = "Smith"
            });

            patient.Gender = AdministrativeGender.Male;
            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Active = true;
            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Sms, ContactPoint.ContactPointUse.Mobile, "123 123 1234")
            };

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
                    { "id", result.Id }
                });

                var queriedPatient = queryResult.Entry.Select(c => c.Resource).OfType<Patient>().FirstOrDefault();

                Assert.NotNull(queriedPatient);
                Assert.IsInstanceOf<Patient>(queriedPatient);

                Assert.AreEqual("Smith", queriedPatient?.Name.First().Family);
                Assert.AreEqual("Matthew", queriedPatient?.Name.First().Given.First());
                Assert.AreEqual(AdministrativeGender.Male, queriedPatient?.Gender);
                Assert.NotNull(queriedPatient?.Telecom.First());
            }
        }

        /// <summary>
        /// Tests the update functionality in the <see cref="PatientResourceHandler"/>
        /// </summary>
        [Test]
        public void TestUpdatePatient()
        {
            var patient = new Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Jessica"
                },
                Family = "Comeau"
            });

            patient.Gender = AdministrativeGender.Female;
            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Active = true;
            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Sms, ContactPoint.ContactPointUse.Mobile, "123 123 1234")
            };

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
        /// Test update functionality with an invalid resource in <see cref="PatientResourceHandler"/>
        /// </summary>
        [Test]
        public void TestUpdatePatientInvalidResource()
        {
            var patient = new Patient();

            patient.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Jessica"
                },
                Family = "Comeau"
            });

            patient.Gender = AdministrativeGender.Female;
            patient.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            patient.Active = true;
            patient.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Sms, ContactPoint.ContactPointUse.Mobile, "123 123 1234")
            };

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

            var actual = (Patient)result;

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
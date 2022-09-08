/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-5-30
 */
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="MedicationResourceHandler"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestMedicationResourceHandler : DataTest
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
        /// Runs setup before each test execution.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            TestApplicationContext.TestAssembly = typeof(TestOrganizationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Medication"
                },
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(MedicationResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        /// <summary>
        /// Tests the create functionality of the <see cref="MedicationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateMedication()
        {
            var medication = new Medication
            {
                //Code = new CodeableConcept("http://snomed.info/sct", "261000", "Codeine phosphate (substance)", null),
                Id = Guid.NewGuid().ToString(),
                Status = Medication.MedicationStatusCodes.Active,
                Batch = new Medication.BatchComponent
                {
                    ExpirationDateElement = new FhirDateTime(2022, 12, 01),
                    LotNumber = "12345"
                }
            };

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

                actual = medicationResourceHandler.Create(medication, TransactionMode.Commit);
                actual = medicationResourceHandler.Read(actual.Id, actual.VersionId);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);

            var actualMedication = (Medication) actual;

            Assert.AreEqual(medication.Id, actualMedication.Id);
            Assert.AreEqual(Medication.MedicationStatusCodes.Active, actualMedication.Status);
            Assert.AreEqual("2022-12-01T00:00:00-05:00", actualMedication.Batch.ExpirationDateElement.Value);
            Assert.AreEqual("12345", actualMedication.Batch.LotNumber);
        }

        /// <summary>
        /// Tests the update functionality of the <see cref="MedicationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdateMedication()
        {
            var medication = new Medication
            {
                //Code = new CodeableConcept("http://snomed.info/sct", "261000", "Codeine phosphate (substance)", null),
                Id = Guid.NewGuid().ToString(),
                Status = Medication.MedicationStatusCodes.Active,
                Batch = new Medication.BatchComponent
                {
                    ExpirationDateElement = new FhirDateTime(2022, 12, 01),
                    LotNumber = "12345"
                }
            };

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

                actual = medicationResourceHandler.Create(medication, TransactionMode.Commit);
                actual = medicationResourceHandler.Read(actual.Id, actual.VersionId);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);

            var actualMedication = (Medication)actual;

            Assert.AreEqual(medication.Id, actualMedication.Id);
            Assert.AreEqual(Medication.MedicationStatusCodes.Active, actualMedication.Status);
            Assert.AreEqual("2022-12-01T00:00:00-05:00", actualMedication.Batch.ExpirationDateElement.Value);
            Assert.AreEqual("12345", actualMedication.Batch.LotNumber);

            actualMedication.Status = Medication.MedicationStatusCodes.EnteredInError;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

                actual = medicationResourceHandler.Update(actualMedication.Id, actualMedication, TransactionMode.Commit);
                actual = medicationResourceHandler.Read(actual.Id, actual.VersionId);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);

            actualMedication = (Medication)actual;

            Assert.AreEqual(medication.Id, actualMedication.Id);
            Assert.AreEqual(Medication.MedicationStatusCodes.EnteredInError, actualMedication.Status);
            Assert.AreEqual("2022-12-01T00:00:00-05:00", actualMedication.Batch.ExpirationDateElement.Value);
            Assert.AreEqual("12345", actualMedication.Batch.LotNumber);
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="MedicationResourceHandler" /> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);
            var methodInfo = typeof(MedicationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);
            var interactions = methodInfo.Invoke(medicationResourceHandler, null);

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
    }
}

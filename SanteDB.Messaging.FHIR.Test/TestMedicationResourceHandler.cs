﻿using FirebirdSql.Data.FirebirdClient;
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

namespace SanteDB.Messaging.FHIR.Test
{
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
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestOrganizationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

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
                Id = Guid.NewGuid().ToString()
            };

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

                actual = medicationResourceHandler.Create(medication, TransactionMode.Commit);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);
            Assert.AreEqual(medication.Id, actual.Id);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

                actual = medicationResourceHandler.Read(actual.Id, actual.VersionId);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);
            Assert.AreEqual(medication.Id, actual.Id);
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
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

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
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);

            actualMedication = (Medication)actual;

            Assert.AreEqual(medication.Id, actualMedication.Id);
            Assert.AreEqual(Medication.MedicationStatusCodes.EnteredInError, actualMedication.Status);
            Assert.AreEqual("2022-12-01T00:00:00-05:00", actualMedication.Batch.ExpirationDateElement.Value);
            Assert.AreEqual("12345", actualMedication.Batch.LotNumber);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var medicationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Medication);

                Assert.NotNull(medicationResourceHandler);

                actual = medicationResourceHandler.Read(actualMedication.Id, actualMedication.VersionId);
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Medication>(actual);

            actualMedication = (Medication)actual;

            Assert.AreEqual(medication.Id, actualMedication.Id);
            Assert.AreEqual(Medication.MedicationStatusCodes.EnteredInError, actualMedication.Status);
            Assert.AreEqual("2022-12-01T00:00:00-05:00", actualMedication.Batch.ExpirationDateElement.Value);
            Assert.AreEqual("12345", actualMedication.Batch.LotNumber);
        }
    }
}

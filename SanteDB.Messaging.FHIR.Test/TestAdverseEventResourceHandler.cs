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
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="AdverseEventResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestAdverseEventResourceHandler : DataTest
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
        /// Set up method to initialize services.
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
                    "Practitioner",
                    "AdverseEvent",
                    "Location"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PractitionerResourceHandler)),
                    new TypeReferenceConfiguration(typeof(AdverseEventResourceHandler)),
                    new TypeReferenceConfiguration(typeof(PatientResourceHandler)),
                    new TypeReferenceConfiguration(typeof(LocationResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        [Test]
        public void TestCreateAdverseEvent()
        {
            var patient = TestUtil.GetFhirMessage("ObservationSubject") as Patient;
            var practitioner = TestUtil.GetFhirMessage("ObservationPerformer") as Practitioner;
            var location = TestUtil.GetFhirMessage("CreateLocation") as Location;
            var adverseEvent = new AdverseEvent
            {
                Identifier = new Identifier
                {
                    System = "http://santedb.org/fhir/test",
                    Value = "6344"
                }
            };

           // adverseEvent.Category.Add(new CodeableConcept("http://hl7.org/fhir/ValueSet/adverse-event-category", "product-use-error"));
            adverseEvent.Event = new CodeableConcept("http://santedb.org/conceptset/v3-FamilyMember", "BRO");
            adverseEvent.Outcome = new CodeableConcept("http://hl7.org/fhir/ValueSet/adverse-event-outcome", "recovering");
            adverseEvent.Seriousness = new CodeableConcept("http://hl7.org/fhir/ValueSet/adverse-event-seriousness", "Non-serious");

            adverseEvent.DateElement = new FhirDateTime(DateTimeOffset.Parse("2021-11-29T08:06:32-05:00"));
            Console.WriteLine(TestUtil.MessageToString(adverseEvent));

            Resource actualPatient;
            Resource actualPractitioner;
            Resource actualAdverseEvent;
            Resource actualLocation;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var patientResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                actualPatient = patientResourceHandler.Create(patient, TransactionMode.Commit);

                var practionnerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);
                actualPractitioner = practionnerResourceHandler.Create(practitioner, TransactionMode.Commit);

                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);
                actualLocation = locationResourceHandler.Create(location, TransactionMode.Commit);

                var adverseEventResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.AdverseEvent);

                adverseEvent.Subject = new ResourceReference($"urn:uuid:{actualPatient.Id}");
                adverseEvent.Recorder = new ResourceReference($"urn:uuid:{actualPractitioner.Id}");
                adverseEvent.Location = new ResourceReference($"urn:uuid:{actualLocation.Id}");

                actualAdverseEvent = adverseEventResourceHandler.Create(adverseEvent, TransactionMode.Commit);
            }

            Assert.NotNull(actualPatient);
            Assert.NotNull(actualPractitioner);
            Assert.NotNull(actualLocation);
            Assert.NotNull(actualAdverseEvent);

            Assert.IsInstanceOf<Patient>(actualPatient);
            Assert.IsInstanceOf<Practitioner>(actualPractitioner);
            Assert.IsInstanceOf<AdverseEvent>(actualAdverseEvent);

            var createdPatient = (Patient)actualPatient;
            var createdPractitioner = (Practitioner)actualPractitioner;
            var createdAdverseEvent = (AdverseEvent)actualAdverseEvent;

            Assert.NotNull(createdPatient);
            Assert.NotNull(createdPractitioner);
            Assert.NotNull(createdAdverseEvent);

            Resource actual;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var adverseEventResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.AdverseEvent);

                actual = adverseEventResourceHandler.Read(createdAdverseEvent.Id, null);
            }

            Assert.NotNull(actual);

            Assert.IsInstanceOf<AdverseEvent>(actual);

            var retrievedAdverseEvent = (AdverseEvent)actual;

            Assert.AreEqual(createdAdverseEvent.Id, retrievedAdverseEvent.Id);
            Assert.IsNotNull(retrievedAdverseEvent.Subject);
            Assert.IsNotNull(retrievedAdverseEvent.Recorder);
        }
    }
}
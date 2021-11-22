using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Test complex relationships
    /// </summary>
    public class TestRelatedPersonResourceHandler : DataTest
    {

        private readonly byte[] AUTH = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        private IRepositoryService<Core.Model.Roles.Patient> m_patientRepository;

        private IRepositoryService<Core.Model.Entities.Person> m_personRepository;

        private IRepositoryService<Core.Model.Entities.EntityRelationship> m_relationshipRepository;

        // Bundler 
        private IServiceManager m_serviceManager;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FirebirdSql.Data.FirebirdClient.FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestRelatedPersonResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            this.m_patientRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Roles.Patient>>();
            this.m_personRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Entities.Person>>();
            this.m_relationshipRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Core.Model.Entities.EntityRelationship>>();

            var testConfiguration = new SanteDB.Messaging.FHIR.Configuration.FhirServiceConfigurationSection()
            {
                Resources = new System.Collections.Generic.List<string>()
                {
                    "Patient",
                    "RelatedPerson",
                    "Bundle"
                },
                OperationHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                ExtensionHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                ProfileHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>(),
                MessageHandlers = new System.Collections.Generic.List<SanteDB.Core.Configuration.TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PatientResourceHandler)),
                    new TypeReferenceConfiguration(typeof(BundleResourceHandler)),
                    new TypeReferenceConfiguration(typeof(RelatedPersonResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        /// <summary>
        /// Test persistence of a complex patient
        /// </summary>
        [Test]
        public void TestPersistComplexPatient()
        {

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {

                // Load the Complex Patient Message
                var request = TestUtil.GetFhirMessage("ComplexPatient") as Bundle;
                var messageString = TestUtil.MessageToString(request);
                var sourcePatient = request.Entry.FirstOrDefault(o => o.Resource.TypeName == "Patient").Resource as Hl7.Fhir.Model.Patient;

                var result = FhirResourceHandlerUtil.GetMappersFor(Hl7.Fhir.Model.ResourceType.Bundle).First().Create(request, Core.Services.TransactionMode.Commit);
                messageString = TestUtil.MessageToString(result);

                Assert.IsInstanceOf<Bundle>(result);
                var bresult = result as Bundle;
                Assert.AreEqual(1, bresult.Entry.Count(o => o.Resource is Hl7.Fhir.Model.Patient));
                Assert.AreEqual(1, bresult.Entry.Count(o => o.Resource is RelatedPerson));
                var createdFhirPatient = bresult.Entry.FirstOrDefault(o => o.Resource.TypeName == "Patient").Resource;
                var createdFhirRelatedPerson = bresult.Entry.FirstOrDefault(o => o.Resource.TypeName == "RelatedPerson").Resource;

                // Ensure that the message is saved correctly
                var sdbPatient = this.m_patientRepository.Get(Guid.Parse(createdFhirPatient.Id));
                Assert.IsNotNull(sdbPatient);

                // Ensure that the person is related correctly
                var sdbPerson = this.m_relationshipRepository.Get(Guid.Parse(createdFhirRelatedPerson.Id));
                Assert.IsNotNull(sdbPerson);
                Assert.AreEqual(sdbPatient.Key, sdbPerson.HolderKey);

                // Get back the RelatedPerson
                var relatedPersonHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson);
                var originalRelatedPerson = relatedPersonHandler.Read(sdbPerson.Key.ToString(), String.Empty);
                messageString = TestUtil.MessageToString(originalRelatedPerson);

                // Ensure that a QUERY in FHIR returns the result
                var patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-1234");
                var queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
                messageString = TestUtil.MessageToString(queryResult);

                // Get the patient - and mother
                patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-1234");
                query.Add("_revinclude", "RelatedPerson:patient");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
                Assert.AreEqual(2, queryResult.Entry.Count);
                messageString = TestUtil.MessageToString(queryResult);

                // Grab the related person 
                var sourceRelatedPerson = queryResult.Entry.Select(o => o.Resource).OfType<RelatedPerson>().First();
                Assert.AreEqual(sdbPerson.Key.ToString(), sourceRelatedPerson.Id);

                // The Persistence layer did not create a patient
                query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-9988");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(0, queryResult.Total);
                messageString = TestUtil.MessageToString(queryResult);

                // Attempt to update the related person's name
                sourceRelatedPerson.Name.First().Family = "SINGH";
                messageString = TestUtil.MessageToString(sourceRelatedPerson);
                var afterRelatedPerson = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson).Update(sourceRelatedPerson.Id, sourceRelatedPerson, TransactionMode.Commit);
                Assert.AreEqual(sdbPerson.Key.ToString(), afterRelatedPerson.Id);
                sdbPerson = this.m_relationshipRepository.Get(Guid.Parse(createdFhirRelatedPerson.Id));
                Assert.IsTrue(sdbPerson.LoadProperty(o => o.TargetEntity).Names.First().Component.Any(c => c.Value == "SINGH"));
                messageString = TestUtil.MessageToString(afterRelatedPerson);

                // The persistence layer did create a SARAH SINGH person
                patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson);
                query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-9988");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
                messageString = TestUtil.MessageToString(queryResult);
                Assert.AreEqual(afterRelatedPerson.Id, queryResult.Entry.First().Resource.Id);

                // Attempt to delete the related person
                var deletedRelatedPerson = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson).Delete(sourceRelatedPerson.Id, TransactionMode.Commit);
                Assert.NotNull(deletedRelatedPerson);
                Assert.IsInstanceOf<RelatedPerson>(deletedRelatedPerson);
                var actual = (RelatedPerson)deletedRelatedPerson;
                // ensure the related person is NOT active
                Assert.IsFalse(actual.Active);

                // read the related person
                var readPerson = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson).Read(sourceRelatedPerson.Id, sourceRelatedPerson.VersionId);
                Assert.NotNull(readPerson);
                Assert.IsInstanceOf<RelatedPerson>(readPerson);
                var readRelatedPerson = (RelatedPerson)readPerson;
                // ensure the related person is NOT active
                Assert.IsFalse(readRelatedPerson.Active);
            }
        }

        /// <summary>
        /// Tests the FHIR Patient->RP->Patient stuff
        /// </summary>
        [Test]
        public void TestPersistComplexPatientPatientRelationship()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {

                // Load the Complex Patient Message
                var request = TestUtil.GetFhirMessage("ComplexPatientPatientRelationship") as Bundle;

                var sourcePatient = request.Entry.FirstOrDefault(o => o.Resource.TypeName == "Patient").Resource as Hl7.Fhir.Model.Patient;
                var relatedPatient = request.Entry.LastOrDefault(o => o.Resource.TypeName == "Patient").Resource as Hl7.Fhir.Model.Patient;

                // Patients must be different
                Assert.AreNotEqual(sourcePatient.Id, relatedPatient.Id);

                //
                var result = FhirResourceHandlerUtil.GetMappersFor(Hl7.Fhir.Model.ResourceType.Bundle).First().Create(request, Core.Services.TransactionMode.Commit);
                Assert.IsInstanceOf<Bundle>(result);
                var bresult = result as Bundle;
                Assert.AreEqual(2, bresult.Entry.Count(o => o.Resource is Hl7.Fhir.Model.Patient));
                Assert.AreEqual(1, bresult.Entry.Count(o => o.Resource is RelatedPerson));

                var createdFhirPatient = bresult.Entry.FirstOrDefault(o => o.Resource.TypeName == "Patient").Resource;
                var createdFhirRelatedPerson = bresult.Entry.FirstOrDefault(o => o.Resource.TypeName == "RelatedPerson").Resource;
                var createdFhirRelatedPatient = bresult.Entry.LastOrDefault(o => o.Resource.TypeName == "Patient").Resource;

                // Ensure focal patient was created
                var sdbFocalPatient = this.m_patientRepository.Get(Guid.Parse(createdFhirPatient.Id));
                Assert.IsNotNull(sdbFocalPatient);

                // Ensure related patient was created
                var sdbRelatedPatient = this.m_patientRepository.Get(Guid.Parse(createdFhirRelatedPatient.Id));
                Assert.IsNotNull(sdbRelatedPatient);

                // Ensure that the focal <> related patient was created
                var sdbRelationship = this.m_relationshipRepository.Get(Guid.Parse(createdFhirRelatedPerson.Id));
                Assert.IsNotNull(sdbRelationship); // was saved
                Assert.AreEqual(sdbFocalPatient.Key, sdbRelationship.HolderKey); // Focal patient is the holder
                                                                                 // Relationship target is a patient
                Assert.IsInstanceOf<Core.Model.Entities.Person>(sdbRelationship.LoadProperty(o => o.TargetEntity));

                // There is a patient related to the person
                var equivRel = sdbRelatedPatient.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.EquivalentEntity);
                Assert.IsNotNull(equivRel);
                // Assert created patient is a the equivalent entity
                Assert.AreEqual(sdbRelatedPatient.Key, equivRel.SourceEntityKey); // Related Patient is the holder

                // Assert that the target of the related patient is the target of the relationship
                Assert.AreEqual(sdbRelationship.TargetEntityKey, equivRel.TargetEntityKey);

                // Ensure that there is a separate PERSON and separate PATIENT with the same identity 
                var relatedPersons = this.m_personRepository.Find(o => o.Identifiers.Any(id => id.Authority.Url == "http://santedb.org/fhir/test" && id.Value == "FHR-4321"));
                Assert.AreEqual(2, relatedPersons.Count());
                Assert.AreEqual(1, relatedPersons.OfType<Core.Model.Roles.Patient>().Count());

                // Ensure that a QUERY for focal patient returns
                var patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-4322");
                var queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);

                // Ensure a QUERY for related person returns
                query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-4321");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);

                // Ensure Query for RelatedPerson returns 1 
                patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson);
                query = new System.Collections.Specialized.NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-4321");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
            }
        }
    }
}
/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-6-21
 */
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Patient = SanteDB.Core.Model.Roles.Patient;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Test complex relationships
    /// </summary
    [ExcludeFromCodeCoverage]
    public class TestRelatedPersonResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        private IRepositoryService<Patient> m_patientRepository;

        private IRepositoryService<Person> m_personRepository;

        private IRepositoryService<EntityRelationship> m_relationshipRepository;

        [SetUp]
        public void DoSetup()
        {
            this.m_patientRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Patient>>();
            this.m_personRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Person>>();
            this.m_relationshipRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<EntityRelationship>>();
        }

        /// <summary>
        /// Test persistence of a complex patient
        /// </summary>
        [Test]
        public void TestPersistComplexPatient()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // Load the Complex Patient Message
                var request = TestUtil.GetFhirMessage("ComplexPatient") as Bundle;
                var messageString = TestUtil.MessageToString(request);
                var sourcePatient = request.Entry.FirstOrDefault(o => o.Resource.TypeName == "Patient").Resource as Hl7.Fhir.Model.Patient;

                var result = FhirResourceHandlerUtil.GetMappersFor(ResourceType.Bundle).First().Create(request, TransactionMode.Commit);
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
                var originalRelatedPerson = relatedPersonHandler.Read(sdbPerson.Key.ToString(), string.Empty);
                messageString = TestUtil.MessageToString(originalRelatedPerson);

                // Ensure that a QUERY in FHIR returns the result
                var patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var query = new NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-1234");
                var queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
                messageString = TestUtil.MessageToString(queryResult);

                // Get the patient - and mother
                patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                query = new NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-1234");
                query.Add("_revinclude", "RelatedPerson:patient");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
                Assert.AreEqual(2, queryResult.Entry.Count);
                messageString = TestUtil.MessageToString(queryResult);

                // Grab the related person 
                var sourceRelatedPerson = queryResult.Entry.Select(o => o.Resource).OfType<RelatedPerson>().First();
                Assert.AreEqual(sdbPerson.Key.ToString(), sourceRelatedPerson.Id);
                Assert.AreEqual("Ontario", sourceRelatedPerson.Address.First().State);
                Assert.AreEqual(1, sourceRelatedPerson.Telecom.Count);
                Assert.AreEqual("905 617 2020", sourceRelatedPerson.Telecom.First().Value);
                Assert.AreEqual("25 Tindale Crt", sourceRelatedPerson.Address.First().District);

                // The Persistence layer did not create a patient
                query = new NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-9988");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(0, queryResult.Total);
                messageString = TestUtil.MessageToString(queryResult);

                // Attempt to update the related person's name
                sourceRelatedPerson.Name.First().Family = "SINGH";
                sourceRelatedPerson.Address.First().Use = Address.AddressUse.Old;
                sourceRelatedPerson.Telecom.RemoveAt(0);
                messageString = TestUtil.MessageToString(sourceRelatedPerson);

                var afterRelatedPerson = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson).Update(sourceRelatedPerson.Id, sourceRelatedPerson, TransactionMode.Commit);
                Assert.AreEqual(sdbPerson.Key.ToString(), afterRelatedPerson.Id);
                sdbPerson = this.m_relationshipRepository.Get(Guid.Parse(createdFhirRelatedPerson.Id));
                Assert.IsTrue(sdbPerson.LoadProperty(o => o.TargetEntity).Names.First().Component.Any(c => c.Value == "SINGH"));
                Assert.AreEqual(Address.AddressUse.Old, sourceRelatedPerson.Address.First().Use);
                Assert.IsFalse(sourceRelatedPerson.Telecom.Any());
                messageString = TestUtil.MessageToString(afterRelatedPerson);

                // The persistence layer did create a SARAH SINGH person
                patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson);
                query = new NameValueCollection();
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
                var readPerson = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson).Read(deletedRelatedPerson.Id, deletedRelatedPerson.VersionId);
                Assert.NotNull(readPerson);
                Assert.IsInstanceOf<RelatedPerson>(readPerson);
                var readRelatedPerson = (RelatedPerson)readPerson;
                Assert.AreEqual(Address.AddressUse.Old, readRelatedPerson.Address.First().Use);
                Assert.IsFalse(readRelatedPerson.Telecom.Any());
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
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // Load the Complex Patient Message
                var request = TestUtil.GetFhirMessage("ComplexPatientPatientRelationship") as Bundle;

                var sourcePatient = request.Entry.FirstOrDefault(o => o.Resource.TypeName == "Patient").Resource as Hl7.Fhir.Model.Patient;
                var relatedPatient = request.Entry.LastOrDefault(o => o.Resource.TypeName == "Patient").Resource as Hl7.Fhir.Model.Patient;

                // Patients must be different
                Assert.AreNotEqual(sourcePatient.Id, relatedPatient.Id);

                //
                var result = FhirResourceHandlerUtil.GetMappersFor(ResourceType.Bundle).First().Create(request, TransactionMode.Commit);
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
                Assert.IsInstanceOf<Person>(sdbRelationship.LoadProperty(o => o.TargetEntity));

                // There is a patient related to the person
                var equivRel = sdbRelatedPatient.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.EquivalentEntity);
                Assert.IsNotNull(equivRel);
                // Assert created patient is a the equivalent entity
                Assert.AreEqual(sdbRelatedPatient.Key, equivRel.SourceEntityKey); // Related Patient is the holder

                // Assert that the target of the related patient is the target of the relationship
                Assert.AreEqual(sdbRelationship.TargetEntityKey, equivRel.TargetEntityKey);

                // Ensure that there is a separate PERSON and separate PATIENT with the same identity 
                var relatedPersons = this.m_personRepository.Find(o => o.Identifiers.Any(id => id.IdentityDomain.Url == "http://santedb.org/fhir/test" && id.Value == "FHR-4321"));
                Assert.AreEqual(2, relatedPersons.Count());
                Assert.AreEqual(1, relatedPersons.OfType<Patient>().Count());

                // Ensure that a QUERY for focal patient returns
                var patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Patient);
                var query = new NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-4322");
                var queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);

                // Ensure a QUERY for related person returns
                query = new NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-4321");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);

                // Ensure Query for RelatedPerson returns 1 
                patientHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson);
                query = new NameValueCollection();
                query.Add("identifier", "http://santedb.org/fhir/test|FHR-4321");
                queryResult = patientHandler.Query(query);
                Assert.AreEqual(1, queryResult.Total);
            }
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="RelatedPersonResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var relatedPersonResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.RelatedPerson);
            var methodInfo = typeof(RelatedPersonResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(methodInfo);

            var interactions = methodInfo.Invoke(relatedPersonResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);

            var resourceInteractionComponents = ((IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions).ToArray();

            Assert.AreEqual(5, resourceInteractionComponents.Length);
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Create));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Read));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.SearchType));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Delete));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Update));
        }
    }
}
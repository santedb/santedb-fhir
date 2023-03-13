/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="PractitionerResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestPractitionerResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };


        /// <summary>
        /// Tests the create functionality using invalid resource in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // expect that the create method throws an InvalidDataException
                Assert.Throws<ArgumentException>(() => practitionerResourceHandler.Create(new Account(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the create functionality in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreatePractitioner()
        {
            //load the practitioner for create
            var practitioner = TestUtil.GetFhirMessage("CreatePractitioner") as Practitioner;

            Resource result;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                result = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                // retrieve the practitioner using the resource handler
                result = practitionerResourceHandler.Read(result.Id, result.VersionId);
            }

            //assert the results are correct
            Assert.NotNull(result);
            Assert.IsInstanceOf<Practitioner>(result);
            var actual = (Practitioner)result;
            var numOfIdentifiers = actual.Identifier.FindAll(i => i.Value == "6324").Count;
            Assert.AreEqual(1, numOfIdentifiers);
            Assert.AreEqual("Practitioner", actual.Name.Single().Family);
            Assert.AreEqual("Test", actual.Name.Single().Given.Single());
            //loaded practitioner must have 5 character code for communication, otherwise firebird test db will add trailing spaces
            //and cause issues with following communication asserts
            Assert.IsTrue(actual.Communication.Any(c => c.Coding.Any(x => x.Code == "en-US")));
            Assert.IsTrue(actual.Communication.Any(c => c.Coding.Any(x => x.Code == "fr-CA")));
            Assert.IsTrue(actual.Photo.Any());
            Assert.IsTrue(actual.BirthDate == new DateTime(1980, 12, 1).ToString("yyyy-MM-dd"));
            Assert.IsTrue(actual.Telecom.First().Value == "905 555 1234");
        }

        /// <summary>
        /// Tests the create functionality using different identifiers in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateTwoPractitionerDifferentIdentifier()
        {
            //load the first practitioner 
            var practitioner = TestUtil.GetFhirMessage("CreatePractitioner") as Practitioner;

            //set up second practitioner with different identifier
            var secondPractitioner = TestUtil.GetFhirMessage("CreatePractitionerDifferentIdentifier") as Practitioner;

            Resource resultOne, resultTwo, pracOne, pracTwo;
            Practitioner actualPracOne, actualPracTwo;


            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                pracOne = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                //check if the practitioner is saved properly
                resultOne = practitionerResourceHandler.Read(pracOne.Id, pracOne.VersionId);
                actualPracOne = (Practitioner)resultOne;
                Assert.AreEqual("Practitioner", actualPracOne.Name.Single().Family);
                Assert.AreEqual("Test", actualPracOne.Name.Single().Given.Single());
                Assert.NotNull(resultOne);
                Assert.IsInstanceOf<Practitioner>(resultOne);

                //attempt to create the second practitioner with different identifier
                pracTwo = practitionerResourceHandler.Create(secondPractitioner, TransactionMode.Commit);

                //check if the practitioner is saved properly
                resultTwo = practitionerResourceHandler.Read(pracTwo.Id, pracTwo.VersionId);
                actualPracTwo = (Practitioner)resultTwo;
                Assert.AreEqual("PracTwo", actualPracTwo.Name.Single().Family);
                Assert.AreEqual("Second", actualPracTwo.Name.Single().Given.Single());
                Assert.NotNull(resultTwo);
                Assert.IsInstanceOf<Practitioner>(resultTwo);

                //test to ensure second create attempt with different identifier created a practitioner with different id
                Assert.AreNotEqual(actualPracOne.Id, actualPracTwo.Id);

                // read first practitioner again and confirm that properties like name wasn't updated due to second create attempt
                resultOne = practitionerResourceHandler.Read(pracOne.Id, pracOne.VersionId);
                actualPracOne = (Practitioner)resultOne;
                Assert.AreNotEqual(actualPracTwo.Name.Single().Family, actualPracOne.Name.Single().Family);
                Assert.AreNotEqual(actualPracTwo.Name.Single().Given.Single(), actualPracOne.Name.Single().Given.Single());
            }
        }

        /// <summary>
        /// Tests the create functionality using duplicate identifiers in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateTwoPractitionerSameIdentifier()
        {
            //load the first practitioner 
            var practitioner = TestUtil.GetFhirMessage("CreatePractitioner") as Practitioner;

            //load the second practitioner with same identifier
            var secondPractitioner = TestUtil.GetFhirMessage("CreatePractitionerSameIdentifier") as Practitioner;

            Resource resultOne, resultTwo, pracOne, pracTwo;
            Practitioner actualPracOne, actualPracTwo;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                pracOne = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                //check if the practitioner is saved properly
                resultOne = practitionerResourceHandler.Read(pracOne.Id, pracOne.VersionId);
                actualPracOne = (Practitioner)resultOne;
                Assert.AreEqual("Practitioner", actualPracOne.Name.Single().Family);
                Assert.AreEqual("Test", actualPracOne.Name.Single().Given.Single());
                Assert.NotNull(resultOne);
                Assert.IsInstanceOf<Practitioner>(resultOne);

                //attempt to create the second practitioner with same identifier
                pracTwo = practitionerResourceHandler.Create(secondPractitioner, TransactionMode.Commit);

                //check if the practitioner is saved properly
                resultTwo = practitionerResourceHandler.Read(pracTwo.Id, pracTwo.VersionId);
                actualPracTwo = (Practitioner)resultTwo;
                Assert.AreEqual("PracTwo", actualPracTwo.Name.Single().Family);
                Assert.AreEqual("Second", actualPracTwo.Name.Single().Given.Single());
                Assert.NotNull(resultTwo);
                Assert.IsInstanceOf<Practitioner>(resultTwo);

                //test to ensure second create attempt with same identifier just created a different version with same practitioner id
                Assert.AreEqual(actualPracOne.Id, actualPracTwo.Id);
                Assert.AreNotEqual(actualPracOne.VersionId, actualPracTwo.VersionId);

                //read first practitioner again and confirm that properties like name has been updated due to second create attempt with same identifier
                resultOne = practitionerResourceHandler.Read(pracOne.Id, pracOne.VersionId);
                actualPracOne = (Practitioner)resultOne;
                Assert.AreEqual(actualPracTwo.Name.Single().Family, actualPracOne.Name.Single().Family);
                Assert.AreEqual(actualPracTwo.Name.Single().Given.Single(), actualPracOne.Name.Single().Given.Single());
            }
        }

        /// <summary>
        /// Tests the delete functionality in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeletePractitioner()
        {
            //load the practitioner for delete
            var practitioner = TestUtil.GetFhirMessage("DeletePractitioner") as Practitioner;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                var result = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                // retrieve the practitioner using the resource handler
                result = practitionerResourceHandler.Read(result.Id, result.VersionId);

                //ensure practitioner was saved properly
                Assert.NotNull(result);
                Assert.IsInstanceOf<Practitioner>(result);
                var actual = (Practitioner)result;
                Assert.AreEqual("Test", actual.Name.Single().Given.Single());
                Assert.AreEqual("Practitioner", actual.Name.Single().Family);

                //delete practitioner
                result = practitionerResourceHandler.Delete(actual.Id, TransactionMode.Commit);

                actual = (Practitioner)result;

                //ensure read is not successful
                Assert.Throws<FhirException>(() => practitionerResourceHandler.Read(actual.Id, null));
            }
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);
            var methodInfo = typeof(PractitionerResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(methodInfo);

            var interactions = methodInfo.Invoke(practitionerResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);

            var resourceInteractionComponents = ((IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions).ToArray();

            Assert.AreEqual(7, resourceInteractionComponents.Length);
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Create));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.HistoryInstance));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Read));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.SearchType));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Vread));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Delete));
            Assert.IsTrue(resourceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Update));
        }

        /// <summary>
        /// Tests the read functionality in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestReadPractitioner()
        {
            //load up a practitioner for read test
            var practitioner = TestUtil.GetFhirMessage("ReadPractitioner") as Practitioner;

            Resource result;

            //create and read the same practitioner
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                result = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                // retrieve the practitioner using the resource handler
                result = practitionerResourceHandler.Read(result.Id, result.VersionId);
            }

            //assert that the results are correct
            Assert.NotNull(result);
            Assert.IsInstanceOf<Practitioner>(result);

            var actual = (Practitioner)result;

            Assert.AreEqual("Practitioner", actual.Name.Single().Family);
            Assert.AreEqual("Test", actual.Name.Single().Given.Single());
            Assert.IsTrue(actual.Identifier.Any(i => i.Value == "6324"));
        }

        /// <summary>
        /// Tests the update functionality in <see cref="PractitionerResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdatePractitioner()
        {
            //load up a practitioner to update
            var practitioner = TestUtil.GetFhirMessage("CreatePractitioner") as Practitioner;

            Resource result;
            Practitioner actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // create the practitioner using the resource handler
                result = practitionerResourceHandler.Create(practitioner, TransactionMode.Commit);

                // retrieve the practitioner using the resource handler
                result = practitionerResourceHandler.Read(result.Id, result.VersionId);

                Assert.NotNull(result);
                Assert.IsInstanceOf<Practitioner>(result);

                actual = (Practitioner)result;


                Assert.AreEqual("Test", actual.Name.Single().Given.Single());
                Assert.AreEqual("Practitioner", actual.Name.Single().Family);

                //update name
                actual.Name.Clear();

                actual.Name.Add(new HumanName
                {
                    Given = new List<string>
                    {
                        "UpdatedGiven"
                    },
                    Family = "UpdatedFamily"
                });


                result = practitionerResourceHandler.Update(actual.Id, actual, TransactionMode.Commit);
            }

            actual = (Practitioner)result;
            Assert.AreEqual("UpdatedGiven", actual.Name.Single().Given.Single());
            Assert.AreEqual("UpdatedFamily", actual.Name.Single().Family);

            //check to ensure previous non-updated values still exists
            Assert.IsTrue(actual.Telecom.Any(t => t.Value == "905 555 1234"));
        }
    }
}
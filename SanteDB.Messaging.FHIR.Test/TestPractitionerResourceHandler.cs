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
 * User: khannan
 * Date: 2021-11-10
 */

using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SanteDB.Core.Model.Roles;
using Patient = SanteDB.Core.Model.Roles.Patient;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="PractitionerResourceHandler"/> class.
    /// </summary>
    public class TestPractitionerResourceHandler : DataTest
    {
        private readonly byte[] AUTH = {0x01, 0x02, 0x03, 0x04, 0x05};

        private IRepositoryService<Patient> m_patientRepository;

        private IRepositoryService<Person> m_personRepository;

        private IRepositoryService<Provider> m_providerRepositoryService;

        private IRepositoryService<EntityRelationship> m_relationshipRepository;

        // Bundler 
        private IServiceManager m_serviceManager;

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
                    "Practitioner"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PractitionerResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        
        [Test]
        public void TestDeletePractitioner()
        {
            //set up a practitioner for test
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Test"
                },
                Family = "Practitioner"
            });

            practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            practitioner.Gender = AdministrativeGender.Male;
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

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

                //ensure practitioner was saved properly
                Assert.NotNull(result);
                Assert.IsInstanceOf<Practitioner>(result);
                var actual = (Practitioner) result;
                Assert.AreEqual("Test", actual.Name.Single().Given.Single());
                Assert.AreEqual("Practitioner", actual.Name.Single().Family);
                
                //delete practitioner
                result = practitionerResourceHandler.Delete(actual.Id, TransactionMode.Commit);

                actual = (Practitioner) result;

                //ensure read is not successful
                Assert.Throws<KeyNotFoundException>(() => practitionerResourceHandler.Read(actual.Id, null));

            }

        }


        [Test]
        public void TestGetInteractions()
        {
            var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();
            PractitionerResourceHandler practitionerResourceHandler = new PractitionerResourceHandler(m_providerRepositoryService, localizationService);
            MethodInfo methodInfo = typeof(PractitionerResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);
            var interactions = methodInfo.Invoke(practitionerResourceHandler, null);

            Assert.True(interactions is IEnumerable<CapabilityStatement.ResourceInteractionComponent>);
            var resouceInteractionComponents = (IEnumerable<CapabilityStatement.ResourceInteractionComponent>)interactions;
            Assert.AreEqual(7, resouceInteractionComponents.Count());
            Assert.IsTrue(resouceInteractionComponents.Any(c => c.Code == CapabilityStatement.TypeRestfulInteraction.Create));

        }

        [Test]
        public void TestCreatePractitioner()
        {
            // set up a practitioner for test
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Test"
                },
                Family = "Practitioner"
            });

            //we need to supply 5 character code for communication, otherwise firebird test db will add trailing spaces
            practitioner.Communication.Add(new CodeableConcept("http://tools.ietf.org/html/bcp47", "en-US"));
            practitioner.Communication.Add(new CodeableConcept("http://tools.ietf.org/html/bcp47", "fr-CA"));
            

            practitioner.Extension.Add(new Extension("http://santedb.org/extensions/core/jpegPhoto", new Base64Binary(new byte[]{0x01, 0x02, 0x03, 0x04, 0x05})));
            practitioner.Extension.Add(new Extension("http://santedb.org/extensions/core/detectedIssue", new Base64Binary(new byte[]{0x01, 0x02, 0x03, 0x04, 0x05})));            
            practitioner.Identifier.Add(new Hl7.Fhir.Model.Identifier("http://santedb.org/fhir/test", "6324"));
            practitioner.BirthDate = new DateTime(1980, 12, 1).ToString("yyyy-MM-dd");
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

            practitioner.Photo.Add(new Attachment()
            {
                Data = new byte[]{0x01, 0x02, 0x03, 0x04, 0x05}
            });

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
            var actual = (Practitioner) result;
            var numOfIdentifiers = actual.Identifier.FindAll(i => i.Value == "6324").Count;
            Assert.AreEqual(1, numOfIdentifiers);
            Assert.AreEqual("Practitioner", actual.Name.Single().Family);
            Assert.AreEqual("Test", actual.Name.Single().Given.Single());
            Assert.IsTrue(actual.Communication.Any(c => c.Coding.Any(x => x.Code == "en-US")));
            Assert.IsTrue(actual.Communication.Any(c => c.Coding.Any(x => x.Code == "fr-CA")));
            Assert.IsTrue(actual.Extension.Any(e => e.Url == "http://santedb.org/extensions/core/detectedIssue"));
            Assert.IsTrue(actual.Photo.Any());
            Assert.IsTrue(actual.BirthDate ==  new DateTime(1980, 12, 1).ToString("yyyy-MM-dd"));
            Assert.IsTrue(actual.Telecom.First().Value == "905 555 1234");
            
        }

        [Test]
        public void TestTwoPractitionerSameIdentifier()
        {
            //  set up first practitioner
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "First"
                },
                Family = "PracOne"
            });

            practitioner.Identifier.Add(new Hl7.Fhir.Model.Identifier("http://santedb.org/fhir/test", "6666"));
            practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            practitioner.Gender = AdministrativeGender.Male;
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

            //set up second practitioner
            var secondPractitioner = new Practitioner();
            secondPractitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Second"
                },
                Family = "PracTwo"
            });
            //add duplicate identifier for test
            secondPractitioner.Identifier.Add(new Hl7.Fhir.Model.Identifier("http://santedb.org/fhir/test", "6666"));
            secondPractitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            secondPractitioner.Gender = AdministrativeGender.Male;
            secondPractitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

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
                Assert.AreEqual("PracOne", (actualPracOne.Name.Single().Family));
                Assert.AreEqual("First", actualPracOne.Name.Single().Given.Single());
                Assert.NotNull(resultOne);
                Assert.IsInstanceOf<Practitioner>(resultOne);
                
                //attempt to create the second practitioner with same identifier
                pracTwo = practitionerResourceHandler.Create(secondPractitioner, TransactionMode.Commit);
                
                //check if the practitioner is saved properly
                resultTwo = practitionerResourceHandler.Read(pracTwo.Id, pracTwo.VersionId);
                actualPracTwo = (Practitioner)resultTwo;
                Assert.AreEqual("PracTwo", (actualPracTwo.Name.Single().Family));
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

         [Test]
        public void TestTwoPractitionerDifferentIdentifier()
        {
            // set up first practitioner
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "First"
                },
                Family = "PracOne"
            });

            
            practitioner.Identifier.Add(new Hl7.Fhir.Model.Identifier("http://santedb.org/fhir/test", "1111"));
            practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

            //set up second practitioner
            var secondPractitioner = new Practitioner();
            secondPractitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Second"
                },
                Family = "PracTwo"
            });

            //add duplicate identifier for test
            secondPractitioner.Identifier.Add(new Hl7.Fhir.Model.Identifier("http://santedb.org/fhir/test", "2222"));
            secondPractitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            secondPractitioner.Gender = AdministrativeGender.Male;
            secondPractitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };

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
                Assert.AreEqual("PracOne", (actualPracOne.Name.Single().Family));
                Assert.AreEqual("First", actualPracOne.Name.Single().Given.Single());
                Assert.NotNull(resultOne);
                Assert.IsInstanceOf<Practitioner>(resultOne);
                
                //attempt to create the second practitioner with different identifier
                pracTwo = practitionerResourceHandler.Create(secondPractitioner, TransactionMode.Commit);
                
                //check if the practitioner is saved properly
                resultTwo = practitionerResourceHandler.Read(pracTwo.Id, pracTwo.VersionId);
                actualPracTwo = (Practitioner)resultTwo;
                Assert.AreEqual("PracTwo", (actualPracTwo.Name.Single().Family));
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

        [Test]
        public void TestReadPractitioner()
        {
            //set up a practitioner
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Test"
                },
                Family = "Practitioner"
            });

            practitioner.Identifier.Add(new Hl7.Fhir.Model.Identifier("http://santedb.org/fhir/test", "6324"));
            practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            practitioner.Address.Add(new Address()
            {
                City = "Hamilton",
                Country = "Canada"

            });
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };


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

            var actual = (Practitioner) result;

            Assert.AreEqual("Practitioner", actual.Name.Single().Family);
            Assert.AreEqual("Test", actual.Name.Single().Given.Single());
            Assert.IsTrue(actual.Identifier.Any(i => i.Value == "6324"));
        }

         [Test]
        public void TestUpdatePractitioner()
        {
            //set up a practitioner
            var practitioner = new Practitioner();

            practitioner.Name.Add(new HumanName
            {
                Given = new List<string>
                {
                    "Test"
                },
                Family = "Patient"
            });

            practitioner.BirthDate = DateTime.Now.ToString("yyyy-MM-dd");
            practitioner.Telecom = new List<ContactPoint>
            {
                new ContactPoint(ContactPoint.ContactPointSystem.Fax, ContactPoint.ContactPointUse.Work, "905 555 1234")
            };


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

                actual = (Practitioner) result;

                
                Assert.AreEqual("Test", actual.Name.Single().Given.Single());
                Assert.AreEqual("Patient", actual.Name.Single().Family);

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
            
            actual = (Practitioner) result;
            Assert.AreEqual("UpdatedGiven", actual.Name.Single().Given.Single());
            Assert.AreEqual("UpdatedFamily", actual.Name.Single().Family);
               
            //check to ensure previous non-updated values still exists
            Assert.IsTrue(actual.Telecom.Any(t => t.Value == "905 555 1234"));

        }


        [Test]
        public void TestCreateInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                // get the resource handler
                var practitionerResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Practitioner);

                // expect that the create method throws an InvalidDataException
                Assert.Throws<InvalidDataException>(() => practitionerResourceHandler.Create(new Account(), TransactionMode.Commit));
            }
        }



    }


}
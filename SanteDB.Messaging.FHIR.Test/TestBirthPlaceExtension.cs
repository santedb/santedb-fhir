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
 * User: Zhiping Yu
 * Date: 2021-12-24
 */

using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Extensions.Patient;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Organization = SanteDB.Core.Model.Entities.Organization;
using Patient = SanteDB.Core.Model.Roles.Patient;
using SanteDB.Core.Model;
using SanteDB.Messaging.FHIR.Exceptions;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Test class for <see cref="BirthPlaceExtension"/>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestBirthPlaceExtension : DataTest
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
            TestApplicationContext.TestAssembly = typeof(TestRelatedPersonResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Patient",
                    "BirthPlaceExtension",
                    "Location"

                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(BirthPlaceExtension)),
                    new TypeReferenceConfiguration(typeof(Patient)),
                    new TypeReferenceConfiguration(typeof(Location))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="BirthPlaceExtension" /> class.
        /// With valid role and birth place.
        /// </summary>
        [Test]
        public void TestBirthPlaceExtensionConstructValidRoleBirthPlace()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();
            var birthPlace = new Place
            {
                Addresses = new List<EntityAddress>
                {
                    new EntityAddress(AddressUseKeys.HomeAddress, "25 Tindale Crt", "Hamilton", "Ontario", "Canada", "L9K 6C7")
                },
                Names = new List<EntityName>
                {
                    new EntityName(NameUseKeys.Search, "25 Tindale Crt, Hamilton, Ontario, Canada, L9K 6C7")
                }
            };

            var patient = new SanteDB.Core.Model.Roles.Patient
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Birthplace, birthPlace)
                }
            };

            var constructedBirthPlace = birthPlaceExtension.Construct(patient).ToArray();

            Assert.IsTrue(constructedBirthPlace.Any());

            var extension = constructedBirthPlace.FirstOrDefault();

            Assert.NotNull(extension);
            Assert.IsInstanceOf<Address>(extension.Value);

            var address = extension.Value as Address;

            Assert.NotNull(address);
            Assert.IsTrue(address.Line.Any());
            Assert.AreEqual("25 Tindale Crt", address.Line.Single());
            Assert.AreEqual("Hamilton", address.City);
            Assert.AreEqual("Canada", address.Country);
            Assert.AreEqual("L9K 6C7", address.PostalCode);
            Assert.AreEqual("Ontario", address.State);
            Assert.AreEqual("25 Tindale Crt, Hamilton, Ontario, Canada, L9K 6C7", address.Text);

        }

        /// <summary>
        /// Tests the construct functionality in <see cref="BirthPlaceExtension" /> class.
        /// With invalid patient.
        /// </summary>
        [Test]
        public void TestConstructFailedWithPlaceOfDeath()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();
            var birthPlace = new Place
            {
                Addresses = new List<EntityAddress>
                {
                    new EntityAddress(AddressUseKeys.HomeAddress, "25 Tindale Crt", "Hamilton", "Ontario", "Canada", "L9K 6C7")
                },
                Names = new List<EntityName>
                {
                    new EntityName(NameUseKeys.Search, "25 Tindale Crt, Hamilton, Ontario, Canada, L9K 6C7")
                }
            };

            var patient = new SanteDB.Core.Model.Roles.Patient
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.PlaceOfDeath, birthPlace)
                }
            };

            var constructedBirthPlace = birthPlaceExtension.Construct(patient).ToArray();

            Assert.IsNotNull(constructedBirthPlace);
            Assert.IsFalse(constructedBirthPlace.Any());
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="BirthPlaceExtension" /> class.
        /// With invalid role.
        /// </summary>
        [Test]
        public void TestConstructFailedWithOrganization()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();
            var birthPlace = new Place
            {
                Addresses = new List<EntityAddress>
                {
                    new EntityAddress(AddressUseKeys.HomeAddress, "25 Tindale Crt", "Hamilton", "Ontario", "Canada", "L9K 6C7")
                },
                Names = new List<EntityName>
                {
                    new EntityName(NameUseKeys.Search, "25 Tindale Crt, Hamilton, Ontario, Canada, L9K 6C7")
                }
            };
            var organization = new Organization
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Birthplace, birthPlace)
                }
            };

            var constructedBirthPlace = birthPlaceExtension.Construct(organization).ToArray();

            Assert.IsNotNull(constructedBirthPlace);
            Assert.IsFalse(constructedBirthPlace.Any());
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="BirthPlaceExtension" /> class.
        /// With invalid place.
        /// </summary>
        [Test]
        public void TestBirthPlaceExtensionConstructInvalidNames()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();

            var birthPlace = new Place
            {
                Addresses = new List<EntityAddress>
                {
                    new EntityAddress(AddressUseKeys.HomeAddress, "25 Tindale Crt", "Hamilton", "Ontario", "Canada", "L9K 6C7")
                },
                Names = new List<EntityName>
                {
                    new EntityName(NameUseKeys.Alphabetic, "25 Tindale Crt, Hamilton, Ontario, Canada, L9K 6C7")
                }
            };
            var patient = new SanteDB.Core.Model.Roles.Patient
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Birthplace, birthPlace)
                }
            };

            var constructedBirthPlace = birthPlaceExtension.Construct(patient).ToArray();

            Assert.IsNotNull(constructedBirthPlace);
            Assert.IsTrue(constructedBirthPlace.Any());

            var extension = constructedBirthPlace.FirstOrDefault();

            Assert.NotNull(extension);
            Assert.IsInstanceOf<Address>(extension.Value);

            var address = extension.Value as Address;

            Assert.IsNull(address.Text);
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="BirthPlaceExtension" /> class.
        /// With valid extension but patient has no birthplace relationship
        /// </summary>
        [Test]
        public void TestParseAddress()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();
            var location = TestUtil.GetFhirMessage("CreateLocation") as Location;
            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);
                actual = locationResourceHandler.Create(location, TransactionMode.Commit);

                var extensionForTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-birthPlace", location.Address);
                var patient = new SanteDB.Core.Model.Roles.Patient();

                birthPlaceExtension.Parse(extensionForTest, patient);

                Assert.IsNotNull(patient.Relationships);
                Assert.IsTrue(patient.Relationships.Count() == 1);
                Assert.IsInstanceOf<Place>(patient.Relationships.Single().LoadProperty(o => o.TargetEntity));
                Assert.IsTrue(patient.Relationships.Single().RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace);
            }
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="BirthPlaceExtension" /> class.
        /// With updated Text in extension but patient has no birthplace relationship
        /// </summary>
        [Test]
        public void TestParseWithAddressUpdatedText()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();
            var location = TestUtil.GetFhirMessage("CreateLocation") as Location;
            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);
                actual = locationResourceHandler.Create(location, TransactionMode.Commit);

                location.Address.Text = "321 New Street, Hamilton, Ontario, Canada";

                var extensionFortest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-birthPlace", location.Address);
                var patient = new SanteDB.Core.Model.Roles.Patient();

                Assert.Throws<FhirException>(() => birthPlaceExtension.Parse(extensionFortest, patient));
            }
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="BirthPlaceExtension" /> class.
        /// With invalid extension value.
        /// </summary>
        [Test]
        public void TestParseWithInvalidExtensionParameter()
        {
            var birthPlaceExtension = this.m_serviceManager.CreateInjected<BirthPlaceExtension>();
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-religion", new FhirString("Test"));
            var patient = new Patient();

            birthPlaceExtension.Parse(extensionforTest, patient);

            Assert.IsFalse(patient.LoadProperty(o=>o.Relationships).Any(c => c.RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace));
        }
    }
}



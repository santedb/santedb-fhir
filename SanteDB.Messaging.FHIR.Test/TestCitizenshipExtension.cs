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
using SanteDB.Core.Model;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Extensions.Patient;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Organization = SanteDB.Core.Model.Entities.Organization;
using Patient = SanteDB.Core.Model.Roles.Patient;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Test class for <see cref="CitizenshipExtension"/>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestCitizenshipExtension : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = {0x01, 0x02, 0x03, 0x04, 0x05};


        /// <summary>
        /// Tests the construct functionality in <see cref="CitizenshipExtension" /> class.
        /// With valid role and citizenship place.
        /// </summary>
        [Test]
        public void TestCitizenshipExtensionConstructValidRoleCitizenship()
        {
            var citizenshipExtension = this.m_serviceManager.CreateInjected<CitizenshipExtension>();
            var citizenPlace = new Place
            {
                Identifiers = new List<EntityIdentifier>
                {
                    new EntityIdentifier(IdentityDomainKeys.Iso3166CountryCode, "NF")
                }
            };

            var patient = new Patient
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Citizen, citizenPlace)
                }
            };

            var constructedCitizenPlace = citizenshipExtension.Construct(patient).ToArray();

            Assert.IsTrue(constructedCitizenPlace.Any());

            var extension = constructedCitizenPlace.FirstOrDefault();

            Assert.IsNotNull(extension);
            Assert.IsInstanceOf<CodeableConcept>(extension.Value);

            var concept = extension.Value as CodeableConcept;

            Assert.NotNull(concept);
            Assert.AreEqual(1, concept.Coding.Count);
            Assert.AreEqual("NF", concept.Coding.Single().Code);
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="CitizenshipExtension" /> class.
        /// With valid role and extension.
        /// </summary>
        [Test]
        public void TestCitizenshipExtensionParseValidRoleExtension()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var citizenshipExtension = this.m_serviceManager.CreateInjected<CitizenshipExtension>();
                var patient = new Patient();
                var extensionForTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-citizenship",
                    new CodeableConcept("urn:oid:1.0.3166.1.2.3", "NF"));

                citizenshipExtension.Parse(extensionForTest, patient);

                Assert.IsNotNull(patient.Relationships);
                Assert.IsTrue(patient.Relationships.Count() == 1);
                Assert.IsInstanceOf<Place>(patient.Relationships.Single().TargetEntity);
                Assert.AreEqual("NF", patient.LoadProperty(o=>o.Relationships).Single().TargetEntity.LoadProperty(o=>o.Identifiers).Single().Value);
                Assert.IsTrue(patient.Relationships.Single().RelationshipTypeKey == EntityRelationshipTypeKeys.Citizen);
                Assert.IsTrue(patient.Relationships.Single().TargetEntity.Identifiers.Any(c => c.IdentityDomainKey == IdentityDomainKeys.Iso3166CountryCode));
            }
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="CitizenshipExtension" /> class.
        /// With invalid extension.
        /// </summary>
        [Test]
        public void TestCitizenshipExtensionParseValidRoleInvalidExtension()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var citizenshipExtension = this.m_serviceManager.CreateInjected<CitizenshipExtension>();
                var patient = new Patient();
                var extensionForTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-citizenship",
                    new FhirString("Test"));

                citizenshipExtension.Parse(extensionForTest, patient);

                // JF: New pattern is that unloaded data is null
                Assert.IsNull(patient.Relationships);
            }
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="CitizenshipExtension" /> class.
        /// With invalid role.
        /// </summary>
        [Test]
        public void TestConstructFailedWithOrganization()
        {
            var citizenshipExtension = this.m_serviceManager.CreateInjected<CitizenshipExtension>();
            var citizenPlace = new Place
            {
                Identifiers = new List<EntityIdentifier>
                {
                    new EntityIdentifier(IdentityDomainKeys.Iso3166CountryCode, "NF")
                }
            };

            var organization = new Organization
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Citizen, citizenPlace)
                }
            };

            var constructedCitizenPlace = citizenshipExtension.Construct(organization).ToArray();

            Assert.IsFalse(constructedCitizenPlace.Any());
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="CitizenshipExtension" /> class.
        /// With invalid authority id for.
        /// </summary>
        [Test]
        public void TestConstructFailedWithRandomAuthorityId()
        {
            var citizenshipExtension = this.m_serviceManager.CreateInjected<CitizenshipExtension>();
            var citizenPlace = new Place
            {
                Identifiers = new List<EntityIdentifier>
                {
                    new EntityIdentifier(Guid.NewGuid(), "NF")
                }
            };

            var patient = new Patient
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Citizen, citizenPlace)
                }
            };

            var constructedCitizenPlace = citizenshipExtension.Construct(patient).ToArray();

            Assert.IsFalse(constructedCitizenPlace.Any());
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="CitizenshipExtension" /> class.
        /// With invalid relationship type.
        /// </summary>
        [Test]
        public void TestConstructFailedWithRelationshipTypeStudent()
        {
            var citizenshipExtension = this.m_serviceManager.CreateInjected<CitizenshipExtension>();
            var citizenPlace = new Place
            {
                Identifiers = new List<EntityIdentifier>
                {
                    new EntityIdentifier(IdentityDomainKeys.Iso3166CountryCode, "NF")
                }
            };

            var patient = new Patient
            {
                Relationships = new List<EntityRelationship>
                {
                    new EntityRelationship(EntityRelationshipTypeKeys.Student, citizenPlace)
                }
            };

            var constructedCitizenPlace = citizenshipExtension.Construct(patient).ToArray();

            Assert.IsFalse(constructedCitizenPlace.Any());
        }
    }
}
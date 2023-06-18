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
 * Date: 2023-5-19
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
    /// Tests the <see cref="OrganizationResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestOrganizationResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };


        /// <summary>
        /// /// <summary>
        /// Tests the create functionality in <see cref="OrganizationResourceHandler"/> class by passing Practitioner instance.
        /// </summary>
        /// </summary>
        [Test]
        public void TestCreateInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                Assert.Throws<ArgumentException>(() => organizationResourceHandler.Create(new Practitioner(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the create functionality in <see cref="OrganizationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateOrganization()
        {
            // set up the test data
            var organization = TestUtil.GetFhirMessage("Organization") as Organization;
            Resource result;
            // execute the operation under test
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Create(organization, TransactionMode.Commit);
                result = organizationResourceHandler.Read(result.Id, result.VersionId);
            }

            // assert create organization successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            var actual = (Organization)result;
            Assert.AreEqual("Hamilton Health Sciences", actual.Name);
            Assert.IsTrue(actual.Alias.All(c => c == "hhs"));
            Assert.IsTrue(actual.Address.Count == 2);
            Assert.IsTrue(actual.Identifier.First().Value == "6324");
            Assert.AreEqual("http://santedb.org/fhir/test", actual.Identifier.First().System);
            Assert.IsTrue(actual.Identifier.Count == 1);
        }

        /// <summary>
        /// Tests the create function for the <see cref="OrganizationResourceHandler"/> class
        /// with part of property.
        /// </summary>
        [Test]
        public void TestCreateOrganizationWithParent()
        {
            var parentOrganization = TestUtil.GetFhirMessage("ParentOrganization") as Organization;
            var organization = TestUtil.GetFhirMessage("Organization") as Organization;

            Resource createdParentOrganization;
            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                createdParentOrganization = organizationResourceHandler.Create(parentOrganization, TransactionMode.Commit);
                organization.PartOf = new ResourceReference($"urn:uuid:{createdParentOrganization.Id}");
                actual = organizationResourceHandler.Create(organization, TransactionMode.Commit);

            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<Organization>(actual);

            var createdOrganization = (Organization)actual;
            Assert.IsNotNull(createdOrganization);

            Resource readOrganization;
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                readOrganization = organizationResourceHandler.Read(createdOrganization.Id, createdOrganization.VersionId);
            }

            Assert.IsNotNull(readOrganization);
            Assert.IsInstanceOf<Organization>(readOrganization);

            var actualOrganization = (Organization)readOrganization;

            Assert.IsNotNull(actualOrganization.Address);
            Assert.AreEqual("Hamilton Health Sciences", actualOrganization.Name);
            Assert.IsTrue(actualOrganization.Alias.All(c => c == "hhs"));
            Assert.IsTrue(actualOrganization.Address.Count == 2);
            Assert.IsTrue(actualOrganization.Identifier.First().Value == "6324");
            Assert.AreEqual("http://santedb.org/fhir/test", actualOrganization.Identifier.First().System);
            Assert.AreEqual(1, actualOrganization.Identifier.Count);
        }

        /// <summary>
        /// Tests the delete functionality in <see cref="OrganizationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeleteOrganization()
        {
            // set up the test data
            var organization = TestUtil.GetFhirMessage("Organization") as Organization;

            Resource result;
            // create the organization
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Create(organization, TransactionMode.Commit);
                result = organizationResourceHandler.Read(result.Id, result.VersionId);
            }

            // assert create organization successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            var readOrganization = (Organization)result;

            // ensure the organization is active
            Assert.IsTrue(readOrganization.Active);
            Assert.AreEqual("Hamilton Health Sciences", readOrganization.Name);
            Assert.IsTrue(readOrganization.Alias.All(c => c == "hhs"));
            Assert.IsTrue(readOrganization.Address.Count == 2);

            // delete the organization
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Delete(result.Id, TransactionMode.Commit);

                try
                {
                    organizationResourceHandler.Read(result.Id, null);
                    Assert.Fail("Should have thrown 410 gone");
                }
                catch (FhirException e) when (e.Status == System.Net.HttpStatusCode.Gone) { }
                catch
                {
                    Assert.Fail("Threw wrong exception");
                }
            }
        }

        /// <summary>
        /// Tests the update functionality in <see cref="OrganizationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdateOrganization()
        {
            // set up the test data
            var organization = TestUtil.GetFhirMessage("Organization") as Organization;

            Resource result;
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Create(organization, TransactionMode.Commit);
                result = organizationResourceHandler.Read(result.Id, result.VersionId);
            }

            // assert create organization successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            var actual = (Organization)result;
            Assert.AreEqual("Hamilton Health Sciences", actual.Name);
            Assert.IsTrue(actual.Alias.All(c => c == "hhs"));
            Assert.IsTrue(actual.Address.Count == 2);

            // update the organization
            organization.Name = "Hamilton Health Science";
            organization.Address.RemoveAt(1);
            organization.Identifier.First().Value = "2021";

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Update(result.Id, organization, TransactionMode.Commit);
                result = organizationResourceHandler.Read(result.Id, result.VersionId);
            }

            // assert update organization successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            actual = (Organization)result;
            Assert.AreEqual("Hamilton Health Science", actual.Name);
            Assert.IsTrue(actual.Address.Count == 1);
            Assert.IsFalse(actual.Extension.Any());
            Assert.AreEqual("2021", actual.Identifier.First().Value);
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="OrganizationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
            var methodInfo = typeof(OrganizationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(methodInfo);

            var interactions = methodInfo.Invoke(organizationResourceHandler, null);

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
    }
}
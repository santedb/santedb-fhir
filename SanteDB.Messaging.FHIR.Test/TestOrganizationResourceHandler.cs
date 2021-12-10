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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using static Hl7.Fhir.Model.ContactPoint;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="OrganizationResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestOrganizationResourceHandler : DataTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = {0x01, 0x02, 0x03, 0x04, 0x05};

        /// <summary>
        /// The service manager.
        /// </summary>
        private IServiceManager m_serviceManager;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestOrganizationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Organization"
                },
                OperationHandlers = new List<TypeReferenceConfiguration>(),
                ExtensionHandlers = new List<TypeReferenceConfiguration>(),
                ProfileHandlers = new List<TypeReferenceConfiguration>(),
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(OrganizationResourceHandler))
                }
            };
            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, this.m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

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
                Assert.Throws<InvalidDataException>(() => organizationResourceHandler.Create(new Practitioner(), TransactionMode.Commit));
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
            var actual = (Organization) result;
            Assert.AreEqual("Hamilton Health Sciences", actual.Name);
            Assert.IsTrue(actual.Alias.All(c => c == "hhs"));
            Assert.IsTrue(actual.Address.Count == 2);
            Assert.IsTrue(actual.Extension.Any(e => e.Url == "http://santedb.org/extensions/core/detectedIssue"));
            Assert.IsTrue(actual.Identifier.First().Value == "6324");
            Assert.AreEqual("http://santedb.org/fhir/test", actual.Identifier.First().System);
            Assert.IsTrue(actual.Identifier.Count == 1);
        }

        [Test]
        public void TestCreateOrganizationWithParent()
        {
            var parentOrganization = new Organization
            {
                Name = "McMaster University Medical School",
                Address = new List<Address>
                {
                    new Address
                    {
                        City = "Hamilton",
                        Text = "1280 Main Street West",
                        PostalCode = "L8S4L8 "
                    }
                },
                Telecom = new List<ContactPoint>
                {
                    new ContactPoint(ContactPointSystem.Phone, ContactPointUse.Work, "905 525 9140")
                }
            };

            Resource createdParentOrganization;
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                createdParentOrganization = organizationResourceHandler.Create(parentOrganization, TransactionMode.Commit);
            }

            var subOrganization = new Organization
            {
                Name = "Hamilton Health Sciences",
                Address = new List<Address>
                {
                    new Address
                    {
                        Text = "237 Barton St E",
                        City = "Hamilton",
                        PostalCode = "L8L 2X2"
                    }
                },

                Telecom = new List<ContactPoint>
                {
                    new ContactPoint(ContactPointSystem.Phone, ContactPointUse.Work, "905 512 2100")
                }
            };

            subOrganization.PartOf = new ResourceReference($"urn:uuid:{createdParentOrganization.Id}");
            Resource subResult;
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                subResult = organizationResourceHandler.Create(subOrganization, TransactionMode.Commit);
                subResult = organizationResourceHandler.Read(subResult.Id, null);
            }

            // assert create organization successfully
            Assert.NotNull(subResult);
            Assert.IsInstanceOf<Organization>(subResult);
            var actualSubOrganization = (Organization) subResult;
            Assert.AreEqual("McMaster University Medical School", actualSubOrganization.Name);
            Assert.IsTrue(actualSubOrganization.Alias.Any(c => c == "hhs"));
            Assert.IsTrue(actualSubOrganization.Address.Count == 1);
            Assert.IsNotNull(actualSubOrganization.PartOf);
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
            var readOrganization = (Organization) result;

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
            }

            // assert deletion organizaiton successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            readOrganization = (Organization) result;

            // ensure the organization is NOT active
            Assert.IsFalse(readOrganization.Active);

            // ensure the deleted organization is the created one
            Assert.IsTrue(readOrganization.Name == "Hamilton Health Sciences");
            Assert.AreEqual(2, readOrganization.Address.Count());
            Assert.AreEqual("hhs", readOrganization.Alias.First());
            Assert.IsTrue(readOrganization.Identifier.First().Value == "6324");
            Assert.AreEqual("http://santedb.org/fhir/test", readOrganization.Identifier.First().System);
            Assert.IsTrue(readOrganization.Identifier.Count == 1);

            // read the organization
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Read(result.Id, readOrganization.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            readOrganization = (Organization) result;
            // ensure the organization is NOT active
            Assert.IsFalse(readOrganization.Active);
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
            var actual = (Organization) result;
            Assert.AreEqual("Hamilton Health Sciences", actual.Name);
            Assert.IsTrue(actual.Alias.All(c => c == "hhs"));
            Assert.IsTrue(actual.Address.Count == 2);

            // update the organization
            organization.Name = "Hamilton Health Science";
            organization.Address.RemoveAt(1);
            organization.Identifier.First().Value = "2021";
            organization.Extension.RemoveAt(0);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Update(result.Id, organization, TransactionMode.Commit);
                result = organizationResourceHandler.Read(result.Id, result.VersionId);
            }

            // assert update organization successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            actual = (Organization) result;
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
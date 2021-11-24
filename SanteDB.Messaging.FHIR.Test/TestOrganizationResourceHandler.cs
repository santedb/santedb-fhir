using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
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
using System.IO;
using System.Linq;
using static Hl7.Fhir.Model.ContactPoint;
using Organization = Hl7.Fhir.Model.Organization;
using Patient = SanteDB.Core.Model.Roles.Patient;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Tests the <see cref="OrganizationResourceHandler"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestOrganizationResourceHandler : DataTest
    {
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        private IRepositoryService<Patient> m_patientRepository;

        private IRepositoryService<Person> m_personRepository;

        private IRepositoryService<EntityRelationship> m_relationshipRepository;

        // Bundler 
        private IServiceManager m_serviceManager;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestOrganizationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            this.m_patientRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Patient>>();
            this.m_personRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<Person>>();
            this.m_relationshipRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<EntityRelationship>>();
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
            Assert.IsTrue(actual.Alias.All(c=>c=="hhs"));
            Assert.True(actual.Address.Count == 2);
            Assert.IsTrue(actual.Extension.Any(e => e.Url == "http://santedb.org/extensions/core/detectedIssue"));
            Assert.IsTrue(actual.Identifier.First().Value == "6324");
            Assert.AreEqual("http://santedb.org/fhir/test", actual.Identifier.First().System);
            Assert.IsTrue(actual.Identifier.Count == 1);
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
            Assert.True(actual.Address.Count == 2);

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
            actual = (Organization)result;
            Assert.AreEqual("Hamilton Health Science", actual.Name);
            Assert.IsTrue(actual.Address.Count == 1);
            Assert.IsFalse(actual.Extension.Any());
            Assert.AreEqual("2021", actual.Identifier.First().Value);
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
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
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
            Assert.True(readOrganization.Address.Count == 2);

            // delete the organization
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Delete(result.Id, TransactionMode.Commit);
            }

            // assert deletion organizaiton successfully
            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            readOrganization = (Organization)result;

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
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", AUTH))
            {
                var organizationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Organization);
                result = organizationResourceHandler.Read(result.Id, readOrganization.VersionId);
            }

            Assert.NotNull(result);
            Assert.IsInstanceOf<Organization>(result);
            readOrganization = (Organization)result;
            // ensure the organization is NOT active
            Assert.IsFalse(readOrganization.Active);
        }
    }
}

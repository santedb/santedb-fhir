using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Handlers;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="FhirResourceHandlerUtil"/> class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class TestFhirResourceHandlerUtil
    {
        /// <summary>
        /// Runs setup before each test execution.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FirebirdSql.Data.FirebirdClient.FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestFhirResourceHandlerUtil).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            TestApplicationContext.Current.Start();
        }

        /// <summary>
        /// Tests the registration of a FHIR resource handler in the <see cref="FhirResourceHandlerUtil"/> class.
        /// </summary>
        [Test]
        public void TestRegisterResourceHandler()
        {
            FhirResourceHandlerUtil.RegisterResourceHandler(new DummyResourceHandler());

            Assert.NotNull(FhirResourceHandlerUtil.GetResourceHandler(ResourceType.DomainResource));
            Assert.IsInstanceOf<DummyResourceHandler>(FhirResourceHandlerUtil.GetResourceHandler(ResourceType.DomainResource));
        }

        /// <summary>
        /// Test the un-registration of a FHIR resource handler in the <see cref="FhirResourceHandlerUtil"/> class.
        /// </summary>
        [Test]
        public void TestUnRegisterResourceHandler()
        {
            FhirResourceHandlerUtil.RegisterResourceHandler(new DummyResourceHandler());

            Assert.NotNull(FhirResourceHandlerUtil.GetResourceHandler(ResourceType.DomainResource));
            Assert.IsInstanceOf<DummyResourceHandler>(FhirResourceHandlerUtil.GetResourceHandler(ResourceType.DomainResource));

            FhirResourceHandlerUtil.UnRegisterResourceHandler(new DummyResourceHandler());

            Assert.Throws<NotSupportedException>(() => FhirResourceHandlerUtil.GetResourceHandler(ResourceType.DomainResource));
        }
    }

    /// <summary>
    /// Represents a dummy FHIR resource handler.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DummyResourceHandler : IFhirResourceHandler
    {
        /// <summary>
        /// Gets the type of resource this handler can perform operations on
        /// </summary>
        public ResourceType ResourceType => ResourceType.DomainResource;

        /// <summary>
        /// Read a specific version of a resource
        /// </summary>
        public Resource Read(string id, string versionId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update a resource
        /// </summary>
        public Resource Update(string id, Resource target, TransactionMode mode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delete a resource
        /// </summary>
        public Resource Delete(string id, TransactionMode mode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a resource
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Query a FHIR resource
        /// </summary>
        public Bundle Query(NameValueCollection parameters)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the history of a specific FHIR object
        /// </summary>
        public Bundle History(string id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the definition for this resource
        /// </summary>
        public CapabilityStatement.ResourceComponent GetResourceDefinition()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the structure definition for this profile
        /// </summary>
        public StructureDefinition GetStructureDefinition()
        {
            throw new NotImplementedException();
        }
    }

}

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
 * User: Nityan Khanna
 * Date: 2021-11-27
 */

using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestFhirResourceHandlerUtil).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
        }

        /// <summary>
        /// Tests the retrieval of a resource handler in the <see cref="FhirResourceHandlerUtil"/> class.
        /// </summary>
        [Test]
        public void TestGetResourceHandlerInvalidResource()
        {
            Assert.Throws<KeyNotFoundException>(() => FhirResourceHandlerUtil.GetResourceHandler("Address"));
        }

        /// <summary>
        /// Tests the retrieval of a resource handler in the <see cref="FhirResourceHandlerUtil"/> class.
        /// </summary>
        [Test]
        public void TestGetResourceHandlerNull()
        {
            Assert.Throws<ArgumentNullException>(() => FhirResourceHandlerUtil.GetResourceHandler(null));
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
            Assert.IsTrue(FhirResourceHandlerUtil.ResourceHandlers.Any(c => c.GetType() == typeof(DummyResourceHandler)));
        }

        /// <summary>
        /// Tests the registration of a FHIR resource handler in the <see cref="FhirResourceHandlerUtil"/> class.
        /// </summary>
        [Test]
        public void TestRegisterResourceHandlerNull()
        {
            Assert.Throws<ArgumentNullException>(() => FhirResourceHandlerUtil.RegisterResourceHandler(null));
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

            Assert.IsFalse(FhirResourceHandlerUtil.ResourceHandlers.Any(c => c.GetType() == typeof(DummyResourceHandler)));
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
        /// Create a resource
        /// </summary>
        public Resource Create(Resource target, TransactionMode mode)
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

        /// <summary>
        /// Get the history of a specific FHIR object
        /// </summary>
        public Bundle History(string id)
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
    }
}
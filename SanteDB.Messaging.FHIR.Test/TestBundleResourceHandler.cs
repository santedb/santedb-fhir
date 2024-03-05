﻿/*
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
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="BundleResourceHandler"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestBundleResourceHandler
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        /// <summary>
        /// Tests the delete functionality of the <see cref="BundleResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDelete()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                Assert.Throws<NotSupportedException>(() => bundleResourceHandler.Delete(Guid.NewGuid().ToString(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the history functionality of the <see cref="BundleResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestHistory()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                Assert.Throws<NotSupportedException>(() => bundleResourceHandler.History(Guid.NewGuid().ToString()));
            }
        }

        /// <summary>
        /// Tests the read functionality of the <see cref="BundleResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestRead()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                Assert.Throws<NotSupportedException>(() => bundleResourceHandler.Read(Guid.NewGuid().ToString(), null));
            }
        }

        /// <summary>
        /// Tests the query functionality of the <see cref="BundleResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestQuery()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                Assert.Throws<NotSupportedException>(() => bundleResourceHandler.Query(new NameValueCollection()));
            }
        }

        /// <summary>
        /// Tests the update functionality of the <see cref="BundleResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdate()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var bundleResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Bundle);

                Assert.Throws<NotSupportedException>(() => bundleResourceHandler.Update(Guid.NewGuid().ToString(), new Bundle(), TransactionMode.Commit));
            }
        }
    }
}
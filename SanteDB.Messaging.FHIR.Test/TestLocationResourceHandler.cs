﻿/*
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
 * User: Jordan Webber
 * Date: 2021-12-01
 */

using FirebirdSql.Data.FirebirdClient;
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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="LocationResourceHandler"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    class TestLocationResourceHandler
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
        /// Setup method for unit tests.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestLocationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Location"
                },
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(LocationResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        [Test]
        public void TestCreateLocation()
        {
            var partOfLocation = TestUtil.GetFhirMessage("CreatePartOfLocation") as Location;

            var location = TestUtil.GetFhirMessage("CreateLocation") as Location;

            Resource actual;
            Resource partOfLocationActual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                partOfLocationActual = locationResourceHandler.Create(partOfLocation, TransactionMode.Commit);
                location.PartOf = new ResourceReference($"urn:uuid:{partOfLocationActual.Id}");
                actual = locationResourceHandler.Create(location, TransactionMode.Commit);
            }

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<Location>(actual);

            var createdLocation = (Location)actual;

            Assert.IsNotNull(createdLocation);

            Resource readLocation;

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                readLocation = locationResourceHandler.Read(createdLocation.Id, createdLocation.VersionId);
            }

            Assert.IsNotNull(readLocation);
            Assert.IsInstanceOf<Location>(readLocation);

            var retrievedLocation = (Location)readLocation;

            Assert.NotNull(retrievedLocation.Alias.First());
            Assert.AreEqual("Test Location", retrievedLocation.Name);
            Assert.AreEqual(Location.LocationMode.Kind, retrievedLocation.Mode);
            Assert.AreEqual(Location.LocationStatus.Active, retrievedLocation.Status);
            Assert.AreEqual("Hamilton", retrievedLocation.Address.City);
            Assert.AreEqual("6324", retrievedLocation.Identifier.First().Value);
            Assert.IsNotNull(retrievedLocation.Position.Latitude);
        }

        [Test]
        public void TestCreateLocationInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                Assert.Throws<InvalidDataException>(() => locationResourceHandler.Create(new Practitioner(), TransactionMode.Commit));
            }
        }
    }
}
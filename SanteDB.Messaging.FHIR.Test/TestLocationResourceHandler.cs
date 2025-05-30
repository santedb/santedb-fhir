/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="LocationResourceHandler"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    class TestLocationResourceHandler : FhirTest
    {
        /// <summary>
        /// The authentication key.
        /// </summary>
        private readonly byte[] AUTH = { 0x01, 0x02, 0x03, 0x04, 0x05 };



        /// <summary>
        /// Tests the create functionality for the <see cref="LocationResourceHandler"/> class.
        /// </summary>
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

        /// <summary>
        /// Tests the creation of an inactive location in the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateSuspendedLocation()
        {
            var location = TestUtil.GetFhirMessage("CreateLocation") as Location;
            location.Status = Location.LocationStatus.Suspended;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                Assert.Throws<NotSupportedException>(() => locationResourceHandler.Create(location, TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the create functionality with an invalid resource for the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestCreateLocationInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                Assert.Throws<ArgumentException>(() => locationResourceHandler.Create(new Practitioner(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the delete functionality of the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeleteLocation()
        {
            var location = TestUtil.GetFhirMessage("CreateLocation") as Location;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                var actual = locationResourceHandler.Create(location, TransactionMode.Commit);

                Assert.IsNotNull(actual);
                Assert.IsInstanceOf<Location>(actual);

                var createdLocation = (Location)actual;

                Assert.IsNotNull(createdLocation);

                var actualDeleted = locationResourceHandler.Delete(createdLocation.Id, TransactionMode.Commit);

                Assert.IsNotNull(actualDeleted);
                Assert.IsInstanceOf<Location>(actualDeleted);

                var deletedLocation = (Location)actualDeleted;

                Assert.IsNotNull(deletedLocation);

                // Try to fetch
                try
                {
                    locationResourceHandler.Read(deletedLocation.Id, null);
                    Assert.Fail("Should throw exception");
                }
                catch (FhirException e) when (e.Status == System.Net.HttpStatusCode.Gone)
                {

                }
                catch
                {
                    Assert.Fail("Threw wrong type of exception");
                }
            }
        }

        /// <summary>
        /// Tests the delete functionality when a invalid guid is given in the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestDeleteLocationInvalidGuid()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                Assert.Throws<KeyNotFoundException>(() => locationResourceHandler.Delete(Guid.NewGuid().ToString(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the update functionality in the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdateLocation()
        {
            var location = TestUtil.GetFhirMessage("UpdateLocation") as Location;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                var actual = locationResourceHandler.Create(location, TransactionMode.Commit);

                Assert.IsNotNull(actual);
                Assert.IsInstanceOf<Location>(actual);

                var createdLocation = (Location)actual;

                Assert.IsNotNull(createdLocation);
                Assert.AreEqual(Location.LocationStatus.Active, createdLocation.Status);
                Assert.AreEqual(Location.LocationMode.Instance, createdLocation.Mode);
                Assert.AreEqual("Ontario", createdLocation.Address.State);

                createdLocation.Status = Location.LocationStatus.Inactive;
                createdLocation.Mode = Location.LocationMode.Kind;
                createdLocation.Address.State = "Bluenose";

                var actualUpdated = locationResourceHandler.Update(createdLocation.Id, createdLocation, TransactionMode.Commit);

                Assert.IsNotNull(actualUpdated);
                Assert.IsInstanceOf<Location>(actualUpdated);

                var updatedLocation = (Location)actualUpdated;

                Assert.IsNotNull(updatedLocation);
                Assert.AreEqual(Location.LocationStatus.Inactive, updatedLocation.Status);
                Assert.AreEqual(Location.LocationMode.Kind, updatedLocation.Mode);
                Assert.AreEqual("Bluenose", updatedLocation.Address.State);
            }
        }

        /// <summary>
        /// Tests the get interactions functionality in <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestGetInteractions()
        {
            var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);
            var methodInfo = typeof(LocationResourceHandler).GetMethod("GetInteractions", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(methodInfo);

            var interactions = methodInfo.Invoke(locationResourceHandler, null);

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

        /// Tests the update functionality when an invalid resource is passed to the update method in the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestUpdateLocationInvalidResource()
        {
            var location = TestUtil.GetFhirMessage("UpdateLocation") as Location;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                var actual = locationResourceHandler.Create(location, TransactionMode.Commit);

                Assert.IsNotNull(actual);
                Assert.IsInstanceOf<Location>(actual);

                var createdLocation = (Location)actual;

                Assert.IsNotNull(createdLocation);
                Assert.AreEqual(Location.LocationStatus.Active, createdLocation.Status);
                Assert.AreEqual(Location.LocationMode.Instance, createdLocation.Mode);
                Assert.AreEqual("Ontario", createdLocation.Address.State);

                createdLocation.Status = Location.LocationStatus.Suspended;
                createdLocation.Mode = Location.LocationMode.Kind;
                createdLocation.Address.State = "Bluenose";

                Assert.Throws<ArgumentException>(() => locationResourceHandler.Update(createdLocation.Id, new Patient(), TransactionMode.Commit));
            }
        }

        /// <summary>
        /// Tests the query functionality in the <see cref="LocationResourceHandler"/> class.
        /// </summary>
        [Test]
        public void TestQueryLocation()
        {
            var location = TestUtil.GetFhirMessage("UpdateLocation") as Location;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);
            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var locationResourceHandler = FhirResourceHandlerUtil.GetResourceHandler(ResourceType.Location);

                var actual = locationResourceHandler.Create(location, TransactionMode.Commit);

                Assert.IsNotNull(actual);
                Assert.IsInstanceOf<Location>(actual);

                var createdLocation = (Location)actual;

                Assert.IsNotNull(createdLocation);

                Assert.AreEqual(Location.LocationStatus.Active, createdLocation.Status);
                Assert.AreEqual(Location.LocationMode.Instance, createdLocation.Mode);
                Assert.AreEqual("Ontario", createdLocation.Address.State);

                var actualQuery = locationResourceHandler.Query(new NameValueCollection
                {
                    { "_id", createdLocation.Id }
                });

                Assert.IsNotNull(actualQuery);
                Assert.IsInstanceOf<Bundle>(actualQuery);

                Assert.AreEqual(1, actualQuery.Entry.Count);

                var queriedLocation = (Location)actualQuery.Entry.First().Resource;

                Assert.IsNotNull(queriedLocation);
                Assert.IsInstanceOf<Location>(queriedLocation);

                Assert.AreEqual(Location.LocationStatus.Active, queriedLocation.Status);
                Assert.AreEqual(Location.LocationMode.Instance, queriedLocation.Mode);
                Assert.AreEqual("Ontario", queriedLocation.Address.State);
            }
        }
    }
}

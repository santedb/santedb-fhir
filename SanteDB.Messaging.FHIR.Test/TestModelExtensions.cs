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
using SanteDB.Core.Interop.Description;
using SanteDB.Messaging.FHIR.Util;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="ModelExtensions"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestModelExtensions
    {
        /// <summary>
        /// Tests the create description functionality in the <see cref="ModelExtensions"/> class.
        /// </summary>
        [Test]
        public void TestCreateDescriptionAllFhirTypes()
        {
            var expected = new ResourceDescription("Patient", "FHIR Resource Patient");
            var actual = FHIRAllTypes.Patient.CreateDescription();

            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Description, actual.Description);
        }

        /// <summary>
        /// Tests the create description functionality in the <see cref="ModelExtensions"/> class.
        /// </summary>
        [Test]
        public void TestCreateDescriptionResourceType()
        {
            var expected = new ResourceDescription("Patient", "FHIR Resource Patient");
            var actual = ResourceType.Patient.CreateDescription();

            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Description, actual.Description);
        }

        /// <summary>
        /// Tests the get resource type functionality in the <see cref="ModelExtensions"/> class.
        /// </summary>
        [Test]
        public void TestGetResourceType()
        {
            var actual = typeof(Hl7.Fhir.Model.Patient).GetResourceType();

            Assert.AreEqual(ResourceType.Patient, actual);
        }

        /// <summary>
        /// Tests the get resource type functionality in the <see cref="ModelExtensions"/> class.
        /// </summary>
        [Test]
        public void TestGetResourceTypeNonResource()
        {
            var actual = typeof(Hl7.Fhir.Model.Address).GetResourceType();

            Assert.IsNull(actual);
        }
    }
}

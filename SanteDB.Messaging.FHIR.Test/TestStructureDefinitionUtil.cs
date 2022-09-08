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
using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="StructureDefinitionUtil"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestStructureDefinitionUtil
    {
        /// <summary>
        /// Runs setup before each test execution.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            TestApplicationContext.TestAssembly = typeof(TestFhirResourceHandlerUtil).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
        }

        /// <summary>
        /// Tests the retrieval generation of a <see cref="StructureDefinition"/> instance.
        /// </summary>
        [Test]
        public void TestGetStructureDefinition()
        {
            var actual = typeof(Patient).GetStructureDefinition();

            Assert.AreEqual("Patient", actual.Id);
            Assert.AreEqual(FHIRVersion.N4_0_0, actual.FhirVersion);
            Assert.AreEqual(PublicationStatus.Active, actual.Status);
        }

        /// <summary>
        /// Tests the retrieval generation of a <see cref="StructureDefinition"/> instance.
        /// </summary>
        [Test]
        public void TestGetStructureDefinitionNull()
        {
            Assert.Throws<ArgumentNullException>(() => StructureDefinitionUtil.GetStructureDefinition(null));
        }
    }
}
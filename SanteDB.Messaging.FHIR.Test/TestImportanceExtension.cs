/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Roles;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Extensions.Patient;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Patient = SanteDB.Core.Model.Roles.Patient;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="ImportanceExtension"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestImportanceExtension : FhirTest
    {
        /// <summary>
        /// Runs setup before each test execution.
        /// </summary>
        [SetUp]
        public void DoSetup()
        {
            this.m_extension = this.m_serviceManager.CreateInjected<ImportanceExtension>();
        }

        /// <summary>
        /// The extension under test.
        /// </summary>
        private IFhirExtensionHandler m_extension;

        /// <summary>
        /// Tests the construct functionality in <see cref="ImportanceExtension" /> class.
        /// With valid role and VipStatus.
        /// </summary>
        [Test]
        public void TestImportanceExtensionConstructValidRoleVipStatus()
        {
            var patient = new Patient
            {
                VipStatus = new Concept
                {
                    Mnemonic = "VIPStatus-ForeignDignitary",
                    Key = VipStatusKeys.ForeignDignitary
                }
            };

            var constructedVipStatus = this.m_extension.Construct(patient).ToArray();
            Assert.AreEqual(1, constructedVipStatus.Length);
            Assert.IsInstanceOf<CodeableConcept>(constructedVipStatus.First().Value);
            var codeableConcept = (CodeableConcept)constructedVipStatus.First().Value;
            Assert.AreEqual("FOR", codeableConcept.Coding.First().Code);
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="ImportanceExtension" /> class.
        /// With invalid role.
        /// </summary>
        [Test]
        public void TestImportanceExtensionConstructInvalidRole()
        {
            var provider = new Provider();
            var constructedVipStatus = this.m_extension.Construct(provider).ToArray();

            Assert.IsEmpty(constructedVipStatus);
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="ImportanceExtension" /> class.
        /// With valid role but no vip status.
        /// </summary>
        [Test]
        public void TestImportanceExtensionConstructValidRoleNoVipStatus()
        {
            var patient = new Patient();
            var constructedVipStatus = this.m_extension.Construct(patient).ToArray();

            Assert.IsEmpty(constructedVipStatus);
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="ImportanceExtension" /> class.
        /// With valid role and extension.
        /// </summary>
        /// 
        [Test]
        public void TestImportanceExtensionParseValidRoleExtension()
        {
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-importance",
                new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-PatientImportance", "BM"));
            var patient = new Patient();

            this.m_extension.Parse(extensionforTest, patient);

            Assert.IsNotNull(patient.VipStatus);
            Assert.AreEqual("VIPStatus-BoardMember", patient.VipStatus.Mnemonic);
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="ImportanceExtension" /> class.
        /// With valid role and invalid extension value code.
        /// </summary>
        [Test]
        public void TestImportanceExtensionParseValidRoleInvalidExtension()
        {
            var extensionUnderTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-religion", new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-PatientImportance", "bm"));
            var patient = new Patient();

            Assert.Throws<FhirException>(() => this.m_extension.Parse(extensionUnderTest, patient));
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="ImportanceExtension" /> class.
        /// With invalid extension value.
        /// </summary>
        [Test]
        public void TestImportanceExtensionParseInvalidExtension()
        {
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-religion", new FhirString("Test"));
            var patient = new Patient();

            this.m_extension.Parse(extensionforTest, patient);
            Assert.IsNull(patient.VipStatus);
        }
    }
}
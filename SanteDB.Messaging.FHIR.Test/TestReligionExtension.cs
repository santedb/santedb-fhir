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
 * User: Shihab Khan
 * Date: 2021-11-30
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Extensions.Patient;
using Patient = SanteDB.Core.Model.Roles.Patient;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="ReligionExtension"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestReligionExtension
    {
        /// <summary>
        /// The service manager.
        /// </summary>
        private IServiceManager m_serviceManager;
        
        /// <summary>
        /// The extension under test.
        /// </summary>
        private IFhirExtensionHandler m_extension;

        [SetUp]
        public void Setup()
        {
            // Force load of the DLL
            var p = FbCharset.Ascii;
            TestApplicationContext.TestAssembly = typeof(TestRelatedPersonResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);

            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            this.m_extension = this.m_serviceManager.CreateInjected<ReligionExtension>();
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="ReligionExtension" /> class.
        /// With valid role and religion.
        /// </summary>
        [Test]
        public void TestReligionExtensionConstructValidRoleReligion()
        {
            var patient = new Patient
            {
                ReligiousAffiliation = new Concept() { Mnemonic = "Talos", Key = Guid.NewGuid()}
            };

            var constructedReligion = this.m_extension.Construct(patient).ToArray();
            Assert.AreEqual(1,constructedReligion.Length);
            Assert.IsInstanceOf<CodeableConcept>(constructedReligion.First().Value);
            var codeableConcept = (CodeableConcept)constructedReligion.First().Value;
            Assert.AreEqual("Talos", codeableConcept.Coding.First().Code);

        }

        /// <summary>
        /// Tests the construct functionality in <see cref="ReligionExtension" /> class.
        /// With invalid role.
        /// </summary>
        [Test]
        public void TestReligionExtensionConstructInvalidRole()
        {
            var provider = new Provider();
            var constructedReligion = this.m_extension.Construct(provider).ToArray();

            Assert.IsEmpty(constructedReligion);
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="ReligionExtension" /> class.
        /// With valid role but no religion.
        /// </summary>
        [Test]
        public void TestReligionExtensionConstructValidRoleNoReligion()
        {
            var patient = new Patient();
            var constructedReligion = this.m_extension.Construct(patient).ToArray();

            Assert.IsEmpty(constructedReligion);
        }


        /// <summary>
        /// Tests the parse functionality in <see cref="ReligionExtension" /> class.
        /// With valid role and extension.
        /// </summary>
        [Test]
        public void TestReligionExtensionParseValidRoleExtension()
        {
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-religion", 
                new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-2.4", "ATH"));
            var patient = new Patient();

            this.m_extension.Parse(extensionforTest, patient);
            
            Assert.IsNotNull(patient.ReligiousAffiliation);
            Assert.AreEqual("RELIGION-Atheist", patient.ReligiousAffiliation.Mnemonic);

        }

        /// <summary>
        /// Tests the parse functionality in <see cref="ReligionExtension" /> class.
        /// With valid role and invalid extension value code.
        /// </summary>
        [Test]
        public void TestReligionExtensionParseValidRoleInvalidExtension()
        {
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-religion", 
                new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-2.4", "ASDF"));
            var patient = new Patient();

            Assert.Throws<FhirException>(()=>this.m_extension.Parse(extensionforTest, patient));
        }
  
        /// <summary>
        /// Tests the parse functionality in <see cref="ReligionExtension" /> class.
        /// With invalid extension value.
        /// </summary>
        [Test]
        public void TestReligionExtensionParseInvalidExtension()
        {
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-religion", new FhirString("Test"));
            var patient = new Patient();

            this.m_extension.Parse(extensionforTest, patient);

            Assert.IsNull(patient.ReligiousAffiliation);
        }
    }
}

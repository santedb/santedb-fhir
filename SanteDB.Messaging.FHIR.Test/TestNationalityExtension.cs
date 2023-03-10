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
 * Date: 2023-3-10
 */
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Extensions.Patient;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Patient = SanteDB.Core.Model.Roles.Patient;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="NationalityExtension"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestNationalityExtension : FhirTest
    {

        /// <summary>
        /// The extension under test.
        /// </summary>
        private IFhirExtensionHandler m_extension;

        [SetUp]
        public void DoSetup()
        {
            this.m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();
            this.m_extension = this.m_serviceManager.CreateInjected<NationalityExtension>();
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="NationalityExtension" /> class.
        /// With valid role and nationality.
        /// </summary>
        [Test]
        public void TestNationalityExtensionConstructValidRoleNationality()
        {
            var patient = new Patient
            {
                NationalityKey = NationalityKeys.Canada
            };

            var constructedNationality = this.m_extension.Construct(patient).ToArray();
            Assert.AreEqual(1, constructedNationality.Length);
            Assert.IsInstanceOf<CodeableConcept>(constructedNationality.First().Value);
            var codeableConcept = (CodeableConcept)constructedNationality.First().Value;
            Assert.AreEqual("CA", codeableConcept.Coding.First().Code);

        }

        /// <summary>
        /// Tests the construct functionality in <see cref="NationalityExtension" /> class.
        /// With invalid role.
        /// </summary>
        [Test]
        public void TestNationalityExtensionConstructInvalidRole()
        {
            var provider = new Provider();
            var constructedNationality = this.m_extension.Construct(provider).ToArray();

            Assert.IsEmpty(constructedNationality);
        }

        /// <summary>
        /// Tests the construct functionality in <see cref="NationalityExtension" /> class.
        /// With valid role but no nationality.
        /// </summary>
        [Test]
        public void TestNationalityExtensionConstructValidRoleNoNationality()
        {
            var patient = new Patient();
            var constructedNationality = this.m_extension.Construct(patient).ToArray();

            Assert.IsEmpty(constructedNationality);
        }


        /// <summary>
        /// Tests the parse functionality in <see cref="NationalityExtension" /> class.
        /// With valid role and extension.
        /// </summary>
        //[Test]
        public void TestNationalityExtensionParseValidRoleExtension()
        {
            //due to lack of nationality data, using family member data here for test
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-nationality",
                new CodeableConcept("http://santedb.org/conceptset/v3-FamilyMember", "BRO"));
            var patient = new Patient();

            this.m_extension.Parse(extensionforTest, patient);

            Assert.IsNotNull(patient.Nationality);
            Assert.AreEqual("Brother", patient.Nationality.Mnemonic);

        }

        /// <summary>
        /// Tests the parse functionality in <see cref="NationalityExtension" /> class.
        /// With valid role and invalid extension value code
        /// </summary>
        [Test]
        public void TestNationalityExtensionParseValidRoleInvalidExtension()
        {
            //due to lack of nationality data, using family member data here for test
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-nationality",
                new CodeableConcept("http://santedb.org/conceptset/v3-FamilyMember", "DFSFSDF"));
            var patient = new Patient();

            Assert.Throws<FhirException>(() => this.m_extension.Parse(extensionforTest, patient));
        }

        /// <summary>
        /// Tests the parse functionality in <see cref="NationalityExtension" /> class.
        /// With invalid extension value
        /// </summary>
        [Test]
        public void TestNationalityExtensionParseInvalidExtensionValue()
        {
            var extensionforTest = new Extension("http://hl7.org/fhir/StructureDefinition/patient-nationality", new FhirString("Test"));
            var patient = new Patient();

            this.m_extension.Parse(extensionforTest, patient);

            Assert.IsNull(patient.Nationality);
        }
    }
}

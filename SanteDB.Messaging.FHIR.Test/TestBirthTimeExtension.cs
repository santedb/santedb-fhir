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
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Messaging.FHIR.Extensions.Patient;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="BirthTimeExtension"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    class TestBirthTimeExtension
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
            this.m_extension = this.m_serviceManager.CreateInjected<BirthTimeExtension>();
        }

        [Test]
        public void TestBirthTimeConstructValidDate()
        {
            var person = new Person
            {
                DateOfBirth = new DateTime(1970, 4, 5),
            };

            var constructedBirthTime = m_extension.Construct(person).ToArray();

            Assert.IsTrue(constructedBirthTime.Any());
            Assert.AreEqual(1, constructedBirthTime.Length);

            Console.WriteLine(constructedBirthTime.First().Value);

            var extension = constructedBirthTime.First();

            Assert.IsNotNull(extension);
            Assert.IsInstanceOf<FhirDateTime>(extension.Value);

            var birthDate = extension.Value as FhirDateTime;

            Assert.IsNotNull(birthDate);
            Assert.AreEqual(new FhirDateTime(1970, 4, 5), birthDate);
        }
    }
}
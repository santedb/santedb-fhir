/*
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
using NUnit.Framework;
using SanteDB.Messaging.FHIR.Util;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.FHIR.Test
{
    /// <summary>
    /// Contains tests for the <see cref="MessageUtil"/> class.
    /// </summary>
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestMessageUtil
    {
        /// <summary>
        /// The escaped data instance under test.
        /// </summary>
        private readonly string escapedData = @"this\#002C is a test \#0024 hello \#007C test\#005C";

        /// <summary>
        /// The unescaped data instance under test.
        /// </summary>
        private readonly string unescapedData = @"this\, is a test \$ hello \| test\\";

        /// <summary>
        /// Tests the escape functionality.
        /// </summary>
        [Test]
        public void TestEscape()
        {
            var actual = MessageUtil.Escape(this.unescapedData);

            Assert.AreEqual(this.escapedData, actual);
        }

        /// <summary>
        /// Tests the unescape functionality.
        /// </summary>
        [Test]
        public void TestUnEscape()
        {
            var actual = MessageUtil.UnEscape(this.escapedData);

            Assert.AreEqual(this.unescapedData, actual);
        }
    }
}

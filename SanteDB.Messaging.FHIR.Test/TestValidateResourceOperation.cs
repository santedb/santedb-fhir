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
using Hl7.Fhir.Model;
using NUnit.Framework;
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.TestFramework;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Operations;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DetectedIssue = SanteDB.Core.BusinessRules.DetectedIssue;

namespace SanteDB.Messaging.FHIR.Test
{
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestValidateResourceOperation
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
        /// Runs setup before each test execution.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            TestApplicationContext.TestAssembly = typeof(TestOrganizationResourceHandler).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
            m_serviceManager = ApplicationServiceContext.Current.GetService<IServiceManager>();

            TestApplicationContext.Current.AddBusinessRule<Core.Model.Roles.Patient>(typeof(SamplePatientBusinessRulesService));

            var testConfiguration = new FhirServiceConfigurationSection
            {
                Resources = new List<string>
                {
                    "Patient"
                },
                
                MessageHandlers = new List<TypeReferenceConfiguration>
                {
                    new TypeReferenceConfiguration(typeof(PatientResourceHandler))
                }
            };

            using (AuthenticationContext.EnterSystemContext())
            {
                FhirResourceHandlerUtil.Initialize(testConfiguration, m_serviceManager);
                ExtensionUtil.Initialize(testConfiguration);
            }
        }

        /// <summary>
        /// Test the validate operation for a FHIR resource.
        /// </summary>
        [Test]
        public void TestValidateInvalidResource()
        {
            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var validateResourceOperation = this.m_serviceManager.CreateInjected<FhirValidateResourceOperation>();

               Assert.Throws<NotSupportedException>(() => validateResourceOperation.Invoke(new Parameters
               {
                   Parameter = new List<Parameters.ParameterComponent>
                   {
                       new Parameters.ParameterComponent
                       {
                           Name = "resource",
                           Resource = new DummyResource()
                       }
                   }
               }));
            }
        }

        /// <summary>
        /// Test the validate operation for a FHIR resource.
        /// </summary>
        [Test]
        public void TestValidateResource()
        {
            var patient = new Patient
            {
                Id = Guid.NewGuid().ToString()
            };

            Resource actual;

            TestUtil.CreateAuthority("TEST", "1.2.3.4", "http://santedb.org/fhir/test", "TEST_HARNESS", this.AUTH);

            using (TestUtil.AuthenticateFhir("TEST_HARNESS", this.AUTH))
            {
                var validateResourceOperation = this.m_serviceManager.CreateInjected<FhirValidateResourceOperation>();

                actual =  validateResourceOperation.Invoke(new Parameters
                {
                    Parameter = new List<Parameters.ParameterComponent>
                    {
                        new Parameters.ParameterComponent
                        {
                            Name = "resource",
                            Resource = patient
                        }
                    }
                });
            }

            Assert.NotNull(actual);
            Assert.IsInstanceOf<OperationOutcome>(actual);

            var actualOperationOutcome = (OperationOutcome) actual;

            Assert.IsTrue(actualOperationOutcome.Issue.Any(c => c.Severity == OperationOutcome.IssueSeverity.Error));
            Assert.IsTrue(actualOperationOutcome.Issue.Any(c => c.Code == OperationOutcome.IssueType.NoStore));
            Assert.IsTrue(actualOperationOutcome.Issue.Any(c => c.Diagnostics == "No Gender"));
        }
    }

    [ExcludeFromCodeCoverage]
    internal class DummyResource : Resource
    {
        public override IDeepCopyable DeepCopy()
        {
            return new DummyResource();
        }
    }

    [ExcludeFromCodeCoverage]
    internal class SamplePatientBusinessRulesService : IBusinessRulesService<Core.Model.Roles.Patient>
    {
        /// <summary>
        /// Gets the next BRE
        /// </summary>
        IBusinessRulesService IBusinessRulesService.Next { get; }

        /// <summary>
        /// Called after an insert occurs
        /// </summary>
        public Core.Model.Roles.Patient AfterInsert(Core.Model.Roles.Patient data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after obsolete committed
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public Core.Model.Roles.Patient AfterDelete(Core.Model.Roles.Patient data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after query
        /// </summary>
        public IQueryResultSet<Core.Model.Roles.Patient> AfterQuery(IQueryResultSet<Core.Model.Roles.Patient> results)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after retrieve
        /// </summary>
        public Core.Model.Roles.Patient AfterRetrieve(Core.Model.Roles.Patient result)
        {
            return result;
        }

        /// <summary>
        /// Called after update committed
        /// </summary>
        public Core.Model.Roles.Patient AfterUpdate(Core.Model.Roles.Patient data)
        {
            return data;
        }

        /// <summary>
        /// Called before an insert occurs
        /// </summary>
        public Core.Model.Roles.Patient BeforeInsert(Core.Model.Roles.Patient data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called before obsolete
        /// </summary>
        public Core.Model.Roles.Patient BeforeDelete(Core.Model.Roles.Patient data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called before an update occurs
        /// </summary>
        public Core.Model.Roles.Patient BeforeUpdate(Core.Model.Roles.Patient data)
        {
            return data;
        }

        /// <summary>
        /// Called to validate a specific object
        /// </summary>
        public List<DetectedIssue> Validate(Core.Model.Roles.Patient data)
        {
            var issues = new List<DetectedIssue>();

            if (data.GenderConceptKey == null)
            {
                issues.Add(new DetectedIssue(DetectedIssuePriorityType.Error, Guid.NewGuid().ToString(), "No Gender", DetectedIssueKeys.InvalidDataIssue));
            }

            return issues;
        }

        /// <summary>
        /// Gets or sets the rule to be run after this rule (for chained rules)
        /// </summary>
        public IBusinessRulesService<Core.Model.Roles.Patient> Next { get; set; }

        /// <summary>
        /// Called after an insert occurs
        /// </summary>
        public object AfterInsert(object data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after obsolete committed
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public object AfterObsolete(object data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after query
        /// </summary>
        public IQueryResultSet AfterQuery(IQueryResultSet  results)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after retrieve
        /// </summary>
        public object AfterRetrieve(object result)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called after update committed
        /// </summary>
        public object AfterUpdate(object data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called before an insert occurs
        /// </summary>
        public object BeforeInsert(object data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called before obsolete
        /// </summary>
        public object BeforeObsolete(object data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called before an update occurs
        /// </summary>
        public object BeforeUpdate(object data)
        {
            return data;
        }

        /// <summary>
        /// Called to validate a specific object
        /// </summary>
        public List<DetectedIssue> Validate(object data)
        {
            return new List<DetectedIssue>();
        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName { get; }
    }
}

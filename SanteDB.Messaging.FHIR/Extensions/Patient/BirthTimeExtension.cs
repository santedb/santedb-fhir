/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using Hl7.Fhir.Model;
using SanteDB.Core.Extensions;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Birth-time extension
    /// </summary>
    public class BirthTimeExtension : IFhirExtensionHandler
    {
        private readonly IDataPersistenceService<DateObservation> m_dateObsPersistence;

        private static readonly Guid BIRTHTIME_OBSERVATION_GUID = Guid.Parse("409538df-26e0-4ffa-b9fc-11a244eae0e5");

        /// <summary>
        /// Date time observation
        /// </summary>
        public BirthTimeExtension(IDataPersistenceService<DateObservation> dtPersistence = null)
        {
            this.m_dateObsPersistence = dtPersistence;
        }

        /// <summary>
        /// Gets the resource type this applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Gets the profile definition
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-birthTime");

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is Person person)
            {
                var btExtension = person.LoadProperty(o => o.Extensions)?.FirstOrDefault(o => o.ExtensionTypeKey == ExtensionTypeKeys.BirthTimeExtension);
                if (btExtension != null)
                {
                    person.Extensions.Remove(btExtension);
                    yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirDateTime((DateTime)btExtension.ExtensionValue));
                }
                // Attempt to find the birth registration event
                else if (this.m_dateObsPersistence == null)
                {
                    var btObs = this.m_dateObsPersistence.Query(o => o.TypeConceptKey == BIRTHTIME_OBSERVATION_GUID && o.Participations.Where(p => p.ParticipationRoleKey == ActParticipationKeys.RecordTarget).Any(p => p.PlayerEntityKey == person.Key) && o.StatusConceptKey == StatusKeys.Completed && o.IsNegated == false, AuthenticationContext.SystemPrincipal).OrderByDescending(o => o.CreationTime).FirstOrDefault();
                    if (btObs != null)
                    {
                        yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirDateTime((DateTime)btObs.Value));
                    }
                }
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (fhirExtension.Value is FhirDateTime dateTime && modelObject is Person person)
            {
                person.LoadProperty(o => o.Extensions).RemoveAll(o => o.ExtensionTypeKey == ExtensionTypeKeys.BirthTimeExtension);
                person.Extensions.Add(new EntityExtension(ExtensionTypeKeys.BirthTimeExtension, typeof(DateExtensionHandler), dateTime.ToDateTime()));
                return true;
            }


            return false;
        }
    }
}
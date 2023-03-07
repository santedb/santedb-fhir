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
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Represents a citizenship extension handler.
    /// </summary>
    public class CitizenshipExtension : IFhirExtensionHandler
    {
        /// <summary>
        /// The place repository service.
        /// </summary>
        private readonly IRepositoryService<Place> m_placeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CitizenshipExtension"/> class.
        /// </summary>
        /// <param name="placeRepository">The place repository service.</param>
        public CitizenshipExtension(IRepositoryService<Place> placeRepository)
        {
            this.m_placeRepository = placeRepository;
        }

        /// <summary>
        /// Applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Get the profile URI
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-citizenship");

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is Core.Model.Roles.Patient patient)
            {
                // citizenship?
                foreach (var citizenshipExtension in patient.LoadCollection(o => o.Relationships).Where(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Citizen))
                {
                    var citizenPlace = citizenshipExtension.LoadProperty(o => o.TargetEntity);
                    var isoCode = citizenPlace.GetIdentifiers().FirstOrDefault(o => o.IdentityDomainKey == IdentityDomainKeys.Iso3166CountryCode);

                    if (isoCode != null)
                    {
                        yield return new Extension(this.Uri.ToString(), new CodeableConcept($"urn:oid:{isoCode.LoadProperty(o => o.IdentityDomain).Oid}", isoCode.Value));
                    }
                }
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is Core.Model.Roles.Patient patient && fhirExtension.Value is CodeableConcept cc)
            {
                var isoCode = cc.Coding.FirstOrDefault(o => o.System == "urn:iso:std:iso:3166:1" || o.System == "urn:oid:1.0.3166.1.2.3");

                if (isoCode != null)
                {
                    var country = this.m_placeRepository.Find(o => o.Identifiers.Where(a => a.IdentityDomainKey == IdentityDomainKeys.Iso3166CountryCode).Any(i => i.Value == isoCode.Code) && StatusKeys.ActiveStates.Contains(o.StatusConceptKey.Value)).SingleOrDefault();

                    if (country != null && !patient.LoadProperty(o => o.Relationships).Any(c => c.TargetEntityKey == country.Key))
                    {
                        patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Citizen, country));

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
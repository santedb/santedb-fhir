﻿/*
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
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Implementation of the birthplace extension
    /// </summary>
    public class BirthPlaceExtension : IFhirExtensionHandler
    {
        // Address Hierarchy
        private readonly Guid[] AddressHierarchy = {
            EntityClassKeys.ServiceDeliveryLocation,
            EntityClassKeys.CityOrTown,
            EntityClassKeys.CountyOrParish,
            EntityClassKeys.State,
            EntityClassKeys.Country,
            EntityClassKeys.Place
        };


        // Place repository
        private IRepositoryService<SanteDB.Core.Model.Entities.Place> m_placeRepository;

        /// <summary>
        /// DI injection
        /// </summary>
        public BirthPlaceExtension(IRepositoryService<SanteDB.Core.Model.Entities.Place> placeRepository)
        {
            this.m_placeRepository = placeRepository;
        }

        /// <summary>
        /// URI which appears on the extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-birthPlace");

        /// <summary>
        /// Profile URI
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Resource this extension applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Construct the extension
        /// </summary>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient)
            {
                // Birthplace?
                var birthPlaceRelationship = patient.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace);
                if (birthPlaceRelationship != null)
                {
                    var address = DataTypeConverter.ToFhirAddress(birthPlaceRelationship.LoadProperty(o => o.TargetEntity).LoadCollection(o => o.Addresses)?.FirstOrDefault());
                    var test = birthPlaceRelationship.TargetEntity.LoadCollection(o => o.Names);
                    address.Text = birthPlaceRelationship.TargetEntity.LoadCollection(o => o.Names)?.FirstOrDefault(c => c.NameUseKey == NameUseKeys.Search)?.ToDisplay();
                    yield return new Extension(this.Uri.ToString(), address);
                }
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SanteDB.Core.Model.Roles.Patient patient && fhirExtension.Value is Hl7.Fhir.Model.Address address)
            {
                // TODO: Update this to use the new more efficient method of getting data from database
                // Something like: Query by names, then order by the address hierarchy?
                var birthPlaceRelationship = patient.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Birthplace);
                var places = ApplicationServiceContext.Current.GetService<IDataPersistenceService<Place>>()?.Query(o => o.Names.Any(c => c.Component.Any(a => a.Value == address.Text)), AuthenticationContext.SystemPrincipal);
                var placeCount = places.Count();
                if (placeCount > 1)
                {
                    var placeClasses = places.ToArray().GroupBy(o => o.ClassConceptKey).OrderBy(o => Array.IndexOf(AddressHierarchy, o.Key.Value));
                    // Take the first wrung of the address hierarchy
                    var loadedPlaces = placeClasses.First();
                    if (loadedPlaces.Count() > 1) // Still more than one type of place
                    {
                        throw new KeyNotFoundException("Cannot find unique birth place registration.");
                    }
                    else
                    {
                        patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Birthplace, loadedPlaces.First()));
                    }
                }
                if (placeCount == 1)
                {
                    if (birthPlaceRelationship == null)
                    {
                        patient.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Birthplace, places.Select(o => o.Key).First()));
                    }
                    else
                    {
                        birthPlaceRelationship.TargetEntityKey = places.Select(o => o.Key).First();
                    }

                    return true;
                }
                else
                {
                    throw new FhirException((System.Net.HttpStatusCode)422, OperationOutcome.IssueType.MultipleMatches, "Cannot find unique birth place registration.");
                }
            }
            return false;
        }
    }
}
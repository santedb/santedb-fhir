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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Facility resource handler
    /// </summary>
    public class LocationResourceHandler : RepositoryResourceHandlerBase<Location, Place>
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(LocationResourceHandler));

        /// <summary>
		/// Create new resource handler
		/// </summary>
        public LocationResourceHandler(IRepositoryService<Place> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {

        }

        /// <summary>
        /// Map the inbound place to a FHIR model
        /// </summary>
        protected override Location MapToFhir(Place model)
        {
            Location retVal = DataTypeConverter.CreateResource<Location>(model);
            retVal.Identifier = model.LoadCollection<EntityIdentifier>("Identifiers").Select(o => DataTypeConverter.ToFhirIdentifier<Entity>(o)).ToList();

            // Map status
            switch (model.StatusConceptKey.ToString().ToUpper())
            {
                case StatusKeyStrings.Active:
                case StatusKeyStrings.New:
                    retVal.Status = Location.LocationStatus.Active;
                    break;
                case StatusKeyStrings.Cancelled:
                    retVal.Status = Location.LocationStatus.Suspended;
                    break;
                case StatusKeyStrings.Nullified:
                case StatusKeyStrings.Obsolete:
                case StatusKeyStrings.Inactive:
                    retVal.Status = Location.LocationStatus.Inactive;
                    break;
            }

            retVal.Name = model.LoadProperty(o => o.Names).FirstOrDefault(o => o.NameUseKey == NameUseKeys.OfficialRecord)?.LoadCollection<EntityNameComponent>("Component")?.FirstOrDefault()?.Value;
            retVal.Alias = model.LoadProperty(o => o.Names).Where(o => o.NameUseKey != NameUseKeys.OfficialRecord)?.Select(n => n.LoadCollection<EntityNameComponent>("Component")?.FirstOrDefault()?.Value).ToList();
            retVal.PhysicalType = DataTypeConverter.ToFhirCodeableConcept(model.ClassConceptKey, "http://terminology.hl7.org/CodeSystem/location-physical-type");
            // Convert the determiner code
            if (model.DeterminerConceptKey == DeterminerKeys.Described)
            {
                retVal.Mode = Location.LocationMode.Kind;
            }
            else
            {
                retVal.Mode = Location.LocationMode.Instance;
            }

            retVal.Type = new List<CodeableConcept>() { DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey, "http://terminology.hl7.org/ValueSet/v3-ServiceDeliveryLocationRoleType") };
            retVal.Telecom = model.LoadProperty(o => o.Telecoms).Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();
            retVal.Address = DataTypeConverter.ToFhirAddress(model.LoadProperty(o => o.Addresses).FirstOrDefault());

            if (model.LoadProperty(o => o.GeoTag) != null)
            {
                retVal.Position = new Location.PositionComponent()
                {
                    Latitude = (decimal)model.GeoTag.Lat,
                    Longitude = (decimal)model.GeoTag.Lng
                };
            }

            // Part of?
            var parent = model.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Parent);
            if (parent != null)
            {
                retVal.PartOf = DataTypeConverter.CreateNonVersionedReference<Location>(parent.LoadProperty(o => o.TargetEntity));
            }

            if(model.GeoTag != null)
            {
                retVal.Position = new Location.PositionComponent()
                {
                    Latitude = (decimal)model.GeoTag.Lat,
                    Longitude = (decimal)model.GeoTag.Lng
                };
            }



            return retVal;
        }

        /// <summary>
        /// Map the incoming FHIR resource to a MODEL resource
        /// </summary>
        /// <param name="resource">The resource to be mapped</param>
        /// <returns></returns>
		protected override Place MapToModel(Location resource)
        {
            Place place = null;

            if (Guid.TryParse(resource.Id, out Guid key))
            {
                place = this.m_repository.Get(key);

                if (place == null)
                {
                    place = new Place
                    {
                        Key = key
                    };
                }
            }
            else if (resource.Identifier.Any())
            {
                foreach (var ii in resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier))
                {
                    if (ii.LoadProperty(o => o.IdentityDomain).IsUnique)
                    {
                        place = this.m_repository.Find(o => o.Identifiers.Where(i => i.IdentityDomainKey == ii.IdentityDomainKey).Any(i => i.Value == ii.Value)).FirstOrDefault();
                    }
                    if (place != null)
                    {
                        break;
                    }
                }

                if (place == null)
                {
                    place = new Place
                    {
                        Key = Guid.NewGuid()
                    };
                }
            }
            else
            {
                place = new Place
                {
                    Key = Guid.NewGuid()
                };
            }

            switch (resource.Status)
            {
                case Location.LocationStatus.Active:
                    place.StatusConceptKey = StatusKeys.Active;
                    break;
                case Location.LocationStatus.Suspended:
                    throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
                case Location.LocationStatus.Inactive:
                    place.StatusConceptKey = StatusKeys.Inactive;
                    break;
            }

            place.ClassConceptKey = DataTypeConverter.ToConcept(resource.PhysicalType)?.Key ?? place.ClassConceptKey ?? EntityClassKeys.Place;

            // add the textual representation of the name of the place as the address text property for search purposes
            // see the BirthPlaceExtension class
            if (!string.IsNullOrEmpty(resource.Address?.Text))
            {
                place.LoadProperty(o => o.Names).RemoveAll(o => o.NameUseKey == NameUseKeys.Search);
                place.LoadProperty(o => o.Names).Add(new EntityName(NameUseKeys.Search, resource.Address.Text));
            }
            place.LoadProperty(o => o.Names).Add(new EntityName(NameUseKeys.OfficialRecord, resource.Name)
            {
                Key = place.Names.FirstOrDefault(o=>o.NameUseKey == NameUseKeys.OfficialRecord)?.Key
            });
            place.LoadProperty(o => o.Names).RemoveAll(o => o.NameUseKey == NameUseKeys.Pseudonym);
            place.LoadProperty(o => o.Names).AddRange(resource.Alias.Select(o => new EntityName(NameUseKeys.Pseudonym, o)));

            if (resource.Mode == Location.LocationMode.Kind)
            {
                place.DeterminerConceptKey = DeterminerKeys.Described;
            }
            else
            {
                place.DeterminerConceptKey = DeterminerKeys.Specific;
            }

            place.TypeConcept = DataTypeConverter.ToConcept(resource.Type.FirstOrDefault());
            place.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList();
            place.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();

            if (resource.Address != null)
            {
                place.Addresses = new List<EntityAddress>() { DataTypeConverter.ToEntityAddress(resource.Address) };
            }

            if (resource.Position != null)
            {
                place.GeoTag = new GeoTag
                {
                    Lat = (double)resource.Position.Latitude,
                    Lng = (double)resource.Position.Longitude
                };
            }

            if (resource.PartOf != null)
            {
                var reference = DataTypeConverter.ResolveEntity<Place>(resource.PartOf, resource);
                if (reference == null)
                {
                    this.m_tracer.TraceError($"Could not resolve {resource.PartOf.Reference}");
                    throw new KeyNotFoundException(m_localizationService.GetString("error.type.KeyNotFoundException.couldNotResolve", new
                    {
                        param = resource.PartOf.Reference
                    }));
                }

                // point the child place entity at the target place entity with a relationship of parent 
                place.LoadProperty(o => o.Relationships).Add(new EntityRelationship(EntityRelationshipTypeKeys.Parent, reference));
            }

            return place;
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Delete,
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Update
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Place resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(Place resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
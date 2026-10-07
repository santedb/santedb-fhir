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
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Practitioner resource handler
    /// </summary>
    public class PractitionerResourceHandler : RepositoryResourceHandlerBase<Practitioner, Core.Model.Entities.Person>
    {
        private readonly Guid[] m_classConcepts = new Guid[]
        {
            EntityClassKeys.UserEntity,
            EntityClassKeys.Provider
        };


        /// <summary>
        /// Create a new resource handler
        /// </summary>
        public PractitionerResourceHandler(IRepositoryService<Core.Model.Entities.Person> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {

        }

        /// <summary>
        /// Can map object
        /// </summary>
        public override bool CanMapObject(object instance) => instance is UserEntity || instance is Provider;

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Entities.Person resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the interactions that this resource handler supports
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Delete,
                TypeRestfulInteraction.Update,
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Vread
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Entities.Person resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Map a user entity to a practitioner
        /// </summary>
        protected override Practitioner MapToFhir(Core.Model.Entities.Person model)
        {
            // Is there a provider that matches this user?
            var provider = model.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.EquivalentEntity && o.ClassificationKey == RelationshipClassKeys.PlayedRoleLink)?.LoadProperty(o => o.TargetEntity) as Provider;
            model = provider ?? model;

            var retVal = DataTypeConverter.CreateResource<Practitioner>(model);

            // Identifiers
            retVal.Identifier = model.LoadCollection(o => o.Identifiers)?.Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

            // ACtive
            retVal.Active = StatusKeys.ActiveStates.Contains(model.StatusConceptKey.Value);

            // Names
            retVal.Name = model.LoadCollection(o => o.Names)?.Select(o => DataTypeConverter.ToFhirHumanName(o)).ToList();

            // Telecoms
            retVal.Telecom = model.LoadCollection(o => o.Telecoms)?.Select(o => DataTypeConverter.ToFhirTelecom(o)).ToList();

            // Address
            retVal.Address = model.LoadCollection(p => p.Addresses)?.Select(o => DataTypeConverter.ToFhirAddress(o)).ToList();

            // Birthdate
            retVal.BirthDateElement = DataTypeConverter.ToFhirDate(provider?.DateOfBirth ?? model.DateOfBirth);

            var photo = (provider?.LoadProperty(o => o.Extensions) ?? model.LoadProperty(o => o.Extensions))?.FirstOrDefault(o => o.ExtensionTypeKey == ExtensionTypeKeys.JpegPhotoExtension);
            if (photo != null)
            {
                retVal.Photo = new List<Attachment>() {
                    new Attachment()
                    {
                        ContentType = "image/jpg",
                        Data = photo.ExtensionValueData
                    }
                };
            }

            if (provider != null)
            {
                retVal.Qualification = new List<Practitioner.QualificationComponent>() { new Practitioner.QualificationComponent() { Code = DataTypeConverter.ToFhirCodeableConcept(provider.SpecialtyKey) } };
            }

            // Language of communication
            retVal.Communication = model.LoadCollection(o => o.LanguageCommunication)?.Select(o => new CodeableConcept("http://tools.ietf.org/html/bcp47", o.LanguageCode)).ToList();

            return retVal;
        }

        /// <summary>
        /// Map a practitioner to a user entity
        /// </summary>
        protected override Core.Model.Entities.Person MapToModel(Practitioner resource)
        {
            Core.Model.Entities.Person retVal = null;

            if (Guid.TryParse(resource.Id, out var key))
            {
                retVal = this.m_repository.Get(key);
            }
            else if (resource.Identifier.Any())
            {
                foreach (var ii in resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier))
                {
                    if (ii.LoadProperty(o => o.IdentityDomain).IsUnique)
                    {
                        retVal = this.m_repository.Find(o => this.m_classConcepts.Contains(o.ClassConceptKey.Value) && StatusKeys.ActiveStates.Contains(o.StatusConceptKey.Value) && o.Identifiers.Where(i => i.IdentityDomainKey == ii.IdentityDomainKey).Any(i => i.Value == ii.Value)).FirstOrDefault();
                    }
                    if (retVal != null)
                    {
                        break;
                    }
                }
            }

            if (retVal == null)
            {
                retVal = new Provider
                {
                    Key = Guid.NewGuid()
                };
            }

            // Organization
            retVal.Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList();
            retVal.Notes = DataTypeConverter.ToNote<EntityNote>(resource.Text);

            // TODO: Extensions
            retVal.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
            retVal.Names = resource.Name.Select(DataTypeConverter.ToEntityName).ToList();
            retVal.StatusConceptKey = !resource.Active.HasValue || resource.Active == true ? StatusKeys.Active : StatusKeys.Inactive;
            retVal.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).ToList();
            retVal.LoadProperty(o => o.Extensions).AddRange(resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, retVal, false)).Concat(resource.ModifierExtension.Select(o => DataTypeConverter.ToEntityExtension(o, retVal, true))).OfType<EntityExtension>());
            retVal.GenderConceptKey = resource.Gender == null ? NullReasonKeys.Unknown : DataTypeConverter.ToConcept(new Coding("http://hl7.org/fhir/administrative-gender", Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key;
            retVal.DateOfBirthXml = resource.BirthDate;
            retVal.DateOfBirthPrecision = DatePrecision.Day;
            retVal.LanguageCommunication = resource.Communication.Select(c => DataTypeConverter.ToLanguageCommunication(c, false)).ToList();

            if (resource.Photo != null && resource.Photo.Any())
            {
                retVal.Extensions.RemoveAll(o => o.ExtensionTypeKey == ExtensionTypeKeys.JpegPhotoExtension);
                retVal.Extensions.Add(new EntityExtension(ExtensionTypeKeys.JpegPhotoExtension, resource.Photo.First().Data));
            }

            if (resource.Qualification.Any() && retVal is Provider pvdr)
            {
                pvdr.Specialty = DataTypeConverter.ToConcept(resource.Qualification.First().Code);
            }

            return retVal;
        }

        /// <summary>
        /// Query for substance administrations.
        /// </summary>
        /// <param name="query">The query to be executed</param>
        /// <param name="fhirParameters">The fhir parameters provided in the query.</param>
        /// <param name="hdsiParameters">The translated hdsi parameters that can be executed by the query.</param>
        /// <returns>Returns the list of models which match the given parameters.</returns>
        protected override IQueryResultSet<Core.Model.Entities.Person> QueryInternal(System.Linq.Expressions.Expression<Func<Core.Model.Entities.Person, bool>> query, NameValueCollection fhirParameters = null, NameValueCollection hdsiParameters = null)
        {

            var typeReference = System.Linq.Expressions.Expression.Call(
                null,
                (System.Reflection.MethodInfo)typeof(Enumerable).GetGenericMethod(nameof(Enumerable.Contains), new Type[] { typeof(Guid) }, new Type[] { typeof(IEnumerable<Guid>), typeof(Guid) }),
                System.Linq.Expressions.Expression.Constant(m_classConcepts),
                System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.MakeMemberAccess(query.Parameters[0], typeof(Core.Model.Entities.Person).GetProperty(nameof(Entity.ClassConcept))), typeof(Guid))
            );

            query = System.Linq.Expressions.Expression.Lambda<Func<Core.Model.Entities.Person, bool>>(
                System.Linq.Expressions.Expression.AndAlso(
                    query.Body,
                    typeReference
                    )
                , query.Parameters);
            return this.m_repository.Find(query);
        }

    }
}
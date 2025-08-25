using Hl7.Fhir.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// A Fhir Resource Handler that handles the person resource type.
    /// </summary>
    public class PersonResourceHandler : RepositoryResourceHandlerBase<Hl7.Fhir.Model.Person, SanteDB.Core.Model.Entities.Person>
    {
        ///<inheritdoc />
        public PersonResourceHandler(IRepositoryService<Core.Model.Entities.Person> repository, ILocalizationService localizationService) : base(repository, localizationService)
        {
        }


        ///<inheritdoc />
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Entities.Person resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        ///<inheritdoc />
        protected override IEnumerable<CapabilityStatement.ResourceInteractionComponent> GetInteractions() =>
            new CapabilityStatement.TypeRestfulInteraction[]
            {
                CapabilityStatement.TypeRestfulInteraction.Create,
                CapabilityStatement.TypeRestfulInteraction.Update,
                CapabilityStatement.TypeRestfulInteraction.Delete,
                CapabilityStatement.TypeRestfulInteraction.Read,
                CapabilityStatement.TypeRestfulInteraction.SearchType,
            }.Select(interaction => new CapabilityStatement.ResourceInteractionComponent { Code = interaction });

        ///<inheritdoc />
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Entities.Person resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        ///<inheritdoc />
        protected override Hl7.Fhir.Model.Person MapToFhir(Core.Model.Entities.Person model)
        {
            if (null == model)
                return null;

            var result = new Hl7.Fhir.Model.Person();

            result.Id = model.Key?.ToString();

            var identifiers = model.LoadProperty(pers => pers.Identifiers);

            if (null != identifiers)
                result.Identifier = identifiers.Select(Util.DataTypeConverter.ToFhirIdentifier).ToList();

            var names = model.LoadProperty(pers => pers.Names);

            if (null != names)
                result.Name = names.Select(Util.DataTypeConverter.ToFhirHumanName).ToList();

            var telecoms = model.LoadProperty(pers => pers.Telecoms);

            if (null != telecoms)
                result.Telecom = telecoms.Select(Util.DataTypeConverter.ToFhirTelecom).ToList();

            if (null == model.GenderConceptKey)
                result.Gender = null;
            else
                result.Gender = Util.DataTypeConverter.ToFhirEnumeration<AdministrativeGender>(model.GenderConceptKey, FhirConstants.CodeSystem_AdministrativeGender, true);

            if (null == model.DateOfBirth)
                result.BirthDate = null;
            else
                result.BirthDateElement = Util.DataTypeConverter.ToFhirDate(model.DateOfBirth);

            var addresses = model.LoadProperty(pers => pers.Addresses);

            if (null != addresses)
                result.Address = addresses.Select(Util.DataTypeConverter.ToFhirAddress).ToList();

            var extensions = model.LoadProperty(pers => pers.Extensions);

            if (null != extensions)
            {
                var photo = extensions.FirstOrDefault(extn => extn.ExtensionTypeKey == ExtensionTypeKeys.JpegPhotoExtension);

                if (null != photo)
                    result.Photo = new Attachment
                    {
                        ContentType = "image/jpeg",
                        Data = photo.ExtensionValueData
                    };

                //Insert other extension handling here.   
            }

            var relationships = model.LoadProperty(pers => pers.Relationships);

            if (null != relationships)
            {
                var scoper = relationships.FirstOrDefault(rel => rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper);

                if (null != scoper)
                    result.ManagingOrganization = Util.DataTypeConverter.CreateNonVersionedReference<Organization>(scoper);
            }

            result.Active = model.StatusConceptKey == StatusKeys.Active || model.StatusConceptKey == StatusKeys.New;

            if (model.ClassConceptKey == EntityClassKeys.Patient)
            {
                result.Link = new List<Person.LinkComponent>
                {
                    new Person.LinkComponent
                    {
                        Target = Util.DataTypeConverter.CreateNonVersionedReference<Patient>(model.Key)
                    }
                };
            }
            else if (model.ClassConceptKey == EntityClassKeys.Provider)
            {
                result.Link = new List<Person.LinkComponent>
                {
                    new Person.LinkComponent
                    {
                        Target = Util.DataTypeConverter.CreateNonVersionedReference<Practitioner>(model.Key)
                    }
                };
            }

                return result;
        }

        ///<inheritdoc />
        protected override Core.Model.Entities.Person MapToModel(Hl7.Fhir.Model.Person resource)
        {
            throw new NotImplementedException();
        }
    }
}

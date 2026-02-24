using Hl7.Fhir.Model;
using SanteDB.Core.Data;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
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
        public PersonResourceHandler(
            IRepositoryService<Core.Model.Entities.Person> repository,
            ILocalizationService localizationService) : base(repository, localizationService)
        {
        }


        ///<inheritdoc />
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Entities.Person resource, IEnumerable<IncludeInstruction> includePaths)
        {
            return includePaths.SelectMany<IncludeInstruction, Resource>(instruction =>
            {
                switch (instruction.Type)
                {
                    case ResourceType.Organization:
                        var handler = FhirResourceHandlerUtil.GetMappersFor(instruction.Type).FirstOrDefault();

                        switch (instruction.JoinPath)
                        {
                            case "managingOrganization":
                                return resource.LoadProperty(res => res.Relationships)
                                    .Where(rel => rel.ClassificationKey != RelationshipClassKeys.ContainedObjectLink &&
                                        rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper &&
                                        rel.LoadProperty(r => r.TargetEntity) is Core.Model.Entities.Organization)
                                    .Select(rel => handler.MapToFhir(rel.TargetEntity));
                            default:
                                m_traceSource.TraceError(ErrorMessages.FHIR_INCLUDE_PATH_UNSUPPORTED, instruction);
                                throw new InvalidOperationException(m_localizationService.GetString("error.type.InvalidOperation.cannotDetermine", new { param = instruction }));
                        }
                    default:
                        m_traceSource.TraceError($"{instruction.Type} is not supported.");
                        throw new InvalidOperationException(this.m_localizationService.GetString("error.type.NotSupportedException.userMessage"));
                }
            });
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

            if (null == result.Meta)
                result.Meta = new Meta();

            result.Meta.VersionId = model.VersionKey?.ToString();
            result.Meta.LastUpdated = model.LastModified();

            var identifiers = model.LoadProperty(pers => pers.Identifiers);

            if (null != identifiers)
                result.Identifier = identifiers.Select(Util.DataTypeConverter.ToFhirIdentifier).ToList();

            var names = model.LoadProperty(pers => pers.Names);

            if (null != names)
                result.Name = names.Select(Util.DataTypeConverter.ToFhirHumanName).ToList();

            var telecoms = model.LoadProperty(pers => pers.Telecoms);

            if (null != telecoms)
                result.Telecom = telecoms.Select(Util.DataTypeConverter.ToFhirTelecom).ToList();

            if (null == model.GenderConceptKey || NullReasonKeys.MissingInformation.Contains(model.GenderConceptKey.Value))
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
                var scoper = relationships.FirstOrDefault(rel => rel.ClassificationKey != RelationshipClassKeys.ContainedObjectLink && rel.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper);

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
            //First, we need to check if the entity already exists.
            //We need to do this to determine the "type" of person we're working with.

            Core.Model.Entities.Person model = null;

            if (resource.Id != null && Guid.TryParse(resource.Id, out Guid entid))
            {
                model = m_repository.Get(entid);
            }
            else if (resource?.Identifier?.Any() == true)
            {
                foreach (var id in resource.Identifier.Select(Util.DataTypeConverter.ToEntityIdentifier))
                {
                    if (null == id || null == id.IdentityDomain || !id.IdentityDomain.IsUnique || string.IsNullOrWhiteSpace(id.Value))
                        continue;

                    model = m_repository.Find(pers => pers.Identifiers.Any(iid => iid.IdentityDomainKey == id.IdentityDomainKey && iid.Value == id.Value)).FirstOrDefault();

                    if (null != model)
                        break;
                }
            }

            if (null == model) //If everything else has failed, create a new Person.
                model = new Core.Model.Entities.Person();

            var modelids = model.LoadProperty(m => m.Identifiers);

            foreach (var resourceid in resource.Identifier)
            {
                var modelid = Util.DataTypeConverter.ToEntityIdentifier(resourceid);

                if (!modelids.Any(mid => mid.IdentityDomainKey == modelid.IdentityDomainKey && mid.Value == modelid.Value))
                {
                    model.Identifiers.Add(modelid);
                }
            }

            model.Names = resource.Name.Select(Util.DataTypeConverter.ToEntityName).ToList();
            model.Telecoms = resource.Telecom.Select(Util.DataTypeConverter.ToEntityTelecomAddress).ToList();

            if (null != resource.Gender)
                model.GenderConceptKey = DataTypeConverter.ToConcept(new Coding(FhirConstants.CodeSystem_AdministrativeGender, Hl7.Fhir.Utility.EnumUtility.GetLiteral(resource.Gender)))?.Key;

            if (null != resource.BirthDateElement)
                model.DateOfBirth = DataTypeConverter.ToDateTimeOffset(resource.BirthDate)?.DateTime;

            model.Addresses = resource.Address.Select(Util.DataTypeConverter.ToEntityAddress).ToList();

            if (null != resource.Photo)
            {
                model.RemoveExtension(ExtensionTypeKeys.JpegPhotoExtension);
                model.AddExtension(ExtensionTypeKeys.JpegPhotoExtension, typeof(Core.Extensions.BinaryExtensionHandler), resource.Photo.Data);
            }

            if (resource.ManagingOrganization != null)
            {
                if (Util.DataTypeConverter.TryResolveResourceReference(resource.ManagingOrganization, null, out var scoper))
                {
                    var rels = model.LoadProperty(m => m.Relationships);

                    var rel = rels.FirstOrDefault(r => r.RelationshipTypeKey == EntityRelationshipTypeKeys.Scoper && r.TargetEntityKey == scoper.Key);

                    if (rel == null)
                    {
                        model.Relationships.Add(new Core.Model.Entities.EntityRelationship
                        {
                            RelationshipTypeKey = EntityRelationshipTypeKeys.Scoper,
                            SourceEntity = model,
                            TargetEntityKey = scoper.Key
                        });
                    }
                }
            }

            //We will consciously ignore active from the FHIR resource. We'll use our own status

            return model;
        }
    }

}


/*
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
 * User: fyfej
 * Date: 2021-8-5
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    
    /// <summary>
    /// Organization resource provider
    /// </summary>
    public class OrganizationResourceHandler : RepositoryResourceHandlerBase<Hl7.Fhir.Model.Organization, SanteDB.Core.Model.Entities.Organization>
    {
        private readonly ILocalizationService m_localizationService;

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(OrganizationResourceHandler));

        /// <summary>
        /// Create a new resource handler
        /// </summary>
        public OrganizationResourceHandler(IRepositoryService<SanteDB.Core.Model.Entities.Organization> repo) : base(repo)
        {
            this.m_localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();

        }


        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.Entities.Organization resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get the interactions 
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions() =>
            new TypeRestfulInteraction[]
            {
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.HistoryInstance
            }.Select(o => new ResourceInteractionComponent() { Code = o });

        /// <summary>
        /// Get reverse included resources
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.Entities.Organization resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Map to FHIR
        /// </summary>
        protected override Hl7.Fhir.Model.Organization MapToFhir(Core.Model.Entities.Organization model)
        {
            var retVal = DataTypeConverter.CreateResource<Hl7.Fhir.Model.Organization>(model);

            retVal.Identifier = model.LoadCollection(o => o.Identifiers).Select(o=>DataTypeConverter.ToFhirIdentifier(o)).ToList();
            retVal.Active = model.StatusConceptKey == StatusKeys.Active;
            retVal.Telecom = model.LoadCollection(o => o.Telecoms).Select(DataTypeConverter.ToFhirTelecom).ToList();
            retVal.Address = model.LoadCollection(o => o.Addresses).Select(DataTypeConverter.ToFhirAddress).ToList();
            retVal.Name = model.LoadCollection(o => o.Names).FirstOrDefault(o => o.NameUseKey == NameUseKeys.OfficialRecord)?.ToDisplay();
            retVal.Alias = model.LoadCollection(o => o.Names).Where(o => o.NameUseKey == NameUseKeys.Pseudonym).Select(o => o.ToDisplay());

            var parent = model.LoadCollection(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Parent);
            if (parent != null) {
                retVal.PartOf = DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Organization>(parent.TargetEntityKey);
            }

            return retVal;
        }

        /// <summary>
        /// Map to Model
        /// </summary>
        protected override Core.Model.Entities.Organization MapToModel(Hl7.Fhir.Model.Organization resource)
        {
            Core.Model.Entities.Organization retVal = null;
            if(Guid.TryParse(resource.Id, out Guid key))
            {
                retVal = this.m_repository.Get(key);
            }
            else if(resource.Identifier?.Count > 0)
            {
                foreach (var ii in resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier))
                {
                    if (ii.LoadProperty(o => o.Authority).IsUnique)
                    {
                        retVal = this.m_repository.Find(o => o.Identifiers.Where(i => i.AuthorityKey == ii.AuthorityKey).Any(i => i.Value == ii.Value)).FirstOrDefault();
                    }
                    if (retVal != null)
                    {
                        break;
                    }
                }
            }
            if(retVal == null)
            {
                retVal = new Core.Model.Entities.Organization()
                {
                    Key = Guid.NewGuid()
                };
            }

            // Organization
            retVal.TypeConcept = resource.Type.Select(o => DataTypeConverter.ToConcept(o)).OfType<Concept>().FirstOrDefault();
            retVal.Addresses = resource.Address.Select(DataTypeConverter.ToEntityAddress).ToList();

            // TODO: Extensions
            retVal.Identifiers = resource.Identifier.Select(DataTypeConverter.ToEntityIdentifier).ToList();
            retVal.Names = new List<EntityName>() { new EntityName(NameUseKeys.OfficialRecord, resource.Name) };
            retVal.Names.AddRange(resource.Alias.Select(o => new EntityName(NameUseKeys.Pseudonym, o)));
            retVal.StatusConceptKey = !resource.Active.HasValue || resource.Active == true ? StatusKeys.Active : StatusKeys.Obsolete;
            retVal.Telecoms = resource.Telecom.Select(DataTypeConverter.ToEntityTelecomAddress).OfType<EntityTelecomAddress>().ToList();

            if(resource.PartOf != null)
            {
                var reference = DataTypeConverter.ResolveEntity<Core.Model.Entities.Organization>(resource.PartOf, resource);
                if(reference == null)
                {
                    this.m_tracer.TraceError($"Could not resolve {resource.PartOf.Reference}");
                    throw new KeyNotFoundException(m_localizationService.FormatString("error.type.KeyNotFoundException.couldNotResolve", new { 
                        param = resource.PartOf.Reference
                    }));
                   
                }
                retVal.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Parent, reference as Entity));
            } 
            retVal.Extensions = resource.Extension.Select(o => DataTypeConverter.ToEntityExtension(o, retVal)).OfType<EntityExtension>().ToList();
            return retVal;
        }
    }
}

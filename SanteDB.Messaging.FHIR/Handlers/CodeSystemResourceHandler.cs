using Hl7.Fhir.Model;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Resource handler that implements code system management
    /// </summary>
    public class CodeSystemResourceHandler : RepositoryResourceHandlerBase<CodeSystem, SanteDB.Core.Model.DataTypes.CodeSystem>
    {
        private readonly IConceptRepositoryService m_conceptRepository;
        private readonly IRepositoryService<SanteDB.Core.Model.DataTypes.ReferenceTerm> m_referenceTermRepository;

        /// <summary>
        /// DI Constructor
        /// </summary>
        public CodeSystemResourceHandler(IConceptRepositoryService conceptRepository,
            IRepositoryService<SanteDB.Core.Model.DataTypes.ReferenceTerm> referenceTermRepository,
            IRepositoryService<Core.Model.DataTypes.CodeSystem> repository,
            ILocalizationService localizationService) : base(repository, localizationService)
        {
            this.m_conceptRepository = conceptRepository;
            this.m_referenceTermRepository = referenceTermRepository;
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetIncludes(Core.Model.DataTypes.CodeSystem resource, IEnumerable<IncludeInstruction> includePaths)
        {
            yield break;
        }

        /// <inheritdoc/>
        protected override IEnumerable<CapabilityStatement.ResourceInteractionComponent> GetInteractions()
        {
            return new[]
             {
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.SearchType
            }.Select(o => new ResourceInteractionComponent
            { Code = o });
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetReverseIncludes(Core.Model.DataTypes.CodeSystem resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            yield break;
        }

        /// <inheritdoc/>
        protected override CodeSystem MapToFhir(Core.Model.DataTypes.CodeSystem model)
        {
            var retVal = DataTypeConverter.CreateResource<CodeSystem>(model);

            retVal.Title = model.Name;
            retVal.CaseSensitive = true;
            retVal.Compositional = false;
            retVal.Url = model.Url;
            retVal.Status = PublicationStatus.Active;
            retVal.Version = model.VersionText;
            retVal.Identifier = new List<Identifier>()
            {
                new Identifier(FhirConstants.OidSystem, model.Oid)
            };
            retVal.Name = model.Domain;
            retVal.Description = new Markdown(model.Description);
            retVal.Concept = this.m_referenceTermRepository.Find(o => o.CodeSystemKey == model.Key).ToArray().Select(o => new CodeSystem.ConceptDefinitionComponent()
            {
                Code = o.Mnemonic,
                Designation = o.LoadProperty(n => n.DisplayNames).Select(n => new CodeSystem.DesignationComponent()
                {
                    Language = n.Language,
                    Value = n.Name
                }).ToList(),
                Display = o.DisplayNames.FirstOrDefault()?.Name,
                Extension = o.LoadProperty(p => p.Concepts).Select(c => new Extension($"{FhirConstants.SanteDBProfile}/extension/codeSystem/concept", new Coding(FhirConstants.SanteDBConceptSystem, c.LoadProperty(p => p.SourceEntity).Mnemonic))).ToList()
            }).ToList();

            return retVal;
        }

        /// <inheritdoc/>
        protected override Core.Model.DataTypes.CodeSystem MapToModel(CodeSystem resource)
        {
            throw new NotImplementedException();
        }

    }
}

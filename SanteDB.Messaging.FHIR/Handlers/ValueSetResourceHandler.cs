using Hl7.Fhir.Model;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Value set resource handler
    /// </summary>
    public class ValueSetResourceHandler : RepositoryResourceHandlerBase<ValueSet, SanteDB.Core.Model.DataTypes.ConceptSet>
    {
        private readonly IConceptRepositoryService m_coneptRepository;

        /// <summary>
        /// DI Ctor
        /// </summary>
        public ValueSetResourceHandler(
            IConceptRepositoryService conceptRepository,
            IRepositoryService<ConceptSet> repository, 
            ILocalizationService localizationService) : base(repository, localizationService)
        {
            this.m_coneptRepository = conceptRepository;
        }

        /// <inheritdoc/>
        protected override IEnumerable<Resource> GetIncludes(ConceptSet resource, IEnumerable<IncludeInstruction> includePaths)
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
        protected override IEnumerable<Resource> GetReverseIncludes(ConceptSet resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            yield break;
        }

        /// <inheritdoc/>
        protected override ValueSet MapToFhir(ConceptSet model)
        {
            var retVal = DataTypeConverter.CreateResource<ValueSet>(model);

            retVal.DateElement = new FhirDateTime(model.UpdatedTime ?? model.CreationTime);
            retVal.Url = model.Url;
            retVal.Identifier = new List<Identifier>()
            {
                new Identifier(FhirConstants.OidSystem, model.Oid)
            };
            retVal.Name = model.Mnemonic;
            retVal.Title = model.Name;

            if (model.LoadProperty(o => o.Composition).Any())
            {
                retVal.Compose = new ValueSet.ComposeComponent();
                retVal.Compose.Exclude = model.Composition.Where(c => c.Operation == ConceptSetCompositionOperation.Exclude).Select(o => new ValueSet.ConceptSetComponent()
                {
                    ValueSet = new String[] { o.LoadProperty(p => p.Target).Url }
                }).ToList();
                retVal.Compose.Include = model.Composition.Where(c => c.Operation == ConceptSetCompositionOperation.Include).Select(o => new ValueSet.ConceptSetComponent()
                {
                    ValueSet = new String[] { o.LoadProperty(p => p.Target).Url }
                }).ToList();
            }

            retVal.Expansion = new ValueSet.ExpansionComponent();
            var expansion = this.m_coneptRepository.ExpandConceptSet(model.Key.Value);
            retVal.Expansion.Contains = expansion.SelectMany(m => m.LoadProperty(p => p.ReferenceTerms)).ToArray().Select(fj => new ValueSet.ContainsComponent()
            {
                Code = fj.LoadProperty(p => p.ReferenceTerm)?.Mnemonic,
                System = fj.ReferenceTerm.LoadProperty(p => p.CodeSystem)?.Url,
                Display = fj.ReferenceTerm.LoadProperty(p => p.DisplayNames).FirstOrDefault()?.Name,
                Version = fj.ReferenceTerm.CodeSystem.VersionText
            }).Union(expansion.ToArray().Select(c=> new ValueSet.ContainsComponent()
            {
                System = FhirConstants.SanteDBConceptSystem,
                Code = c.Mnemonic,
                Display = c.LoadProperty(p => p.ConceptNames).FirstOrDefault()?.Name
            })).ToList();

            return retVal;
        }

        /// <inheritdoc/>
        protected override ConceptSet MapToModel(ValueSet resource)
        {
            throw new NotImplementedException();
        }
    }
}

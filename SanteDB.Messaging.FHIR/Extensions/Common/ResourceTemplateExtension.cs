using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Common
{
    /// <summary>
    /// Represents an extension which dictates the template attached to a resource
    /// </summary>
    public class ResourceTemplateExtension : IFhirExtensionHandler
    {

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ResourceTemplateExtension));
        private readonly ITemplateDefinitionRepositoryService m_templateRepository;

        /// <summary>
        /// Default CTOR
        /// </summary>
        /// <param name="templateRepository"></param>
        public ResourceTemplateExtension(ITemplateDefinitionRepositoryService templateRepository)
        {
            this.m_templateRepository = templateRepository;
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.Patient>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.Immunization>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.Encounter>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.Observation>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.Condition>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.AllergyIntolerance>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.AdverseEvent>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
            QueryRewriter.AddSearchParam<Hl7.Fhir.Model.MedicationAdministration>("data-template", "template.mnemonic", QueryParameterRewriteType.String);
        }

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/data-template");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => null;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is IHasTemplate iht && iht.TemplateKey.HasValue)
            {
                var template = this.m_templateRepository.Get(iht.TemplateKey.Value);
                yield return new Extension(this.Uri.ToString(), new FhirString(template.Mnemonic));

            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(fhirExtension.Value is FhirString fhs && modelObject is IHasTemplate iht)
            {
                var template = this.m_templateRepository.Find(o => o.Mnemonic == fhs.Value && o.ObsoletionTime == null).Select(o => o.Key).FirstOrDefault();
                if(template.HasValue)
                {
                    iht.TemplateKey = template.Value;
                    return true;
                }
                else
                {
                    throw new KeyNotFoundException(String.Format(ErrorMessages.REFERENCE_NOT_FOUND, template.Value));
                }
            }
            return false;
        }
    }
}

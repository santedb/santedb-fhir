using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Immunization
{
    /// <summary>
    /// Handles products on an immunization
    /// </summary>
    [DisplayName("Administered Substance")]
    [System.ComponentModel.Description("Provides a direct reference to the type of Material that was administered")]
    public class AdministeredSubstanceExtensionHandler : IFhirExtensionHandlerEx
    {
        private readonly FhirServiceConfigurationSection m_configuration;

        /// <summary>
        /// DI ctor
        /// </summary>
        public AdministeredSubstanceExtensionHandler(IConfigurationManager configurationManager)
        {
            this.m_configuration = configurationManager.GetSection<FhirServiceConfigurationSection>();
        }

        /// <inheritdoc/>
        public virtual Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/administered-substance");

        /// <inheritdoc/>
        public Uri ProfileUri => Uri;

        /// <inheritdoc/>
        public virtual ResourceType? AppliesTo => ResourceType.Immunization;

        /// <inhertidoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.Reference;

        /// <inhertidoc/>
        public bool IsModifier => true;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is SubstanceAdministration adm && 
                adm.LoadProperty(o=>o.Participations).Any(p=>p.ParticipationRoleKey == ActParticipationKeys.Product))
            {

                foreach (var prod in adm.Participations.Where(p => p.ParticipationRoleKey == ActParticipationKeys.Product)) {
                    yield return new Extension(Uri.ToString(), DataTypeConverter.CreateNonVersionedReference<Substance>(prod.LoadProperty(o => o.PlayerEntity)));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(modelObject is SubstanceAdministration sbadm && 
                fhirExtension.Value is ResourceReference rr)
            {
                var resolved = DataTypeConverter.ResolveEntity<Material>(rr, (Resource)fhirExtension.Annotation<Hl7.Fhir.Model.Immunization>() ?? fhirExtension.Annotation<Hl7.Fhir.Model.MedicationAdministration>());
                if (resolved == null || resolved.DeterminerConceptKey == DeterminerKeys.Described)
                {
                    
                    return false;
                }
                else if (!sbadm.LoadProperty(o => o.Participations).Any(r => r.ParticipationRoleKey == ActParticipationKeys.Product))
                {
                    sbadm.Participations.RemoveAll(o => o.ParticipationRoleKey == ActParticipationKeys.Product);
                    sbadm.Participations.Add(new ActParticipation(ActParticipationKeys.Product, resolved));
                    return true;
                }
            }
            return false;
        }
        
    }
}

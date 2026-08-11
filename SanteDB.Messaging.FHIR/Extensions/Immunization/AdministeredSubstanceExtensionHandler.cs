using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Immunization
{
    /// <summary>
    /// Handles products on an immunization
    /// </summary>
    public class AdministeredSubstanceExtensionHandler : IFhirExtensionHandler
    {
        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/administered-substance");

        /// <inheritdoc/>
        public Uri ProfileUri => this.Uri;

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Immunization;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is SubstanceAdministration adm && 
                adm.LoadProperty(o=>o.Participations).Any(p=>p.ParticipationRoleKey == ActParticipationKeys.Product))
            {

                foreach (var prod in adm.Participations.Where(p => p.ParticipationRoleKey == ActParticipationKeys.Product)) {
                    yield return new Extension(this.Uri.ToString(), DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Substance>(prod.LoadProperty(o => o.PlayerEntity)));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(modelObject is SubstanceAdministration sbadm && 
                fhirExtension.Value is ResourceReference rr)
            {
                var resolved = DataTypeConverter.ResolveEntity<Material>(rr, (Resource)fhirExtension.Annotation<Hl7.Fhir.Model.Immunization>() ?? (Resource)fhirExtension.Annotation<Hl7.Fhir.Model.MedicationAdministration>());
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

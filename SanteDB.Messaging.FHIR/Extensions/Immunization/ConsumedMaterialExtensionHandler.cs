using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
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
    /// Consumed materials handler
    /// </summary>
    public class ConsumedMaterialExtensionHandler : IFhirExtensionHandler
    {

        // consumed quantity
        private readonly string ConsumedQuantityExtensionUrl = $"{FhirConstants.SanteDBProfile}/extensions/consumed-material#consumed-quantity";

        /// <inheritdoc/>
        public virtual Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extensions/consumed-material");

        /// <inheritdoc/>
        public Uri ProfileUri => Uri;

        /// <inheritdoc/>
        public virtual ResourceType? AppliesTo => ResourceType.Immunization;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is SubstanceAdministration adm &&
                adm.LoadProperty(o => o.Participations).Any(p => p.ParticipationRoleKey == ActParticipationKeys.Consumable))
            {

                foreach (var prod in adm.Participations.Where(p => p.ParticipationRoleKey == ActParticipationKeys.Consumable))
                {
                    yield return new Extension(Uri.ToString(), DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Medication>(prod.LoadProperty(o => o.PlayerEntity)))
                    {
                        Extension = new List<Extension>()
                        {
                            new Extension(ConsumedQuantityExtensionUrl, new FhirDecimal(prod.Quantity))
                        }
                    };
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is SubstanceAdministration sbadm &&
                fhirExtension.Value is ResourceReference rr)
            {
                var resolved = DataTypeConverter.ResolveEntity<ManufacturedMaterial>(rr, (Resource)fhirExtension.Annotation<Hl7.Fhir.Model.Immunization>() ?? fhirExtension.Annotation<Hl7.Fhir.Model.MedicationAdministration>());
                if (resolved == null || resolved.DeterminerConceptKey == DeterminerKeys.Specific)
                {
                    return false;
                }

                var quantity = fhirExtension.Extension.Find(o => o.Url == ConsumedQuantityExtensionUrl)?.Value as FhirDecimal;

                var existing = sbadm.LoadProperty(o => o.Participations).FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.Consumable && o.PlayerEntityKey == resolved.Key);
                if(existing == null)
                {
                    sbadm.Participations.Add(new ActParticipation(ActParticipationKeys.Consumable, resolved)
                    {
                        Quantity = (int?)quantity?.Value
                    });
                }
                else
                {
                    existing.BatchOperation = Core.Model.DataTypes.BatchOperationType.Update;
                    existing.Quantity = (int?)quantity?.Value ?? existing.Quantity;
                }
                return true;
            }
            return false;
        }
    }
}

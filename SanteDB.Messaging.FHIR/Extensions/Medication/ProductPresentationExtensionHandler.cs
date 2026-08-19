using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Medication
{
    /// <summary>
    /// Extension handler for presentation
    /// </summary>
    public class ProductPresentationExtensionHandler : IFhirExtensionHandler
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ProductPresentationExtensionHandler));
        private readonly IRepositoryService<EntityRelationship> m_entityRelationshipService;

        /// <summary>
        /// DI Ctor
        /// </summary>
        public ProductPresentationExtensionHandler(IRepositoryService<EntityRelationship> entityRelationshipService)
        {
            this.m_entityRelationshipService = entityRelationshipService;
        }

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/Medication/productPresentation");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Medication;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if (modelObject is ManufacturedMaterial mmat)
            {
                if (mmat.DeterminerConceptKey == DeterminerKeys.Specific)
                {
                    mmat = this.m_entityRelationshipService.Find(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.Instance && o.TargetEntityKey == modelObject.Key).FirstOrDefault()?.LoadProperty(o => o.SourceEntity) as ManufacturedMaterial;
                }

                var hgRel = mmat.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.HasGenerialization);
                if (hgRel != null)
                {
                    var product = hgRel.LoadProperty(o => o.TargetEntity) as Material;
                    var qty = product.LoadProperty(o => o.QuantityConcept);
                    yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToQuantity((decimal)hgRel.Quantity, qty.Key, qty));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            // TODO: Implement this
            this.m_tracer.TraceWarning("Processing of presentations on inbound materials is not yet supported");
            return false; // We cannot parse these extensions
        }
    }
}

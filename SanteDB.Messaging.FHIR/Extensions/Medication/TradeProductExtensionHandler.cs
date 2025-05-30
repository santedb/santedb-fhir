using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Medication
{
    /// <summary>
    /// Trade product extension handler for those which are 
    /// </summary>
    public class TradeProductExtensionHandler : IFhirExtensionHandler
    {
        private readonly IRepositoryService<EntityRelationship> m_entityRelationshipService;

        /// <summary>
        /// DI Ctor
        /// </summary>
        public TradeProductExtensionHandler(IRepositoryService<EntityRelationship> entityRelationshipService)
        {
            this.m_entityRelationshipService = entityRelationshipService;
        }

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/Medication/productDefinition");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Medication;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is ManufacturedMaterial mmat && mmat.DeterminerConceptKey == DeterminerKeys.Specific)
            {
                var product = this.m_entityRelationshipService.Find(o=>o.RelationshipTypeKey == EntityRelationshipTypeKeys.Instance && o.TargetEntityKey == mmat.Key).FirstOrDefault();
                if (product != null)
                {
                    yield return new Extension(this.Uri.ToString(), DataTypeConverter.CreateNonVersionedReference<Hl7.Fhir.Model.Medication>(product.LoadProperty(o => o.SourceEntity)));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is ManufacturedMaterial mmat && fhirExtension.Value is ResourceReference rr)
            {
                var resolved = DataTypeConverter.ResolveEntity<ManufacturedMaterial>(rr, null);
                if (resolved != null)
                {
                    mmat.LoadProperty(o => o.Relationships).Add(new EntityRelationship(EntityRelationshipTypeKeys.Instance, modelObject.Key) { SourceEntityKey = resolved.Key });
                    return true;
                }
                else
                {
                    throw new KeyNotFoundException(rr.Url.ToString());
                }
            }
            return false;
        }
    }
}

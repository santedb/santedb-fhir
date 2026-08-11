using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Medication
{
    /// <summary>
    /// Trade product extension handler for those which are 
    /// </summary>
    public class SubstanceExtensionHandler : IFhirExtensionHandler
    {

        /// <summary>
        /// DI Ctor
        /// </summary>
        public SubstanceExtensionHandler()
        {
        }

        /// <inheritdoc/>
        public Uri Uri => new Uri($"{FhirConstants.SanteDBProfile}/extension/Medication/substanceDefinition");

        /// <inheritdoc/>
        public Uri ProfileUri => new Uri(FhirConstants.SanteDBProfile);

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Medication;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is ManufacturedMaterial mmat && mmat.DeterminerConceptKey == DeterminerKeys.DescribedQualified)
            {
                var sub = mmat.LoadProperty(o => o.Relationships).FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.HasGenerialization)?.LoadProperty(o => o.TargetEntity);
                if (sub != null)
                {
                    yield return new Extension(this.Uri.ToString(), DataTypeConverter.CreateNonVersionedReference<Substance>(sub));
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (modelObject is ManufacturedMaterial mmat && fhirExtension.Value is ResourceReference rr)
            {
                var resolved = DataTypeConverter.ResolveEntity<Material>(rr, null);
                
                if (resolved != null && !mmat.LoadProperty(o=>o.Relationships).Any(r=>r.RelationshipTypeKey == EntityRelationshipTypeKeys.HasGenerialization && r.TargetEntityKey != resolved.Key))
                {
                    mmat.LoadProperty(o => o.Relationships).RemoveAll(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.HasGenerialization);
                    mmat.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.HasGenerialization, modelObject.Key) { SourceEntityKey = resolved.Key });
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

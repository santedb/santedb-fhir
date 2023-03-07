/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-5-30
 */
using Hl7.Fhir.Model;
using SanteDB.Core;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Allergy / intolerance resource handler
    /// </summary>
    public class AllergyIntoleranceResourceHandler : RepositoryResourceHandlerBase<AllergyIntolerance, CodedObservation>
    {
        // Applicable type concepts
        private List<Guid> m_typeConcepts;

        /// <summary>
        /// Type concepts
        /// </summary>
        public AllergyIntoleranceResourceHandler(IRepositoryService<CodedObservation> repo, IRepositoryService<Concept> conceptRepo, ILocalizationService localizationService) : base(repo, localizationService)
        {
            this.m_typeConcepts = conceptRepo.Find(o => o.ConceptSets.Any(cs => cs.Mnemonic == "AllergyIntoleranceCode")).Select(o => o.Key.Value).ToList();
        }

        /// <summary>
        /// Can map this object
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public override bool CanMapObject(object instance) => instance is AllergyIntolerance ||
            instance is CodedObservation cobs && this.m_typeConcepts.Contains(cobs.TypeConceptKey.GetValueOrDefault());

        /// <summary>
        /// Map coded allergy intolerance resource to FHIR
        /// </summary>
		protected override AllergyIntolerance MapToFhir(CodedObservation model)
        {
            var retVal = DataTypeConverter.CreateResource<AllergyIntolerance>(model);

            retVal.Identifier = model.LoadCollection<ActIdentifier>(nameof(model.Identifiers)).Select(DataTypeConverter.ToFhirIdentifier).ToList();


            string status = null;
            if (model.StatusConceptKey == StatusKeys.Active)
            {
                status = "active";
            }
            else if (model.StatusConceptKey == StatusKeys.Completed)
            {
                status = "resolved";
            }
            else if (model.StatusConceptKey == StatusKeys.Inactive || model.StatusConceptKey == StatusKeys.Obsolete)
            {
                status = "inactive";
            }

            if (model.StatusConceptKey == StatusKeys.Nullified)
            {
                retVal.VerificationStatus = new CodeableConcept("http://hl7.org/fhir/ValueSet/allergyintolerance-verification", "entered-in-error");
            }
            else if (null != status)
            {
                retVal.ClinicalStatus = new CodeableConcept("http://hl7.org/fhir/ValueSet/allergyintolerance-clinical", status);
            }

            if (model.StartTime.HasValue || model.StopTime.HasValue)
            {
                retVal.Onset = new Period
                {
                    StartElement = model.StartTime.HasValue ? DataTypeConverter.ToFhirDateTime(model.StartTime) : null,
                    EndElement = model.StopTime.HasValue ? DataTypeConverter.ToFhirDateTime(model.StopTime) : null
                };
            }
            else
            {
                retVal.Onset = DataTypeConverter.ToFhirDateTime(model.ActTime);
            }

            retVal.RecordedDateElement = DataTypeConverter.ToFhirDateTime(model.CreationTime);

            var participations = model.LoadCollection<ActParticipation>(nameof(model.Participations));

            var recordtarget = participations.FirstOrDefault(p => p.ParticipationRoleKey == ActParticipationKeys.RecordTarget);

            if (null != recordtarget)
            {
                retVal.Patient = DataTypeConverter.CreateVersionedReference<Patient>(recordtarget.LoadProperty<Entity>(nameof(recordtarget.PlayerEntity)));
            }

            var asserter = participations.FirstOrDefault(p => p.ParticipationRoleKey == ActParticipationKeys.Authororiginator);

            if (null != asserter)
            {
                retVal.Asserter = DataTypeConverter.CreateVersionedReference<Practitioner>(asserter.LoadProperty<Entity>(nameof(asserter.PlayerEntity)));
            }

            if (model.TypeConceptKey == IntoleranceObservationTypeKeys.OtherIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Intolerance;
                retVal.CategoryElement.Clear();
            }
            else if (model.TypeConceptKey == IntoleranceObservationTypeKeys.DrugIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Allergy;
                retVal.Category = new AllergyIntolerance.AllergyIntoleranceCategory?[] { AllergyIntolerance.AllergyIntoleranceCategory.Medication };
            }
            else if (model.TypeConceptKey == IntoleranceObservationTypeKeys.DrugNonAllergyIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Intolerance;
                retVal.Category = new AllergyIntolerance.AllergyIntoleranceCategory?[] { AllergyIntolerance.AllergyIntoleranceCategory.Medication };
            }
            else if (model.TypeConceptKey == IntoleranceObservationTypeKeys.FoodIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Allergy;
                retVal.Category = new AllergyIntolerance.AllergyIntoleranceCategory?[] { AllergyIntolerance.AllergyIntoleranceCategory.Food };
            }
            else if (model.TypeConceptKey == IntoleranceObservationTypeKeys.FoodNonAllergyIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Intolerance;
                retVal.Category = new AllergyIntolerance.AllergyIntoleranceCategory?[] { AllergyIntolerance.AllergyIntoleranceCategory.Food };
            }
            else if (model.TypeConceptKey == IntoleranceObservationTypeKeys.EnvironmentalIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Allergy;
                retVal.Category = new AllergyIntolerance.AllergyIntoleranceCategory?[] { AllergyIntolerance.AllergyIntoleranceCategory.Environment };
            }
            else if (model.TypeConceptKey == IntoleranceObservationTypeKeys.EnvironmentalNonAllergyIntolerance)
            {
                retVal.Type = AllergyIntolerance.AllergyIntoleranceType.Intolerance;
                retVal.Category = new AllergyIntolerance.AllergyIntoleranceCategory?[] { AllergyIntolerance.AllergyIntoleranceCategory.Environment };
            }
            else
            {
                //TODO: Throw exception here, cannot translate.
            }

            var actrelationshipservice = ApplicationServiceContext.Current.GetService<IDataPersistenceService<ActRelationship>>();

            var criticality = actrelationshipservice.Query(actr => actr.SourceEntityKey == model.Key && actr.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && actr.TargetAct.TypeConceptKey == ObservationTypeKeys.Severity, AuthenticationContext.Current.Principal);

            if (null == criticality)
            {
                var contextAct = actrelationshipservice.Query(o => o.TargetActKey == model.Key, AuthenticationContext.Current.Principal).FirstOrDefault();

                if (contextAct != null)
                {
                    criticality = actrelationshipservice.Query(o => o.SourceEntityKey == contextAct.SourceEntityKey && o.RelationshipTypeKey == ActRelationshipTypeKeys.HasComponent && o.TargetAct.TypeConceptKey == ObservationTypeKeys.Severity, AuthenticationContext.Current.Principal);
                }
            }

            if (null != criticality)
            {
                var cc = DataTypeConverter.ToFhirCodeableConcept((criticality as CodedObservation).ValueKey, "http://hl7.org/fhir/ValueSet/allergy-intolerance-criticality");
                retVal.CriticalityElement = new Code<AllergyIntolerance.AllergyIntoleranceCriticality> { ObjectValue = cc.Text };
            }

            retVal.Code = DataTypeConverter.ToFhirCodeableConcept(model.ValueKey);

            return retVal;
        }

        /// <summary>
        /// Map allergy intolerance from FHIR to a coded observation
        /// </summary>
		protected override CodedObservation MapToModel(AllergyIntolerance resource)
        {
            var retVal = new CodedObservation
            {
                Relationships = new List<ActRelationship>(),
                Participations = new List<ActParticipation>(),
                Identifiers = resource.Identifier.Select(DataTypeConverter.ToActIdentifier).ToList()
            };

            switch (resource.ClinicalStatus.TypeName)
            {
                case "active":
                    retVal.StatusConceptKey = StatusKeys.Active;
                    break;
                case "resolved":
                    retVal.StatusConceptKey = StatusKeys.Completed;
                    break;
                case "inactive":
                    retVal.StatusConceptKey = StatusKeys.Inactive;
                    break;
            }

            if (resource.VerificationStatus.TypeName == "entered-in-error")
            {
                retVal.StatusConceptKey = StatusKeys.Nullified;
            }

            if (resource.Onset is Period onset)
            {
                retVal.StartTime = DataTypeConverter.ToDateTimeOffset(onset.StartElement);
                retVal.StopTime = DataTypeConverter.ToDateTimeOffset(onset.EndElement);
            }
            else if (resource.Onset is FhirDateTime acttime)
            {
                retVal.ActTime = DataTypeConverter.ToDateTimeOffset(acttime);
            }

            if (null != resource.Patient)
            {
                retVal.Participations.Add(resource.Patient.Reference.StartsWith("urn:uuid:") ?
                    new ActParticipation(ActParticipationKeys.RecordTarget, Guid.Parse(resource.Patient.Reference.Substring(9))) :
                    new ActParticipation(ActParticipationKeys.RecordTarget, DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Patient, resource))
                    );
            }

            if (null != resource.Asserter)
            {
                retVal.Participations.Add(resource.Asserter.Reference.StartsWith("urn:uuid:") ?
                    new ActParticipation(ActParticipationKeys.Authororiginator, Guid.Parse(resource.Asserter.Reference.Substring(9))) :
                    new ActParticipation(ActParticipationKeys.Authororiginator, DataTypeConverter.ResolveEntity<Core.Model.Roles.Provider>(resource.Asserter, resource))
                    );
            }
            else if (null != resource.Recorder)
            {
                retVal.Participations.Add(resource.Asserter.Reference.StartsWith("urn:uuid:") ?
                    new ActParticipation(ActParticipationKeys.Authororiginator, Guid.Parse(resource.Recorder.Reference.Substring(9))) :
                    new ActParticipation(ActParticipationKeys.Authororiginator, DataTypeConverter.ResolveEntity<Core.Model.Roles.Provider>(resource.Recorder, resource))
                    );
            }

            if (null != resource.RecordedDateElement)
            {
                retVal.CreationTime = DataTypeConverter.ToDateTimeOffset(resource.RecordedDateElement).GetValueOrDefault();
            }

            //HACK: Need to rewrite this logic to be extensible.

            bool isintolerance = resource.Type == AllergyIntolerance.AllergyIntoleranceType.Intolerance;

            switch(resource.Category.Where(c=> null != c).FirstOrDefault())
            {
                case AllergyIntolerance.AllergyIntoleranceCategory.Biologic:
                    retVal.TypeConceptKey = isintolerance ? IntoleranceObservationTypeKeys.OtherIntolerance : (Guid?)null;
                    break;
                case AllergyIntolerance.AllergyIntoleranceCategory.Medication:
                    retVal.TypeConceptKey = isintolerance ? IntoleranceObservationTypeKeys.DrugNonAllergyIntolerance : IntoleranceObservationTypeKeys.DrugIntolerance;
                    break;
                case AllergyIntolerance.AllergyIntoleranceCategory.Food:
                    retVal.TypeConceptKey = isintolerance ? IntoleranceObservationTypeKeys.FoodNonAllergyIntolerance : IntoleranceObservationTypeKeys.FoodIntolerance;
                    break;
                case AllergyIntolerance.AllergyIntoleranceCategory.Environment:
                    retVal.TypeConceptKey = isintolerance ? IntoleranceObservationTypeKeys.EnvironmentalNonAllergyIntolerance : IntoleranceObservationTypeKeys.EnvironmentalIntolerance;
                    break;
                default:
                    break;
            }

            if (resource.Criticality != null)
            {
                var criticalityTarget = new CodedObservation
                {
                    Value = DataTypeConverter.ToConcept(resource.CriticalityElement.TypeName, "http://hl7.org/fhir/ValueSet/allergy-intolerance-criticality"),
                    TypeConceptKey = ObservationTypeKeys.Severity
                };
                retVal.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.HasComponent, criticalityTarget));
            }

            retVal.Value = DataTypeConverter.ToConcept(resource.Code);

            return retVal;
        }

        /// <summary>
        /// Query which filters only allergies and intolerances
        /// </summary>
        protected override IQueryResultSet<CodedObservation> Query(Expression<Func<CodedObservation, bool>> query)
        {
            var anyRef = base.CreateConceptSetFilter(ConceptSetKeys.AllergyIntoleranceTypes, query.Parameters[0]);
            query = System.Linq.Expressions.Expression.Lambda<Func<CodedObservation, bool>>(System.Linq.Expressions.Expression.AndAlso(query.Body, anyRef), query.Parameters);
            return base.Query(query);
        }

        /// <summary>
        /// Get interactions
        /// </summary>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new TypeRestfulInteraction[]
            {
            }.Select(o => new ResourceInteractionComponent() { Code = o });
        }

        /// <summary>
        /// Get included resources
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(CodedObservation resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get reverse included resources
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(CodedObservation resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(m_localizationService.GetString("error.type.NotImplementedException"));
        }
    }
}
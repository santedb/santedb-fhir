/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using Hl7.Fhir.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hl7.Fhir.Model.CapabilityStatement;
using Organization = SanteDB.Core.Model.Entities.Organization;
using Patient = Hl7.Fhir.Model.Patient;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Encounter resource handler for loading and disclosing of patient encounters
    /// </summary>
    public class EncounterResourceHandler : RepositoryResourceHandlerBase<Encounter, PatientEncounter>
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(EncounterResourceHandler));

        /// <summary>
        /// Create new resource handler
        /// </summary>
        public EncounterResourceHandler(IRepositoryService<PatientEncounter> repo, ILocalizationService localizationService) : base(repo, localizationService)
        {
        }

        /// <summary>
        /// Get includes
        /// </summary>
        protected override IEnumerable<Resource> GetIncludes(PatientEncounter resource, IEnumerable<IncludeInstruction> includePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Get the interactions supported
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new List<TypeRestfulInteraction>
            {
                TypeRestfulInteraction.SearchType,
                TypeRestfulInteraction.Read,
                TypeRestfulInteraction.Vread,
                TypeRestfulInteraction.HistoryInstance,
                TypeRestfulInteraction.Create,
                TypeRestfulInteraction.Delete,
                TypeRestfulInteraction.Update,
            }.Select(o => new ResourceInteractionComponent
            { Code = o });
        }

        /// <summary>
        /// Get the reverse includes
        /// </summary>
        protected override IEnumerable<Resource> GetReverseIncludes(PatientEncounter resource, IEnumerable<IncludeInstruction> reverseIncludePaths)
        {
            throw new NotImplementedException(this.m_localizationService.GetString("error.type.NotImplementedException"));
        }

        /// <summary>
        /// Map the specified patient encounter to a FHIR based encounter
        /// </summary>
        protected override Encounter MapToFhir(PatientEncounter model)
        {
            var retVal = DataTypeConverter.CreateResource<Encounter>(model);

            // Map the identifier
            retVal.Identifier = model.LoadCollection<ActIdentifier>("Identifiers").Select(o => DataTypeConverter.ToFhirIdentifier(o)).ToList();

            // Map status keys
            switch (model.StatusConceptKey.ToString().ToUpper())
            {
                case StatusKeyStrings.Active:
                case StatusKeyStrings.New:
                    switch (model.MoodConceptKey.ToString().ToUpper())
                    {
                        case MoodConceptKeyStrings.Eventoccurrence:
                        case MoodConceptKeyStrings.Request:
                            retVal.Status = Encounter.EncounterStatus.InProgress;
                            break;

                        case MoodConceptKeyStrings.Intent:
                        case MoodConceptKeyStrings.Promise:
                            retVal.Status = Encounter.EncounterStatus.Planned;
                            break;
                    }

                    break;

                case StatusKeyStrings.Cancelled:
                    retVal.Status = Encounter.EncounterStatus.Cancelled;
                    break;

                case StatusKeyStrings.Nullified:
                    retVal.Status = Encounter.EncounterStatus.EnteredInError;
                    break;

                case StatusKeyStrings.Obsolete:
                    retVal.Status = Encounter.EncounterStatus.Unknown;
                    break;

                case StatusKeyStrings.Completed:
                    retVal.Status = Encounter.EncounterStatus.Finished;
                    break;
            }

            if (model.StartTime.HasValue || model.StopTime.HasValue)
            {
                retVal.Period = DataTypeConverter.ToPeriod(model.StartTime, model.StopTime);
            }
            else
            {
                retVal.Period = DataTypeConverter.ToPeriod(model.ActTime, model.ActTime);
            }

            retVal.ReasonCode = new List<CodeableConcept>
            {
                DataTypeConverter.ToFhirCodeableConcept(model.ReasonConceptKey)
            };

            retVal.Class = DataTypeConverter.ToFhirCodeableConcept(model.TypeConceptKey).GetCoding();

            // Map associated
            var associated = model.LoadCollection<ActParticipation>("Participations").ToArray();

            // Subject of encounter
            retVal.Subject = DataTypeConverter.CreateNonVersionedReference<Patient>(associated.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKeys.RecordTarget)?.LoadProperty<Entity>("PlayerEntity"));

            // Locations
            retVal.Location = associated.Where(o => o.LoadProperty<Entity>("PlayerEntity") is Place).Select(o => new Encounter.LocationComponent
            {
                Period = DataTypeConverter.ToPeriod(model.CreationTime, null),
                Location = DataTypeConverter.CreateVersionedReference<Location>(o.PlayerEntity)
            }).ToList();

            // Service provider
            var cst = associated.FirstOrDefault(o => o.LoadProperty<Entity>("PlayerEntity") is Organization && o.ParticipationRoleKey == ActParticipationKeys.Custodian);

            if (cst != null)
            {
                retVal.ServiceProvider = DataTypeConverter.CreateVersionedReference<Hl7.Fhir.Model.Organization>(cst.PlayerEntity);
            }

            // Participants
            retVal.Participant = associated.Where(o => o.LoadProperty<Entity>("PlayerEntity") is Provider || o.LoadProperty<Entity>("PlayerEntity") is UserEntity).Select(o => new Encounter.ParticipantComponent
            {
                Type = new List<CodeableConcept>
                {
                    DataTypeConverter.ToFhirCodeableConcept(o.ParticipationRoleKey)
                },
                Individual = DataTypeConverter.CreateVersionedReference<Practitioner>(o.PlayerEntity)
            }).ToList();

            return retVal;
        }

        /// <summary>
        /// Map to model the encounter
        /// </summary>
        protected override PatientEncounter MapToModel(Encounter resource)
        {
            var status = resource.Status;

            var retVal = new PatientEncounter
            {
                TypeConcept = DataTypeConverter.ToConcept(resource.Class, "http://santedb.org/conceptset/v3-ActEncounterCode"),
                // TODO: Extensions
                Extensions = resource.Extension.Select(DataTypeConverter.ToActExtension).OfType<ActExtension>().ToList(),
                Identifiers = resource.Identifier.Select(DataTypeConverter.ToActIdentifier).ToList(),
                Key = Guid.NewGuid(),
                StatusConceptKey = status == Encounter.EncounterStatus.Finished ? StatusKeys.Completed :
                    status == Encounter.EncounterStatus.Cancelled ? StatusKeys.Cancelled :
                    status == Encounter.EncounterStatus.InProgress || status == Encounter.EncounterStatus.Arrived ? StatusKeys.Active :
                    status == Encounter.EncounterStatus.Planned ? StatusKeys.New :
                    status == Encounter.EncounterStatus.EnteredInError ? StatusKeys.Nullified : StatusKeys.Inactive,
                MoodConceptKey = status == Encounter.EncounterStatus.Planned ? ActMoodKeys.Intent : ActMoodKeys.Eventoccurrence,
                ReasonConcept = DataTypeConverter.ToConcept(resource.ReasonCode.FirstOrDefault()),
                StartTime = DataTypeConverter.ToDateTimeOffset(resource.Period.Start),
                StopTime = DataTypeConverter.ToDateTimeOffset(resource.Period.End),
                Participations = new List<ActParticipation>()
            };



            if (!Guid.TryParse(resource.Id, out var key))
            {
                key = Guid.NewGuid();
            }

            retVal.Key = key;

            // Attempt to resolve relationships
            if (resource.Subject != null)
            {
                // if the subject is a UUID then add the record target key
                // otherwise attempt to resolve the reference
                var target = DataTypeConverter.ResolveEntity<Core.Model.Roles.Patient>(resource.Subject, resource);
                retVal.Participations.Add(new ActParticipation(ActParticipationKeys.RecordTarget, target));
            }

            // Attempt to resolve organization
            if (resource.ServiceProvider != null)
            {
                var target = DataTypeConverter.ResolveEntity<Organization>(resource.ServiceProvider, resource);
                retVal.Participations.Add(new ActParticipation(ActParticipationKeys.Custodian, target));
            }

            // TODO : Other Participations
            return retVal;
        }
    }
}
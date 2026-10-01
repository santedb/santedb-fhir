using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Exceptions;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Recorded sex or gender extension - see: https://hl7.org/fhir/extensions/StructureDefinition-individual-recordedSexOrGender.html
    /// </summary>
    public class RecordedSexOrGenderExtension : IFhirExtensionHandlerEx
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(RecordedSexOrGenderExtension));
        private readonly IRepositoryService<CodedObservation> m_observationRepository;
        private readonly Guid[] m_observationTypeCodes;
        private readonly FhirServiceConfigurationSection m_configuration;

        public RecordedSexOrGenderExtension(
            IRepositoryService<CodedObservation> observationRepository,
            IConceptRepositoryService conceptRepository,
            IConfigurationManager configurationManager)
        {
            this.m_observationRepository = observationRepository;
            this.m_observationTypeCodes = conceptRepository.Find(o => o.ConceptSets.Any(c => c.Mnemonic == "RecordedSexOrGenderType")).Select(o => o.Key.Value).ToArray();
            this.m_configuration = configurationManager.GetSection<FhirServiceConfigurationSection>();
        }

        /// <inheritdoc/>
        public FHIRAllTypes ValueType => FHIRAllTypes.Extension;

        /// <inheritdoc/>
        public bool IsModifier => false;

        /// <inheritdoc/>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/individual-recordedSexOrGender");

        public Uri ProfileUri => this.Uri;

        /// <inheritdoc/>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <inheritdoc/>
        public IEnumerable<Extension> Construct(IAnnotatedResource modelObject)
        {
            if(modelObject is Core.Model.Roles.Patient pat)
            {
                // Attempt to locate the observed gender concept 
                var observedGenderConcept = this.m_observationRepository.Find(o => this.m_observationTypeCodes.Contains(o.TypeConceptKey.Value) && StatusKeys.ActiveStates.Contains(o.StatusConceptKey.Value) &&
                    o.Participations.Where(p => p.ParticipationRoleKey == ActParticipationKeys.RecordTarget).Any(p => p.PlayerEntityKey == pat.Key)).OrderByDescending(o=>o.ActTime).FirstOrDefault();
                if(observedGenderConcept != null)
                {
                    // Create the necessary extension composition
                    yield return new Extension(this.Uri.ToString(), null)
                    {
                        Extension = new List<Extension>()
                        {
                            new Extension("value", DataTypeConverter.ToFhirCodeableConcept(observedGenderConcept.ValueKey, FhirConstants.CodeSystem_AdministrativeGender)),
                            new Extension("type", DataTypeConverter.ToFhirCodeableConcept(observedGenderConcept.TypeConceptKey, FhirConstants.CodeSystem_Loinc))
                        }
                    };
                }
            }
        }

        /// <inheritdoc/>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if(modelObject is Core.Model.Roles.Patient pat && 
                fhirExtension.Value == null &&
                fhirExtension.Extension.Any())
            {
                var typeExtension = fhirExtension.Extension.FirstOrDefault(o => o.Url == "type");
                var valueExtension = fhirExtension.Extension.FirstOrDefault(o => o.Url == "value");

                if(typeExtension?.Value is CodeableConcept typeCc && valueExtension?.Value is CodeableConcept valueCc)
                {
                    var typeKey = DataTypeConverter.ToConcept(typeCc)?.Key;
                    var valueKey = DataTypeConverter.ToConcept(valueCc)?.Key;

                    // Locate existing 
                    var observedGenderConcept = this.m_observationRepository.Find(o => o.TypeConceptKey == typeKey && StatusKeys.ActiveStates.Contains(o.StatusConceptKey.Value) &&
                            o.Participations.Where(p => p.ParticipationRoleKey == ActParticipationKeys.RecordTarget).Any(p => p.PlayerEntityKey == pat.Key)).OrderByDescending(o => o.ActTime).FirstOrDefault();

                    var newGenderConcept = new CodedObservation()
                    {
                        TypeConceptKey = typeKey,
                        MoodConceptKey = ActMoodKeys.Eventoccurrence,
                        StatusConceptKey = StatusKeys.Completed,
                        ValueKey = valueKey,
                        ActTime = DateTimeOffset.Now,
                        Participations = new List<ActParticipation>()
                        {
                            new ActParticipation(ActParticipationKeys.RecordTarget, pat.Key)
                        },
                        Relationships = observedGenderConcept != null ? new List<ActRelationship>() { 
                            new ActRelationship(ActRelationshipTypeKeys.Replaces, observedGenderConcept.Key)
                        } : null,
                        BatchOperation = observedGenderConcept == null || observedGenderConcept.ValueKey != valueKey ?
                            Core.Model.DataTypes.BatchOperationType.InsertOrUpdate :
                            Core.Model.DataTypes.BatchOperationType.Ignore
                    };

                    if(newGenderConcept.BatchOperation != Core.Model.DataTypes.BatchOperationType.Ignore)
                    {
                        var bundle = fhirExtension.Annotation<Hl7.Fhir.Model.Patient>()?.Annotation<SanteDB.Core.Model.Collection.Bundle>();
                        if (bundle != null)
                        {
                            bundle.Add(newGenderConcept);
                        }
                        else
                        {
                            pat.Participations = pat.Participations ?? new List<ActParticipation>();
                            pat.Participations.Add(new ActParticipation(ActParticipationKeys.RecordTarget, pat.Key)
                            {
                                Act = newGenderConcept
                            });
                        }
                    }
                    return true;
                }
                else if(this.m_configuration.StrictProcessing)
                {
                    throw new FhirException(System.Net.HttpStatusCode.BadRequest, OperationOutcome.IssueType.Incomplete, "Must carry a type and value extension");
                }
                else
                {
                    this.m_tracer.TraceWarning("Cannot set recorded sex - extension must carry a type and a value");
                }

            }
            return false;
        }
    }
}

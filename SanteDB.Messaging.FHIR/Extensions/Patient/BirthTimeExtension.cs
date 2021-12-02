﻿using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using SanteDB.Core.Model;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Messaging.FHIR.Util;
using Person = SanteDB.Core.Model.Entities.Person;

namespace SanteDB.Messaging.FHIR.Extensions.Patient
{
    /// <summary>
    /// Birth-time extension
    /// </summary>
    public class BirthTimeExtension : IFhirExtensionHandler
    {
        /// <summary>
        /// Gets the resource type this applies to
        /// </summary>
        public ResourceType? AppliesTo => ResourceType.Patient;

        /// <summary>
        /// Gets the profile definition
        /// </summary>
        public Uri ProfileUri => this.Uri;

        /// <summary>
        /// Gets the URI of this extension
        /// </summary>
        public Uri Uri => new Uri("http://hl7.org/fhir/StructureDefinition/patient-birthTime");

        /// <summary>
        /// Construct the extentsion
        /// </summary>
        public IEnumerable<Extension> Construct(IIdentifiedEntity modelObject)
        {
            if (modelObject is Person person && person.DateOfBirthPrecision > DatePrecision.Day)
            {
                yield return new Extension(this.Uri.ToString(), DataTypeConverter.ToFhirDateTime(person.DateOfBirth));
            }
        }

        /// <summary>
        /// Parse the extension
        /// </summary>
        public bool Parse(Extension fhirExtension, IdentifiedData modelObject)
        {
            if (fhirExtension.Value is FhirDateTime dateTime && modelObject is Person person)
            {
                person.DateOfBirth = DataTypeConverter.ToDateTimeOffset(dateTime)?.Date;
                person.DateOfBirthPrecision = DatePrecision.Full;
                return true;
            }

            return false;
        }
    }
}
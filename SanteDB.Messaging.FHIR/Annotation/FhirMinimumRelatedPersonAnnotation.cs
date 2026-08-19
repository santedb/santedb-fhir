using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Annotation
{
    /// <summary>
    /// HACK: This whole annotation exists so that when we convey a Patient->RelatedPerson->Patient we
    /// don't emit too much information on the RelatedPerson
    /// </summary>
    internal struct FhirMinimumRelatedPersonAnnotation
    {
    }
}

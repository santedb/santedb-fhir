using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Messaging.FHIR.Annotation
{
    internal class FhirAlreadyProcessedAnnotation
    {

        public FhirAlreadyProcessedAnnotation(IdentifiedData processedResource)
        {
            this.ProcessedResource = processedResource;
        }

        public IdentifiedData ProcessedResource { get; private set; }
    }
}

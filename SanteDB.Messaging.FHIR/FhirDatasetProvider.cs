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
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using SanteDB.Core.Data.Initialization;
using SanteDB.Core.Diagnostics;
using SanteDB.Messaging.FHIR.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// Scans the configured directory on application startup to import or seed data into SanteDB from FHIR
    /// </summary>
    /// <remarks>
    /// <para>This service is a <see cref="IDatasetProvider"/> which translates FHIR bundles into dataset</para>
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class FhirDatasetProvider : IDatasetProvider
    {
        // Trace source
        private readonly Tracer m_traceSource = new Tracer(FhirConstants.TraceSourceName);

        /// <summary>
        /// Get all datasets converted from FHIR
        /// </summary>
        public IEnumerable<Dataset> GetDatasets()
        {
            var dataDirectory = Path.Combine(Path.GetDirectoryName(typeof(FhirDatasetProvider).Assembly.Location), "data", "fhir");
            if (Directory.Exists(dataDirectory))
            {
                var fhirXmlParser = new FhirXmlParser();
                var fhirJsonParser = new FhirJsonParser();
                foreach (var file in Directory.GetFiles(dataDirectory, "*.json").Union(Directory.GetFiles(dataDirectory, "*.xml")))
                {
                    Resource fhirResource = null;
                    using (var fs = File.OpenText(file))
                    {
                        switch (Path.GetExtension(file).ToLowerInvariant())
                        {
                            case ".json":
                                using (var jr = new JsonTextReader(fs))
                                {
                                    fhirResource = fhirJsonParser.Parse(jr) as Resource;
                                }
                                break;

                            case ".xml":
                                using (var xr = XmlReader.Create(fs))
                                {
                                    fhirResource = fhirXmlParser.Parse(xr) as Resource;
                                }
                                break;
                        }
                    }

                    // No FHIR resource
                    if (fhirResource == null)
                    {
                        throw new InvalidOperationException($"Could not parse a FHIR resource from {file}");
                    }

                    // Process the resource
                    if (!fhirResource.TryDeriveResourceType(out ResourceType rt))
                    {
                        throw new InvalidOperationException($"FHIR API doesn't support {fhirResource.TypeName}");
                    }
                    var handler = FhirResourceHandlerUtil.GetResourceHandler(rt);
                    if (handler is IFhirResourceMapper mapper)
                    {
                        var resource = mapper.MapToModel(fhirResource);
                        yield return new Dataset(fhirResource.Id)
                        {
                            Action = new List<DataInstallAction>()
                            {
                                new  DataUpdate()
                                {
                                    Element = resource,
                                    InsertIfNotExists = true
                                }
                            }
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"This instance of SanteDB does not support {rt}");
                    }
                }
            }

        }

    }
}
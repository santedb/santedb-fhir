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
 * Date: 2021-10-29
 */
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Newtonsoft.Json;
using RestSrvr;
using RestSrvr.Message;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Configuration;
using SanteDB.Messaging.FHIR.Exceptions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using Formatting = Newtonsoft.Json.Formatting;

namespace SanteDB.Messaging.FHIR.Rest.Serialization
{
    /// <summary>
    /// Represents a dispatch message formatter which uses the JSON.NET serialization
    /// </summary>
    /// <remarks>This serialization is used because the SanteDB FHIR resources have extra features not contained in the pure HL7 API provided by HL7 International (such as operators to/from primitiives, generation of text, etc.). This
    /// dispatch formatter is responsible for the serialization and de-serialization of FHIR objects to/from JSON and XML using the SanteDB classes for FHIR resources.</remarks>
    [ExcludeFromCodeCoverage]
    public class FhirMessageDispatchFormatter : IDispatchMessageFormatter
    {
        // Configuration for the service
        private readonly FhirServiceConfigurationSection m_configuration;

        // Trace source
        private readonly Tracer m_traceSource = new Tracer(FhirConstants.TraceSourceName);

        // Default settings
        private readonly ParserSettings m_settings = new ParserSettings
        {
            AcceptUnknownMembers = false,
            AllowUnrecognizedEnums = true,
            DisallowXsiAttributesOnRoot = false,
            PermissiveParsing = true
        };

        /// <summary>
        /// Creates a new instance of the FHIR message dispatch formatter
        /// </summary>
        public FhirMessageDispatchFormatter()
        {
            this.m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<FhirServiceConfigurationSection>();
        }

        /// <summary>
        /// Deserialize the request
        /// </summary>
        public void DeserializeRequest(EndpointOperation operation, RestRequestMessage request, object[] parameters)
        {
            try
            {
                var httpRequest = RestOperationContext.Current.IncomingRequest;
                var contentType = httpRequest.Headers["Content-Type"];

                for (var pNumber = 0; pNumber < parameters.Length; pNumber++)
                {
                    var parm = operation.Description.InvokeMethod.GetParameters()[pNumber];

                    // Simple parameter
                    if (parameters[pNumber] != null)
                    {
                        continue;
                    }

                    // Use XML Serializer
                    if (contentType?.StartsWith("application/fhir+xml") == true)
                    {
                        var parser = new FhirXmlParser(this.m_settings);
                        using (var xr = XmlReader.Create(request.Body))
                        {
                            parameters[pNumber] = parser.Parse(xr);
                        }
                    }
                    // Use JSON Serializer
                    else if (contentType?.StartsWith("application/fhir+json") == true)
                    {
                        var parser = new FhirJsonParser(this.m_settings);
                        using (var sr = new StreamReader(request.Body))
                        using (var jr = new JsonTextReader(sr))
                        {
                            parameters[pNumber] = parser.Parse(jr);
                        }
                    }
                    else if (contentType != null) // TODO: Binaries
                    {
                        throw new InvalidOperationException("Invalid request format");
                    }
                }
            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(EventLevel.Error, e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Serialize the reply
        /// </summary>
        public void SerializeResponse(RestResponseMessage responseMessage, object[] parameters, object result)
        {
            try
            {
                // Outbound control
                var httpRequest = RestOperationContext.Current.IncomingRequest;
                string accepts = httpRequest.Headers["Accept"],
                    contentType = httpRequest.Headers["Content-Type"],
                    formatParm = httpRequest.QueryString["_format"];

                var isOutputPretty = httpRequest.QueryString["_pretty"] == "true";

                SummaryType? summaryType = SummaryType.False;
                if (httpRequest.QueryString["_summary"] != null)
                {
                    summaryType = EnumUtility.ParseLiteral<SummaryType>(httpRequest.QueryString["_summary"], true);
                }

                if (accepts == "*/*") // Any = null
                {
                    accepts = null;
                }

                contentType = accepts ?? contentType ?? formatParm;

                // No specified content type
                if (String.IsNullOrEmpty(contentType))
                {
                    contentType = this.m_configuration.DefaultResponseFormat == FhirResponseFormatConfiguration.Json ? "application/fhir+json" : "application/fhir+xml";
                }

                var charset = ContentType.GetCharSetFromHeaderValue(contentType);
                var format = ContentType.GetMediaTypeFromHeaderValue(contentType);

                if (result is Base baseObject)
                {
                    var ms = new MemoryStream();
                    // The request was in JSON or the accept is JSON
                    switch (format)
                    {
                        case "application/fhir+xml":
                            using (var xw = XmlWriter.Create(ms, new XmlWriterSettings
                            {
                                Encoding = new UTF8Encoding(false),
                                Indent = isOutputPretty
                            }))
                            {
                                new FhirXmlSerializer().Serialize(baseObject, xw, summaryType.Value);
                            }

                            break;

                        case "application/fhir+json":
                            using (var sw = new StreamWriter(ms, new UTF8Encoding(false), 1024, true))
                            using (var jw = new JsonTextWriter(sw)
                            {
                                Formatting = isOutputPretty ? Formatting.Indented : Formatting.None,
                                DateFormatHandling = DateFormatHandling.IsoDateFormat
                            })
                            {
                                new FhirJsonSerializer(new SerializerSettings
                                {
                                    Pretty = isOutputPretty
                                }).Serialize(baseObject, jw);
                            }

                            break;

                        default:
                            throw new FhirException((HttpStatusCode) 406, OperationOutcome.IssueType.NotSupported, $"{contentType} not supported");
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    responseMessage.Body = ms;
                }
                else if (result == null)
                {
                    responseMessage.StatusCode = 204; // no content
                }
                else
                {
                    throw new InvalidOperationException("FHIR return values must inherit from Base");
                }

                RestOperationContext.Current.OutgoingResponse.ContentType = contentType;
                RestOperationContext.Current.OutgoingResponse.AppendHeader("X-PoweredBy", string.Format("{0} v{1} ({2})", Assembly.GetEntryAssembly().GetName().Name, Assembly.GetEntryAssembly().GetName().Version, Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion));
                RestOperationContext.Current.OutgoingResponse.AppendHeader("X-GeneratedOn", DateTime.Now.ToString("o"));
            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(EventLevel.Error, e.ToString());
                throw;
            }
        }
    }
}
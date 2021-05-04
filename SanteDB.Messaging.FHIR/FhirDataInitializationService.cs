﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Handlers;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace SanteDB.Messaging.FHIR
{
    /// <summary>
    /// FHIR based data initialization service
    /// </summary>
    public class FhirDataInitializationService : IDaemonService, IReportProgressChanged
    {

        // Trace source
        private Tracer m_traceSource = new Tracer(FhirConstants.TraceSourceName);

        /// <summary>
        /// True if the service is running
        /// </summary>
        public bool IsRunning => false;

        /// <summary>
        /// Name of the service
        /// </summary>
        public string ServiceName => "FHIR Based DataSet Initialization Service";

        /// <summary>
        /// Fired when progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Service has started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Install dataset using FHIR services
        /// </summary>
        public void InstallDataset(object sender, EventArgs e)
        {

            using (AuthenticationContext.EnterSystemContext())
            {
                // Data directory
                var dataDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "data", "fhir");
                var fhirXmlParser = new FhirXmlParser();
                var fhirJsonParser = new FhirJsonParser();

                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                this.m_traceSource.TraceInfo("Scanning Directory {0} for FHIR objects", dataDirectory);

                // Process
                foreach (var f in Directory.GetFiles(dataDirectory, "*.*"))
                {
                    try
                    {
                        Resource fhirResource = null;

                        switch (Path.GetExtension(f).ToLowerInvariant())
                        {
                            case ".json":
                                using (var fs = File.OpenRead(f))
                                using (var tr = new StreamReader(fs))
                                using (var jr = new JsonTextReader(tr))
                                {
                                    fhirResource = fhirJsonParser.Parse(jr) as Resource;
                                }
                                break;
                            case ".xml":
                                using (var fs = File.OpenRead(f))
                                using (var xr = XmlReader.Create(fs))
                                {
                                    fhirResource = fhirXmlParser.Parse(xr) as Resource;
                                }
                                break;
                            case ".completed":
                            case ".response":
                                continue; // skip file
                        }

                        // No FHIR resource
                        if (fhirResource == null)
                        {
                            throw new InvalidOperationException($"Could not parse a FHIR resource from {f}");
                        }

                        // Process the resource
                        var handler = FhirResourceHandlerUtil.GetResourceHandler(fhirResource.ResourceType);
                        if (handler == null)
                        {
                            throw new InvalidOperationException($"This instance of SanteMPI does not support {fhirResource.ResourceType}");
                        }

                        // Handle the resource
                        Resource fhirResult = null;
                        try
                        {
                            fhirResult = handler.Create(fhirResource, TransactionMode.Commit);
                            File.Move(f, Path.ChangeExtension(f, "completed")); // Move to completed so it is not processed again.
                        }
                        catch (Exception ex)
                        {
                            fhirResult = DataTypeConverter.CreateOperationOutcome(ex);
                        }

                        // Write the response
                        using (var fs = File.Create(Path.ChangeExtension(f, "response")))
                        {
                            switch (Path.GetExtension(f).ToLowerInvariant())
                            {
                                case ".json":
                                    using (var tw = new StreamWriter(fs))
                                    using (var jw = new JsonTextWriter(tw))
                                    {
                                        new FhirJsonSerializer().Serialize(fhirResult, jw);
                                    }
                                    break;
                                case ".xml":
                                    using (var xw = XmlWriter.Create(fs))
                                    {
                                        new FhirXmlSerializer().Serialize(fhirResult, xw);
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.m_traceSource.TraceError("Error applying {0}: {1}", f, ex);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Start the service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);
            if (ApplicationServiceContext.Current.HostType == SanteDBHostType.Server)
            {
                ApplicationServiceContext.Current.Started += this.InstallDataset;
            }
            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

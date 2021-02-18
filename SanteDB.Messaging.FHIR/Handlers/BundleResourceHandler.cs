﻿/*
 * Portions Copyright 2019-2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE)
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
 * User: fyfej (Justin Fyfe)
 * Date: 2019-11-27
 */
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Messaging.FHIR.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Reflection;
using SanteDB.Core.Diagnostics;
using RestSrvr;
using static Hl7.Fhir.Model.CapabilityStatement;

namespace SanteDB.Messaging.FHIR.Handlers
{
    /// <summary>
    /// Represents a FHIR resource handler for bundles
    /// </summary>
    public class BundleResourceHandler : RepositoryResourceHandlerBase<Hl7.Fhir.Model.Bundle, SanteDB.Core.Model.Collection.Bundle>
    {
        

        /// Gets the interaction that this resource handler provider
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<ResourceInteractionComponent> GetInteractions()
        {
            return new ResourceInteractionComponent[]
            {
                new ResourceInteractionComponent() { Code = TypeRestfulInteraction.Create },
                new ResourceInteractionComponent() { Code = TypeRestfulInteraction.Update }
            };
        }


        /// <summary>
        /// Maps a OpenIZ bundle as FHIR
        /// </summary>
        protected override Hl7.Fhir.Model.Bundle MapToFhir(Core.Model.Collection.Bundle model, RestOperationContext webOperationContext)
        {
            return new Hl7.Fhir.Model.Bundle()
            {
                Type = Hl7.Fhir.Model.Bundle.BundleType.Collection,
                // TODO: Actually construct a response bundle 
            };
            
        }

        /// <summary>
        /// Map FHIR resource to our bundle
        /// </summary>
        protected override Core.Model.Collection.Bundle MapToModel(Hl7.Fhir.Model.Bundle resource, RestOperationContext webOperationContext)
        {
            var retVal = new Core.Model.Collection.Bundle();
            foreach(var entry in resource.Entry)
            {
                var entryType = entry.Resource.GetType();
                if (entryType == null)
                    continue;
                var handler = FhirResourceHandlerUtil.GetResourceHandler(entryType.GetCustomAttribute<XmlRootAttribute>().ElementName) as IBundleResourceHandler;
                if (handler == null)
                {
                    this.traceSource.TraceWarning("Can't find bundle handler for {0}...", entryType.Name);
                    continue;
                }
                retVal.Add(handler.MapToModel(entry.Resource, webOperationContext, resource));
            }
            retVal.Item.RemoveAll(o => o == null);
            return retVal;
        }
    }
}

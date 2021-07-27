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
 * User: khannan (Nityan Khanna)
 * Date: 2021-7-27
 */

using Hl7.Fhir.Model;
using SanteDB.Messaging.FHIR.Extensions;
using System;
using System.Collections.Generic;
using SanteDB.Core;
using SanteDB.Core.Services;

namespace SanteDB.Messaging.FHIR.Operations
{
	/// <summary>
	/// Represents a FHIR MDM query links operation.
	/// </summary>
	public class FhirMdmQueryLinksOperation : IFhirOperationHandler
	{
		/// <summary>
		/// Gets the name of the operation
		/// </summary>
		public string Name => "mdm-query-links";

		/// <summary>
		/// Gets the URI where this operation is defined
		/// </summary>
		public Uri Uri => new Uri("OperationDefinition/mdm-query-links", UriKind.Relative);

		/// <summary>
		/// The type that this operation handler applies to (or null if it applies to all)
		/// </summary>
		public ResourceType[] AppliesTo => new[]
		{
			ResourceType.Organization,
			ResourceType.Patient,
			ResourceType.Practitioner
		};

		/// <summary>
		/// Get the parameter list for this object
		/// </summary>
		public IDictionary<string, FHIRAllTypes> Parameters => new Dictionary<string, FHIRAllTypes>()
		{
			{ "count", FHIRAllTypes.Integer },
			{ "linkSource", FHIRAllTypes.String },
			{ "matchResult", FHIRAllTypes.String },
			{ "offset", FHIRAllTypes.Integer }
		};

		/// <summary>
		/// True if the operation impacts the object state
		/// </summary>
		public bool IsGet => true;

		/// <summary>
		/// Invoke the specified operation
		/// </summary>
		/// <param name="parameters">The parameter set to action</param>
		/// <returns>The result of the operation</returns>
		public Resource Invoke(Parameters parameters)
		{
			var merger = ApplicationServiceContext.Current.GetService(typeof(IRecordMergingService<>).MakeGenericType(typeof(SanteDB.Core.Model.Roles.Patient))) as IRecordMergingService;

			//var merger = ApplicationServiceContext.Current.GetService<IRecordMergingService>();

			if (merger == null)
			{
				throw new InvalidOperationException("No merging service configuration");
			}

			var result = merger.GetMergeCandidates(Guid.Empty);

			var resource = new Parameters();

			resource.Parameter.Add(new Parameters.ParameterComponent
			{
				Name = "hello"
			});

			// TODO
			// get links
			// call scoring service
			// build response
			// return

			return resource;
		}
	}
}

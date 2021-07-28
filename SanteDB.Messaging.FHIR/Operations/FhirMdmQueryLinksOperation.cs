/*
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
using SanteDB.Core;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RestSrvr;
using Patient = SanteDB.Core.Model.Roles.Patient;

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
			var offset = int.TryParse(RestOperationContext.Current.IncomingRequest.QueryString["offset"], out var tempOffset) ? tempOffset : 0;
			var count = int.TryParse(RestOperationContext.Current.IncomingRequest.QueryString["count"], out var tempCount) ? (int?)tempCount : null;

			var merger = ApplicationServiceContext.Current.GetService(typeof(IRecordMergingService<>).MakeGenericType(typeof(SanteDB.Core.Model.Roles.Patient))) as IRecordMergingService;

			if (merger == null)
			{
				throw new InvalidOperationException("No merging service configuration");
			}

			var matchingService = ApplicationServiceContext.Current.GetService<IRecordMatchingService>();

			if (matchingService == null)
			{
				throw new InvalidOperationException("No record matching service found");
			}

			var patientService = ApplicationServiceContext.Current.GetService<IRepositoryService<SanteDB.Core.Model.Roles.Patient>>();

			var result = merger.GetMergeCandidates(Guid.Empty, offset, count);

			var resource = new Parameters();

			foreach (var c in result.OfType<EntityRelationship>())
			{
				var matchResult = matchingService.Classify(patientService.Get(c.TargetEntityKey.Value), new List<Patient>
				{
					new Patient
					{
						Key = c.SourceEntityKey
					}
				}, "org.santedb.matcher.example").FirstOrDefault(x => x.Classification == RecordMatchClassification.Match || x.Classification == RecordMatchClassification.NonMatch);

				if (matchResult == null)
				{
					continue;
				}

				resource.Parameter.Add(new Parameters.ParameterComponent
				{
					Name = "link",
					Part = new List<Parameters.ParameterComponent>
					{
						new Parameters.ParameterComponent
						{
							Name = "masterResourceId",
							Value = new FhirString(c.TargetEntityKey.ToString())
						},
						new Parameters.ParameterComponent
						{
							Name = "sourceResourceId",
							Value = new FhirString(matchResult.Record.Key.ToString())
						},
						new Parameters.ParameterComponent
						{
							Name = "matchResult",
							Value = new FhirString(matchResult.Classification == RecordMatchClassification.Match ? "MATCH" : "NO_MATCH"),
						},
						new Parameters.ParameterComponent
						{
							Name = "linkSource",
							Value = new FhirString("auto"),
						},
						new Parameters.ParameterComponent
						{
							Name = "score",
							Value = new FhirDecimal(Convert.ToDecimal(matchResult.Score))
						}
					}
				});
			}

			// TODO
			// get links
			// call scoring service
			// build response
			// return

			return resource;
		}
	}
}

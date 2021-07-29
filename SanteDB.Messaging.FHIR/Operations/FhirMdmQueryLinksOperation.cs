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
 * User: khannan (Nityan Khanna) & ibrahim (Mo Ibrahim)
 * Date: 2021-7-27
 */

using Hl7.Fhir.Model;
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Messaging.FHIR.Extensions;
using SanteDB.Persistence.MDM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Expression = System.Linq.Expressions.Expression;
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
			ResourceType.Patient
		};

		/// <summary>
		/// Get the parameter list for this object
		/// </summary>
		public IDictionary<string, FHIRAllTypes> Parameters => new Dictionary<string, FHIRAllTypes>()
		{
			{ "_count", FHIRAllTypes.Integer },
			{ "linkSource", FHIRAllTypes.String },
			{ "matchResult", FHIRAllTypes.String },
			{ "_offset", FHIRAllTypes.Integer }
		};

		/// <summary>
		/// The link source map.
		/// </summary>
		private static readonly Dictionary<string, Guid> linkSourceMap = new Dictionary<string, Guid>
		{
			{ "AUTO", MdmConstants.AutomagicClassification },
			{ "MANUAL", MdmConstants.VerifiedClassification },
		};

		/// <summary>
		/// The match result source map.
		/// </summary>
		private static readonly Dictionary<string, RecordMatchClassification> matchResultMap = new Dictionary<string, RecordMatchClassification>
		{
			{ "MATCH", RecordMatchClassification.Match },
			{ "POSSIBLE_MATCH", RecordMatchClassification.Probable },
			{ "NO_MATCH", RecordMatchClassification.NonMatch },
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
			var linkSource = RestOperationContext.Current.IncomingRequest.QueryString["linkSource"]?.ToUpperInvariant();
			var matchResult = RestOperationContext.Current.IncomingRequest.QueryString["matchResult"]?.ToUpperInvariant();

			var matchingService = ApplicationServiceContext.Current.GetService<IRecordMatchingService>();

			if (matchingService == null)
			{
				throw new InvalidOperationException("No record matching service found");
			}

			var configuration = RestOperationContext.Current.IncomingRequest.QueryString["_configurationName"] ?? "org.santedb.matcher.example";

			if (string.IsNullOrEmpty(configuration))
			{
				throw new InvalidOperationException("No resource merge configuration specified. Use the ?_configurationName parameter specify a configuration.");
			}

			var entityRelationshipService = ApplicationServiceContext.Current.GetService<IRepositoryService<EntityRelationship>>();
			var patientService = ApplicationServiceContext.Current.GetService<IRepositoryService<Patient>>();

			Expression<Func<EntityRelationship, bool>> queryExpression = c => c.RelationshipTypeKey == MdmConstants.CandidateLocalRelationship && c.ObsoleteVersionSequenceId == null;

			if (!string.IsNullOrEmpty(linkSource) && linkSourceMap.TryGetValue(linkSource, out var linkSourceKey))
			{
				var updatedExpression = Expression.MakeBinary(ExpressionType.AndAlso, queryExpression.Body, Expression.MakeBinary(ExpressionType.Equal, Expression.Property(Expression.Parameter(typeof(EntityRelationship)), typeof(EntityRelationship), nameof(EntityRelationship.ClassificationKey)), Expression.Constant(linkSourceKey, typeof(Guid?))));

				queryExpression = Expression.Lambda<Func<EntityRelationship, bool>>(updatedExpression, queryExpression.Parameters);
			}

			var relationships = entityRelationshipService.Find(queryExpression, offset, count, out _, null);

			Expression<Func<RecordMatchClassification, bool>> matchClassificationExpression = x => x == RecordMatchClassification.Match || x == RecordMatchClassification.Probable || x == RecordMatchClassification.NonMatch;

			if (!string.IsNullOrEmpty(matchResult) && matchResultMap.TryGetValue(matchResult, out var matchResultKey))
			{
				matchClassificationExpression = x => x == matchResultKey;
			}

			var resource = new Parameters();

			foreach (var entityRelationship in relationships)
			{
				var classificationResult = matchingService.Classify(patientService.Get(entityRelationship.TargetEntityKey.Value), new List<Patient>
				{
					new Patient
					{
						Key = entityRelationship.SourceEntityKey
					}
				}, configuration).FirstOrDefault(x => matchClassificationExpression.Compile().Invoke(x.Classification));

				if (classificationResult == null)
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
							Value = new FhirString(entityRelationship.TargetEntityKey.ToString())
						},
						new Parameters.ParameterComponent
						{
							Name = "sourceResourceId",
							Value = new FhirString(classificationResult.Record.Key.ToString())
						},
						new Parameters.ParameterComponent
						{
							Name = "matchResult",
							Value = new FhirString(matchResultMap.First(c => c.Value == classificationResult.Classification).Key)
						},
						new Parameters.ParameterComponent
						{
							Name = "linkSource",
							Value = new FhirString(linkSourceMap.First(c => c.Value == entityRelationship.ClassificationKey).Key)
						},
						new Parameters.ParameterComponent
						{
							Name = "score",
							Value = new FhirDecimal(Convert.ToDecimal(classificationResult.Score))
						}
					}
				});
			}

			return resource;
		}
	}
}

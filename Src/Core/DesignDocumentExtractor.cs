#region Licence Info 
/*
	Copyright 2011 · Artem Tikhomirov, Stas Girkin, Mikhail Anikeev-Naumenko																					
																																					
	Licensed under the Apache License, Version 2.0 (the "License");					
	you may not use this file except in compliance with the License.					
	You may obtain a copy of the License at																	
																																					
	    http://www.apache.org/licenses/LICENSE-2.0														
																																					
	Unless required by applicable law or agreed to in writing, software			
	distributed under the License is distributed on an "AS IS" BASIS,				
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.	
	See the License for the specific language governing permissions and			
	limitations under the License.																						
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CouchDude.SchemeManager
{
	/// <summary>Extracts design documents from standard CouchDB list.</summary>
	public class DesignDocumentExtractor : IDesignDocumentExtractor
	{
		/// <summary>Extracts design documents.</summary>
		public IDictionary<string, DesignDocument> Extract(IEnumerable<JObject> rawDocuments)
		{
			if (rawDocuments == null) throw new ArgumentNullException("rawDocuments");

			return rawDocuments.Select(GetDocument).ToDictionary(doc => doc.Id, doc => doc);
		}

		static DesignDocument GetDocument(JObject rowObject)
		{
			var keyProperty = rowObject["key"] as JValue;
			if (keyProperty == null)
				throw new ParseException("Document list row object should contain 'key' property.");

			var id = keyProperty.Value<string>();
			if (!id.StartsWith(DesignDocument.IdPrefix))
				throw new ParseException(
					"Document list row object's 'key' property should start with " + DesignDocument.IdPrefix + "'.");

			var valueProperty = rowObject["value"] as JObject;
			if (valueProperty == null)
				throw new ParseException(
					"Document list row object should contain 'value' property.");

			var revProperty = valueProperty["rev"] as JValue;
			if (revProperty == null)
				throw new ParseException(
					"Document list row's value property object should contain 'rev' property.");

			var documentProperty = rowObject["doc"] as JObject;
			if (documentProperty == null)
				throw new ParseException(
					"Document list row object should contain 'doc' property.");

			return new DesignDocument(documentProperty, id, revProperty.Value<string>());
		}
	}
}

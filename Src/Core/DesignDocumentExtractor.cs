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
using System.Json;
using System.Linq;

namespace CouchDude.SchemeManager
{
	/// <summary>Extracts design documents from standard CouchDB list.</summary>
	public class DesignDocumentExtractor : IDesignDocumentExtractor
	{
		/// <summary>Extracts design documents.</summary>
		public IDictionary<string, DesignDocument> Extract(IEnumerable<JsonObject> rawDocuments)
		{
			if (rawDocuments == null) throw new ArgumentNullException("rawDocuments");
			return rawDocuments.Select(jsonDoc => new DesignDocument(jsonDoc)).ToDictionary(doc => doc.Id, doc => doc);
		}
	}
}

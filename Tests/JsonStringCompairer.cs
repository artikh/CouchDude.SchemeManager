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

using System.Collections.Generic;
using System.Json;
using CouchDude.Utils;

namespace CouchDude.SchemeManager.Tests
{
	public class JsonStringCompairer: IEqualityComparer<string>
	{
		static readonly JsonObjectComparier Comparier = new JsonObjectComparier();

		public bool Equals(string x, string y)
		{
			var aValue = Parse(x);
			var bValue = Parse(y);
			return Comparier.Equals(aValue, bValue);
		}

		private static JsonValue Parse(string str) { return JsonValue.Parse(str); }

		public int GetHashCode(string obj)
		{
			return JsonValue.Parse(obj).GetHashCode();
		}
	}
}
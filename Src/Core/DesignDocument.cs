
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

using System.Json;
using System.Linq;

namespace CouchDude.SchemeManager
{
	/// <summary>Design document descriptor.</summary>
	public class DesignDocument: Document
	{
		/// <summary>Design ID prefix.</summary>
		public const string IdPrefix = "_design/";
		
		/// <constructor />
		public DesignDocument(JsonObject documentJson): base(documentJson) { }

		/// <summary>Determines if documents apears new.</summary>
		public bool IsNew { get { return Revision == null; } }

		/// <inheritdoc/>
		public bool Equals(DesignDocument other)
		{
			base.Eq
			return GetWithoutRev(this).Equals(GetWithoutRev(other));
		}

		private static JsonObject GetWithoutRev(Document document)
		{
			if (ReferenceEquals(document, null)) return null;


			var rawJsonObject = document.RawJsonObject;
			
			if (rawJsonObject.ContainsKey(RevisionPropertyName))
			{
				var objectPropertiesWithoutRev = rawJsonObject.Where(kvp => kvp.Key != RevisionPropertyName);
				var objectWithoutRev = new JsonObject(objectPropertiesWithoutRev);
				return objectWithoutRev;
			}

			return rawJsonObject;
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof (DesignDocument)) return false;
			return Equals((DesignDocument) obj);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		/// <inheritdoc/>
		public static bool operator ==(DesignDocument left, DesignDocument right)
		{
			return Equals(left, right);
		}

		/// <inheritdoc/>
		public static bool operator !=(DesignDocument left, DesignDocument right)
		{
			return !Equals(left, right);
		}

		/// <inheritdoc/>
		public DesignDocument CopyWithRevision(string newRevision)
		{
			var jsonObject = new JsonObject(RawJsonObject);
			jsonObject[RevisionPropertyName] = newRevision;
			return new DesignDocument(jsonObject);
		}
	}
}
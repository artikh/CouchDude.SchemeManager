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

using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;

namespace CouchDude.SchemeManager
{
	/// <summary>Orchestrates Couch Dude's actions.</summary>
	public class Engine: HttpClient
	{
		private static readonly ILog Log = LogManager.GetCurrentClassLogger();

		private readonly IDesignDocumentAssembler designDocumentAssembler;
		private readonly IDesignDocumentExtractor designDocumentExtractor;

		/// <constructor />
		internal Engine(IDesignDocumentExtractor designDocumentExtractor, IDesignDocumentAssembler designDocumentAssembler)
		{
			if (designDocumentAssembler == null)
				throw new ArgumentNullException("designDocumentAssembler");


			this.designDocumentExtractor = designDocumentExtractor;
			this.designDocumentAssembler = designDocumentAssembler;
		}

		/// <constructor />
		internal Engine(
			IDesignDocumentExtractor designDocumentExtractor, IDesignDocumentAssembler designDocumentAssembler, HttpMessageHandler messageHandler)
			: base(messageHandler)
		{
			if(messageHandler == null) throw new ArgumentNullException("messageHandler");
			if(designDocumentAssembler == null) 
				throw new ArgumentNullException("designDocumentAssembler");
			

			this.designDocumentExtractor = designDocumentExtractor;
			this.designDocumentAssembler = designDocumentAssembler;
		}

		/// <summary>Creates engine based on provided <paramref name="directoryInfo"/>.</summary>
		public static Engine CreateStandard(DirectoryInfo directoryInfo)
		{
			var directory = new Directory(directoryInfo);
			var documentAssembler = new DesignDocumentAssembler(directory);
			var designDocumentExtractor = new DesignDocumentExtractor();
			return new Engine(designDocumentExtractor, documentAssembler);
		}

		/// <summary>Checks if database design document set have changed comparing with
		/// one generated from file system tree.</summary>
		public bool CheckIfChanged(Uri databaseUri, string userName = null, string password = null)
		{
			if (databaseUri == null) throw new ArgumentNullException("databaseUri");
			if (!databaseUri.IsAbsoluteUri)
				throw new ArgumentException("databaseUri should be absolute", "databaseUri");
			if (!databaseUri.Scheme.StartsWith("http"))
				throw new ArgumentException("databaseUri should be http or https", "databaseUri");

			var docsFromDatabase = GetDesignDocumentsFromDatabase(databaseUri, userName, password);
			var docsFromFileSystem = GetDesignDocumentsFromFileSystem();

			return GetChangedDocuments(docsFromFileSystem, docsFromDatabase).Any();
		}

		/// <summary>Updates database design document set with one generated from file system
		/// tree.</summary>
		public void PushIfChanged(Uri databaseUri, string userName = null, string password = null)
		{			
			if(databaseUri == null) throw new ArgumentNullException("databaseUri");
			if(!databaseUri.IsAbsoluteUri) 
				throw new ArgumentException("databaseUri should be absolute", "databaseUri");
			if(!databaseUri.Scheme.StartsWith("http")) 
				throw new ArgumentException("databaseUri should be http or https", "databaseUri");

			var docsFromDatabase = GetDesignDocumentsFromDatabase(databaseUri, userName, password);
			var docsFromFileSystem = GetDesignDocumentsFromFileSystem();
			var changedDocs = GetChangedDocuments(docsFromFileSystem, docsFromDatabase);
			Log.InfoFormat("{0} design documents will be pushed to database.", changedDocs.Count);

			foreach (var changedDoc in changedDocs) 
			{
				//пропускает папки, создаваемые при билде солюшена
				if (changedDoc.Id == "_design/obj" || changedDoc.Id == "_design/bin" || changedDoc.Id == "_design/Properties") continue;

				Log.InfoFormat("Pushing document {0} to the database.", changedDoc.Id);

				var documentUri = new Uri(databaseUri, changedDoc.Id);
				changedDoc.Definition.ToString(Formatting.None);

				var request = new HttpRequestMessage(HttpMethod.Put, documentUri) {
					Content = new StringContent(changedDoc.Definition.ToString(Formatting.None))
				};
				var response = SendAsync(request, userName, password).Result;
				response.EnsureSuccessStatusCode();
			}
		}

		/// <summary>Удаляет базу данных и создает снова.</summary>
		public void Truncate(Uri databaseUri, string userName = null, string password = null)
		{
			if (databaseUri == null) throw new ArgumentNullException("databaseUri");
			if (!databaseUri.IsAbsoluteUri)
				throw new ArgumentException("databaseUri should be absolute", "databaseUri");
			if (!databaseUri.Scheme.StartsWith("http"))
				throw new ArgumentException("databaseUri should be http or https", "databaseUri");

			Task.WaitAll(
				SendAsync(new HttpRequestMessage(HttpMethod.Delete, databaseUri), userName, password),
				SendAsync(new HttpRequestMessage(HttpMethod.Put, databaseUri), userName, password)
			);
		}


		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string userName, string password)
		{
			if(userName != null && password != null)
			{
				var credentialsString = string.Concat(userName, ":", password);
				var credentialsBytes = Encoding.UTF8.GetBytes(credentialsString);
				var base64String = Convert.ToBase64String(credentialsBytes);
				request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64String);
			}

			return base.SendAsync(request);
		}


		/// <summary>Generates design documents from directory content.</summary>
		public IEnumerable<string> Generate()
		{
			return GetDesignDocumentsFromFileSystem().Values
				.Select(dd => dd.Definition.ToString(Formatting.Indented));
		}
		
		private static IList<DesignDocument> GetChangedDocuments(
			IDictionary<string, DesignDocument> docsFromFs, 
			IDictionary<string, DesignDocument> docsFromDb)
		{
			Log.Info("Figuring out if any document have changed.");
			var changedDocuments = new List<DesignDocument>();
			foreach (var docFromFs in docsFromFs.Values)
			{
				DesignDocument docFromDb;
				if (!docsFromDb.TryGetValue(docFromFs.Id, out docFromDb))
				{
					Log.InfoFormat("Design document {0} is new:\n{1}", docFromFs.Id, docFromFs.Definition);
					changedDocuments.Add(docFromFs);
				}
				else if (docFromDb != docFromFs)
				{
					Log.InfoFormat("Design document {0} have changed:\n{1}", docFromFs.Id, docFromFs.Definition);
					changedDocuments.Add(docFromFs.CopyWithRevision(docFromDb.Revision));
				}
			}

			if(Log.IsInfoEnabled)
			{
				if(changedDocuments.Count == 0)
					Log.Info("No design documents have changed.");
				else
					Log.InfoFormat("{0} design documents will be pushed to database.", changedDocuments.Count);
			}

			return changedDocuments;
		}

		private IDictionary<string, DesignDocument> GetDesignDocumentsFromDatabase(Uri databaseUri, string userName, string password) 
		{
			Log.Info("Downloading design documents from database...");
			var request = new HttpRequestMessage(
				HttpMethod.Get, 
				databaseUri + @"_all_docs?startkey=""_design/""&endkey=""_design0""&include_docs=true");
			var response = SendAsync(request, userName, password).Result;
			using(var stream = response.Content.ReadAsStreamAsync().Result)
			using (var reader = new StreamReader(stream))
			{
				var designDocumentsFromDatabase = designDocumentExtractor.Extract(reader);
				Log.InfoFormat(
					"{0} design documens downloaded from database.", designDocumentsFromDatabase.Count);
				return designDocumentsFromDatabase;
			}
		}

		private IDictionary<string, DesignDocument> GetDesignDocumentsFromFileSystem()
		{
			Log.Info("Creating design documents from file system...");
			var designDocumentsFromFileSystem = designDocumentAssembler.Assemble();
			Log.InfoFormat(
				"{0} design documens created from file system.", designDocumentsFromFileSystem.Count);
			return designDocumentsFromFileSystem;
		}
	}
}
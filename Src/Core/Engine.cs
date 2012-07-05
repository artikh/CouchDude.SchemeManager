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
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CouchDude.SchemeManager
{
	/// <summary>Orchestrates Couch Dude's actions.</summary>
	public class Engine
	{
		private static readonly ILog Log = LogManager.GetCurrentClassLogger();

		private readonly IDesignDocumentAssembler designDocumentAssembler;
		readonly HttpMessageHandler messageHandler;
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
		internal Engine(IDesignDocumentExtractor designDocumentExtractor, IDesignDocumentAssembler designDocumentAssembler, HttpMessageHandler messageHandler)
		{
			if(messageHandler == null) throw new ArgumentNullException("messageHandler");
			if(designDocumentAssembler == null) 
				throw new ArgumentNullException("designDocumentAssembler");
			

			this.designDocumentExtractor = designDocumentExtractor;
			this.designDocumentAssembler = designDocumentAssembler;
			this.messageHandler = messageHandler;
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

			var docsFromFileSystem = GetDesignDocumentsFromFileSystem();

			while (true)
			{
				var docsFromDatabase = GetDesignDocumentsFromDatabase(databaseUri, userName, password);
				var changedDocs = GetChangedDocuments(docsFromFileSystem, docsFromDatabase);
				if(changedDocs.Count == 0)
				{
					Log.Info("Design documents are identical now");
					return;
				}
				Log.InfoFormat("{0} design documents will be pushed to the database.", changedDocs.Count);

				PushChangedDocsToDatabase(databaseUri, userName, password, changedDocs).Wait();
				Log.InfoFormat("{0} design documents have been pushed to the database.", changedDocs.Count);
			}
		}

		Task PushChangedDocsToDatabase(Uri databaseUri, string userName, string password, IEnumerable<DesignDocument> changedDocs)
		{
			var dbApi = ConsturctDatabaseApi(databaseUri, userName, password);
			return dbApi.BulkUpdate(b => {
				foreach (var changedDoc in changedDocs)
				{
					Log.TraceFormat("Pushing document {0} to the database.", changedDoc.Id);
					if (changedDoc.IsNew)
						b.Create(changedDoc);
					else
						b.Update(changedDoc);
				}
			});
		}

		/// <summary>Удаляет базу данных и создает снова.</summary>
		public void PurgeDatabase(Uri databaseUri, string userName = null, string password = null)
		{
			if (databaseUri == null) throw new ArgumentNullException("databaseUri");
			if (!databaseUri.IsAbsoluteUri)
				throw new ArgumentException("databaseUri should be absolute", "databaseUri");
			if (!databaseUri.Scheme.StartsWith("http"))
				throw new ArgumentException("databaseUri should be http or https", "databaseUri");


			Log.InfoFormat("Purging database {0}", databaseUri);

			PurgeDatabaseInternal(databaseUri, userName, password).Wait();
		}

		async Task PurgeDatabaseInternal(Uri databaseUri, string userName, string password)
		{
			const int batchSize = 1000;

			var dbApi = ConsturctDatabaseApi(databaseUri, userName, password);
			while (true)
			{
				var batchRequestResult = await dbApi.Query(new ViewQuery {
					ViewName = "_all_docs",
					IncludeDocs = false,
					Limit = batchSize
				});
				if (batchRequestResult.Count == 0)
				{
					Log.Info("No more documents to purge from database");
					return;
				}
				Log.InfoFormat("Deleting {0} documents", batchRequestResult.Count);
				await dbApi.BulkUpdate(
					b => {
						foreach (var row in batchRequestResult.Rows)
						{
							var docId = row.DocumentId;
							var docRevision = (string) row.Value["rev"];
							b.Delete(docId, docRevision);
						}
					});
			}
		}
		
		IDatabaseApi ConsturctDatabaseApi(Uri databaseUri, string userName, string password)
		{
			var serverUriString = databaseUri.GetComponents(
				UriComponents.SchemeAndServer, UriFormat.SafeUnescaped);
			var databaseName = databaseUri.LocalPath.Trim('/');
			var settings = new CouchApiSettings(serverUriString);
			if(userName != null && password != null)
				settings.Credentials = new Credentials(userName, password);
			return settings.CreateCouchApi(messageHandler).Db(databaseName);
		}

		/// <summary>Generates design documents from directory content.</summary>
		public IEnumerable<string> Generate()
		{
			return GetDesignDocumentsFromFileSystem().Values.Select(dd => dd.ToString());
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
					Log.InfoFormat("Design document {0} is new", docFromFs.Id);
					Log.Trace(docFromFs);
					changedDocuments.Add(docFromFs);
				}
				else if (docFromDb != docFromFs)
				{
					Log.InfoFormat("Design document {0} have changed", docFromFs.Id);
					Log.Trace(docFromFs);
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

			var dbApi = ConsturctDatabaseApi(databaseUri, userName, password);
			var result = dbApi.Query(new ViewQuery {
				ViewName = "_all_docs",
				StartKey = "_design/",
				EndKey = "_design0",
				IncludeDocs = true
			}).Result.Rows.Select(r => r.Document.RawJsonObject);
			
			var designDocumentsFromDatabase = designDocumentExtractor.Extract(result);
			Log.InfoFormat("{0} design documens downloaded from database.", designDocumentsFromDatabase.Count);
			return designDocumentsFromDatabase;
		}

		private IDictionary<string, DesignDocument> GetDesignDocumentsFromFileSystem()
		{
			Log.Info("Creating design documents from file system...");
			var designDocumentsFromFileSystem = designDocumentAssembler.Assemble();
			Log.InfoFormat("{0} design documens created from file system.", designDocumentsFromFileSystem.Count);
			return designDocumentsFromFileSystem;
		}
	}
}
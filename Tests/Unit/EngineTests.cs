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
using System.Net;
using System.Net.Http;
using System.Text;
using Moq;
using Xunit;

namespace CouchDude.SchemeManager.Tests.Unit
{
	public class EngineTests
	{
		private readonly JsonObject docA =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc1"", ""_rev"": ""1"", ""prop"": ""prop value of doc1"" }") as JsonObject;

		private readonly JsonObject docAWithoutRev =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc1"", ""prop"": ""prop value of doc1"" }") as JsonObject;

		private readonly JsonObject docB =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc2"", ""_rev"": ""1"", ""prop"": ""prop value of doc2"" }") as JsonObject;

		private readonly JsonObject docBWithoutRev =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc2"", ""prop"": ""prop value of doc2"" }") as JsonObject;

		private readonly JsonObject docB2WithoutRev =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc2"", ""_rev"": ""1"", ""prop"": ""prop value of doc2, version 2"" }") as JsonObject;

		private readonly JsonObject docC =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc3"", ""_rev"": ""1"", ""prop"": ""prop value of doc3"" }") as JsonObject;

		private readonly JsonObject docCWithoutRev =
			JsonValue.Parse(@"{ ""_id"": ""_design/doc3"", ""prop"": ""prop value of doc3"" }") as JsonObject;

		[Fact]
		public void ShuldPassGenerateRequestThroughTo()
		{
			var engine = new Engine(
				Mock.Of<IDesignDocumentExtractor>(), 
				Mock.Of<IDesignDocumentAssembler>(a => a.Assemble() == CreateDesignDocumentMap(docA)),
				new MockMessageHandler());
      var generatedJsonStingDocs = engine.Generate().ToArray();

			Assert.Equal(1, generatedJsonStingDocs.Length);
			Assert.Equal(docA.ToString(), generatedJsonStingDocs[0], new JsonStringCompairer());
		}

		[Fact]
		public void ShouldReturnFalseIfHaveNotChanged()
		{
			var engine = new Engine(
				Mock.Of<IDesignDocumentExtractor>(
					e => e.Extract(It.IsAny<IEnumerable<JsonObject>>()) == CreateDesignDocumentMap(docA, docB, docC)
				),
				Mock.Of<IDesignDocumentAssembler>(a => a.Assemble() == CreateDesignDocumentMap(docAWithoutRev, docBWithoutRev, docCWithoutRev)),
				GetNoResultsMessageHandler());

			Assert.False(engine.CheckIfChanged(new Uri("http://example.com/db1")));
		}

		[Fact]
		public void ShouldReturnTrueIfThereAreMoreDocumentOnDisk()
		{
			var engine = new Engine(
				Mock.Of<IDesignDocumentExtractor>(
					e => e.Extract(It.IsAny<IEnumerable<JsonObject>>()) == CreateDesignDocumentMap(docA, docB)
					),
				Mock.Of<IDesignDocumentAssembler>(a => a.Assemble() == CreateDesignDocumentMap(docAWithoutRev, docBWithoutRev, docCWithoutRev)),
				GetNoResultsMessageHandler());

			Assert.True(engine.CheckIfChanged(new Uri("http://example.com/db1")));
		}

		[Fact]
		public void ShouldReturnTrueIfDocumentOnDiskHaveChanged()
		{
			var engine = new Engine(
				Mock.Of<IDesignDocumentExtractor>(
					e => e.Extract(It.IsAny<IEnumerable<JsonObject>>()) == CreateDesignDocumentMap(docA, docB, docC)
					),
				Mock.Of<IDesignDocumentAssembler>(a => a.Assemble() == CreateDesignDocumentMap(docAWithoutRev, docB2WithoutRev, docCWithoutRev)),
				GetNoResultsMessageHandler());

			Assert.True(engine.CheckIfChanged(new Uri("http://example.com/db1")));
		}

		[Fact]
		public void ShouldPushNewDocumensWithoutRevisionsWithPut()
		{
			var expectedDoc = docAWithoutRev;
			var mockHandler = GetNoResultsMessageHandler();

			var stage = 0;
			var extractorMock = new Mock<IDesignDocumentExtractor>();
			extractorMock
				.Setup(e => e.Extract(It.IsAny<IEnumerable<JsonObject>>()))
				.Returns(
					(IEnumerable<JsonObject> _) =>
					{
						switch (stage++)
						{
							case 0: return new Dictionary<string, DesignDocument>(0);
							case 1: return CreateDesignDocumentMap(expectedDoc);
							default: throw new Exception();
						}
					});
			var engine = new Engine(
				extractorMock.Object,
				Mock.Of<IDesignDocumentAssembler>(a => a.Assemble() == CreateDesignDocumentMap(expectedDoc)),
				mockHandler
			);

			engine.PushIfChanged(new Uri("http://example.com/db1"));

			Assert.Equal("http://example.com/db1/_bulk_docs", mockHandler.Requests[1].RequestUri.ToString());
			Assert.Equal("POST", mockHandler.Requests[1].Method.ToString());
			Assert.Equal("{\"docs\":[" + expectedDoc + "]}", mockHandler.RequestBodies[1], new JsonStringCompairer());
		}


		[Fact]
		public void ShouldPushUpdatedDocumensWithDbDocumentRevisionWithPut()
		{
			var expectedDoc = new JsonObject(docB2WithoutRev);

			int stage = 0;

			var extractorMock = new Mock<IDesignDocumentExtractor>();
			extractorMock
				.Setup(e => e.Extract(It.IsAny<IEnumerable<JsonObject>>()))
				.Returns(
					(IEnumerable<JsonObject> _) => {
						switch (stage++)
						{
							case 0: return CreateDesignDocumentMap(docB);
							case 1: return CreateDesignDocumentMap(expectedDoc);
							default: throw new Exception();
						}
					});

			var handler = GetNoResultsMessageHandler();
			var engine = new Engine(
				extractorMock.Object, 
				Mock.Of<IDesignDocumentAssembler>(a => a.Assemble() == CreateDesignDocumentMap(docB2WithoutRev)),
				handler
			);

			engine.PushIfChanged(new Uri("http://example.com/db1"));

			Assert.Equal("http://example.com/db1/_bulk_docs", handler.Requests[1].RequestUri.ToString());
			Assert.Equal("POST", handler.Requests[1].Method.ToString());
			
			expectedDoc["_rev"] = docB["_rev"];
			Assert.Equal("{\"docs\":[" + expectedDoc + "]}", handler.RequestBodies[1], new JsonStringCompairer());
		}

		private static IDictionary<string, DesignDocument> CreateDesignDocumentMap(params JsonObject[] objects)
		{
			var map = new Dictionary<string, DesignDocument>(objects.Length);
			foreach (var jsonObject in objects)
			{
				var id = (string)jsonObject["_id"];
				if (jsonObject.ContainsKey("_rev"))
				{
					map.Add(id, new DesignDocument(jsonObject));
				}
				else
					map.Add(id, new DesignDocument(jsonObject));
			}
			return map;
		}

		static MockMessageHandler GetNoResultsMessageHandler()
		{
			return new MockMessageHandler(ProcessRequest);
		}

		static string GetResultText(HttpRequestMessage request)
		{
			if (request.RequestUri.ToString().Contains("_all_docs")) return "{\"rows\":[]";
			if (request.RequestUri.ToString().Contains("_bulk_docs")) return "[]";
			
			return "{\"ok\":true}";
		}

		static HttpResponseMessage ProcessRequest(HttpRequestMessage request)
		{
			return ConstructResponseManager(GetResultText(request));
		}

		static HttpResponseMessage ConstructResponseManager(string resultText)
		{
			return new HttpResponseMessage(HttpStatusCode.OK) {
				Content = new StringContent(resultText, Encoding.UTF8, "application/json")
			};
		}
	}
}

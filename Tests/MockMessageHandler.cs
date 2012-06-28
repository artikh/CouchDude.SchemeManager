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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CouchDude.SchemeManager.Tests
{
	internal class MockMessageHandler : HttpMessageHandler
	{
		readonly Stack<HttpRequestMessage> requests = new Stack<HttpRequestMessage>();
		readonly Stack<string> requestBodies = new Stack<string>();

		public Func<HttpRequestMessage, HttpResponseMessage> ProcessRequest { get; private set; }
		public HttpRequestMessage[] Requests { get { return requests.ToArray(); } }
		public string[] RequestBodies { get { return requestBodies.ToArray(); } }
		public HttpRequestMessage Request { get{ return requests.Peek(); } }
		public string RequestBody { get{ return requestBodies.Peek(); } }

		public MockMessageHandler(string responseText) : this(HttpStatusCode.OK, responseText) { }

		public MockMessageHandler(HttpStatusCode code, string responseText)
			: this(new HttpResponseMessage {StatusCode = code, Content = new StringContent(responseText)}) { }
		public MockMessageHandler()
			: this(new HttpResponseMessage {
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
			}) { }
		public MockMessageHandler(HttpResponseMessage response): this(_ => response) { }
		public MockMessageHandler(Exception exception): this(_ => { throw exception; }) {  }
		public MockMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> processRequest) { ProcessRequest = processRequest; }
		
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
		{
			requests.Push(request);
			requestBodies.Push(Request.Content != null? Request.Content.ReadAsStringAsync().Result: null);
			return Task.Factory.StartNew(() => ProcessRequest(request));
		}
	}
}


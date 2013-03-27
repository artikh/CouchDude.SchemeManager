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
using System.Diagnostics;
using System.IO;
using CommandLine;
using Common.Logging;
using Common.Logging.Simple;

#pragma warning disable 0649

namespace CouchDude.SchemeManager.Console
{
	class Program
	{
		private static ILog log;

		private const int OkReturnCode = 0;
		private const int IncorrectOptionsReturnCode = 1;
		private const int UnknownErrorReturnCode = 2;
		

		static int Main(string[] args)
		{
			var options = new Options();
			ICommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(System.Console.Error));
			if (!parser.ParseArguments(args, options))
			{
				System.Console.Error.WriteLine("Some of argumens are incorrect");
				return IncorrectOptionsReturnCode;
			}

			LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(options.Verbose ? LogLevel.Info: LogLevel.Warn, false, true, true, null);
			log = LogManager.GetCurrentClassLogger();

			var directoryPath = !string.IsNullOrWhiteSpace(options.BaseDirectory)? options.BaseDirectory: Environment.CurrentDirectory;

			var baseDirectory = new DirectoryInfo(directoryPath);
			if(!baseDirectory.Exists)
			{
				log.ErrorFormat("Provided directory {0} does not exist.", options.BaseDirectory);
				return IncorrectOptionsReturnCode;
			}

			var url = new Lazy<Uri>(() => ParseDatabaseUrl(options));

			if (options.Command == CommandType.Help)
				System.Console.WriteLine(options.GetUsage());
			else
				try 
				{
					ExecuteCommand(options.Command, baseDirectory, url, options.UserName, options.Password);
				}
				catch (Exception e)
				{
					log.ErrorFormat(e.ToString());
					return UnknownErrorReturnCode;
				}

			return OkReturnCode;
		}

		private static void ExecuteCommand(CommandType command, DirectoryInfo baseDirectory, Lazy<Uri> url, string userName, string password) 
		{
			var engine = Engine.CreateStandard(baseDirectory);
			switch (command)
			{
				case CommandType.Help:
					break;
				case CommandType.Generate:
					var generatedDocuments = engine.Generate();
					foreach (var generatedDocument in generatedDocuments)
					{
						System.Console.WriteLine(generatedDocument);
						System.Console.WriteLine();
					}
					break;
				case CommandType.Check:
					var haveChanged =
						engine.CheckIfChanged(url.Value, userName, password);
					System.Console.WriteLine(haveChanged? "Changed": "Have not changed");
					break;
				case CommandType.Push:
					engine.PushIfChanged(url.Value, userName, password);
					break;
				case CommandType.Purge:
					engine.PurgeDatabase(url.Value, userName, password);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		static Uri ParseDatabaseUrl(Options options)
		{
			Uri uri;
			if (!options.DatabaseUrl.EndsWith("/"))
				options.DatabaseUrl = options.DatabaseUrl + "/";

			if(!Uri.TryCreate(options.DatabaseUrl, UriKind.RelativeOrAbsolute, out uri))
			{
				log.ErrorFormat("Provided URI is malformed: {0}", options.DatabaseUrl);
				Environment.Exit(IncorrectOptionsReturnCode);
			}
			if (uri.Scheme != "http" && uri.Scheme != "https")
			{
				log.ErrorFormat("Provided URI is not of HTTP(S) scheme: {0}", options.DatabaseUrl);
				Environment.Exit(IncorrectOptionsReturnCode);
			}
			if (!uri.IsAbsoluteUri)
			{
				log.ErrorFormat("Provided URI is not absolute: {0}", options.DatabaseUrl);
				Environment.Exit(IncorrectOptionsReturnCode);
			}
			return uri;
		}
	}
}

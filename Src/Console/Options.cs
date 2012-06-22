using System.Reflection;
using CommandLine;
using CommandLine.Text;

namespace CouchDude.SchemeManager.Console
{
	public sealed class Options
	{
		public Options()
		{
			Command = CommandType.Help;
			BaseDirectory = string.Empty;
		}

		[Option("c", "command", Required = true, HelpText = "help|generate|check|push")]
		public CommandType Command { get; set; }

		[Option("a", "address", HelpText = "Database URL")]
		public string DatabaseUrl { get; set; }

		[Option("p", "password", HelpText = "Database access password")]
		public string Password { get; set; }

		[Option("u", "user", HelpText = "Database access user name")]
		public string UserName { get; set; }

		[Option("v", "verbose", HelpText = "Log diagnostics to console window")]
		public bool Verbose { get; set; }

		[Option("d", "directory", HelpText = "Base directory for document generation")]
		public string BaseDirectory { get; set; }

		[HelpOption(HelpText = "Dispaly this help screen")]
		public string GetUsage()
		{
			var help = new HelpText { AdditionalNewLineAfterOption = true };
			var execName = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
			help.AddPreOptionsLine(string.Format("Usage: {0} -c {{command}} -a http://admin:passw0rd@example.com:5984/yourdb [-d .\\designDocuments] [-u user1 -p passw0rd]", execName));
			help.AddOptions(this);
			return help;
		}
	}
}
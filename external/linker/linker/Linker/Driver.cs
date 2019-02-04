﻿//
// Driver.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// (C) 2006 Jb Evain
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.XPath;

using Mono.Linker.Steps;

namespace Mono.Linker {

	public class Driver {

#if FEATURE_ILLINK
		static readonly string _linker = "IL Linker";
#else
		static readonly string _linker = "Mono CIL Linker";
#endif

		public static int Main (string [] args)
		{
			return Execute (args);
		}

		public static int Execute (string[] args, ILogger customLogger = null)
		{
			if (args.Length == 0)
				Usage ("No parameters specified");

			try {

				Driver driver = new Driver (args);
				driver.Run (customLogger);

			} catch (Exception e) {
				Console.WriteLine ("Fatal error in {0}", _linker);
				Console.WriteLine (e);
				return 1;
			}

			return 0;
		}

		Queue<string> _queue;
		bool _needAddBypassNGenStep;

		public Driver (string [] args)
		{
			_queue = new Queue<string> (args);
		}

		bool HaveMoreTokens ()
		{
			return _queue.Count > 0;
		}

		public void Run (ILogger customLogger = null)
		{
			Pipeline p = GetStandardPipeline ();
			using (LinkContext context = GetDefaultContext (p)) {
				if (customLogger != null)
					context.Logger = customLogger;

				I18nAssemblies assemblies = I18nAssemblies.All;
				var custom_steps = new List<string> ();
				var excluded_features = new HashSet<string> ();
				bool dumpDependencies = false;

				bool resolver = false;
				while (HaveMoreTokens ()) {
					string token = GetParam ();
					if (token.Length < 2)
						Usage ("Option is too short");

					if (!(token [0] == '-' || token [1] == '/'))
						Usage ("Expecting an option, got instead: " + token);

					if (token [0] == '-' && token [1] == '-') {

						if (token.Length < 3)
							Usage ("Option is too short");

						if (token == "--skip-unresolved") {
							bool ignoreUnresolved = bool.Parse (GetParam ());
							context.IgnoreUnresolved = ignoreUnresolved;
							context.Resolver.IgnoreUnresolved = ignoreUnresolved;
							continue;
						}

						if (token == "--verbose") {
							context.LogMessages = true;
							continue;
						}

						if (token == "--dependencies-file") {
							context.Tracer.DependenciesFileName = GetParam ();
							continue;
						}

						if (token == "--dump-dependencies") {
							dumpDependencies = true;
							continue;
						}

						if (token == "--reduced-tracing") {
							context.EnableReducedTracing = bool.Parse (GetParam ());
							continue;
						}

						if (token == "--used-attrs-only") {
							context.KeepUsedAttributeTypesOnly = bool.Parse (GetParam ());
							continue;
						}

						if (token == "--strip-security") {
							if (bool.Parse (GetParam ()))
								p.AddStepBefore (typeof (MarkStep), new RemoveSecurityStep ());
							continue;
						}

						if (token == "--strip-resources") {
							context.StripResources = bool.Parse (GetParam ());
							continue;
						}

						if (token == "--exclude-feature") {
							var name = GetParam ();
							if (!excluded_features.Contains (name))
								excluded_features.Add (name);
							continue;
						}

						switch (token [2]) {
						case 'v':
							Version ();
							break;
						case 'a':
							About ();
							break;
						default:
							Usage (null);
							break;
						}
					}

					switch (token [1]) {
					case 'd':
						DirectoryInfo info = new DirectoryInfo (GetParam ());
						context.Resolver.AddSearchDirectory (info.FullName);
							break;
					case 'o':
						context.OutputDirectory = GetParam ();
						break;
					case 'c':
						context.CoreAction = ParseAssemblyAction (GetParam ());
						break;
					case 'u':
						context.UserAction = ParseAssemblyAction (GetParam ());
						break;
					case 'p':
						AssemblyAction action = ParseAssemblyAction (GetParam ());
						context.Actions [GetParam ()] = action;
						break;
					case 's':
						custom_steps.Add (GetParam ());
						break;
					case 't':
						context.KeepTypeForwarderOnlyAssemblies = true;
						break;
					case 'x':
						foreach (string file in GetFiles (GetParam ()))
							p.PrependStep (new ResolveFromXmlStep (new XPathDocument (file)));
						resolver = true;
						break;
					case 'r':
					case 'a':
						var rootVisibility = (token [1] == 'r')
							? ResolveFromAssemblyStep.RootVisibility.PublicAndFamily
							: ResolveFromAssemblyStep.RootVisibility.Any;
						foreach (string file in GetFiles (GetParam ()))
							p.PrependStep (new ResolveFromAssemblyStep (file, rootVisibility));
						resolver = true;
						break;
					case 'i':
						foreach (string file in GetFiles (GetParam ()))
							p.PrependStep (new ResolveFromXApiStep (new XPathDocument (file)));
						resolver = true;
						break;
					case 'l':
						assemblies = ParseI18n (GetParam ());
						break;
					case 'm':
						context.SetParameter (GetParam (), GetParam ());
						break;
					case 'b':
						context.LinkSymbols = bool.Parse (GetParam ());
						break;
					case 'g':
						if (!bool.Parse (GetParam ()))
							p.RemoveStep (typeof (RegenerateGuidStep));
						break;
					case 'z':
						if (!bool.Parse (GetParam ()))
							p.RemoveStep (typeof (BlacklistStep));
						break;
					case 'v':
						context.KeepMembersForDebugger = bool.Parse (GetParam ());
						break;
					default:
						Usage ("Unknown option: `" + token [1] + "'");
						break;
					}
				}

				if (!resolver)
					Usage ("No resolver was created (use -x, -a or -i)");

				if (dumpDependencies)
					context.Tracer.Start ();

				foreach (string custom_step in custom_steps)
					AddCustomStep (p, custom_step);

				p.AddStepAfter (typeof (LoadReferencesStep), new LoadI18nAssemblies (assemblies));

				if (_needAddBypassNGenStep) {
					p.AddStepAfter (typeof (SweepStep), new AddBypassNGenStep ());
				}

				if (excluded_features.Count > 0) {
					var excluded = new string [excluded_features.Count];
					excluded_features.CopyTo (excluded);
					context.ExcludedFeatures = excluded;
				}

				try {
					p.Process (context);
				}
				finally {
					if (dumpDependencies)
						context.Tracer.Finish ();
				}
			}
		}

		protected static void AddCustomStep (Pipeline pipeline, string arg)
		{
			int pos = arg.IndexOf (":");
			if (pos == -1) {
				pipeline.AppendStep (ResolveStep (arg));
				return;
			}

			string [] parts = arg.Split (':');
			if (parts.Length != 2)
				Usage ("Step is specified as TYPE:STEP");

			if (parts [0].IndexOf (",") > -1)
				pipeline.AddStepBefore (FindStep (pipeline, parts [1]), ResolveStep (parts [0]));
			else if (parts [1].IndexOf (",") > -1)
				pipeline.AddStepAfter (FindStep (pipeline, parts [0]), ResolveStep (parts [1]));
			else
				Usage ("No comma separator in TYPE or STEP");
		}

		static Type FindStep (Pipeline pipeline, string name)
		{
			foreach (IStep step in pipeline.GetSteps ()) {
				Type t = step.GetType ();
				if (t.Name == name)
					return t;
			}

			return null;
		}

		static IStep ResolveStep (string type)
		{
			Type step = Type.GetType (type, false);
			if (step == null)
				Usage (String.Format ("Step type '{0}' not found.", type));
			if (!typeof (IStep).IsAssignableFrom (step))
				Usage (String.Format ("Step type '{0}' does not implement IStep interface.", type));
			return (IStep) Activator.CreateInstance (step);
		}

		static string [] GetFiles (string param)
		{
			if (param.Length < 1 || param [0] != '@')
				return new string [] {param};

			string file = param.Substring (1);
			return ReadLines (file);
		}

		static string [] ReadLines (string file)
		{
			var lines = new List<string> ();
			using (StreamReader reader = new StreamReader (file)) {
				string line;
				while ((line = reader.ReadLine ()) != null)
					lines.Add (line);
			}
			return lines.ToArray ();
		}

		protected static I18nAssemblies ParseI18n (string str)
		{
			I18nAssemblies assemblies = I18nAssemblies.None;
			string [] parts = str.Split (',');
			foreach (string part in parts)
				assemblies |= (I18nAssemblies) Enum.Parse (typeof (I18nAssemblies), part.Trim (), true);

			return assemblies;
		}


		AssemblyAction ParseAssemblyAction (string s)
		{
			var assemblyAction = (AssemblyAction)Enum.Parse(typeof(AssemblyAction), s, true);
			if ((assemblyAction == AssemblyAction.AddBypassNGen) || (assemblyAction == AssemblyAction.AddBypassNGenUsed)) {
				_needAddBypassNGenStep = true;
			}
			return assemblyAction;
		}

		string GetParam ()
		{
			if (_queue.Count == 0)
				Usage ("Expecting a parameter");

			return _queue.Dequeue ();
		}

		static LinkContext GetDefaultContext (Pipeline pipeline)
		{
			LinkContext context = new LinkContext (pipeline);
			context.CoreAction = AssemblyAction.Skip;
			context.UserAction = AssemblyAction.Link;
			context.OutputDirectory = "output";
			return context;
		}

		static void Usage (string msg)
		{
			Console.WriteLine (_linker);
			if (msg != null)
				Console.WriteLine ("Error: " + msg);
#if FEATURE_ILLINK
			Console.WriteLine ("illink [options] -x|-a|-i file");
#else
			Console.WriteLine ("monolinker [options] -x|-a|-i file");
#endif

			Console.WriteLine ("   --about             About the {0}", _linker);
			Console.WriteLine ("   --version           Print the version number of the {0}", _linker);
			Console.WriteLine ("   --skip-unresolved   Ignore unresolved types, methods, and assemblies (true or false)");
			Console.WriteLine ("   --verbose           Log messages indicating progress and warnings");
			Console.WriteLine ("   --dependencies-file Specify the dependencies file path, if unset the default path is used: <output directory>/linker-dependencies.xml.gz");
			Console.WriteLine ("   --dump-dependencies Dump dependencies for the linker analyzer tool");
			Console.WriteLine ("   --reduced-tracing   Reduces dependency output related to assemblies that will not be modified");
			Console.WriteLine ("   --used-attrs-only   Attributes on types, methods, etc will be removed if the attribute type is not used");
			Console.WriteLine ("   --strip-security    In linked assemblies, attributes on assemblies, types, and methods related to security will be removed");
			Console.WriteLine ("   --strip-resources   Remove link xml resources that were processed (true or false), default to true");
			Console.WriteLine ("   --exclude-feature   Any code which has feature-name dependency will be removed");
			Console.WriteLine ("   -out                Specify the output directory, default to `output'");
			Console.WriteLine ("   -c                  Action on the core assemblies, skip, copy, copyused, addbypassngen, addbypassngenused or link, default to skip");
			Console.WriteLine ("   -u                  Action on the user assemblies, skip, copy, copyused, addbypassngen, addbypassngenused or link, default to link");
			Console.WriteLine ("   -p                  Action per assembly");
			Console.WriteLine ("   -s                  Add a new step to the pipeline.");
			Console.WriteLine ("   -t                  Keep assemblies in which only type forwarders are referenced.");
			Console.WriteLine ("   -d                  Add a directory where the linker will look for assemblies");
			Console.WriteLine ("   -b                  Generate debug symbols for each linked module (true or false)");
			Console.WriteLine ("   -g                  Generate a new unique guid for each linked module (true or false)");
			Console.WriteLine ("   -v                  Keep members needed by debugger (true or false)");
			Console.WriteLine ("   -l                  List of i18n assemblies to copy to the output directory");
			Console.WriteLine ("                         separated with a comma: none,all,cjk,mideast,other,rare,west");
			Console.WriteLine ("                         default is all");
			Console.WriteLine ("   -x                  Link from an XML descriptor");
			Console.WriteLine ("   -a                  Link from a list of assemblies");
			Console.WriteLine ("   -r                  Link from a list of assemblies using roots visible outside of the assembly");
			Console.WriteLine ("   -i                  Link from an mono-api-info descriptor");
			Console.WriteLine ("   -z                  Include default preservations (true or false), default to true");
			Console.WriteLine ("");

			Environment.Exit (1);
		}

		static void Version ()
		{
			Console.WriteLine ("{0} Version {1}",
				_linker,
				System.Reflection.Assembly.GetExecutingAssembly ().GetName ().Version);

			Environment.Exit(1);
		}

		static void About ()
		{
			Console.WriteLine ("For more information, visit the project Web site");
			Console.WriteLine ("   http://www.mono-project.com/");

			Environment.Exit(1);
		}

		static Pipeline GetStandardPipeline ()
		{
			Pipeline p = new Pipeline ();
			p.AppendStep (new LoadReferencesStep ());
			p.AppendStep (new BlacklistStep ());
			p.AppendStep (new PreserveDependencyLookupStep ());
			p.AppendStep (new TypeMapStep ());
			p.AppendStep (new MarkStep ());
			p.AppendStep (new SweepStep ());
			p.AppendStep (new CleanStep ());
			p.AppendStep (new RegenerateGuidStep ());
			p.AppendStep (new OutputStep ());
			return p;
		}
	}
}

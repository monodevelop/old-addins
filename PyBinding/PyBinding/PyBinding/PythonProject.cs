// PythonProject.cs
//
// Copyright (c) 2008 Christian Hergert <chris@dronelabs.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Text;
using System.Xml;

using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Gettext;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;
using MonoDevelop.Projects.CodeGeneration;

using PyBinding.Compiler;

namespace PyBinding
{
	public class PythonProject : Project
	{
		static readonly string s_ProjectType = "Python";
		
		public override string ProjectType {
			get {
				return s_ProjectType;
			}
		}
		
		public PythonProject ()
		{
		}
		
		public PythonProject (string languageName, 
		                      ProjectCreateInformation info,
		                      XmlElement projectOptions)
		{
			PythonConfiguration defaultConfig;
			string binPath;
			
			if (!String.Equals (s_ProjectType, languageName)) {
				throw new ArgumentException ("Not Python Project");
			}
			
			if (info != null) {
				binPath = info.BinPath;
				this.Name = info.ProjectName;
			}
			else {
				binPath = ".";
			}
			
			// Setup our Debug configuration
			defaultConfig = CreateConfiguration ("Debug") as PythonConfiguration;
			this.Configurations.Add (defaultConfig);
			
			// Setup our Release configuration
			defaultConfig = CreateConfiguration ("Release") as PythonConfiguration;
			defaultConfig.Optimize = true;
			this.Configurations.Add (defaultConfig);
			
			// Setup proper paths for all configurations
			foreach (PythonConfiguration config in this.Configurations) {
				config.OutputDirectory = Path.Combine (binPath, config.Name);
			}
		}
		
		public override SolutionItemConfiguration CreateConfiguration (string configName)
		{
			PythonConfiguration config = new PythonConfiguration ();
			config.Name = configName;
			return config;
		}
		
		protected override BuildResult DoBuild (IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			BuildResult         result;
			PythonConfiguration config;
			
			config = (PythonConfiguration) GetConfiguration (configuration);
			result = new BuildResult ();
			
			if (config.Runtime != null && config.Runtime.Compiler != null) {
				IPythonCompiler compiler = config.Runtime.Compiler;
				
				foreach (ProjectFile projectFile in Files) {
					if (projectFile.BuildAction != BuildAction.Compile)
						continue;
					
					compiler.Compile (this, projectFile.FilePath, config, result);
				}
			}
			
			return result;
		}

		protected override bool OnGetCanExecute (MonoDevelop.Projects.ExecutionContext context, ConfigurationSelector solutionConfiguration)
		{
			PythonConfiguration config = (PythonConfiguration) GetConfiguration (solutionConfiguration);
			return config.Runtime != null && context.ExecutionHandler.CanExecute (new PythonExecutionCommand (config));
		}

		
		protected override void DoExecute (IProgressMonitor monitor,
		                                   ExecutionContext context,
		                                   ConfigurationSelector configuration)
		{
			PythonConfiguration config;
			IConsole console;
			
			config = (PythonConfiguration) GetConfiguration (configuration);
			
			// Make sure we have a module to execute
			if (config.Runtime == null || String.IsNullOrEmpty (config.Module)) {
				MessageService.ShowMessage ("No target module specified!");
				return;
			}
			
			monitor.Log.WriteLine ("Running project...");
			
			// Create a console, external if needed
			if (config.ExternalConsole) {
				console = context.ExternalConsoleFactory.CreateConsole (!config.PauseConsoleOutput);
			}
			else {
				console = context.ConsoleFactory.CreateConsole (!config.PauseConsoleOutput);
			}
			
			AggregatedOperationMonitor operationMonitor = new AggregatedOperationMonitor (monitor);
			
			try {
				PythonExecutionCommand cmd = new PythonExecutionCommand (config);
				
				if (!context.ExecutionHandler.CanExecute (cmd)) {
					monitor.ReportError ("The selected execution mode is not supported for Python projects.", null);
					return;
				}
				
				IProcessAsyncOperation op = context.ExecutionHandler.Execute (cmd, console);
				operationMonitor.AddOperation (op);
				op.WaitForCompleted ();
				
				monitor.Log.WriteLine ("The operation exited with code: {0}", op.ExitCode);
			}
			catch (Exception ex) {
				monitor.ReportError ("Cannot execute \"" + config.Runtime.Path + "\"", ex);
			}
			finally {
				operationMonitor.Dispose ();
				console.Dispose ();
			}
		}

		public override bool IsCompileable (string fileName)
		{
			return Path.GetExtension (fileName) == ".py";
		}
	}
}

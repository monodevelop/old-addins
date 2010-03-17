using System;
using System.Collections.Generic;

using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Tasks;

namespace MonoDevelop.CodeAnalysis.Gui {
	
	/// <summary>
	/// Class that interacts with MonoDevelop GUI.
	/// </summary>
	static class ResultsReporter {
		private static double work_complete = 0.0;
		
		/// <value>
		/// Status bar complete work amount (0 to 1).
		/// </value>
		public static double WorkComplete {
			get { return work_complete; }
			set {
				work_complete = value;
				DispatchService.GuiDispatch (delegate {
					IdeApp.Workbench.StatusBar.SetProgressFraction (value);
				});
			}
		}		
		
		/// <summary>
		/// Informs the GUI that analysis has started.
		/// </summary>
		public static void AnalysisStarted (string entryName)
		{
			DispatchService.GuiDispatch (delegate {
				ResetProgressBar ();
				IdeApp.Workbench.StatusBar.BeginProgress (AddinCatalog.GetString ("Analyzing {0}...", entryName));
				TaskService.Errors.Clear ();
			});
		}		

		/// <summary>
		/// Informs the GUI that analysis has finished.
		/// </summary>
		public static void AnalysisFinished ()
		{
			DispatchService.GuiDispatch (delegate {
				IdeApp.Workbench.StatusBar.EndProgress ();
				IdeApp.Workbench.StatusBar.ShowMessage (AddinCatalog.GetString ("Analysis has finished."));
				ResetProgressBar ();
			});
		}		
		
		/// <summary>
		/// Reports an error to GUI.
		/// </summary>
		public static void ReportError (CodeAnalysisException ex)
		{
			DispatchService.GuiDispatch (delegate {
				MessageService.ShowError (ex.Message, ex.StackTrace);
			});
		}

		/// <summary>
		/// Displays violation list in GUI.
		/// </summary>
		public static void ReportViolations (IEnumerable<IViolation> violations)
		{
			TaskService.ShowErrors ();
			
			foreach (IViolation v in violations)
				AddViolation (v);
		}

		/// <summary>
		/// Adds a violation to GUI (currently, Task View)
		/// </summary>
		private static void AddViolation (IViolation v)
		{
			// TODO: replace Task View with our own GUI			
			TaskSeverity type = TaskSeverity.Warning;
			
			if ((v.Severity == Severity.Critical || v.Severity == Severity.High)
			    && (v.Confidence == Confidence.Total || v.Confidence == Confidence.High))
				type = TaskSeverity.Error;
			
			string text = v.Problem + Environment.NewLine + v.Solution;

			// TODO: handle Location
			Task task = new Task (v.Location.File, text, v.Location.Column, v.Location.Line, type, TaskPriority.Normal, MainAnalyzer.CurrentProject);
			TaskService.Errors.Add (task);				  
		}
		
		static void ResetProgressBar ()
		{
			WorkComplete = 0.0;
		}
	}
}

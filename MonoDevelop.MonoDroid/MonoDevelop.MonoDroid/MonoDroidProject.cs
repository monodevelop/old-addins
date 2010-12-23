// 
// MonoDroidProject.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2010 Novell, Inc. (http://www.novell.com)
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
using System.Xml;
using MonoDevelop.Core;
using MonoDevelop.Core.Serialization;
using MonoDevelop.Projects;
using System.IO;
using System.Collections.Generic;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using MonoDevelop.Ide.Tasks;

namespace MonoDevelop.MonoDroid
{

	public class MonoDroidProject : DotNetProject
	{
		internal const string FX_MONODROID = "MonoDroid";
		
		#region Properties
		
		[ItemProperty ("AndroidApplication")]
		string androidApplicationUnparsed;
		
		[ProjectPathItemProperty ("AndroidResgenFile")]
		string androidResgenFile;
		
		[ItemProperty ("AndroidResgenClass")]
		string androidResgenClass;
		
		[ProjectPathItemProperty ("AndroidManifest")]
		string androidManifest;
		
		[ItemProperty ("MonoDroidResourcePrefix")]
		string monoDroidResourcePrefix;
		
		public override string ProjectType {
			get { return "MonoDroid"; }
		}
		
		public override bool IsLibraryBasedProjectType {
			get { return true; }
		}
		
		bool isAndroidApplication;
			
		public bool IsAndroidApplication {
			get { return isAndroidApplication; }
			set {
				if (value == isAndroidApplication)
					return;
				isAndroidApplication = value;
				androidApplicationUnparsed = value.ToString ();
				NotifyModified ("IsAndroidApplication");
			}
		}
		
		public FilePath AndroidResgenFile {
			get { return androidResgenFile; }
			set {
				if (value == "")
					value = null;
				if (value == androidResgenFile)
					return;
				androidResgenFile = value;
				NotifyModified ("AndroidResgenFile");
			}
		}
		
		public FilePath AndroidResgenClass {
			get { return androidResgenClass; }
			set {
				if (value == "")
					value = null;
				if (value == androidResgenClass)
					return;
				androidResgenClass = value;
				NotifyModified ("AndroidResgenClass");
			}
		}
		
		public FilePath AndroidManifest {
			get { return androidManifest; }
			set {
				if (value == "")
					value = null;
				if (value == androidManifest)
					return;
				androidManifest = value;
				NotifyModified ("AndroidManifest");
			}
		}
		
		public string MonoDroidResourcePrefix {
			get { return monoDroidResourcePrefix; }
			set {
				if (value == "")
					value = null;
				if (value == monoDroidResourcePrefix)
					return;
				monoDroidResourcePrefix = value;
				resPrefixes = null;
				NotifyModified ("MonoDroidResourcePrefix");
			}
		}
		
		#endregion
		
		#region Constructors
		
		public MonoDroidProject ()
		{
			Init ();
		}
		
		public MonoDroidProject (string languageName)
			: base (languageName)
		{
			Init ();
		}
		
		public MonoDroidProject (string languageName, ProjectCreateInformation info, XmlElement projectOptions)
			: base (languageName, info, projectOptions)
		{
			Init ();
			
			var androidResgenFileAtt = projectOptions.Attributes ["AndroidResgenFile"];
			if (androidResgenFileAtt != null)
				this.androidResgenFile = MakePathNative (androidResgenFileAtt.Value);
			
			var androidResgenClassAtt = projectOptions.Attributes ["AndroidResgenClass"];
			if (androidResgenClassAtt != null)
				this.androidResgenClass = androidResgenClassAtt.Value;
			
			var androidApplicationAtt = projectOptions.Attributes ["AndroidApplication"];
			if (androidApplicationAtt != null) {
				this.IsAndroidApplication = bool.Parse (androidApplicationAtt.Value);
			}
			
			var androidManifestAtt = projectOptions.Attributes ["AndroidManifest"];
			if (androidManifestAtt != null) {
				this.IsAndroidApplication = true;
				this.AndroidManifest = MakePathNative (androidManifestAtt.Value);
			}
			
			monoDroidResourcePrefix = "Resources";
		}
		
		string MakePathNative (string path)
		{
			char c = Path.DirectorySeparatorChar == '\\'? '/' : '\\'; 
			return path.Replace (c, Path.DirectorySeparatorChar);
		}
		
		void Init ()
		{
			//set parameters to ones required for MonoDroid build
			TargetFramework = Runtime.SystemAssemblyService.GetTargetFramework (FX_MONODROID);
			
			MonoDroidFramework.DeviceManager.IncrementOpenProjectCount ();
		}
		
		public override SolutionItemConfiguration CreateConfiguration (string name)
		{
			var conf = new MonoDroidProjectConfiguration (name);
			conf.CopyFrom (base.CreateConfiguration (name));
			return conf;
		}
		
		public override bool SupportsFormat (FileFormat format)
		{
			return format.Id == "MSBuild10";
		}
		
		protected override void OnEndLoad ()
		{
			// Migration logic for AndroidManifest element is run if it exists and AndroidApplication is empty
			// In order to do this, we don't let the deserializer handle AndroidApplicatio, but parse it here
			if (!string.IsNullOrEmpty (androidApplicationUnparsed)) {
				isAndroidApplication = string.Equals (androidApplicationUnparsed, "true", StringComparison.OrdinalIgnoreCase);
			}
			else if (!string.IsNullOrEmpty (androidManifest)) {
				androidApplicationUnparsed = "True";
				isAndroidApplication = true;
				if (!File.Exists (androidManifest))
					androidManifest = null;
			}
			
			base.OnEndLoad ();
		}

		#endregion
		
		#region Execution
		
		/// <summary>
		/// User setting of device for running app in simulator. Null means use default.
		/// </summary>
		public string GetDeviceTarget (MonoDroidProjectConfiguration conf)
		{
			//FIXME: do we really want this to be per-project/per-configuration? or should it be a global MD setting?
			var device = UserProperties.GetValue<string> (GetDeviceTargetKey (conf));
			if (string.IsNullOrEmpty (device))
				return null;
			return device;
		}
		
		public void SetDeviceTarget (MonoDroidProjectConfiguration conf, string value)
		{
			UserProperties.SetValue<string> (GetDeviceTargetKey (conf), value);
		}
		
		string GetDeviceTargetKey (MonoDroidProjectConfiguration conf)
		{
			return "AndroidDeviceId-" + conf.Id;
		}
		
		bool HackGetUserAssemblyPaths = false;
		
		//HACK: base.GetUserAssemblyPaths depends on GetOutputFileName being an assembly
		new IList<string> GetUserAssemblyPaths (ConfigurationSelector configuration)
		{
			try {
				HackGetUserAssemblyPaths = true;
				return base.GetUserAssemblyPaths (configuration);
			} finally {
				HackGetUserAssemblyPaths = false;
			}
		}
		
		public override FilePath GetOutputFileName (ConfigurationSelector configuration)
		{
			if (!IsAndroidApplication)
				return base.GetOutputFileName (configuration);
				
			if (HackGetUserAssemblyPaths)
				return base.GetOutputFileName (configuration);
			
			var cfg = GetConfiguration (configuration);
			if (cfg == null)
				return FilePath.Null;
			return cfg.ApkPath;
		}
		
		protected override bool CheckNeedsBuild (ConfigurationSelector configuration)
		{
			var apkBuildTime = GetLastBuildTime (configuration);
			if (apkBuildTime == DateTime.MinValue)
				return true;
			
			var dllBuildTime = base.GetLastBuildTime (configuration);
			if (dllBuildTime == DateTime.MinValue || dllBuildTime > apkBuildTime)
				return true;
			
			if (base.CheckNeedsBuild (configuration))
				return true;
			
			if  (Files.Any (file => file.BuildAction == MonoDroidBuildAction.AndroidResource
					&& File.Exists (file.FilePath) && File.GetLastWriteTime (file.FilePath) > apkBuildTime))
				return true;
				
			var conf = GetConfiguration (configuration);
			var manifestFile = GetManifestFileName (conf);
			if (!manifestFile.IsNullOrEmpty && File.Exists (manifestFile)
				&& File.GetLastWriteTime (manifestFile) > apkBuildTime)
				return true;
			
			return false;
		}
		
		protected override ExecutionCommand CreateExecutionCommand (ConfigurationSelector configSel,
		                                                            DotNetProjectConfiguration configuration)
		{
			var conf = (MonoDroidProjectConfiguration) configuration;
			
			return new MonoDroidExecutionCommand (conf.PackageName,
				conf.ApkSignedPath, TargetRuntime, TargetFramework, conf.DebugMode) {
				UserAssemblyPaths = GetUserAssemblyPaths (configSel)
			};
		}
		
		protected override bool OnGetCanExecute (MonoDevelop.Projects.ExecutionContext context, ConfigurationSelector config)
		{
			var cfg = GetConfiguration (config);
			if (cfg == null)
				return false;
			var cmd = CreateExecutionCommand (config, cfg);
			return context.ExecutionHandler.CanExecute (cmd);
		}
		
		protected override void OnExecute (IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configSel)
		{
			var conf = (MonoDroidProjectConfiguration) GetConfiguration (configSel);
			
			if (NeedsBuilding (configSel)) {
				monitor.ReportError (
					GettextCatalog.GetString ("MonoDroid projects must be built before uploading"), null);
				return;
			}
			
			var manifestFile = conf.ObjDir.Combine ("android", "AndroidManifest.xml");
			if (!File.Exists (manifestFile)) {
				monitor.ReportError ("Intermediate manifest file is missing", null);
				return;
			}
			
			var manifest = AndroidAppManifest.Load (manifestFile);
			var activity = manifest.GetLaunchableActivityName ();
			if (string.IsNullOrEmpty (activity)) {
				monitor.ReportError ("Application does not contain a launchable activity", null);
				return;
			}
			activity = manifest.PackageName + "/" + activity;
			
			IConsole console = null;
			var opMon = new AggregatedOperationMonitor (monitor);
			try {
				var handler = context.ExecutionHandler as MonoDroidExecutionHandler;
				bool useHandlerDevice = handler != null && handler.DeviceTarget != null;
				
				AndroidDevice device = null;
				
				if (useHandlerDevice) {
					device = handler.DeviceTarget;
				} else {
					var deviceId = GetDeviceTarget (conf);
					if (deviceId != null)
						device = MonoDroidFramework.DeviceManager.GetDevice (deviceId);
					if (device == null)
						SetDeviceTarget (conf, null);
				}
				
				var uploadOp = MonoDroidUtility.SignAndUpload (monitor, this, configSel, false, ref device);
				
				//user cancelled device selection
				if (device == null)
					return;
				
				opMon.AddOperation (uploadOp);
				uploadOp.WaitForCompleted ();
				
				if (!uploadOp.Success || monitor.IsCancelRequested)
					return;
				
				//successful, persist the device choice
				if (!useHandlerDevice)
					SetDeviceTarget (conf, device.ID);
				
				var command = (MonoDroidExecutionCommand) CreateExecutionCommand (configSel, conf);
				command.Device = device;
				command.Activity = activity;
				
				//FIXME: would be nice to skip this if it's a debug handler, which will set another value later
				var propOp = MonoDroidFramework.Toolbox.SetProperty (device, "debug.mono.extra", string.Empty);
				opMon.AddOperation (propOp);
				propOp.WaitForCompleted ();
				if (!propOp.Success) {
					monitor.ReportError (GettextCatalog.GetString ("Count not clear debug settings on device"),
						new Exception (propOp.GetOutput ()));
					return;
				}
				
				console = context.ConsoleFactory.CreateConsole (false);
				var executeOp = context.ExecutionHandler.Execute (command, console);
				opMon.AddOperation (executeOp);
				executeOp.WaitForCompleted ();
				
			} finally {
				opMon.Dispose ();
				if (console != null)
					console.Dispose ();
			}
		}
		
		public bool PackageNeedsSigning (MonoDroidProjectConfiguration conf)
		{
			return !File.Exists (conf.ApkSignedPath) ||
				File.GetLastWriteTime (conf.ApkSignedPath) < File.GetLastWriteTime (conf.ApkPath);
		}
		
		public IAsyncOperation SignPackage (ConfigurationSelector configSel)
		{
			TaskService.Errors.ClearByOwner (this);
			
			var monitor = IdeApp.Workbench.ProgressMonitors.GetBuildProgressMonitor ();
			
			DispatchService.ThreadDispatch (delegate {
            	 SignPackageAsync (monitor, configSel);
            }, null);
			
			return monitor.AsyncOperation;
		}
		
		void SignPackageAsync (IProgressMonitor monitor, ConfigurationSelector configSel)
		{
			monitor.BeginTask ("Signing package", 0);
			
			BuildResult result = null;
			try {
				result = this.OnRunTarget (monitor, "SignAndroidPackage", configSel);
				if (result.ErrorCount > 0) {
					monitor.ReportError ("Signing failed", null);
				}
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Signing failed."), ex);
			}
			DispatchService.GuiDispatch (delegate {
				SignPackageDone (monitor, result); // disposes the monitor
			});
		}
		
		void SignPackageDone (IProgressMonitor monitor, BuildResult result)
		{
			monitor.EndTask ();
			
			if (result != null && result.Errors.Count > 0) {
				var tasks = new Task [result.Errors.Count];
				for (int n = 0; n < tasks.Length; n++) {
					tasks [n] = new Task (result.Errors [n], this);
				}
				TaskService.Errors.AddRange (tasks);
				TaskService.ShowErrors ();
			}
			
			monitor.Dispose ();
		}
		
		#endregion
		
		#region Platform properties
		
		public override bool SupportsFramework (MonoDevelop.Core.Assemblies.TargetFramework framework)
		{
			return framework.Id == FX_MONODROID;
		}
		
		#endregion
		
		#region Resgen
		
		protected override void OnFileChangedInProject (ProjectFileEventArgs e)
		{
			base.OnFileChangedInProject (e);
			if (Loading)
				return;
			
			if (e.ProjectFile.BuildAction == MonoDroidBuildAction.AndroidResource)
				QueueResgenUpdate ();
		}
		
		protected override void OnFileRemovedFromProject (ProjectFileEventArgs e)
		{
			base.OnFileRemovedFromProject (e);
			if (Loading)
				return;
			
			if (e.ProjectFile.BuildAction == MonoDroidBuildAction.AndroidResource)
				QueueResgenUpdate ();
			//clear the manifest element if the file is removed
			else if (!AndroidManifest.IsNullOrEmpty && e.ProjectFile.FilePath == AndroidManifest)
				AndroidManifest = null;
		}
		
		protected override void OnFileRenamedInProject (ProjectFileRenamedEventArgs e)
		{
			base.OnFileRenamedInProject (e);
			if (Loading)
				return;
			
			if (e.ProjectFile.BuildAction == MonoDroidBuildAction.AndroidResource)
				QueueResgenUpdate ();
			//if renaming the file to "AndroidManifest.xml", and the manifest element is not in use, set it as a convenience
			else if (AndroidManifest.IsNullOrEmpty && e.NewName.ToRelative (BaseDirectory) == "AndroidManifest.xml")
				AndroidManifest = e.NewName;
			//track manifest file renames or things will break
			else if (AndroidManifest == e.OldName)
				AndroidManifest = e.NewName;
		}
		
		protected override void OnFileAddedToProject (ProjectFileEventArgs e)
		{
			base.OnFileAddedToProject (e);
			if (Loading)
				return;
			
			if (e.ProjectFile.BuildAction == MonoDroidBuildAction.AndroidResource)
				QueueResgenUpdate ();
			//if adding a file called AndroidManifest.xml, and the manifest element is not in use, set it as a convenience
			//TODO: is it worth coping with LogicalNames?
			else if (AndroidManifest.IsNullOrEmpty && e.ProjectFile.FilePath.ToRelative (BaseDirectory) == "AndroidManifest.xml")
				AndroidManifest = e.ProjectFile.FilePath;
		}
		
		protected override void OnFilePropertyChangedInProject (ProjectFileEventArgs e)
		{
			base.OnFilePropertyChangedInProject (e);
			if (Loading)
				return;
			
			if (e.ProjectFile.BuildAction == MonoDroidBuildAction.AndroidResource)
				QueueResgenUpdate ();
		}
		
		bool resgenUpdateQueued;
		object resgenLockObj = new object ();
		//this is fired off with a timeout, so it's effectively rate-limited
		//if multiple changes take place at once
		void QueueResgenUpdate ()
		{
			lock (resgenLockObj) {
				if (resgenUpdateQueued)
					return;
				resgenUpdateQueued = true;
				GLib.Timeout.Add (3000, delegate {
					lock (resgenLockObj)
						resgenUpdateQueued = false;
					using (var monitor = IdeApp.Workbench.ProgressMonitors.GetBuildProgressMonitor ())
						RunTarget (monitor, "UpdateAndroidResources", IdeApp.Workspace.ActiveConfiguration);
					return false;
				});
			}
		}
		
		#endregion
		
		protected override IList<string> GetCommonBuildActions ()
		{
			return new string[] {
				BuildAction.Compile,
				MonoDroidBuildAction.AndroidResource,
				BuildAction.None,
			};
		}
		
		public new MonoDroidProjectConfiguration GetConfiguration (ConfigurationSelector configuration)
		{
			return (MonoDroidProjectConfiguration) base.GetConfiguration (configuration);
		}
		
		public override string GetDefaultBuildAction (string fileName)
		{
			var baseAction = base.GetDefaultBuildAction (fileName);
			if (baseAction == BuildAction.Compile)
				return baseAction;
			
			var parentDir = ((FilePath)fileName).ToRelative (BaseDirectory).ParentDirectory;
			if (!parentDir.IsNullOrEmpty) {
				var parentOfParentDir = parentDir.ParentDirectory;
				if (!parentOfParentDir.IsNullOrEmpty) {
					foreach (var prefix in MonoDroidResourcePrefixes)
						if (prefix == parentOfParentDir)
							return MonoDroidBuildAction.AndroidResource;
				}
			}
				
			return baseAction;
		}
		
		public IEnumerable<KeyValuePair<string,ProjectFile>> GetAndroidResources (string kind)
		{
			var alreadyReturned = new HashSet<string> ();
			var splitChars = new[] { '/' };
			foreach (var pf in Files) {
				if (pf.BuildAction != MonoDroidBuildAction.AndroidResource)
					continue;
				var id = GetAndroidResourceID (pf);
				var split = id.Split (splitChars, StringSplitOptions.RemoveEmptyEntries);
				if (split.Length != 2 || !split[0].StartsWith (kind))
					continue;
				id = Path.GetFileNameWithoutExtension (split[1]);
				if (alreadyReturned.Add (id))
					yield return new KeyValuePair<string, ProjectFile> (id, pf);
			}		
		}
		
		string GetAndroidResourceID (ProjectFile pf)
		{
			if (!string.IsNullOrEmpty (pf.ResourceId))
				return pf.ResourceId;
			var f = pf.ProjectVirtualPath.ToString ();
			foreach (var prefix in MonoDroidResourcePrefixes) {
				var s = prefix.ToString ();
				if (f.StartsWith (s)) {
					f = f.Substring (s.Length);
					break;
				}
			}
			return f.Replace ('\\', '/');
		}
		
		FilePath[] resPrefixes;
		
		FilePath[] MonoDroidResourcePrefixes {
			get {
				if (resPrefixes != null)
					return resPrefixes;
				
				if (string.IsNullOrEmpty (MonoDroidResourcePrefix))
					return (resPrefixes = new FilePath[] { "Resources" });
				
				var split = MonoDroidResourcePrefix.Split (new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				var list = new List<FilePath> ();
				for (int i = 0; i < split.Length; i++) {
					var s = split[i].Trim ();
					if (s.Length == 0)
						continue;
					list.Add (MakePathNative (s));
				}
				return (resPrefixes = list.ToArray ());
			}
		}
		
		AndroidPackageNameCache packageNameCache;
		
		public string GetPackageName (MonoDroidProjectConfiguration conf)
		{
			var pf = GetManifestFile (conf);

			//no manifest, use the same default package name as the MSBuild tasks do
			if (pf == null) {
				var name = conf.CompiledOutputName.FileNameWithoutExtension;
				return string.Format ("{0}.{0}", name.Replace (" ", "").ToLowerInvariant ());
			}

			if (packageNameCache == null)
				packageNameCache = new AndroidPackageNameCache (this);
			
			return packageNameCache.GetPackageName (pf.Name);
		}
		
		FilePath GetManifestFileName (MonoDroidProjectConfiguration conf)
		{
			if (conf != null && !conf.AndroidManifest.IsNullOrEmpty)
				return conf.AndroidManifest;
			return this.AndroidManifest;
		}
		
		public ProjectFile GetManifestFile (MonoDroidProjectConfiguration conf)
		{
			var manifestFile = GetManifestFileName (conf);
			if (manifestFile.IsNullOrEmpty)
				return null;
			return Files.GetFile (manifestFile);
		}
		
		public AndroidAppManifest AddManifest ()
		{
			if (AndroidManifest.IsNullOrEmpty)
				AndroidManifest = BaseDirectory.Combine ("Properties", "AndroidManifest.xml");
			if (!Directory.Exists (AndroidManifest.ParentDirectory))
				Directory.CreateDirectory (AndroidManifest.ParentDirectory);
			var manifest = AndroidAppManifest.Create (GetDefaultPackageName (), Name);
			manifest.WriteToFile (AndroidManifest);
			AddFile (AndroidManifest);
			return manifest;
		}
		
		public string GetPackageName (ConfigurationSelector conf)
		{
			return GetPackageName ((MonoDroidProjectConfiguration)GetConfiguration (conf));
		}
		
		string GetDefaultPackageName ()
		{
			string sanitized = SanitizeName (Name);
			if (sanitized.Length == 0)
				sanitized = "application";
			return sanitized + "." + sanitized;
		}
		
		static string SanitizeName (string name)
		{
			var sb = new StringBuilder ();
			foreach (char c in name)
				if (char.IsLetterOrDigit (c))
					sb.Append (char.ToLowerInvariant (c));
			return sb.ToString ();
		}
		
		bool disposed = false;
		
		public override void Dispose ()
		{
			lock (this) {
				if (disposed)
					return;
				disposed = true;
			}
			
			MonoDroidFramework.DeviceManager.DecrementOpenProjectCount ();
			
			if (packageNameCache != null) {
				packageNameCache.Dispose ();
				packageNameCache = null;
			}
			
			base.Dispose ();
		}
	}
	
	static class MonoDroidBuildAction
	{
		public static readonly string AndroidResource = "AndroidResource";
	}
}

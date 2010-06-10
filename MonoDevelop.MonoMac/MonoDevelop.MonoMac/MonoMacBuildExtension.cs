// 
// MonoMacBuildExtension.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2009-2010 Novell, Inc. (http://www.novell.com)
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
using System.Linq;
using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using MonoDevelop.Core.ProgressMonitoring;
using System.Xml;
using System.Text;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Mono.Addins;
using MonoDevelop.MacDev;
using Mono.Unix;
using MonoDevelop.MacDev.Plist;

namespace MonoDevelop.MonoMac
{
	
	public class MonoMacBuildExtension : ProjectServiceExtension
	{
		protected override BuildResult Build (IProgressMonitor monitor, SolutionEntityItem item, ConfigurationSelector configuration)
		{
			var proj = item as MonoMacProject;
			if (proj == null || proj.CompileTarget != CompileTarget.Exe)
				return base.Build (monitor, item, configuration);
			
			var conf = (MonoMacProjectConfiguration) configuration.GetConfiguration (item);
			var resDir = conf.AppDirectory.Combine ("Contents", "Resources");
			var appDir = conf.AppDirectory;
			
			//make sure the codebehind files are updated before building
			var res = MacBuildUtilities.UpdateCodeBehind (monitor, proj.CodeBehindGenerator, proj.Files);
			if (res.ErrorCount > 0)
				return res;
			
			res = res.Append (base.Build (monitor, item, configuration));
			if (res.ErrorCount > 0)
				return res;
			
			//copy exe, mdb, refs, copy-to-output, Content files to Resources
			var filesToCopy = GetCopyFiles (proj, configuration, conf).Where (NeedsBuilding).ToList ();
			if (filesToCopy.Count > 0) {
				monitor.BeginTask ("Copying resource files to app bundle", filesToCopy.Count);
				foreach (var f in filesToCopy) {
					f.EnsureOutputDirectory ();
					File.Copy (f.Input, f.Output, true);
					monitor.Log.WriteLine ("Copied {0}", f.Output.ToRelative (appDir));
					monitor.Step (1);
				}
				monitor.EndTask ();
			}
			
			if (!PropertyService.IsMac) {
				res.AddWarning ("Cannot compile xib files on non-Mac platforms");
			} else {
				//Interface Builder files
				if (res.Append (MacBuildUtilities.CompileXibFiles (monitor, proj.Files, resDir)).ErrorCount > 0)
					return res;
			}
			
			//info.plist
			var plistOut = conf.AppDirectory.Combine ("Contents", "Info.plist");
			var appInfoIn = proj.Files.GetFile (proj.BaseDirectory.Combine ("Info.plist"));
			if (new FilePair (proj.FileName, plistOut).NeedsBuilding () ||
			    	(appInfoIn != null && new FilePair (appInfoIn.FilePath, plistOut).NeedsBuilding ()))
				if (res.Append (MergeInfoPlist (monitor, proj, conf, appInfoIn, plistOut)).ErrorCount > 0)
					return res;
			
			//launch script
			var ls = conf.LaunchScript;
			if (!File.Exists (ls)) {
				if (!Directory.Exists (ls.ParentDirectory))
					Directory.CreateDirectory (ls.ParentDirectory);
				var src = AddinManager.CurrentAddin.GetFilePath ("MonoMacLaunchScript.sh");
				File.Copy (src, ls, true);
				var fi = new UnixFileInfo (ls);
				fi.FileAccessPermissions |= FileAccessPermissions.UserExecute
					| FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute;
			}
			
			//pkginfo
			var pkgInfo = conf.AppDirectory.Combine ("Contents", "PkgInfo");
			if (!File.Exists (pkgInfo))
				using (var f = File.OpenWrite (pkgInfo))
					f.Write (new byte [] { 0X41, 0X50, 0X50, 0X4C, 0x3f, 0x3f, 0x3f, 0x3f}, 0, 8); // "APPL???"
			
			return res;
		}
		
		BuildResult MergeInfoPlist (IProgressMonitor monitor, MonoMacProject proj, MonoMacProjectConfiguration conf, 
		                            ProjectFile template, FilePath plistOut)
		{
			return MacBuildUtilities.CreateMergedPlist (monitor, template, plistOut, (PlistDocument doc) => {
				var result = new BuildResult ();
				var dict = doc.Root as PlistDictionary;
				if (dict == null)
					doc.Root = dict = new PlistDictionary ();
				
				//required keys that the user is likely to want to modify
				SetIfNotPresent (dict, "CFBundleName", proj.Name);
				SetIfNotPresent (dict, "CFBundleIdentifier", "com.yourcompany." + proj.Name);
				SetIfNotPresent (dict, "CFBundleShortVersionString", proj.Version);
				SetIfNotPresent (dict, "CFBundleVersion", "1");
				SetIfNotPresent (dict, "LSMinimumSystemVersion", "10.6");
				SetIfNotPresent (dict, "CFBundleDevelopmentRegion", "English");
				
				//required keys that the user probably should not modify
				dict["CFBundleExecutable"] = conf.LaunchScript.FileName;
				SetIfNotPresent (dict, "CFBundleInfoDictionaryVersion", "6.0");
				SetIfNotPresent (dict, "CFBundlePackageType", "APPL");
				SetIfNotPresent (dict, "CFBundleSignature", "????");
				
				return result;
			});
		}
		
		static void SetIfNotPresent (PlistDictionary dict, string key, PlistObjectBase value)
		{
			if (!dict.ContainsKey (key))
				dict[key] = value;
		}
		
		protected override bool GetNeedsBuilding (SolutionEntityItem item, ConfigurationSelector configuration)
		{
			if (base.GetNeedsBuilding (item, configuration))
				return true;
			
			var proj = item as MonoMacProject;
			if (proj == null || proj.CompileTarget != CompileTarget.Exe)
				return false;
			var conf = (MonoMacProjectConfiguration) configuration.GetConfiguration (item);
			
			//all content files
			if (GetCopyFiles (proj, configuration, conf).Where (NeedsBuilding).Any ())
				return true;
			
			if (PropertyService.IsMac) {
				//Interface Builder files
				var resDir = conf.AppDirectory.Combine ("Contents", "Resources");
				if (MacBuildUtilities.GetIBFilePairs (proj.Files, resDir).Any (NeedsBuilding))
					return true;
			}
			
			//the Info.plist
			var plistOut = conf.AppDirectory.Combine ("Contents", "Info.plist");
			var appInfoIn = proj.Files.GetFile (proj.BaseDirectory.Combine ("Info.plist"));
			if (new FilePair (proj.FileName, plistOut).NeedsBuilding () ||
			    	(appInfoIn != null && new FilePair (appInfoIn.FilePath, plistOut).NeedsBuilding ()))
			    return true;
			
			//launch script
			var ls = conf.LaunchScript;
			if (!File.Exists (ls))
				return true;
			
			//pkginfo
			if (!File.Exists (conf.AppDirectory.Combine ("Contents", "PkgInfo")))
			    return true;
			
			return false;
		}
		
		protected override void Clean (IProgressMonitor monitor, SolutionEntityItem item, ConfigurationSelector configuration)
		{
			base.Clean (monitor, item, configuration);
			
			var proj = item as MonoMacProject;
			if (proj == null || proj.CompileTarget != CompileTarget.Exe)
				return;
			var conf = (MonoMacProjectConfiguration) configuration.GetConfiguration (item);
			
			if (Directory.Exists (conf.AppDirectory))
				Directory.Delete (conf.AppDirectory, true);
		}
		
		protected override BuildResult Compile (IProgressMonitor monitor, SolutionEntityItem item, BuildData buildData)
		{
			return base.Compile (monitor, item, buildData);
		}
		
		static bool NeedsBuilding (FilePair fp)
		{
			return fp.NeedsBuilding ();
		}
		
		static BuildResult BuildError (string error)
		{
			var br = new BuildResult ();
			br.AddError (error);
			return br;
		}
		
		static IEnumerable<FilePair> GetCopyFiles (MonoMacProject project, ConfigurationSelector sel, MonoMacProjectConfiguration conf)
		{
			var resDir = conf.AppDirectoryResources;
			var output = conf.CompiledOutputName;
			yield return new FilePair (output, resDir.Combine (output.FileName));
			
			if (conf.DebugMode) {
				FilePath mdbFile = project.TargetRuntime.GetAssemblyDebugInfoFile (output);
				if (File.Exists (mdbFile))
					yield return new FilePair (mdbFile, resDir.Combine (mdbFile.FileName));
			}
			
			foreach (FileCopySet.Item s in project.GetSupportFileList (sel))
				yield return new FilePair (s.Src, resDir.Combine (s.Target));
			
			foreach (var pf in project.Files)
				if (pf.BuildAction == BuildAction.Content)
					yield return new FilePair (pf.FilePath, pf.ProjectVirtualPath.ToAbsolute (resDir));
		}
	}
}

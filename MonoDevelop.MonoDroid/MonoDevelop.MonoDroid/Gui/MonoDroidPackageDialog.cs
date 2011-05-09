// 
// MonoDroidPackageDialog.cs
//  
// Author:
//       Carlos Alberto Cortez <ccortes@novell.com>
// 
// Copyright (c) 2011 Novell, Inc.
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

namespace MonoDevelop.MonoDroid.Gui
{
	public partial class MonoDroidPackageDialog : Gtk.Dialog
	{	
		public MonoDroidPackageDialog (MonoDroidProjectConfiguration config, string baseDirectory, string fileName)
		{
			this.Build ();
			
			configLabel.Text = config.Name;
			
			if (config.AndroidUseSharedRuntime)
				infoLabel.Text = "The shared runtime and the platform package will not be included in the resulting package.";
			else
				infoLabel.Text = "The shared runtime and platform package will be included in the resulting package.";
			
			folderEntry.Path = baseDirectory;
			fileEntry.Text = fileName;

			folderEntry.PathChanged += (o,e) => Validate ();
			fileEntry.Changed += (o, e) => Validate ();
			
			Validate ();
		}
		
		void Validate ()
		{
			buttonOk.Sensitive = folderEntry.Path.Length > 0 && fileEntry.Text.Trim ().Length > 0;
		}
		
		public string TargetFolder {
			get {
				return folderEntry.Path;
			}
		}
		
		public string TargetFile {
			get {
				return System.IO.Path.Combine (TargetFolder, fileEntry.Text + ".apk");
			}
		}
	}
}


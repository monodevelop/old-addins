// 
// IPhoneOptionsPanelWidget.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2009 Novell, Inc. (http://www.novell.com)
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
using Gtk;
using MonoDevelop.Projects.Gui.Dialogs;
using MonoDevelop.Core;
using MonoDevelop.Projects;

namespace MonoDevelop.IPhone.Gui
{
	
	public class IPhoneOptionsPanel : ItemOptionsPanel
	{
		IPhoneOptionsPanelWidget panel;
		
		public override Widget CreatePanelWidget ()
		{
			panel = new IPhoneOptionsPanelWidget ();
			panel.Load ((IPhoneProject) ConfiguredProject);
			return panel;
		}
		
		public override void ApplyChanges ()
		{
			panel.Store ((IPhoneProject) ConfiguredProject);
		}
		
		public override bool IsVisible ()
		{
			return ConfiguredProject is IPhoneProject
				&& (((IPhoneProject)ConfiguredProject).CompileTarget == CompileTarget.Exe);
		}
	}

	internal partial class IPhoneOptionsPanelWidget : Gtk.Bin
	{

		public IPhoneOptionsPanelWidget ()
		{
			this.Build ();
		}
		
		public void Load (IPhoneProject proj)
		{
			devRegionEntry.Text = proj.BundleDevelopmentRegion ?? "";
			bundleIdEntry.Text = proj.BundleIdentifier ?? "";
			bundleVersionEntry.Text = proj.BundleVersion ?? "";
			displayNameEntry.Text = proj.BundleDisplayName ?? "";
			
			mainNibPicker.Project = proj;
			mainNibPicker.EntryIsEditable = true;
			mainNibPicker.DefaultFilter = "*.xib";
			mainNibPicker.DialogTitle = GettextCatalog.GetString ("Select main interface file...");
			mainNibPicker.SelectedFile = proj.MainNibFile.ToString () ?? "";
			
			appIconPicker.Project = proj;
			appIconPicker.DefaultFilter = "*.png";
			appIconPicker.DialogTitle = GettextCatalog.GetString ("Select application icon...");
			appIconPicker.SelectedFile = proj.BundleIcon.ToString () ?? "";
		}
		
		public void Store (IPhoneProject proj)
		{
			proj.BundleDevelopmentRegion = NullIfEmpty (devRegionEntry.Text);
			proj.BundleIdentifier = NullIfEmpty (bundleIdEntry.Text);
			proj.BundleVersion = NullIfEmpty (bundleVersionEntry.Text);
			proj.BundleDisplayName = NullIfEmpty (displayNameEntry.Text);
			proj.MainNibFile = NullIfEmpty (mainNibPicker.SelectedFile);
			proj.BundleIcon = NullIfEmpty (appIconPicker.SelectedFile);
		}
		
		string NullIfEmpty (string s)
		{
			if (s == null || s.Length != 0)
				return s;
			return null;
		}
	}
}

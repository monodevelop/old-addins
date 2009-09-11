// 
// IPhoneSigningKeyPanelWidget.cs
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
using System.Linq;
using Gtk;
using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Projects.Gui.Dialogs;
using MonoDevelop.Projects;
using System.Security.Cryptography.X509Certificates;

namespace MonoDevelop.IPhone.Gui
{
	
	class IPhoneSigningKeyPanel : MultiConfigItemOptionsPanel
	{
		IPhoneSigningKeyPanelWidget widget;
		
		public override bool IsVisible ()
		{
			return ConfiguredProject is IPhoneProject;
		}
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			AllowMixedConfigurations = false;
			return (widget = new IPhoneSigningKeyPanelWidget ((IPhoneProject)ConfiguredProject));
		}
		
		public override void LoadConfigData ()
		{
			widget.LoadPanelContents ((IPhoneProjectConfiguration)CurrentConfiguration);
		}

		public override void ApplyChanges ()
		{
			widget.StorePanelContents ((IPhoneProjectConfiguration)CurrentConfiguration);
		}
	}
	
	partial class IPhoneSigningKeyPanelWidget : Gtk.Bin
	{
		IList<MobileProvision> profiles;
		
		ListStore identityStore = new ListStore (typeof (string), typeof (string), typeof (X509Certificate2));
		ListStore profileStore = new ListStore (typeof (string), typeof (string), typeof (MobileProvision));
		
		public IPhoneSigningKeyPanelWidget (IPhoneProject project)
		{
			this.Build ();
			
			resourceRulesEntry.DefaultFilter = "*.plist";
			resourceRulesEntry.Project = project;
			resourceRulesEntry.EntryIsEditable = true;
			
			entitlementsEntry.DefaultFilter = "*.plist";
			entitlementsEntry.Project = project;
			entitlementsEntry.EntryIsEditable = true;
			
			additionalArgsEntry.AddOptions (IPhoneBuildOptionsPanelWidget.menuOptions);
			
			enableSigningCheck.Toggled += delegate {
				signingTable.Sensitive = enableSigningCheck.Active;
			};
			
			profiles = MobileProvision.GetAllInstalledProvisions ();
			
			var txtRenderer = new CellRendererText ();
			txtRenderer.Ellipsize = Pango.EllipsizeMode.End;
			
			identityCombo.Model = identityStore;
			identityCombo.PackStart (txtRenderer, true);
			identityCombo.AddAttribute (txtRenderer, "markup", 0);
			
			identityCombo.RowSeparatorFunc = delegate (TreeModel model, TreeIter iter) {
				return (string)model.GetValue (iter, 0) == "-";
			};
			
			identityCombo.Changed += delegate {
				profileStore.Clear ();
				TreeIter iter;
				if (identityStore.GetIter (out iter, new TreePath (new int[] { identityCombo.Active }))) {
					var name = (string) identityStore.GetValue (iter, 1);
					if (name.StartsWith (Keychain.DIST_CERT_PREFIX)) {
						var cert = (X509Certificate2) identityStore.GetValue (iter, 1);
						
						foreach (var mp in profiles) {
							foreach (var profileCert in mp.DeveloperCertificates) {
								if (profileCert.Thumbprint == cert.Thumbprint) {
									profileStore.AppendValues (mp.Name, mp.Uuid, mp);
									break;
								}
							}
						}
						
						provisioningCombo.Sensitive = true;
						return;
					}
				}
				provisioningCombo.Sensitive = false;
			};
			
			provisioningCombo.Model = profileStore;
			provisioningCombo.PackStart (txtRenderer, true);
			provisioningCombo.AddAttribute (txtRenderer, "markup", 0);
			
			var signingCerts = Keychain.GetAllSigningCertificates ().ToList ();
			signingCerts.Sort ();
			
			identityStore.AppendValues ("<b>iPhone Developer (Automatic)</b>", Keychain.DEV_CERT_PREFIX, null);
			identityStore.AppendValues ("<b>iPhone Distribution (Automatic)</b>", Keychain.DIST_CERT_PREFIX, null);
			
			identityStore.AppendValues ("-", "-", null);
			foreach (var cert in signingCerts) {
				string cn = Keychain.GetCertificateCommonName (cert);
				if (cn.StartsWith (Keychain.DEV_CERT_PREFIX))
					identityStore.AppendValues (cn, cn, cert);
			}
			
			identityStore.AppendValues ("-", "-", null);
			foreach (var cert in signingCerts) {
				string cn = Keychain.GetCertificateCommonName (cert);
				if (cn.StartsWith (Keychain.DIST_CERT_PREFIX))
					identityStore.AppendValues (cn, cn, cert);
			}
			
			this.ShowAll ();
		}
		
		public void LoadPanelContents (IPhoneProjectConfiguration cfg)
		{
			enableSigningCheck.Active = !string.IsNullOrEmpty (cfg.CodesignKey);
			signingTable.Sensitive = enableSigningCheck.Active;
			
			SigningKey = cfg.CodesignKey;
			ProvisionFingerprint = cfg.CodesignProvision;
			entitlementsEntry.SelectedFile = cfg.CodesignEntitlements;
			resourceRulesEntry.SelectedFile = cfg.CodesignResourceRules;
			additionalArgsEntry.Text = cfg.CodesignExtraArgs ?? "";
		}
		
		public void StorePanelContents (IPhoneProjectConfiguration cfg)
		{
			cfg.CodesignKey = enableSigningCheck.Active? SigningKey : null;
			cfg.CodesignProvision = enableSigningCheck.Active? ProvisionFingerprint : null;
			cfg.CodesignEntitlements = entitlementsEntry.SelectedFile;
			cfg.CodesignResourceRules = resourceRulesEntry.SelectedFile;
			cfg.CodesignExtraArgs = NullIfEmpty (additionalArgsEntry.Entry.Text);
		}
		
		string SigningKey {
			get {
				TreeIter iter;
				if (identityStore.GetIter (out iter, new TreePath (new int[] { identityCombo.Active })))
					return (string) identityStore.GetValue (iter, 1);
				return null;
			}
			set {
				SelectMatchingItem (identityCombo, 1, value);
			}
		}
		
		string ProvisionFingerprint {
			get {
				TreeIter iter;
				if (!provisioningCombo.Sensitive)
					return null;
				if (profileStore.GetIter (out iter, new TreePath (new int[] { provisioningCombo.Active })))
					return (string) profileStore.GetValue (iter, 1);
				return null;
			}
			set {
				if (string.IsNullOrEmpty (value))
					provisioningCombo.Active = 0;
				else
					SelectMatchingItem (provisioningCombo, 1, value);
			}
		}
		
		bool SelectMatchingItem (ComboBox combo, int column, object value)
		{
			var m = combo.Model;
			TreeIter iter;
			int i = 0;
			if (m.GetIterFirst (out iter)) {
				do {
					if (value.Equals (m.GetValue (iter, column))) {
						combo.Active = i;
						return true;
					}
					i++;
				} while (m.IterNext (ref iter));
			}
			return false;
		}
		
		string NullIfEmpty (string s)
		{
			return (s == null || s.Length == 0)? null : s;
		}
	}
}

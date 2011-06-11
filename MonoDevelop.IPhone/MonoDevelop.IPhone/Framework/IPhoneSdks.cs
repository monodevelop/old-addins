// 
// IPhoneFrameworkBackend.cs
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
using System.Linq;
using System.IO;
using System.Collections.Generic;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core;
using Mono.Addins;
using MonoDevelop.Ide;
using Gtk;
using MonoDevelop.Core.Serialization;
using MonoDevelop.MacDev.Plist;

using System.Runtime.InteropServices;

namespace MonoDevelop.IPhone
{
	public static class IPhoneSdks
	{
		public static AppleIPhoneSdk Native { get; private set; }
		public static MonoTouchSdk MonoTouch { get; private set; }
		
		static IPhoneSdks ()
		{
			var mtRoot = Environment.GetEnvironmentVariable ("MD_MTOUCH_SDK_ROOT");
			
			//FIXME: find a way to pass this through to mtouch too 
			var devRoot = Environment.GetEnvironmentVariable ("MD_IPHONE_SDK_ROOT");
			
			Native = new AppleIPhoneSdk (devRoot ?? GetConfiguredNativeSdkRoot () ?? "/Developer");
			MonoTouch = new MonoTouchSdk (mtRoot ?? GetConfiguredMonoTouchSdkRoot () ?? "/Developer");
		}
		
		public static MonoDevelop.Projects.BuildResult GetSimOnlyError ()
		{
			var res = new MonoDevelop.Projects.BuildResult ();
			res.AddError (GettextCatalog.GetString (
				"The evaluation version of MonoTouch does not support targeting the device. " + 
				"Please go to http://monotouch.net to purchase the full version."));
			return res;
		}
		
		public static void CheckInfoCaches ()
		{
			Native.CheckCaches ();
			MonoTouch.CheckCaches ();
		}
		
		const string NATIVE_SDK_KEY = "MonoDevelop.IPhone.NativeSdkRoot";
		const string MTOUCH_SDK_KEY = "MonoDevelop.IPhone.MonoTouchSdkRoot";
		
		internal static string GetConfiguredNativeSdkRoot ()
		{
			return PropertyService.Get<string> (NATIVE_SDK_KEY, null);
		}
		
		internal static void SetConfiguredNativeSdkRoot (string value)
		{
			if (value == "/Developer")
				value = null;
			if (value == PropertyService.Get<string> (NATIVE_SDK_KEY))
				return;
			PropertyService.Set (NATIVE_SDK_KEY, value);
			if (Environment.GetEnvironmentVariable ("MD_IPHONE_SDK_ROOT") != null)
				Native = new AppleIPhoneSdk (value ?? "/Developer");
		}
		
		internal static string GetConfiguredMonoTouchSdkRoot ()
		{
			return PropertyService.Get<string> (MTOUCH_SDK_KEY, null);
		}
		
		internal static void SetConfiguredMonoTouchSdkRoot (string value)
		{
			if (value == "/Developer")
				value = null;
			if (value == PropertyService.Get<string> (MTOUCH_SDK_KEY))
				return;
			PropertyService.Set (MTOUCH_SDK_KEY, value);
			if (Environment.GetEnvironmentVariable ("MD_MTOUCH_SDK_ROOT") != null)
				MonoTouch = new MonoTouchSdk (value ?? "/Developer");
		}
	}
	
	public class MonoTouchInstalledCondition : ConditionType
	{
		public override bool Evaluate (NodeElement conditionNode)
		{
			return IPhoneSdks.MonoTouch.IsInstalled;
		}
	}
}

// 
// Keychain.cs
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
using System.Runtime.InteropServices;
using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Core.Serialization;

namespace MonoDevelop.IPhone
{


	public static class Keychain
	{
		const string SecurityLib = "/System/Library/Frameworks/Security.framework/Security";
		const string CFLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
		
		[DllImport (CFLib, EntryPoint="CFRelease")]
		static extern void CFReleaseInternal (IntPtr cfRef);
		
		static void CFRelease (IntPtr cfRef)
		{
			if (cfRef != IntPtr.Zero)
				CFReleaseInternal (cfRef);
		}
		
		[DllImport (SecurityLib)]
		static extern OSStatus SecKeychainSearchCreateFromAttributes (IntPtr keychainOrArray, SecItemClass itemClass, IntPtr attrList, out IntPtr searchRef);
		
		[DllImport (SecurityLib)]
		static extern OSStatus SecKeychainSearchCopyNext (IntPtr searchRef, out IntPtr itemRef);

		[DllImport (SecurityLib)]
		static extern OSStatus SecCertificateCopyCommonName (IntPtr certificate, out IntPtr commonName);
		
		[DllImport (SecurityLib)]
		static extern OSStatus SecIdentitySearchCreate (IntPtr keychainOrArray, CssmKeyUse keyUsage, out IntPtr searchRef);
		
		[DllImport (SecurityLib)]
		static extern OSStatus SecIdentitySearchCopyNext (IntPtr searchRef, out IntPtr identity);
		
		[DllImport (SecurityLib)]
		static extern OSStatus SecIdentityCopyCertificate (IntPtr identityRef, out IntPtr certificateRef);
		
		#region CFString handling
		
		struct CFRange {
			public int Location, Length;
			public CFRange (int l, int len)
			{
				Location = l;
				Length = len;
			}
		}
		
		[DllImport (CFLib, CharSet=CharSet.Unicode)]
		extern static int CFStringGetLength (IntPtr handle);

		[DllImport (CFLib, CharSet=CharSet.Unicode)]
		extern static IntPtr CFStringGetCharactersPtr (IntPtr handle);
		
		[DllImport (CFLib, CharSet=CharSet.Unicode)]
		extern static IntPtr CFStringGetCharacters (IntPtr handle, CFRange range, IntPtr buffer);
		
		static string FetchString (IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return null;
			
			string str;
			
			int l = CFStringGetLength (handle);
			IntPtr u = CFStringGetCharactersPtr (handle);
			IntPtr buffer = IntPtr.Zero;
			if (u == IntPtr.Zero){
				CFRange r = new CFRange (0, l);
				buffer = Marshal.AllocCoTaskMem (l * 2);
				CFStringGetCharacters (handle, r, buffer);
				u = buffer;
			}
			unsafe {
				str = new string ((char *) u, 0, l);
			}
			
			if (buffer != IntPtr.Zero)
				Marshal.FreeCoTaskMem (buffer);
			
			return str;
		}
		
		#endregion
		
		public static List<string> GetAllCertificateNames ()
		{
			IntPtr attrList = IntPtr.Zero; //match any attributes
			IntPtr searchRef, itemRef;
			
			//null keychain means use default
			var res = SecKeychainSearchCreateFromAttributes (IntPtr.Zero, SecItemClass.Certificate, attrList, out searchRef);
			if (res != OSStatus.Ok)
				throw new Exception (String.Format ("Could not enumerate certificates from the keychain. Error code {0}", (int)res));
			
			var list = new List<string> ();
			
			while (SecKeychainSearchCopyNext (searchRef, out itemRef) == OSStatus.Ok) {
				IntPtr commonName;
				if (SecCertificateCopyCommonName (itemRef, out commonName) == OSStatus.Ok) {
					list.Add (FetchString (commonName));
					CFRelease (commonName);
				}
				CFRelease (itemRef);
			}
			CFRelease (searchRef);
			return list;
		}
		
		public static List<string> GetAllSigningIdentities ()
		{
			IntPtr searchRef, itemRef, certRef, commonName;
			
			//null keychain means use default
			var res = SecIdentitySearchCreate (IntPtr.Zero, CssmKeyUse.Sign, out searchRef);
			if (res != OSStatus.Ok)
				throw new Exception (String.Format ("Could not enumerate identities from the keychain. Error code {0}", (int)res));
			
			var list = new List<string> ();
			
			while (SecIdentitySearchCopyNext (searchRef, out itemRef) == OSStatus.Ok) {
				if (SecIdentityCopyCertificate (itemRef, out certRef) == OSStatus.Ok) {
					if (SecCertificateCopyCommonName (certRef, out commonName) == OSStatus.Ok) {
						string name = FetchString (commonName);
						if (name != null)
							list.Add (name);
						CFRelease (commonName);
					}
					CFRelease (certRef);
				}
				CFRelease (itemRef);
			}
			CFRelease (searchRef);
			return list;
		}
		
		public const string DEV_CERT_PREFIX  = "iPhone Developer:";
		public const string DIST_CERT_PREFIX = "iPhone Distribution:";
		
		public static string GetCertificateName (IPhoneProject project, bool distribution)
		{
			var keys = project.UserProperties.GetValue<SigningKeyInformation> ("IPhoneSigningKeys");
			
			if (keys != null) {
				string key = distribution? keys.Distribution : keys.Developer;
				if (key != null)
					return key;
			}
			
			string cmp = distribution? DIST_CERT_PREFIX : DEV_CERT_PREFIX;
			foreach (string certName in GetAllSigningIdentities ())
				if (certName.StartsWith (cmp))
					return certName;
			return null;
		}
		
		enum SecItemClass : uint
		{
			InternetPassword = 1768842612, // 'inet'
			GenericPassword = 1734700656,  // 'genp'
			AppleSharePassword = 1634953328, // 'ashp'
			Certificate =  0x80000000 + 0x1000,
			PublicKey = 0x0000000A + 5,
			PrivateKey = 0x0000000A + 6,
			SymmetricKey = 0x0000000A + 7
		}
		
		enum OSStatus
		{
			Ok = 0,
			ItemNotFound = -25300,
		}
		
		enum SecKeyAttribute
		{
			KeyClass =          0,
			PrintName =         1,
			Alias =             2,
			Permanent =         3,
			Private =           4,
			Modifiable =        5,
			Label =             6,
			ApplicationTag =    7,
			KeyCreator =        8,
			KeyType =           9,
			KeySizeInBits =    10,
			EffectiveKeySize = 11,
			StartDate =        12,
			EndDate =          13,
			Sensitive =        14,
			AlwaysSensitive =  15,
			Extractable =      16,
			NeverExtractable = 17,
			Encrypt =          18,
			Decrypt =          19,
			Derive =           20,
			Sign =             21,
			Verify =           22,
			SignRecover =      23,
			VerifyRecover =    24,
			Wrap =             25,
			Unwrap =           26,
		}
		
		[Flags]
		enum CssmKeyUse : uint
		{
			Any =				0x80000000,
			Encrypt =			0x00000001,
			Decrypt =			0x00000002,
			Sign =				0x00000004,
			Verify =			0x00000008,
			SignRecover =		0x00000010,
			VerifyRecover =		0x00000020,
			Wrap =				0x00000040,
			Unwrap =			0x00000080,
			Derive =			0x00000100
		}
	}
	
	[DataItem]
	sealed class SigningKeyInformation
	{
		
		public SigningKeyInformation (string developer, string distribution)
		{
			this.Distribution = NullIfEmpty (distribution);
			this.Developer = NullIfEmpty (developer);
		}
		
		public SigningKeyInformation ()
		{
		}
		
		[ItemProperty]
		public string Developer { get; private set; }
		
		[ItemProperty]
		public string Distribution { get; private set; }
		
		public static SigningKeyInformation Default {
			get {
				string dev = PropertyService.Get<string> ("IPhoneSigningKey.Developer", null);
				string dist = PropertyService.Get<string> ("IPhoneSigningKey.Distribution", null);
				return new SigningKeyInformation (dev, dist);
			}
		}
		
		string NullIfEmpty (string s)
		{
			return (s == null || s.Length == 0)? null : s;
		}
	}
}
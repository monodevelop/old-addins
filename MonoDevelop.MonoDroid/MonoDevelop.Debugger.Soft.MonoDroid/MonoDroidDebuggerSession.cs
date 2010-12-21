// 
// MonoDroidDebuggerSession.cs
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
using Mono.Debugger.Soft;
using Mono.Debugging;
using Mono.Debugging.Client;
using System.Threading;
using System.Diagnostics;
using MonoDevelop.MonoDroid;
using System.IO;
using MonoDevelop.Core;
using System.Net.Sockets;
using System.Net;
using System.Text;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.Debugger.Soft.MonoDroid
{
	public class MonoDroidDebuggerSession : RemoteSoftDebuggerSession
	{
		ChainedAsyncOperationSequence launchOp;
		
		protected override void OnRun (DebuggerStartInfo startInfo)
		{
			var dsi = (MonoDroidDebuggerStartInfo) startInfo;
			var cmd = dsi.ExecutionCommand;
			
			long date = 0;
			launchOp = new ChainedAsyncOperationSequence (
				new ChainedAsyncOperation<AndroidToolbox.GetDateOperation> () {
					Create = () => MonoDroidFramework.Toolbox.GetDeviceDate (cmd.Device),
					Completed = (op) => {
						if (op.Success) {
							date = op.Date;
						} else {
							this.OnDebuggerOutput (true, GettextCatalog.GetString ("Failed to get date from device"));
							this.OnDebuggerOutput (true, op.GetOutput ());
						}
					},
				},
				new ChainedAsyncOperation<AndroidToolbox.AdbOutputOperation> () {
					Create = () => {
						long expireDate = date + 30; // 30 seconds
						string monoOptions = string.Format ("debug={0}:{1}:{2},timeout={3},server=y", dsi.Address, dsi.DebugPort, 0, expireDate);
						return MonoDroidFramework.Toolbox.SetProperty (cmd.Device, "debug.mono.extra", monoOptions);
					},
					Completed = (op) => {
						if (!op.Success) {
							this.OnDebuggerOutput (true, GettextCatalog.GetString ("Failed to set debug property on device"));
							this.OnDebuggerOutput (true, op.GetOutput ());
						}
					}
				},
				new ChainedAsyncOperation () {
					Create = () => MonoDroidFramework.Toolbox.ForwardPort (cmd.Device, dsi.DebugPort, dsi.DebugPort, null, null),
					Completed = (op) => {
						if (!op.Success) {
							this.OnDebuggerOutput (true, GettextCatalog.GetString ("Failed to forward port on device"));
						}
					}
				},
				new ChainedAsyncOperation () {
					Create = () => MonoDroidFramework.Toolbox.ForwardPort (cmd.Device, dsi.OutputPort, dsi.OutputPort, null, null),
					Completed = (op) => {
						if (!op.Success) {
							this.OnDebuggerOutput (true, GettextCatalog.GetString ("Failed to forward port on device"));
						} else
							MonoDroidFramework.DeviceManager.SetDeviceLastForwarded (cmd.Device.ID);
					}
				},
				new ChainedAsyncOperation () {
					Create = () => MonoDroidFramework.Toolbox.StartActivity (cmd.Device, cmd.Activity,
						(s, m) => OnTargetOutput (false, m),
						(s, m) => OnTargetOutput (true, m)
					),
					Completed = (op) => {
						if (!op.Success)
							this.OnDebuggerOutput (true, GettextCatalog.GetString ("Failed to start activity"));
					}
				}
			);
			launchOp.Completed += delegate(IAsyncOperation op) {
				if (!op.Success)
					EndSession ();
				launchOp = null;
			};
			
			TargetExited += delegate {
				EndLaunch ();
			};
			
			launchOp.Start ();
			
			// Connect to the process, giving it a time to start.
			System.Threading.Thread.Sleep (200);

			StartConnecting (dsi, -1, 800);
		}

		void ProcessOutput (object sender, string message)
		{
			OnTargetOutput (true, message);
		}
		
		void ProcessError (object sender, string message)
		{
			OnTargetOutput (false, message);
		}
		
		protected override string GetListenMessage (RemoteDebuggerStartInfo dsi)
		{
			//var cmd = ((MonoDroidDebuggerStartInfo)dsi).ExecutionCommand;
			string message = GettextCatalog.GetString ("Waiting for debugger to connect on {0}:{1}...", dsi.Address, dsi.DebugPort);
			return message;
		}
		
		protected override void EndSession ()
		{
			base.EndSession ();
			EndLaunch ();
		}
		
		void EndLaunch ()
		{
			if (launchOp == null)
				return;
			if (!launchOp.IsCompleted) {
				try {
					launchOp.Cancel ();
					launchOp = null;
				} catch {}
			}
		}
		
		protected override void OnExit ()
		{
			base.OnExit ();
			EndLaunch ();
		}
		//FIXME: ShouldRetryConnection only works on master, not 2.4.x
		/*
		protected override bool ShouldRetryConnection (Exception exc, int attemptNumber)
		{
			if (exc is IOException)
				return true;

			return base.ShouldRetryConnection (exc, attemptNumber);
		}*/
	}
	
	class MonoDroidDebuggerStartInfo : RemoteDebuggerStartInfo
	{
		public MonoDroidExecutionCommand ExecutionCommand { get; private set; }
		
		public MonoDroidDebuggerStartInfo (IPAddress address, MonoDroidExecutionCommand cmd)
			: base (cmd.PackageName, address, cmd.DebugPort, cmd.OutputPort)
		{
			ExecutionCommand = cmd;
		}
	}
}

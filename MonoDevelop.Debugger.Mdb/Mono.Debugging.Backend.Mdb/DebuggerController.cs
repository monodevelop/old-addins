using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters.Binary;
using Mono.Remoting.Channels.Unix;
using System.Threading;
using Mono.Debugging.Backend;
using Mono.Debugging.Client;

namespace Mono.Debugging.Backend.Mdb
{
	class DebuggerController : MarshalByRefObject, IDebuggerController
	{
		string remotingChannel = "tcp";
		IDebuggerServer debugger;
		Process process;
		IDebuggerSessionFrontend frontend;
		MonoDebuggerSession session;
		string unixRemotingFile;
		
		ManualResetEvent runningEvent = new ManualResetEvent (false);
		ManualResetEvent exitRequestEvent = new ManualResetEvent (false);
		ManualResetEvent exitedEvent = new ManualResetEvent (false);

		public IDebuggerServer DebuggerServer {
			get { return debugger; }
		}
		
		public DebuggerController (MonoDebuggerSession session, IDebuggerSessionFrontend frontend)
		{
			this.session = session;
			this.frontend = frontend;
		}

		#region IDebuggerController Members

		public void RegisterDebugger (IDebuggerServer debugger)
		{
			lock (this)
			{
				this.debugger = debugger;
				runningEvent.Set ();
			}
		}

		public void WaitForExit()
		{
			exitRequestEvent.WaitOne();
		}

		public void NotifyStarted ()
		{
			frontend.NotifyStarted ();
		}

		public void OnTargetEvent (TargetEventArgs args)
		{
			frontend.NotifyTargetEvent (args);
			if (args.Type == TargetEventType.TargetExited)
				StopDebugger ();
		}
		
		public void OnTargetOutput (bool isStderr, string line)
		{
			frontend.NotifyTargetOutput (isStderr, line);
		}
		
		public void OnDebuggerOutput (bool isStderr, string line)
		{
			frontend.NotifyDebuggerOutput (isStderr, line);
		}

		public bool OnCustomBreakpointAction (string actionId, object handle)
		{
			return frontend.NotifyCustomBreakpointAction (actionId, handle);
		}
		
		public void UpdateBreakpoint (object handle, int count, string lastTrace)
		{
			session.UpdateBreakEvent (handle, count, lastTrace);
		}
		
		public void NotifySourceFileLoaded (string[] fullFilePaths)
		{
			foreach (string file in fullFilePaths)
				frontend.NotifySourceFileLoaded (file);
		}

		public void NotifySourceFileUnloaded (string[] fullFilePaths)
		{
			foreach (string file in fullFilePaths)
				frontend.NotifySourceFileUnloaded (file);
		}
		
		#endregion

		public void StartDebugger (MonoDebuggerStartInfo startInfo)
		{
			lock (this)
			{
				exitRequestEvent.Reset ();

				string chId = RegisterRemotingChannel();

				BinaryFormatter bf = new BinaryFormatter();
				ObjRef oref = RemotingServices.Marshal(this);
				MemoryStream ms = new MemoryStream();
				bf.Serialize(ms, oref);
				string sref = Convert.ToBase64String(ms.ToArray());
				try
				{
					Process process = new Process();
					process.Exited += new EventHandler (ProcessExited);
					string location = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
					string argv = string.Empty;
					//if (isDebugMode) argv += " --debug";
					argv += " --debug '" + Path.Combine(location, "DebuggerServer.exe") + "' ";

					process.StartInfo = new ProcessStartInfo ("mono", argv);
					
					if (startInfo != null) {
						string monoPath = Path.Combine (startInfo.MonoPrefix, "bin");
						process.StartInfo.FileName = Path.Combine (monoPath, "mono");
						if (startInfo.ServerEnvironment != null) {
							foreach (KeyValuePair<string,string> evar in startInfo.ServerEnvironment)
								process.StartInfo.EnvironmentVariables [evar.Key] = evar.Value;
						}
					}
					
					process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardInput = true;
					process.EnableRaisingEvents = true;
					process.Start();
					
					// The server expects 3 lines with the following content:
					// 1) location of the Mono.Debugging assembly (needed since it may be located
					//    in a different directory)
					// 2) Remting channel to use
					// 3) Serialized reference to the IDebuggerController
					process.StandardInput.WriteLine (typeof(DebuggerSession).Assembly.Location);
					process.StandardInput.WriteLine (chId);
					process.StandardInput.WriteLine (sref);
					process.StandardInput.Flush();
					this.process = process;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error launching server: " + ex.ToString());
					throw;
				}
			}
			
			if (!runningEvent.WaitOne (15000, false)) {
				throw new ApplicationException ("Could not create the debugger process.");
			}
		}

		void ProcessExited (object sender, EventArgs args)
		{
			lock (this) {
				exitedEvent.Set ();
				Process p = (Process) sender;
				if (p != process) return;

				// The process suddently died
				runningEvent.Reset ();
				debugger = null;
				process = null;
			}
		}
		
		public void Exit ()
		{
			object elock = new object ();
			lock (elock) {
				ThreadPool.QueueUserWorkItem (delegate {
					try {
						debugger.Exit ();
						lock (elock) {
							Monitor.PulseAll (elock);
						}
					} catch {
						// Ignore
					}
				});
				
				// If the debugger does not stop, kill it.
				if (!Monitor.Wait (elock, 3000)) {
					StopDebugger ();
					OnTargetEvent (new TargetEventArgs (TargetEventType.TargetExited));
				}
			}
		}
		
		public void StopDebugger ()
		{
			try {
				Process oldProcess;
				lock (this) {
					if (debugger == null)
						return;
					runningEvent.Reset ();
					exitedEvent.Reset ();
					exitRequestEvent.Set ();
					oldProcess = process;
					debugger = null;
					process = null;
				}
	
				if (!exitedEvent.WaitOne (2000, false)) {
					try {
						oldProcess.Kill ();
					} catch {
					}
				}
			} catch (Exception ex) {
				Console.WriteLine (ex);
			}
		}

		string RegisterRemotingChannel()
		{
			if (remotingChannel == "tcp")
			{
				IChannel ch = ChannelServices.GetChannel("tcp");
				if (ch == null)
				{
					IDictionary dict = new Hashtable();
					BinaryClientFormatterSinkProvider clientProvider = new BinaryClientFormatterSinkProvider();
					BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();

					dict["port"] = 0;
					serverProvider.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;

					ChannelServices.RegisterChannel(new TcpChannel(dict, clientProvider, serverProvider), false);
				}
			}
			else
			{
				IChannel ch = ChannelServices.GetChannel ("unix");
				if (ch == null) {
					unixRemotingFile = Path.GetTempFileName (); 
					ChannelServices.RegisterChannel (new UnixChannel (unixRemotingFile), false);
				}
			}
			return remotingChannel;
		}
	}
}

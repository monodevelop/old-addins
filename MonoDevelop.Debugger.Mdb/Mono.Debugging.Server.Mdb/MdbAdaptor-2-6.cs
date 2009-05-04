/*

WARNING WARNING WARNING 
  
This class is compiled by the debugger add-in at runtime. It is done in
this way because features implemented here depend on the installed MDB version.
   
*/

using System;
using Mono.Debugger;
using MDB=Mono.Debugger;
using Mono.Debugging.Backend.Mdb;

namespace DebuggerServer
{
	public class MdbAdaptor_2_6: MdbAdaptor
	{
		public override void SetupXsp (DebuggerConfiguration config)
		{
			config.SetupXSP ();
			config.StopOnManagedSignals = true;
		}
		
		public override void InitializeSession (MonoDebuggerStartInfo startInfo, MDB.DebuggerSession session)
		{
			session.AddUserModulePath (startInfo.WorkingDirectory);
			if (startInfo.UserModules != null) {
				foreach (string path in startInfo.UserModules)
					session.AddUserModule (path);
			}
		}
	}
}

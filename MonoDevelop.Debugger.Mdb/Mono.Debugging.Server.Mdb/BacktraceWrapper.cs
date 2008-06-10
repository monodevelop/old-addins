using System;
using System.Text;
using System.Collections.Generic;
	
using MD = Mono.Debugger;
using DL = Mono.Debugging.Client;

using Mono.Debugging.Client;
using Mono.Debugging.Backend;
using Mono.Debugger.Languages;

namespace DebuggerServer
{
	class BacktraceWrapper: MarshalByRefObject, IBacktrace, IObjectValueSource
	{
		MD.Backtrace backtrace;
		MD.StackFrame[] frames;
	       
		public BacktraceWrapper (MD.Backtrace backtrace)
		{
			this.backtrace = backtrace;
		}
	       
		public int FrameCount {
			get { return backtrace.Count; }
		}
	       
		public DL.StackFrame[] GetStackFrames (int firstIndex, int lastIndex)
		{
			if (frames == null)
				frames = backtrace.Frames;
			
			//FIXME: validate indices

			List<DL.StackFrame> list = new List<DL.StackFrame> ();
			for (int i = firstIndex; i <= lastIndex && i < backtrace.Count; i ++) {
				MD.StackFrame frame = frames [i];
				MD.SourceLocation md_location = frame.SourceLocation;
				DL.SourceLocation dl_location;
				string method = null;
				string filename = null;
				int line = -1;
				
				if (frame.Method != null) {
					method = frame.Method.Name;
					int p = method.IndexOf ('(');
					if (p != -1) {
						StringBuilder sb = new StringBuilder ();
						foreach (TargetVariable var in frame.Method.GetParameters (frame.Thread)) {
							if (sb.Length > 0)
								sb.Append (", ");
							sb.Append (var.Name).Append (" = ").Append (Util.ObjectToString (frame, var.GetObject (frame)));
						}
						sb.Append (')');
						method = method.Substring (0, p+1) + sb.ToString ();
					}
				} else if (frame.Name != null) {
					method = frame.Name.Name;
				} else {
					method = "?";
				}
				
				if (frame.SourceAddress != null) {
					if (frame.SourceAddress.SourceFile != null)
						filename = frame.SourceAddress.SourceFile.FileName;
					line = frame.SourceAddress.Row;
				}
				
				list.Add (new DL.StackFrame (frame.TargetAddress.Address, new DL.SourceLocation (method, filename, line)));
			}
			
			return list.ToArray ();
		}
		
		public ObjectValue[] GetLocalVariables (int frameIndex)
		{
			if (frames == null)
				frames = backtrace.Frames;
			
			List<ObjectValue> vars = new List<ObjectValue> ();
			MD.StackFrame frame = frames [frameIndex];
			foreach (TargetVariable var in frame.Method.GetLocalVariables (frame.Thread))
				vars.Add (Util.CreateObjectValue (frame.Thread, this, new ObjectPath ("FR", frameIndex.ToString(), "LV", var.Name), var.GetObject (frame)));
			
			return vars.ToArray ();
		}
		
		public ObjectValue[] GetParameters (int frameIndex)
		{
			List<ObjectValue> vars = new List<ObjectValue> ();
			MD.StackFrame frame = frames [frameIndex];
			foreach (TargetVariable var in frame.Method.GetParameters (frame.Thread))
				vars.Add (Util.CreateObjectValue (frame.Thread, this, new ObjectPath ("FR", frameIndex.ToString (), "PS", var.Name), var.GetObject (frame)));
			
			return vars.ToArray ();
		}
		
		public ObjectValue GetThisReference (int frameIndex)
		{
			MD.StackFrame frame = frames [frameIndex];
			if (frame.Method.HasThis)
				return Util.CreateObjectValue (frame.Thread, this, new ObjectPath ("FR", frameIndex.ToString (), "TR"), frame.Method.GetThis (frame.Thread).GetObject (frame));
			else
				return null;
		}
		
		public ObjectValue[] GetExpressionValues (int frameIndex, string[] expressions)
		{
			ObjectValue[] values = new ObjectValue [expressions.Length];
			for (int n=0; n<values.Length; n++) {
				string exp = expressions[n];
				TargetObject ob = GetExpressionTargetObject (frames[frameIndex], exp);
				if (ob != null)
					values [n] = Util.CreateObjectValue (frames[frameIndex].Thread, this, new ObjectPath ("FR", frameIndex.ToString(), "EXP", exp), ob);
				else
					values [n] = ObjectValue.CreateUnknownValue (exp);
			}
			return values;
		}
		
		public TargetObject GetExpressionTargetObject (MD.StackFrame frame, string exp)
		{
			FrameExpressionValueSource source = new FrameExpressionValueSource (frame);
			return source.GetValue (exp);
		}
		
		public ObjectValue[] GetChildren (ObjectPath path, int index, int count)
		{
			if (path [0] == "FR") {
				
				// Frames query
				if (frames == null)
					frames = backtrace.Frames;
				
				MD.StackFrame frame = frames [int.Parse (path [1])];
				
				if (path [2] == "LV") {
					// Local variables query
					foreach (TargetVariable var in frame.Method.GetLocalVariables (frame.Thread)) {
						if (var.Name == path [3])
							return Util.GetObjectValueChildren (frame.Thread, this, var.GetObject (frame), path, 4, 0, index, count);
					}
				}
				else if (path [2] == "PS") {
					// Parameters query
					foreach (TargetVariable var in frame.Method.GetParameters (frame.Thread)) {
						if (var.Name == path [3])
							return Util.GetObjectValueChildren (frame.Thread, this, var.GetObject (frame), path, 4, 0, index, count);
					}
				}
				else if (path [2] == "TR") {
					// This reference
					TargetVariable var = frame.Method.GetThis (frame.Thread);
					return Util.GetObjectValueChildren (frame.Thread, this, var.GetObject (frame), path, 3, 0, index, count);
				}
				else if (path [2] == "EXP") {
					// This reference
					TargetObject val = GetExpressionTargetObject (frame, path[3]);
					return Util.GetObjectValueChildren (frame.Thread, this, val, path, 4, 0, index, count);
				}
			}
			return null;
		}
	}
	
	class FrameExpressionValueSource: IExpressionValueSource
	{
		MD.StackFrame frame;
		
		public FrameExpressionValueSource (MD.StackFrame frame)
		{
			this.frame = frame;
		}
		
		public TargetObject GetValue (string name)
		{
			if (frame.Method == null)
				return null;
			
			// Look in variables
			
			foreach (TargetVariable var in frame.Method.GetLocalVariables (frame.Thread))
				if (var.Name == name)
					return var.GetObject (frame);
			
			// Look in parameters
			
			foreach (TargetVariable var in frame.Method.GetParameters (frame.Thread))
				if (var.Name == name)
					return var.GetObject (frame);
			
			// Look in fields
			
			TargetStructObject thisobj = null;
			
			if (frame.Method.HasThis) {
				TargetObject ob = frame.Method.GetThis (frame.Thread).GetObject (frame);
				thisobj = Util.GetRealObject (frame.Thread, ob) as TargetStructObject;
			}
			
			TargetStructType type = frame.Method.GetDeclaringType (frame.Thread);
			
			while (type != null)
			{
				TargetClass cls = type.GetClass (frame.Thread);
				
				foreach (TargetPropertyInfo prop in type.ClassType.Properties) {
					if (prop.Name == name && prop.CanRead && (prop.IsStatic || thisobj != null)) {
						MD.RuntimeInvokeResult result = frame.Thread.RuntimeInvoke (prop.Getter, thisobj, new TargetObject[0], true, false);
						return result.ReturnObject;
					}
				}
				foreach (TargetFieldInfo field in type.ClassType.Fields) {
					if (field.Name == name && (field.IsStatic || thisobj != null))
						return cls.GetField (frame.Thread, thisobj, field);
				}
			}
			return null;
		}
	}
}
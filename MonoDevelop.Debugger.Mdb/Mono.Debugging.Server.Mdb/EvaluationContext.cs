// EvaluationContext.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
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
//
//

using System;
using Mono.Debugger;
using Mono.Debugger.Languages;
using Mono.Debugging.Evaluation;

namespace DebuggerServer
{
	public class MdbEvaluationContext: EvaluationContext
	{
		Thread thread;
		StackFrame frame;
		
		public Thread Thread {
			get {
				return thread;
			}
		}
		
		public StackFrame Frame {
			get {
				return frame;
			}
		}
		
		public TargetObject Exception { get; private set; }

		public MdbEvaluationContext (Thread thread, StackFrame frame, TargetObject exception, Mono.Debugging.Client.EvaluationOptions options): base (options)
		{
			Evaluator = Server.Instance.Evaluator;
			Adapter = Server.Instance.MdbObjectValueAdaptor;
			this.thread = thread;
			this.frame = frame;
			this.Exception = exception;
		}
		
		public TargetObject GetRealObject (object ob)
		{
			return ObjectUtil.GetRealObject (this, (TargetObject) ob);
		}
		
		public override void CopyFrom (Mono.Debugging.Evaluation.EvaluationContext gctx)
		{
			base.CopyFrom (gctx);
			MdbEvaluationContext ctx = (MdbEvaluationContext) gctx;
			thread = ctx.thread;
			frame = ctx.frame;
		}

		public override void WriteDebuggerError (System.Exception ex)
		{
			Server.Instance.WriteDebuggerError (ex);
		}

		public override void WriteDebuggerOutput (string message, params object[] values)
		{
			Server.Instance.WriteDebuggerOutput (message, values);
		}
	}
}

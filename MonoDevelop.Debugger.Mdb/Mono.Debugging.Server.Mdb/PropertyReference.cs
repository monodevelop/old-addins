// PropertyVariable.cs
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
using Mono.Debugger.Languages;
using Mono.Debugger;
using Mono.Debugging.Client;
using Mono.Debugging.Evaluation;

namespace DebuggerServer
{
	class PropertyReference: ValueReference
	{
		TargetPropertyInfo prop;
		TargetStructObject thisobj;
		
		public PropertyReference (EvaluationContext ctx, TargetPropertyInfo prop, TargetStructObject thisobj): base (ctx)
		{
			this.prop = prop;
			if (!prop.IsStatic)
				this.thisobj = thisobj;
		}
		
		public override object Type {
			get {
				return prop.Type;
			}
		}
		
		public override object DeclaringType {
			get {
				return prop.Getter.DeclaringType;
			}
		}
		
		public override object Value {
			get {
				MdbEvaluationContext ctx = (MdbEvaluationContext) Context;
				return ctx.GetRealObject (Server.Instance.RuntimeInvoke (ctx, prop.Getter, thisobj, new TargetObject[0]));
			}
			set {
				MdbEvaluationContext ctx = (MdbEvaluationContext) Context;
				Server.Instance.RuntimeInvoke (ctx, prop.Setter, thisobj, new TargetObject[] { (TargetObject) value });
			}
		}
		
		public override string Name {
			get {
				return prop.Name;
			}
		}

		public override ObjectValueFlags Flags {
			get {
				ObjectValueFlags flags = ObjectValueFlags.Property | ObjectUtil.GetAccessibility (prop.Accessibility);
				if (!prop.CanWrite) flags |= ObjectValueFlags.ReadOnly;
				if (prop.IsStatic) flags |= ObjectValueFlags.Global;
				return flags;
			}
		}
	}
}

// 
// PropertyValueReference.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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
using Mono.Debugging.Evaluation;
using Mono.Debugger;
using Mono.Debugging.Client;

namespace MonoDevelop.Debugger.Soft
{
	public class PropertyValueReference: ValueReference
	{
		PropertyInfoMirror property;
		object obj;
		TypeMirror declaringType;
		Value[] indexerArgs;
		
		public PropertyValueReference (EvaluationContext ctx, PropertyInfoMirror property, object obj, TypeMirror declaringType, Value[] indexerArgs): base (ctx)
		{
			this.property = property;
			this.obj = obj;
			this.declaringType = declaringType;
			this.indexerArgs = indexerArgs;
		}
		
		public override ObjectValueFlags Flags {
			get {
				return ObjectValueFlags.Property;
			}
		}

		public override string Name {
			get {
				int i = property.Name.LastIndexOf ('.');
				if (i != -1)
					return property.Name.Substring (i + 1);
				else
					return property.Name;
			}
		}

		public override object Type {
			get {
				return property.PropertyType;
			}
		}

		public override object Value {
			get {
				Context.AssertTargetInvokeAllowed ();
				SoftEvaluationContext ctx = (SoftEvaluationContext) Context;
				return ctx.RuntimeInvoke (property.GetGetMethod (), obj ?? declaringType, indexerArgs);
			}
			set {
				Context.AssertTargetInvokeAllowed ();
				SoftEvaluationContext ctx = (SoftEvaluationContext) Context;
				Value[] args = new Value [indexerArgs != null ? indexerArgs.Length + 1 : 1];
				indexerArgs.CopyTo (args, 0);
				args [args.Length - 1] = (Value) value;
				ctx.RuntimeInvoke (property.GetSetMethod (), obj ?? declaringType, args);
			}
		}
	}
}

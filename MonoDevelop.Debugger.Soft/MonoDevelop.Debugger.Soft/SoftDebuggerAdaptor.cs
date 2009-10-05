// 
// SoftDebuggerAdaptor.cs
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
using System.Diagnostics;
using Mono.Debugger;
using Mono.Debugging.Evaluation;
using Mono.Debugging.Client;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using ST = System.Threading;

namespace MonoDevelop.Debugger.Soft
{
	public class SoftDebuggerAdaptor : ObjectValueAdaptor
	{
		public SoftDebuggerAdaptor ()
		{
		}
		
		public override string CallToString (EvaluationContext ctx, object obj)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			
			if (obj is StringMirror)
				return ((StringMirror)obj).Value;
			else if (obj is EnumMirror) {
				EnumMirror eob = (EnumMirror) obj;
				return eob.StringValue;
			}
			else if (obj is PrimitiveValue)
				return ((PrimitiveValue)obj).Value.ToString ();
			else if (obj == null)
				return string.Empty;
			else if ((obj is ObjectMirror) && cx.AllowTargetInvoke) {
				ObjectMirror ob = (ObjectMirror) obj;
				MethodMirror ts = ob.Type.GetMethod ("ToString");
				StringMirror res = (StringMirror) ob.InvokeMethod (cx.Thread, ts, new Value[0]);
				return res.Value;
			}
			return GetValueTypeName (ctx, obj);
		}


		public override object Cast (EvaluationContext ctx, object obj, object targetType)
		{
			if (obj == null)
				return null;
			object res = TryCast (ctx, obj, targetType);
			if (res != null)
				return res;
			else
				throw new EvaluatorException ("Can't cast an object of type '{0}' to type '{1}'", GetValueTypeName (ctx, obj), GetTypeName (ctx, targetType));
		}

		public override object TryCast (EvaluationContext ctx, object obj, object targetType)
		{
			if (obj == null)
				return null;
			object otype = GetValueType (ctx, obj);
			if (otype is TypeMirror) {
				if ((targetType is TypeMirror) && ((TypeMirror)targetType).IsAssignableFrom ((TypeMirror)otype))
					return obj;
			} else if (otype is Type) {
				if ((targetType is Type) && ((Type)targetType).IsAssignableFrom ((Type)otype))
					return obj;
			}
			return null;
		}

		public override ICollectionAdaptor CreateArrayAdaptor (EvaluationContext ctx, object arr)
		{
			return new ArrayAdaptor ((ArrayMirror) arr);
		}

		public override object CreateNullValue (EvaluationContext ctx, object type)
		{
			return null;
		}

		protected override ObjectValue CreateObjectValueImpl (EvaluationContext ctx, Mono.Debugging.Backend.IObjectValueSource source, ObjectPath path, object obj, ObjectValueFlags flags)
		{
			string typeName = obj != null ? GetValueTypeName (ctx, obj) : "";
			
			if ((obj is PrimitiveValue) || (obj is StringMirror) || (obj is EnumMirror)) {
				return ObjectValue.CreatePrimitive (source, path, typeName, ctx.Evaluator.TargetObjectToExpression (ctx, obj), flags);
			}
			else if (obj is ArrayMirror) {
				return ObjectValue.CreateObject (source, path, typeName, ctx.Evaluator.TargetObjectToExpression (ctx, obj), flags, null);
			}
			else if (obj is ObjectMirror) {
				ObjectMirror co = (ObjectMirror) obj;
				TypeDisplayData tdata = GetTypeDisplayData (ctx, co.Type);
				
				string tvalue;
				if (!string.IsNullOrEmpty (tdata.ValueDisplayString))
					tvalue = EvaluateDisplayString (ctx, co, tdata.ValueDisplayString);
				else
					tvalue = ctx.Evaluator.TargetObjectToExpression (ctx, obj);
				
				string tname;
				if (!string.IsNullOrEmpty (tdata.TypeDisplayString))
					tname = EvaluateDisplayString (ctx, co, tdata.TypeDisplayString);
				else
					tname = typeName;
				
				ObjectValue oval = ObjectValue.CreateObject (source, path, tname, tvalue, flags, null);
				if (!string.IsNullOrEmpty (tdata.NameDisplayString))
					oval.Name = EvaluateDisplayString (ctx, co, tdata.NameDisplayString);
				return oval;
			}
			else if (obj is StructMirror) {
				string tvalue = ctx.Evaluator.TargetObjectToExpression (ctx, obj);
				return ObjectValue.CreateObject (source, path, typeName, tvalue, flags, null);
			}
			else if (obj == null)
				return ObjectValue.CreateObject (source, path, typeName, "(null)", flags, null);
			
			return ObjectValue.CreateUnknown (path.LastName);
		}

		public override object CreateTypeObject (EvaluationContext ctx, object type)
		{
			TypeMirror t = (TypeMirror) type;
			return t.GetTypeObject ();
		}

		public override object CreateValue (EvaluationContext ctx, object type, params object[] args)
		{
			ctx.AssertTargetInvokeAllowed ();
			
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			TypeMirror t = (TypeMirror) type;
			
			TypeMirror[] types = new TypeMirror [args.Length];
			for (int n=0; n<args.Length; n++)
				types [n] = (TypeMirror) GetValueType (ctx, args [n]);
			
			Value[] values = new Value[args.Length];
			for (int n=0; n<args.Length; n++)
				values[n] = (Value) args [n];
			
			MethodMirror ctor = OverloadResolve (cx, ".ctor", t, types, true, true, true);
			return t.NewInstance (cx.Thread, ctor, values);
		}

		public override object CreateValue (EvaluationContext ctx, object value)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			if (value is string)
				return cx.Thread.Domain.CreateString ((string)value);
			else
				return cx.Session.VirtualMachine.CreateValue (value);
		}

		public override object GetBaseValue (EvaluationContext ctx, object val)
		{
			return val;
		}

		public override object GetEnclosingType (EvaluationContext ctx)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			return cx.Frame.Method.DeclaringType;
		}

		public override string[] GetImportedNamespaces (EvaluationContext ctx)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			HashSet<string> namespaces = new HashSet<string> ();
			foreach (TypeMirror type in cx.Session.GetAllTypes ())
				namespaces.Add (type.Namespace);
			
			string[] nss = new string [namespaces.Count];
			namespaces.CopyTo (nss);
			return nss;
		}

		public override ValueReference GetIndexerReference (EvaluationContext ctx, object target, object index)
		{
			TypeMirror type = GetValueType (ctx, target) as TypeMirror;
			while (type != null) {
				foreach (PropertyInfoMirror prop in type.GetProperties ()) {
					MethodMirror met = prop.GetGetMethod ();
					if (met != null && !met.IsStatic && met.GetParameters ().Length > 0)
						return new PropertyValueReference (ctx, prop, target, null, new Value[] { (Value) index});
				}
				type = type.BaseType;
			}
			return null;
		}

		public override ValueReference GetLocalVariable (EvaluationContext ctx, string name)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			LocalVariable local = cx.Frame.Method.GetLocal (name);
			if (local != null)
				return new VariableValueReference (ctx, name, local);
			else
				return null;
		}

		public override IEnumerable<ValueReference> GetLocalVariables (EvaluationContext ctx)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			LocalVariable[] locals = cx.Frame.Method.GetLocals ();
			foreach (LocalVariable var in locals) {
				if (!var.IsArg)
					yield return new VariableValueReference (ctx, var.Name, var);
			}
		}

		public override ValueReference GetMember (EvaluationContext ctx, object t, object co, string name)
		{
			TypeMirror type = (TypeMirror) t;
			FieldInfoMirror field = type.GetField (name);
			if (field != null)
				return new FieldValueReference (ctx, field, co, type);
			PropertyInfoMirror prop = type.GetProperty (name);
			if (prop != null)
				return new PropertyValueReference (ctx, prop, co, type, null);
			else
				return null;
		}

		public override IEnumerable<ValueReference> GetMembers (EvaluationContext ctx, object t, object co, BindingFlags bindingFlags)
		{
			TypeMirror type = t as TypeMirror;
			while (type != null) {
				foreach (FieldInfoMirror field in type.GetFields ()) {
					if (field.IsStatic && ((bindingFlags & BindingFlags.Static) == 0))
						continue;
					if (!field.IsStatic && ((bindingFlags & BindingFlags.Instance) == 0))
						continue;
					if (field.IsPublic && ((bindingFlags & BindingFlags.Public) == 0))
						continue;
					if (!field.IsPublic && ((bindingFlags & BindingFlags.NonPublic) == 0))
						continue;
					yield return new FieldValueReference (ctx, field, co, type);
				}
				foreach (PropertyInfoMirror prop in type.GetProperties (bindingFlags)) {
					MethodMirror met = prop.GetGetMethod ();
					if (met == null || met.GetParameters ().Length != 0)
						continue;
					if (met.IsStatic && ((bindingFlags & BindingFlags.Static) == 0))
						continue;
					if (!met.IsStatic && ((bindingFlags & BindingFlags.Instance) == 0))
						continue;
					if (met.IsPublic && ((bindingFlags & BindingFlags.Public) == 0))
						continue;
					if (!met.IsPublic && ((bindingFlags & BindingFlags.NonPublic) == 0))
						continue;
					yield return new PropertyValueReference (ctx, prop, co, type, null);
				}
				type = type.BaseType;
			}
		}

		public override void GetNamespaceContents (EvaluationContext ctx, string namspace, out string[] childNamespaces, out string[] childTypes)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			HashSet<string> types = new HashSet<string> ();
			HashSet<string> namespaces = new HashSet<string> ();
			string namspacePrefix = namspace.Length > 0 ? namspace + "." : "";
			foreach (TypeMirror type in cx.Session.GetAllTypes ()) {
				if (type.Namespace == namspace || type.Namespace.StartsWith (namspacePrefix)) {
					namespaces.Add (type.Namespace);
					types.Add (type.FullName);
				}
			}
			childNamespaces = new string [namespaces.Count];
			namespaces.CopyTo (childNamespaces);
			
			childTypes = new string [types.Count];
			types.CopyTo (childTypes);
		}

		public override IEnumerable<ValueReference> GetParameters (EvaluationContext ctx)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			LocalVariable[] locals = cx.Frame.Method.GetLocals ();
			foreach (LocalVariable var in locals) {
				if (var.IsArg)
					yield return new VariableValueReference (ctx, var.Name, var);
			}
		}

		public override ValueReference GetThisReference (EvaluationContext ctx)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			if (cx.Frame.Method.IsStatic)
				return null;
			Value val = cx.Frame.GetThis ();
			return LiteralValueReference.CreateTargetObjectLiteral (ctx, "this", val);
		}

		public override object[] GetTypeArgs (EvaluationContext ctx, object type)
		{
			// TODO
			return new object [0];
		}

		public override object GetType (EvaluationContext ctx, string name, object[] typeArgs)
		{
			SoftEvaluationContext cx = (SoftEvaluationContext) ctx;
			int i = name.IndexOf (',');
			if (i != -1)
				name = name.Substring (0, i).Trim ();
			TypeMirror tm = cx.Session.GetType (name);
			if (tm != null)
				return tm;
			foreach (AssemblyMirror asm in cx.Thread.Domain.GetAssemblies ()) {
				tm = asm.GetType (name, false, false);
				if (tm != null)
					return tm;
			}
			return null;
		}

		public override IEnumerable<object> GetNestedTypes (EvaluationContext ctx, object type)
		{
			TypeMirror t = (TypeMirror) type;
			foreach (TypeMirror nt in t.GetNestedTypes ())
				yield return nt;
		}
		
		public override string GetTypeName (EvaluationContext ctx, object val)
		{
			TypeMirror tm = val as TypeMirror;
			if (tm != null)
				return tm.FullName;
			else
				return ((Type)val).FullName;
		}

		public override object GetValueType (EvaluationContext ctx, object val)
		{
			if (val is ObjectMirror)
				return ((ObjectMirror)val).Type;
			if (val is StructMirror)
				return ((StructMirror)val).Type;
			if (val is PrimitiveValue) {
				PrimitiveValue pv = (PrimitiveValue) val;
				if (pv.Value == null)
					return typeof(Object);
				else
					return ((PrimitiveValue)val).Value.GetType ();
			}
			if (val is ArrayMirror)
				return ((ArrayMirror)val).Type;
			if (val is EnumMirror)
				return ((EnumMirror)val).Type;
			throw new NotSupportedException ();
		}

		public override bool HasMethod (EvaluationContext gctx, object targetType, string methodName, object[] argTypes, BindingFlags flags)
		{
			SoftEvaluationContext ctx = (SoftEvaluationContext) gctx;
			
			TypeMirror[] types = new TypeMirror [argTypes.Length];
			for (int n=0; n<argTypes.Length; n++)
				types [n] = (TypeMirror) argTypes [n];
			
			MethodMirror met = OverloadResolve (ctx, methodName, (TypeMirror) targetType, types, (flags & BindingFlags.Instance) != 0, (flags & BindingFlags.Static) != 0, false);
			return met != null;
		}

		public override bool IsArray (EvaluationContext ctx, object val)
		{
			return val is ArrayMirror;
		}

		public override bool IsClass (object type)
		{
			TypeMirror t = type as TypeMirror;
			return t != null && (t.IsClass || t.IsValueType);
		}

		public override bool IsNull (EvaluationContext ctx, object val)
		{
			return val == null;
		}

		public override bool IsPrimitive (EvaluationContext ctx, object val)
		{
			return val is PrimitiveValue;
		}

		protected override TypeDisplayData OnGetTypeDisplayData (EvaluationContext gctx, object type)
		{
			SoftEvaluationContext ctx = (SoftEvaluationContext) gctx;
			TypeDisplayData td = new TypeDisplayData ();
			TypeMirror t = (TypeMirror) type;
			foreach (CustomAttributeDataMirror attr in t.GetCustomAttributes (true)) {
				string attName = attr.Constructor.DeclaringType.FullName;
				if (attName == "System.Diagnostics.DebuggerDisplayAttribute") {
					DebuggerDisplayAttribute at = BuildAttribute<DebuggerDisplayAttribute> (attr);
					td.NameDisplayString = at.Name;
					td.TypeDisplayString = at.Type;
					td.ValueDisplayString = at.Value;
				}
				else if (attName == "System.Diagnostics.DebuggerTypeProxyAttribute") {
					DebuggerTypeProxyAttribute at = BuildAttribute<DebuggerTypeProxyAttribute> (attr);
					td.IsProxyType = true;
					td.ProxyType = at.ProxyTypeName;
					if (!string.IsNullOrEmpty (td.ProxyType))
						ForceLoadType (ctx, t, td.ProxyType);
				}
			}
			foreach (FieldInfoMirror fi in t.GetFields ()) {
				DebuggerBrowsableAttribute att = GetAttribute <DebuggerBrowsableAttribute> (fi.GetCustomAttributes (true));
				if (att != null) {
					if (td.MemberData == null)
						td.MemberData = new Dictionary<string, DebuggerBrowsableState> ();
					td.MemberData [fi.Name] = att.State;
				}
			}
			foreach (PropertyInfoMirror pi in t.GetProperties ()) {
				DebuggerBrowsableAttribute att = GetAttribute <DebuggerBrowsableAttribute> (pi.GetCustomAttributes (true));
				if (att != null) {
					if (td.MemberData == null)
						td.MemberData = new Dictionary<string, DebuggerBrowsableState> ();
					td.MemberData [pi.Name] = att.State;
				}
			}
			return td;
		}
					    
		T GetAttribute<T> (CustomAttributeDataMirror[] attrs)
		{
			foreach (CustomAttributeDataMirror attr in attrs) {
				if (attr.Constructor.DeclaringType.FullName == typeof(T).FullName)
					return BuildAttribute<T> (attr);
			}
			return default(T);
		}
		
		void ForceLoadType (SoftEvaluationContext ctx, TypeMirror helperType, string typeName)
		{
			if (!ctx.AllowTargetInvoke)
				return;
			TypeMirror tm = helperType.GetTypeObject ().Type;
			TypeMirror[] ats = new TypeMirror[] { ctx.Session.GetType ("System.String") };
			MethodMirror met = OverloadResolve (ctx, "GetType", tm, ats, false, true, true);
			tm.InvokeMethod (ctx.Thread, met, new Value[] {(Value) CreateValue (ctx, typeName)});
		}
		
		T BuildAttribute<T> (CustomAttributeDataMirror attr)
		{
			List<object> args = new List<object> ();
			foreach (CustomAttributeTypedArgumentMirror arg in attr.ConstructorArguments) {
				Console.WriteLine ("pp arg:" + arg.Value + " tt:" + arg.ArgumentType);
				args.Add (arg.Value);
			}
			Type type = typeof(T);
			object at = Activator.CreateInstance (type, args.ToArray ());
			foreach (CustomAttributeNamedArgumentMirror arg in attr.NamedArguments) {
				object val = arg.TypedValue.Value;
				string postFix = "";
				if (arg.TypedValue.ArgumentType == typeof(Type))
					postFix = "TypeName";
				if (arg.Field != null)
					type.GetField (arg.Field.Name + postFix).SetValue (at, val);
				else if (arg.Property != null)
					type.GetProperty (arg.Property.Name + postFix).SetValue (at, val, null);
			}
			return (T) at;
		}
		
		TypeMirror ToTypeMirror (EvaluationContext ctx, object type)
		{
			TypeMirror t = type as TypeMirror;
			if (t != null)
				return t;
			return (TypeMirror) GetType (ctx, ((Type)type).FullName);
		}

		public override object RuntimeInvoke (EvaluationContext gctx, object targetType, object target, string methodName, object[] argTypes, object[] argValues)
		{
			SoftEvaluationContext ctx = (SoftEvaluationContext) gctx;
			ctx.AssertTargetInvokeAllowed ();
			
			TypeMirror type = target != null ? ((ObjectMirror) target).Type : (TypeMirror) targetType;
			
			TypeMirror[] types = new TypeMirror [argTypes.Length];
			for (int n=0; n<argTypes.Length; n++)
				types [n] = ToTypeMirror (ctx, argTypes [n]);
			
			Value[] values = new Value[argValues.Length];
			for (int n=0; n<argValues.Length; n++)
				values[n] = (Value) argValues [n];
			
			MethodMirror method = OverloadResolve (ctx, methodName, type, types, target != null, target == null, true);
			return ctx.RuntimeInvoke (method, target ?? targetType, values);
		}
		
		public static MethodMirror OverloadResolve (SoftEvaluationContext ctx, string methodName, TypeMirror type, TypeMirror[] argtypes, bool allowInstance, bool allowStatic, bool throwIfNotFound)
		{
			List<MethodMirror> candidates = new List<MethodMirror> ();
			TypeMirror currentType = type;
			while (currentType != null) {
				foreach (MethodMirror met in currentType.GetMethods ()) {
					if (met.Name == methodName) {
						ParameterInfoMirror[] pars = met.GetParameters ();
						if (pars.Length == argtypes.Length && (met.IsStatic && allowStatic || !met.IsStatic && allowInstance))
							candidates.Add (met);
					}
				}
				currentType = currentType.BaseType;
			}
			
			if (candidates.Count == 1) {
				string error;
				int matchCount;
				if (IsApplicable (ctx, candidates[0], argtypes, out error, out matchCount))
					return candidates [0];

				if (throwIfNotFound)
					throw new EvaluatorException ("Invalid arguments for method `{0}': {1}", methodName, error);
				else
					return null;
			}

			if (candidates.Count == 0) {
				if (throwIfNotFound)
					throw new EvaluatorException ("Method `{0}' not found in type `{1}'.", methodName, type.Name);
				else
					return null;
			}

			return OverloadResolve (ctx, methodName, argtypes, candidates, throwIfNotFound);
		}

		static bool IsApplicable (SoftEvaluationContext ctx, MethodMirror method, TypeMirror[] types, out string error, out int matchCount)
		{
			ParameterInfoMirror[] mparams = method.GetParameters ();
			matchCount = 0;

			for (int i = 0; i < types.Length; i++) {
				
				TypeMirror param_type = mparams[i].ParameterType;

				if (param_type.FullName == types [i].FullName) {
					matchCount++;
					continue;
				}

				if (param_type.IsAssignableFrom (types [i]))
					continue;

				error = String.Format (
					"Argument {0}: Cannot implicitly convert `{1}' to `{2}'",
					i, types [i].FullName, param_type.FullName);
				return false;
			}

			error = null;
			return true;
		}

		static MethodMirror OverloadResolve (SoftEvaluationContext ctx, string methodName, TypeMirror[] argtypes, List<MethodMirror> candidates, bool throwIfNotFound)
		{
			// Ok, no we need to find an exact match.
			MethodMirror match = null;
			int bestCount = -1;
			bool repeatedBestCount = false;
			
			foreach (MethodMirror method in candidates) {
				string error;
				int matchCount;
				
				if (!IsApplicable (ctx, method, argtypes, out error, out matchCount))
					continue;

				if (matchCount == bestCount) {
					repeatedBestCount = true;
				} else if (matchCount > bestCount) {
					match = method;
					bestCount = matchCount;
					repeatedBestCount = false;
				}
			}
			
			if (match == null) {
				if (!throwIfNotFound)
					return null;
				if (methodName != null)
					throw new EvaluatorException ("Invalid arguments for method `{0}'.", methodName);
				else
					throw new EvaluatorException ("Invalid arguments for indexer.");
			}
			
			if (repeatedBestCount) {
				// If there is an ambiguous match, just pick the first match. If the user was expecting
				// something else, he can provide more specific arguments
				
/*				if (!throwIfNotFound)
					return null;
				if (methodName != null)
					throw new EvaluatorException ("Ambiguous method `{0}'; need to use full name", methodName);
				else
					throw new EvaluatorException ("Ambiguous arguments for indexer.", methodName);
*/			}
			 
			return match;
		}		

		public override object TargetObjectToObject (EvaluationContext gctx, object obj)
		{
			SoftEvaluationContext ctx = (SoftEvaluationContext) gctx;
			
			if (obj is ArrayMirror) {
				ArrayMirror arr = (ArrayMirror) obj;
				StringBuilder tn = new StringBuilder (arr.Type.GetElementType ().FullName);
				tn.Append ("[");
				for (int n=0; n<arr.Rank; n++) {
					if (n>0)
						tn.Append (',');
					tn.Append (arr.GetLength (n));
				}
				tn.Append ("]");
				return new LiteralExp (tn.ToString ());
			}
			else if (obj is StringMirror)
				return ((StringMirror)obj).Value;
			else if (obj is ObjectMirror) {
				ObjectMirror co = (ObjectMirror) obj;
				TypeDisplayData tdata = GetTypeDisplayData (ctx, co.Type);
				if (!string.IsNullOrEmpty (tdata.ValueDisplayString))
					return new LiteralExp (EvaluateDisplayString (ctx, co, tdata.ValueDisplayString));
				// Return the type name
				if (tdata.TypeDisplayString != null)
					return new LiteralExp ("{" + tdata.TypeDisplayString + "}");
				return new LiteralExp ("{" + co.Type.FullName + "}");
			}
			else if (obj is StructMirror) {
				StructMirror co = (StructMirror) obj;
				if (ctx.AllowTargetInvoke) {
					if (co.Type.FullName == "System.Decimal")
						return new LiteralExp (CallToString (ctx, co));
				}
				return new LiteralExp ("{" + co.Type.FullName + "}");
			}
			else if (obj is EnumMirror) {
				EnumMirror eob = (EnumMirror) obj;
				return new LiteralExp (eob.StringValue);
			}
			else if (obj is PrimitiveValue) {
				return ((PrimitiveValue)obj).Value;
			}
			else if (obj == null)
				return new LiteralExp ("(null)");
			
			return new LiteralExp ("?");
		}
	}

	class MethodCall: AsyncOperation
	{
		SoftEvaluationContext ctx;
		MethodMirror function;
		object object_argument;
		Value[] param_objects;
		Value result;
		ST.Thread thread;
		InvocationException exception;
		
		public MethodCall (SoftEvaluationContext ctx, MethodMirror function, object object_argument, Value[] param_objects)
		{
			this.ctx = ctx;
			this.function = function;
			this.object_argument = object_argument;
			this.param_objects = param_objects;
		}

		public override void Invoke ( )
		{
			thread = new ST.Thread (delegate () {
				try {
					if (object_argument is ObjectMirror)
						result = ((ObjectMirror)object_argument).InvokeMethod (ctx.Thread, function, param_objects);
					else
						result = ((TypeMirror)object_argument).InvokeMethod (ctx.Thread, function, param_objects);
				} catch (InvocationException ex) {
					exception = ex;
				}
			});
			thread.IsBackground = true;
			thread.Start ();
		}

		public override void Abort ( )
		{
			thread.Abort ();
		}

		public override bool WaitForCompleted (int timeout)
		{
			return thread.Join (timeout);
		}

		public Value ReturnValue {
			get {
				if (exception != null)
					throw new EvaluatorException (exception.Message);
				return result;
			}
		}
	}
}

﻿using System;

namespace ICSharpCode.Decompiler.Tests.TestCases.Pretty
{
	public class PatternMatching
	{
		public class X
		{
			public int A { get; set; }
			public string B { get; set; }
			public object C { get; set; }
		}

		public struct S
		{
			public int I;
		}

		public void SimpleTypePattern(object x)
		{
			if (x is string value)
			{
				Console.WriteLine(value);
			}
		}

		public void TypePatternWithShortcircuit(object x)
		{
			Use(F() && x is string text && text.Contains("a"));
			if (F() && x is string text2 && text2.Contains("a"))
			{
				Console.WriteLine(text2);
			}
		}

		public void TypePatternWithShortcircuitAnd(object x)
		{
			if (x is string text && text.Contains("a"))
			{
				Console.WriteLine(text);
			}
			else
			{
				Console.WriteLine();
			}
		}

		public void TypePatternWithShortcircuitOr(object x)
		{
			if (!(x is string text) || text.Contains("a"))
			{
				Console.WriteLine();
			}
			else
			{
				Console.WriteLine(text);
			}
		}

		public void TypePatternWithShortcircuitOr2(object x)
		{
			if (F() || !(x is string value))
			{
				Console.WriteLine();
			}
			else
			{
				Console.WriteLine(value);
			}
		}

		public void TypePatternValueTypesCondition(object x)
		{
			if (x is int num)
			{
				Console.WriteLine("Integer: " + num);
			}
			else
			{
				Console.WriteLine("else");
			}
		}

		public void TypePatternValueTypesCondition2()
		{
			if (GetObject() is int num)
			{
				Console.WriteLine("Integer: " + num);
			}
			else
			{
				Console.WriteLine("else");
			}
		}

		public void TypePatternValueTypesWithShortcircuitAnd(object x)
		{
			if (x is int num && num.GetHashCode() > 0)
			{
				Console.WriteLine("Positive integer: " + num);
			}
			else
			{
				Console.WriteLine("else");
			}
		}

		public void TypePatternValueTypesWithShortcircuitOr(object x)
		{
			if (!(x is int value) || value.GetHashCode() > 0)
			{
				Console.WriteLine();
			}
			else
			{
				Console.WriteLine(value);
			}
		}

#if ROSLYN3 || OPT
		// Roslyn 2.x generates a complex infeasible path in debug builds, which RemoveInfeasiblePathTransform
		// currently cannot handle. Because this would increase the complexity of that transform, we ignore
		// this case.
		public void TypePatternValueTypesWithShortcircuitOr2(object x)
		{
			if (F() || !(x is int value))
			{
				Console.WriteLine();
			}
			else
			{
				Console.WriteLine(value);
			}
		}
#endif

		public void TypePatternGenerics<T>(object x)
		{
			if (x is T val)
			{
				Console.WriteLine(val.GetType().FullName);
			}
			else
			{
				Console.WriteLine("not a " + typeof(T).FullName);
			}
		}

		public void TypePatternGenericRefType<T>(object x) where T : class
		{
			if (x is T val)
			{
				Console.WriteLine(val.GetType().FullName);
			}
			else
			{
				Console.WriteLine("not a " + typeof(T).FullName);
			}
		}

		public void TypePatternGenericValType<T>(object x) where T : struct
		{
			if (x is T val)
			{
				Console.WriteLine(val.GetType().FullName);
			}
			else
			{
				Console.WriteLine("not a " + typeof(T).FullName);
			}
		}

		public void TypePatternValueTypesWithShortcircuitAndMultiUse(object x)
		{
			if (x is int num && num.GetHashCode() > 0 && num % 2 == 0)
			{
				Console.WriteLine("Positive integer: " + num);
			}
			else
			{
				Console.WriteLine("else");
			}
		}

		public void TypePatternValueTypesWithShortcircuitAndMultiUse2(object x)
		{
			if ((x is int num && num.GetHashCode() > 0 && num % 2 == 0) || F())
			{
				Console.WriteLine("true");
			}
			else
			{
				Console.WriteLine("else");
			}
		}

		public void TypePatternValueTypesWithShortcircuitAndMultiUse3(object x)
		{
			if (F() || (x is int num && num.GetHashCode() > 0 && num % 2 == 0))
			{
				Console.WriteLine("true");
			}
			else
			{
				Console.WriteLine("else");
			}
		}

		public void TypePatternValueTypes()
		{
			Use(F() && GetObject() is int num && num.GetHashCode() > 0 && num % 2 == 0);
		}

		public static void NotTypePatternVariableUsedOutsideTrueBranch(object x)
		{
			string text = x as string;
			if (text != null && text.Length > 5)
			{
				Console.WriteLine("pattern matches");
			}
			if (text != null && text.Length > 10)
			{
				Console.WriteLine("other use!");
			}
		}

		public static void NotTypePatternBecauseVarIsNotDefAssignedInCaseOfFallthrough(object x)
		{
#if OPT
			string obj = x as string;
			if (obj == null)
			{
				Console.WriteLine("pattern doesn't match");
			}
			Console.WriteLine(obj == null);
#else
			string text = x as string;
			if (text == null)
			{
				Console.WriteLine("pattern doesn't match");
			}
			Console.WriteLine(text == null);
#endif
		}

		public void GenericTypePatternInt<T>(T x)
		{
			if (x is int value)
			{
				Console.WriteLine(value);
			}
			else
			{
				Console.WriteLine("not an int");
			}
		}

		public void GenericValueTypePatternInt<T>(T x) where T : struct
		{
			if (x is int value)
			{
				Console.WriteLine(value);
			}
			else
			{
				Console.WriteLine("not an int");
			}
		}

		public void GenericRefTypePatternInt<T>(T x) where T : class
		{
			if (x is int value)
			{
				Console.WriteLine(value);
			}
			else
			{
				Console.WriteLine("not an int");
			}
		}

		public void GenericTypePatternString<T>(T x)
		{
			if (x is string value)
			{
				Console.WriteLine(value);
			}
			else
			{
				Console.WriteLine("not a string");
			}
		}

		public void GenericRefTypePatternString<T>(T x) where T : class
		{
			if (x is string value)
			{
				Console.WriteLine(value);
			}
			else
			{
				Console.WriteLine("not a string");
			}
		}

		public void GenericValueTypePatternStringRequiresCastToObject<T>(T x) where T : struct
		{
			if ((object)x is string value)
			{
				Console.WriteLine(value);
			}
			else
			{
				Console.WriteLine("not a string");
			}
		}

#if CS80
		public void RecursivePattern_Type(object x)
		{
			if (x is X { C: string c })
			{
				Console.WriteLine("Test " + c);
			}
			else
			{
				Console.WriteLine("not Test");
			}
		}

		public void RecursivePattern_Constant(object obj)
		{
			if (obj is X { C: null } x)
			{
				Console.WriteLine("Test " + x);
			}
			else
			{
				Console.WriteLine("not Test");
			}
		}

		public void RecursivePattern_StringConstant(object obj)
		{
			if (obj is X { B: "Hello" } x)
			{
				Console.WriteLine("Test " + x);
			}
			else
			{
				Console.WriteLine("not Test");
			}
		}

		public void RecursivePattern_MultipleConstants(object obj)
		{
			if (obj is X { A: 42, B: "Hello" } x)
			{
				Console.WriteLine("Test " + x);
			}
			else
			{
				Console.WriteLine("not Test");
			}
		}

		public void RecursivePattern_ValueTypeWithField(object obj)
		{
			if (obj is S { I: 42 } s)
			{
				Console.WriteLine("Test " + s);
			}
			else
			{
				Console.WriteLine("not Test");
			}
		}

		public void RecursivePattern_MultipleConstantsMixedWithVar(object x)
		{
			if (x is X { A: 42, C: var c, B: "Hello" })
			{
				Console.WriteLine("Test " + c);
			}
			else
			{
				Console.WriteLine("not Test");
			}
		}
#endif
		private bool F()
		{
			return true;
		}

		private object GetObject()
		{
			throw new NotImplementedException();
		}

		private void Use(bool x)
		{
		}
	}
}

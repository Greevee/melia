using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// The handler sources the pricer reads its hit counts, geometry and pad
	/// timings out of.
	/// </summary>
	/// <remarks>
	/// Nothing in the skill data links a skill to the buffs it applies, the
	/// pads it creates or the number of times it hits, so all three are derived
	/// from the code rather than declared. Laima is indexed second so its
	/// overrides win, matching what actually runs.
	/// </remarks>
	public static class SfrSources
	{
		private static readonly string[] SkillRoots =
		[
			Path.Combine("src", "ZoneServer", "Skills", "Handlers"),
			Path.Combine("src", "ZoneServer", "Packages", "Laima", "Skills"),
		];

		private static readonly string[] BuffRoots =
		[
			Path.Combine("src", "ZoneServer", "Buffs", "Handlers"),
			Path.Combine("src", "ZoneServer", "Packages", "Laima", "Buffs"),
		];

		private static readonly string[] PadRoots =
		[
			Path.Combine("src", "ZoneServer", "Packages", "Laima", "Pads"),
			Path.Combine("src", "ZoneServer", "Skills", "Handlers", "Pads"),
			Path.Combine("src", "ZoneServer", "Pads"),
		];

		private static readonly Regex SkillAttribute = new(@"\[SkillHandler\(([^)]+)\)\]", RegexOptions.Compiled);
		private static readonly Regex BuffAttribute = new(@"\[BuffHandler\(([^)]+)\)\]", RegexOptions.Compiled);
		private static readonly Regex PadAttribute = new(@"\[PadHandler\(([^)]+)\)\]", RegexOptions.Compiled);
		private static readonly Regex SkillIdRef = new(@"SkillId\.(\w+)", RegexOptions.Compiled);
		private static readonly Regex BuffIdRef = new(@"BuffId\.(\w+)", RegexOptions.Compiled);
		private static readonly Regex PadNameRef = new(@"PadName\.(\w+)", RegexOptions.Compiled);
		private static readonly Regex PadStringRef = new(@"""(\w+)""", RegexOptions.Compiled);
		private static readonly Regex ConstDecl = new(@"const\s+(?:int|float|double|long)\s+(\w+)\s*=\s*([^;]+);", RegexOptions.Compiled);
		private static readonly Regex LocalDecl = new(@"\bvar\s+(\w+)\s*=\s*([^;]+);", RegexOptions.Compiled);
		private static readonly Regex CastPrefix = new(@"\(\s*(?:int|float|long|double)\s*\)", RegexOptions.Compiled);
		private static readonly Regex LevelToken = new(@"(?:pad\.)?(?:Trigger\.)?(?:[Ss]kill\.Level|NumArg1)", RegexOptions.Compiled);
		private static readonly Regex NumericSuffix = new(@"(?<=[0-9])[fFdD]\b", RegexOptions.Compiled);
		private static readonly Regex PureArithmetic = new(@"^[0-9.+\-*/() ]+$", RegexOptions.Compiled);
		private static readonly Regex Identifier = new(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);
		private static readonly Regex SetUpdateInterval = new(@"SetUpdateInterval\(\s*([^)]+)\)", RegexOptions.Compiled);
		private static readonly Regex PadTargetCap = new(@"Trigger\.(?:MaxActorCount|MaxConcurrentUseCount)\s*=\s*([^;]+);", RegexOptions.Compiled);

		private static Dictionary<string, string> _skillHandlers;
		private static Dictionary<string, string> _buffHandlers;
		private static Dictionary<string, PadInfo> _pads;
		private static readonly Dictionary<string, Dictionary<string, string>> _literals = [];
		private static readonly object _syncLock = new();

		/// <summary>
		/// Returns the handler source that runs for a skill, or null when it
		/// has none.
		/// </summary>
		/// <param name="skillName"></param>
		public static string SkillHandler(string skillName)
		{
			lock (_syncLock)
				_skillHandlers ??= IndexByAttribute(SkillRoots, SkillAttribute, SkillIdRef);

			return _skillHandlers.TryGetValue(skillName, out var text) ? text : null;
		}

		/// <summary>
		/// Returns the handler source for a buff, or null when it has none.
		/// </summary>
		/// <param name="buffName"></param>
		public static string BuffHandler(string buffName)
		{
			lock (_syncLock)
				_buffHandlers ??= IndexByAttribute(BuffRoots, BuffAttribute, BuffIdRef);

			return _buffHandlers.TryGetValue(buffName, out var text) ? text : null;
		}

		/// <summary>
		/// Every skill that has a handler, with its source.
		/// </summary>
		public static IReadOnlyDictionary<string, string> AllSkillHandlers
		{
			get
			{
				lock (_syncLock)
					return _skillHandlers ??= IndexByAttribute(SkillRoots, SkillAttribute, SkillIdRef);
			}
		}

		/// <summary>
		/// Pad name to what one press gets out of that pad.
		/// </summary>
		public static IReadOnlyDictionary<string, PadInfo> Pads
		{
			get
			{
				lock (_syncLock)
				{
					if (_pads != null)
						return _pads;

					_pads = [];

					foreach (var root in PadRoots)
					{
						var full = Path.Combine(SfrData.Root, root);
						if (!Directory.Exists(full))
							continue;

						foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
						{
							var text = ReadFile(file);
							if (text == null)
								continue;

							var names = new HashSet<string>();

							foreach (Match attr in PadAttribute.Matches(text))
							{
								foreach (Match m in PadNameRef.Matches(attr.Groups[1].Value))
									names.Add(m.Groups[1].Value);

								// A handler may name its pad as PadName.X or as a bare string.
								foreach (Match m in PadStringRef.Matches(attr.Groups[1].Value))
									names.Add(m.Groups[1].Value);
							}

							if (names.Count == 0)
								continue;

							var interval = SetUpdateInterval.Match(text);
							var info = new PadInfo
							{
								Text = text,
								IntervalExpression = interval.Success ? interval.Groups[1].Value : null,
								CapExpressions = PadTargetCap.Matches(text).Select(m => m.Groups[1].Value).ToArray(),
								DamagePerTick = PadDamagePerTick(text),
							};

							foreach (var name in names)
								_pads[name] = info;
						}
					}

					return _pads;
				}
			}
		}

		/// <summary>
		/// Returns the skill-factor multiple one pad tick deals, or zero when
		/// the pad deals no damage.
		/// </summary>
		/// <remarks>
		/// A pad delivers damage three ways: PadDamageEnemy, its own
		/// SCR_SkillHit, or a debuff that carries the damage. A heal pad's
		/// factor is a heal factor, which this model does not price.
		/// </remarks>
		/// <param name="text"></param>
		private static float PadDamagePerTick(string text)
		{
			if (text.Contains("SCR_CalculateHeal"))
				return 0f;

			var direct = Regex.Match(text, @"PadDamageEnemy\(\s*\w+\s*,\s*([0-9.]+)f?");
			if (direct.Success)
				return SfrData.ParseFloat(direct.Groups[1].Value);

			var targeted = Regex.Match(text, @"PadTargetDamage\([^)]*?RelationType\.\w+\s*,\s*([0-9.]+)f?");
			if (targeted.Success)
				return SfrData.ParseFloat(targeted.Groups[1].Value);

			if (Regex.IsMatch(text, @"PadDamageEnemy\(|PadTargetDamage\(|SCR_SkillHit"))
				return 1f;

			if (BuffIdRef.Matches(text).Select(m => m.Groups[1].Value).Distinct().Any(BuffDamagesOnTick))
				return 1f;

			return 0f;
		}

		/// <summary>
		/// Methods whose damage calls are the buff's own clock rather than a
		/// counter-attack.
		/// </summary>
		private static readonly string[] BuffTickMethods = ["WhileActive", "OnUpdate", "OnActivate", "OnStart", "Update"];

		/// <summary>
		/// Returns whether a buff deals damage on its own tick, rather than
		/// when its holder is hit.
		/// </summary>
		/// <remarks>
		/// A buff that damages whoever attacked its holder is a counter and
		/// belongs to that attack, not to the press that applied the shield.
		/// </remarks>
		/// <param name="buffName"></param>
		public static bool BuffDamagesOnTick(string buffName)
		{
			var source = BuffHandler(buffName);
			if (source == null)
				return false;

			foreach (Match hit in Regex.Matches(source, @"SCR_SkillHit|TakeDamage"))
			{
				string method = null;

				foreach (Match decl in Regex.Matches(source[..hit.Index], @"\b(?:void|Task|bool|int|float)\s+(\w+)\s*\("))
					method = decl.Groups[1].Value;

				if (method != null && BuffTickMethods.Contains(method))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns every name a source declares as a const or a local, mapped
		/// to the expression it was declared with.
		/// </summary>
		/// <remarks>
		/// Values are kept as source rather than numbers so a local declared
		/// from other constants stays resolvable. The first declaration wins,
		/// so a later self-referencing assignment reads as the baseline.
		/// </remarks>
		/// <param name="text"></param>
		public static Dictionary<string, string> Literals(string text)
		{
			lock (_syncLock)
			{
				if (_literals.TryGetValue(text, out var cached))
					return cached;

				var found = new Dictionary<string, string>();

				foreach (Match m in ConstDecl.Matches(text))
					found.TryAdd(m.Groups[1].Value, m.Groups[2].Value);

				foreach (Match m in LocalDecl.Matches(text))
					found.TryAdd(m.Groups[1].Value, m.Groups[2].Value);

				return _literals[text] = found;
			}
		}

		/// <summary>
		/// Returns the numeric value of a timing expression, or null when it
		/// reads something the model cannot see.
		/// </summary>
		/// <remarks>
		/// Handles the shapes pad and skill handlers actually use: a literal, a
		/// const, a local, and a base plus perLevel times skill level sum. The
		/// level is substituted rather than dropped, because a pad whose
		/// lifetime scales with rank is not unknown.
		/// </remarks>
		/// <param name="expression"></param>
		/// <param name="names"></param>
		/// <param name="level"></param>
		/// <param name="depth"></param>
		public static float? ResolveExpression(string expression, IReadOnlyDictionary<string, string> names, int level, int depth = 0)
		{
			if (expression == null || depth > 4)
				return null;

			var expr = CastPrefix.Replace(expression, "").Trim();
			expr = LevelToken.Replace(expr, level.ToString(CultureInfo.InvariantCulture));
			expr = NumericSuffix.Replace(expr, "");

			if (PureArithmetic.IsMatch(expr))
				return Arithmetic.Evaluate(expr);

			var expanded = expr;

			foreach (var name in Identifier.Matches(expr).Select(m => m.Value).Distinct().OrderByDescending(n => n.Length))
			{
				if (names.TryGetValue(name, out var replacement))
					expanded = Regex.Replace(expanded, @"\b" + Regex.Escape(name) + @"\b", "(" + replacement + ")");
			}

			if (expanded == expr)
				return null;

			return ResolveExpression(expanded, names, level, depth + 1);
		}

		/// <summary>
		/// Returns milliseconds from a TimeSpan assignment matching the given
		/// pattern, following one level of indirection through a local.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="pattern"></param>
		/// <param name="names"></param>
		/// <param name="level"></param>
		public static float? TimeSpanMs(string text, string pattern, IReadOnlyDictionary<string, string> names, int level)
		{
			foreach (var (unit, scale) in new[] { ("Milliseconds", 1f), ("Seconds", 1000f) })
			{
				var m = Regex.Match(text, pattern + @"\s*TimeSpan\.From" + unit + @"\(\s*([^;]+?)\)\s*;");
				if (!m.Success)
					continue;

				var value = ResolveExpression(m.Groups[1].Value, names, level);
				if (value != null)
					return value * scale;
			}

			// `pad.Trigger.LifeTime = duration;` - the TimeSpan was built earlier.
			var indirect = Regex.Match(text, pattern + @"\s*(\w+)\s*;");
			if (indirect.Success)
				return TimeSpanMs(text, @"\b" + Regex.Escape(indirect.Groups[1].Value) + @"\s*=", names, level);

			return null;
		}

		/// <summary>
		/// Returns the span of the braced block that follows the given offset,
		/// or null when the body has no braces.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="start"></param>
		public static (int Start, int End)? BracedBlock(string text, int start)
		{
			var openAt = text.IndexOf('{', start);
			if (openAt < 0)
				return null;

			var depth = 0;

			for (var i = openAt; i < text.Length; ++i)
			{
				if (text[i] == '{')
					depth++;
				else if (text[i] == '}')
				{
					depth--;
					if (depth == 0)
						return (openAt, i);
				}
			}

			return null;
		}

		/// <summary>
		/// Returns the span of a method's body by name, for following a call
		/// one level down.
		/// </summary>
		/// <remarks>
		/// Matched by scanning call-shaped occurrences and testing the line for
		/// an access modifier, rather than one regex for the whole signature -
		/// a pattern general enough for the real declarations backtracks
		/// catastrophically when it misses.
		/// </remarks>
		/// <param name="text"></param>
		/// <param name="name"></param>
		public static (int Start, int End)? MethodBody(string text, string name)
		{
			foreach (Match m in Regex.Matches(text, @"\b" + Regex.Escape(name) + @"\s*\("))
			{
				var lineStart = text.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1;
				var head = text[lineStart..m.Index];

				if (!Regex.IsMatch(head, @"^\s*(?:private|public|protected|internal)\b"))
					continue;

				var close = text.IndexOf(')', m.Index + m.Length - 1);
				if (close < 0)
					continue;

				// An abstract or interface declaration ends at the semicolon, with no body.
				var tail = text[(close + 1)..Math.Min(text.Length, close + 41)].Split('{')[0];
				if (tail.Contains(';'))
					continue;

				return BracedBlock(text, close);
			}

			return null;
		}

		/// <summary>
		/// Indexes handler sources by the ids their attribute names.
		/// </summary>
		/// <param name="roots"></param>
		/// <param name="attribute"></param>
		/// <param name="idRef"></param>
		private static Dictionary<string, string> IndexByAttribute(string[] roots, Regex attribute, Regex idRef)
		{
			var found = new Dictionary<string, string>();

			foreach (var root in roots)
			{
				var full = Path.Combine(SfrData.Root, root);
				if (!Directory.Exists(full))
					continue;

				foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
				{
					var text = ReadFile(file);
					if (text == null)
						continue;

					foreach (Match attr in attribute.Matches(text))
					{
						foreach (Match id in idRef.Matches(attr.Groups[1].Value))
							found[id.Groups[1].Value] = text;
					}
				}
			}

			return found;
		}

		/// <summary>
		/// Reads a source file, returning null when it cannot be opened.
		/// </summary>
		/// <param name="path"></param>
		private static string ReadFile(string path)
		{
			try
			{
				return File.ReadAllText(path);
			}
			catch (IOException)
			{
				return null;
			}
		}
	}

	/// <summary>
	/// What one press gets out of a pad, before its timings are resolved at a
	/// particular skill level.
	/// </summary>
	public class PadInfo
	{
		/// <summary>
		/// The pad handler's source.
		/// </summary>
		public string Text { get; init; }

		/// <summary>
		/// The expression its update interval is set from, if it ticks.
		/// </summary>
		public string IntervalExpression { get; init; }

		/// <summary>
		/// Expressions bounding how many targets it holds at once.
		/// </summary>
		public string[] CapExpressions { get; init; } = [];

		/// <summary>
		/// Skill-factor multiple one tick deals, or zero for a utility pad.
		/// </summary>
		public float DamagePerTick { get; init; }
	}

	/// <summary>
	/// A four-operator arithmetic evaluator, for the timing expressions the
	/// handlers write their pad lifetimes as.
	/// </summary>
	public static class Arithmetic
	{
		/// <summary>
		/// Returns the value of an arithmetic expression, or null when it does
		/// not parse.
		/// </summary>
		/// <param name="expression"></param>
		public static float? Evaluate(string expression)
		{
			var at = 0;

			try
			{
				var value = ParseSum(expression, ref at);

				SkipSpace(expression, ref at);

				return at == expression.Length ? value : null;
			}
			catch (FormatException)
			{
				return null;
			}
		}

		/// <summary>
		/// Parses a run of additions and subtractions.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="at"></param>
		private static float ParseSum(string text, ref int at)
		{
			var value = ParseProduct(text, ref at);

			while (true)
			{
				SkipSpace(text, ref at);

				if (at < text.Length && text[at] == '+')
				{
					at++;
					value += ParseProduct(text, ref at);
				}
				else if (at < text.Length && text[at] == '-')
				{
					at++;
					value -= ParseProduct(text, ref at);
				}
				else
				{
					return value;
				}
			}
		}

		/// <summary>
		/// Parses a run of multiplications and divisions.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="at"></param>
		private static float ParseProduct(string text, ref int at)
		{
			var value = ParseAtom(text, ref at);

			while (true)
			{
				SkipSpace(text, ref at);

				if (at < text.Length && text[at] == '*')
				{
					at++;
					value *= ParseAtom(text, ref at);
				}
				else if (at < text.Length && text[at] == '/')
				{
					at++;

					var divisor = ParseAtom(text, ref at);
					if (divisor == 0)
						throw new FormatException("Division by zero.");

					value /= divisor;
				}
				else
				{
					return value;
				}
			}
		}

		/// <summary>
		/// Parses a number, a parenthesized group, or a unary sign.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="at"></param>
		private static float ParseAtom(string text, ref int at)
		{
			SkipSpace(text, ref at);

			if (at >= text.Length)
				throw new FormatException("Unexpected end of expression.");

			if (text[at] == '-')
			{
				at++;
				return -ParseAtom(text, ref at);
			}

			if (text[at] == '+')
			{
				at++;
				return ParseAtom(text, ref at);
			}

			if (text[at] == '(')
			{
				at++;

				var value = ParseSum(text, ref at);

				SkipSpace(text, ref at);

				if (at >= text.Length || text[at] != ')')
					throw new FormatException("Unclosed group.");

				at++;

				return value;
			}

			var start = at;

			while (at < text.Length && (char.IsDigit(text[at]) || text[at] == '.'))
				at++;

			if (at == start)
				throw new FormatException($"Expected a number at {at}.");

			return float.Parse(text[start..at], NumberStyles.Float, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Advances past whitespace.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="at"></param>
		private static void SkipSpace(string text, ref int at)
		{
			while (at < text.Length && text[at] == ' ')
				at++;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// What a skill's handler delivers per press: how many times it hits, how
	/// wide it reaches, and what it hands to pads and debuffs.
	/// </summary>
	/// <remarks>
	/// Every rule here was added after a specific skill priced into the
	/// thousands. The data's multiHitCount is dead, splash fields are zero for
	/// every projectile, and a handler that calls the explicit
	/// GetSplashParameters form makes its own .txt geometry decorative - so all
	/// of it comes out of the code.
	/// </remarks>
	public static class SfrHandlerAnalysis
	{
		/// <summary>
		/// Helpers that resolve their own targets and apply LimitBySDR inside.
		/// </summary>
		private static readonly string[] SdrHelpers = ["SkillAttack", "SplashDamage"];

		private static readonly Regex PadNameRef = new(@"PadName\.(\w+)", RegexOptions.Compiled);
		private static readonly Regex BuffIdRef = new(@"BuffId\.(\w+)", RegexOptions.Compiled);
		private static readonly Regex CountedForLoop = new(@"for\s*\(\s*var\s+\w+\s*=\s*0\s*;\s*\w+\s*<\s*(\w+)\s*;", RegexOptions.Compiled);
		private static readonly Regex FloatConst = new(@"const\s+float\s+(\w+)\s*=\s*(-?[0-9.]+)f?", RegexOptions.Compiled);
		private static readonly Regex NumericConst = new(@"const\s+(?:int|float)\s+(\w+)\s*=\s*(-?[0-9.]+)f?", RegexOptions.Compiled);
		private static readonly Regex NumericLocal = new(@"var\s+(\w+)\s*=\s*(-?[0-9.]+)f?\s*;", RegexOptions.Compiled);

		private static readonly Dictionary<string, HashSet<int>> _conditionalLines = [];
		private static readonly Dictionary<string, (string Bound, int Start, int End)[]> _bounceLoops = [];
		private static readonly Dictionary<string, (int Start, int End)[]> _widthSpans = [];
		private static readonly object _syncLock = new();

		/// <summary>
		/// Returns how many damage applications one cast makes against a single
		/// target.
		/// </summary>
		/// <remarks>
		/// The real count takes several shapes: an explicit MultiHit, a
		/// SkillHitInfo HitCount, a bounded loop around the damage call, or just
		/// repeated damage calls. The largest wins, because undercounting
		/// inflates the factor - the ceiling is the budget for the whole press
		/// divided by its hits.
		/// </remarks>
		/// <param name="skillName"></param>
		public static int HitsPerCast(string skillName)
		{
			var text = SfrSources.SkillHandler(skillName);
			if (text == null)
				return 1;

			var consts = NumericConsts(text);
			var spans = WidthSpans(text);
			var bounce = BounceLoops(text).Select(l => l.Bound).ToHashSet();
			var counts = new List<float>();

			foreach (Match m in Regex.Matches(text, @"MultiHit\(\s*(\w+)\s*\)"))
			{
				var value = ResolveCount(text, consts, m.Groups[1].Value);
				if (value > 0 && !UnderCondition(text, m.Index))
					counts.Add(value);
			}

			foreach (Match m in Regex.Matches(text, @"HitCount\s*=\s*(\w+)"))
			{
				var value = ResolveCount(text, consts, m.Groups[1].Value);
				if (value > 0 && !UnderCondition(text, m.Index))
					counts.Add(value);
			}

			// A bounded loop around the damage call repeats it that many times,
			// unless each pass resolves a new target - then it is reach.
			foreach (Match m in CountedForLoop.Matches(text))
			{
				if (bounce.Contains(m.Groups[1].Value))
					continue;

				var value = ResolveCount(text, consts, m.Groups[1].Value);
				if (value > 1)
					counts.Add(value);
			}

			// Only call sites that hit the press's own target count. A second
			// one inside a foreach over targets or a bounce region is the same
			// hit landing elsewhere.
			var calls = Regex.Matches(text, @"SCR_SkillHit\s*\(|SkillAttack\s*\(")
				.Count(m => !InSpans(m.Index, spans));

			if (calls > 1)
				counts.Add(calls);

			if (counts.Count > 0)
				return Math.Max(1, (int)Math.Round(counts.Max()));

			return Math.Max(1, calls);
		}

		/// <summary>
		/// Returns the splash fields the handler overrides, since the data does
		/// not bind them.
		/// </summary>
		/// <remarks>
		/// Skill.GetSplashParameters has two forms and 88 Laima handlers call
		/// the explicit one, so their splashRange and splashHeight in the .txt
		/// are dead. Handlers also bound targets themselves and only reach
		/// LimitBySDR if they call it or go through a helper that does.
		/// </remarks>
		/// <param name="skillName"></param>
		/// <param name="entry"></param>
		public static void ApplyHandlerGeometry(string skillName, SkillEntryData entry)
		{
			var text = SfrSources.SkillHandler(skillName);
			if (text == null)
				return;

			var consts = NumericConsts(text);

			// Geometry is as often a plain local as a const - a bounce radius is
			// `var splashRadius = 100`. Only read here; the hit count deliberately
			// leaves a local unresolved, since a conditional bonus is not the press.
			foreach (Match m in NumericLocal.Matches(text))
				consts.TryAdd(m.Groups[1].Value, SfrData.ParseFloat(m.Groups[2].Value));

			float? Resolve(string token)
			{
				if (token == null)
					return null;

				if (consts.TryGetValue(token, out var known))
					return known;

				return float.TryParse(token.TrimEnd('f'), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
			}

			var call = Regex.Match(text, @"GetSplashParameters\((?:[^();]|\([^()]*\))*?length:\s*([\w.]+)\s*,\s*width:\s*([\w.]+)(?:\s*,\s*angle:\s*([\w.]+))?");

			if (call.Success)
			{
				// The geometry rebuilds Length as splashHeight*2 and Width as splashRange*2.
				var length = Resolve(call.Groups[1].Value);
				var width = Resolve(call.Groups[2].Value);
				var angle = call.Groups[3].Success ? Resolve(call.Groups[3].Value) : null;

				if (length != null)
					entry.Fields["splashHeight"] = Str(length.Value / 2f);

				if (width != null)
					entry.Fields["splashRange"] = Str(width.Value / 2f);

				if (angle != null)
					entry.Fields["splashAngle"] = Str(angle.Value);

				entry.HandlerArea = true;
			}
			else if (ApplyDirectSplashArea(text, Resolve, entry))
			{
				entry.HandlerArea = true;
			}

			var shape = Regex.Match(text, @"GetSplashArea\(\s*SplashType\.(\w+)");
			if (shape.Success)
				entry.Fields["splashType"] = shape.Groups[1].Value;

			entry.UseSdr = text.Contains("LimitBySDR") || SdrHelpers.Any(h => text.Contains(h + "("));

			var caps = new List<float>();

			foreach (Match m in Regex.Matches(text, @"targetCount\s*=\s*([\w.]+)"))
			{
				var value = Resolve(m.Groups[1].Value);
				if (value > 0)
					caps.Add(value.Value);
			}

			foreach (Match m in Regex.Matches(text, @"\.Take\(\s*([\w.]+)\s*\)"))
			{
				var value = Resolve(m.Groups[1].Value);
				if (value > 0)
					caps.Add(value.Value);
			}

			// A bounce loop reaches its trip count in extra targets, on top of the main one.
			foreach (var loop in BounceLoops(text))
			{
				var value = Resolve(loop.Bound);
				if (value > 0)
					caps.Add(value.Value + 1);
			}

			if (caps.Count > 0)
				entry.TargetCap = (int)caps.Min();
		}

		/// <summary>
		/// Applies a splash area the handler constructs itself rather than
		/// asking the skill for it.
		/// </summary>
		/// <remarks>
		/// Every Force skill's splash fields are zero, so a bounce fan and a
		/// splash circle both read as single-target until the constructed shape
		/// is taken as the real reach. The float-typed shapes count too:
		/// matching only the integer names left 32 CircleF areas invisible, so
		/// Elementalist_Meteor priced against the 50x20 box in its .txt rather
		/// than the 140-radius circle its handler actually builds.
		/// </remarks>
		/// <param name="text"></param>
		/// <param name="resolve"></param>
		/// <param name="entry"></param>
		private static bool ApplyDirectSplashArea(string text, Func<string, float?> resolve, SkillEntryData entry)
		{
			var fan = Regex.Match(text, @"new\s+FanF?\(\s*[^,()]+,\s*[^,()]+,\s*([\w.]+)\s*,\s*([\w.]+)\s*\)");
			if (fan.Success)
			{
				var height = resolve(fan.Groups[1].Value);
				var angle = resolve(fan.Groups[2].Value);

				if (height > 0 && angle > 0)
				{
					// A Fan is bounded by height and angle alone; width only has
					// to clear the zero-guard, so it carries the height.
					entry.Fields["splashType"] = "Fan";
					entry.Fields["splashHeight"] = Str(height.Value / 2f);
					entry.Fields["splashRange"] = Str(height.Value / 2f);
					entry.Fields["splashAngle"] = Str(angle.Value);

					return true;
				}
			}

			var circle = Regex.Match(text, @"new\s+CircleF?\(\s*[^,()]+,\s*([\w.]+)\s*\)");
			if (circle.Success)
			{
				var radius = resolve(circle.Groups[1].Value);

				if (radius > 0)
				{
					entry.Fields["splashType"] = "Circle";
					entry.Fields["splashRange"] = Str(radius.Value / 2f);

					return true;
				}
			}

			var square = Regex.Match(text, @"new\s+SquareF?\(\s*[^,()]+,\s*[^,()]+,\s*([\w.]+)\s*,\s*([\w.]+)\s*\)");
			if (square.Success)
			{
				var height = resolve(square.Groups[1].Value);
				var width = resolve(square.Groups[2].Value);

				if (height > 0 && width > 0)
				{
					entry.Fields["splashType"] = "Square";
					entry.Fields["splashHeight"] = Str(height.Value / 2f);
					entry.Fields["splashRange"] = Str(width.Value / 2f);

					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns whether this skill's basicCast is a channel hold rather than
		/// a wind-up.
		/// </summary>
		/// <remarks>
		/// A cast-time skill commits its cost in Handle, after the cast
		/// resolves; a channel commits at the start because it is already
		/// running. enableCastMove is deliberately not read here - a skill that
		/// casts while moving is a wind-up that does not root you.
		/// </remarks>
		/// <param name="skillName"></param>
		/// <param name="entry"></param>
		public static bool IsChannel(string skillName, SkillEntryData entry)
		{
			if (entry.Num("shootTime") >= SfrDials.ChannelShootMs)
				return true;

			var text = SfrSources.SkillHandler(skillName);
			if (text == null)
				return false;

			var start = Regex.Match(text, @"void StartDynamicCast\b[^{]*\{");
			if (!start.Success)
				return false;

			var span = SfrSources.BracedBlock(text, start.Index);
			if (span == null)
				return false;

			var body = text[span.Value.Start..span.Value.End];

			return Regex.IsMatch(body, @"TrySpendSp|IncreaseOverheat|SkillCreatePad|CreatePad");
		}

		/// <summary>
		/// Returns how many times its own direct hit a skill's DoT deals over
		/// its full run.
		/// </summary>
		/// <remarks>
		/// The bleed family computes per-tick damage as a multiple of the hit
		/// that applied it, so the total is derivable from the handler's own
		/// constants and the buff's updateTime.
		/// </remarks>
		/// <param name="skillName"></param>
		/// <param name="level"></param>
		public static (float Multiple, string Buff, int Ticks) DotMultiple(string skillName, int level)
		{
			var text = SfrSources.SkillHandler(skillName);
			if (text == null)
				return (0f, null, 0);

			var consts = new Dictionary<string, float>();

			foreach (Match m in FloatConst.Matches(text))
				consts.TryAdd(m.Groups[1].Value, SfrData.ParseFloat(m.Groups[2].Value));

			if (consts.Count == 0)
				return (0f, null, 0);

			float? Find(string pattern)
			{
				foreach (var pair in consts)
				{
					if (Regex.IsMatch(pair.Key, pattern))
						return pair.Value;
				}

				return null;
			}

			var baseDamage = Find(@"Base.*Damage$");
			var perLevel = Find(@"DamagePerLevel$");

			if (baseDamage == null && perLevel == null)
				return (0f, null, 0);

			var duration = Find(@"^\w*Base\w*Duration$") ?? Find(@"Duration$");
			if (duration == null)
				return (0f, null, 0);

			var perLevelDuration = Find(@"DurationPerLevel$") ?? 0f;
			var seconds = (duration.Value + perLevelDuration * level) / 1000f;

			var buff = BuffIdRef.Matches(text)
				.Select(m => m.Groups[1].Value)
				.FirstOrDefault(b => SfrData.BuffUpdateTime(b) > 0);

			if (buff == null)
				return (0f, null, 0);

			var tick = SfrData.BuffUpdateTime(buff);
			var ticks = (int)(seconds / tick);
			var perTick = (baseDamage ?? 0f) + (perLevel ?? 0f) * level;

			return (ticks * perTick, buff, ticks);
		}

		/// <summary>
		/// Returns the press-equivalent hits a skill's pads deliver, and what
		/// could not be read.
		/// </summary>
		/// <param name="skillName"></param>
		/// <param name="level"></param>
		public static (float Total, List<(string Pad, int Ticks, float PerTick)> Used, List<(string Pad, string Reason)> Unknown) PadHits(string skillName, int level)
		{
			var used = new List<(string, int, float)>();
			var unknown = new List<(string, string)>();
			var text = SfrSources.SkillHandler(skillName);

			if (text == null)
				return (0f, used, unknown);

			var total = 0f;

			foreach (var pad in PadNames(skillName))
			{
				if (!SfrSources.Pads.TryGetValue(pad, out var info))
					continue;

				var profile = PadProfile(info, text, level);

				if (profile.Reason != null)
				{
					unknown.Add((pad, profile.Reason));
					continue;
				}

				if (profile.Ticks <= 0)
					continue;

				total += profile.Ticks * profile.PerTick * SfrDials.PadUptime;
				used.Add((pad, profile.Ticks, profile.PerTick));
			}

			return (total, used, unknown);
		}

		/// <summary>
		/// Returns the ticks and per-tick multiple for one pad, resolved at
		/// this skill's level.
		/// </summary>
		/// <remarks>
		/// The lifetime is read from the pad handler first and the skill
		/// handler second, because a skill that hands its pad a duration sets it
		/// at the creation site. A damaging pad with no update interval is a
		/// burst: it lands once.
		/// </remarks>
		/// <param name="info"></param>
		/// <param name="skillText"></param>
		/// <param name="level"></param>
		public static (int Ticks, float PerTick, float Life, string Reason) PadProfile(PadInfo info, string skillText, int level)
		{
			if (info.DamagePerTick <= 0)
				return (0, 0f, 0f, null);

			var names = SfrSources.Literals(info.Text);
			var lifeMs = SfrSources.TimeSpanMs(info.Text, @"LifeTime\s*=", names, level);

			if (lifeMs == null && skillText != null)
				lifeMs = SfrSources.TimeSpanMs(skillText, @"LifeTime\s*=", SfrSources.Literals(skillText), level);

			if (lifeMs == null || lifeMs <= 0)
				return (0, 0f, 0f, "damages but its tick rate or lifetime could not be read");

			var interval = SfrSources.ResolveExpression(info.IntervalExpression, names, level);

			if (interval == null || interval <= 0)
				return (1, info.DamagePerTick, lifeMs.Value / 1000f, null);

			var ticks = Math.Max(1, (int)(lifeMs.Value / interval.Value));

			if (ticks > SfrDials.MaxPadTicks)
				return (0, 0f, 0f, $"would tick {ticks} times over {lifeMs.Value / 1000f:0}s - it outlives the press");

			return (ticks, info.DamagePerTick, lifeMs.Value / 1000f, null);
		}

		/// <summary>
		/// Returns the press-equivalent hits a skill delivers through a buff it
		/// applies.
		/// </summary>
		/// <remarks>
		/// Some skills hold no damage call at all - the hit is inside the
		/// debuff's WhileActive - and read as utility until the buff handler is
		/// followed.
		/// </remarks>
		/// <param name="skillName"></param>
		/// <param name="level"></param>
		public static (float Total, List<(string Buff, int Ticks, float PerTick)> Used, List<(string Buff, string Reason)> Unknown) BuffHits(string skillName, int level)
		{
			var used = new List<(string, int, float)>();
			var unknown = new List<(string, string)>();
			var text = SfrSources.SkillHandler(skillName);

			if (text == null)
				return (0f, used, unknown);

			var names = SfrSources.Literals(text);
			var total = 0f;

			foreach (Match m in Regex.Matches(text, @"StartBuff\(\s*BuffId\.(\w+)([^;]*);"))
			{
				var buff = m.Groups[1].Value;
				var args = m.Groups[2].Value;

				if (!SfrSources.BuffDamagesOnTick(buff))
					continue;

				if (used.Any(u => u.Item1 == buff))
					continue;

				var source = SfrSources.BuffHandler(buff);
				var ticks = 1;
				var tick = SfrData.BuffUpdateTime(buff);

				if (tick > 0 && source != null && Regex.IsMatch(source, @"WhileActive|OnUpdate"))
				{
					float? duration = null;

					foreach (var (unit, scale) in new[] { ("Milliseconds", 1f), ("Seconds", 1000f) })
					{
						var d = Regex.Match(args, @"TimeSpan\.From" + unit + @"\(\s*([^)]+)\)");
						if (!d.Success)
							continue;

						duration = SfrSources.ResolveExpression(d.Groups[1].Value, names, level);

						if (duration != null)
						{
							duration *= scale;
							break;
						}
					}

					if (duration > 0)
						ticks = Math.Max(1, (int)(duration.Value / 1000f / tick));
				}

				if (ticks > SfrDials.MaxBuffTicks)
				{
					unknown.Add((buff, $"damages for {ticks} ticks - it outlives the press"));
					continue;
				}

				total += ticks;
				used.Add((buff, ticks, 1f));
			}

			return (total, used, unknown);
		}

		/// <summary>
		/// Returns the pads a skill's handler creates.
		/// </summary>
		/// <param name="skillName"></param>
		public static string[] PadNames(string skillName)
		{
			var text = SfrSources.SkillHandler(skillName);
			if (text == null)
				return [];

			return PadNameRef.Matches(text).Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n).ToArray();
		}

		/// <summary>
		/// Returns how many targets the pad delivering this skill can hold at
		/// once, or null when nothing bounds it.
		/// </summary>
		/// <remarks>
		/// MaxUseCount is deliberately not read: it caps a pad's activations
		/// over its whole life rather than at one moment, which is hit count for
		/// a ticking pad, and it is as often a sentinel as a real bound.
		/// </remarks>
		/// <param name="skillName"></param>
		/// <param name="level"></param>
		public static int? PadTargetCap(string skillName, int level)
		{
			var caps = new List<int>();

			foreach (var pad in PadNames(skillName))
			{
				if (!SfrSources.Pads.TryGetValue(pad, out var info))
					continue;

				var names = SfrSources.Literals(info.Text);

				foreach (var expr in info.CapExpressions)
				{
					var value = SfrSources.ResolveExpression(expr, names, level);
					if (value >= 1)
						caps.Add((int)value.Value);
				}
			}

			return caps.Count > 0 ? caps.Min() : null;
		}

		/// <summary>
		/// Returns the rider multiplier and kinds for every skill whose handler
		/// applies a buff its damage number does not account for.
		/// </summary>
		/// <remarks>
		/// A skill that also grants block, applies a DoT or lands a stun is not
		/// paid for in damage alone, so its damage ceiling is reduced. The
		/// lowest matching multiplier wins.
		/// </remarks>
		public static Dictionary<string, (float Multiplier, string[] Kinds)> ScanRiders()
		{
			var found = new Dictionary<string, (float, string[])>();

			foreach (var pair in SfrSources.AllSkillHandlers)
			{
				var buffs = BuffIdRef.Matches(pair.Value).Select(m => m.Groups[1].Value).Distinct().ToArray();
				var kinds = new List<string>();

				foreach (var (kind, pattern) in SfrDials.RiderPatterns)
				{
					if (buffs.Any(b => Regex.IsMatch(b, pattern)))
						kinds.Add(kind);
				}

				if (kinds.Count == 0)
					continue;

				var mult = kinds.Min(k => SfrDials.RiderMultipliers[k]);

				if (!found.TryGetValue(pair.Key, out var existing) || mult < existing.Item1)
					found[pair.Key] = (mult, kinds.OrderBy(k => k).ToArray());
			}

			return found;
		}

		/// <summary>
		/// Returns what this skill's press delivers that the price could not
		/// account for.
		/// </summary>
		/// <remarks>
		/// A channel or a pad is fine once its ticks are read out of the pad
		/// handler - those are countable. Only what stays unreadable is a
		/// warning.
		/// </remarks>
		/// <param name="skillName"></param>
		public static List<string> DeliveryWarnings(string skillName)
		{
			var found = new List<string>();
			var text = SfrSources.SkillHandler(skillName);

			if (text == null)
				return ["no handler found - hits per cast assumed 1"];

			var level = SfrData.SkillMaxLevel(skillName);
			var pads = PadHits(skillName, level);
			var buffs = BuffHits(skillName, level);

			var direct = text.Contains("SCR_SkillHit") || text.Contains("MultiHit") || text.Contains("SkillAttack(");

			foreach (var (pad, reason) in pads.Unknown)
				found.Add($"pad '{pad}' {reason}");

			foreach (var (buff, reason) in buffs.Unknown)
				found.Add($"debuff '{buff}' {reason}");

			if (Regex.IsMatch(text, @"SpawnMonster|PadCreateMonster|SummonHelper"))
				found.Add("summons - a pet's damage is not modelled here");

			var entry = SfrData.Skills.TryGetValue(skillName, out var data) ? data : new SkillEntryData(skillName);
			var countable = pads.Used.Count > 0 || (direct && !HasUnboundedLoop(text));

			// IDynamicCasted covers cast-time skills as well as channels, so it
			// only means "channel" when the commitment is the hold itself.
			var channels = entry.Num("basicCast") <= 0 || entry.Num("shootTime") >= SfrDials.ChannelShootMs;

			if (channels && Regex.IsMatch(text, @"IDynamicCasted|StartDynamicCast|ChannelingBuff") && !countable)
				found.Add("channels, and no pad was found to read its ticks from");

			if (entry.Num("shootTime") >= SfrDials.ChannelShootMs && !countable)
				found.Add("shootTime is a channel hold, not an animation, and no pad bounds it");

			if (!direct && pads.Total <= 0 && buffs.Total <= 0)
				found.Add("no damage delivery found - this is likely utility, which gets no damage budget");

			if (IsMeshDelivery(text))
			{
				found.Add("fans out to targets twice without tracking who was hit - "
					+ "hits per target depend on the pull, not the handler");
			}

			return found;
		}

		/// <summary>
		/// Returns bounded for-loops that resolve a fresh target each pass
		/// instead of re-hitting one.
		/// </summary>
		/// <remarks>
		/// A bounce loop's trip count is reach, not hit count. The
		/// discriminator is the bounce idiom itself: a TryGet...Target call, or
		/// the loop feeding the set that tracks who has been hit.
		/// </remarks>
		/// <param name="text"></param>
		public static (string Bound, int Start, int End)[] BounceLoops(string text)
		{
			lock (_syncLock)
			{
				if (_bounceLoops.TryGetValue(text, out var cached))
					return cached;

				var found = new List<(string, int, int)>();

				foreach (Match m in CountedForLoop.Matches(text))
				{
					var span = SfrSources.BracedBlock(text, m.Index + m.Length);
					if (span == null)
						continue;

					var body = text[span.Value.Start..span.Value.End];

					if (Regex.IsMatch(body, @"TryGet\w*Target\s*\(|hitTargets\s*\.\s*Add\s*\("))
						found.Add((m.Groups[1].Value, span.Value.Start, span.Value.End));
				}

				return _bounceLoops[text] = found.ToArray();
			}
		}

		/// <summary>
		/// Returns regions whose damage calls reach another target rather than
		/// hitting again.
		/// </summary>
		/// <remarks>
		/// Only a fan-out onto entities an earlier call site skipped qualifies.
		/// A plain second pass over the same resolved list is the ordinary
		/// two-hit idiom and both passes land on every target in it.
		/// </remarks>
		/// <param name="text"></param>
		public static (int Start, int End)[] WidthSpans(string text)
		{
			lock (_syncLock)
			{
				if (_widthSpans.TryGetValue(text, out var cached))
					return cached;

				var spans = BounceLoops(text).Select(l => (l.Start, l.End)).ToList();
				var disjoint = DisjointCollections(text);

				foreach (Match m in Regex.Matches(text, @"foreach\s*\(\s*var\s+[\w(), ]+\s+in\s+(\w+)"))
				{
					if (!disjoint.Contains(m.Groups[1].Value))
						continue;

					var span = SfrSources.BracedBlock(text, m.Index + m.Length);
					if (span != null)
						spans.Add((span.Value.Start, span.Value.End));
				}

				// The bounce loop's damage call is often one level down in a
				// helper, so the methods it invokes join the region.
				foreach (var (start, end) in spans.ToArray())
				{
					var names = Regex.Matches(text[start..end], @"(?:this\.)?(\w+)\s*\(")
						.Select(m => m.Groups[1].Value)
						.Distinct();

					foreach (var name in names)
					{
						var body = SfrSources.MethodBody(text, name);
						if (body != null)
							spans.Add((body.Value.Start, body.Value.End));
					}
				}

				return _widthSpans[text] = spans.ToArray();
			}
		}

		/// <summary>
		/// Returns target lists built to hold entities some earlier call site
		/// did not hit.
		/// </summary>
		/// <param name="text"></param>
		private static HashSet<string> DisjointCollections(string text)
		{
			var found = new HashSet<string>();

			foreach (Match m in Regex.Matches(text, @"var\s+(\w+)\s*=\s*([^;]*);", RegexOptions.Singleline))
			{
				if (Regex.IsMatch(m.Groups[2].Value, @"!=\s*\w*[Tt]arget|\.Contains\s*\("))
					found.Add(m.Groups[1].Value);
			}

			return found;
		}

		/// <summary>
		/// Returns whether a fan-out can reach the same target more than once,
		/// by a count the handler does not state.
		/// </summary>
		/// <remarks>
		/// A splash centred on another target is rebuilt per target, so the
		/// areas overlap and their damage lands twice on the middle of a pile.
		/// That count is a property of the pull, so it is neither hits nor
		/// width. Tracking who has been hit makes it disjoint again.
		/// </remarks>
		/// <param name="text"></param>
		public static bool IsMeshDelivery(string text)
		{
			if (Regex.IsMatch(text, @"(?:HashSet|List)\s*<\s*ICombatEntity\s*>") && text.Contains(".Contains("))
				return false;

			var loops = new List<(int Start, int End)>();

			foreach (Match m in Regex.Matches(text, @"\bfor(?:each)?\s*\("))
			{
				var span = SfrSources.BracedBlock(text, m.Index + m.Length);
				if (span != null)
					loops.Add((span.Value.Start, span.Value.End));
			}

			foreach (Match m in Regex.Matches(text, @"(?:new\s+\w+|\w+\.Centered)\(\s*\w*[Tt]arget\w*\.Position"))
			{
				if (loops.Any(l => l.Start <= m.Index && m.Index <= l.End))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns whether a damage call sits inside a loop whose trip count
		/// the model cannot read.
		/// </summary>
		/// <remarks>
		/// This is what separates a countable channel - a fixed sequence of
		/// awaited hits - from one that keeps hitting for as long as it is held.
		/// </remarks>
		/// <param name="text"></param>
		public static bool HasUnboundedLoop(string text)
		{
			foreach (Match m in Regex.Matches(text, @"while\s*\(([^)]*)\)"))
			{
				if (!Regex.IsMatch(m.Groups[1].Value, @"<\s*[0-9]+"))
					return true;
			}

			// The damage call can sit in a helper the loop calls, so the loop
			// body itself is not evidence either way. The loop is enough.
			return Regex.IsMatch(text, @"for\s*\(\s*;\s*;");
		}

		/// <summary>
		/// Returns a hit count the handler gets from a helper method rather
		/// than a constant.
		/// </summary>
		/// <remarks>
		/// A count that branches on target size resolves to nothing as a token,
		/// so the M branch is taken - matching the size the geometry already
		/// assumes. Only a method call is followed: a plain local stays
		/// unresolved, since a conditional bonus is not the baseline press.
		/// </remarks>
		/// <param name="text"></param>
		/// <param name="token"></param>
		private static float? MethodHits(string text, string token)
		{
			var call = Regex.Match(text, @"var\s+" + Regex.Escape(token) + @"\s*=\s*(\w+)\s*\(");
			if (!call.Success)
				return null;

			var body = Regex.Match(text, @"\b" + Regex.Escape(call.Groups[1].Value) + @"\s*\([^)]*\)\s*\{");
			if (!body.Success)
				return null;

			var span = SfrSources.BracedBlock(text, body.Index);
			if (span == null)
				return null;

			var source = text[span.Value.Start..span.Value.End];

			var sized = Regex.Match(source, @"SizeType\.M\b[^;{]*[\){]\s*return\s+([0-9.]+)");
			if (sized.Success)
				return SfrData.ParseFloat(sized.Groups[1].Value);

			var returns = Regex.Matches(source, @"return\s+([0-9.]+)\s*;")
				.Select(m => SfrData.ParseFloat(m.Groups[1].Value))
				.ToArray();

			return returns.Length > 0 ? returns.Max() : null;
		}

		/// <summary>
		/// Returns whether the match at the given offset sits on a line guarded
		/// by an if.
		/// </summary>
		/// <remarks>
		/// A multi-hit the handler only reaches when a debuff is up is a bonus,
		/// not the baseline press, and counting it as baseline divides the whole
		/// budget by it. Indentation is the discriminator, which the tab rule
		/// makes reliable.
		/// </remarks>
		/// <param name="text"></param>
		/// <param name="position"></param>
		public static bool UnderCondition(string text, int position)
		{
			var lineStart = text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;

			return ConditionalLines(text).Contains(lineStart);
		}

		/// <summary>
		/// Returns the offsets of every line that sits under an if.
		/// </summary>
		/// <param name="text"></param>
		private static HashSet<int> ConditionalLines(string text)
		{
			lock (_syncLock)
			{
				if (_conditionalLines.TryGetValue(text, out var cached))
					return cached;

				var lines = text.Split('\n');
				var starts = new int[lines.Length];
				var indents = new int[lines.Length];
				var at = 0;

				for (var i = 0; i < lines.Length; ++i)
				{
					starts[i] = at;
					at += lines[i].Length + 1;
					indents[i] = lines[i].Length - lines[i].TrimStart('\t').Length;
				}

				var found = new HashSet<int>();

				for (var i = 0; i < lines.Length; ++i)
				{
					if (lines[i].Trim().Length == 0)
						continue;

					for (var j = i - 1; j >= 0; --j)
					{
						var above = lines[j].Trim();

						if (above.Length == 0 || above == "{" || above == "}")
							continue;

						if (indents[j] >= indents[i])
							break;

						if (Regex.IsMatch(above, @"^(else\b|if\s*\()"))
							found.Add(starts[i]);

						break;
					}
				}

				return _conditionalLines[text] = found;
			}
		}

		/// <summary>
		/// Returns the numeric consts a handler declares.
		/// </summary>
		/// <param name="text"></param>
		private static Dictionary<string, float> NumericConsts(string text)
		{
			var found = new Dictionary<string, float>();

			foreach (Match m in NumericConst.Matches(text))
				found.TryAdd(m.Groups[1].Value, SfrData.ParseFloat(m.Groups[2].Value));

			return found;
		}

		/// <summary>
		/// Resolves a hit-count token against the handler's consts, falling
		/// back to a helper method.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="consts"></param>
		/// <param name="token"></param>
		private static float ResolveCount(string text, Dictionary<string, float> consts, string token)
		{
			if (consts.TryGetValue(token, out var known))
				return known;

			if (float.TryParse(token, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out var literal))
			{
				return literal;
			}

			return MethodHits(text, token) ?? 0f;
		}

		/// <summary>
		/// Returns whether a position falls inside any of the given spans.
		/// </summary>
		/// <param name="position"></param>
		/// <param name="spans"></param>
		private static bool InSpans(int position, (int Start, int End)[] spans)
			=> spans.Any(s => s.Start <= position && position <= s.End);

		/// <summary>
		/// Formats a number the way the data files carry it.
		/// </summary>
		/// <param name="value"></param>
		private static string Str(float value)
			=> value.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}
}

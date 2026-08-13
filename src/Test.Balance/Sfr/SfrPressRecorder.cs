using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Melia.Zone.Scripting;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// One application of damage the pipeline made during a press.
	/// </summary>
	/// <param name="Target"></param>
	/// <param name="Damage"></param>
	/// <param name="HitCount"></param>
	public readonly record struct PressHit(ICombatEntity Target, float Damage, int HitCount);

	/// <summary>
	/// Watches the real damage pipeline and records every application one
	/// press makes, per target.
	/// </summary>
	/// <remarks>
	/// The hook is SCR_Combat_AfterCalc, which SCR_CalculateDamage invokes once
	/// per damage application, after the hit-count multiplier has been folded
	/// in. That is the one point every source of skill damage passes through -
	/// direct hits, splash loops, pad ticks and debuff ticks alike - and it is
	/// reached through the public scriptable-function registry, so nothing in
	/// the server has to be modified to observe it.
	///
	/// Dodge, block and crit are pinned off for the duration. All three are
	/// rolled inside the same function, and a dodge returns before the hook
	/// runs at all, so leaving them live would make a hit count a coin flip.
	/// The pricer carries its own crit allowance for the same reason.
	///
	/// The hook itself is installed once, process-wide, and reference counted
	/// rather than swapped per instance. Dispatch reads an AsyncLocal pointer
	/// to "whichever recorder this logical press belongs to", which is what
	/// lets several presses run at once on their own arenas without one
	/// press's swap stealing another's hook mid-measurement.
	///
	/// AsyncLocal rather than [ThreadStatic]: a multi-hit handler paces itself
	/// with `await skill.Wait(...)`, and its continuation resumes on whichever
	/// pool thread happens to be free, not the thread that started the press.
	/// A thread-static pointer sees only what a press does before its first
	/// await; everything after silently goes unrecorded, since the mob still
	/// takes the damage, but on a thread whose slot was never set. AsyncLocal
	/// is captured into the async state machine at each await and restored on
	/// the resuming thread, so it survives exactly the hop that breaks a
	/// thread-static one.
	/// </remarks>
	public sealed class SfrPressRecorder : IDisposable
	{
		private const string AfterCalc = "SCR_Combat_AfterCalc";
		private const string DodgeChance = "SCR_GetDodgeChance";
		private const string BlockChance = "SCR_GetBlockChance";
		private const string CritChance = "SCR_GetCritChance";

		private static readonly object _installLock = new();
		private static int _refCount;
		private static bool _pinnedRolls;
		private static (string Name, CombatCalcFunction Func)[] _replaced;

		private static readonly AsyncLocal<SfrPressRecorder> _current = new();

		private readonly object _syncLock = new();
		private readonly List<PressHit> _hits = [];
		private readonly ICombatEntity _caster;
		private readonly ICombatEntity[] _allies;

		/// <summary>
		/// Installs the hook on first use and marks this thread as the one
		/// whose presses it should record.
		/// </summary>
		/// <param name="caster"></param>
		/// <param name="pinRolls">
		/// Whether dodge, block and crit are held at zero. True for the damage
		/// pass, where a rolled crit would make a hit count a coin flip. False
		/// for the buff pass, whose whole subject is buffs that move those
		/// rolls - Finestra and High Guard measure nothing at all with them
		/// pinned, since crit rate and block are all they do.
		/// </param>
		/// <param name="allies">
		/// Party members whose damage counts as the caster's own, for a buff
		/// measured across a party. Empty for every other measurement.
		/// </param>
		public SfrPressRecorder(ICombatEntity caster, bool pinRolls = true, ICombatEntity[] allies = null)
		{
			_caster = caster;
			_allies = allies ?? [];

			if (_current.Value != null)
				throw new InvalidOperationException("A recorder is already active on this logical call context.");

			lock (_installLock)
			{
				if (_refCount == 0)
				{
					var afterCalc = ScriptableFunctions.Combat.Get(AfterCalc);

					_replaced =
					[
						(AfterCalc, afterCalc),
						(DodgeChance, ScriptableFunctions.Combat.Get(DodgeChance)),
						(BlockChance, ScriptableFunctions.Combat.Get(BlockChance)),
						(CritChance, ScriptableFunctions.Combat.Get(CritChance)),
					];

					Replace(AfterCalc, (attacker, target, skill, modifier, result) =>
					{
						var value = afterCalc(attacker, target, skill, modifier, result);
						_current.Value?.Record(attacker, target, result);

						return value;
					});

					if (pinRolls)
					{
						Replace(DodgeChance, (_, _, _, _, _) => 0f);
						Replace(BlockChance, (_, _, _, _, _) => 0f);
						Replace(CritChance, (_, _, _, _, _) => 0f);
					}

					_pinnedRolls = pinRolls;
				}
				else if (_pinnedRolls != pinRolls)
				{
					// The hook is process-wide, so the first recorder's choice
					// is what every concurrent one gets. Silently handing back
					// the wrong one is how a buff pass would read a pinned crit
					// rate as no effect.
					throw new InvalidOperationException($"A recorder with pinRolls:{_pinnedRolls} is already installed.");
				}

				_refCount++;
			}

			_current.Value = this;
		}

		/// <summary>
		/// Every damage application recorded so far.
		/// </summary>
		public IReadOnlyList<PressHit> Hits
		{
			get
			{
				lock (_syncLock)
					return _hits.ToArray();
			}
		}

		/// <summary>
		/// Drops everything recorded so far, so a warm-up press does not count
		/// towards the measured one.
		/// </summary>
		public void Clear()
		{
			lock (_syncLock)
				_hits.Clear();
		}

		/// <summary>
		/// Returns the distinct entities that took damage above zero.
		/// </summary>
		public ICombatEntity[] Damaged()
			=> this.Hits.Where(h => h.Damage > 0 && h.Target != _caster).Select(h => h.Target).Distinct().ToArray();

		/// <summary>
		/// Returns the damage the measured caster itself took, which is the
		/// signal a defensive or crowd-control press is priced against.
		/// </summary>
		public float DamageTakenByCaster()
			=> this.Hits.Where(h => h.Target == _caster).Sum(h => h.Damage);

		/// <summary>
		/// Returns how many damage applications landed on one entity, counting
		/// a multi-hit application as the hits it displays.
		/// </summary>
		/// <param name="target"></param>
		public int HitsOn(ICombatEntity target)
			=> this.Hits.Where(h => h.Target == target && h.Damage > 0).Sum(h => Math.Max(1, h.HitCount));

		/// <summary>
		/// Returns the damage one entity took across the whole press.
		/// </summary>
		/// <param name="target"></param>
		public float DamageOn(ICombatEntity target)
			=> this.Hits.Where(h => h.Target == target).Sum(h => h.Damage);

		/// <summary>
		/// Returns the damage every enemy took across the whole press,
		/// excluding whatever the caster itself took.
		/// </summary>
		public float TotalDamage()
			=> this.Hits.Where(h => h.Target != _caster && Array.IndexOf(_allies, h.Target) < 0).Sum(h => h.Damage);

		/// <summary>
		/// Unmarks this call context and, once every recorder has been
		/// disposed, puts the registry back the way it was found.
		/// </summary>
		public void Dispose()
		{
			_current.Value = null;

			lock (_installLock)
			{
				if (--_refCount > 0)
					return;

				// Removed before re-registering: registering over a live entry
				// records an override, which would leave the package chain
				// pointing at this hook rather than at what it replaced.
				foreach (var (name, func) in _replaced)
				{
					ScriptableFunctions.Combat.Remove(name);
					ScriptableFunctions.Combat.Register(name, func);
				}

				_replaced = null;
			}
		}

		private static void Replace(string name, CombatCalcFunction func)
		{
			ScriptableFunctions.Combat.Remove(name);
			ScriptableFunctions.Combat.Register(name, func);
		}

		/// <summary>
		/// Records one application the measured caster either dealt or took,
		/// ignoring everything else on the map - mob infighting does not
		/// happen, so nothing else is this press's business.
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="target"></param>
		/// <param name="result"></param>
		private void Record(ICombatEntity attacker, ICombatEntity target, SkillHitResult result)
		{
			if (target == null || (!IsCasters(attacker) && target != _caster))
				return;

			lock (_syncLock)
				_hits.Add(new PressHit(target, result.Damage, result.HitCount));
		}

		/// <summary>
		/// Returns whether an attack is the caster's own doing, directly or
		/// through something it summoned.
		/// </summary>
		/// <remarks>
		/// A statue, familiar or raised skeleton attacks under its own handle,
		/// so a plain identity check dropped everything it did and the skill
		/// that summoned it read as damaging nothing at all -
		/// Dievdirbys_CarveOwl and the Sorcerer and Necromancer summons were
		/// all held unpriced. MonsterSkillCreateMob and the summon handlers
		/// both stamp OwnerHandle with the caster's handle, so the damage is
		/// attributable; the caster's own pads already attack as the caster
		/// and are unaffected.
		/// </remarks>
		/// <param name="attacker"></param>
		private bool IsCasters(ICombatEntity attacker)
		{
			if (attacker == _caster || Array.IndexOf(_allies, attacker) >= 0)
				return true;

			return attacker is ISubActor sub && sub.OwnerHandle != 0 && sub.OwnerHandle == _caster.Handle;
		}
	}

	/// <summary>
	/// Holds a skill's factor at a chosen value for the length of a
	/// measurement.
	/// </summary>
	/// <remarks>
	/// Skill.Data is the SkillDb entry itself, shared by every Skill instance
	/// in the process, so the override is saved and restored under a lock keyed
	/// on that same Data object. Two presses of the *same* skill never overlap
	/// in the roster run, so this only ever contends with itself; it is keyed
	/// per skill rather than process-wide so pricing different skills on
	/// different threads is not serialized through one lock for the whole 10 s
	/// a press can run.
	/// </remarks>
	public sealed class SfrFactorScope : IDisposable
	{
		private readonly Skill _skill;
		private readonly float _factor;
		private readonly float _factorByLevel;

		/// <summary>
		/// Overrides the skill's factor until disposed.
		/// </summary>
		/// <remarks>
		/// Takes no lock. SyntheticActors.GiveSkill gives every measured skill
		/// a private copy of its SkillData, so this writes to state no other
		/// measurement can see. It used to hold a monitor on the shared
		/// SkillData for the whole press, which serialized every window of the
		/// same skill against every other - 32 concurrent presses measured a
		/// speedup of 1.1x out of a possible 32x.
		/// </remarks>
		/// <param name="skill"></param>
		/// <param name="factor"></param>
		/// <param name="factorByLevel"></param>
		public SfrFactorScope(Skill skill, float factor, float factorByLevel = 0f)
		{
			_skill = skill;

			_factor = skill.Data.Factor;
			_factorByLevel = skill.Data.FactorByLevel;

			skill.Data.Factor = factor;
			skill.Data.FactorByLevel = factorByLevel;
			skill.Properties.InvalidateAll();
		}

		/// <summary>
		/// Restores the skill's own factor.
		/// </summary>
		public void Dispose()
		{
			_skill.Data.Factor = _factor;
			_skill.Data.FactorByLevel = _factorByLevel;
			_skill.Properties.InvalidateAll();
		}
	}

	/// <summary>
	/// Holds a skill's SP cost at a chosen value for the length of a
	/// measurement.
	/// </summary>
	/// <remarks>
	/// The same private-copy arrangement SfrFactorScope relies on: every
	/// measured skill gets its own SkillData through SyntheticActors.GiveSkill,
	/// so this writes state no other window can see and takes no lock. The
	/// per-level term is zeroed alongside it, so what a press charges is the
	/// pinned value itself whatever level the skill is measured at.
	/// </remarks>
	public sealed class SfrSpScope : IDisposable
	{
		private readonly Skill _skill;
		private readonly float _basicSp;
		private readonly float _lvUpSpendSp;

		/// <summary>
		/// Overrides the skill's SP cost until disposed.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="basicSp"></param>
		public SfrSpScope(Skill skill, float basicSp)
		{
			_skill = skill;

			_basicSp = skill.Data.BasicSp;
			_lvUpSpendSp = skill.Data.LvUpSpendSp;

			skill.Data.BasicSp = basicSp;
			skill.Data.LvUpSpendSp = 0;
			skill.Properties.InvalidateAll();
		}

		/// <summary>
		/// Restores the skill's own SP cost.
		/// </summary>
		public void Dispose()
		{
			_skill.Data.BasicSp = _basicSp;
			_skill.Data.LvUpSpendSp = _lvUpSpendSp;
			_skill.Properties.InvalidateAll();
		}
	}
}

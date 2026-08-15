using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Properties;
using Melia.Zone.Buffs;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Newtonsoft.Json.Linq;
using Yggdrasil.Logging;

namespace Melia.Zone.Buffs.Base
{
	/// <summary>
	/// Base class for buff handlers.
	/// </summary>
	public abstract class BuffHandler : IBuffHandler
	{
		/// <summary>
		/// Prefix used for storing property modifiers in buff Vars.
		/// </summary>
		public const string ModifierVarPrefix = "Melia.Modifier.";

		/// <summary>
		/// Initializes buff handler.
		/// </summary>
		public BuffHandler()
		{
			ScriptableFunctions.Load(this);
		}

		/// <summary>
		/// Callback for when the buff is activated, either by starting or
		/// overbuffing it. Not called once the max overbuff count is reached.
		/// </summary>
		/// <remarks>
		/// This callback is usually the right choice for most buffs that
		/// apply a simple bonus that stacks up until the max overbuff count
		/// is reached.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="activationType"></param>
		public virtual void OnActivate(Buff buff, ActivationType activationType)
		{
		}

		/// <summary>
		/// Callback for when the buff's duration is extended, regardless of
		/// whether the overbuff max count was reached or not.
		/// </summary>
		/// <remarks>
		/// This callback presents an alternative to OnActivate, in case it's
		/// ever necessary for the handler to react to continued extensions
		/// after the max overbuff count was reached.
		/// 
		/// OnExtend is called in addition to OnActivate up until the max
		/// overbuff count is reached. Afterwards, OnExtend is the only
		/// callback that is called.
		/// </remarks>
		/// <param name="buff"></param>
		public virtual void OnExtend(Buff buff)
		{
		}

		/// <summary>
		/// Callback for regular updates while the buff is active. Only called
		/// for buffs that have an update time.
		/// </summary>
		/// <param name="buff"></param>
		public virtual void WhileActive(Buff buff)
		{
		}

		/// <summary>
		/// Callback for when the buff runs out or is manually stopped.
		/// </summary>
		/// <param name="buff"></param>
		public virtual void OnEnd(Buff buff)
		{
		}

		/// <summary>
		/// Returns one of the caption ratios of the skill that granted the
		/// buff, resolved against the buff's level and the caster's reinforce
		/// ability.
		/// </summary>
		/// <remarks>
		/// This is the magnitude the skill's description displays, so a handler
		/// reading it can never disagree with the tooltip. The unit is whatever
		/// the description declares - a handler feeding a rate property divides
		/// by 100 itself.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="slot"></param>
		/// <returns></returns>
		protected static float GetCaptionRatio(Buff buff, int slot)
		{
			if (!ZoneServer.Instance.Data.SkillDb.TryFind(buff.SkillId, out var skillData))
			{
				Log.Warning($"GetCaptionRatio: Buff '{buff.Id}' has no skill data for SkillId '{buff.SkillId}', did the caller forget to pass skillId to StartBuff?");
				return 0;
			}

			var funcName = slot switch
			{
				1 => "SCR_Get_CaptionRatio",
				2 => "SCR_Get_CaptionRatio2",
				3 => "SCR_Get_CaptionRatio3",
				_ => throw new ArgumentOutOfRangeException(nameof(slot), $"No caption ratio {slot}."),
			};

			// A skill whose caption depends on more than level and reinforce
			// ability - a live stat, an ability check - declares an override
			// under this name, which SkillProperties.CalculateProperty also
			// looks for. Reading it here too means a handler can never end
			// up with a different number than the tooltip.
			if (ScriptableFunctions.Skill.TryGet(funcName + "_" + skillData.ClassName, out var overrideFunc)
				&& buff.Caster is ICombatEntity casterEntity && casterEntity.TryGetSkill(buff.SkillId, out var skill))
			{
				return overrideFunc(skill);
			}

			var (baseValue, byLevel, maxValue) = slot switch
			{
				1 => (skillData.CaptionRatio1, skillData.CaptionRatio1ByLevel, skillData.CaptionRatio1Max),
				2 => (skillData.CaptionRatio2, skillData.CaptionRatio2ByLevel, skillData.CaptionRatio2Max),
				3 => (skillData.CaptionRatio3, skillData.CaptionRatio3ByLevel, skillData.CaptionRatio3Max),
				_ => throw new ArgumentOutOfRangeException(nameof(slot), $"No caption ratio {slot}."),
			};

			var value = baseValue + (byLevel * buff.NumArg1);
			value += value * GetReinforceRate(buff);

			if (maxValue != 0)
				value = Math.Min(maxValue, value);

			return value;
		}

		/// <summary>
		/// Applies one caption ratio to a property as both a flat bonus and a
		/// rate, so a stat a buff raises is raised on both of its axes.
		/// </summary>
		/// <remarks>
		/// One slot per stat, and the flat half is the same number the rate
		/// half is a percentage of: a ratio of 30 grants +30% and +30 flat. A
		/// percentage alone is worth nothing to a character whose base value in
		/// that stat is small, which is every character at the level a buff is
		/// first taken, and a flat bonus alone stops mattering at the level cap.
		/// Pairing them is also what keeps a stat's whole magnitude inside one
		/// slot, where the pricer's scalar can move all of it - the client has
		/// three slots and no more.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="flatProperty"></param>
		/// <param name="rateProperty"></param>
		/// <param name="ratio"></param>
		protected static void AddPairedPropertyModifier(Buff buff, ICombatEntity target, string flatProperty, string rateProperty, float ratio)
		{
			AddPropertyModifier(buff, target, flatProperty, ratio);
			AddPropertyModifier(buff, target, rateProperty, ratio / 100f);
		}

		/// <summary>
		/// Removes both halves of a paired property modifier.
		/// </summary>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="flatProperty"></param>
		/// <param name="rateProperty"></param>
		protected static void RemovePairedPropertyModifier(Buff buff, ICombatEntity target, string flatProperty, string rateProperty)
		{
			RemovePropertyModifier(buff, target, flatProperty);
			RemovePropertyModifier(buff, target, rateProperty);
		}

		/// <summary>
		/// Returns how long the buff of the skill that granted this buff is
		/// meant to last.
		/// </summary>
		/// <param name="buff"></param>
		/// <returns></returns>
		protected static TimeSpan GetCaptionTime(Buff buff)
		{
			if (!ZoneServer.Instance.Data.SkillDb.TryFind(buff.SkillId, out var skillData))
			{
				Log.Warning($"GetCaptionTime: Buff '{buff.Id}' has no skill data for SkillId '{buff.SkillId}', did the caller forget to pass skillId to StartBuff?");
				return TimeSpan.Zero;
			}

			return TimeSpan.FromSeconds(skillData.CaptionTime + (skillData.CaptionTimeByLevel * buff.NumArg1));
		}

		/// <summary>
		/// Returns the caster's reinforce ability rate for the skill that
		/// granted the buff, or zero if they no longer have it.
		/// </summary>
		/// <param name="buff"></param>
		/// <returns></returns>
		private static float GetReinforceRate(Buff buff)
		{
			if (buff.Caster is not ICombatEntity caster || !caster.TryGetSkill(buff.SkillId, out var skill))
				return 0;

			return ScriptableFunctions.Skill.Get("SCR_Get_AbilityReinforceRate")(skill);
		}

		/// <summary>
		/// Returns the name of the variable used to store modifiers for
		/// the given property.
		/// </summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		private static string GetModifierVarName(string propertyName)
			=> ModifierVarPrefix + propertyName;

		/// <summary>
		/// Returns true if the property is a transient buff modifier that should
		/// not be persisted to the database. This prevents desync issues where
		/// buff modifiers could persist even after the buff expires.
		/// </summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public static bool IsBuffTransientProperty(string propertyName)
			=> propertyName.EndsWith("_BM");

		/// <summary>
		/// Modifies the property on the target and saves the value in the buff,
		/// to be able to later undo the change.
		/// </summary>
		/// <remarks>
		/// Repeated calls to this method will stack the modifications, while
		/// one call to RemovePropertyModifier will undo all of them.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="modifierName"></param>
		/// <param name="value"></param>
		protected static void AddModifier(Buff buff, ICombatEntity target, string modifierName, float value)
		{
			var varName = GetModifierVarName(modifierName);

			if (buff.Vars.TryGetFloat(varName, out var oldValue))
				value += oldValue;

			buff.Vars.SetFloat(varName, value);
		}

		/// <summary>
		/// Undoes the modifications done to the property on target from
		/// ApplyModifier.
		/// </summary>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="modifierName"></param>
		protected static void RemoveModifier(Buff buff, ICombatEntity target, string modifierName)
		{
			var varName = GetModifierVarName(modifierName);

			if (buff.Vars.TryGetFloat(varName, out var value))
			{
				buff.Vars.Remove(varName);
			}
		}

		/// <summary>
		/// Modifies the property on the target and saves the value in the buff,
		/// to be able to later undo the change.
		/// </summary>
		/// <remarks>
		/// Repeated calls to this method will stack the modifications, while
		/// one call to RemovePropertyModifier will undo all of them.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="propertyName"></param>
		/// <param name="value"></param>
		protected static void AddPropertyModifier(Buff buff, ICombatEntity target, string propertyName, float value)
		{
			if (!PropertyTable.Exists(target.Properties.Namespace, propertyName))
			{
				Log.Warning($"AddPropertyModifier: {buff.Id} tried to add to property {propertyName} but doesn't exist in id namespace: {target.Properties.Namespace}.");
				return;
			}

			var varName = GetModifierVarName(propertyName);

			if (buff.Vars.TryGetFloat(varName, out var oldValue))
				value += oldValue;

			buff.Vars.SetFloat(varName, value);
			target.Properties.Modify(propertyName, value - oldValue);

			RefreshCaptionOverridesIfRelevant(target, propertyName);
		}

		/// <summary>
		/// Resends the target's caption overrides if the changed property is
		/// one a caption override reads live. What counts is declared by the
		/// overrides themselves via CaptionOverrideDependencies, not by this
		/// method - a new override that reads a new stat needs no change
		/// here.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="propertyName"></param>
		private static void RefreshCaptionOverridesIfRelevant(ICombatEntity target, string propertyName)
		{
			if (target is not Character character)
				return;

			if (!CaptionOverrideDependencies.DependsOn(propertyName))
				return;

			Send.ZC_NORMAL.CaptionOverrides(character);
		}

		/// <summary>
		/// Undoes the modifications done to the property on target from
		/// ApplyPropertyModifier.
		/// </summary>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="propertyName"></param>
		protected static void RemovePropertyModifier(Buff buff, ICombatEntity target, string propertyName)
		{
			if (!PropertyTable.Exists(target.Properties.Namespace, propertyName))
			{
				Log.Warning($"RemovePropertyModifier: {buff.Id} tried to remove to property {propertyName} but doesn't exist in id namespace: {target.Properties.Namespace}.");
				return;
			}

			var varName = GetModifierVarName(propertyName);

			if (buff.Vars.TryGetFloat(varName, out var value))
			{
				target.Properties.Modify(propertyName, -value);
				buff.Vars.Remove(varName);

				RefreshCaptionOverridesIfRelevant(target, propertyName);
			}
		}

		/// <summary>
		/// Sets the value of a property modifier on the target.
		/// </summary>
		/// <remarks>
		/// Removes existing modifiers for the property and then applies the new one.
		/// This is essentially the same as calling RemovePropertyModifier followed
		/// by AddPropertyModifier.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="propertyName"></param>
		/// <param name="value"></param>
		protected static void UpdatePropertyModifier(Buff buff, ICombatEntity target, string propertyName, float value)
			=> SetPropertyModifier(buff, target, propertyName, value);

		/// <summary>
		/// Updates the value of a property modifier on the target.
		/// </summary>
		/// <remarks>
		/// Removes existing modifiers for the property and then applies the new one.
		/// This is essentially the same as calling RemovePropertyModifier followed
		/// by AddPropertyModifier.
		/// </remarks>
		/// <param name="buff"></param>
		/// <param name="target"></param>
		/// <param name="propertyName"></param>
		/// <param name="value"></param>
		protected static void SetPropertyModifier(Buff buff, ICombatEntity target, string propertyName, float value)
		{
			if (!PropertyTable.Exists(propertyName))
				return;

			RemovePropertyModifier(buff, target, propertyName);
			AddPropertyModifier(buff, target, propertyName, value);
		}
	}
}

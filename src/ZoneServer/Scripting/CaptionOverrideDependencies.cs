using System;
using System.Collections.Generic;

namespace Melia.Zone.Scripting
{
	/// <summary>
	/// Declares the properties a caption ratio override method reads live,
	/// right on the method itself - so the association between an override
	/// and what it depends on lives in one atomic place, alongside the
	/// [ScriptableFunction] attribute that names it. Picked up by
	/// ScriptableFunctions.Load and fed into CaptionOverrideDependencies.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class CaptionOverrideDependsOnAttribute : Attribute
	{
		/// <summary>
		/// Returns the properties the override reads live.
		/// </summary>
		public string[] PropertyNames { get; }

		/// <summary>
		/// Creates new attribute.
		/// </summary>
		/// <param name="propertyNames"></param>
		public CaptionOverrideDependsOnAttribute(params string[] propertyNames)
		{
			this.PropertyNames = propertyNames;
		}
	}

	/// <summary>
	/// Registry of the properties caption ratio overrides read live, so a
	/// property change can trigger a resend without the trigger site having
	/// to know which overrides exist or what they depend on.
	/// </summary>
	/// <remarks>
	/// A caption override (see calc_caption.cs) registers its own
	/// dependencies next to itself, atomically, instead of some shared base
	/// class hardcoding a list of "properties that matter" - that list would
	/// otherwise have to be extended by hand every time a new override reads
	/// a new stat, in a file that has no other reason to know about it.
	/// </remarks>
	public static class CaptionOverrideDependencies
	{
		private static readonly HashSet<string> _properties = new();

		/// <summary>
		/// Registers one or more properties a caption override reads live.
		/// </summary>
		/// <param name="propertyNames"></param>
		public static void Register(params string[] propertyNames)
		{
			foreach (var propertyName in propertyNames)
				_properties.Add(propertyName);
		}

		/// <summary>
		/// Returns whether any registered caption override reads the given
		/// property live.
		/// </summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public static bool DependsOn(string propertyName)
			=> _properties.Contains(propertyName);
	}
}

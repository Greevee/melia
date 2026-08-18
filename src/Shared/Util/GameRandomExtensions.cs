using System.Collections.Generic;
using System.Linq;

namespace Melia.Shared.Util
{
	/// <summary>
	/// Collection helpers that roll against <see cref="GameRandom"/>.
	/// </summary>
	/// <remarks>
	/// Named apart from Yggdrasil's Random() rather than replacing it: two
	/// extension methods of the same name on the same type are ambiguous
	/// wherever both namespaces are imported, which is most of the server.
	/// </remarks>
	public static class GameRandomExtensions
	{
		/// <summary>
		/// Returns a random element of the sequence.
		/// </summary>
		/// <param name="source"></param>
		public static T PickRandom<T>(this IEnumerable<T> source)
		{
			var list = source as IList<T> ?? source.ToList();

			return list[GameRandom.Get().Next(list.Count)];
		}
	}
}

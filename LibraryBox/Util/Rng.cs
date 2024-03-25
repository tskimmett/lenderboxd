namespace LibraryBox;

public static class Rng
{
	/// <summary>
	/// Used to perform a random roll with a provided probability of success.
	/// </summary>
	/// <param name="chance">A number between 0 and 1, representing the chance for success.</param>
	/// <param name="tries">A number indicating the number if times to attempt the roll.</param>
	/// <returns>True if the roll was successful, false otherwise.</returns>
	public static bool Roll(double chance, uint tries = 1)
	{
		for (var i = 0; i < tries; i++)
		{
			if (Random.Shared.NextDouble() < chance)
			{
				return true;
			}
		}
		return false;
	}
}

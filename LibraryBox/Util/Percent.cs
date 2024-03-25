namespace LibraryBox;

using System;


[GenerateSerializer]
public readonly record struct Percent(int value)
{
	[Id(0)]
	readonly int _value = value switch
	{
		> 100 => 100,
		< 0 => 0,
		_ => value
	};

	public uint Mult(uint gold)
		=> (uint)Math.Round(gold * _value * .1m);

	public static implicit operator int(Percent p) => p._value;
	public static implicit operator Percent(int i) => new(i);
}
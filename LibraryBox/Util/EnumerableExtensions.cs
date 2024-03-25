﻿namespace LibraryBox;

public static class EnumerableExtensions
{
	public static void Deconstruct<T>(this T[] array, out T first, out T[] rest)
	{
		first = array[0];
		rest = GetRestOfArray(array, 1);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T[] rest)
	{
		first = array[0];
		second = array[1];
		rest = GetRestOfArray(array, 2);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T[] rest)
	{
		first = array[0];
		second = array[1];
		third = array[2];
		rest = GetRestOfArray(array, 3);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T[] rest)
	{
		first = array[0];
		second = array[1];
		third = array[2];
		fourth = array[3];
		rest = GetRestOfArray(array, 4);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T[] rest)
	{
		first = array[0];
		second = array[1];
		third = array[2];
		fourth = array[3];
		fifth = array[4];
		rest = GetRestOfArray(array, 5);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T sixth, out T[] rest)
	{
		first = array[0];
		second = array[1];
		third = array[2];
		fourth = array[3];
		fifth = array[4];
		sixth = array[5];
		rest = GetRestOfArray(array, 6);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T sixth, out T seventh, out T[] rest)
	{
		first = array[0];
		second = array[1];
		third = array[2];
		fourth = array[3];
		fifth = array[4];
		sixth = array[5];
		seventh = array[6];
		rest = GetRestOfArray(array, 7);
	}
	public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T sixth, out T seventh, out T eighth, out T[] rest)
	{
		first = array[0];
		second = array[1];
		third = array[2];
		fourth = array[3];
		fifth = array[4];
		sixth = array[5];
		seventh = array[6];
		eighth = array[7];
		rest = GetRestOfArray(array, 8);
	}

	static T[] GetRestOfArray<T>(T[] array, int skip)
	{
		return array.Skip(skip).ToArray();
	}
}
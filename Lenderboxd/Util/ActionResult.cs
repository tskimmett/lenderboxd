
namespace Lenderboxd;

[GenerateSerializer]
public readonly record struct ActionResult
{
	public static readonly ActionResult Success = new(true);

	public bool Succeeded { get; }
	public CustomError? Error { get; }

	public ActionResult(bool success)
	{
		Succeeded = success;
	}

	public ActionResult(CustomError error)
	{
		Succeeded = false;
		Error = error;
	}

	public static implicit operator ActionResult(bool success) => new(success);
	public static implicit operator ActionResult(CustomError error) => new(error);
	// public static implicit operator bool(ActionResult result) => result.Succeeded;

	public override string ToString()
	{
		return Succeeded ? "Succeeded" : Error!.Value.ToString();
	}
}
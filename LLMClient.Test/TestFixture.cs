namespace LLMClient.Test;

public static class TestFixture
{
	public static void RunInStaThread(Action action)
	{
		RunInStaThreadCore(action);
	}

	private static void RunInStaThreadCore(Action action)
	{
		Exception? exception = null;
		var thread = new Thread(() =>
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				exception = ex;
			}
		});

		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();

		if (exception != null)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
		}
	}

	public static T RunInStaThread<T>(Func<T> action)
	{
		T? result = default;
		RunInStaThreadCore(() => result = action());
		return result!;
	}
}

using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace LLMClient.UI.Component
{
    public struct PostDispatcherrAwaiter : INotifyCompletion
    {
        private readonly Dispatcher _dispatcher;

        public PostDispatcherrAwaiter(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public bool IsCompleted => _dispatcher.CheckAccess();

        public void OnCompleted(Action continuation)
        {
            _dispatcher.BeginInvoke(continuation);
        }

        public void GetResult() { }
    }
}
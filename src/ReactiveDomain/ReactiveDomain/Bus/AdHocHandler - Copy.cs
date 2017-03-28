using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Bus
{
    public class AdHocHandler<T>: IHandle<T> where T: Message
    {
        private readonly Action<T> _handle;

        public AdHocHandler(Action<T> handle)
        {
            Ensure.NotNull(handle, "handle");
            _handle = handle;
        }

        public void Handle(T message)
        {
            _handle(message);
        }
    }
}
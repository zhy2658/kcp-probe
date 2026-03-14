using System;
using System.Collections.Generic;
using Google.Protobuf;
using KcpServer;

namespace Kcp.Core
{
    public class PacketDispatcher
    {
        private readonly object _lock = new object();
        private readonly Dictionary<uint, Action<BaseMessage>?> _handlers = new Dictionary<uint, Action<BaseMessage>?>();
        
        public event Action<BaseMessage>? OnMessageDispatch;
        public event Action<LogLevel, string>? OnLog;

        public void RegisterHandler(uint msgId, Action<BaseMessage> handler)
        {
            lock (_lock)
            {
                if (_handlers.ContainsKey(msgId))
                {
                    _handlers[msgId] += handler;
                }
                else
                {
                    _handlers[msgId] = handler;
                }
            }
        }

        public void UnregisterHandler(uint msgId, Action<BaseMessage> handler)
        {
            lock (_lock)
            {
                if (_handlers.ContainsKey(msgId))
                {
                    _handlers[msgId] -= handler;
                    if (_handlers[msgId] == null)
                    {
                        _handlers.Remove(msgId);
                    }
                }
            }
        }

        public void Dispatch(ReadOnlyMemory<byte> data)
        {
            try
            {
                var baseMsg = BaseMessage.Parser.ParseFrom(data.Span);
                OnMessageDispatch?.Invoke(baseMsg);
                
                Action<BaseMessage>? handler = null;
                lock (_lock)
                {
                    if (_handlers.TryGetValue(baseMsg.MsgId, out var h))
                    {
                        handler = h;
                    }
                }
                
                handler?.Invoke(baseMsg);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(LogLevel.Error, $"Dispatch Error: {ex.Message}");
            }
        }
        
        public T ParsePayload<T>(BaseMessage baseMsg) where T : IMessage<T>, new()
        {
            var msg = new T();
            msg.MergeFrom(baseMsg.Payload);
            return msg;
        }
    }
}

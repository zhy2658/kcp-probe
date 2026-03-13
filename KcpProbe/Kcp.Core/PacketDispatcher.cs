using System;
using System.Collections.Generic;
using Google.Protobuf;
using KcpServer;

namespace Kcp.Core
{
    public class PacketDispatcher
    {
        private readonly Dictionary<uint, Action<BaseMessage>> _handlers = new Dictionary<uint, Action<BaseMessage>>();
        private static PacketDispatcher? _instance;
        public static PacketDispatcher Instance => _instance ??= new PacketDispatcher();

        public void RegisterHandler(uint msgId, Action<BaseMessage> handler)
        {
            _handlers[msgId] = handler;
        }

        public void Dispatch(byte[] data)
        {
            try
            {
                var baseMsg = BaseMessage.Parser.ParseFrom(data);
                if (_handlers.TryGetValue(baseMsg.MsgId, out var handler))
                {
                    handler(baseMsg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispatch Error: {ex.Message}");
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

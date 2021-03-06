﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using DistCache.Common.NetworkManagement;
using DistCache.Common;
using DistCache.Common.Utilities;
using DistCache.Common.Protocol.Messages;

namespace DistCache.Server.Protocol.Handlers
{
    public class ServerHandShakeHandler : SocketHandler
    {
        public enum HandShakeState
        {
            Awaiting,
            Authorised,
            NotAuthorised,
            ProtocolError
        }

        public Guid TemporaryID { get; private set; }

        public HandShakeState State { get; private set; } = HandShakeState.Awaiting;
        private CacheServer _server;


        public ServerHandShakeHandler(TcpClient tcp, CacheServer server, Guid tempGuid) : base(tcp, server.Config)
        {
            this.TemporaryID = tempGuid;
            this._server = server;
        }

        protected override bool HandleMessages(byte[] message)
        {
            try
            {
                HandShakeRequest msg = BsonUtilities.Deserialise<HandShakeRequest>(message);
                Console.WriteLine("HandShakeRequest rec");
                if (Config.Password.Equals(msg.AuthPassword))
                {
                    State = HandShakeState.Authorised;
                }
                else
                {
                    State = HandShakeState.NotAuthorised;
                }

                var waiter = new TaskCompletionSource<bool>();

                this.SendMessage(new HandShakeOutcome()
                {
                    MessageType = State == HandShakeState.Authorised ? MessageTypeEnum.AuthRequestOk : MessageTypeEnum.AuthRequestError,
                    ServerGuid = State == HandShakeState.Authorised ? this._server.ServerGuid : Guid.Empty
                }, waiter);

                waiter.Task.Wait();
                if (waiter.Task.Result && State == HandShakeState.Authorised)
                {
                    if (msg.MessageType == MessageTypeEnum.ClientAuthRequest)
                    {
                        _server.ClientConnected(msg.RegisteredGuid, TemporaryID);
                    }
                    else if (msg.MessageType == MessageTypeEnum.ServerAuthRequest)
                    {
                        _server.ServerConnected(msg.RegisteredGuid, TemporaryID);
                    }
                }
                else
                {
                    _server.ConnectionFailed(this, TemporaryID);
                }
            }
            catch (Exception ex)
            {
                this.State = HandShakeState.ProtocolError;
                _server.ConnectionFailed(this, TemporaryID);
            }
            return false;
        }


    }
}

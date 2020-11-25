using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;
using NetttyModel.Event;
using NettyModel.Event;
using Newtonsoft.Json;
using System;
using System.Threading;

namespace DotNettyClient.DotNetty
{
    public class NettyClientChannelHandler : SimpleChannelInboundHandler<ChatInfo>
    {
        private string serverIP;
        private int serverPort;
        public NettyClientChannelHandler(string serverIP, int serverPort)
        {
            this.serverIP = serverIP;
            this.serverPort = serverPort;
        }

        public override bool IsSharable => true;//标注一个channel handler可以被多个channel安全地共享。

        /// <summary>
        /// 收到服务端消息
        /// </summary>
        /// <param name="ctx">通道处理上下文</param>
        /// <param name="msg">接收内容</param>
        protected override void ChannelRead0(IChannelHandlerContext ctx, ChatInfo msg)
        {
            try
            {
                // 收到发送给服务端的心跳包，服务端回应
                if (msg.Code == (int)NettyCodeEnum.Ping)
                {
                    ClientEventHandler.LstSendPings.Clear();
                    ClientEventHandler.RecordLogEvent?.Invoke(true, "收到Android端心跳回应");
                    return;
                }
                // 发送数据给服务端，服务端处理成功回应
                if (msg.Code == (int)NettyCodeEnum.OK)
                {
                    lock (ClientEventHandler.LockOjb)
                    {
                        ClientEventHandler.LstNeedSendDatas.RemoveAll(cu => cu.ChatInfo.ReqId == msg.ReqId);
                    }
                    return;
                }
                // 收到服务端发送过来的聊天内容
                if (msg.Code == (int)NettyCodeEnum.Chat)
                {
                    ClientEventHandler.ReceiveEventFromClientEvent?.Invoke(msg);
                }
                var eventMsg = JsonConvert.SerializeObject(msg);
                ClientEventHandler.RecordLogEvent?.Invoke(true, $"收到Android端消息：{eventMsg}");
            }
            catch (Exception ex)
            {
                ClientEventHandler.RecordLogEvent?.Invoke(false, $"读取数据异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 消息接收完毕
        /// </summary>
        /// <param name="context">通道处理上下文</param>
        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            base.ChannelReadComplete(context);
            context.Flush();
            ClientEventHandler.RecordLogEvent?.Invoke(true, "ChannelReadComplete");
        }

        /// <summary>
        /// 注册通道
        /// </summary>
        /// <param name="context">通道处理上下文</param>
        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
            ClientEventHandler.RecordLogEvent?.Invoke(false, $"注册通道：{context.Channel.RemoteAddress}");
            ClientEventHandler.RunSendData(context);
        }

        /// <summary>
        /// 通道激活
        /// </summary>
        /// <param name="context">通道处理上下文</param>
        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            ClientEventHandler.RecordLogEvent?.Invoke(false, $"通道激活：{context.Channel.RemoteAddress}");
            ClientEventHandler.IsConnect = true;
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
            ClientEventHandler.RecordLogEvent?.Invoke(false, $"断开连接：{context.Channel.RemoteAddress}");
            ClientEventHandler.IsConnect = false;
        }

        /// <summary>
        /// 注销通道
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelUnregistered(IChannelHandlerContext context)
        {
            base.ChannelUnregistered(context);
            ClientEventHandler.RecordLogEvent?.Invoke(false, $"注销通道：{context.Channel.RemoteAddress}");
            ClientEventHandler.IsConnect = false;
            Thread.Sleep(TimeSpan.FromSeconds(5));

            if (!ClientEventHandler.IsConnect)
            {
                NettyClient nettyClient = new NettyClient(serverIP, serverPort);
                nettyClient.ConnectServer().Wait();
            }
        }

        /// <summary>
        /// 异常
        /// </summary>
        /// <param name="context"></param>
        /// <param name="exception"></param>
        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            ClientEventHandler.RecordLogEvent?.Invoke(false, $"Exception: {exception.Message}");
            ClientEventHandler.IsConnect = false;
            context.CloseAsync();
        }

        /// <summary>
        /// 读写超时通知
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="evt"></param>
        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            base.UserEventTriggered(ctx, evt);
            if (evt is IdleStateEvent)
            {
                var eventState = evt as IdleStateEvent;
                if (eventState != null)
                {
                    switch (eventState.State)
                    {
                        // 长时间未读取到数据，则发送心跳数据
                        case IdleState.ReaderIdle:
                            ClientEventHandler.SendPingMsg(ctx);
                            break;
                        case IdleState.WriterIdle:
                            break;
                        case IdleState.AllIdle:
                            break;
                    }
                }
            }
        }
    }
}

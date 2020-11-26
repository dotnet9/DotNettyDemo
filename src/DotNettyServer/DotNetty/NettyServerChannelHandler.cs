using DotNetty.Transport.Channels;
using MessagePack;
using NetttyModel.Event;
using NettyModel.Event;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;

namespace DotNettyServer.DotNetty
{
    /// <summary>
    /// 因为服务器只需要响应传入的消息，所以只需要实现ChannelHandlerAdapter就可以了
    /// </summary>
    public class NettyServerChannelHandler : SimpleChannelInboundHandler<ChatInfo>
    {
        private ConcurrentDictionary<string, IChannelHandlerContext> dictClients = new ConcurrentDictionary<string, IChannelHandlerContext>();
        public event Action<ChatInfo> ReceiveEventFromClientEvent;
        public event Action<string> ReceiveClientOnlineEvent;
        //bool:true：正常日志，false:异常日志
        public event Action<bool, string> RecordLogEvent;


        /// <summary>
        /// 发送数据到客户端
        /// </summary>
        /// <param name="testEvent"></param>
        public void SendData(ChatInfo testEvent)
        {
            try
            {
                if (dictClients == null || dictClients.Count <= 0)
                {
                    RecordLogEvent?.Invoke(false, $"未连接客户端，无法发送数据");
                    return;
                }
                foreach (var kvp in dictClients)
                {
                    RecordLogEvent?.Invoke(true, $"向客户端（{kvp.Key}）发送消息：{testEvent.Data}");
                    testEvent.ToId = kvp.Key;
                    kvp.Value.WriteAndFlushAsync(testEvent);
                }
            }
            catch (Exception ex)
            {
                RecordLogEvent?.Invoke(false, $"发送数据异常：{ex.Message}");
            }
        }
        public override bool IsSharable => true;//标注一个channel handler可以被多个channel安全地共享。

        /// <summary>
        /// 收到客户端回应
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="msg"></param>
        protected override void ChannelRead0(IChannelHandlerContext ctx, ChatInfo msg)
        {
            try
            {
                // 服务端收到心跳，直接回应
                if (msg.Code == (int)NettyCodeEnum.Ping)
                {
                    ctx.WriteAndFlushAsync(msg);
                    RecordLogEvent?.Invoke(true, $"收到心跳并原文回应：{ctx.Channel.RemoteAddress}");
                    return;
                }
                if (msg.Code == (int)NettyCodeEnum.Chat)
                {
                    RecordLogEvent?.Invoke(true, $"服务端接收到聊天消息({ctx.Channel.RemoteAddress}):" + JsonConvert.SerializeObject(msg));

                    foreach (var kvp in dictClients)
                    {
                        if (ctx.Channel.RemoteAddress.ToString() == kvp.Key)
                        {
                            // 回应收到消息成功
                            msg.Code = (int)NettyCodeEnum.OK;
                        }
                        else
                        {
                            // 群发消息
                            msg.Code = (int)NettyCodeEnum.Chat;
                        }
                        kvp.Value.WriteAndFlushAsync(msg);
                    }
                    msg.FromId = ctx.Channel.RemoteAddress.ToString();
                    ReceiveEventFromClientEvent?.Invoke(msg);
                    return;
                }
                RecordLogEvent?.Invoke(true, $"服务端接收到消息({ctx.Channel.RemoteAddress}):" + JsonConvert.SerializeObject(msg));

            }
            catch (Exception ex)
            {
                RecordLogEvent?.Invoke(false, $"收到客户端消息，处理失败({ctx.Channel.RemoteAddress})：{ex.Message}");
            }
        }

        /// <summary>
        /// 消息读取完成
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            base.ChannelReadComplete(context);
            context.Flush();
            RecordLogEvent?.Invoke(true, $"ChannelReadComplete");
        }

        /// <summary>
        /// 注册通道
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
            RecordLogEvent?.Invoke(true, $"注册通道：{context.Channel.RemoteAddress}");
        }

        /// <summary>
        /// 通道激活
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelActive(IChannelHandlerContext context)
        {
            var clientAddress = context.Channel.RemoteAddress.ToString();
            dictClients[clientAddress] = context;
            ReceiveClientOnlineEvent?.Invoke(clientAddress);
            RecordLogEvent?.Invoke(true, $"客户端上线：{context.Channel.RemoteAddress}");
            RecordLogEvent?.Invoke(true, $"通道激活：{context.Channel.RemoteAddress}");
            base.ChannelActive(context);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelInactive(IChannelHandlerContext context)
        {
            RecordLogEvent?.Invoke(false, $"断开连接：{context.Channel.RemoteAddress}");
            base.ChannelInactive(context);
        }

        /// <summary>
        /// 注销通道
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelUnregistered(IChannelHandlerContext context)
        {
            base.ChannelUnregistered(context);
            RecordLogEvent?.Invoke(false, $"注销通道：{context.Channel.RemoteAddress}");
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            RecordLogEvent?.Invoke(false, $"异常：{exception.Message}");
            context.CloseAsync();
        }
    }
}

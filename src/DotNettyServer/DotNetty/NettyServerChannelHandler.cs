﻿using DotNetty.Transport.Channels;
using MessagePack;
using NetttyModel.Event;
using NettyModel.Event;
using Newtonsoft.Json;
using System;

namespace DotNettyServer.DotNetty
{
    /// <summary>
    /// 因为服务器只需要响应传入的消息，所以只需要实现ChannelHandlerAdapter就可以了
    /// </summary>
    public class NettyServerChannelHandler : SimpleChannelInboundHandler<ChatInfo>
    {
        private IChannelHandlerContext channelHandlerContext;
        public event Action<ChatInfo> ReceiveEventFromClientEvent;
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
                if (channelHandlerContext == null)
                {
                    RecordLogEvent?.Invoke(false, $"未连接客户端，无法发送数据");
                    return;
                }
                channelHandlerContext.WriteAndFlushAsync(testEvent);
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
            channelHandlerContext = ctx;
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

                    // 回应收到消息成功
                    msg.Code = (int)NettyCodeEnum.OK;
                    ctx.WriteAndFlushAsync(msg);
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
            RecordLogEvent?.Invoke(true, $"ChannelReadComplete" );
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
            channelHandlerContext = context;
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

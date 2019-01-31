﻿using Discord;
using Skuld.Discord.Services;
using System;
using System.Threading.Tasks;

namespace Skuld.Discord.Extensions
{
    public static class Messages
    {
        public static Task QueueMessage(this Embed embed, Models.MessageType type, IUser user, IMessageChannel channel, string content = "", string filepath = null, Exception exception = null, double timeout = 0.0)
        {
            MessageQueue.AddMessage(new Models.SkuldMessage
            {
                Channel = channel,
                Meta = new Models.SkuldMessageMeta
                {
                    Exception = exception,
                    Timeout = timeout,
                    Type = type
                },
                Content = new Models.SkuldMessageContent
                {
                    Embed = embed,
                    Message = content,
                    User = user,
                    File = filepath
                }
            });
            return Task.CompletedTask;
        }
        public static Task QueueMessage(this string content, Models.MessageType type, IUser user, IMessageChannel channel, string filepath = null, Exception exception = null, double timeout = 0.0)
        {
            MessageQueue.AddMessage(new Models.SkuldMessage
            {
                Channel = channel,
                Meta = new Models.SkuldMessageMeta
                {
                    Exception = exception,
                    Timeout = timeout,
                    Type = type
                },
                Content = new Models.SkuldMessageContent
                {
                    Message = content,
                    Embed = null,
                    User = user,
                    File = filepath
                }
            });
            return Task.CompletedTask;
        }
    }
}
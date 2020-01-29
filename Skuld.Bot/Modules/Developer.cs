﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Skuld.Bot.Services;
using Skuld.Core;
using Skuld.Core.Extensions;
using Skuld.Core.Extensions.Formatting;
using Skuld.Core.Extensions.Verification;
using Skuld.Core.Models;
using Skuld.Core.Models.Commands;
using Skuld.Core.Utilities;
using Skuld.Discord.BotListing;
using Skuld.Discord.Extensions;
using Skuld.Discord.Models;
using Skuld.Discord.Preconditions;
using Skuld.Discord.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Skuld.Bot.Commands
{
    [Group]
    [RequireBotFlag(BotAccessLevel.BotAdmin)]
    public class Developer : ModuleBase<ShardedCommandContext>
    {
        public SkuldConfig Configuration { get => HostSerivce.Configuration; }

        #region BotAdmin

        [Command("bean")]
        public async Task Bean(IGuildUser user, [Remainder]string reason = null)
        {
            using var Database = new SkuldDbContextFactory().CreateDbContext();

            var usr = Database.Users.FirstOrDefault(x => x.Id == user.Id);

            if (usr.Flags.IsBitSet(DiscordUtilities.Banned))
            {
                usr.Flags -= DiscordUtilities.Banned;
                usr.BanReason = null;
                await
                    EmbedExtensions.FromSuccess(SkuldAppContext.GetCaller(), $"Un-beaned {user.Mention}", Context)
                .QueueMessageAsync(Context).ConfigureAwait(false);
            }
            else
            {
                if (reason == null)
                {
                    await
                        EmbedExtensions.FromError($"{nameof(reason)} needs a value", Context)
                    .QueueMessageAsync(Context).ConfigureAwait(false);
                    return;
                }
                usr.Flags += DiscordUtilities.Banned;
                usr.BanReason = reason;
                await
                    EmbedExtensions.FromSuccess(SkuldAppContext.GetCaller(), $"Beaned {user.Mention} for reason: `{reason}`", Context)
                .QueueMessageAsync(Context).ConfigureAwait(false);
            }

            await Database.SaveChangesAsync().ConfigureAwait(false);
        }

        [Command("shardrestart"), Summary("Restarts shard")]
        public async Task ReShard(int shard)
        {
            await Context.Client.GetShard(shard).StopAsync().ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await Context.Client.GetShard(shard).StartAsync().ConfigureAwait(false);
        }

        [Command("dumpshards"), Summary("Shard Info"), Alias("dumpshard")]
        public async Task Shard()
        {
            var lines = new List<string[]>
            {
                new string[] { "Shard", "State", "Latency", "Guilds" }
            };
            foreach (var item in Context.Client.Shards)
            {
                lines.Add(new string[] { item.ShardId.ToString(), item.ConnectionState.ToString(), item.Latency.ToString(), item.Guilds.Count.ToString() });
            }

            await $"```\n{lines.PrettyLines(2)}```".QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("getshard"), Summary("Gets all information about specific shard")]
        public async Task ShardGet(int shardid = -1)
        {
            if (shardid > -1)
                await ShardInfo(shardid).ConfigureAwait(false);
            else
                await ShardInfo(Context.Client.GetShardIdFor(Context.Guild)).ConfigureAwait(false);
        }

        public async Task ShardInfo(int shardid)
        {
            var shard = Context.Client.GetShard(shardid);
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = $"Shard ID: {shardid}",
                    IconUrl = shard.CurrentUser.GetAvatarUrl()
                },
                Footer = new EmbedFooterBuilder
                {
                    Text = "Generated at"
                },
                Timestamp = DateTime.Now,
                Color = EmbedExtensions.RandomEmbedColor()
            };
            embed.AddInlineField("Guilds", shard.Guilds.Count.ToString());
            embed.AddInlineField("Status", shard.ConnectionState);
            embed.AddInlineField("Latency", shard.Latency + "ms");
            embed.AddField("Game", shard.Activity.Name);
            await embed.Build().QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("shard"), Summary("Gets the shard the guild is on")]
        public async Task ShardGet(ulong guildId = 0)
        {
            IGuild guild;
            if (guildId != 0)
                guild = BotService.DiscordClient.GetGuild(guildId);
            else
                guild = Context.Guild;

            await $"{Context.User.Mention} the server: `{guild.Name}` is on `{Context.Client.GetShardIdFor(guild)}`".QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("setgame"), Summary("Set Game")]
        public async Task Game([Remainder]string title)
        {
            await Context.Client.SetGameAsync(title).ConfigureAwait(false);
        }

        [Command("resetgame"), Summary("Reset Game")]
        public async Task ResetGame()
        {
            foreach (var shard in Context.Client.Shards)
            {
                await shard.SetGameAsync($"{Configuration.Prefix}help | {shard.ShardId + 1}/{Context.Client.Shards.Count}", type: ActivityType.Listening).ConfigureAwait(false);
            }
        }

        [Command("setstream"), Summary("Sets stream")]
        public async Task Stream(string streamer, [Remainder]string title)
        {
            await Context.Client.SetGameAsync(title, "https://twitch.tv/" + streamer, ActivityType.Streaming).ConfigureAwait(false);
        }

        [Command("setactivity"), Summary("Sets activity")]
        public async Task ActivityAsync(ActivityType activityType, [Remainder]string status)
        {
            await Context.Client.SetGameAsync(status, null, activityType).ConfigureAwait(false);
        }

        [Command("grantxp"), Summary("Grant Exp")]
        public async Task GrantExp(ulong amount, [Remainder]IUser user = null)
        {
            if (user == null)
                user = Context.User;
            
            using var database = new SkuldDbContextFactory().CreateDbContext();

            var usr = await database.InsertOrGetUserAsync(user).ConfigureAwait(false);

            await usr.GrantExperienceAsync(amount, Context.Guild, true).ConfigureAwait(false);

            await EmbedExtensions.FromSuccess($"Gave {user.Mention} {amount.ToString("N0")}xp", Context).QueueMessageAsync(Context).ConfigureAwait(false);
        }

        #endregion BotAdmin

        #region BotOwner

        [Command("populate")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task Populate(ulong? guildId = null)
        {
            using var database = new SkuldDbContextFactory().CreateDbContext();

            IGuild guild;

            if (guildId.HasValue)
                guild = Context.Client.GetGuild(guildId.Value);
            else
                guild = Context.Guild;

            await guild.DownloadUsersAsync().ConfigureAwait(false);

            var users = await guild.GetUsersAsync().ConfigureAwait(false);

            foreach (var user in users)
            {
                await database.InsertOrGetUserAsync(user).ConfigureAwait(false);
            }

            await
                EmbedExtensions.FromSuccess(SkuldAppContext.GetCaller(), $"Added all users for guild `{guild.Name}`", Context)
                .QueueMessageAsync(Context)
                .ConfigureAwait(false);
        }

        [Command("stop")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task Stop()
        {
            await "Stopping!".QueueMessageAsync(Context).ConfigureAwait(false);
            await BotService.StopBotAsync("StopCmd").ConfigureAwait(false);
        }

        [Command("addflag")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task SetFlag(BotAccessLevel level, [Remainder]IUser user = null)
        {
            if (user == null)
                user = Context.User;

            using var Database = new SkuldDbContextFactory().CreateDbContext();

            var dbUser = await Database.InsertOrGetUserAsync(user).ConfigureAwait(false);

            bool added = false;

            switch (level)
            {
                case BotAccessLevel.BotOwner:
                    if (!dbUser.Flags.IsBitSet(DiscordUtilities.BotCreator))
                    {
                        dbUser.Flags += DiscordUtilities.BotCreator;
                        added = true;
                    }
                    break;

                case BotAccessLevel.BotAdmin:
                    if (!dbUser.Flags.IsBitSet(DiscordUtilities.BotAdmin))
                    {
                        dbUser.Flags += DiscordUtilities.BotAdmin;
                        added = true;
                    }
                    break;

                case BotAccessLevel.BotTester:
                    if (!dbUser.Flags.IsBitSet(DiscordUtilities.BotTester))
                    {
                        dbUser.Flags += DiscordUtilities.BotTester;
                        added = true;
                    }
                    break;

                case BotAccessLevel.BotDonator:
                    if (!dbUser.Flags.IsBitSet(DiscordUtilities.BotDonator))
                    {
                        dbUser.Flags += DiscordUtilities.BotDonator;
                        added = true;
                    }
                    break;
            }

            if (added)
            {
                await Database.SaveChangesAsync().ConfigureAwait(false);

                await $"Added flag `{level}` to {user.Mention}".QueueMessageAsync(Context).ConfigureAwait(false);
            }
            else
            {
                await $"{user.Mention} already has the flag `{level}`".QueueMessageAsync(Context).ConfigureAwait(false);
            }
        }

        [Command("flags")]
        public async Task GetFlags([Remainder]IUser user = null)
        {
            if (user == null)
                user = Context.User;

            using var Database = new SkuldDbContextFactory().CreateDbContext();

            var dbUser = await Database.InsertOrGetUserAsync(user).ConfigureAwait(false);

            List<BotAccessLevel> flags = new List<BotAccessLevel>();

            if (dbUser.Flags.IsBitSet(DiscordUtilities.BotCreator))
                flags.Add(BotAccessLevel.BotOwner);
            if (dbUser.Flags.IsBitSet(DiscordUtilities.BotAdmin))
                flags.Add(BotAccessLevel.BotAdmin);
            if (dbUser.Flags.IsBitSet(DiscordUtilities.BotDonator))
                flags.Add(BotAccessLevel.BotDonator);
            if (dbUser.Flags.IsBitSet(DiscordUtilities.BotTester))
                flags.Add(BotAccessLevel.BotTester);

            flags.Add(BotAccessLevel.Normal);

            await $"{user.Mention} has the flags `{string.Join(", ", flags)}`".QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("jsoncommands")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task Commands()
        {
            var Modules = new List<ModuleSkuld>();

            BotService.CommandService.Modules.ToList().ForEach(module =>
            {
                ModuleSkuld mod = new ModuleSkuld
                {
                    Name = module.Name,
                    Commands = new List<CommandSkuld>()
                };
                module.Commands.ToList().ForEach(cmd =>
                {
                    var parameters = new List<ParameterSkuld>();

                    cmd.Parameters.ToList().ForEach(paras =>
                    {
                        parameters.Add(new ParameterSkuld
                        {
                            Name = paras.Name,
                            Optional = paras.IsOptional
                        });
                    });

                    mod.Commands.Add(new CommandSkuld
                    {
                        Name = cmd.Name,
                        Description = cmd.Summary,
                        Aliases = cmd.Aliases.ToArray(),
                        Parameters = parameters.ToArray()
                    });
                });
                Modules.Add(mod);
            });

            var filename = Path.Combine(SkuldAppContext.StorageDirectory, "commands.json");

            File.WriteAllText(filename, JsonConvert.SerializeObject(Modules));

            await $"Written commands to {filename}".QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("name"), Summary("Name")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task Name([Remainder]string name)
            => await Context.Client.CurrentUser.ModifyAsync(x => x.Username = name).ConfigureAwait(false);

        [Command("status"), Summary("Status")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task Status(string status)
        {
            switch (status.ToLowerInvariant())
            {
                case "online":
                    await Context.Client.SetStatusAsync(UserStatus.Online).ConfigureAwait(false);
                    break;

                case "afk":
                    await Context.Client.SetStatusAsync(UserStatus.AFK).ConfigureAwait(false);
                    break;

                case "dnd":
                case "do not disturb":
                case "donotdisturb":
                case "busy":
                    await Context.Client.SetStatusAsync(UserStatus.DoNotDisturb).ConfigureAwait(false);
                    break;

                case "idle":
                case "away":
                    await Context.Client.SetStatusAsync(UserStatus.Idle).ConfigureAwait(false);
                    break;

                case "offline":
                    await Context.Client.SetStatusAsync(UserStatus.Offline).ConfigureAwait(false);
                    break;

                case "invisible":
                    await Context.Client.SetStatusAsync(UserStatus.Invisible).ConfigureAwait(false);
                    break;

                default:
                    break;
            }
        }

        [Command("dropuser")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task DropUser(ulong userId)
        {
            using var database = new SkuldDbContextFactory().CreateDbContext();

            await database.DropUserAsync(userId);
        }

        [Command("dropguild")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task DropGuild(ulong guildId)
        {
            using var database = new SkuldDbContextFactory().CreateDbContext();

            await database.DropGuildAsync(guildId);
        }

        [Command("merge")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task Merge(ulong oldId, ulong newId)
        {
            if (Context.Client.GetUser(newId) == null)
            {
                await $"No. {newId} is not a valid user Id".QueueMessageAsync(Context).ConfigureAwait(false);
                return;
            }
            if (newId == oldId)
            {
                await $"No.".QueueMessageAsync(Context).ConfigureAwait(false);
                return;
            }
            try
            {
                //UserAccount
                {
                    using var db = new SkuldDbContextFactory().CreateDbContext();

                    var oldUser = db.Users.FirstOrDefault(x => x.Id == oldId);
                    var newUser = db.Users.FirstOrDefault(x => x.Id == newId);

                    if (oldUser != null && newUser != null)
                    {
                        newUser.Money += oldUser.Money;
                        newUser.Title = oldUser.Title;
                        newUser.Language = oldUser.Language;
                        newUser.Patted = oldUser.Patted;
                        newUser.Pats = oldUser.Pats;
                        newUser.UnlockedCustBG = oldUser.UnlockedCustBG;
                        newUser.Background = oldUser.Background;

                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                //Reputation
                {
                    using var db = new SkuldDbContextFactory().CreateDbContext();

                    var repee = db.Reputations.AsQueryable().Where(x => x.Repee == oldId);
                    var reper = db.Reputations.AsQueryable().Where(x => x.Reper == oldId);

                    if (repee.Any())
                    {
                        foreach (var rep in repee)
                        {
                            if (!db.Reputations.Any(x => x.Repee == newId && x.Reper == rep.Reper))
                            {
                                rep.Repee = newId;
                            }
                        }
                    }

                    if (reper.Any())
                    {
                        foreach (var rep in reper)
                        {
                            if (!db.Reputations.Any(x => x.Reper == newId && x.Repee == rep.Repee))
                            {
                                rep.Reper = newId;
                            }
                        }
                    }

                    if (repee.Any() || reper.Any())
                    {
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                //Pastas
                {
                    using var db = new SkuldDbContextFactory().CreateDbContext();

                    var pastas = db.Pastas.AsQueryable().Where(x => x.OwnerId == oldId);

                    if (pastas.Any())
                    {
                        foreach (var pasta in pastas)
                        {
                            pasta.OwnerId = newId;
                        }

                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                //PastaVotes
                {
                    //TODO: Add When implemented
                    using var db = new SkuldDbContextFactory().CreateDbContext();
                }

                //CommandUsage
                {
                    using var db = new SkuldDbContextFactory().CreateDbContext();

                    var commands = db.UserCommandUsage.AsQueryable().Where(x => x.UserId == oldId);

                    if (commands.Any())
                    {
                        foreach (var command in commands)
                        {
                            command.UserId = newId;
                        }

                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                //Experience
                {
                    using var db = new SkuldDbContextFactory().CreateDbContext();

                    var experiences = db.UserXp.AsQueryable().Where(x => x.UserId == oldId);

                    if (experiences.Any())
                    {
                        foreach (var experience in experiences)
                        {
                            experience.UserId = newId;
                        }

                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                //Prune Old User
                {
                    using var db = new SkuldDbContextFactory().CreateDbContext();

                    await db.DropUserAsync(oldId);
                }

                await $"Successfully merged data from {oldId} into {newId}".QueueMessageAsync(Context);
            }
            catch (Exception ex)
            {
                Log.Error("MergeCmd", ex.Message, ex);
                await "Check the console log".QueueMessageAsync(Context);
            }
        }

        [Command("moneyadd"), Summary("Gives money to people"), RequireDatabase]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task GiveMoney(IGuildUser user, ulong amount)
        {
            using var Database = new SkuldDbContextFactory().CreateDbContext();

            var usr = Database.Users.FirstOrDefault(x => x.Id == user.Id);
            usr.Money += amount;

            await Database.SaveChangesAsync().ConfigureAwait(false);

            await $"User {user.Username} now has: {(await Database.GetOrInsertGuildAsync(Context.Guild).ConfigureAwait(false)).MoneyIcon}{usr.Money.ToString("N0")}".QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("leave"), Summary("Leaves a server by id")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task LeaveServer(ulong id)
        {
            var guild = Context.Client.GetGuild(id);
            await guild.LeaveAsync().ContinueWith(async x =>
            {
                if (Context.Client.GetGuild(id) == null)
                {
                    await EmbedExtensions.FromSuccess($"Left guild **{guild.Name}**", Context).QueueMessageAsync(Context).ConfigureAwait(false);
                }
                else
                {
                    await EmbedExtensions.FromError($"Hmm, I haven't left **{guild.Name}**", Context).QueueMessageAsync(Context).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [Command("pubstats"), Summary("no")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task PubStats()
        {
            await "Ok, publishing stats to the Discord Bot lists.".QueueMessageAsync(Context).ConfigureAwait(false);
            string list = "";
            int shardcount = Context.Client.Shards.Count;
            await Context.Client.SendDataAsync(Configuration.IsDevelopmentBuild, Configuration.DiscordGGKey, Configuration.DBotsOrgKey, Configuration.B4DToken).ConfigureAwait(false);
            foreach (var shard in Context.Client.Shards)
            {
                list += $"I sent ShardID: {shard.ShardId} Guilds: {shard.Guilds.Count} Shards: {shardcount}\n";
            }
            await list.QueueMessageAsync(Context).ConfigureAwait(false);
        }

        [Command("eval"), Summary("no")]
        [RequireBotFlag(BotAccessLevel.BotOwner)]
        public async Task EvalStuff([Remainder]string code)
        {
            try
            {
                if (code.ToLowerInvariant().Contains("token") || code.ToLowerInvariant().Contains("key"))
                {
                    await EmbedExtensions.FromError("Nope.", Context).QueueMessageAsync(Context).ConfigureAwait(false);
                    return;
                }
                if (code.StartsWith("```cs", StringComparison.Ordinal))
                {
                    code = code.Replace("`", "");
                    code = code.Remove(0, 2);
                }
                else if (code.StartsWith("```", StringComparison.Ordinal))
                {
                    code = code.Replace("`", "");
                }

                var embed = new EmbedBuilder();
                var globals = new Globals().Context = Context as ShardedCommandContext;
                var soptions = ScriptOptions
                    .Default
                    .WithReferences(typeof(SkuldDatabaseContext).Assembly)
                    .WithReferences(typeof(ShardedCommandContext).Assembly, typeof(ShardedCommandContext).Assembly,
                    typeof(SocketGuildUser).Assembly, typeof(Task).Assembly, typeof(Queryable).Assembly,
                    typeof(BotService).Assembly)
                    .WithImports(typeof(SkuldDatabaseContext).FullName)
                    .WithImports(typeof(ShardedCommandContext).FullName, typeof(ShardedCommandContext).FullName,
                    typeof(SocketGuildUser).FullName, typeof(Task).FullName, typeof(Queryable).FullName,
                    typeof(BotService).FullName);

                var script = CSharpScript.Create(code, soptions, globalsType: typeof(ShardedCommandContext));
                script.Compile();

                var result = (await script.RunAsync(globals: globals).ConfigureAwait(false)).ReturnValue;

                embed.Author = new EmbedAuthorBuilder
                {
                    Name = result.GetType().ToString()
                };
                embed.Color = EmbedExtensions.RandomEmbedColor();
                embed.Description = $"{result}";
                if (result != null)
                {
                    await embed.Build().QueueMessageAsync(Context).ConfigureAwait(false);
                }
                else
                {
                    await "Result is empty or null".QueueMessageAsync(Context).ConfigureAwait(false);
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (NullReferenceException ex) { /*Do nothing here*/ }
#pragma warning restore CS0168 // Variable is declared but never used
            catch (Exception ex)
            {
                Log.Error("EvalCMD", "Error with eval command " + ex.Message, ex);

                await EmbedExtensions.FromError($"Error with eval command\n\n{ex.Message}", Context).QueueMessageAsync(Context).ConfigureAwait(false);
            }
        }

        public class Globals
        {
            public ShardedCommandContext Context { get; set; }
        }

        #endregion BotOwner
    }
}
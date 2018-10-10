﻿using Booru.Net;
using Discord;
using Discord.Commands;
using Skuld.APIS;
using Skuld.APIS.NekoLife.Models;
using Skuld.Commands;
using Skuld.Core.Commands.Attributes;
using Skuld.Extensions;
using SysEx.Net;
using System.Threading.Tasks;

namespace Skuld.Modules
{
    [Group, RequireNsfw]
    public class Lewd : SkuldBase<SkuldCommandContext>
    {
        public SysExClient SysExClient { get; set; }
        public BooruClient BooruClient { get; set; }
        public NekosLifeClient NekosLifeClient { get; set; }

        [Command("lewdneko"), Summary("Lewd Neko Grill"), Ratelimit(20, 1, Measure.Minutes)]
        public async Task LewdNeko()
        {
            var neko = await NekosLifeClient.GetAsync(NekoImageType.LewdNeko);
            if (neko != null)
                await ReplyAsync(Context.Channel, new EmbedBuilder { ImageUrl = neko }.Build());
            else
                await ReplyAsync(Context.Channel, "Hmmm <:Thunk:350673785923567616>, I got an empty response.");
        }

        [Command("lewdkitsune"), Summary("Lewd Kitsunemimi Grill"), Ratelimit(20, 1, Measure.Minutes)]
        public async Task LewdKitsune()
        {
            var kitsu = await SysExClient.GetLewdKitsuneAsync();
            StatsdClient.DogStatsd.Increment("web.get");
            await ReplyAsync(Context.Channel, new EmbedBuilder { ImageUrl = kitsu }.Build());
        }

        [Command("danbooru"), Summary("Gets stuff from danbooru"), Ratelimit(20, 1, Measure.Minutes)]
        [Alias("dan")]
        public async Task Danbooru(params string[] tags)
        {
            var post = await GetBooruPostAsync(BooruBoards.Danb, tags);

            await ReplyAsync(Context.Channel, post);
        }

        [Command("gelbooru"), Summary("Gets stuff from gelbooru"), Ratelimit(20, 1, Measure.Minutes)]
        [Alias("gel")]
        public async Task Gelbooru(params string[] tags)
        {
            var post = await GetBooruPostAsync(BooruBoards.Gelb, tags);

            await ReplyAsync(Context.Channel, post);
        }

        [Command("rule34"), Summary("Gets stuff from rule34"), Ratelimit(20, 1, Measure.Minutes)]
        [Alias("r34")]
        public async Task R34(params string[] tags)
        {
            var post = await GetBooruPostAsync(BooruBoards.Ru34, tags);

            await ReplyAsync(Context.Channel, post);
        }

        [Command("e621"), Summary("Gets stuff from e621"), Ratelimit(20, 1, Measure.Minutes)]
        public async Task E621(params string[] tags)
        {
            var post = await GetBooruPostAsync(BooruBoards.E621, tags);

            await ReplyAsync(Context.Channel, post);
        }

        [Command("konachan"), Summary("Gets stuff from konachan"), Ratelimit(20, 1, Measure.Minutes)]
        [Alias("kona", "kc")]
        public async Task KonaChan(params string[] tags)
        {
            if (tags.ContainsBlacklistedTags())
            {
                await ReplyAsync(Context.Channel, "Your tags contains a banned tag, please remove it.");
            }
            else
            {
                var cleantags = tags.AddBlacklistedTags();
                var posts = await BooruClient.GetKonaChanImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts != null)
                {
                    var post = posts.GetRandomImage();
                    if (post != null)
                    {
                        await ReplyAsync(Context.Channel, post.GetMessage(post.PostUrl));
                        return;
                    }
                }
                await ReplyAsync(Context.Channel, "Couldn't find an image");
            }
        }

        [Command("yandere"), Summary("Gets stuff from yandere"), Ratelimit(20, 1, Measure.Minutes)]
        public async Task Yandere(params string[] tags)
        {
            var post = await GetBooruPostAsync(BooruBoards.Yand, tags);

            await ReplyAsync(Context.Channel, post);
        }

        [Command("real"), Summary("Gets stuff from Realbooru"), Ratelimit(20, 1, Measure.Minutes)]
        public async Task Realbooru(params string[] tags)
        {
            var post = await GetBooruPostAsync(BooruBoards.Real, tags);

            await ReplyAsync(Context.Channel, post);
        }

        [Command("hentaibomb"), Summary("images from all hentai booru's"), Ratelimit(20, 1, Measure.Minutes)]
        public async Task HentaiBomb(params string[] tags)
        {
            if (tags.ContainsBlacklistedTags())
            {
                await ReplyAsync(Context.Channel, "Your tags contains a banned tag, please remove it.");
            }
            else
            {
                string msg = "";
                var cleantags = tags.AddBlacklistedTags();

                var posts = await BooruClient.GetYandereImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts != null)
                {
                    var post = posts.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl)+"\n";
                        return;
                    }
                }

                var posts2 = await BooruClient.GetKonaChanImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts2 != null)
                {
                    var post = posts2.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl) + "\n";
                        return;
                    }
                }

                var posts3 = await BooruClient.GetE621ImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts3 != null)
                {
                    var post = posts3.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl) + "\n";
                        return;
                    }
                }

                var posts4 = await BooruClient.GetRule34ImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts4 != null)
                {
                    var post = posts4.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl) + "\n";
                        return;
                    }
                }

                var posts5 = await BooruClient.GetGelbooruImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts5 != null)
                {
                    var post = posts5.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl) + "\n";
                        return;
                    }
                }

                var posts6 = await BooruClient.GetDanbooruImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts6 != null)
                {
                    var post = posts6.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl) + "\n";
                        return;
                    }
                }

                var posts7 = await BooruClient.GetRealBooruImagesAsync(cleantags);
                StatsdClient.DogStatsd.Increment("web.get");
                if (posts7 != null)
                {
                    var post = posts7.GetRandomImage();
                    if (post != null)
                    {
                        msg += post.GetMessage(post.PostUrl) + "\n";
                        return;
                    }
                }

                await ReplyAsync(msg);
            }
        }

        private async Task<string> GetBooruPostAsync(BooruBoards booru, string[] tags)
        {
            if (tags.ContainsBlacklistedTags()) return "Your tags contains a banned tag, please remove it.";

            var cleantags = tags.AddBlacklistedTags();

            dynamic posts = null;

            switch(booru)
            {
                case BooruBoards.Danb:
                    posts = await BooruClient.GetDanbooruImagesAsync(tags);
                    break;

                case BooruBoards.E621:
                    posts = await BooruClient.GetE621ImagesAsync(tags);
                    break;

                case BooruBoards.Gelb:
                    posts = await BooruClient.GetGelbooruImagesAsync(tags);
                    break;

                case BooruBoards.Kona:
                    posts = await BooruClient.GetKonaChanImagesAsync(tags);
                    break;

                case BooruBoards.Real:
                    posts = await BooruClient.GetRealBooruImagesAsync(tags);
                    break;

                case BooruBoards.Ru34:
                    posts = await BooruClient.GetRule34ImagesAsync(tags);
                    break;

                case BooruBoards.Yand:
                    posts = await BooruClient.GetYandereImagesAsync(tags);
                    break;
            }

            StatsdClient.DogStatsd.Increment("web.get");

            if (posts == null) return "Couldn't find an image.";

            var post = posts.GetRandomImage();

            return post.GetMessage(post.PostUrl);
        }
    }
}
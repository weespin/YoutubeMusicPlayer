using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CSCore.Codecs.AAC;
using CSCore.SoundOut;
using Wox.Plugin;
using YoutubeExplode;
using AudioEncoding = YoutubeExplode.Models.MediaStreams.AudioEncoding;

namespace YoutubeMusicPlayer {
    public class Main : IPlugin {
        PluginInitContext _context;
        private readonly Dictionary<string, Func<string, List<Result>>> _terms = new Dictionary<string, Func<string, List<Result>>>();

        public void Init ( PluginInitContext context ) {
            _context = context;
        }
        private YoutubeClient youtubeClient = new YoutubeClient();
        public static string Title;
        public static string Channel;
       
        public static WasapiOut OutPut { get; set; } = new WasapiOut();
        private List<Result> GetPlaying()
        {
           
            if (string.IsNullOrEmpty(Title))
            {
                return new List<Result>()
                {
                    SingleResult("No track playing").First(),
                };
            }
            var status = OutPut.PlaybackState == PlaybackState.Playing? "Now Playing" : "Paused";
            var toggleAction = OutPut.PlaybackState == PlaybackState.Playing  ? "Pause" : "Resume";
            return new List<Result>()
            {
                new Result()
                {
                    IcoPath ="icon.png",
                    Title = Title,
                    SubTitle = $"{status} | by {Channel}",
                },
                new Result()
                {
                   IcoPath ="icon.png",
                    Title = "Pause / Resume",
                    SubTitle = $"{toggleAction}: {Title}",
                    Action = _ =>
                    {
                        if ( OutPut.PlaybackState == PlaybackState.Playing)
                           OutPut.Pause();
                        else
                            OutPut.Play();
                        return true;
                    }
                },
                new Result()
                {
                    IcoPath ="icon.png",
                    Title = "Stop",
                    SubTitle = $"Stop: {Title}",
                    Action = context =>
                    {
                        OutPut.Stop();
                        return true;
                    }
                },
                ToggleMute().First(),
                new Result()
                {
                    IcoPath ="icon.png",
                    Title = "Change Volume",
                    SubTitle = $"ytm vol",
                },
            };
        }
        private List<Result> ToggleMute(string arg = null)
        {
            var toggleAction =OutPut.Volume==0f ? "Unmute" : "Mute";
            return SingleResult("Toggle Mute", $"{toggleAction}: {Title}", new Action(
                () =>
                {
                    if (OutPut.Volume == 0)
                    {
                        OutPut.Volume = 1;
                    }
                    else
                    {
                        OutPut.Volume = 0;
                    }
                }));
        }
        private List<Result> SingleResult(string title, string subtitle = "", Action action = default(Action)) =>
            new List<Result>()
            {
                new Result()
                {
                    Title = title,
                    SubTitle = subtitle,
                    IcoPath = "icon.png",
                    Action = _ =>
                    {
                        action();
                        return true;
                    }
                }
            };
        private async Task<string> DownloadImageAsync(string uniqueId, string url)
        {
            if (!Directory.Exists("Cache"))
                Directory.CreateDirectory("Cache");
            var path = $@"{"Cache"}\{uniqueId}.jpg";

            if (File.Exists(path))
            {
                return path;
            }

            using (var wc = new WebClient())
            {
                await wc.DownloadFileTaskAsync(new Uri(url), path);
            }

            return path;
        }
        public List<Result> Query ( Query query )
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return GetPlaying();
            }

            if (_terms.ContainsKey(query.FirstSearch))
            {
                return _terms[query.FirstSearch].Invoke(query.SecondToEndSearch);
            }
            var search = query.Search;
            if (query.FirstSearch == "vol")
            {
                if (query.SecondSearch != "")
                {
                    int vol;
                    if (
                        int.TryParse(query.SecondSearch, out vol))
                    {
                        if (vol >= 0 && vol <= 100)
                        {
                            return SingleResult("Set Volume to " + vol, "", () =>
                            {
                                var actualvol = vol / 100f;
                        
                                OutPut.Volume = actualvol;
                            });
                        }
                    }

                    return SingleResult("Incorrect volume", "From 0 to 100");
                }
                else
                {
                    return SingleResult("Type volume", "From 0 to 100");
                }
            }
                var results = youtubeClient.SearchVideosAsync(search,1).Result.Take(6);
                List<Result> result = new List<Result>();
                foreach (var item in results)
                {
                    result.Add(new Result()
                    {
                        Title = item.Title,
                        SubTitle = item.Author,
                        IcoPath = DownloadImageAsync(item.Id,item.Thumbnails.LowResUrl).Result,
                        Action = e =>
                        {
                                var streamInfoSet = youtubeClient.GetVideoMediaStreamInfosAsync(item.Id);
                                var streamInfo = streamInfoSet.Result.Audio.FirstOrDefault(n => n.AudioEncoding == AudioEncoding.Aac);
                                if (streamInfo == null)
                                {
                                    return false;
                                }

                                if (OutPut.PlaybackState == PlaybackState.Playing)
                                {
                                    OutPut.Stop();
                                }
                                OutPut.Initialize(new AacDecoder(streamInfo.Url));
                                OutPut.Play();
                                Title = item.Title;
                                Channel = item.Author;
                                return true;
                        }
                    });
                }
                return result;
        }
    }
}

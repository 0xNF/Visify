using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Visify.Areas.Identity.Data;
using Visify.Models;
using Visify.Services;

namespace Visify.Controllers
{
    
    [Authorize]
    public class GraphController : Controller
    {

        private readonly UserManager<VisifyUser> _userManager;
        private readonly ILogger<GraphController> logger;

        public GraphController(UserManager<VisifyUser> um, ILogger<GraphController> _logger) {
            this._userManager = um;
            this.logger = _logger;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(ArtistCollabGraph));
        }

        public IActionResult ArtistCollabGraph() {
            return View();
        }


        public IActionResult ArtistSongGroupGraph() {
            return View();
        }


        private class FDGNode {
            public readonly string id;
            public readonly int group;
            public readonly string label;
            public readonly int level;
            public int size;

            public FDGNode(string id, int group, string label, int level, int size) {
                this.id = id;
                this.group = group;
                this.label = label;
                this.level = level;
                this.size = size;
            }
        }

        private class FDGEdge {
            public readonly string source;
            public readonly string target;
            public double strength;
            public readonly List<Dictionary<string, string>> tracks = new List<Dictionary<string, string>>();

            public FDGEdge(string source, string target, double strength) {
                this.source = source;
                this.target = target;
                this.strength = strength;
            }
        }

        private class FDGCollabEdge : FDGEdge {

            public new readonly List<string> tracks = new List<string>();

            public FDGCollabEdge(string source, string target, double strength, List<string> tracks) : base(source, target, strength) {
                this.tracks = tracks;
            }
        }


        private class TDict {
            public readonly string name;
            public readonly string sid;
            public readonly string album;
            public readonly int size;

            public TDict(string name, string sid, string album, int size) {
                this.name = name;
                this.sid = sid;
                this.album = album;
                this.size = size;
            }
        }

        /// <summary>
        /// Artist Song Group Count api method
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Graph/artistsonglink")]
        public async Task<IActionResult> GetArtistLinks() {

            VisifyUser user = await _userManager.GetUserAsync(User);
            if (user == null) {
                logger.LogInformation($"Tried to get Artist Song Count graph data for user {_userManager.GetUserId(User)}, but user did not exist");
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get the tracks and artists
            VOption<IList<VisifySavedTrack>> stracks = await DatabaseService.GetUsersSavedTracks(user.Id, limit:10_000);
            if (!stracks.WasSuccess) {
                logger.LogError($"Failed to retreive saved tracks for user {_userManager.GetUserId(User)}", stracks.ErrorMessage);
                return Json("FAILED");
            }

            // create a proper d3 structure
            Dictionary<string, FDGNode> nodes = new Dictionary<string, FDGNode>();
            Dictionary<string, FDGEdge> edges = new Dictionary<string, FDGEdge>();

            foreach (VisifySavedTrack vst in stracks.Value) {
                string tid = $"tid_{vst.VisifyTrack.SpotifyId}";
                string[] idarr = new string[2];
                foreach (VisifyArtist va in vst.VisifyTrack.Artists) {
                    string sid = $"aid_{va.SpotifyId}";
                    idarr[0] = sid;
                    idarr[1] = tid;
                    Array.Sort(idarr);
                    string lid0 = String.Join("", idarr);
                    string lid1 = $"at_{lid0}";
                    if (!nodes.ContainsKey(sid)) {
                        nodes.Add(sid, new FDGNode(sid, 0, va.ArtistName, 0, 1));
                    }
                    else {
                        nodes[sid].size += 1;
                    }
                    nodes.Add(lid1, new FDGNode(lid1, 0, vst.VisifyTrack.TrackName, 1, 1));
                    if (!edges.ContainsKey(lid0)){
                        edges.Add(lid0, new FDGEdge(lid1, sid, 0.1));
                    }
                }
            }

            Dictionary<string, object> keys = new Dictionary<string, object>() {
                {"nodes", nodes.Values },
                {"links", edges.Values }
            };
            return Json(keys);
        }

        /// <summary>
        /// Artist Song Group Count api method
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Graph/artistcollablink")]
        public async Task<IActionResult> GetArtistCollabLinks() {

            VisifyUser user = await _userManager.GetUserAsync(User);
            if (user == null) {
                logger.LogInformation($"Tried to get Artist Collab Link graph data for user {_userManager.GetUserId(User)}, but user did not exist");
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get the tracks and artists
            VOption<IList<VisifySavedTrack>> stracks = await DatabaseService.GetUsersSavedTracks(user.Id, limit: 10_000);
            if (!stracks.WasSuccess) {
                logger.LogError($"Failed to retreive saved tracks for user {_userManager.GetUserId(User)}", stracks.ErrorMessage);
                return Json("FAILED");
            }

            // create a proper d3 structure
            Dictionary<string, FDGNode> nodes = new Dictionary<string, FDGNode>();
            Dictionary<string, FDGEdge> edges = new Dictionary<string, FDGEdge>();

            foreach (VisifySavedTrack vst in stracks.Value) {
                string tid = $"tid_{vst.VisifyTrack.SpotifyId}";
                string[] idarr = new string[2];
                Stack<string> artistsOnThisTrack = new Stack<string>(vst.VisifyTrack.Artists.Count);
                for (int i = 0; i < vst.VisifyTrack.Artists.Count; i++) {
                    VisifyArtist va = vst.VisifyTrack.Artists[i];
                    if (!nodes.ContainsKey(va.SpotifyId)) {
                        nodes.Add(va.SpotifyId, new FDGNode(va.SpotifyId, 0, va.ArtistName, 1, 1000));
                    }
                    artistsOnThisTrack.Push(va.SpotifyId);
                }

                while(artistsOnThisTrack.Count > 0) {
                    string a = artistsOnThisTrack.Pop();
                    foreach (string othera in artistsOnThisTrack) {
                        idarr[0] = a;
                        idarr[1] = othera;
                        Array.Sort(idarr);
                        string lid0 = String.Join("", idarr);
                        Dictionary<string, string> tn = new Dictionary<string, string>() {
                                {"name", vst.VisifyTrack.TrackName }
                            };
                        if (edges.ContainsKey(lid0)) {
                            edges[lid0].strength += 0.1;
                            edges[lid0].tracks.Add(tn);
                        }
                        else {
                            FDGEdge n = new FDGEdge(othera, a, 0.1);
                            n.tracks.Add(tn);
                            edges.Add(lid0, n);
                        }
                    }
                }
            }

            Dictionary<string, object> keys = new Dictionary<string, object>() {
                {"nodes", nodes.Values },
                {"links", edges.Values }
            };
            return Json(keys);
        }
    }
}